# Copilot instructions — Invoice Builder

Quotation and invoice builder for several businesses owned by one person: branded, versioned templates made in a free-form designer, sent by email and WhatsApp, paid via Paynow.

**Sources of truth (read before coding, cite by section):**
- `docs/spec.md` — rules R1–R33, data model, screens, deployment.
- `docs/design-system.md` — tokens, formats, components, screen layouts (DS §).
- `docs/template-rendering.md` — designer JSON → HTML → PDF.
- `docs/paynow.md` — payments.
- `docs/fiscalisation.md` — phase 2 ZIMRA fiscalisation (spec §9), only for prompts 28+.

If a request conflicts with these files, or something isn't covered, **stop and ask**. Don't invent rules, settings or packages.

## Stack (fixed)

- .NET 10, C# 14, nullable on, warnings as errors.
- Solution: `InvoiceBuilder.Domain`, `InvoiceBuilder.Application`, `InvoiceBuilder.Infrastructure`, `InvoiceBuilder.Web` (Blazor Server + MVC API controllers); tests in `tests/`.
- Clean Architecture. Dependencies point inward; Domain references nothing.
- EF Core + SQL Server. Repository + Unit of Work per aggregate (no `DbContext` outside Infrastructure).
- MediatR for commands/queries; FluentValidation as a MediatR pipeline behaviour.
- Result pattern for expected failures (`Result`, `Result<T>`, `Error`). Exceptions only for bugs and infrastructure faults.
- HTTP endpoints are **MVC controllers** (`[ApiController]`), used for webhooks, client-link actions and the future MAUI app. Blazor components call the Application layer through MediatR via thin UI services, never repositories.
- Blazor **Server** render mode only (no WebAssembly, no `.Client` project). MudBlazor with the custom `IbTheme` from the theme kit.
- ASP.NET Identity (self-hosted), cookie auth for the app, required 2FA for Owner.
- Hangfire (SQL Server storage) for background jobs. Serilog for logging. Playwright for .NET for PDFs. Fabric.js for the designer canvas. QRCoder for pay QR codes.
- Tests: xUnit, FluentAssertions-style assertions via **Shouldly**, NSubstitute; integration tests with Testcontainers (SQL Server).
- Hosting: Windows Server + IIS. Secrets: user-secrets in dev, IIS app-pool environment variables in prod, `ValidateOnStart`. Per-business secrets encrypted with Data Protection.

## Conventions

- Folders by feature inside each layer: `Application/Invoices/Commands/IssueInvoice/…`.
- File-scoped namespaces, primary constructors for DI, records for DTOs/commands, `sealed` by default.
- Every entity has `TenantId`; business-scoped ones also `BusinessId`. Global query filters do the scoping. Never call `IgnoreQueryFilters()` outside a named, reviewed admin query.
- Money is `decimal`. Use the `Money` value object (amount + `CurrencyCode`) in Domain. Never `double`.
- Time comes from `TimeProvider` (inject it). Store UTC; display in Africa/Harare with the DS §5.2 formats via `IDisplayFormatter`.
- Options classes bind config sections with `ValidateDataAnnotations().ValidateOnStart()`.
- Log with structured properties (`{InvoiceId}`), never string concatenation. Never log secrets, tokens or full card/phone numbers.
- Migrations are created with `dotnet ef migrations add` and also exported as idempotent SQL scripts (`dotnet ef migrations script --idempotent`) into `db/migrations/`.

## Non-negotiable rules

1. **Issued documents are immutable** (R5). No code path may change an issued document's lines, totals, currency, rate, snapshots or template version. Corrections are credit notes.
2. **Numbers are gap-free and assigned at issue** (R6), inside the same transaction, with a row lock on `NumberSequence`.
3. **Money never mixes currencies** (R11, R12). Totals, reports and allocations are per currency.
4. **Payments are confirmed server-side only** (R19, `paynow.md`): verified hash + status poll, idempotent on reference. The browser return never changes state.
5. **Tenant and business isolation** (R1, R2). Every query is scoped. Every controller action and UI service checks the user may act on that business. Client links grant access to exactly one document.
6. **Audit everything that changes state** (R33) through `IActivityLog`; activity is append-only.
7. **Templates render through one renderer** (R25). Preview, client view and PDF use `ITemplateRenderer`. Bound values are HTML-encoded, and PDF pages have no network access.
8. **Accessibility target WCAG 2.2 AAA where possible.** Use the tokens from DS §2 only (they're verified), keep focus visible, make every action keyboard-reachable, give icon-only buttons `aria-label`, and keep targets ≥ 44 px. Respect `prefers-reduced-motion` (DS §4).
9. **Status is never shown by colour alone.** Use `StatusPill` (dot + word, DS §5.3).
10. **Copy and formats come from DS §5.** Currency codes are ISO (`USD`, `ZWG`); "ZiG" is display text only. Dates, money and numbers go through `IDisplayFormatter`. Button labels follow the screens in DS §8.
11. **Email Provider Configuration**
- Use **SendGrid** as the email provider for sending invoices.
- Rotate the client link token on each re-send; the old link stops working.

## How to work

- Work in small, buildable steps. After each step: `dotnet build` and `dotnet test` must pass.
- Write tests first for rules (R-numbers), calculations, parsers, hashing and state machines. Name tests after the rule: `R17_balance_zero_marks_invoice_paid`.
- One prompt equals one focused change set. Don't refactor unrelated code.
- Don't add NuGet packages that aren't listed in the spec, these instructions or the current prompt. If you think one is needed, ask.
- When something is ambiguous, list numbered questions instead of guessing.
- At the end of each prompt, summarise what changed, any `// VERIFY:` or `// TODO(question):` markers, and what to check by hand.
