---
description: "Quotes, invoices, receipts and credit notes lists with filters (S2)."
---

# 15 · Documents lists

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screen S2 per design-system §8.2.

## Do
1. `GetDocumentsQuery` with type, status (Overdue derived per R4), client, date range, currency, search on number/client, paging and sort.
2. Pages `/quotes`, `/invoices`, `/receipts` (receipts and credit notes in tabs).
3. Row click: Draft → editor; issued → read-only view.
4. Row actions per DS §8.2: Send again, Download PDF, Record payment (opens the drawer from prompt 20), Create credit note (prompt 20), Convert to invoice, Duplicate.
5. Export CSV of the filtered list.
6. Empty states per type.

## Done when
Lists filter and sort correctly; overdue invoices show "N days late".
