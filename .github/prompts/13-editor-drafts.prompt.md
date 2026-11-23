---
description: "Draft quotes and invoices with line items, totals, payment plan and autosave (S3 part 1)."
---

# 13 · Document editor: drafts, lines, totals and instalment plans

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screen S3 regions 1–5 per design-system §8.3 (canvas 2), drafts only.

## Do
1. Commands: `CreateDraftDocument` (type, client), `UpdateDraftHeader`, `UpsertDraftLines` (full list with positions), `SetDiscount`, `DuplicateDocument`. All reject non-Draft (R5).
2. Editor page `/invoices/{id}` and `/quotes/{id}` with the 72 px rail, header (breadcrumb, "Draft invoice", status pill, "Saved HH:mm"), client card + picker, dates/terms/currency (quotes: Valid until), the line items table (drag handle, Enter adds a line, Alt+↑/↓ reorders, remove), catalogue search, totals list, payment-methods chips, notes.
3. Totals use `DocumentCalculator` (prompt 02) and count up under the motion rules.
4. Autosave 5 s after the last edit; on a concurrency conflict show "This draft changed elsewhere. Reload to see the latest." with Reload.
5. Required-field summary disables Issue (client, ≥ 1 line, due date / valid until).
6. Payment plan card per design-system §8.3 region 5 (canvas 10): `SetInstalmentPlan` / `ClearInstalmentPlan` commands (Draft only), generator (count, frequency, first due incl. "On acceptance"/"On issue"), editable rows, running check, Issue disabled until it matches (R34). Changing lines re-checks the plan and shows the difference.
7. Right column placeholder "Live preview" (filled in prompt 14).

## Tests first
Application tests for `UpsertDraftLines` validation (qty > 0, price ≥ 0, description required, ≤ 200 lines) and totals for the canvas sample (2,400.00); `SetInstalmentPlan` rejects plans that don't sum to the total and rejects "On acceptance" on invoices.

## Done when
I can build the INV-0142 sample as a draft and see $2,400.00.
