# Page 019 — Design (المتحدثون · Speakers list)

Flutter screen design. Grounded in `Mockup.html` screen 19 (line ~1334). RTL,
Arabic-primary.

## Layout (top → bottom, from the mockup)
1. **App bar / header** — title `المتحدثون`.
2. **Speakers list** (`sp-list`) — a **vertical**, scrollable column of speaker
   cards (`sp-card`), one per `items` entry, in `displayOrder`. Each card holds:
   - an **avatar** (`⚓` / `★`) — `photoRelativePath` when present, else a
     placeholder glyph,
   - a **rank line** (e.g. `القبطان البحري`) — the `rank`,
   - the speaker **name** (`nameArabic` in Arabic, `name` in English),
   - a **`المزيد` / More** link → the profile (20).
3. **Bottom nav** — the five-slot bar.

## Data binding
- **List** binds to `PublicSpeakers.items` (Page_019_API E1): render
  `items.length` cards **in the order received** — no client re-sort
  (Page_019_Logic L-2).
- **Avatar** binds to `summary.photoRelativePath`; show the placeholder (`⚓` / `★`)
  when empty.
- **Rank line** binds to `summary.rank`; **name** binds to `summary.nameArabic` /
  `summary.name` per locale; an optional country label binds to
  `summary.countryNameAr` / `summary.countryNameEn`.
- **Tap / `المزيد`** → navigate to `/speakers/{summary.id}` (Page_020) — passing
  only the id (Page_019_Logic L-3); the profile fetches its own detail.

## States
- **Loading** — a skeleton list of card placeholders while `GET /app/speakers`
  runs.
- **Loaded** — the vertical `sp-card` list, ordered by `displayOrder` then name.
- **Empty** — `items` empty → an **empty state** ("no speakers yet"), not an
  error (Page_019_Logic L-5).
- **No photo (per card)** — the placeholder avatar (`⚓` / `★`).
- **Error / offline** — the standard list error / retry state.

## RTL / localization
- Whole screen mirrored RTL; the list scrolls vertically.
- The card name uses `nameArabic` in Arabic and `name` in English; the rank line
  and any country label follow the active locale.
- The avatar and `المزيد` affordance mirror to the RTL side per the mockup
  `sp-card` layout.
- Avatar placeholder and rank/name styling use theme tokens (no raw colours).
