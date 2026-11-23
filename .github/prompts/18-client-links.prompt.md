---
description: "Public document view, payment plan display, quote accept/decline and convert to invoice (C1, C2)."
---

# 18 · Client links: view and quote acceptance

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screens C1 and C2 per design-system §8.11–§8.12 (canvas 4 and 8), rules R8, R26, R27.

## Do
1. Anonymous route `/d/{token}` (rate-limited, no auth cookie needed): resolve the token hash → document; 404 page "This link isn't valid anymore" for unknown/expired.
2. Business-themed layout (logo, accent rule) using Theme tokens, phone-first, max-width 480.
3. Invoice view: amount due, due pill, pay buttons (wired in prompt 19), items, Save PDF, Ask a question (WhatsApp deep link to the business number).
4. Quote view: items, terms link, payment plan card when present, Accept form (full name, checkbox), "Accept quote" or "Accept & pay instalment 1 · {amount}" when instalment 1 is due on acceptance (R35), "Request changes" with comment; expired message.
5. `RecordViewCommand` (R27), `AcceptQuoteCommand` / `DeclineQuoteCommand` (R8: name, UTC time, IP, user agent).
6. Owner side: "Needs attention" item and email (email wired in prompt 21); `ConvertQuoteToInvoiceCommand` creating a linked Draft invoice (once) and copying the plan.
7. R35: accepting a quote whose instalment 1 is due on acceptance converts and issues the invoice in the same command (number, snapshots, fiscal hook as prompt 14) and redirects the client to that invoice's link to pay instalment 1.
8. Invoice view with a plan: header pill "Next: {amount} due {date}" / "Instalment overdue", payment plan card with per-instalment StatusPill and receipt links (design-system §8.11).

## Tests first
Token lookup uses the hash; accepted quote can't be accepted again; expired quote rejects acceptance; convert twice fails; R35 acceptance creates exactly one issued invoice with the plan copied and "On acceptance" resolved to the acceptance date.

## Done when
Opening a link marks Viewed once; accepting records the signer and shows in the client's timeline.
