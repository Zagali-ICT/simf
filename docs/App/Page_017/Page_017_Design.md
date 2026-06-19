# Page 017 — Design (تفاصيل الجلسة · Session detail)

Flutter screen design. Grounded in `Mockup.html` screen 17 (line ~1230). RTL,
Arabic-primary.

## Layout (top → bottom, from the mockup)
1. **App bar** — back chevron + title `تفاصيل الجلسة`.
2. **Header card** (frame 889:2716, navy `#192B41`) —
   - **title** (`h2`) + a gold 40×40 **index** badge (`02`, LTR) on the same row,
   - **meta** line: time window (`09:00 — 10:30`, LTR) `·` weekday · date
     (`الإثنين · 03 نوفمبر`),
   - **action row** (frame 889:2715): **`رابط الجلسة`** (beige hairline, white —
     shown **only when `liveStreamUrl` is non-null**, opens Live 25) + **`ملخص
     الجلسة`** (gold hairline, gold text — always shown, opens AI summary 34). The
     prior hall/category tag pills are **removed** in the restructured frame.
3. **Body** (`ag-d-body`), stacked sections (`ag-d-sec`):
   - **وصف الجلسة** — heading + description paragraph (hidden when null).
   - **المتحدثون** — heading + speaker cards (frame 889:2722): a 40×40 rounded
     **photo** (`photoRelativePath` via the `SpeakerPhoto` asset, beige hairline,
     person-glyph fallback) at the inline-end, with the name (`b`) + the **country
     flag** emoji (from `countryId`, D-271 — rendered by `core/country_flag.dart`)
     over the rank (`small`, e.g. `القبطان البحري`, host = `المضيف`). Each card is
     tappable → Speaker profile (20).
   - **اسأل المحاور** (frame 1056:12876) — a full-width navy card (centred user
     glyph over the label) shown to everyone → Send question (26); a guest tapping
     it is routed to sign-in by the auth gate.
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
  `/sessions/:sessionId/my-seat`, reusing the same seat-map payload.
- **Action buttons** — **`رابط الجلسة`** renders only when `liveStreamUrl` is
  non-null → `/live?sessionId={id}` (Live 25); **`ملخص الجلسة`** always renders →
  `/ai-summary?sessionId={id}` (AI summary 34). The prior category/hall tag pills
  are gone (restructured frame).
- **`اسأل المحاور`** card → `/live/question?sessionId={id}` (Send question 26);
  always rendered, the route's own auth gate routes a guest to sign-in.
- **Speaker card** binds to the cached `speakers[]`: **avatar** ←
  `photoRelativePath` (placeholder when null), **flag** ← `countryId` (the client
  maps the id → a flag asset; hide when null), **country label / tooltip** ←
  `countryNameAr` / `countryNameEn` per locale (D-271). Tap → `/speakers/:speakerId`.
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
- **Live / recorded affordance (links out, D-271)** — when the refreshed detail
  has a non-null `liveStreamUrl` the screen exposes a **LIVE** entry into the live
  player (**screen 25**); a recorded session links to the same screen for playback
  + the AI summary (محضر). Questions deep-link to **screen 26** (open only in the
  5-min-before → end window — Page_017_Logic L-9). These are navigation targets,
  not rendered inline on screen 17.

## RTL / localization
- Whole screen mirrored RTL; the back chevron and `عرض ←` follow RTL.
- All text uses the paired AR/EN fields per active locale; times render in the
  device tz.
- Inside the Arabic seat phrase, the **row letter and seat number are `dir="ltr"`**
  (per the mockup) so `B` and `12` read correctly within the RTL line.
- The speaker **flag** (from `countryId`) and **avatar** (`photoRelativePath`) are
  locale-neutral graphics; the **country name** uses `countryNameAr` /
  `countryNameEn` per locale (D-271).
