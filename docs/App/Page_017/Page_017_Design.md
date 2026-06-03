# Page 017 — Design (تفاصيل الجلسة · Session detail)

Flutter screen design. Grounded in `Mockup.html` screen 17 (line ~1230). RTL,
Arabic-primary.

## Layout (top → bottom, from the mockup)
1. **App bar** — back chevron + title `تفاصيل الجلسة`.
2. **Header band** (`ag-d-head`) —
   - **index** chip (`02`),
   - **meta** line: weekday · date (`الإثنين · 03 نوفمبر`) `·` time window
     (`09:00 — 10:30`),
   - **title** (`h2`),
   - **tags row** (`ag-d-tags`): a **hall** pill with a pin glyph
     (`القاعة الرئيسية · HALL A`) + a **category** pill (`جلسة رئيسية`).
3. **Body** (`ag-d-body`), stacked sections (`ag-d-sec`):
   - **وصف الجلسة** — heading + description paragraph.
   - **المتحدثون** — heading + speaker cards (`ag-d-spk` → `sp`): round avatar +
     name (`b`) + rank/role (`small`, e.g. `القبطان البحري · RSNF`, host = `المضيف`).
     Each card is tappable → Speaker profile (20).
   - **مقعدي** *(login + reservation only)* — a **brass-bordered** card
     (`ag-d-seat`): a small seat glyph + `الصف <span dir="ltr">B</span> · مقعد
     <span dir="ltr">12</span>` + a sub-line (`تأكد من إبراز بطاقتك عند الدخول`) +
     a trailing `عرض ←` link → My Seat map (18).
   - **CTAs** (`ag-d-cta`): a filled primary **`أضف إلى تقويمي`** (calendar glyph)
     + a secondary **`تذكير`** (clock glyph).
4. **Bottom nav** — the five-slot bar (Agenda active).

## Data binding
- **Header / tags / description / speakers** bind to the **cached
  `PublicSessionListItem`** (Page_017_API E1) — drawn on open with no fetch.
  Optionally refresh the live seat count from `PublicSessionDetail` (E2).
- **مقعدي card** binds to `SessionSeatMap.myCell` (E3): `rowLabel` → `الصف …`,
  `seatNumber` → `مقعد …`. **Render the card only when `myCell != null`** (and the
  caller is an approved signed-in account). The `عرض ←` link routes to
  `/agenda/:sessionId/my-seat`, reusing the same seat-map payload.
- **Category pill** renders only when `categoryName` is present (the "main
  session" / type tag).
- **Speaker card** tap → `/speakers/:speakerId`.
- **أضف إلى تقويمي** → build a calendar event from the cached session → OS
  add-event intent. **تذكير** → schedule a local notification. Both client-local
  (Page_017_API E4).

## States
- **Guest / not-logged-in** — full detail renders; the **مقعدي card and its
  section are absent**; the two CTAs still work (client-local).
- **Approved, no booking** — `myCell == null` → card absent; the rest renders.
- **Approved, with a booking** — the brass seat card shows row + seat.
- **Not found** — a stale cached tap onto a soft-deleted session → the detail
  endpoint 404s → a "session removed / not found" placeholder.
- **Offline** — the whole screen (and both CTAs) renders from the cached session;
  only the optional live seat-count refresh is skipped.

## RTL / localization
- Whole screen mirrored RTL; the back chevron and `عرض ←` follow RTL.
- All text uses the paired AR/EN fields per active locale; times render in the
  device tz.
- Inside the Arabic seat phrase, the **row letter and seat number are `dir="ltr"`**
  (per the mockup) so `B` and `12` read correctly within the RTL line.
