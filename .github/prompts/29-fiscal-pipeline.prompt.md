---
description: "Queue, serial per-device processing, retries, fiscal copy re-render and re-send, retry command, alerts."
---

# 29 · Fiscalisation: submission pipeline

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
`docs/fiscalisation.md` §3 with spec rules F4–F9.

## Tests first (FakeFiscalProvider + FakeTimeProvider)
- Success → Fiscalised, fiscal PDF rendered; re-sent only when `SentBeforeFiscalisation` (F6).
- Transient error → retries at 1, 5, 15, 60 min then hourly; Failed after 24 h (F7).
- Validation error → Failed, Needs-attention item, owner email, no retries (F7).
- `RetryFiscalisationCommand` after fixing a tax mapping → Fiscalised (F8).
- Credit note waits until its invoice is Fiscalised and carries the original fiscal reference (F9).
- Two documents for the same device never submit concurrently (F9).

## Do
1. `FiscalisationService : IFiscalisationService` replacing the no-op: decides applicability, sets Pending, enqueues `FiscaliseDocumentJob`.
2. `FiscaliseDocumentJob` per §3 steps 1–6 with the per-device lock (`sp_getapplock`, 2-minute timeout) or a per-device Hangfire queue, and `EnsureDayOpenAsync` from §4.
3. Map document snapshot → `FiscalDocument` (ISO currency, lines with HS code and provider tax ID, tax totals, payments, original reference, reason).
4. Fiscal copy: re-render PDF with the fiscalised block; if sent before, `ResendFiscalCopyJob` re-sends on the original channels with the copy note from F6; mark activity.
5. Sending before fiscalisation sets `SentBeforeFiscalisation = true` (prompt 21's send command).
6. Needs-attention items and owner email for failures; dashboard counts.

## Done when
With the fake provider, issuing and sending an invoice produces a pending copy, then a fiscal copy re-sent automatically; failures surface on the dashboard.
