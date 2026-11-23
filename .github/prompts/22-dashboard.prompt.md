---
description: "Stat tiles, recent documents, needs attention and collected chart (S1)."
---

# 22 · Dashboard

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screen S1 per design-system §8.1 (canvas 1), figures per spec §4.1.

## Do
1. `GetDashboardQuery(currency)` returning outstanding, overdue (sum, count, oldest days; plan invoices contribute overdue instalments only, R38), instalments due in the next 7 days (count, amount), collected this month vs previous (%), quotes awaiting reply (count, value), recent documents (6), attention items, collected per month (6).
2. Header greeting by time of day, search (documents + clients quick results), USD/ZiG toggle (R12: separate figures), "New document" menu.
3. Tiles, recent documents with tabs, Needs attention actions (Send reminder, Record payment, Convert to invoice, Review recurring, overdue and upcoming instalments per design-system §8.1 region 4), chart with an aria-label.
4. Cache the query per business for 60 s; invalidate on Issue, Payment and Acceptance events.

## Tests first
Figures for a seeded set including ZiG and USD and a credit note: no cross-currency sums; % change with zero last month shows "—".

## Done when
The dashboard matches canvas 1 with seeded data.
