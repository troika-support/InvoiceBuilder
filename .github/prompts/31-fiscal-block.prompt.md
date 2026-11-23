---
description: "Designer element, renderer states, QR generation, publish rule and document fiscal panel."
---

# 31 · Fiscalisation: fiscal block in templates

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
`fiscalisation.md` §5, design-system §8.15–§8.16, spec F5, F6, F11.

## Tests first
- Renderer: fiscal block in NotRequired (renders nothing), Pending, Fiscalised (QR payload equals stored URL), Failed (renders like Pending).
- Schema migration v1 → v2 leaves existing templates unchanged.
- Publish rule: invoice template without a block can't publish once a business using it has fiscalisation enabled; with one block on the last page it can.
- PDF test: fiscalised 60-line invoice shows the block on the last page only.

## Do
1. `TemplateDefinition` schema v2 with `fiscalBlock` and the binding whitelist additions; migration function.
2. Renderer support with QRCoder SVG data URI; designer Insert item, properties, and the preview state picker.
3. Built-in templates (Minimal, Bold header, Classic ledger) gain a fiscal block on the last page.
4. Document fiscal panel (DS §8.15) on issued invoices and credit notes with Retry and Create credit note actions; "Verify on ZIMRA" link.
5. Client link pages show the block exactly as the PDF does.

## Done when
Pending and fiscalised copies look right in preview, PDF and client link; publishing enforces the rule.
