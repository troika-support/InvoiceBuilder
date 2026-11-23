---
description: "TemplateDefinition, HTML renderer and Playwright PDF with the 60-line pagination test."
---

# 05 · Template renderer and PDF (riskiest first)

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Prove designer JSON → HTML → PDF with a flowing items table (`docs/template-rendering.md` §1–§5, §8).

## Tests first
In Integration.Tests (`[Trait("Category","Pdf")]`), using a hand-written "Studio Bold" definition matching canvas 3:
1. 3 lines → 1 page.
2. 60 lines → 3 pages and "DESCRIPTION" appears on every page (PdfPig).
3. Totals text appears once, on the last page, after the last line.
4. Footer "Pay online" appears on every page.
5. A 500-character description stays within its column (no text beyond the page's right margin).
Unit tests: binding whitelist (unknown path renders empty + warning), HTML-encoding of `<script>` in client name, token fallback.

## Do
1. Application: `TemplateDefinition` records (schema v1, §2), `DocumentRenderModel`, `ITemplateRenderer`, `IPdfRenderer`.
2. Infrastructure/Rendering: `HtmlTemplateRenderer` (§4, including the thead-spacer approach), `ThemeTokenResolver`, `BindingResolver` (§3), `FontEmbedder` (Manrope + IBM Plex Mono from `assets/fonts`).
3. `PlaywrightBrowserPool` (singleton, lazy, restart after 200 renders) and `PlaywrightPdfRenderer` (§5: network blocked, 20 s timeout, PrintBackground).
4. Dev endpoint `GET /dev/render-sample?lines=60` (Development only) that returns the PDF.
5. If the thead spacer fails test 2, implement the two-pass fallback in §4 and say which one you used.

## Done when
All five PDF tests pass locally; the dev endpoint opens a 3-page PDF that looks like canvas 3.
