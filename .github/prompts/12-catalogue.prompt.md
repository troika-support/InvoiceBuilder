---
description: "Catalogue items and tax rate picker (S7)."
---

# 12 · Catalogue

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Screen S7 per design-system §8.7.

## Do
1. CRUD for `CatalogueItem` with name, description, unit, unit price, currency, tax rate, optional HS code (R13a, hint "Optional. Needed once fiscalisation is on."), recurring flag. Lines added from the catalogue copy the HS code.
2. Items used on documents can only be archived (R13 snapshots keep old documents intact).
3. `SearchCatalogueQuery` for the editor's "Add from catalogue…" (top 10, name/description contains).

## Done when
Items can be added, edited, archived and searched.
