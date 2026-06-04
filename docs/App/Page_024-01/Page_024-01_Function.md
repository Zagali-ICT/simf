# Page 024-01 — Function (تفاصيل النسخة · Past-edition detail)

What the user does on this screen. Grounded in `Mockup.html` screen 24-01 (line
~1678) — the detail opened from the Archive list (screen 24) `اعرف المزيد ←` link.

## Privilege / auth gate
**Anonymous — public.** Like the Archive list (24), the per-edition detail is an
open read; no login is required. The only gate is the **archive-visibility
operations toggle** (D-166): while the toggle is off, the detail returns 404 (the
whole Archive surface is hidden until an edition is published), exactly like the
list returns an empty set.

## Elements (top → bottom, from the mockup)
1. **Header** — back chevron + the edition title (`أرشيف 2024`).
2. **Cover banner** — the edition cover image (`CoverImageRelativePath`) with the
   **year** overlaid (`2024`).
3. **Title block** —
   - a label `عنوان الملتقى`,
   - the **edition title** (`TitleAr` / `TitleEn`, e.g. *الملتقى البحري السعودي الدولي 2024*),
   - the **نبذة** summary paragraph (`SummaryAr` / `SummaryEn`).
4. **Two info boxes** —
   - **المكان** (place) → `LocationAr` / `LocationEn` (e.g. *الرياض · واجهة الرياض*),
   - **الزمن** (date label) → `DateLabelAr` / `DateLabelEn` (e.g. *نوفمبر 2024 · 3 أيام*).
5. **Three counters** — **الفعاليات** (`Sessions`) · **الحضور** (`Attendees`) ·
   **المتحدثون** (`Speakers`).
6. **Deferred sections** (sketched in the mockup, not yet backed by data — §9 / D-273):
   - **الصور والفيديو** (gallery / video),
   - **عناوين الجلسات** (session titles),
   - **المتحدثون السابقون** (past speakers).
7. **Bottom nav** — the five-slot bar (Media-coverage slot active).

## What the user does
1. **Open one edition** — from the Archive list (24), tap a year card's
   `اعرف المزيد ←` → this detail, addressed by the edition id.
2. **Read the edition** — title, summary, place, date label and the three counters,
   all from **one** `GET /app/archive/{id}` call (Page_024-01_Logic L-1).
3. **Go back** — the back chevron returns to the Archive list.

The gallery / session-titles / past-speakers blocks render as **"coming soon"**
placeholders until the entity models them (Page_024-01_Logic L-3).

## Acceptance criteria
- The screen opens **without login** when the archive is visible; a hidden archive
  (toggle off) or an unknown / inactive edition yields a **404** "not found" state.
- The cover, year, title, summary, place, date label and the three counters all
  render from the single `GET /app/archive/{id}` payload.
- Arabic-primary / RTL; the year renders `dir="ltr"`.
- The deferred sections show a clear placeholder, not broken/empty rows.

## Where it fits in the journey
**Journey — Content & events**: Archive list (24) → **Past-edition detail (24-01)**.
A leaf read screen; its only outbound action is *back* to the list.
