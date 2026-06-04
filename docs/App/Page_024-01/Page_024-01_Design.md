# Page 024-01 — Design (تفاصيل النسخة · Past-edition detail)

Flutter screen design. Grounded in `Mockup.html` screen 24-01 (line ~1678). RTL,
Arabic-primary. *Flutter wiring is deferred to the coordinated Flutter pass (todo #9).*

## Layout (top → bottom, from the mockup)
1. **App bar** — back chevron + the edition title (`أرشيف 2024`).
2. **Cover banner** — fixed-height image (`coverImageRelativePath`); the **year**
   overlaid bottom-end (`2024`, `dir="ltr"`). Gradient fallback when the cover is null.
3. **Title block** —
   - a small accent label `عنوان الملتقى`,
   - the **edition title** (`titleAr` / `titleEn`),
   - the **نبذة** paragraph (`summaryAr` / `summaryEn`) — hidden when null.
4. **Two info boxes** (row) —
   - **المكان** → `locationAr` / `locationEn`,
   - **الزمن** → `dateLabelAr` / `dateLabelEn`.
   Each box hides when its value is null (never an empty labelled box).
5. **Three counters** (row) — **الفعاليات** (`sessions`) · **الحضور** (`attendees`)
   · **المتحدثون** (`speakers`); numbers `dir="ltr"`.
6. **Deferred sections** (placeholders — §9 / D-273) —
   - **الصور والفيديو** (gallery / video),
   - **عناوين الجلسات** (session titles),
   - **المتحدثون السابقون** (past speakers).
   Render a "coming soon" placeholder; **not** backed by the DTO yet.
7. **Bottom nav** — the five-slot bar (Media-coverage slot active).

## Data binding
- The whole screen binds to **one** `PublicArchiveEditionDetail` (Page_024-01_API E1).
- Null-guard every optional scalar (`summary*`, `location*`, `dateLabel*`, `cover*`):
  hide the box / use the gradient fallback.
- Pick `*Ar` vs `*En` by active locale; year + counter numbers render `dir="ltr"`.
- The deferred sections are static placeholders bound to **no** data until the entity
  models them.

## States
- **Loading** — skeleton (cover bar + title lines + counter boxes) while the read runs.
- **Loaded** — the full edition; deferred sections show "coming soon".
- **Not found** — 404 (archive hidden, or unknown / inactive edition) → a "not found"
  empty state with a back action (Page_024-01_Logic L-2/L-5).
- **Error / offline** — retry state; the detail is not persisted across launches.

## RTL / localization
- Whole screen mirrored RTL; the back chevron follows RTL.
- The **year** + counter numbers render `dir="ltr"` inside Arabic labels.
- All colours, radii and spacing via theme tokens (no raw colours / inline styles in
  the Flutter build).
