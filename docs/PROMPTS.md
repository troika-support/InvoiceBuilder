# Prompt index

Run in order. One prompt per fresh Copilot Chat (Agent mode). Commit after each.

| # | Slash command | Builds | You check | Hours |
|---|---|---|---|---|
| 00 | `/00-plan` | Read the kit and reply with a plan and questions. No code. | The structure matches the stack list; no packages appear that the spec doesn't name; answer its questions in the chat. | 1 |
| 01 | `/01-solution-setup` | Create the solution, projects, central package management and DI entry points. | Central package file has only the listed packages; MediatR version decision recorded; app runs. | 3 |
| 02 | `/02-domain` | Entities, value objects, enums, Result type and pure rule services (incl. instalment plans), tests first. | Read the test names against R-numbers; check the rounding examples by hand. | 10 |
| 03 | `/03-persistence` | DbContext, configurations, unique indexes, repositories, unit of work, migration and seed data. | Open the generated SQL script: unique indexes and filtered indexes present; decimal precision right. | 6 |
| 04 | `/04-application-core` | MediatR pipeline, validation, clock, audit log, formatting helpers, options. | Formatter output matches the design-system table character for character. | 4 |
| 05 | `/05-render-pdf-spike` | TemplateDefinition, HTML renderer and Playwright PDF with the 60-line pagination test. | Open /dev/render-sample?lines=60: header row on each page, totals after the last row, footer on each page, fonts correct. | 10 |
| 06 | `/06-paynow-gateway` | IPaymentGateway for Paynow, hash verification, status mapping, webhook controller and confirm command. | Every // VERIFY: is listed in the summary; check them against Paynow's docs before going live. | 8 |
| 07 | `/07-fake-gateway` | Development-only fake gateway and a simulator page to drive payments end to end. | Turn UseFake on in Production config locally: the app refuses to start. | 3 |
| 08 | `/08-auth-roles` | ASP.NET Identity, 2FA for Owner, roles, business access policies, sign-in pages. | Sign out and back in with a wrong code 5 times: lockout message shows; recovery codes work once each. | 6 |
| 09 | `/09-ui-shell` | Install the theme kit, layout, sidebar, business switcher, shared components and error pages. | Tab through the shell: visible focus everywhere; switch businesses; toggle dark mode; check contrast on a few pairs with a browser tool. | 6 |
| 10 | `/10-business-settings` | Business profile, numbering, tax and currency, payment methods, messaging settings (S10). | View page source/network after saving the Paynow key: it never appears; activity entries exist. | 6 |
| 11 | `/11-clients` | Clients list and detail with activity timeline (S6). | Import a CSV with a duplicate and a bad email: preview flags both; nothing saved until confirmed. | 5 |
| 12 | `/12-catalogue` | Catalogue items and tax rate picker (S7). | Archive an item used on a draft: the draft keeps its line. | 3 |
| 13 | `/13-editor-drafts` | Draft quotes and invoices with line items, totals, payment plan and autosave (S3 part 1). | Keyboard only: add three lines, reorder one, remove one; totals update; screen reader reads the total. | 10 |
| 14 | `/14-issue-preview-pdf` | Issue documents with gap-free numbers, live preview iframe and PDF download (S3 part 2). | Change the client's email after issuing: the PDF and link still show the old snapshot. | 6 |
| 15 | `/15-document-lists` | Quotes, invoices, receipts and credit notes lists with filters (S2). | Filter Overdue with a ZiG and a USD invoice: amounts stay in their own currencies. | 4 |
| 16 | `/16-designer-canvas` | Fabric.js canvas, insert panel, layers and properties panel (S4 part 1). | Compare Test PDF to the canvas: positions within 1 mm, fonts and colours right. | 12 |
| 17 | `/17-templates-themes` | Templates list, themes, versioning and publish (S4 part 2, S5). | Open an invoice issued on v1 after publishing v2: still v1. | 5 |
| 18 | `/18-client-links` | Public document view, payment plan display, quote accept/decline and convert to invoice (C1, C2). | Open the link in a private window: no app chrome, business theme only; keyboard and screen reader can accept. | 7 |
| 19 | `/19-client-payments` | Pay with EcoCash or card from C1 and the payment result page (C3). | Refresh /paid with ?status=Paid added to the URL: the page still shows our real status. | 5 |
| 20 | `/20-payments-ledger` | Ledger, record payment drawer, receipts, credit notes and instalment rescheduling (S8). | Receipt PDF shows the allocation lines and currency; credit note numbering uses CRN-. | 8 |
| 21 | `/21-messaging` | IEmailSender via provider API, WhatsApp Cloud API, send dialog and outbound log (R28). | Message preview matches the copy; no secrets in logs; failed sends show the error on the document. | 6 |
| 22 | `/22-dashboard` | Stat tiles, recent documents, needs attention and collected chart (S1). | Switch USD/ZiG: every tile changes; nothing sums both. | 5 |
| 23 | `/23-background-jobs` | Hangfire setup, recurring schedules (S9), reminders (incl. per instalment), quote expiry, payment polling. | Trigger RemindersJob twice manually: one reminder per invoice per day. | 7 |
| 24 | `/24-reports-exports` | CSV exports and client statement PDF (spec §4.2). | Open the CSV in Excel: amounts are numbers, dates sort correctly. | 3 |
| 25 | `/25-hardening` | Security, privacy, rate limits, health checks and accessibility pass as a checklist. | Read the pass/fail list; run a header scanner on the staging URL. | 5 |
| 26 | `/26-integration-tests` | End-to-end flows against SQL Server containers and the fake gateway. | Break a rule on purpose (e.g. comment out the number lock): the right test fails. | 5 |
| 27 | `/27-deploy` | Publish profile, server setup script, runbook and smoke test. | Follow the runbook on a fresh VM without help; note every step that needed guessing. | 5 |
| | **Phase 2 — ZIMRA fiscalisation (spec §9)** | | | |
| 28 | `/28-fiscal-domain` | FiscalStatus, fiscal entities, IFiscalProvider port and FakeFiscalProvider, tests first. | Read the error codes for the checklist; confirm fiscal data has no setters after creation. | 6 |
| 29 | `/29-fiscal-pipeline` | Queue, serial per-device processing, retries, fiscal copy re-render and re-send, retry command, alerts. | Use the fake provider's switches to force each failure; check the activity timeline tells the story in order. | 8 |
| 30 | `/30-fiscal-days-settings` | Settings › Fiscalisation screen, tax sync and mapping, readiness checklist, day open/close automation and manual controls. | Switch to Live: confirm dialog wording; credentials never reach the browser. | 6 |
| 31 | `/31-fiscal-block` | Designer element, renderer states, QR generation, publish rule and document fiscal panel. | Scan the fiscalised PDF's QR with a phone: it opens the stored URL. | 6 |
| 32 | `/32-fiscal-provider-adapter` | Real provider adapter against its sandbox, contract tests, runbook and onboarding checklist. | Walk through fiscalisation.md §7 steps 3–4 yourself in Sandbox; tick each box. | 8 |

## Time plan (full-time, ~32 focused hours a week)

| Week | Prompts | Hours |
|---|---|---|
| 1 | 00–04 | 24 |
| 2 | 05–08 | 27 |
| 3 | 09–13 | 30 |
| 4 | 14–17 | 27 |
| 5 | 18–22 | 31 |
| 6 | 23–27 | 25 |
| Phase 2 (fiscalisation) | 28–32 | 34 |

**Phase 1: about 164 hours, 6 weeks. Phase 2 fiscalisation: about 34 hours (~2 weeks), built when a business needs it.** Estimates include reviewing, fixing and committing. Add 20% buffer for the designer (16) and real Paynow/WhatsApp setup.
