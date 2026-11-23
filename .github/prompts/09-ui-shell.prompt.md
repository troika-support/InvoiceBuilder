---
description: "Install the theme kit, layout, sidebar, business switcher, shared components and error pages."
---

# 09 · UI shell and theme kit

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
The app frame every screen sits in (design-system §1–§7).

## Do
1. Copy `assets/theme-kit/` into the Web project as described in `assets/theme-kit/README.md`: `Theme/IbTheme.cs`, `wwwroot/css/ib-tokens.css`, `wwwroot/css/ib-mud-bridge.css`, `Components/Shared/StatusKind.cs`, `StatusPill.razor`, `MoneyText.razor`, `EmptyState.razor`, `Layout/AppShell.razor`, `Layout/SideNav.razor`. Copy `assets/fonts/*.woff2` to `wwwroot/fonts/` (`assets/fonts/README.md`). Fix any MudBlazor API differences for the installed version without changing token values.
2. `MudThemeProvider` with `IbTheme.Theme`, dark mode following the OS with a user toggle stored per user.
3. SideNav per DS §7 (Owner vs Staff items), current page `aria-current="page"`; collapsed 72 px rail variant for editor and designer; top bar + drawer below 960 px.
4. Create `Components/Shared/BusinessSwitcher.razor` (SideNav already references it): a full-width `MudMenu` button showing the logo tile, the caption "Business" and the name, listing the user's businesses, changing `ICurrentBusiness`.
5. Skip link "Skip to content", one `h1` per page, `<PageTitle>` pattern.
6. Error pages: 404 "We can't find that page", 403 "You don't have access to this business", 500 with correlation ID. Blazor reconnect UI restyled with tokens.
7. Snackbar config (bottom-left, 4 s) plus an `aria-live` region service.

## Done when
Every nav link opens a placeholder page using `EmptyState`; keyboard-only navigation works; reduced-motion disables transitions.
