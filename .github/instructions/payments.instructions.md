---
description: "Payments, Paynow and money rules"
applyTo: "src/InvoiceBuilder.Infrastructure/Payments/**,src/InvoiceBuilder.Application/Payments/**,src/InvoiceBuilder.Web/Controllers/Webhooks/**,src/InvoiceBuilder.Domain/Payments/**"
---

- Follow `docs/paynow.md`. Mark anything you couldn't verify against Paynow's current docs with `// VERIFY:`.
- Only the server confirms payments: verify the callback hash, then poll the status. Ignore browser return parameters.
- Confirmation is idempotent: check the Payment status first and rely on the unique (BusinessId, ProviderReference) index.
- Allocations follow R16; balance and status follow R17; one receipt per confirmed payment (R18).
- Never mix currencies (R11). A payment's currency equals the invoice currency.
- Read the integration key through `IBusinessSecretStore` (Data Protection). Never log it, return it to the UI or keep it in memory longer than the call.
- The webhook controller is anonymous, rate-limited and form-encoded only. It returns 200 for unknown references.
