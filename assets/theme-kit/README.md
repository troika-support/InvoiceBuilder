# Theme kit

Ready-made app-chrome theme for Invoice Builder, matching `docs/design-system.md`. Prompt 09 installs it.

| File | Copy to (in `src/InvoiceBuilder.Web/`) | What it is |
|---|---|---|
| `Theme/IbTheme.cs` | `Theme/` | MudBlazor `MudTheme` (light + dark palettes, type scale, radius) |
| `wwwroot/css/ib-tokens.css` | `wwwroot/css/` | CSS tokens, `@font-face`, status pills, shell, focus, reduced-motion |
| `wwwroot/css/ib-mud-bridge.css` | `wwwroot/css/` | Makes MudBlazor components follow the tokens (heights, radius, focus) |
| `Components/Shared/StatusKind.cs` | `Components/Shared/` | UI status vocabulary enum |
| `Components/Shared/StatusPill.razor` | `Components/Shared/` | Dot + word pill, never colour alone |
| `Components/Shared/MoneyText.razor` | `Components/Shared/` | Mono, tabular money via `IDisplayFormatter.Money` |
| `Components/Shared/EmptyState.razor` | `Components/Shared/` | Title, message, one action |
| `Layout/AppShell.razor` | `Components/Layout/` | Providers, skip link, sidebar, main, live region, dark-mode attribute |
| `Layout/SideNav.razor` | `Components/Layout/` | Role-based navigation; full or 72 px rail |
| `../fonts/*.woff2` | `wwwroot/fonts/` | Manrope + IBM Plex Mono (SIL OFL) |

In `App.razor` `<head>`, load in this order: MudBlazor CSS, `css/ib-tokens.css`, `css/ib-mud-bridge.css`.

Dependencies the kit expects from earlier prompts:
- `IDisplayFormatter.Money(decimal, string, bool)` (prompt 04)
- authorization policies `Owner` and `Staff` (Staff = Owner or Staff role) (prompt 08)
- `BusinessSwitcher.razor` (created in prompt 09)
- `_Imports.razor` with `@using MudBlazor`, `@using InvoiceBuilder.Web.Theme`, `@using InvoiceBuilder.Web.Components.Shared`, `@using InvoiceBuilder.Web.Components.Layout`, `@using InvoiceBuilder.Application.Common` (wherever `IDisplayFormatter` lives)

Written against MudBlazor 8.x. Property names on `PaletteLight`/`Typography` can move between major versions. Keep the values and adapt the shapes.

Every text colour pair was contrast-checked (see `docs/design-system.md` §2). Don't add new colours here without checking them to at least 7:1 (4.5:1 minimum for large text).
