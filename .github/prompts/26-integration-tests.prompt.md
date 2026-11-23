---
description: "End-to-end flows against SQL Server containers and the fake gateway."
---

# 26 · Integration tests for main flows

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Confidence across the main flows with real dependencies.

## Do
Integration tests (Testcontainers SQL Server + `WebApplicationFactory`) for:
1. Draft → issue → link viewed → fake EcoCash paid → receipt → invoice Paid → dashboard figures.
2. Quote → sent → accepted → converted → invoice issued with a new number.
3. Part payment + manual payment + credit note → correct balance and statuses.
4. Template v1 → issue → publish v2 → old document still renders v1.
4a. Quote with a 3-instalment plan, instalment 1 on acceptance → accepted → invoice issued automatically → instalment 1 paid online → instalment 2 overdue next month → reminder → manual payment → Paid.
5. Recurring monthly over three fake months → three drafts, no duplicates on re-run.
6. Business isolation: user from business A can't reach B's documents via UI services, controllers or client link guessing.
7. Webhook replay: same Paynow callback 3 times → one receipt.

## Done when
`dotnet test` passes locally and in CI with the Pdf category included.
