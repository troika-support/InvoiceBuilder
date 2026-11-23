---
description: "Blazor and MudBlazor UI rules for Invoice Builder"
applyTo: "src/InvoiceBuilder.Web/**/*.razor,src/InvoiceBuilder.Web/**/*.razor.cs,src/InvoiceBuilder.Web/**/*.razor.css,src/InvoiceBuilder.Web/wwwroot/css/**"
---

- Build each screen from its section in `docs/design-system.md` §8. Use the region order, copy and button labels written there.
- Use the theme kit: `IbTheme`, `ib-tokens.css`, `StatusPill`, `MoneyText`, `EmptyState`, `AppShell`. No raw hex values in components; use `var(--ib-…)` tokens or `Color.*` from the theme.
- Components get data through UI services in `Web/Services/*` that send MediatR requests. Never inject a repository or `DbContext` into a component.
- Every form field has a visible label above it (`Label` + `Variant.Outlined`). Required fields are marked in text ("required"), not only with `*`.
- Icon-only buttons need `aria-label`. Interactive elements are real buttons or links, never `div` with `@onclick`.
- Touch targets are at least 44 px. Focus outline stays visible (2 px `--ib-primary`).
- Status always goes through `StatusPill`. Money, dates and document numbers go through `MoneyText` / `IDisplayFormatter`.
- Motion: use the `ib-motion` classes only; they're disabled under `prefers-reduced-motion`.
- Long work (PDF, sending) runs in background jobs. Show a snackbar plus an `aria-live="polite"` message, never block the circuit.
- Tables use `MudTable`/`MudDataGrid` inside a horizontally scrollable box on small screens.
- Each page sets `<PageTitle>` as "{Screen} · Invoice Builder" and has one `h1`.
