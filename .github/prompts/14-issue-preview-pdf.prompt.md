---
description: "Issue documents with gap-free numbers, live preview iframe and PDF download (S3 part 2)."
---

# 14 · Issue, numbering, live preview and PDF

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Finish S3: live preview (template-rendering §6), issue (R5, R6, R32) and PDF.

## Do
1. `DocumentPreview` component: sandboxed iframe `srcdoc` with `ITemplateRenderer` output, scaled to fit, debounced 400 ms; template picker (published versions only) and "Edit design" link; "Page N of M · A4" from the renderer's page estimate or the last PDF.
2. `IssueDocumentCommand`: transaction → allocate number (R6) → snapshot client, business, tax (rate, fiscal tax ID) and HS codes and template version → status Issued → client link token (R26, store hash) → activity → commit; then `IFiscalisationService.QueueAsync` (R32) and enqueue PDF render (Hangfire queue `pdf`, stores via `IFileStore`).
3. Split button "Issue & send" with menu Email, WhatsApp, Copy link, Issue without sending. Sending is wired in prompt 21; for now Copy link and Issue without sending work.
4. "PDF" button: drafts render on demand; issued documents download the stored PDF.
5. Issued documents open in a read-only view with actions (DS §8.2 list).

## Tests first
Issue twice concurrently → one succeeds, one gets `Document.NotEditable`; issued document ignores later client and template changes; an "On issue" first instalment resolves to the issue date; issuing with a plan that no longer matches the total fails.

## Done when
Issuing INV sample produces INV-0001-style number per prefix, a stored PDF matching the preview, and a copyable link.
