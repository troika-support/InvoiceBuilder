---
description: "Ledger, record payment drawer, receipts, credit notes and instalment rescheduling (S8)."
---

# 20 · Payments ledger, manual payments, receipts, credit notes and rescheduling

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screen S8 per design-system §8.8 (canvas 6); rules R15–R18 and credit notes (R4).

## Do
1. `/payments`: period select, method tiles (ZiG separate), ledger table with status pills, Export.
2. "Record a payment" drawer: client, amount, date, method (Cash, Bank transfer, Other), open-invoice checklist with remaining balances and auto-fill oldest first, preview line "INV-xxxx becomes Paid · Receipt RCT-xxxx", "Send receipt to client by WhatsApp".
3. `RecordManualPaymentCommand` → Confirmed payment + allocations + receipt (R18) + activity.
4. Receipt document view and PDF (receipt layout from the business's receipt template or the built-in).
4a. Allocation across a plan invoice is a single allocation to the invoice; instalment progress is derived (R37). The drawer shows "Covers instalment 2 and $200.00 of instalment 3" under the preview line.
4b. `RescheduleInstalmentsCommand` (Owner): dialog from design-system §8.3 (issued view), R36 rules, required reason, optional client notification (queued for prompt 21's sender), activity entry.
5. `IssueCreditNoteCommand` from an issued invoice: full or partial, required reason, rules R13b enforced with clear messages (e.g. "Credit notes can't be issued more than 12 months after the invoice."); full credit → Void (R4, R17).

## Tests first
Over-allocation rejected; allocation across two invoices; partial credit then payment → Paid at the right balance; payment of 1,000 on a 3 × 800 plan → #1 Paid, #2 Part-paid; reschedule of a paid instalment rejected.

## Done when
Recording $600 against INV-0137 from canvas 6 makes it Paid and creates RCT with both payments listed on the client timeline.
