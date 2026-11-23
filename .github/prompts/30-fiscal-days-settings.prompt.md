---
description: "Settings › Fiscalisation screen, tax sync and mapping, readiness checklist, day open/close automation and manual controls."
---

# 30 · Fiscalisation: settings, tax mapping and fiscal days

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screen F-S1 per design-system §8.14, spec F1, F3, F10 and `fiscalisation.md` §4 and §6.

## Do
1. `/settings/fiscal` with regions 1–6 of DS §8.14; Owner only; the phase 1 "Later" card in Tax & currency now links here.
2. Commands: `SaveFiscalConfig`, `TestFiscalConnection`, `SyncFiscalTaxes`, `MapTaxRate`, `EnableFiscalisation` (runs `FiscalReadiness`), `SwitchFiscalEnvironment` (confirm dialog copy from DS), `OpenFiscalDay`, `CloseFiscalDay`.
3. `CloseFiscalDaysJob` every 15 minutes per §4 (23:30 local or max hours − 1, respects the device lock), close-failure alerts and retries.
4. Health check `fiscal` (Degraded when any enabled business has a CloseFailed day or Failed documents older than 1 hour).
5. Every change logged in activity; credentials write-only.

## Tests first
Close job timing at 23:29 vs 23:30 and at max-hours − 1; job skips while the device lock is held; enabling fails with each missing checklist item.

## Done when
With the fake provider I can connect, sync and map taxes, enable, see the day open on first invoice and close on schedule or by button.
