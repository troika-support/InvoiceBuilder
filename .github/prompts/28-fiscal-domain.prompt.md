---
description: "FiscalStatus, fiscal entities, IFiscalProvider port and FakeFiscalProvider, tests first."
---

# 28 · Fiscalisation: domain, port and fake provider

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
The fiscal model and provider port from spec §9.1–§9.2 and `docs/fiscalisation.md` §2.

## Tests first
- `FiscalStatus` transitions: NotRequired is final; Pending → Fiscalised | Failed; Failed → Pending only via retry; fiscal data can't change once set (F6).
- F2 applicability: invoice before Applies-from → NotRequired; quote and payment receipt → NotRequired; credit note on a business with fiscalisation enabled → required.
- F3 enabling checklist: each missing item blocks enabling with its own error code.
- F12 HS code resolution order: line snapshot → catalogue item → business default → error.

## Do
1. Domain: `FiscalConfig`, `FiscalTax`, `FiscalDay`, `FiscalSubmission`, the new `Document` fields (`FiscalStatus`, `FiscalData` as an immutable value object, `SentBeforeFiscalisation`), `FiscalApplicability` and `FiscalReadiness` services.
2. Persistence: configurations, indexes from spec §9.2, migration `Fiscalisation` + idempotent SQL script; credentials encrypted with Data Protection purpose `Fiscal.Credentials`.
3. Application: `IFiscalProvider` and records exactly as `fiscalisation.md` §2, `FiscalError` (Transient / Validation), `IFiscalProviderFactory` (resolves by `FiscalConfig.Provider`).
4. `FakeFiscalProvider` (Development/tests only; startup fails if configured in Production) with switches for closed day, validation error, timeout, slow close; deterministic receipt numbers and a QR URL on a fake domain.

## Done when
Tests pass; migration applies; the fake provider is resolvable in Development.
