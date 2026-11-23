---
description: "Real provider adapter against its sandbox, contract tests, runbook and onboarding checklist."
---

# 32 · Fiscalisation: first provider adapter and go-live

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
A working `{Provider}FiscalProvider` for the provider I chose `[provider]`, verified in its sandbox (`fiscalisation.md` §2, §7).

## Before you start
Ask me for the provider's current API docs and sandbox credentials. Do not guess endpoints, auth or field names — every assumption gets `// VERIFY:`.

## Do
1. Adapter in `Infrastructure/Fiscalisation/{Provider}/`: typed `HttpClient` + standard resilience handler (no automatic retry on submit), auth as the provider requires, mapping to and from `FiscalDocument` / `FiscalReceipt`, error mapping to Transient / Validation, capabilities (`RequiresHsCode`, `ManagesDaysAutomatically`, `MaxDayHours`).
2. Contract tests (opt-in `FISCAL_SANDBOX=1`): status, tax sync, open day, USD invoice with tax, USD no tax, ZWG invoice, partial credit note, full credit note, close day — each asserting a QR URL and verification code come back.
3. Logging: request/response bodies stored in `FiscalSubmission` with credentials and personal data redacted; never in log files.
4. Update `docs/runbook.md`: fiscal onboarding (checklist §7), switching to Live, what to do when the provider is down, day-close failures.
5. Deploy script: add the provider credential setting names to the app-pool variable list if any are global (per-business ones stay in the database).

## Done when
All contract tests pass against the sandbox and the QR codes verify on ZIMRA's test verification page.
