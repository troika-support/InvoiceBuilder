# Template rendering — designer JSON → HTML → PDF

The riskiest part of the product. One renderer turns a template version plus a document into HTML. That HTML is used three ways:
- the editor's live preview,
- the client link view,
- the PDF, via Playwright Chromium.

Get this right before building screens (prompt 05).

## 1. Pipeline

```
Fabric.js canvas (browser)  ──save──▶  TemplateDefinition JSON (our schema, stored on TemplateVersion)
                                              │
Document + Business + Client + Theme  ──▶  DocumentRenderModel
                                              │
                         ITemplateRenderer.RenderHtmlAsync(definition, model) ──▶ HTML string
                                              │
                ┌─────────────────────────────┼──────────────────────────────┐
         Live preview (iframe srcdoc)   Client link page             IPdfRenderer (Playwright) ──▶ PDF bytes
```

Rules:
- **Never store Fabric's raw JSON as the source of truth.** The designer converts Fabric objects ↔ `TemplateDefinition` in JS (`templateMapper.js`). The server only knows `TemplateDefinition`.
- The renderer is a pure function of (definition, model). There's no clock and no I/O inside it. Fonts and images come in as data URIs or absolute file URLs resolved before rendering.
- Units: **millimetres** everywhere in the definition. Canvas scale: 1 mm = 3.7795 px at 100% zoom (96 dpi).

## 2. TemplateDefinition schema (v1)

```jsonc
{
  "schemaVersion": 1,
  "page": { "size": "A4", "widthMm": 210, "heightMm": 297, "marginMm": { "top": 0, "right": 0, "bottom": 0, "left": 0 } },
  "themeTokens": ["ink", "accent", "paper", "muted"],      // tokens this template expects
  "firstPage":        { "elements": [ /* Element[] */ ] },
  "continuationPage": { "elements": [ /* Element[] */ ], "itemsTop": 40 },  // where the table resumes on page 2+
  "flow": {
    "itemsTable": { /* ItemsTableElement, placed on firstPage */ },
    "totals":     { "mode": "followTable", "gapMm": 12, "xMm": 108, "widthMm": 84, "heightMm": 20, "style": { } },
    "instalmentSchedule": { "gapMm": 8, "xMm": 18, "widthMm": 174, "style": { } },   // optional; follows totals, only drawn when the document has a plan
    "footerReserveMm": 30                                 // space kept clear at the bottom of every page
  }
}
```

Element (common): `id`, `type`, `xMm`, `yMm`, `widthMm`, `heightMm`, `rotation` (0 only in v1), `locked`, `hidden`, `zIndex`, `repeat` (`"first" | "all" | "last"`).

| type | Extra fields |
|---|---|
| `text` | `text` (may contain bindings `{{client.name}}`), `font` (token or family), `sizePt`, `weight`, `color` (token or hex), `align`, `lineHeight`, `letterSpacing`, `uppercase` |
| `image` | `assetId`, `fit` (`contain`/`cover`), `binding` (optional: `business.logo`) |
| `shape` | `shape` (`rect`/`ellipse`), `fill`, `stroke`, `strokeWidthMm`, `radiusMm` |
| `line` | `x2Mm`, `y2Mm`, `stroke`, `strokeWidthMm` |
| `field` | `binding` (see §3), `format` (`date`, `money`, `text`), plus text styling |
| `payQr` | `binding: "document.clientLinkUrl"`, `fill`, `background` |
| `signature` | `binding: "acceptance.signerName"`, styling |
| `instalmentSchedule` | columns (`no`, `dueDate`, `amount`, `status`, `remaining`), header/row styles; renders only when the document has a plan; flows after the totals block with the same no-split rule |
| `itemsTable` | `columns` (`description`, `qty`, `rate`, `tax`, `amount` with widths %), `header` style, `row` style, `zebra`, `showDetailLine` |

Colour values are either a token name (`"accent"`) or a hex (`"#111111"`). The renderer resolves tokens from the Theme; unknown tokens fall back to `ink`/`paper` and log a warning.

## 3. Bindings

Allowed paths (whitelist, nothing else resolves):

`business.tradingName`, `business.registeredName`, `business.email`, `business.phone`, `business.address`, `business.logo`,
`client.name`, `client.email`, `client.phone`, `client.address`,
`document.typeLabel` (Invoice/Quote/Receipt/Credit note), `document.number`, `document.issueDate`, `document.dueDate`, `document.validUntil`, `document.currency`, `document.subtotal`, `document.discountTotal`, `document.taxTotal`, `document.total`, `document.amountPaid`, `document.balance`, `document.notes`, `document.terms`, `document.clientLinkUrl`,
`plan.count`, `plan.nextInstalment.number`, `plan.nextInstalment.amount`, `plan.nextInstalment.dueDate`, `plan.overdueAmount`,
`acceptance.signerName`, `acceptance.acceptedAt`,
`page.number`, `page.count`.

Formatting follows design-system §5.2. All bound values are HTML-encoded. Bindings never execute code.

## 4. HTML output

```html
<!doctype html><html><head><meta charset="utf-8">
<style>
  @page { size: A4; margin: 0; }
  @font-face { ... }               /* fonts as data URIs */
  :root { --ink:#111; --accent:#F59E0B; ... }  /* theme tokens */
  .page-fixed { position: fixed; } /* repeat:"all" elements → repeat on every printed page */
  .first-only { position: absolute; }
  table.items { width: 100%; border-collapse: collapse; }
  table.items thead { display: table-header-group; }   /* header repeats on each page */
  table.items tr { break-inside: avoid; }
  .totals { break-inside: avoid; }
</style></head>
<body>
  <!-- repeat:"all" elements: position:fixed in mm -->
  <!-- firstPage elements: position:absolute on a 210×297mm first-page layer -->
  <!-- flow region: a block starting at itemsTable.yMm with left/right from itemsTable.xMm/widthMm,
       padding-bottom = footerReserveMm; on page 2+ a spacer of continuationPage.itemsTop
       is achieved with a thead spacer row of that height (only visible after page 1) -->
  <!-- totals: mode followTable → normal flow after the table with margin-top gapMm and margin-left xMm;
       mode lastPageFixed → placed absolutely on the last page (rendered after measuring) -->
</body></html>
```

Approach for page 2+ top offset: render the table inside a single-cell outer table whose `thead` holds a spacer row of height `continuationPage.itemsTop` mm. Chromium repeats `thead` on every printed page. On page 1 the flow region starts lower than the spacer, because the outer block is positioned at `itemsTable.yMm − itemsTop`. Prove this in prompt 05 with the 60-line test. If it fails, fall back to a two-pass render: render, then measure row heights with `page.EvaluateAsync`, then paginate manually.

`page.number` / `page.count`: use Playwright `HeaderTemplate`/`FooterTemplate` spans `pageNumber`/`totalPages` for footer text. Inside the body, page numbers are not available. Templates that need them place a `field` with `repeat:"all"`, and the renderer moves it into the footer template.

## 5. PDF (Playwright)

```csharp
public sealed class PlaywrightPdfRenderer(IBrowserPool pool) : IPdfRenderer
{
    public async Task<byte[]> RenderAsync(string html, CancellationToken ct)
    {
        await using var lease = await pool.RentAsync(ct);           // reuses a launched Chromium
        var page = await lease.Browser.NewPageAsync();
        await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.EvaluateAsync("document.fonts.ready");
        return await page.PdfAsync(new() { Format = "A4", PrintBackground = true, PreferCSSPageSize = true,
                                           Margin = new() { Top = "0", Right = "0", Bottom = "0", Left = "0" } });
    }
}
```

- `IBrowserPool`: 1–2 Chromium instances, launched lazily, restarted after N renders or on crash. It's registered as a singleton.
- PDFs are rendered in the Hangfire queue `pdf`. Issued documents are rendered once, stored by `IFileStore` and keyed by (documentId, templateVersionId). Drafts are rendered on demand and not stored.
- Block network access in the page (`page.RouteAsync("**/*", r => r.AbortAsync())` except `data:`), so a template can never fetch remote content.
- Timeout per render: 20 s.

## 6. Live preview

The editor sends the draft document to `POST /api/preview` (or calls the renderer service directly in Blazor Server). The resulting HTML goes into a sandboxed `<iframe sandbox="" srcdoc="…">`, scaled with CSS `transform` to fit the panel. Refresh is debounced to 400 ms after the last change.

## 7. Designer (Fabric.js)

- `wwwroot/js/designer/templateCanvas.js` — creates the Fabric canvas, A4 page rect, grid, snapping (to page edges, centre, other objects, 1 mm grid when Alt held), selection events → .NET via `DotNetObjectReference`.
- `wwwroot/js/designer/templateMapper.js` — `toDefinition(canvas) → TemplateDefinition` and `fromDefinition(def, theme) → canvas objects`. Special objects (itemsTable, totals, payQr, field) are Fabric groups with a `ibType` property and placeholder rendering.
- Blazor `TemplateCanvas.razor` — owns load/save; properties panel edits go through `canvasApi.updateElement(id, patch)`; undo/redo stack lives in JS (max 100 steps).
- Interop over SignalR only for selection changes, property edits and save. Dragging stays client-side.

## 8. Tests

- Renderer unit tests (snapshot HTML) for each element type, token resolution, binding encoding (`<script>` in client name renders as text), missing-token fallback.
- PDF integration tests (Playwright installed in the test environment):
  1. 3 lines → 1 page.
  2. 60 lines → 3 pages; the header row appears on each page (assert via `pdf text extraction` with PdfPig: count of "DESCRIPTION" = page count).
  3. Totals never split from the last row group.
  4. Footer text present on every page.
  5. Long description (500 chars) wraps, doesn't overflow the column.
- Round-trip test in JS (Vitest or Jest optional; or a Playwright component test): `fromDefinition(toDefinition(x))` equals x for each element type.

## 9. Troubleshooting

| Symptom | Check |
|---|---|
| Fonts fall back to Times | Fonts embedded as data URIs? `document.fonts.ready` awaited? |
| Background colours missing | `PrintBackground = true` |
| Table header not repeating | `thead { display: table-header-group }`; no `overflow:hidden` on ancestors |
| Fixed elements on page 1 only | They must be `position: fixed`, not `absolute` |
| Chromium won't start on IIS | App-pool identity can read the browser folder; `PLAYWRIGHT_BROWSERS_PATH` set to a shared folder; VC++ redistributable present |
| Slow first PDF | Pool warm-up at app start (`IHostedService`) |
