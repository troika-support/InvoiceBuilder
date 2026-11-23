---
description: "Templates list, themes, versioning and publish (S4 part 2, S5)."
---

# 17 · Templates, themes and publishing

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screen S5 per design-system §8.5 and versioning rules R21–R23.

## Do
1. `/templates` grid: thumbnail (first page PNG rendered by Playwright screenshot), name, document types, live version, "Used by N documents".
2. New template: blank, or from built-ins Minimal, Bold header, Classic ledger (ship three `TemplateDefinition` JSON files under `Infrastructure/Rendering/BuiltIns/`), each with an instalment schedule after the totals.
3. `PublishTemplateCommand`: draft → new version; previous versions kept; issued documents keep their version (R22). Confirm dialog "Publish vN? New documents will use it. Issued documents keep their version."
4. Themes tab: create/edit theme tokens (ink, accent, paper, muted, heading font, body font) with live contrast check; warn below 4.5:1 and block publish below 3:1 (design-system §2.3).
5. "New theme from logo": extract 3 dominant colours from the uploaded logo (server-side, no new package — simple quantisation) and prefill tokens.
6. Business default template per document type.

## Done when
Publishing v2 changes new documents only; switching theme restyles token-bound elements.
