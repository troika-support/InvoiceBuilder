---
description: "IEmailSender via provider API, WhatsApp Cloud API, send dialog and outbound log (R28)."
---

# 21 · Email and WhatsApp sending

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Deliver documents and receipts by email and WhatsApp (spec §2, R28, §4.3).

## Do
1. `IEmailSender` (Application) with `ProviderApiEmailSender` (Infrastructure) for `[email provider]` behind config `Email:Provider`, `Email:ApiKey` (secret), `Email:From`. Ask me which provider before writing the adapter; keep a `LogOnlyEmailSender` for Development.
2. `IWhatsAppSender` using WhatsApp Business Cloud API (typed `HttpClient` + resilience): template messages `document_link` and `payment_receipt` with parameters (client name, document number, amount, link). Template names configurable per business. Mark API details `// VERIFY:`.
3. `SendDocumentCommand` (channels, recipients, message) → `OutboundMessage` rows → Hangfire jobs per channel → status updates; Sent status + activity on success.
4. Send dialog from "Issue & send" and "Send again": channel checkboxes, recipient fields prefilled, message preview, attach PDF (email).
5. Email templates (HTML + text) styled with the business theme: document sent, receipt, reminder, instalment reminder ("Instalment 2 of 3, $800.00, due 1 Dec 2026"), plan rescheduled, quote accepted (to owner), recurring drafts ready (to owner).
6. WhatsApp delivery status webhook (`POST /webhooks/whatsapp`) updating `OutboundMessage` (verify signature).

## Done when
In Development, sending logs both channels; with real keys, an email arrives with the PDF and a working link.
