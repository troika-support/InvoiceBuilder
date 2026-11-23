# Invoice Builder — GitHub Copilot build kit

Everything GitHub Copilot (Agent mode, Visual Studio) needs to build Invoice Builder from an empty repository in 28 ordered steps (phase 1), plus 5 phase 2 steps for ZIMRA fiscalisation. Invoice Builder is a quotation and invoice platform for several businesses, with a free-form template designer, client pay links (Paynow), and email and WhatsApp delivery. It's built on .NET 10 with Blazor Server and MudBlazor.

## What's in the kit

| Path | What it does |
|---|---|
| `.github/copilot-instructions.md` | **The** repo instruction file: stack, conventions, 10 non-negotiable rules, how to work. Copilot reads it on every request. |
| `.github/instructions/*.instructions.md` | Targeted rules applied by file glob: UI, rendering, payments, tests, fiscalisation (phase 2). |
| `.github/prompts/NN-*.prompt.md` | 33 build prompts: phase 1 `/00-plan` … `/27-deploy`; phase 2 fiscalisation `/28-fiscal-domain` … `/32-fiscal-provider-adapter`. |
| `docs/spec.md` | Product spec: rules R1–R40 (incl. instalment plans R34–R40), data model, screens, deployment, licences. |
| `docs/design-system.md` | Tokens (contrast-verified), type, formats, status words, components, every screen region by region. |
| `docs/template-rendering.md` | The riskiest part: designer JSON → HTML → PDF with flowing tables. |
| `docs/paynow.md` | Payment initiation, callback, hash, polling, idempotency. |
| `docs/fiscalisation.md` | Phase 2: ZIMRA FDMS via a third-party provider — port, pipeline, fiscal days, fiscal block, onboarding checklist. |
| `docs/PROMPTS.md` | Index: number, slash command, what it builds, what you check, hours, time plan. |
| `docs/BUILD-PROMPTS.md` | All prompts in one copy-paste file with setup steps and the time plan. |
| `assets/theme-kit/` | MudTheme, CSS tokens, MudBlazor bridge, StatusPill, MoneyText, EmptyState, AppShell, SideNav. |
| `assets/fonts/` | Manrope and IBM Plex Mono woff2 files (SIL OFL). |
| `tools/contrast.py` | Re-checks token contrast when you change colours. |

The screen designs are the 11 artboards on the "Invoice Builder UI" canvas. `design-system.md` §8 is their written version and wins where they differ.

## Set up

1. Create an empty Git repository and copy everything in this folder to its root.
2. In Visual Studio: **Tools > Options > GitHub > Copilot > Copilot Chat**, enable custom instructions (`.github/copilot-instructions.md`) and prompt files if the option is listed.
3. Install the .NET 10 SDK, SQL Server (or use a container), Docker Desktop (for Testcontainers) and PowerShell 7.
4. Open the folder, open Copilot Chat, switch to **Agent** mode.
5. Type `/00-plan` and answer its questions. Then run `/01-solution-setup` and continue in order. `docs/BUILD-PROMPTS.md` has the same prompts to paste if slash commands don't show.

## Good habits

- **One prompt per fresh chat.** Long chats drift.
- **Commit after each prompt** once its "You check" passes (see `docs/PROMPTS.md`).
- When Copilot misses something, point it at the section: "Re-read design-system §8.3 region 4 and fix the totals list."
- Don't let it add packages the spec doesn't name. Ask it to stop and list questions instead.
- Run the risky steps early and don't skip their tests: **05** (PDF pagination) and **06** (Paynow).
- Close every `// VERIFY:` marker before go-live (Paynow and WhatsApp API details).

## Decide or confirm before you start

- MediatR version and licence (spec §8). Asked in step 01.
- Email provider (spec §2). Asked in step 21.
- Placeholders in the spec: other business names, server paths, retention periods, minimum part payment, SQL Server edition, Paynow currencies.
- Phase 2: whether fiscalisation applies to each business (accountant), and which provider (after sandbox trials) before step 32.
- A Paynow test integration before step 06. A WhatsApp Cloud API app and approved templates before step 21.
