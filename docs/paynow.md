# Paynow integration reference

Clients pay invoices, including individual instalments, from the client link (spec R15–R20, R35, R40). A quote "deposit" is instalment 1 of the invoice created on acceptance. Each business has its own Paynow integration. This is not subscription billing: a confirmed payment creates a Payment, its allocations and a Receipt.

> **Checked against developers.paynow.co.zw on 23 Nov 2026** (Initiate a transaction, Express Checkout, Status Update, Generating/Validating Hash, Test Mode). See §10 for what was confirmed, what changed in the code, and what is still open.

## 1. Settings (per business and currency, `PaymentGatewayConfig`)

Paynow issues a **separate integration ID and key for each currency**. A business has up to two Paynow rows, one for USD and one for ZiG (unique on BusinessId, Provider, Currency), each with its own test/live switch. Settings → Payment methods has a section for each.

| Setting | Where | Notes |
|---|---|---|
| Currency | DB | `USD` or `ZWG`. Rows saved before the split became USD (migration `PaymentGatewayCurrency`) |
| IntegrationId | DB | Shown in settings |
| IntegrationKey | DB, encrypted with Data Protection purpose `Paynow.IntegrationKey` | Write-only in UI ("Saved ••••") |
| ResultUrl | Computed: `https://{host}/webhooks/paynow/{businessId}/{usd\|zwg}` | Public HTTPS. The currency picks the key that checks the hash. The old `/webhooks/paynow/{businessId}` still works and means USD, for payments started before the split |
| ReturnUrl | Computed: `https://{host}/d/{token}/paid` | Where Paynow sends the browser back |
| IsLive | DB | Off = test mode (Paynow test merchant behaviour) |

Host comes from `App:PublicBaseUrl` option (`ValidateOnStart`).

## 2. Ports (Application)

```csharp
public interface IPaymentGateway
{
    Task<Result<PaymentInitiation>> InitiateWebAsync(PaymentRequest request, CancellationToken ct);
    Task<Result<PaymentInitiation>> InitiateMobileAsync(PaymentRequest request, string phone, MobileMethod method, CancellationToken ct);
    Task<Result<GatewayStatus>> CheckStatusAsync(Guid businessId, CurrencyCode currency, string pollUrl, CancellationToken ct);
    bool VerifyCallback(Guid businessId, CurrencyCode currency, IReadOnlyDictionary<string, string> fields);
}
public sealed record PaymentRequest(Guid BusinessId, Guid PaymentId, string Reference, decimal Amount, string Currency, string Description, string PayerEmail);
public sealed record PaymentInitiation(string? RedirectUrl, string PollUrl, string? Instructions);
public sealed record GatewayStatus(string Reference, string? ProviderReference, decimal Amount, GatewayState State);
public enum GatewayState { Pending, Paid, Cancelled, Failed }
public enum MobileMethod { EcoCash, OneMoney, InnBucks }
```

Implementation: `Infrastructure/Payments/Paynow/PaynowGateway.cs`, using the official package where it fits. Otherwise use a typed `HttpClient` with the standard resilience handler.

## 3. Flow

1. Client taps "Pay with EcoCash" on `/d/{token}`. The page asks for the mobile number (prefilled from the client record) and the amount: full balance by default, or part if allowed (R20).
2. `StartOnlinePaymentCommand` creates `Payment { Status = Pending, Method = EcoCash, Reference = "{InvoiceNo}-{shortId}" }` plus a pending allocation to the invoice. It then calls `InitiateMobileAsync` and stores `PollUrl` and the Paynow reference when returned.
3. UI shows "Approve the payment on your phone" with a polite live region and polls our own endpoint `GET /d/{token}/payments/{paymentId}` every 5 s (max 2 min).
4. Paynow POSTs to `ResultUrl` (form-encoded). `PaynowWebhookController`:
   - reads the form, loads the business config, `VerifyCallback` (hash) → 400 if bad,
   - finds the Payment by reference (and business) → 200 and ignore if unknown,
   - **calls `CheckStatusAsync(pollUrl)`** (authoritative),
   - dispatches `ConfirmOnlinePaymentCommand` with the status.
5. `ConfirmOnlinePaymentCommand` is idempotent:
   - Payment already Confirmed or Failed → no-op.
   - Paid → amount must equal the requested amount (else flag for review and stop). Status becomes Confirmed, allocations are applied, the invoice balance and status are updated (R17), a Receipt is issued (R18), an activity entry is added and receipt sending is queued.
   - Cancelled or Failed → Failed, activity entry.
6. Hangfire job `PollPendingPaymentsJob` every 2 minutes: Pending payments older than 1 minute and younger than 24 hours → `CheckStatusAsync` → same confirm command. Older than 24 hours → Failed ("No confirmation from Paynow").
7. ReturnUrl page `/d/{token}/paid` shows Pending, Confirmed or Failed from **our** database, never from query-string values.

## 4. Hash

Concatenate the URL-decoded values of all fields except `hash`, in the order they appear in the message, with no separators, then append the integration key. UTF-8, SHA-512, uppercase hex. Compare in constant time (`CryptographicOperations.FixedTimeEquals`). The docs' worked example (id 1201, key `3e9fed89-…`) is a unit test. Because order matters, the webhook reads the raw request body rather than `Request.Form`.

## 5. Status strings → GatewayState

| Paynow status (case-insensitive) | State |
|---|---|
| `Paid`, `Awaiting Delivery`, `Delivered` | Paid |
| `Created`, `Sent` | Pending |
| `Cancelled` | Cancelled |
| `Failed`, `Disputed`, `Refunded` | Failed (flag Disputed/Refunded for review) |

This is Paynow's complete documented list. `Paid` = settled at next settlement; `Awaiting Delivery` / `Delivered` = paid, funds held in suspense; `Cancelled` can't be resumed; `Disputed` holds funds; `Refunded` returned them. Paynow resends a status update up to 10 times if our endpoint returns an HTTP error.

## 6. Currency

A Payment's currency equals the invoice currency, and **the invoice currency picks the Paynow integration** (confirmed by the owner: each currency needs its own integration ID and key). No request field carries a currency; Paynow charges in the currency of the integration, so a ZiG invoice is always sent through the ZiG integration and never the USD one.

- Starting a payment, the status poll and the poll job all use the integration for the payment's currency.
- The webhook uses the currency in its URL, because only that integration's key can verify the hash.
- If the business has no integration for the invoice's currency, the client link shows no pay button (bank details only) and starting a payment fails with "Online payment isn't available for this invoice."

USD and ZWG are the only currencies the app supports (R11).

## 7. Security

- The webhook endpoint is anonymous, rate-limited (60/min per IP) and accepts only `application/x-www-form-urlencoded`.
- The integration key never leaves the server and never appears in logs. Log the reference, status and the first 6 characters of the hash only.
- No entitlement or status change on the browser return. The server confirms only (callback + poll).

## 8. Tests

- Hash: known vector (build one with the official package or the docs example), tampered field → false, missing hash → false.
- Status mapping for every string in §5.
- Confirm command: Paid → payment Confirmed, receipt created, invoice Paid; duplicate Paid → one receipt only; amount mismatch → not confirmed, flagged; Cancelled → Failed.
- Webhook controller: bad hash → 400; unknown reference → 200 with no changes; a ZiG callback is verified and polled with the ZiG integration; unknown currency in the URL → 400; the old URL uses USD.
- Settings: USD and ZiG save as separate rows with their own keys; a missing key names the currency.
- `FakePaymentGateway` (prompt 07) drives all of this in development: a dev-only page simulates Paid, Cancelled and a delayed confirmation.

## 9. Troubleshooting

| Symptom | Check |
|---|---|
| No callback arrives | ResultUrl is public HTTPS and reachable; IIS doesn't block POST; the poll job still confirms |
| Hash mismatch | Field order, URL-decoding, trimmed values, the right business's key for the payment's currency (USD and ZiG keys differ) |
| Stuck "Waiting for Paynow" | Poll job running in Hangfire? PollUrl stored? |
| Paid twice in records | The idempotency check on Payment status plus the unique (BusinessId, ProviderReference) index |

## 10. Checked against Paynow's docs (23 Nov 2026)

**Confirmed as built:** endpoints `https://www.paynow.co.zw/interface/initiatetransaction` (web) and `/interface/remotetransaction` (express checkout); fields `id, reference, amount (2 dp, no symbol), additionalinfo, returnurl, resulturl, authemail, status=Message, hash`, plus `phone` and `method` for mobile money; response `status=Ok`, `browserurl`, `pollurl`, `hash` (verified before use) or `status=Error` + `error`; status update fields `reference, amount, paynowreference, pollurl, status, hash`; the status list in §5; the hash rule in §4.

**Changed after the check:**
- **InnBucks** has no phone prompt. Paynow returns `authorizationcode` and `authorizationexpires`; the client enters the code in the InnBucks app or opens `com.innbucks.customer://purchase?paymentToken={code}`. The payment page now shows the code, its expiry and an "Open InnBucks" button, then keeps checking.
- **Mobile money `instructions`** returned by Paynow are now shown to the client.
- **Test mode:** `authemail` must be the merchant's Paynow login email or test payments can't complete. In test mode the app sends the **business email**, so set the business email in Settings to the email you log in to Paynow with. Live: express checkout sends the client's email (business email if the client has none); web checkout sends only the client's own email, because a registered address makes Paynow ask the payer to log in.
- **`merchanttrace`** = the payment id (32 chars), so a resent request is recognised.
- **No automatic HTTP retries of POSTs** to Paynow (or SendGrid/WhatsApp): a retry after a timeout could start a second payment prompt. The poll job and webhook re-checks are the safe retry.

**Test numbers (EcoCash/OneMoney, test mode):** 0771111111 success after 5 s · 0772222222 success after 30 s · 0773333333 cancelled after 30 s · 0774444444 "insufficient balance" at once. Only the merchant account can fake a web payment ("[TESTING: Faked Success]"). Go live with "Request to be Set Live" after one successful test.

**Still open — ask Paynow:**
1. **InnBucks in test mode** isn't documented.
2. **Polling** isn't on a docs page; the app POSTs an empty body to `pollurl`, as Paynow's own SDKs do.
3. **O'mari** (OTP flow) isn't supported by the app yet.

**Resolved:** currency. Each currency needs its own integration ID and key (confirmed by the owner, 23 Nov 2026). The app now stores one integration per currency; see §1 and §6.
