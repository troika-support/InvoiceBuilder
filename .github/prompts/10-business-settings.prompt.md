---
description: "Business profile, numbering, tax and currency, payment methods, messaging settings (S10)."
---

# 10 · Business settings

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screen S10 per design-system §8.10 (canvas 7).

## Do
1. Routes `/settings/profile`, `/settings/tax`, `/settings/payments`, `/settings/messaging`, `/settings/reminders`, `/settings/team` with the left section nav.
2. Profile & numbering: logo upload (PNG/SVG ≤ 1 MB, stored as Asset), trading name, registered name, email, WhatsApp number, address; numbering table with editable prefix, read-only next number, live example (R6). Prefix changes affect future numbers only.
3. Tax & currency: default currency, terms (R9), quote validity days (R7), tax rates list with effective-from and optional fiscal tax ID (R13), "Allow part payments" + minimum % (R20).
4. Payment methods: Paynow integration ID, key (write-only, "Saved ••••"), live/test switch, "Test connection" (initiates nothing; validates hash config), bank details text for manual transfers.
5. Messaging: email "from" name and reply-to; WhatsApp phone number ID and token (write-only).
6. Reminders: R30 settings with defaults.
7. ZIMRA fiscalisation card, disabled with the "Later" badge (R32).
8. "+ Add a business" creates a business with default sequences and the default theme.
9. Every save writes an activity entry (R33). Validation messages inline.

## Done when
I can change Troika's profile, prefixes and Paynow key; the key never comes back to the browser.
