# Invoice Builder — build prompts, step by step

All prompts in one file. Paste each block into **Copilot Chat in Agent mode**, one step per fresh chat, in order. The same prompts exist as slash commands in `.github/prompts/`.

## Before you start

1. Copy the kit's contents into the root of an empty Git repository (keep `.github/`, `docs/`, `assets/`).
2. Visual Studio 2022 17.14+ or 2026: Tools > Options > GitHub > Copilot > Copilot Chat → enable **custom instructions** (and prompt files if listed).
3. Install the .NET 10 SDK, SQL Server (Developer or a container), Docker Desktop (for Testcontainers) and PowerShell 7.
4. Open the folder, open Copilot Chat, switch to **Agent**, pick a strong model.
5. Commit after every step once *You check* passes.

**Extra prerequisites:**
- Before step 06: a Paynow merchant account with an integration (test mode) for Troika Design Workshop — integration ID and key.
- Before step 21: an email provider account and API key; a WhatsApp Business Cloud API app, phone number ID, token and approved templates `document_link` and `payment_receipt`.
- Before step 27: a Windows Server VM with IIS, a DNS name and TLS certificate, and SQL Server.
- Before step 28 (phase 2): your accountant's confirmation that fiscalisation applies, and the Applies-from date.
- Before step 32: the chosen fiscalisation provider's API docs and sandbox credentials for each business (separate taxpayers).

## Contents

- [Step 00 — Plan (no code)](#step-00-plan-no-code)
- [Step 01 — Solution setup](#step-01-solution-setup)
- [Step 02 — Domain model and rules (tests first)](#step-02-domain-model-and-rules-tests-first)
- [Step 03 — Persistence](#step-03-persistence)
- [Step 04 — Application core](#step-04-application-core)
- [Step 05 — Template renderer and PDF (riskiest first)](#step-05-template-renderer-and-pdf-riskiest-first)
- [Step 06 — Paynow gateway and webhook](#step-06-paynow-gateway-and-webhook)
- [Step 07 — Fake payment gateway and simulator](#step-07-fake-payment-gateway-and-simulator)
- [Step 08 — Authentication and roles](#step-08-authentication-and-roles)
- [Step 09 — UI shell and theme kit](#step-09-ui-shell-and-theme-kit)
- [Step 10 — Business settings](#step-10-business-settings)
- [Step 11 — Clients](#step-11-clients)
- [Step 12 — Catalogue](#step-12-catalogue)
- [Step 13 — Document editor: drafts, lines, totals and instalment plans](#step-13-document-editor-drafts-lines-totals-and-instalment-plans)
- [Step 14 — Issue, numbering, live preview and PDF](#step-14-issue-numbering-live-preview-and-pdf)
- [Step 15 — Documents lists](#step-15-documents-lists)
- [Step 16 — Template designer: canvas](#step-16-template-designer-canvas)
- [Step 17 — Templates, themes and publishing](#step-17-templates-themes-and-publishing)
- [Step 18 — Client links: view and quote acceptance](#step-18-client-links-view-and-quote-acceptance)
- [Step 19 — Online payment from the client link](#step-19-online-payment-from-the-client-link)
- [Step 20 — Payments ledger, manual payments, receipts, credit notes and rescheduling](#step-20-payments-ledger-manual-payments-receipts-credit-notes-and-rescheduling)
- [Step 21 — Email and WhatsApp sending](#step-21-email-and-whatsapp-sending)
- [Step 22 — Dashboard](#step-22-dashboard)
- [Step 23 — Background jobs and recurring invoices](#step-23-background-jobs-and-recurring-invoices)
- [Step 24 — Reports and exports](#step-24-reports-and-exports)
- [Step 25 — Hardening](#step-25-hardening)
- [Step 26 — Integration tests for main flows](#step-26-integration-tests-for-main-flows)
- [Step 27 — Deploy to IIS](#step-27-deploy-to-iis)
- [Step 28 — Fiscalisation: domain, port and fake provider](#step-28-fiscalisation-domain-port-and-fake-provider)
- [Step 29 — Fiscalisation: submission pipeline](#step-29-fiscalisation-submission-pipeline)
- [Step 30 — Fiscalisation: settings, tax mapping and fiscal days](#step-30-fiscalisation-settings-tax-mapping-and-fiscal-days)
- [Step 31 — Fiscalisation: fiscal block in templates](#step-31-fiscalisation-fiscal-block-in-templates)
- [Step 32 — Fiscalisation: first provider adapter and go-live](#step-32-fiscalisation-first-provider-adapter-and-go-live)

---

## Step 00 Plan (no code)

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Understand the product before writing code.

#### Do
1. Read `.github/copilot-instructions.md`, every file in `.github/instructions/`, and `docs/spec.md`, `docs/design-system.md`, `docs/template-rendering.md`, `docs/paynow.md`.
2. Reply with:
   - a 10-line summary of the product and phase-1 scope (spec §1),
   - the solution and folder structure you will create,
   - the aggregates and entities (spec §6) with their aggregate roots,
   - the NuGet packages you expect, each mapped to where the spec or instructions name it,
   - numbered questions about anything unclear or conflicting, including every `[placeholder]` you need decided now.
3. Do not create or change any files.

#### Done when
I have answered your questions. Wait for my answers before prompt 01.
````

**You check:** The structure matches the stack list; no packages appear that the spec doesn't name; answer its questions in the chat.

---

## Step 01 Solution setup

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
An empty, building Clean Architecture solution (copilot-instructions "Stack").

#### Do
1. Create `InvoiceBuilder.sln` with `src/InvoiceBuilder.Domain`, `src/InvoiceBuilder.Application`, `src/InvoiceBuilder.Infrastructure`, `src/InvoiceBuilder.Web` (Blazor Server, interactive server render mode only, plus MVC controllers), and `tests/InvoiceBuilder.Domain.Tests`, `tests/InvoiceBuilder.Application.Tests`, `tests/InvoiceBuilder.Integration.Tests`.
2. Project references: Application → Domain; Infrastructure → Application; Web → Application + Infrastructure; tests reference what they test.
3. `Directory.Build.props`: `net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`, `AnalysisLevel latest-recommended`.
4. `Directory.Packages.props` (central package management) with exactly: MediatR, FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Design, Microsoft.AspNetCore.Identity.EntityFrameworkCore, MudBlazor, Hangfire.Core, Hangfire.SqlServer, Hangfire.AspNetCore, Serilog.AspNetCore, Serilog.Sinks.File, Microsoft.Playwright, QRCoder, Microsoft.Extensions.Http.Resilience, xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Shouldly, NSubstitute, Testcontainers.MsSql, Microsoft.AspNetCore.Mvc.Testing, Microsoft.Extensions.TimeProvider.Testing, UglyToad.PdfPig. Use current stable versions. For MediatR, stop and ask which version (spec §8).
5. `.editorconfig` (file-scoped namespaces, `var` when obvious, 4 spaces, sorted usings) and `.gitignore` for .NET.
6. DI entry points: `Application/DependencyInjection.cs` (`AddApplication()`), `Infrastructure/DependencyInjection.cs` (`AddInfrastructure(IConfiguration)`), both called from `Web/Program.cs`. Add Serilog bootstrap logging in `Program.cs`.
7. One placeholder test per test project so `dotnet test` runs.

#### Done when
`dotnet build` has no warnings, `dotnet test` passes, and the Web app starts and shows the default page.
````

**You check:** Central package file has only the listed packages; MediatR version decision recorded; app runs.

---

## Step 02 Domain model and rules (tests first)

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
The pure domain for spec §3 and §6, with rules proven by tests before implementation.

#### Tests first
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

#### Do
1. `Result`, `Result<T>`, `Error(code, message)` in `Domain/Common`.
2. Value objects: `Money`, `CurrencyCode` (ISO `USD` and `ZWG` only; "ZiG" is display text, R11), `HsCode` (optional, up to 8 digits), `DocumentNumber`, `TaxPercent`, `Discount`.
3. Aggregates per spec §6: `Business` (+ NumberSequence, TaxRate), `Client`, `CatalogueItem`, `Document` (+ DocumentLine, QuoteAcceptance), `Payment` (+ PaymentAllocation), `Template` (+ TemplateVersion), `Theme`, `RecurringSchedule`. Every entity gets the base fields from spec §6.
4. Enums: `DocumentType`, `DocumentStatus`, `PaymentMethod`, `PaymentStatus`, `Cadence`.
5. Pure services: `DocumentCalculator` (R10, R13, R14), `DocumentStateMachine` (R4, R5, R7, R8), `PaymentAllocator` (R16, R17), `InstalmentPlanner` (generate and validate, R34–R36) and `InstalmentStatusCalculator` (R37, R38). `Instalment` is an entity inside the `Document` aggregate.
6. Domain events (records) for Issued, Viewed, Accepted, PaymentConfirmed, used later by the activity log.

#### Done when
All listed tests pass; Domain has no package references.
````

**You check:** Read the test names against R-numbers; check the rounding examples by hand.

---

## Step 03 Persistence

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
SQL Server persistence for the spec §6 model with tenant/business filters.

#### Do
1. `Infrastructure/Persistence/AppDbContext` (inherits `IdentityDbContext<AppUser>`), one `IEntityTypeConfiguration` per entity; decimal precisions per R10; JSON columns for snapshots, TemplateDefinition and Theme tokens.
2. Every unique index from spec §6, including the filtered ones (Document number where not null; Payment ProviderReference where not null).
3. Global query filters on `TenantId` and (where present) `BusinessId` via `ICurrentTenant` / `ICurrentBusiness` (interfaces in Application, implemented in Web later; use a fixed dev tenant for now).
4. `RowVersion` concurrency tokens; `CreatedAt`/`UpdatedAt` set by a `SaveChanges` interceptor using `TimeProvider`.
5. Repository interfaces in Application (`IDocumentRepository`, `IClientRepository`, `IPaymentRepository`, `ITemplateRepository`, `IBusinessRepository`, `ICatalogueRepository`, `IRecurringRepository`) and `IUnitOfWork` (`SaveChangesAsync`, `BeginTransactionAsync`). Implementations in Infrastructure.
6. `NumberSequenceRepository.AllocateAsync(businessId, type)` uses `UPDLOCK, ROWLOCK` inside the caller's transaction (R6).
7. Migration `Initial`, exported to `db/migrations/0001_initial.sql` (idempotent).
8. `Instalment` configuration (unique DocumentId + Position, amount decimal(18,2)) per spec §6.
9. Dev seed (Development only): one tenant, business "Troika Design Workshop" (USD, Net 14, no tax), number sequences per spec R6 defaults, 5 clients and 6 catalogue items from the canvas sample data, Troika Amber theme (design-system §2.3), and one issued invoice INV-0150 for $2,400.00 with a 3 × $800.00 monthly plan, first instalment paid.

#### Tests first
Integration test (Testcontainers): 20 parallel `AllocateAsync` calls produce 1–20 with no gaps or duplicates (R6). Query filter test: business A can't read business B's clients.

#### Done when
Migration applies to a fresh container, tests pass, and seed data is visible in SQL.
````

**You check:** Open the generated SQL script: unique indexes and filtered indexes present; decimal precision right.

---

## Step 04 Application core

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Cross-cutting Application plumbing every feature uses.

#### Do
1. MediatR registration plus pipeline behaviours: `ValidationBehaviour` (FluentValidation → `Result` failure with field errors), `LoggingBehaviour`, `UnitOfWorkBehaviour` (commits for commands marked `ITransactionalCommand`).
2. `ICurrentUser`, `ICurrentTenant`, `ICurrentBusiness` interfaces; `IActivityLog.AddAsync(entityType, entityId, action, summary)` writing `ActivityEntry` (R33), plus a domain-event dispatcher that logs Issued, Viewed, Accepted and PaymentConfirmed.
3. `IDisplayFormatter` implementing design-system §5.2 exactly (dates in Africa/Harare, `$2,400.00`, `ZiG 18,200.00` for currency code `ZWG`, "N days late"), with at least `Date(DateTimeOffset)`, `ShortDate(DateTimeOffset)`, `Time(DateTimeOffset)`, `Money(decimal amount, string currency, bool showCurrency = true)` and `DaysLate(int days)` (the theme kit's `MoneyText` calls `Money`).
4. Options: `AppOptions` (PublicBaseUrl, TimeZone = Africa/Harare), `StorageOptions` (FilesRoot), `PdfOptions` (PoolSize = 2, TimeoutSeconds = 20) with `ValidateOnStart`.
5. `IFileStore` (Save/Open/Delete by key) and a local-disk implementation under `StorageOptions.FilesRoot`.
6. `IFiscalisationService` (`QueueAsync(Guid documentId, CancellationToken)`) with `NoOpFiscalisationService` (R32). Phase 2 (prompt 29) replaces the implementation; keep the interface as is.

#### Tests first
Formatter tests for every row of design-system §5.2; validation behaviour returns field errors without calling the handler.

#### Done when
Tests pass; app starts with options validated (missing PublicBaseUrl fails fast in Production).
````

**You check:** Formatter output matches the design-system table character for character.

---

## Step 05 Template renderer and PDF (riskiest first)

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Prove designer JSON → HTML → PDF with a flowing items table (`docs/template-rendering.md` §1–§5, §8).

#### Tests first
In Integration.Tests (`[Trait("Category","Pdf")]`), using a hand-written "Studio Bold" definition matching canvas 3:
1. 3 lines → 1 page.
2. 60 lines → 3 pages and "DESCRIPTION" appears on every page (PdfPig).
3. Totals text appears once, on the last page, after the last line.
4. Footer "Pay online" appears on every page.
5. A 500-character description stays within its column (no text beyond the page's right margin).
Unit tests: binding whitelist (unknown path renders empty + warning), HTML-encoding of `<script>` in client name, token fallback.

#### Do
1. Application: `TemplateDefinition` records (schema v1, §2), `DocumentRenderModel`, `ITemplateRenderer`, `IPdfRenderer`.
2. Infrastructure/Rendering: `HtmlTemplateRenderer` (§4, including the thead-spacer approach), `ThemeTokenResolver`, `BindingResolver` (§3), `FontEmbedder` (Manrope + IBM Plex Mono from `assets/fonts`).
3. `PlaywrightBrowserPool` (singleton, lazy, restart after 200 renders) and `PlaywrightPdfRenderer` (§5: network blocked, 20 s timeout, PrintBackground).
4. Dev endpoint `GET /dev/render-sample?lines=60` (Development only) that returns the PDF.
5. If the thead spacer fails test 2, implement the two-pass fallback in §4 and say which one you used.

#### Done when
All five PDF tests pass locally; the dev endpoint opens a 3-page PDF that looks like canvas 3.
````

**You check:** Open /dev/render-sample?lines=60: header row on each page, totals after the last row, footer on each page, fonts correct.

---

## Step 06 Paynow gateway and webhook

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Server-side payment confirmation per `docs/paynow.md` (spec R15–R20).

#### Tests first
- Hash: known vector, tampered field → false, missing hash → false (§4).
- Status mapping for each row of §5.
- `ConfirmOnlinePaymentCommand`: Paid → Confirmed + receipt + invoice Paid; duplicate Paid → one receipt; amount mismatch → not confirmed + flagged; Cancelled → Failed (§8).
- Webhook: bad hash → 400; unknown reference → 200, nothing changes.

#### Do
1. Application: `IPaymentGateway` and records exactly as `paynow.md` §2; `StartOnlinePaymentCommand`, `ConfirmOnlinePaymentCommand`, `IBusinessSecretStore`.
2. Infrastructure/Payments/Paynow: `PaynowGateway` (official package if it fits, else typed `HttpClient` + `AddStandardResilienceHandler`), `PaynowHash`, `PaynowStatusMapper`. Mark unverified details `// VERIFY:`.
3. `DataProtectionBusinessSecretStore` (purpose `Paynow.IntegrationKey`).
4. `Web/Controllers/Webhooks/PaynowWebhookController` at `POST /webhooks/paynow/{businessId}` (§3 step 4, §7).
5. Receipt issuing (R18) inside the confirm command, using the R6 number allocation.

#### Done when
Tests pass; the webhook is reachable anonymously and rate-limited.
````

**You check:** Every // VERIFY: is listed in the summary; check them against Paynow's docs before going live.

---

## Step 07 Fake payment gateway and simulator

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Develop and demo payments without Paynow (`paynow.md` §8).

#### Do
1. `FakePaymentGateway : IPaymentGateway` registered when `Payments:UseFake = true` (Development only; startup fails if true in Production).
2. Dev page `/dev/payments` (Owner only): lists Pending payments with buttons Simulate paid, Simulate cancelled, Simulate paid after 30 s. These post a correctly hashed form to the real webhook controller.
3. Fake `CheckStatusAsync` returns whatever was simulated.

#### Done when
From the dev page, simulating Paid confirms the payment and creates a receipt through the same code path as Paynow.
````

**You check:** Turn UseFake on in Production config locally: the app refuses to start.

---

## Step 08 Authentication and roles

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Sign-in and authorisation per spec §1.1 and §5.1.

#### Do
1. Identity with `AppUser`, roles Owner and Staff, cookie auth, lockout 5 tries / 15 min, password reset by email (use `IEmailSender` stub until prompt 21).
2. Required authenticator-app 2FA for Owner (enrolment page with QR via QRCoder, recovery codes).
3. `UserBusiness` assignments; `ICurrentBusiness` from the user's selected business (stored per user); policy `CanActOnBusiness` used by UI services and controllers (R2). Also register authorization policies named `Owner` (role Owner) and `Staff` (role Owner or Staff); the theme kit's SideNav uses these names.
4. Seeding command `dotnet run -- seed-owner --email … --tenant "…"` that creates the tenant and first Owner (no self-registration).
5. Sign-in, 2FA, forgot-password and reset pages styled with the theme tokens (centred card, max-width 420, product mark, DS §5.1 voice).
6. Implement `ICurrentUser`/`ICurrentTenant`/`ICurrentBusiness` in Web; replace the dev fixed tenant.

#### Tests first
Integration: Staff can't open `/settings`; a user assigned to business A gets 403 on B's client via controller; Owner without 2FA is forced to enrol.

#### Done when
I can seed an owner, enrol 2FA, sign in and see the empty app.
````

**You check:** Sign out and back in with a wrong code 5 times: lockout message shows; recovery codes work once each.

---

## Step 09 UI shell and theme kit

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
The app frame every screen sits in (design-system §1–§7).

#### Do
1. Copy `assets/theme-kit/` into the Web project as described in `assets/theme-kit/README.md`: `Theme/IbTheme.cs`, `wwwroot/css/ib-tokens.css`, `wwwroot/css/ib-mud-bridge.css`, `Components/Shared/StatusKind.cs`, `StatusPill.razor`, `MoneyText.razor`, `EmptyState.razor`, `Layout/AppShell.razor`, `Layout/SideNav.razor`. Copy `assets/fonts/*.woff2` to `wwwroot/fonts/` (`assets/fonts/README.md`). Fix any MudBlazor API differences for the installed version without changing token values.
2. `MudThemeProvider` with `IbTheme.Theme`, dark mode following the OS with a user toggle stored per user.
3. SideNav per DS §7 (Owner vs Staff items), current page `aria-current="page"`; collapsed 72 px rail variant for editor and designer; top bar + drawer below 960 px.
4. Create `Components/Shared/BusinessSwitcher.razor` (SideNav already references it): a full-width `MudMenu` button showing the logo tile, the caption "Business" and the name, listing the user's businesses, changing `ICurrentBusiness`.
5. Skip link "Skip to content", one `h1` per page, `<PageTitle>` pattern.
6. Error pages: 404 "We can't find that page", 403 "You don't have access to this business", 500 with correlation ID. Blazor reconnect UI restyled with tokens.
7. Snackbar config (bottom-left, 4 s) plus an `aria-live` region service.

#### Done when
Every nav link opens a placeholder page using `EmptyState`; keyboard-only navigation works; reduced-motion disables transitions.
````

**You check:** Tab through the shell: visible focus everywhere; switch businesses; toggle dark mode; check contrast on a few pairs with a browser tool.

---

## Step 10 Business settings

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screen S10 per design-system §8.10 (canvas 7).

#### Do
1. Routes `/settings/profile`, `/settings/tax`, `/settings/payments`, `/settings/messaging`, `/settings/reminders`, `/settings/team` with the left section nav.
2. Profile & numbering: logo upload (PNG/SVG ≤ 1 MB, stored as Asset), trading name, registered name, email, WhatsApp number, address; numbering table with editable prefix, read-only next number, live example (R6). Prefix changes affect future numbers only.
3. Tax & currency: default currency, terms (R9), quote validity days (R7), tax rates list with effective-from and optional fiscal tax ID (R13), "Allow part payments" + minimum % (R20).
4. Payment methods: Paynow integration ID, key (write-only, "Saved ••••"), live/test switch, "Test connection" (initiates nothing; validates hash config), bank details text for manual transfers.
5. Messaging: email "from" name and reply-to; WhatsApp phone number ID and token (write-only).
6. Reminders: R30 settings with defaults.
7. ZIMRA fiscalisation card, disabled with the "Later" badge (R32).
8. "+ Add a business" creates a business with default sequences and the default theme.
9. Every save writes an activity entry (R33). Validation messages inline.

#### Done when
I can change Troika's profile, prefixes and Paynow key; the key never comes back to the browser.
````

**You check:** View page source/network after saving the Paynow key: it never appears; activity entries exist.

---

## Step 11 Clients

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screen S6 per design-system §8.6 (canvas 5).

#### Do
1. Queries: `GetClientsQuery` (search, filter All / With balance due / Overdue, paging), `GetClientDetailQuery` (balance per currency, avg days to pay, contacts, last 20 activity entries). Commands: Create, Update, Archive client.
2. List with initials avatar, last document, balance, billed this year; selected row style per DS.
3. Detail panel with tiles, contacts, actions (New quote, New invoice, Send statement — statement wired in prompt 24), timeline (`MudTimeline`, dot + text).
4. New/Edit client drawer: name, email, phone, WhatsApp, address, default currency, notes. Duplicate name in a business → inline error.
5. CSV import: name, email, phone, address columns; preview with errors before saving.
6. Empty state: "No clients yet" + "Add your first client".

#### Tests first
Avg days to pay calculation (paid invoices only); balance per currency never summed across currencies (R12).

#### Done when
Seeded clients show; create, edit and import work.
````

**You check:** Import a CSV with a duplicate and a bad email: preview flags both; nothing saved until confirmed.

---

## Step 12 Catalogue

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screen S7 per design-system §8.7.

#### Do
1. CRUD for `CatalogueItem` with name, description, unit, unit price, currency, tax rate, optional HS code (R13a, hint "Optional. Needed once fiscalisation is on."), recurring flag. Lines added from the catalogue copy the HS code.
2. Items used on documents can only be archived (R13 snapshots keep old documents intact).
3. `SearchCatalogueQuery` for the editor's "Add from catalogue…" (top 10, name/description contains).

#### Done when
Items can be added, edited, archived and searched.
````

**You check:** Archive an item used on a draft: the draft keeps its line.

---

## Step 13 Document editor: drafts, lines, totals and instalment plans

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screen S3 regions 1–5 per design-system §8.3 (canvas 2), drafts only.

#### Do
1. Commands: `CreateDraftDocument` (type, client), `UpdateDraftHeader`, `UpsertDraftLines` (full list with positions), `SetDiscount`, `DuplicateDocument`. All reject non-Draft (R5).
2. Editor page `/invoices/{id}` and `/quotes/{id}` with the 72 px rail, header (breadcrumb, "Draft invoice", status pill, "Saved HH:mm"), client card + picker, dates/terms/currency (quotes: Valid until), the line items table (drag handle, Enter adds a line, Alt+↑/↓ reorders, remove), catalogue search, totals list, payment-methods chips, notes.
3. Totals use `DocumentCalculator` (prompt 02) and count up under the motion rules.
4. Autosave 5 s after the last edit; on a concurrency conflict show "This draft changed elsewhere. Reload to see the latest." with Reload.
5. Required-field summary disables Issue (client, ≥ 1 line, due date / valid until).
6. Payment plan card per design-system §8.3 region 5 (canvas 10): `SetInstalmentPlan` / `ClearInstalmentPlan` commands (Draft only), generator (count, frequency, first due incl. "On acceptance"/"On issue"), editable rows, running check, Issue disabled until it matches (R34). Changing lines re-checks the plan and shows the difference.
7. Right column placeholder "Live preview" (filled in prompt 14).

#### Tests first
Application tests for `UpsertDraftLines` validation (qty > 0, price ≥ 0, description required, ≤ 200 lines) and totals for the canvas sample (2,400.00); `SetInstalmentPlan` rejects plans that don't sum to the total and rejects "On acceptance" on invoices.

#### Done when
I can build the INV-0142 sample as a draft and see $2,400.00.
````

**You check:** Keyboard only: add three lines, reorder one, remove one; totals update; screen reader reads the total.

---

## Step 14 Issue, numbering, live preview and PDF

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Finish S3: live preview (template-rendering §6), issue (R5, R6, R32) and PDF.

#### Do
1. `DocumentPreview` component: sandboxed iframe `srcdoc` with `ITemplateRenderer` output, scaled to fit, debounced 400 ms; template picker (published versions only) and "Edit design" link; "Page N of M · A4" from the renderer's page estimate or the last PDF.
2. `IssueDocumentCommand`: transaction → allocate number (R6) → snapshot client, business, tax (rate, fiscal tax ID) and HS codes and template version → status Issued → client link token (R26, store hash) → activity → commit; then `IFiscalisationService.QueueAsync` (R32) and enqueue PDF render (Hangfire queue `pdf`, stores via `IFileStore`).
3. Split button "Issue & send" with menu Email, WhatsApp, Copy link, Issue without sending. Sending is wired in prompt 21; for now Copy link and Issue without sending work.
4. "PDF" button: drafts render on demand; issued documents download the stored PDF.
5. Issued documents open in a read-only view with actions (DS §8.2 list).

#### Tests first
Issue twice concurrently → one succeeds, one gets `Document.NotEditable`; issued document ignores later client and template changes; an "On issue" first instalment resolves to the issue date; issuing with a plan that no longer matches the total fails.

#### Done when
Issuing INV sample produces INV-0001-style number per prefix, a stored PDF matching the preview, and a copyable link.
````

**You check:** Change the client's email after issuing: the PDF and link still show the old snapshot.

---

## Step 15 Documents lists

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screen S2 per design-system §8.2.

#### Do
1. `GetDocumentsQuery` with type, status (Overdue derived per R4), client, date range, currency, search on number/client, paging and sort.
2. Pages `/quotes`, `/invoices`, `/receipts` (receipts and credit notes in tabs).
3. Row click: Draft → editor; issued → read-only view.
4. Row actions per DS §8.2: Send again, Download PDF, Record payment (opens the drawer from prompt 20), Create credit note (prompt 20), Convert to invoice, Duplicate.
5. Export CSV of the filtered list.
6. Empty states per type.

#### Done when
Lists filter and sort correctly; overdue invoices show "N days late".
````

**You check:** Filter Overdue with a ZiG and a USD invoice: amounts stay in their own currencies.

---

## Step 16 Template designer: canvas

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screen S4 per design-system §8.4 (canvas 3) and template-rendering §7.

#### Do
1. Add Fabric.js (MIT) as a static file under `wwwroot/lib/fabric/` (pinned version; no CDN).
2. `wwwroot/js/designer/templateCanvas.js` and `templateMapper.js` per §7: A4 page, zoom 50/75/100/Fit, snapping with pink guides and mm label, 1 mm grid with Alt, selection events to .NET, undo/redo (100 steps).
3. Special objects: text, image/logo, shape, line, data field (binding picker from §3 whitelist), items table zone (dashed teal, badge "Items table · overflows onto page 2+"), totals block, instalment schedule (flow element after totals, template-rendering §2; add renderer support and a renderer test: plan of 3 renders 3 rows, no plan renders nothing), pay QR, signature.
4. `TemplateCanvas.razor` page `/templates/{id}/design`: top bar (name, version line, undo/redo, zoom, "Preview with sample data", Test PDF, Publish — publish wired in prompt 17), Insert grid, Layers (select, lock, hide, drag reorder), Properties (X/Y/W/H mm, element options, token swatches), First page / Continuation pages tabs.
5. Keyboard: arrows nudge 1 mm (Shift 5 mm), Delete, Ctrl+Z/Y, Ctrl+D; Layers list is the keyboard path to every element.
6. Save draft version (`SaveTemplateDraftCommand`) with the `TemplateDefinition` JSON; "Test PDF" renders the draft with sample data through prompt 05's renderer.

#### Tests first
Mapper round trip for each element type (`fromDefinition(toDefinition(x))` deep-equals x); server validation rejects unknown bindings and elements outside the page.

#### Done when
I can recreate canvas 3's "Studio Bold" layout, save it and get a matching Test PDF.
````

**You check:** Compare Test PDF to the canvas: positions within 1 mm, fonts and colours right.

---

## Step 17 Templates, themes and publishing

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screen S5 per design-system §8.5 and versioning rules R21–R23.

#### Do
1. `/templates` grid: thumbnail (first page PNG rendered by Playwright screenshot), name, document types, live version, "Used by N documents".
2. New template: blank, or from built-ins Minimal, Bold header, Classic ledger (ship three `TemplateDefinition` JSON files under `Infrastructure/Rendering/BuiltIns/`), each with an instalment schedule after the totals.
3. `PublishTemplateCommand`: draft → new version; previous versions kept; issued documents keep their version (R22). Confirm dialog "Publish vN? New documents will use it. Issued documents keep their version."
4. Themes tab: create/edit theme tokens (ink, accent, paper, muted, heading font, body font) with live contrast check; warn below 4.5:1 and block publish below 3:1 (design-system §2.3).
5. "New theme from logo": extract 3 dominant colours from the uploaded logo (server-side, no new package — simple quantisation) and prefill tokens.
6. Business default template per document type.

#### Done when
Publishing v2 changes new documents only; switching theme restyles token-bound elements.
````

**You check:** Open an invoice issued on v1 after publishing v2: still v1.

---

## Step 18 Client links: view and quote acceptance

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screens C1 and C2 per design-system §8.11–§8.12 (canvas 4 and 8), rules R8, R26, R27.

#### Do
1. Anonymous route `/d/{token}` (rate-limited, no auth cookie needed): resolve the token hash → document; 404 page "This link isn't valid anymore" for unknown/expired.
2. Business-themed layout (logo, accent rule) using Theme tokens, phone-first, max-width 480.
3. Invoice view: amount due, due pill, pay buttons (wired in prompt 19), items, Save PDF, Ask a question (WhatsApp deep link to the business number).
4. Quote view: items, terms link, payment plan card when present, Accept form (full name, checkbox), "Accept quote" or "Accept & pay instalment 1 · {amount}" when instalment 1 is due on acceptance (R35), "Request changes" with comment; expired message.
5. `RecordViewCommand` (R27), `AcceptQuoteCommand` / `DeclineQuoteCommand` (R8: name, UTC time, IP, user agent).
6. Owner side: "Needs attention" item and email (email wired in prompt 21); `ConvertQuoteToInvoiceCommand` creating a linked Draft invoice (once) and copying the plan.
7. R35: accepting a quote whose instalment 1 is due on acceptance converts and issues the invoice in the same command (number, snapshots, fiscal hook as prompt 14) and redirects the client to that invoice's link to pay instalment 1.
8. Invoice view with a plan: header pill "Next: {amount} due {date}" / "Instalment overdue", payment plan card with per-instalment StatusPill and receipt links (design-system §8.11).

#### Tests first
Token lookup uses the hash; accepted quote can't be accepted again; expired quote rejects acceptance; convert twice fails; R35 acceptance creates exactly one issued invoice with the plan copied and "On acceptance" resolved to the acceptance date.

#### Done when
Opening a link marks Viewed once; accepting records the signer and shows in the client's timeline.
````

**You check:** Open the link in a private window: no app chrome, business theme only; keyboard and screen reader can accept.

---

## Step 19 Online payment from the client link

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Connect C1 to prompt 06 and build C3 per design-system §8.13 (canvas 9).

#### Do
1. "Pay with EcoCash" opens a sheet: mobile number (prefilled, editable), amount (full balance; part allowed per R20 with min), method choice EcoCash / OneMoney / InnBucks → `StartOnlinePaymentCommand` (mobile). "Pay by card" → web initiation and redirect.
1a. Invoices with a plan (R40): choices "Pay next instalment · {amount}" (default; overdue + due remaining, or the next upcoming), "Pay full balance", "Other amount" (≥ R20 minimum). Part payments are always allowed on plan invoices.
2. Waiting state: "Approve the payment on your phone" with polite live region; poll `GET /d/{token}/payments/{paymentId}` every 5 s up to 2 min.
3. `/d/{token}/paid` shows Pending / Confirmed / Failed from our database only (paynow.md §3 step 7) with the C3 copy.
4. Paid invoices: hide pay buttons, show "Paid in full" and receipt links.
5. Hide online buttons when the business has no Paynow config or the currency isn't supported (paynow.md §6).

#### Done when
With the fake gateway, a full and a part payment both end on C3 and the invoice shows Part-paid then Paid.
````

**You check:** Refresh /paid with ?status=Paid added to the URL: the page still shows our real status.

---

## Step 20 Payments ledger, manual payments, receipts, credit notes and rescheduling

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screen S8 per design-system §8.8 (canvas 6); rules R15–R18 and credit notes (R4).

#### Do
1. `/payments`: period select, method tiles (ZiG separate), ledger table with status pills, Export.
2. "Record a payment" drawer: client, amount, date, method (Cash, Bank transfer, Other), open-invoice checklist with remaining balances and auto-fill oldest first, preview line "INV-xxxx becomes Paid · Receipt RCT-xxxx", "Send receipt to client by WhatsApp".
3. `RecordManualPaymentCommand` → Confirmed payment + allocations + receipt (R18) + activity.
4. Receipt document view and PDF (receipt layout from the business's receipt template or the built-in).
4a. Allocation across a plan invoice is a single allocation to the invoice; instalment progress is derived (R37). The drawer shows "Covers instalment 2 and $200.00 of instalment 3" under the preview line.
4b. `RescheduleInstalmentsCommand` (Owner): dialog from design-system §8.3 (issued view), R36 rules, required reason, optional client notification (queued for prompt 21's sender), activity entry.
5. `IssueCreditNoteCommand` from an issued invoice: full or partial, required reason, rules R13b enforced with clear messages (e.g. "Credit notes can't be issued more than 12 months after the invoice."); full credit → Void (R4, R17).

#### Tests first
Over-allocation rejected; allocation across two invoices; partial credit then payment → Paid at the right balance; payment of 1,000 on a 3 × 800 plan → #1 Paid, #2 Part-paid; reschedule of a paid instalment rejected.

#### Done when
Recording $600 against INV-0137 from canvas 6 makes it Paid and creates RCT with both payments listed on the client timeline.
````

**You check:** Receipt PDF shows the allocation lines and currency; credit note numbering uses CRN-.

---

## Step 21 Email and WhatsApp sending

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Deliver documents and receipts by email and WhatsApp (spec §2, R28, §4.3).

#### Do
1. `IEmailSender` (Application) with `ProviderApiEmailSender` (Infrastructure) for `[email provider]` behind config `Email:Provider`, `Email:ApiKey` (secret), `Email:From`. Ask me which provider before writing the adapter; keep a `LogOnlyEmailSender` for Development.
2. `IWhatsAppSender` using WhatsApp Business Cloud API (typed `HttpClient` + resilience): template messages `document_link` and `payment_receipt` with parameters (client name, document number, amount, link). Template names configurable per business. Mark API details `// VERIFY:`.
3. `SendDocumentCommand` (channels, recipients, message) → `OutboundMessage` rows → Hangfire jobs per channel → status updates; Sent status + activity on success.
4. Send dialog from "Issue & send" and "Send again": channel checkboxes, recipient fields prefilled, message preview, attach PDF (email).
5. Email templates (HTML + text) styled with the business theme: document sent, receipt, reminder, instalment reminder ("Instalment 2 of 3, $800.00, due 1 Dec 2026"), plan rescheduled, quote accepted (to owner), recurring drafts ready (to owner).
6. WhatsApp delivery status webhook (`POST /webhooks/whatsapp`) updating `OutboundMessage` (verify signature).

#### Done when
In Development, sending logs both channels; with real keys, an email arrives with the PDF and a working link.
````

**You check:** Message preview matches the copy; no secrets in logs; failed sends show the error on the document.

---

## Step 22 Dashboard

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screen S1 per design-system §8.1 (canvas 1), figures per spec §4.1.

#### Do
1. `GetDashboardQuery(currency)` returning outstanding, overdue (sum, count, oldest days; plan invoices contribute overdue instalments only, R38), instalments due in the next 7 days (count, amount), collected this month vs previous (%), quotes awaiting reply (count, value), recent documents (6), attention items, collected per month (6).
2. Header greeting by time of day, search (documents + clients quick results), USD/ZiG toggle (R12: separate figures), "New document" menu.
3. Tiles, recent documents with tabs, Needs attention actions (Send reminder, Record payment, Convert to invoice, Review recurring, overdue and upcoming instalments per design-system §8.1 region 4), chart with an aria-label.
4. Cache the query per business for 60 s; invalidate on Issue, Payment and Acceptance events.

#### Tests first
Figures for a seeded set including ZiG and USD and a credit note: no cross-currency sums; % change with zero last month shows "—".

#### Done when
The dashboard matches canvas 1 with seeded data.
````

**You check:** Switch USD/ZiG: every tile changes; nothing sums both.

---

## Step 23 Background jobs and recurring invoices

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Spec R7, R29–R31, paynow.md §3 step 6, screen S9 (design-system §8.9).

#### Do
1. Hangfire with SQL Server storage, queues `default`, `pdf`, `messages`; dashboard at `/ops/jobs` (Owner only).
2. Recurring jobs (Africa/Harare): `RecurringInvoicesJob` daily 06:00, `RemindersJob` daily 08:00, `ExpireQuotesJob` daily 00:15, `PollPendingPaymentsJob` every 2 minutes.
3. All jobs idempotent (R31): run-date keys, `ReminderLog` unique index, schedule `NextRunDate` advanced in the same transaction.
3a. `RemindersJob` handles plans per R39: offsets per instalment, one combined message per invoice per day naming each instalment.
4. `/recurring` list and editor drawer (cadence, start, end/count, Draft vs Auto issue, Paused); "Make recurring" action on issued invoices.
5. Owner notifications for recurring drafts ready (in-app + email).

#### Tests first
Running each job twice on the same `FakeTimeProvider` date creates nothing extra; two instalments hitting reminder offsets on the same day → one combined reminder; monthly cadence on 31 Jan → 28/29 Feb → 31 Mar rule (use last day of month when the day doesn't exist).

#### Done when
Jobs appear in the dashboard; a monthly schedule produces one draft per month.
````

**You check:** Trigger RemindersJob twice manually: one reminder per invoice per day.

---

## Step 24 Reports and exports

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Exports in spec §4.2.

#### Do
1. CSV exports (UTF-8 with BOM, invariant numbers, ISO dates) for documents list, payments ledger.
2. Client statement for a date range: opening balance, documents, payments, closing balance, per currency; HTML through the renderer with a built-in statement template; PDF via `IPdfRenderer`; "Send statement" from the client panel.

#### Done when
Statement for Harare Builders Co. reconciles with its invoices and payments.
````

**You check:** Open the CSV in Excel: amounts are numbers, dates sort correctly.

---

## Step 25 Hardening

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Production readiness. Work through the checklist and report pass/fail for each item.

#### Do — checklist
1. HTTPS only, HSTS, security headers (CSP allowing self + `data:` fonts/images; frame-ancestors none, except the preview iframe which uses `srcdoc`), Referrer-Policy, X-Content-Type-Options.
2. Antiforgery on all forms and state-changing controllers (webhooks excluded and signature-verified).
3. Rate limits: `/d/*` 60/min/IP, webhooks 60/min/IP, sign-in 10/min/IP.
4. Data Protection key ring persisted to `[path]`, protected (DPAPI-NG or certificate); app name set.
5. Secrets audit: nothing in appsettings*.json; all options `ValidateOnStart`.
6. Health checks `/health/live`, `/health/ready` (DB, Hangfire, Playwright pool).
7. Logging: correlation ID middleware; PII and secrets redaction enricher; retention 30 days for files.
8. Upload validation (type sniffing, size limits, SVG sanitised or rasterised).
9. Accessibility pass on every screen: axe DevTools (or Playwright + axe) with zero serious issues; manual keyboard and screen-reader pass for editor, designer (Layers path), client links.
10. Privacy: activity and logs avoid full phone numbers; documented retention placeholder `[set by OWNER]`.

#### Done when
Every item is marked pass, or has a written follow-up I agreed to.
````

**You check:** Read the pass/fail list; run a header scanner on the staging URL.

---

## Step 26 Integration tests for main flows

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Confidence across the main flows with real dependencies.

#### Do
Integration tests (Testcontainers SQL Server + `WebApplicationFactory`) for:
1. Draft → issue → link viewed → fake EcoCash paid → receipt → invoice Paid → dashboard figures.
2. Quote → sent → accepted → converted → invoice issued with a new number.
3. Part payment + manual payment + credit note → correct balance and statuses.
4. Template v1 → issue → publish v2 → old document still renders v1.
4a. Quote with a 3-instalment plan, instalment 1 on acceptance → accepted → invoice issued automatically → instalment 1 paid online → instalment 2 overdue next month → reminder → manual payment → Paid.
5. Recurring monthly over three fake months → three drafts, no duplicates on re-run.
6. Business isolation: user from business A can't reach B's documents via UI services, controllers or client link guessing.
7. Webhook replay: same Paynow callback 3 times → one receipt.

#### Done when
`dotnet test` passes locally and in CI with the Pdf category included.
````

**You check:** Break a rule on purpose (e.g. comment out the number lock): the right test fails.

---

## Step 27 Deploy to IIS

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Repeatable deployment to Windows Server / IIS (spec §7).

#### Do
1. Publish profile (framework-dependent, win-x64), `web.config` with in-process hosting and stdout logs off.
2. `deploy/Setup-Server.ps1`: install hosting bundle check, create the app pool (no managed code, identity), site with HTTPS binding, folders for files, logs and the Data Protection keys with ACLs, `PLAYWRIGHT_BROWSERS_PATH` and Chromium install, app-pool environment variables for every secret setting name (values prompted, never stored in the script).
3. `deploy/Publish.ps1`: build, test, apply the idempotent SQL script, copy with `app_offline.htm`, warm-up request.
4. `deploy/Smoke-Test.ps1`: `/health/ready`, sign-in page 200, render a sample PDF via an Owner-only endpoint, Paynow webhook rejects a bad hash.
5. `docs/runbook.md`: first run (seed owner, enrol 2FA, configure business, Paynow test mode, email/WhatsApp keys), go-live checklist (Paynow live keys, ResultUrl reachable, WhatsApp templates approved, backups scheduled, `// VERIFY:` items closed), backup and restore, rotating secrets, troubleshooting (template-rendering §9, paynow §9).

#### Done when
A clean Windows Server VM goes from nothing to a passing smoke test using only the scripts and runbook.
````

**You check:** Follow the runbook on a fresh VM without help; note every step that needed guessing.

---

# Phase 2 — ZIMRA fiscalisation

Build once phase 1 is live and a business needs fiscalisation. Reference: `docs/fiscalisation.md`, spec §9.

---

## Step 28 Fiscalisation: domain, port and fake provider

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
The fiscal model and provider port from spec §9.1–§9.2 and `docs/fiscalisation.md` §2.

#### Tests first
- `FiscalStatus` transitions: NotRequired is final; Pending → Fiscalised | Failed; Failed → Pending only via retry; fiscal data can't change once set (F6).
- F2 applicability: invoice before Applies-from → NotRequired; quote and payment receipt → NotRequired; credit note on a business with fiscalisation enabled → required.
- F3 enabling checklist: each missing item blocks enabling with its own error code.
- F12 HS code resolution order: line snapshot → catalogue item → business default → error.

#### Do
1. Domain: `FiscalConfig`, `FiscalTax`, `FiscalDay`, `FiscalSubmission`, the new `Document` fields (`FiscalStatus`, `FiscalData` as an immutable value object, `SentBeforeFiscalisation`), `FiscalApplicability` and `FiscalReadiness` services.
2. Persistence: configurations, indexes from spec §9.2, migration `Fiscalisation` + idempotent SQL script; credentials encrypted with Data Protection purpose `Fiscal.Credentials`.
3. Application: `IFiscalProvider` and records exactly as `fiscalisation.md` §2, `FiscalError` (Transient / Validation), `IFiscalProviderFactory` (resolves by `FiscalConfig.Provider`).
4. `FakeFiscalProvider` (Development/tests only; startup fails if configured in Production) with switches for closed day, validation error, timeout, slow close; deterministic receipt numbers and a QR URL on a fake domain.

#### Done when
Tests pass; migration applies; the fake provider is resolvable in Development.
````

**You check:** Read the error codes for the checklist; confirm fiscal data has no setters after creation.

---

## Step 29 Fiscalisation: submission pipeline

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
`docs/fiscalisation.md` §3 with spec rules F4–F9.

#### Tests first (FakeFiscalProvider + FakeTimeProvider)
- Success → Fiscalised, fiscal PDF rendered; re-sent only when `SentBeforeFiscalisation` (F6).
- Transient error → retries at 1, 5, 15, 60 min then hourly; Failed after 24 h (F7).
- Validation error → Failed, Needs-attention item, owner email, no retries (F7).
- `RetryFiscalisationCommand` after fixing a tax mapping → Fiscalised (F8).
- Credit note waits until its invoice is Fiscalised and carries the original fiscal reference (F9).
- Two documents for the same device never submit concurrently (F9).

#### Do
1. `FiscalisationService : IFiscalisationService` replacing the no-op: decides applicability, sets Pending, enqueues `FiscaliseDocumentJob`.
2. `FiscaliseDocumentJob` per §3 steps 1–6 with the per-device lock (`sp_getapplock`, 2-minute timeout) or a per-device Hangfire queue, and `EnsureDayOpenAsync` from §4.
3. Map document snapshot → `FiscalDocument` (ISO currency, lines with HS code and provider tax ID, tax totals, payments, original reference, reason).
4. Fiscal copy: re-render PDF with the fiscalised block; if sent before, `ResendFiscalCopyJob` re-sends on the original channels with the copy note from F6; mark activity.
5. Sending before fiscalisation sets `SentBeforeFiscalisation = true` (prompt 21's send command).
6. Needs-attention items and owner email for failures; dashboard counts.

#### Done when
With the fake provider, issuing and sending an invoice produces a pending copy, then a fiscal copy re-sent automatically; failures surface on the dashboard.
````

**You check:** Use the fake provider's switches to force each failure; check the activity timeline tells the story in order.

---

## Step 30 Fiscalisation: settings, tax mapping and fiscal days

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
Screen F-S1 per design-system §8.14, spec F1, F3, F10 and `fiscalisation.md` §4 and §6.

#### Do
1. `/settings/fiscal` with regions 1–6 of DS §8.14; Owner only; the phase 1 "Later" card in Tax & currency now links here.
2. Commands: `SaveFiscalConfig`, `TestFiscalConnection`, `SyncFiscalTaxes`, `MapTaxRate`, `EnableFiscalisation` (runs `FiscalReadiness`), `SwitchFiscalEnvironment` (confirm dialog copy from DS), `OpenFiscalDay`, `CloseFiscalDay`.
3. `CloseFiscalDaysJob` every 15 minutes per §4 (23:30 local or max hours − 1, respects the device lock), close-failure alerts and retries.
4. Health check `fiscal` (Degraded when any enabled business has a CloseFailed day or Failed documents older than 1 hour).
5. Every change logged in activity; credentials write-only.

#### Tests first
Close job timing at 23:29 vs 23:30 and at max-hours − 1; job skips while the device lock is held; enabling fails with each missing checklist item.

#### Done when
With the fake provider I can connect, sync and map taxes, enable, see the day open on first invoice and close on schedule or by button.
````

**You check:** Switch to Live: confirm dialog wording; credentials never reach the browser.

---

## Step 31 Fiscalisation: fiscal block in templates

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
`fiscalisation.md` §5, design-system §8.15–§8.16, spec F5, F6, F11.

#### Tests first
- Renderer: fiscal block in NotRequired (renders nothing), Pending, Fiscalised (QR payload equals stored URL), Failed (renders like Pending).
- Schema migration v1 → v2 leaves existing templates unchanged.
- Publish rule: invoice template without a block can't publish once a business using it has fiscalisation enabled; with one block on the last page it can.
- PDF test: fiscalised 60-line invoice shows the block on the last page only.

#### Do
1. `TemplateDefinition` schema v2 with `fiscalBlock` and the binding whitelist additions; migration function.
2. Renderer support with QRCoder SVG data URI; designer Insert item, properties, and the preview state picker.
3. Built-in templates (Minimal, Bold header, Classic ledger) gain a fiscal block on the last page.
4. Document fiscal panel (DS §8.15) on issued invoices and credit notes with Retry and Create credit note actions; "Verify on ZIMRA" link.
5. Client link pages show the block exactly as the PDF does.

#### Done when
Pending and fiscalised copies look right in preview, PDF and client link; publishing enforces the rule.
````

**You check:** Scan the fiscalised PDF's QR with a phone: it opens the stored URL.

---

## Step 32 Fiscalisation: first provider adapter and go-live

**Paste this into Copilot Chat:**

````markdown
Use **Agent mode**. Follow `.github/copilot-instructions.md`.

#### Goal
A working `{Provider}FiscalProvider` for the provider I chose `[provider]`, verified in its sandbox (`fiscalisation.md` §2, §7).

#### Before you start
Ask me for the provider's current API docs and sandbox credentials. Do not guess endpoints, auth or field names — every assumption gets `// VERIFY:`.

#### Do
1. Adapter in `Infrastructure/Fiscalisation/{Provider}/`: typed `HttpClient` + standard resilience handler (no automatic retry on submit), auth as the provider requires, mapping to and from `FiscalDocument` / `FiscalReceipt`, error mapping to Transient / Validation, capabilities (`RequiresHsCode`, `ManagesDaysAutomatically`, `MaxDayHours`).
2. Contract tests (opt-in `FISCAL_SANDBOX=1`): status, tax sync, open day, USD invoice with tax, USD no tax, ZWG invoice, partial credit note, full credit note, close day — each asserting a QR URL and verification code come back.
3. Logging: request/response bodies stored in `FiscalSubmission` with credentials and personal data redacted; never in log files.
4. Update `docs/runbook.md`: fiscal onboarding (checklist §7), switching to Live, what to do when the provider is down, day-close failures.
5. Deploy script: add the provider credential setting names to the app-pool variable list if any are global (per-business ones stay in the database).

#### Done when
All contract tests pass against the sandbox and the QR codes verify on ZIMRA's test verification page.
````

**You check:** Walk through fiscalisation.md §7 steps 3–4 yourself in Sandbox; tick each box.

---

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
