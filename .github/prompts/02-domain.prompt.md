---
description: "Entities, value objects, enums, Result type and pure rule services (incl. instalment plans), tests first."
---

# 02 · Domain model and rules (tests first)

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
The pure domain for spec §3 and §6, with rules proven by tests before implementation.

## Tests first
In `InvoiceBuilder.Domain.Tests`, write failing tests for:
- R10 line amount rounding: 3 × 33.335 = 100.01; 0.333 × 3.00 = 1.00 (AwayFromZero).
- R14 discount pro-rating: 10% on lines 100 and 50 → totals 135.00; fixed 15.00 on lines 100 and 50 → 10.00 and 5.00 allocated.
- R13 tax on discounted line amounts; tax snapshot kept after rate changes.
- R11 adding Money with different currencies returns a failure Result.
- R4/R5 state machine: Draft → Issued allowed; editing lines after issue fails with `Document.NotEditable`; Quote Accepted → Converted only once; Expired quote can't be accepted.
- R16 allocation rules: sum ≤ payment, each ≤ balance, same client and currency.
- R17 balance and status: total 2,400, confirmed 600 → PartPaid; 2,400 → Paid; credit note 2,400 → Void.
- R9 due date from terms (Net 14 from 23 Oct 2026 = 6 Nov 2026).
- R34 instalment plans: 3 × equal split of 2,400.00 → 800.00 each; 3 × split of 100.00 → 33.33, 33.33, 33.34; sum ≠ total → failure; dates not increasing → failure; 1 or 25 instalments → failure.
- R37 instalment states: total 2,400 in 3 × 800; paid 1,000 → #1 Paid, #2 Part-paid (200 left), #3 Upcoming; credit 300 → #3 reduced to 500 first; overdue derived from FakeTimeProvider date.
- R38 overdue amount = remaining of overdue instalments only.
- R36 reschedule: changing a paid instalment → failure; unpaid amounts must sum to the balance.
- R13b credit notes: reason required; currency must match the invoice; tax rate not on the invoice → failure; credits above the invoice total (across notes) → failure; issued 12 months + 1 day after the invoice → failure.

## Do
1. `Result`, `Result<T>`, `Error(code, message)` in `Domain/Common`.
2. Value objects: `Money`, `CurrencyCode` (ISO `USD` and `ZWG` only; "ZiG" is display text, R11), `HsCode` (optional, up to 8 digits), `DocumentNumber`, `TaxPercent`, `Discount`.
3. Aggregates per spec §6: `Business` (+ NumberSequence, TaxRate), `Client`, `CatalogueItem`, `Document` (+ DocumentLine, QuoteAcceptance), `Payment` (+ PaymentAllocation), `Template` (+ TemplateVersion), `Theme`, `RecurringSchedule`. Every entity gets the base fields from spec §6.
4. Enums: `DocumentType`, `DocumentStatus`, `PaymentMethod`, `PaymentStatus`, `Cadence`.
5. Pure services: `DocumentCalculator` (R10, R13, R14), `DocumentStateMachine` (R4, R5, R7, R8), `PaymentAllocator` (R16, R17), `InstalmentPlanner` (generate and validate, R34–R36) and `InstalmentStatusCalculator` (R37, R38). `Instalment` is an entity inside the `Document` aggregate.
6. Domain events (records) for Issued, Viewed, Accepted, PaymentConfirmed, used later by the activity log.

## Done when
All listed tests pass; Domain has no package references.
