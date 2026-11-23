# Invoice Builder — design system

How the app looks and reads. The screen designs live on the "Invoice Builder UI" canvas (11 artboards); this file is the written contract Copilot builds from. Where the canvas and this file differ, **this file wins** (tokens below were adjusted after a contrast check).

Two visual layers, never mixed:
- **App chrome**: the Invoice Builder look below (slate, teal, Manrope). Used for every staff screen.
- **Business branding**: each business's theme (logo, colours, fonts from its Theme). Used only on documents, the template designer page, and client-link pages (C1–C3).

---

## 1. Brand

- Product name: **Invoice Builder**. Mark: a 32 px rounded square (radius 8) in Teal 400 `#14B8A6` with "IB" in Slate 900, weight 800. Placeholder until a real logo exists — don't draw anything else.
- Signature motifs:
  - **Monospace money.** Every amount, document number and reference is set in IBM Plex Mono with tabular figures, so columns line up.
  - **Status pill with a dot.** A soft pill, a 6 px dot and a word. Never colour alone.
- Business logos are uploaded assets; never redraw them.

## 2. Colour tokens

Verified with a contrast script (WCAG 2.2). Every text pair below reaches **AAA (≥ 7:1)** unless marked.

### 2.1 App chrome

| Token | Light | Dark | Use |
|---|---|---|---|
| `--ib-bg` | `#F4F5F7` | `#0B1120` | Page background |
| `--ib-surface` | `#FFFFFF` | `#111827` | Cards, panels, tables |
| `--ib-surface-2` | `#F1F5F9` | `#1F2937` | Inset areas, stat tiles |
| `--ib-border` | `#E2E6EB` | `#1F2937` | Card borders, row dividers |
| `--ib-border-strong` | `#D5DAE1` | `#334155` | Input borders, secondary buttons |
| `--ib-text` | `#0F172A` | `#F1F5F9` | Body and headings (17.9 / 16.2) |
| `--ib-text-2` | `#334155` | `#CBD5E1` | Labels (10.4 / 12.0) |
| `--ib-muted` | `#434E60` | `#A3B0C2` | Secondary text, column heads (8.4 on surface, 7.7 on bg / 8.1) |
| `--ib-primary` | `#115E59` | `#2DD4BF` | Primary buttons, links, focus ring |
| `--ib-on-primary` | `#FFFFFF` | `#042F2E` | Text on primary (7.6 / 7.8) |
| `--ib-primary-soft` | `#E6F4F2` | `#134E4A` | Selected rows, chosen options |
| `--ib-primary-soft-text` | `#0B4F4A` | `#CCFBF1` | Text on primary-soft (8.3 / 8.4) |
| `--ib-danger` | `#991B1B` | `#FCA5A5` | Error text, overdue labels (8.3 / 9.4) |
| `--ib-success` | `#166534` | `#86EFAC` | Positive deltas (7.1) |
| `--ib-nav-bg` | `#0F172A` | `#020617` | Sidebar |
| `--ib-nav-text` | `#CBD5E1` | `#CBD5E1` | Sidebar links (12.0) |
| `--ib-nav-muted` | `#A9B4C4` | `#A9B4C4` | Sidebar small labels (8.5) |
| `--ib-nav-active-bg` | `#134E4A` | `#134E4A` | Current page (white text 9.5) |
| `--ib-brand-mark` | `#14B8A6` | `#14B8A6` | Product mark only, never text |
| `--ib-chart` | `#99D5CF` / current `#115E59` | `#134E4A` / current `#2DD4BF` | Bars |

Note: the canvas mockups use `#0F766E` for primary and `#556070` for muted. Both only reach AA, so the tokens above replace them.

### 2.2 Status colours (pill background / text / dot)

| Status | Light bg | Light text | Dot | Dark bg | Dark text | Ratio |
|---|---|---|---|---|---|---|
| Draft | `#E2E8F0` | `#1E293B` | `#475569` | `#334155` | `#E2E8F0` | 11.9 / 8.4 |
| Sent, Viewed | `#DBEAFE` | `#1E3A8A` | `#1D4ED8` | `#1E3A8A` | `#DBEAFE` | 8.5 |
| Accepted | `#CCFBF1` | `#134E4A` | `#0F766E` | `#134E4A` | `#CCFBF1` | 8.4 |
| Part-paid, Pending | `#FEF3C7` | `#78350F` | `#B45309` | `#78350F` | `#FEF3C7` | 8.2 |
| Paid, Confirmed | `#DCFCE7` | `#14532D` | `#15803D` | `#14532D` | `#DCFCE7` | 8.3 |
| Overdue, Failed, Declined | `#FEE2E2` | `#7F1D1D` | `#B91C1C` | `#7F1D1D` | `#FEE2E2` | 8.2 |
| Expired, Void | `#E2E8F0` | `#1E293B` | `#475569` (hollow) | `#334155` | `#E2E8F0` | 11.9 |

### 2.3 Sample business theme (Troika Amber)

Used on the canvas for documents and client links: Ink `#111111`, Accent `#F59E0B`, Paper `#FFFFFF`, Muted `#6B6B6B`, client-page muted text `#44403C` on `#F5F5F4` (9.4), Ink on Accent 8.8. Business themes are user data; the designer warns when a text/background token pair is below 4.5:1 and blocks publish below 3:1.

## 3. Type

Fonts self-hosted (SIL OFL): **Manrope** (400, 500, 600, 700, 800) for all UI; **IBM Plex Mono** (400, 500, 600) for money, numbers and references, with `font-variant-numeric: tabular-nums`.

| Style | Size / line | Weight | Use |
|---|---|---|---|
| Display | 26 / 32, −0.02em | 800 | Page title (H1) |
| Title | 18 / 24 | 800 | Panel title, client name |
| Heading | 16 / 22 | 700 | Card heading (H2) |
| Body | 14 / 20 | 400–600 | Default |
| Label | 13 / 18 | 600 | Form labels, filters |
| Caption | 12 / 16 | 500–700 | Hints, pills, table head (uppercase, +0.04em) |
| Money L | 26 / 32 mono | 600 | Stat tiles |
| Money M | 18 / 24 mono | 600 | Totals |
| Money S | 14 / 20 mono | 400–600 | Table cells |

Client-link pages use 15 px body and 16 px buttons (phone first).

## 4. Space, shape, elevation, motion

- Spacing scale (px): 4, 8, 12, 16, 20, 24, 32, 40. Page padding 28–32 desktop, 16 phone. Card padding 20.
- Radius: inputs and buttons 10, cards 14, pills 999, small chips 6, client-page cards 16, client buttons 12.
- Elevation: cards are flat with a 1 px border. Only floating things get shadows: menus/dialogs `0 12px 32px rgba(15,23,42,.12)`, A4 previews `0 8px 24px rgba(15,23,42,.14)`.
- Touch targets ≥ 44 × 44 px (client pages ≥ 48–52).
- Focus: 2 px `--ib-primary` outline, 2 px offset, on every interactive element. Never remove it.
- Motion: 120–200 ms ease-out for hover, press, panel open and row highlight; 240 ms for drawers. Totals count up on change in the editor (≤ 300 ms). **Reduced motion:** under `prefers-reduced-motion: reduce`, every transition and animation is off (opacity changes allowed at ≤ 100 ms). Nothing auto-plays or loops.

## 5. Content

### 5.1 Voice
Plain, short, active. Speak to the owner as "you". Buttons say what happens ("Issue & send", "Record payment", "Convert to invoice"), never "Submit" or "OK". Errors say what went wrong and how to fix it.

### 5.2 Fixed formats

| Thing | Format | Example |
|---|---|---|
| Date | d MMM yyyy | 23 Oct 2026 |
| Short date (tables) | d MMM | 30 Oct |
| Time | HH:mm, 24-hour, Africa/Harare | 12:18 |
| USD | $ + #,##0.00 | $2,400.00 |
| ZiG (stored as ISO `ZWG`) | "ZiG " + #,##0.00 | ZiG 18,200.00 |
| Table amounts | #,##0.00, currency in column head or prefix when mixed | 1,200.00 |
| Document numbers | prefix + 4 digits | INV-0142 |
| Phone | +263 77 000 0000 | — |
| Relative overdue | "N days late" | 9 days late |

### 5.3 Status vocabulary

| Word | Means | Icon/dot |
|---|---|---|
| Draft | Not issued; editable | grey dot |
| Sent | Issued and delivered | blue dot |
| Viewed | Client opened the link | blue dot |
| Accepted / Declined | Client's decision on a quote | teal / red dot |
| Expired | Quote passed validity | hollow grey |
| Part-paid | Some confirmed payment, balance left | amber dot |
| Paid | Balance zero | green dot |
| Overdue | Past due with balance | red dot |
| Waiting for Paynow | Online payment started, not confirmed | amber dot |
| Confirmed / Failed | Payment outcome | green / red dot |
| Void | Fully credited | hollow grey |
| Instalment overdue | Invoice with a plan has an overdue instalment (R38) | red dot |
| Upcoming / Due | Instalment not yet due / due today | grey dot / blue dot |

## 6. Components (MudBlazor)

| Component | Build with | Notes |
|---|---|---|
| App shell | `MudLayout`, custom sidebar `nav` | Sidebar 240 px; collapses to 72 px icon rail in the editor and designer; becomes a top drawer below 960 px. |
| Business switcher | `MudMenu` on a full-width button | Shows logo tile, "Business" caption, name. |
| Primary / secondary / text buttons | `MudButton` Filled / Outlined / Text | Height 44, radius 10, weight 700. Split button for "Issue & send" (`MudButtonGroup` + menu). |
| Stat tile | `MudPaper` (Outlined) | Label, Money L, caption. |
| Status pill | `StatusPill` shared component (theme kit) | Status enum in; renders dot + word; never colour only. |
| Money | `MoneyText` shared component | Mono, tabular, currency-aware (5.2). |
| Data tables | `MudTable` / `MudDataGrid` | Uppercase caption heads, 14 px rows ≥ 48 px, horizontal scroll box on small screens. |
| Forms | `MudTextField`, `MudSelect`, `MudDatePicker`, `MudCheckBox`, `MudRadioGroup` | Labels above fields, `Variant.Outlined`, radius 10. |
| Line items editor | `MudTable` with inline fields + drag handle (`MudDropContainer`) | Keyboard: Enter adds a line, Alt+↑/↓ reorders. |
| Live preview | `DocumentPreview` component: sandboxed `iframe` with renderer HTML | Same renderer as PDF (spec R25). |
| Designer canvas | `TemplateCanvas` component wrapping Fabric.js via JS interop | See `template-rendering.md`. |
| Activity timeline | `MudTimeline` (start-aligned, dense) | Dot colour per status vocabulary + text. |
| Dialog / side panel | `MudDialog` (desktop), `MudDrawer` Anchor.End (record payment) | Focus trapped, Esc closes, returns focus. |
| Toast | `MudSnackbar` | Bottom-left, 4 s, pause on hover; also announced via `aria-live="polite"`. |
| Empty states | Shared `EmptyState` component | Title, one line, one primary action. |

## 7. Layout

- **Desktop (≥ 1280):** sidebar 240 + content (max-width 1440, padding 28/32). Two-column content uses flex wrap: main `flex: 999 1 560px`, side `flex: 1 1 300–360px`.
- **Tablet (960–1279):** same, side panels drop under main.
- **Phone (< 960):** sidebar becomes a top bar with menu button; tables scroll horizontally inside their card; stat tiles 2 × 2; editor preview moves to a "Preview" tab.
- **Navigation (Owner):** Dashboard, Quotes, Invoices, Receipts & credit notes, Clients, Catalogue, Templates, Payments, Recurring; Settings pinned at the bottom. **Staff:** same minus Templates, Recurring and Settings.
- Client-link pages: single column, max-width 480, phone-first; business theme header with logo and accent rule (4 px).

## 8. Screens

### 8.1 Dashboard — `/` (canvas 1)
1. Header: H1 "Good afternoon, {FirstName}" (morning < 12:00, afternoon < 17:00, evening). Sub: "Here's where {Business} stands this month." Search field "Search documents, clients…" (width 280). Currency toggle USD | ZiG (`aria-pressed`). Primary "New document" with menu: New quote, New invoice.
2. Four stat tiles: Outstanding (amount, "N open invoices"); Overdue (label in danger, amount, "N invoices · oldest N days"; plan invoices count only overdue instalments); Collected this month (amount, "▲ N% vs {PrevMonth}" in success or "▼" in danger); Quotes awaiting reply (count, "{amount} in the pipeline").
3. Recent documents card: tabs All / Quotes / Invoices / Overdue; columns Number (link, mono), Client, Due (overdue shows "N days late" in danger), Amount (right, mono), Status pill. Footer link "View all documents →". Empty: "No documents yet" + "Create your first quote".
4. Needs attention card: overdue invoices and overdue instalments ("INV-0150 · instalment 2 of 3 is 4 days late": Send reminder, Record payment), instalments due in the next 7 days ("3 instalments due this week · $2,150.00": View), accepted quotes (Convert to invoice), upcoming recurring runs (Review before sending). Empty: "You're all caught up."
5. Collected chart: 6 bars, current month in primary, labelled months, `role="img"` with an aria-label listing values.

### 8.2 Documents lists — `/quotes`, `/invoices`, `/receipts`
1. H1 (Quotes / Invoices / Receipts & credit notes), count + business; "New quote"/"New invoice".
2. Filters: search, status multi-select, client, date range, currency.
3. Table: Number, Client, Issued, Due/Valid to, Amount, Balance (invoices), Status. Row click opens the editor (draft) or the read-only document view (issued) with actions: Send again, Download PDF, Record payment, Create credit note, Convert to invoice (accepted quotes), Duplicate.
4. Bulk: Export CSV. Empty state per type.

### 8.3 Document editor — `/invoices/{id}`, `/quotes/{id}` (canvas 2)
1. Icon rail sidebar (72 px).
2. Header: breadcrumb "Invoices / {Business}", H1 number (or "Draft invoice" before issue), status pill, "Saved HH:mm" (autosave every 5 s of idle). Buttons: Save draft, PDF, split "Issue & send" (menu: Email, WhatsApp, Copy link, Issue without sending).
3. Client & details card: client card (avatar initials, name, email, phone, Change); Issue date, Terms, Due date, Currency (quotes: Valid until instead of Terms/Due). Note: "The number is assigned when you issue. The exchange rate is locked on the document at that moment."
4. Line items card: "Add from catalogue…" search; columns Description (+ detail line), Qty, Rate, Tax, Amount, remove; "Add line"; totals list Subtotal, "+ Discount", Tax (if any), "Total {CUR}" (Money M).
5. Payment plan card (canvas 10): switch "Split into instalments" (off by default). When on:
   - generator row: Number of instalments (2–24), Frequency (Weekly, Every 2 weeks, Monthly, Quarterly), First due (date, or "On acceptance" for quotes / "On issue" for invoices), button "Split evenly";
   - schedule table: #, Due (date picker), Amount (mono input), share of total (%), remove; "Add instalment";
   - running check under the table: "Scheduled $2,400.00 of $2,400.00" in success text with a check icon, or "$50.00 left to schedule" / "$50.00 over the total" in danger text with a cross icon; Issue stays disabled until it matches (R34);
   - Terms and Due date fields in region 3 are replaced by "Due: by instalments · last on {date}".
6. Payment & terms card: "Let the client pay with" chips (EcoCash (Paynow), Visa / Mastercard, Bank transfer); Notes to client.
7. Live preview: template select "{Business} · {Template} (vN)", "Edit design" link, A4 preview, "Page N of M · A4".
- Issued invoices with a plan: the read-only view shows the schedule with states (StatusPill: Upcoming, Due, Part-paid, Paid, Overdue), paid and remaining per instalment, and the Owner action "Reschedule" (dialog: unpaid rows editable, paid rows locked with a lock icon and "Paid", the same running check against the balance, required "Reason", checkbox "Let {Client} know about the new dates").
- Errors: inline under fields; "Issue" disabled with a summary list when required fields are missing (client, at least one line, due date).

### 8.4 Template designer — `/templates/{id}/design` (canvas 3)
1. Top bar: back, editable template name, sub "Invoice & quote · A4 · Editing draft vN — vM is live on K documents", Undo/Redo, Zoom (50/75/100/Fit), "Preview with sample data" toggle, "Test PDF", "Publish vN".
2. Left: Insert grid (Text, Image / logo, Shape, Data field, Items table, Totals, Instalment schedule, Pay QR, Signature) and Layers list (select, lock, hide, reorder by drag).
3. Centre: dotted canvas, A4 page; selection handles; pink snap guides with mm label; Items table zone dashed teal with badge "Items table · overflows onto page 2+"; tabs First page / Continuation pages.
4. Right: selection properties (X, Y, W, H in mm; element-specific options, e.g. Totals "Shows" and "Position rule"; Fill token swatches with "Uses theme tokens, so a theme swap restyles it."); Theme list with swatches; "+ New theme from logo".
- Keyboard: arrows nudge 1 mm (Shift = 5 mm), Delete removes, Ctrl+Z/Y, Ctrl+D duplicates; every element is also selectable from Layers (keyboard alternative to the canvas).

### 8.5 Templates — `/templates`
Grid of template cards (thumbnail, name, doc types, live version, "Used by N documents"), "New template" (blank, or start from a built-in: Minimal, Bold header, Classic ledger), themes tab with swatch cards.

### 8.6 Clients — `/clients` (canvas 5)
1. H1 "Clients", "N clients · {Business}", Import CSV, New client.
2. List card: search "Search by name, email or phone", Show filter (All clients / With balance due / Overdue); columns Client (initials avatar, name, email), Last document, Balance, Billed this year. Selected row: primary-soft background + 3 px inset bar.
3. Detail panel: name, "Client since {Month yyyy} · pays in {CUR}", Edit; tiles Balance due, Avg. days to pay; contacts; actions New quote, New invoice, Send statement; Activity timeline.

### 8.7 Catalogue — `/catalogue`
Table of items (Name, Description, Unit, Price, Currency, Tax, HS code, Recurring); the HS code field has the hint "Optional. Needed once fiscalisation is on."; inline add/edit drawer; archive instead of delete when used on documents.

### 8.8 Payments — `/payments` (canvas 6)
1. H1 "Payments", sub "{Month yyyy} · online payments confirm themselves; record cash and bank transfers here", Period select, Export.
2. Tiles per method (EcoCash, Card, Bank transfer) + "ZiG received — kept separate, not converted".
3. Ledger: Date, From · applied to (client + invoice numbers/receipt), Method, Amount, Status (Waiting for Paynow / Confirmed / Failed).
4. Record a payment drawer: Client, Amount, Date, Method (Cash, Bank transfer, Other), "Apply to open invoices" checklist with remaining balances, preview line "INV-xxxx becomes Paid · Receipt RCT-xxxx", "Send receipt to client by WhatsApp", "Save payment".

### 8.9 Recurring — `/recurring`
List of schedules (client, source invoice, cadence, next run, mode Draft/Auto, Paused toggle); editor drawer for cadence, start, end/count, mode.

### 8.10 Business settings — `/settings/{section}` (canvas 7)
Sections: Profile & numbering, Tax & currency, Payment methods (Paynow integration ID/key — key write-only, shows "Saved ••••"), Email & WhatsApp, Reminders, Team & access. Profile: logo tile + Replace; Trading name, Registered name, Email, WhatsApp number, Address. Numbering table: Document, Prefix, Next number (read-only), Example. Tax & currency: default currency, default tax ("No tax (not VAT-registered)"). ZIMRA fiscalisation card dashed with "Later" badge, disabled switch. Footer Discard / Save changes. "+ Add a business" in header.

### 8.11 Client link: invoice — `/d/{token}` (canvas 4)
Business-themed header: logo, download icon button; "Invoice {No} for {Client}", amount due (36 px mono), pill "Due {date}" + "in N days" (or "N days overdue"). Card "Pay this invoice": "Pay with EcoCash" (accent), "Pay by card" (outlined), note "Secured by Paynow. You can also pay part now and the rest later." Invoices with a plan (canvas 11): the header pill reads "Next: {amount} due {date}" (or "Instalment overdue" in bad tone); the pay card offers "Pay next instalment · {amount}" (primary), "Pay full balance · {amount}", "Other amount"; a "Payment plan" card lists each instalment (number, date, amount, StatusPill) with paid ones showing the receipt link. Items card with total. Buttons Save PDF, Ask a question (WhatsApp deep link). Footer "Sent with Invoice Builder". Paid invoices show "Paid in full" and receipt links instead of payment buttons.

### 8.12 Client link: quote — `/d/{token}` (canvas 8)
Header "Quote {No} for {Client}", amount, "Valid until {date}". "What's included" items + total + terms link. "Accept this quote": Your full name, checkbox "I accept the scope, price and terms on behalf of {Client}.", button "Accept quote". A deposit is set up as instalment 1 "On acceptance" (R35). Quotes with a plan show a "Payment plan" card above the form (same list as the invoice, no states) and the button reads "Accept & pay instalment 1 · {amount}" when instalment 1 is due on acceptance (R35), otherwise "Accept quote", "Request changes" (opens a comment field). Footer "Your name and the time of acceptance are recorded on the quote." Expired: "This quote expired on {date}. Ask {Business} for an updated one."

### 8.13 Client link: payment result — `/d/{token}/paid` (canvas 9)
Success: check icon, "Payment received", "Thank you, {Client}. A receipt has also been sent to your {channel}." Details list: Amount paid, Invoice, Receipt, Method, Balance due. "Download receipt", "Back to invoice". Pending: "Waiting for confirmation from Paynow…" with a polite live region and auto-refresh every 5 s for up to 2 min. Failed: "The payment didn't go through" + "Try again".

---

## Phase 2 screens — fiscalisation (spec §9)

Status words added to §5.3 vocabulary for fiscal state (shown with `StatusPill` using the same tones):

| Word | Means | Tone |
|---|---|---|
| Fiscalisation pending | Issued, waiting for the provider | warn |
| Fiscalised | Fiscal data stored | ok |
| Fiscalisation failed | Needs the owner's action | bad |

### 8.14 Settings › Fiscalisation — `/settings/fiscal`
1. Header "Fiscalisation", sub "Send invoices and credit notes to ZIMRA through your fiscalisation provider." Environment pill (Sandbox = info tone with the word "Sandbox", Live = ok tone with "Live").
2. Readiness checklist card, each row with a check or cross icon plus text:
   - Provider connected
   - Taxes synced and mapped
   - Templates include a fiscal block
   - Applies-from date set
   - HS codes covered
   The "Enable fiscalisation" switch sits under the checklist and stays disabled with "Finish the checklist to enable" until every row passes.
3. Provider card: provider select, Device ID, credentials (write-only, "Saved ••••"), environment radio Sandbox / Live (switching to Live asks "Switch {Business} to live fiscalisation? Live receipts are reported to ZIMRA."), "Test connection".
4. Fiscal day card: "Day {N} open since {HH:mm}" or "No day open", receipts today, last error. Buttons Open day / Close day (confirm dialogs), Refresh. Close failures show in danger text with "Try again".
5. Tax mapping table: Your tax rate | Provider tax (select) | Percent | status. "Sync taxes" button and "Last synced {date time}".
6. Defaults: Applies-from date, default HS code (hint "Used when a line has none").

### 8.15 Fiscal panel on issued documents
In the read-only document view (S3), a card under the header:
- **Pending:** warn pill "Fiscalisation pending", "Attempt {n} · next try {HH:mm}". When sent already, the line "Sent before fiscalisation. The fiscal copy will be sent automatically."
- **Fiscalised:** ok pill "Fiscalised" plus the QR thumbnail, verification code (mono), fiscal day, receipt global number, device ID, time. Link "Verify on ZIMRA" opens the QR URL.
- **Failed:** bad pill "Fiscalisation failed", provider message, guidance text from spec F8, buttons "Retry fiscalisation" and "Create credit note".
- Attempts history (collapsible): time, result, message.
- Dashboard "Needs attention" gains: "INV-xxxx couldn't be fiscalised" (Retry) and "Fiscal day didn't close" (Open settings).

### 8.16 Fiscal block in the designer
Insert grid gains "Fiscal block" (QR icon with a shield). Properties:
- Layout (Stacked / Inline)
- QR size (mm)
- Show device ID / day / global number
- Label

The "Preview with sample data" menu gains a state picker: Not fiscal / Pending / Fiscalised. Publishing without the block, when required, shows "Add a fiscal block to the last page. {Business} has fiscalisation on." and the Publish button stays disabled.
