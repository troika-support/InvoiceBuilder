---
description: "Template definition, HTML renderer, PDF and designer canvas rules"
applyTo: "src/InvoiceBuilder.Infrastructure/Rendering/**,src/InvoiceBuilder.Application/Templates/**,src/InvoiceBuilder.Web/wwwroot/js/designer/**,src/InvoiceBuilder.Web/Components/Designer/**"
---

- Follow `docs/template-rendering.md` exactly. The `TemplateDefinition` schema is §2 and the bindings whitelist is §3.
- The renderer is pure: (definition, render model) → HTML. It does no I/O, uses no clock and never reads the database.
- HTML-encode every bound value. Never put user text into `style` or `<script>`.
- All positions and sizes are in millimetres in C# and JSON. Convert to px only inside the JS canvas (1 mm = 3.7795 px at 100%).
- Theme colours are referenced by token name. Resolve them in one place (`ThemeTokenResolver`).
- PDF: use `IBrowserPool`, block all network requests except `data:`, apply a 20 s timeout, set `PrintBackground = true`.
- Fabric objects are never persisted. Map them with `templateMapper.js` (`toDefinition` / `fromDefinition`) and keep the round trip lossless.
- Any change to the schema bumps `schemaVersion` and adds a migration function for older definitions.
