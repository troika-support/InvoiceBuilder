---
description: "IPaymentGateway for Paynow, hash verification, status mapping, webhook controller and confirm command."
---

# 06 · Paynow gateway and webhook

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Server-side payment confirmation per `docs/paynow.md` (spec R15–R20).

## Tests first
- Hash: known vector, tampered field → false, missing hash → false (§4).
- Status mapping for each row of §5.
- `ConfirmOnlinePaymentCommand`: Paid → Confirmed + receipt + invoice Paid; duplicate Paid → one receipt; amount mismatch → not confirmed + flagged; Cancelled → Failed (§8).
- Webhook: bad hash → 400; unknown reference → 200, nothing changes.

## Do
1. Application: `IPaymentGateway` and records exactly as `paynow.md` §2; `StartOnlinePaymentCommand`, `ConfirmOnlinePaymentCommand`, `IBusinessSecretStore`.
2. Infrastructure/Payments/Paynow: `PaynowGateway` (official package if it fits, else typed `HttpClient` + `AddStandardResilienceHandler`), `PaynowHash`, `PaynowStatusMapper`. Mark unverified details `// VERIFY:`.
3. `DataProtectionBusinessSecretStore` (purpose `Paynow.IntegrationKey`).
4. `Web/Controllers/Webhooks/PaynowWebhookController` at `POST /webhooks/paynow/{businessId}` (§3 step 4, §7).
5. Receipt issuing (R18) inside the confirm command, using the R6 number allocation.

## Done when
Tests pass; the webhook is reachable anonymously and rate-limited.
