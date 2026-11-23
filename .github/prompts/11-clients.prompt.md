---
description: "Clients list and detail with activity timeline (S6)."
---

# 11 · Clients

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screen S6 per design-system §8.6 (canvas 5).

## Do
1. Queries: `GetClientsQuery` (search, filter All / With balance due / Overdue, paging), `GetClientDetailQuery` (balance per currency, avg days to pay, contacts, last 20 activity entries). Commands: Create, Update, Archive client.
2. List with initials avatar, last document, balance, billed this year; selected row style per DS.
3. Detail panel with tiles, contacts, actions (New quote, New invoice, Send statement — statement wired in prompt 24), timeline (`MudTimeline`, dot + text).
4. New/Edit client drawer: name, email, phone, WhatsApp, address, default currency, notes. Duplicate name in a business → inline error.
5. CSV import: name, email, phone, address columns; preview with errors before saving.
6. Empty state: "No clients yet" + "Add your first client".

## Tests first
Avg days to pay calculation (paid invoices only); balance per currency never summed across currencies (R12).

## Done when
Seeded clients show; create, edit and import work.
