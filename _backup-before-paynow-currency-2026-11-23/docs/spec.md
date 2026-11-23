# Invoice Builder — product spec

Source of truth for what the app does. `design-system.md` covers how it looks; `template-rendering.md` and `paynow.md` cover the two risky integrations. If anything here is unclear, ask before guessing.

Working name: **Invoice Builder**. Owner: Troika Design Workshop. Values in `[brackets]` are unknown and must stay as placeholders or settings.

---

## 1. Purpose and users

Create, brand, send and get paid for quotations and invoices across several businesses owned by one person, with a free-form designer for document templates.

- **Phase 1 (this kit):** one tenant (the owner's account) with several **business profiles** (e.g. Troika Design Workshop and `[other businesses]`). Every business has its own branding, numbering, clients, catalogue, templates and payment setup.
- **Phase 2 (spec §9 and §10, prompts 28+):** ZIMRA FDMS fiscalisation (§9) and a .NET MAUI app (§10, being specified).
- **Later (design for, don't build):** multi-tenant SaaS signup and billing.

### 1.1 Roles

| Role | Can |
|---|---|
| Owner | Everything in the tenant, including settings, templates and team. |
| Staff | Create and send documents, record payments, manage clients and catalogue for the businesses they are assigned to. No settings, no template publishing. |
| Client (anonymous, token link) | View one document, accept/decline a quote, pay an invoice, download PDF. No account. |

Phase 1 ships with Owner only; Staff exists in the model and auth policies so it can be switched on later.

### 1.2 Out of scope (phase 1)

Multi-tenant signup and subscription billing, accounting exports beyond CSV, inventory/stock, purchase orders, multiple languages.

## 2. External systems

| System | Use | Notes |
|---|---|---|
| Paynow | EcoCash, OneMoney, InnBucks, Visa/Mastercard payments from the client link | See `paynow.md`. Integration ID/key per business, stored encrypted. |
| WhatsApp Business Cloud API | Send document links and receipts | Proactive sends need approved template messages. Phone number per business. |
| Email provider API | Send documents, receipts, reminders | Provider: `[email provider]`. Behind `IEmailSender`; provider chosen by config. |
| Playwright for .NET (headless Chromium) | Render template HTML to PDF | See `template-rendering.md`. Runs on the IIS server. |
| Fabric.js (MIT) | Free-form template designer canvas in the browser | Wrapped in a Blazor component via JS interop. |

## 3. Business rules

Each rule is testable. Defaults are settings unless marked fixed.

### 3.1 Tenancy and businesses
- **R1.** Every row except Tenant and global lookups carries `TenantId`; EF global query filters enforce it. Business-scoped rows also carry `BusinessId`. (fixed)
- **R2.** A user only sees businesses they are assigned to; the Owner sees all. The active business is chosen in the sidebar switcher and stored per user.

### 3.2 Documents
- **R3.** Document types: Quote, Invoice, Receipt, CreditNote. They share a base (number, business, client, currency, exchange rate, dates, lines, totals, template version, status, notes).
- **R4.** Lifecycle:
  - Quote: Draft → Sent → Viewed → Accepted | Declined | Expired → (Accepted) Converted.
  - Invoice: Draft → Issued (Sent) → Viewed → PartPaid → Paid; Overdue is derived (due date passed and balance > 0; for invoices with an instalment plan, see R38); Void only via a full credit note.
  - Receipt: created when a payment is confirmed; never edited.
  - CreditNote: references one invoice; amount ≤ that invoice's uncredited total.
- **R5.** A document is editable only while Draft. Issuing locks lines, totals, currency, exchange rate, client snapshot, business snapshot and template version. Corrections after issue are credit notes. (fixed)
- **R6.** Numbers are assigned at issue, not at draft. Sequences are per business per type, gap-free and never reused, allocated inside the issuing transaction. Format = prefix + zero-padded number (defaults: `QUO-`, `INV-`, `RCT-`, `CRN-`; 4 digits).
- **R7.** Quote validity default: 14 days from issue (setting). After it, status becomes Expired (nightly job) and acceptance is refused.
- **R8.** Accepting a quote records the signer's typed full name, timestamp (UTC), IP address and user agent. "Convert to invoice" copies lines and any instalment plan into a new Draft invoice linked to the quote; a quote converts once. Exception R35: a quote with an amount due on acceptance converts and issues automatically.
- **R9.** Payment terms default: Net 14 (options Net 7, 14, 30, due on receipt, custom date). Due date = issue date + terms.

### 3.3 Money
- **R10.** Amounts are `decimal(18,2)`; quantities `decimal(18,3)`; rates `decimal(18,4)`. Line amount = round(qty × unit price, 2, MidpointRounding.AwayFromZero). Discount and tax are computed per line then summed. (fixed)
- **R11.** Currencies: USD and ZiG. Stored and sent to integrations as ISO 4217 codes **`USD`** and **`ZWG`**; "ZiG" is display text only (design-system §5.2). Each document has one currency. The exchange rate (ZiG per USD) is captured on the document at issue for reporting; amounts are never converted on the document itself.
- **R12.** Reports never add USD and ZiG together; they show each currency separately.
- **R13.** Tax rates are per business, with name, percentage, effective-from date and an optional **fiscal tax ID** (the provider/ZIMRA tax identifier, used in phase 2). Default for Troika Design Workshop: none (not VAT-registered). A line's tax rate is snapshotted at issue.
- **R13a.** Catalogue items and document lines carry an optional **HS code** (up to 8 digits) and the line snapshots it at issue with the tax rate.
- **R13b.** Credit notes (phase 1 rules that phase 2 relies on): reference exactly one issued invoice; require a reason (required text); same currency as the invoice; tax lines limited to the invoice's tax rates; total credited across all credit notes ≤ invoice total; not allowed more than 12 months after the invoice's issue date.
- **R14.** Discount: per document, either a percentage or a fixed amount, applied before tax, pro-rated across taxable lines.

### 3.4 Payments
- **R15.** A Payment has amount, currency, method (EcoCash, OneMoney, InnBucks, Card, BankTransfer, Cash, Other), date, reference and status (Pending, Confirmed, Failed). Only Confirmed payments count.
- **R16.** Payments are allocated to one or more invoices of the same client and currency. Sum of allocations ≤ payment amount; allocation to an invoice ≤ its balance.
- **R17.** Invoice balance = total − credit notes − confirmed allocations. Balance 0 → Paid; 0 < paid < total → PartPaid.
- **R18.** Each confirmed payment creates one Receipt (numbered per R6) listing its allocations.
- **R19.** Paynow payments start Pending; they are confirmed only after the hash-verified result callback or a status poll says Paid (see `paynow.md`). Callback handling is idempotent on the Paynow reference.
- **R20.** Partial online payments are allowed when the invoice setting "Allow part payments" is on (default on), and always on invoices with an instalment plan; minimum part payment set by the owner per business, default 30% of balance.

### 3.5 Templates
- **R21.** A template belongs to a business, applies to one or more document types and has page size A4 (fixed for phase 1).
- **R22.** Templates are versioned. Editing creates a draft version; Publish makes it current. Issued documents keep the version they were issued with forever.
- **R23.** Elements reference theme tokens (colours, fonts) rather than fixed values where possible. A theme belongs to a business; switching a template's theme restyles every token-bound element.
- **R24.** Each template has a First page layout and a Continuation page layout. The Items table zone flows across pages; the Totals block follows the table (default gap 12 mm) or sits fixed on the last page.
- **R25.** The same HTML renderer produces the live preview, the client link view and the PDF (see `template-rendering.md`).

### 3.6 Delivery and client link
- **R26.** Every issued document gets a client link `/d/{token}`; token = 32 random bytes, base64url, stored hashed. Links expire `[set by OWNER]`, default never for invoices, at quote expiry for quotes.
- **R27.** First open of a client link sets Viewed and logs an activity entry. Later opens are logged but don't change status.
- **R28.** Send channels per document: email (PDF attached + link), WhatsApp (template message with link), copy link. A WhatsApp send outside the 24-hour window uses an approved template only.

### 3.7 Recurring and reminders
- **R29.** A recurring schedule copies a source invoice's lines on a cadence (weekly, monthly, quarterly, yearly) with start, end/occurrence count and "issue automatically" or "create as draft". Default: create as draft.
- **R30.** Reminders: before due (default 3 days), on due date, and after due (default every 7 days, max 3). Per business on/off; per invoice pause. For invoices with an instalment plan the same schedule runs per instalment (R39).
- **R31.** Jobs are idempotent: running a job twice for the same day never creates duplicate invoices or reminders.

### 3.7a Instalment plans
- **R34.** Quotes and invoices may carry an optional instalment plan while Draft. The editor generates it from a count (2–24), a frequency (weekly, every 2 weeks, monthly, quarterly) and the first due date; amounts are equal, rounded down to the cent, with the remainder on the last instalment. Any instalment's amount and date can then be edited. Saving requires: amounts > 0, sum = document total exactly, dates strictly increasing, first date ≥ issue date (or "On acceptance"/"On issue", R35). Removing the plan returns to normal terms (R9).
- **R35.** The first instalment can be due **"On acceptance"** (quotes) or **"On issue"** (invoices); this replaces a separate deposit. When a client accepts a quote whose first instalment is due on acceptance, the quote is converted and the invoice **issued automatically** (R5, R6, R32 apply), the plan is copied with "On acceptance" resolved to the acceptance date, and the client is taken straight to pay instalment 1.
- **R36.** The plan is locked at issue with the rest of the document (R5). **Reschedule** (Owner only) may change the dates and amounts of instalments that have nothing paid; paid and part-paid instalments are locked; the new unpaid amounts must sum to the balance due; a reason is required and the change is logged (R33) with an option to notify the client. Rescheduling never changes lines or totals, so it has no fiscal effect.
- **R37.** Instalment state is derived, not stored: confirmed allocations to the invoice are applied to instalments **oldest due first** (overpaying rolls into the next; underpaying leaves it part-paid); credit-note amounts reduce instalments **from the last backwards**. States: Upcoming (date in the future), Due (today), Overdue (date passed, remaining > 0), Part-paid, Paid. Invoice due date = last instalment's date.
- **R38.** An invoice with a plan is overdue only when an instalment is overdue. Its pill reads "Instalment overdue"; the overdue amount is the sum of overdue instalments' remaining amounts (used by the dashboard and lists), not the whole balance.
- **R39.** Reminders run per instalment using R30's offsets; when several fall on the same day for one invoice, one combined message is sent. Message names the instalment ("Instalment 2 of 3, $800.00, due 1 Dec 2026").
- **R40.** On the client link, an invoice with a plan shows the schedule and offers **Pay next instalment** (sum of overdue + currently due remaining, or the next upcoming one), **Pay full balance**, or **Other amount** (≥ R20 minimum). Quotes show the plan above the acceptance form.

### 3.8 Fiscalisation seam
- **R32.** `IFiscalisationService.QueueAsync(documentId)` is called after invoice and credit note issue (outside the issuing transaction). Phase 1 implementation is a no-op. The setting "Fiscalise invoices with FDMS" is shown disabled with a "Later" badge. Phase 2 replaces this with §9.

### 3.9 Audit
- **R33.** Every state change (issue, send, view, accept, payment, credit, template publish, settings change) writes an `ActivityEntry` (who, what, when UTC, entity, before/after summary). Activity is append-only.

## 4. Reports and notifications

### 4.1 Dashboard figures (per active business, per currency)
Outstanding (sum of open balances, count), Overdue (sum, count, oldest days; for plans, overdue instalment amounts only, R38), Instalments due in the next 7 days (count, amount), Collected this month (vs previous month, %), Quotes awaiting reply (count, value), Collected per month for the last 6 months.

### 4.2 Exports
CSV: documents list, payments ledger, client statement (also PDF).

### 4.3 Notifications
| Event | To | Channel |
|---|---|---|
| Document issued and sent | Client | Email / WhatsApp, per send choice |
| Quote accepted or declined | Owner | In-app "Needs attention" + email |
| Payment confirmed | Client (receipt), Owner | Email/WhatsApp receipt; in-app |
| Payment failed | Owner | In-app |
| Invoice reminder (R30) / instalment reminder (R39) | Client | Business default channel |
| Instalment plan rescheduled (R36), if "notify client" ticked | Client | Business default channel |
| Recurring drafts ready | Owner | In-app + email |

## 5. Screens

| # | Screen | Route | Roles | Design section |
|---|---|---|---|---|
| S1 | Dashboard | `/` | Owner, Staff | DS 8.1 |
| S2 | Documents list (quotes, invoices, receipts, credit notes) | `/quotes`, `/invoices`, `/receipts` | Owner, Staff | DS 8.2 |
| S3 | Document editor + live preview | `/invoices/{id}`, `/quotes/{id}` | Owner, Staff | DS 8.3 |
| S4 | Template designer | `/templates/{id}/design` | Owner | DS 8.4 |
| S5 | Templates and themes list | `/templates` | Owner | DS 8.5 |
| S6 | Clients list + detail | `/clients`, `/clients/{id}` | Owner, Staff | DS 8.6 |
| S7 | Catalogue | `/catalogue` | Owner, Staff | DS 8.7 |
| S8 | Payments ledger + record payment | `/payments` | Owner, Staff | DS 8.8 |
| S9 | Recurring schedules | `/recurring` | Owner | DS 8.9 |
| S10 | Business settings | `/settings/{section}` | Owner | DS 8.10 |
| C1 | Client link: invoice | `/d/{token}` | Client | DS 8.11 |
| C2 | Client link: quote acceptance | `/d/{token}` | Client | DS 8.12 |
| C3 | Client link: payment result | `/d/{token}/paid` | Client | DS 8.13 |

### 5.1 Sign-in
Password reset by email. No open self-registration: a first-run setup page (`/account/setup`) creates the tenant and first Owner, and only works while no users exist; later users are added by the Owner (Settings > Team). The `seed-owner` command remains for scripted installs.

## 6. Data model

All entities: `Id` (Guid v7), `TenantId`, `CreatedAt`, `UpdatedAt` (UTC), `RowVersion`. Business-scoped entities also `BusinessId`.

| Entity | Key fields | Unique / indexes |
|---|---|---|
| Tenant | Name | — |
| Business | TradingName, RegisteredName, Email, WhatsAppNumber, Address, LogoAssetId, DefaultCurrency, DefaultTermsDays, DefaultThemeId, QuoteValidityDays, AllowPartPayments | (TenantId, TradingName) |
| UserBusiness | UserId, BusinessId, Role | (UserId, BusinessId) |
| NumberSequence | BusinessId, DocumentType, Prefix, NextValue, Padding | (BusinessId, DocumentType) |
| Client | Name, Email, Phone, WhatsApp, Address, DefaultCurrency, Notes | (BusinessId, Name) |
| CatalogueItem | Name, Description, Unit, UnitPrice, Currency, TaxRateId?, HsCode?, IsRecurring, IsArchived | (BusinessId, Name) |
| TaxRate | Name, Percent, EffectiveFrom, FiscalTaxId?, IsArchived | (BusinessId, Name, EffectiveFrom) |
| Document | Type, Number?, Status, ClientId, ClientSnapshot (json), BusinessSnapshot (json), Currency (ISO: USD/ZWG), ExchangeRate?, IssueDate?, DueDate?, ValidUntil?, Discount (kind, value), Subtotal, DiscountTotal, TaxTotal, Total, Notes, Terms, CreditReason?, TemplateVersionId, SourceDocumentId?, IssuedAt? | (BusinessId, Type, Number) where Number not null |
| DocumentLine | DocumentId, Position, Description, Detail, Quantity, UnitPrice, TaxRateId?, TaxPercentSnapshot, FiscalTaxIdSnapshot?, HsCodeSnapshot?, LineDiscount, LineTotal | (DocumentId, Position) |
| QuoteAcceptance | DocumentId, Decision, SignerName, AcceptedAt, Ip, UserAgent, Comment | (DocumentId) |
| ClientLink | DocumentId, TokenHash, ExpiresAt?, FirstViewedAt?, LastViewedAt? | (TokenHash) |
| Payment | ClientId, Amount, Currency, Method, Status, PaidAt, Reference, ProviderReference?, PollUrl? | (BusinessId, ProviderReference) where not null |
| PaymentAllocation | PaymentId, DocumentId, Amount | (PaymentId, DocumentId) |
| Instalment | DocumentId, Position, DueKind (Date, OnAcceptance, OnIssue), DueDate?, Amount, Note? | (DocumentId, Position) |
| Document (plan fields) | PlanCount?, PlanFrequency?, PlanRescheduledAt? | — |
| Template | Name, DocumentTypes (flags), CurrentVersionId?, ThemeId | (BusinessId, Name) |
| TemplateVersion | TemplateId, Version, Definition (json, see `template-rendering.md`), PublishedAt?, PublishedBy | (TemplateId, Version) |
| Theme | Name, Tokens (json: colours, fonts) | (BusinessId, Name) |
| Asset | Kind (Logo, Image, Font), FileName, ContentType, Bytes/Path, Sha256 | (BusinessId, Sha256) |
| RecurringSchedule | SourceDocumentId, Cadence, StartDate, EndDate?, MaxOccurrences?, AutoIssue, NextRunDate, Paused | — |
| ReminderLog | DocumentId, InstalmentPosition?, Kind, SentAt, Channel | (DocumentId, Kind, SentOn date) — one row per invoice per day; combined instalments listed in the message |
| OutboundMessage | DocumentId?, Channel, To, TemplateName, Status, ProviderMessageId, Error, SentAt | (Channel, ProviderMessageId) |
| PaymentGatewayConfig | BusinessId, Provider, IntegrationId, IntegrationKeyEncrypted, ResultUrl, ReturnUrl, IsLive | (BusinessId, Provider) |
| ActivityEntry | EntityType, EntityId, Action, ActorUserId?, ActorLabel, OccurredAt, Summary | index (EntityType, EntityId, OccurredAt) |

Personal data: client names, emails, phones, addresses and quote signer names/IPs. Retention `[set by OWNER]`; documents themselves are kept for at least the legal record-keeping period `[confirm with accountant]`.

## 7. Infrastructure and deployment

- .NET 10, Blazor Server (no WebAssembly / no `.Client` project), MudBlazor UI.
- Host: Windows Server, IIS (in-process), HTTPS only. SQL Server `[edition/version]`.
- Hangfire with SQL Server storage; dashboard at `/ops/jobs`, Owner only.
- Playwright Chromium installed on the server by a deploy script (`pwsh bin/.../playwright.ps1 install chromium`). PDF rendering runs in a Hangfire queue `pdf` with a small browser pool.
- File storage for assets and generated PDFs: local folder outside the web root `[path]`, behind `IFileStore` (blob storage can replace it later).
- Secrets: `dotnet user-secrets` in development; IIS app-pool environment variables in production (set by a PowerShell script); `ValidateOnStart` on all options. Per-business secrets (Paynow key, WhatsApp token) encrypted in the database with ASP.NET Data Protection; key ring persisted to `[path]` and protected with DPAPI-NG / certificate.
- Logging: Serilog to rolling files + SQL Server sink optional; correlation ID per request.
- Health checks: `/health/live`, `/health/ready` (DB, Hangfire, Playwright browser available).
- Public endpoints: `/d/{token}` client links and `/webhooks/paynow/{businessId}`; both rate-limited.

## 8. Licences to confirm

| Component | Licence | Note |
|---|---|---|
| MudBlazor | MIT | — |
| Fabric.js | MIT | — |
| Playwright for .NET | Apache-2.0 | Chromium bundled under its own licences. |
| Hangfire.Core / SqlServer | LGPL-3.0 | Commercial "Pro" features not used. |
| MediatR | v12 Apache-2.0; v13+ commercial licence above a revenue threshold | Confirm which version and whether a licence key is needed. |
| FluentValidation | Apache-2.0 | — |
| Serilog | Apache-2.0 | — |
| QRCoder (pay QR) | MIT | — |
| UglyToad.PdfPig (tests only) | Apache-2.0 | Reads PDF text in tests. |
| xUnit, NSubstitute, Shouldly, Testcontainers | Apache-2.0 / BSD-3 / BSD-3 / MIT | Test projects only. |
| Paynow .NET package | `[confirm licence]` | Or a typed HttpClient. |
| Fonts (Manrope, IBM Plex Mono) | SIL OFL | Self-host the font files. |

---

## 9. Phase 2 — ZIMRA FDMS fiscalisation

Built after phase 1 is live (prompts 28–32). Reference: `docs/fiscalisation.md`. Whether and from when each business must fiscalise is `[confirm with accountant]`; the feature is off until a business turns it on.

Decisions: connect through a **third-party fiscalisation provider** behind our own provider-neutral port (first adapter chosen after sandbox evaluation, `[provider]`); **each business is a separate taxpayer** with its own provider account and device; fiscal days are **automatic with manual controls**; on failure the invoice is **issued and may be sent anyway**, then a fiscal copy follows.

### 9.1 Rules
- **F1.** Fiscalisation is configured per business: provider, credentials (encrypted, write-only), device ID, environment (Sandbox/Live), Enabled, Applies-from date, default HS code `[optional]`.
- **F2.** Instalment plans don't change fiscalisation: the invoice is fiscalised once for its full total at issue; payments and reschedules are not submitted. Fiscal documents are Invoices and Credit Notes issued on or after Applies-from while Enabled. Quotes and payment Receipts are never fiscalised. Debit notes are out of scope.
- **F3.** Enabling is blocked until every active tax rate has a fiscal tax ID mapped from the provider's tax list (synced, not typed), and the business's invoice and credit-note templates include a Fiscal block (F11).
- **F4.** Issuing is unchanged (R5, R6): the document gets its number, `FiscalStatus = Pending`, and a fiscal submission is queued. The owner may send it immediately.
- **F5.** Until fiscalised, the PDF and client link show the Fiscal block in its pending state ("Fiscal verification pending"). A copy sent in this state is recorded on the document as "Sent before fiscalisation".
- **F6.** On success the document stores the provider's fiscal data (receipt ID, receipt global number, fiscal day number, device ID, verification code, QR URL, fiscalised at), `FiscalStatus = Fiscalised`, the PDF is re-rendered as the fiscal copy, and — if any pre-fiscal copy was sent — the fiscal copy is re-sent automatically on the same channels with the note "This is the fiscalised copy of {Number}. Please keep this one." Fiscal data is immutable once stored.
- **F7.** Transient failures (timeout, network, provider 5xx, day closed) retry automatically at 1, 5, 15 and 60 minutes, then hourly for 24 hours. Validation failures stop retrying: `FiscalStatus = Failed` with the provider message, shown in Needs attention and emailed to the owner.
- **F8.** A failed document is fixed without editing it (R5 stands): correct the cause in settings (tax mapping, HS codes on the catalogue, provider config) and press "Retry fiscalisation", which resubmits from the original snapshot plus the corrected mappings; or issue a credit note and a new invoice.
- **F9.** Submissions for one device are processed one at a time in issue order. A credit note is submitted only after its invoice is Fiscalised and references the invoice's fiscal receipt.
- **F10.** Fiscal days: before a submission, if no day is open, open one. A job closes the open day at 23:30 local time, or earlier when it reaches the provider's maximum day length minus 60 minutes. Close failures alert the owner. Settings show the current day and offer Open day / Close day buttons.
- **F11.** Fiscal block: a designer element showing the QR code (from the QR URL), verification code, fiscal day number, receipt global number, device ID and the fiscal label `[confirm required wording]`. Invoice and credit-note templates can't be published without it once any business using them has fiscalisation enabled. It renders nothing for non-fiscal documents.
- **F12.** Lines need an HS code when the provider requires it: line snapshot → catalogue item → business default; issuing is blocked with "Add an HS code to line N or set a default in Fiscalisation settings." when none is found.
- **F13.** Every provider call is stored as a `FiscalSubmission` attempt (request, response, status, timing; credentials never stored). Retention `[statutory period, confirm]`.
- **F14.** Before going live a business completes the onboarding checklist in `fiscalisation.md` §7 (sandbox tests, sample approval, live switch).

### 9.2 Data model additions

| Entity | Key fields | Unique / indexes |
|---|---|---|
| FiscalConfig | BusinessId, Provider, CredentialsEncrypted, DeviceId, Environment, Enabled, AppliesFrom, DefaultHsCode?, MaxDayHours?, LastTaxSyncAt? | (BusinessId) |
| FiscalTax | BusinessId, ProviderTaxId, Name, Percent?, Code | (BusinessId, ProviderTaxId) |
| FiscalDay | BusinessId, DeviceId, DayNo, OpenedAt, ClosedAt?, Status (Open, Closing, Closed, CloseFailed), LastError | (BusinessId, DeviceId, DayNo) |
| FiscalSubmission | DocumentId, Attempt, Status, ProviderReceiptId?, ReceiptGlobalNo?, FiscalDayNo?, VerificationCode?, QrUrl?, DeviceId, RequestJson, ResponseJson, Error, StartedAt, CompletedAt? | (DocumentId, Attempt) |
| Document (new fields) | FiscalStatus (NotRequired, Pending, Fiscalised, Failed), FiscalData (json, immutable once set), SentBeforeFiscalisation | index (BusinessId, FiscalStatus) |

### 9.3 Screens
| # | Screen | Route | Design section |
|---|---|---|---|
| F-S1 | Settings › Fiscalisation | `/settings/fiscal` | DS 8.14 |
| F-S2 | Fiscal panel on issued documents | (in S3 read-only view) | DS 8.15 |
| F-S3 | Fiscal block in the designer | (S4) | DS 8.16 |

## 10. Phase 2 — Mobile app (.NET MAUI)

Being specified. Will be added as prompts 33+.
