---
description: "Fabric.js canvas, insert panel, layers and properties panel (S4 part 1)."
---

# 16 · Template designer: canvas

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screen S4 per design-system §8.4 (canvas 3) and template-rendering §7.

## Do
1. Add Fabric.js (MIT) as a static file under `wwwroot/lib/fabric/` (pinned version; no CDN).
2. `wwwroot/js/designer/templateCanvas.js` and `templateMapper.js` per §7: A4 page, zoom 50/75/100/Fit, snapping with pink guides and mm label, 1 mm grid with Alt, selection events to .NET, undo/redo (100 steps).
3. Special objects: text, image/logo, shape, line, data field (binding picker from §3 whitelist), items table zone (dashed teal, badge "Items table · overflows onto page 2+"), totals block, instalment schedule (flow element after totals, template-rendering §2; add renderer support and a renderer test: plan of 3 renders 3 rows, no plan renders nothing), pay QR, signature.
4. `TemplateCanvas.razor` page `/templates/{id}/design`: top bar (name, version line, undo/redo, zoom, "Preview with sample data", Test PDF, Publish — publish wired in prompt 17), Insert grid, Layers (select, lock, hide, drag reorder), Properties (X/Y/W/H mm, element options, token swatches), First page / Continuation pages tabs.
5. Keyboard: arrows nudge 1 mm (Shift 5 mm), Delete, Ctrl+Z/Y, Ctrl+D; Layers list is the keyboard path to every element.
6. Save draft version (`SaveTemplateDraftCommand`) with the `TemplateDefinition` JSON; "Test PDF" renders the draft with sample data through prompt 05's renderer.

## Tests first
Mapper round trip for each element type (`fromDefinition(toDefinition(x))` deep-equals x); server validation rejects unknown bindings and elements outside the page.

## Done when
I can recreate canvas 3's "Studio Bold" layout, save it and get a matching Test PDF.
