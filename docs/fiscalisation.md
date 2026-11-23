# ZIMRA fiscalisation reference (phase 2)

Spec §9 (rules F1–F14) says *what*. This file says *how*: the provider-neutral port, the processing pipeline, fiscal days, the fiscal block and the onboarding checklist.

> **Status of facts.** ZIMRA's FDMS rules below come from ZIMRA's public guidance and the Fiscal Device Gateway API spec v7.2. Provider behaviour (endpoints, auth, field names, limits, prices) **must be confirmed against the chosen provider's current docs and sandbox**. Mark anything unconfirmed `// VERIFY:`.

## 1. Background (ZIMRA FDMS)

- VAT-registered operators must fiscalise through ZIMRA's Fiscalisation Data Management System (FDMS). ZIMRA's guidance also says the Income Tax Act lets the Minister extend fiscalisation to all taxpayers, including those below the VAT threshold. Confirm what applies to each business with an accountant.
- Connection options: hardware fiscal devices, a self-built virtual fiscal device (direct API), or a third-party fiscalisation service. **We use a third-party provider.**
- Core FDMS concepts the provider exposes in some form:
  - **Device**: one per business here, registered to the taxpayer.
  - **Fiscal day**: opened before receipts and closed with counters. Its maximum length is set by ZIMRA config.
  - **Receipts**: FiscalInvoice, CreditNote and DebitNote. Each is hash-chained and signed.
  - **Fiscal data returned**: receipt ID, receipt global number, fiscal day number, verification code, and a QR URL on ZIMRA's domain.
- ZIMRA credit-note rules (mirrored in spec R13b):
  - must reference the original receipt and include a reason,
  - the original must be no more than 12 months old,
  - the total credited can't exceed the original,
  - tax lines must be a subset of the original's,
  - the currency must match.
- Currencies use ISO 4217 codes: `USD` and `ZWG`.

## 2. The port

```csharp
// Application/Fiscalisation
public interface IFiscalProvider
{
    string Name { get; }
    FiscalProviderCapabilities Capabilities { get; }     // RequiresHsCode, ManagesDaysAutomatically, MaxDayHours?, SupportsCreditNotes

    Task<Result<FiscalDeviceStatus>> GetStatusAsync(FiscalContext ctx, CancellationToken ct);  // day open?, day no, opened at, last error
    Task<Result<IReadOnlyList<ProviderTax>>> GetTaxesAsync(FiscalContext ctx, CancellationToken ct);
    Task<Result<FiscalDayResult>> OpenDayAsync(FiscalContext ctx, CancellationToken ct);
    Task<Result<FiscalDayResult>> CloseDayAsync(FiscalContext ctx, CancellationToken ct);
    Task<Result<FiscalReceipt>> SubmitAsync(FiscalContext ctx, FiscalDocument doc, CancellationToken ct);
}

public sealed record FiscalContext(Guid BusinessId, string DeviceId, FiscalEnvironment Environment, FiscalCredentials Credentials);
public sealed record FiscalDocument(
    FiscalDocumentKind Kind,            // Invoice, CreditNote
    Guid DocumentId, string Number, DateTimeOffset IssuedAt, string Currency /* USD|ZWG */,
    FiscalBuyer? Buyer,                 // name, TIN/VAT if known, address
    IReadOnlyList<FiscalLine> Lines,    // description, hsCode, qty, unitPrice, lineTotal, providerTaxId, taxPercent
    IReadOnlyList<FiscalTaxTotal> Taxes,
    IReadOnlyList<FiscalPayment> Payments, // method + amount; invoices on credit → "Credit" `// VERIFY: provider mapping`
    decimal Total,
    FiscalReference? Original,          // credit notes: original's provider receipt ID / global no / day no
    string? Reason);                    // credit notes
public sealed record FiscalReceipt(string ProviderReceiptId, long ReceiptGlobalNo, int FiscalDayNo,
                                   string DeviceId, string VerificationCode, string QrUrl, DateTimeOffset FiscalisedAt);
```

Error mapping: every adapter maps provider errors to `FiscalError.Transient` (retry) or `FiscalError.Validation(code, message)` (stop). Unknown errors count as Transient for the first three attempts, then as Validation.

Implementations:
- `FakeFiscalProvider`: deterministic receipts, plus switches to simulate a closed day, a validation error, timeouts and a slow day close. Development and tests only.
- `{Provider}FiscalProvider`: the first real adapter (prompt 32), in `Infrastructure/Fiscalisation/{Provider}/`, using a typed `HttpClient` with the standard resilience handler (no retries inside the handler for Submit; the pipeline owns retries).

## 3. Pipeline

```
IssueDocumentCommand (phase 1)
   └─ after commit: IFiscalisationService.QueueAsync(documentId)
        ├─ not required (F2) → FiscalStatus = NotRequired
        └─ required → FiscalStatus = Pending, enqueue FiscaliseDocumentJob on queue "fiscal-{deviceId}"
FiscaliseDocumentJob (one worker per device queue → serial, F9)
   1. load document snapshot + mappings; credit note? wait until original Fiscalised (re-enqueue in 1 min)
   2. ensure day open (§4)
   3. create FiscalSubmission(attempt n) → provider.SubmitAsync
   4. success → store FiscalData (immutable), FiscalStatus = Fiscalised, activity,
               enqueue RenderPdf(fiscal copy) → if SentBeforeFiscalisation → enqueue ResendFiscalCopy (F6)
   5. transient → schedule retry per F7 back-off; after 24 h → Failed
   6. validation → Failed, Needs-attention item + owner email (F7)
RetryFiscalisationCommand (F8) → new attempt from the same snapshot with current mappings
```

Serialisation: use a Hangfire queue per device with one worker, or a distributed lock (`sp_getapplock`) around steps 2–4 keyed by device. Pick the lock if per-device queues are awkward in Hangfire config.

Idempotency: providers usually return the original receipt for an identical resubmission (ZIMRA does). Still check `FiscalStatus` before submitting and store the provider receipt ID uniquely.

## 4. Fiscal days

- `EnsureDayOpenAsync(ctx)`: `GetStatusAsync`. If no day is open, call `OpenDayAsync` and record a `FiscalDay`. Skip this if the provider capability `ManagesDaysAutomatically` is true, and only record what the provider reports.
- `CloseFiscalDaysJob` runs every 15 minutes. For each enabled business with an open day, close it when local time ≥ 23:30 or open hours ≥ (MaxDayHours − 1). It doesn't close while a submission for that device is in flight (same lock as §3).
- Close failure → `FiscalDay.Status = CloseFailed`, owner alert, retry every 15 minutes, and a manual Close day button.
- Manual controls (Settings › Fiscalisation): Open day, Close day, Refresh status. Both buttons ask for confirmation and are logged.

## 5. Fiscal block (designer + renderer)

New `TemplateDefinition` element type `fiscalBlock` (schema v2; add a v1→v2 migration that changes nothing):

| Field | Meaning |
|---|---|
| `layout` | `stacked` (QR above text) or `inline` (QR left, text right) |
| `qrSizeMm` | 18–40, default 24 |
| `showDeviceId`, `showDayNo`, `showGlobalNo` | default true |
| `label` | default `[confirm required wording]` |
| style | text tokens as for `text` |

New bindings (whitelist additions): `fiscal.status`, `fiscal.verificationCode`, `fiscal.qrUrl`, `fiscal.receiptGlobalNo`, `fiscal.dayNo`, `fiscal.deviceId`, `fiscal.fiscalisedAt`.

Rendering:
- **NotRequired:** nothing (zero height, no gap).
- **Pending:** a dashed box the size of the QR, with "Fiscal verification pending" and the document number.
- **Fiscalised:** a QR code generated server-side with QRCoder from `fiscal.qrUrl` as an SVG data URI, plus the fields.
- **Failed:** like Pending. Failures are an internal state and never shown to clients.

Publishing rule (F11): templates whose document types include Invoice or CreditNote, used by any business with fiscalisation enabled, need exactly one `fiscalBlock` on the last page (`repeat: "last"`) to publish.

## 6. Settings and tax mapping

- **Sync taxes:** `GetTaxesAsync` → upsert `FiscalTax`. Each active `TaxRate` picks a `FiscalTax` from a dropdown (never typed). Include a mapping for "No tax"; its exempt or zero-rated choice is `[confirm with accountant]`.
- **Enable switch** stays disabled with a checklist until F3 is met:
  - credentials saved and status OK,
  - taxes synced and every rate mapped,
  - templates contain a fiscal block,
  - Applies-from set,
  - default HS code set, if the provider requires HS codes and any catalogue item lacks one.

## 7. Onboarding checklist (per business)

1. Accountant confirms the obligation and Applies-from date. `[ ]`
2. Provider account created for the business (its TIN/VAT number), device registered, sandbox credentials received. `[ ]`
3. Settings › Fiscalisation in **Sandbox**: credentials saved, status OK, taxes synced and mapped. `[ ]`
4. Sandbox runs, each producing a valid QR. Scan and verify on ZIMRA's verification page; sandbox QR codes point at ZIMRA's test domain. `[ ]`
   - USD invoice with tax,
   - USD invoice with no tax,
   - ZWG invoice,
   - partial credit note,
   - full credit note,
   - day close.
5. Samples sent to ZIMRA (or the provider) for approval, if required. `[ ]`
6. Live credentials entered, environment **Live**, Applies-from reached, Enabled on. `[ ]`
7. First live invoice verified by scanning its QR. `[ ]`

## 8. Tests

- Domain: FiscalStatus transitions. Fiscal data can't change once set. Credit notes wait for the original.
- Pipeline with `FakeFiscalProvider`:
  - success → fiscal copy rendered and re-sent only when sent before fiscalisation;
  - transient error → retries on schedule (FakeTimeProvider);
  - validation error → Failed + attention item, no retry;
  - Retry after a mapping fix → success;
  - two documents for one device are never submitted concurrently;
  - day auto-opens before the first submission;
  - close job respects in-flight submissions.
- Renderer: fiscal block in all four states; QR decodes to the stored URL (decode the SVG with a QR reader in tests, or compare the QRCoder payload).
- Publishing rule: a template without the block can't publish once fiscalisation is enabled.
- Adapter contract tests (prompt 32): run against the provider sandbox, opt-in via `FISCAL_SANDBOX=1`.

## 9. Troubleshooting

| Symptom | Check |
|---|---|
| Everything stuck in Pending | Device queue worker running? Lock held by a crashed job (expire after 2 min)? Provider status? |
| "Day not open" errors | Provider manages days itself? Capability flag right? Clock skew on the server? |
| Credit note Failed: original not found | Original fiscalised in the same environment (sandbox vs live)? Original reference fields mapped correctly? |
| Tax validation errors | Rate mapped to the right provider tax; percent matches; "No tax" mapping |
| QR scans "not found" | Sandbox QR checked on the live portal (or the reverse) |
