---
description: "CSV exports and client statement PDF (spec §4.2)."
---

# 24 · Reports and exports

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Exports in spec §4.2.

## Do
1. CSV exports (UTF-8 with BOM, invariant numbers, ISO dates) for documents list, payments ledger.
2. Client statement for a date range: opening balance, documents, payments, closing balance, per currency; HTML through the renderer with a built-in statement template; PDF via `IPdfRenderer`; "Send statement" from the client panel.

## Done when
Statement for Harare Builders Co. reconciles with its invoices and payments.
