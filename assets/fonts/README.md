# Fonts

Self-hosted Latin subsets (woff2) from Fontsource 5.3.0, both under the SIL Open Font License 1.1 (licence files included).

| File | Family | Weight |
|---|---|---|
| `manrope-latin-400…800-normal.woff2` | Manrope | 400, 500, 600, 700, 800 |
| `ibm-plex-mono-latin-400…600-normal.woff2` | IBM Plex Mono | 400, 500, 600 |

Copy them to `src/InvoiceBuilder.Web/wwwroot/fonts/`; `ib-tokens.css` declares the `@font-face` rules. The PDF renderer (prompt 05) embeds the same files as data URIs.
