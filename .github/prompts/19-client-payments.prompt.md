---
description: "Pay with EcoCash or card from C1 and the payment result page (C3)."
---

# 19 · Online payment from the client link

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Connect C1 to prompt 06 and build C3 per design-system §8.13 (canvas 9).

## Do
1. "Pay with EcoCash" opens a sheet: mobile number (prefilled, editable), amount (full balance; part allowed per R20 with min), method choice EcoCash / OneMoney / InnBucks → `StartOnlinePaymentCommand` (mobile). "Pay by card" → web initiation and redirect.
1a. Invoices with a plan (R40): choices "Pay next instalment · {amount}" (default; overdue + due remaining, or the next upcoming), "Pay full balance", "Other amount" (≥ R20 minimum). Part payments are always allowed on plan invoices.
2. Waiting state: "Approve the payment on your phone" with polite live region; poll `GET /d/{token}/payments/{paymentId}` every 5 s up to 2 min.
3. `/d/{token}/paid` shows Pending / Confirmed / Failed from our database only (paynow.md §3 step 7) with the C3 copy.
4. Paid invoices: hide pay buttons, show "Paid in full" and receipt links.
5. Hide online buttons when the business has no Paynow config or the currency isn't supported (paynow.md §6).

## Done when
With the fake gateway, a full and a part payment both end on C3 and the invoice shows Part-paid then Paid.
