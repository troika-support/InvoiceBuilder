---
description: "ZIMRA fiscalisation rules (phase 2)"
applyTo: "src/InvoiceBuilder.Domain/Fiscalisation/**,src/InvoiceBuilder.Application/Fiscalisation/**,src/InvoiceBuilder.Infrastructure/Fiscalisation/**,src/InvoiceBuilder.Web/Components/Pages/Settings/Fiscal*"
---

- Follow spec §9 (F1–F14) and `docs/fiscalisation.md`. Provider details you can't confirm from the provider's docs get `// VERIFY:`.
- Domain and Application code talks only to `IFiscalProvider`. No provider types leak out of `Infrastructure/Fiscalisation/{Provider}/`.
- Issued documents stay immutable (R5). Fiscal data is written once and never changed. Retries resubmit from the issue snapshot.
- Submissions for one device run one at a time, in issue order. Credit notes wait for their invoice.
- Classify every provider error as Transient or Validation. Only Transient retries, on the F7 schedule.
- Currency codes go to the provider as ISO `USD` / `ZWG`.
- Never log or store provider credentials in `FiscalSubmission`, logs or exceptions. Redact buyer personal data in stored request bodies.
- Sandbox and Live are separate. Never send sandbox data to Live or the reverse; the environment comes from `FiscalConfig` only.
