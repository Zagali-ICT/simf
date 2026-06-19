# Page 018 — Design (مقعدي · خريطة الجلوس · My Seat map)

Flutter screen design. Grounded in `Mockup.html` screen 18 (line ~1284). RTL,
Arabic-primary.

> **As built — Figma 898:2873 "Your seat" (2026-06-19, commit `60458a5`,
> device-verified TXZ W09).** Deltas from the Mockup.html design below: the legend
> is forced LTR and reads محجوز · متاح · مقعدك; available seats are transparent
> with a beige outline (not a surface fill), reserved a darker fill, mine gold;
> seats are squares sized to the row width and centred (no horizontal scroll); the
> "إرشادي إلى مقعدي" button uses the exact `iconamoon:location` SVG. The header
> back chevron + app-bar controls follow the shared natural-direction shell
> (owner 2026-06-18), not this frame's left-side chevron.

## Layout (top → bottom, from the mockup)
1. **App bar** — back chevron + title `مقعدي`.
2. **Banner** (`seat-banner`) —
   - a label `الجلسة` + the **session name** (`b`),
   - a **seat badge** (`seat-badge`): `صف <b dir="ltr">B</b>` · `مقعد <b dir="ltr">12</b>`
     — the user's row + seat from `myCell` (omit / "no seat yet" when null).
3. **Hall plan** (`hall`) —
   - a **stage** strip (`stage` → `stage-lbl` `المسرح · STAGE`) pinned at the top,
   - **rows** (`rows`): one row per `rowLabels` entry — a row label (`rl`, e.g. `B`)
     followed by `seatsPerRow` seat cells (`i`). Cell variants:
     - default = **available**,
     - `tk` = **taken / reserved**,
     - `me` = **mine** (brass + glow).
4. **Legend** (`seat-leg`) — as built to frame 907:1591 the legend is forced LTR,
   reading `محجوز` (reserved swatch) · `متاح` (available, beige-outline) ·
   `مقعدك` (mine, gold) — label then swatch, not mirrored with the RTL page.
5. **Actions** (`seat-actions`) —
   - a filled primary **`إرشادي إلى مقعدي`** → Map (15),
   - a secondary **`مشاركة الموقع`** → native share sheet.
6. **Bottom nav** — the five-slot bar (Agenda active).

## Data binding
- **Grid** binds to `SessionSeatMap` (Page_018_API E1): draw `rowLabels.length`
  rows × `seatsPerRow` cells; colour each cell by the status derivation
  (mine = `myCell`, reserved = in `reservedCells`, else available). `kind` lets an
  `AdminReservedRow` cell read as blocked.
- **Banner badge** binds to `myCell.rowLabel` / `myCell.seatNumber`; hide / show a
  "no seat yet" state when `myCell == null`.
- **Scroll/focus** the grid to the highlighted `myCell` on open (the "point to a
  specific one").
- **`إرشادي إلى مقعدي`** → navigate to `/map` (Page_015). **`مشاركة الموقع`** →
  native share. Both client-local (Page_018_API E3).
- **(Optional) picker mode** — tapping an **available** cell calls
  `POST …/seats/reserve`; a free-seat button calls `…/reserve-random`; the held
  cell can be released via `DELETE …/seats/mine`; each re-reads E1 to repaint
  (Page_018_Logic L-4).

## States
- **Loading** — skeleton grid while the (cache-miss) `GET …/seats` runs.
- **Has a seat** — the brass cell + banner badge; legend; both actions enabled.
- **No seat yet** — the grid renders with no highlight; the banner shows "no seat
  yet"; (picker mode, if on, invites a pick).
- **No layout** — `rowLabels` empty / `seatsPerRow` 0 → "seat map not available
  yet" placeholder (admin has not configured the hall layout).
- **Gated / error** — 401/403 → not reachable (auth-gated route); 404 → "session
  removed".
- **Offline** — renders from the cached `SessionSeatMap` carried over from screen
  17; the live repaint after a pick is skipped until back online.

## RTL / localization
- Whole screen mirrored RTL; the back chevron follows RTL.
- The **stage stays at the top**; the rows mirror RTL but row letters + seat
  numbers render `dir="ltr"` inside the Arabic labels (`صف B · مقعد 12`).
- Legend + action labels are bilingual per the active locale.
- The mine seat uses the **gold** accent (beige hairline); reserved = a darker
  navy fill (no border); available = **transparent** with a beige hairline (the
  navyDeep card shows through) — all via theme tokens (no raw colours). Seats are
  squares (≤20px) sized to the row width and centred; no horizontal scroll.
