# Page 016 — Design (الجلسات · Sessions)

Flutter screen design. Grounded in `Mockup.html` screen 16 (line ~1193). RTL,
Arabic-primary.

> **Rename (D-271):** the screen title, the bottom-nav label and the filter pills
> are renamed from **الأجندة / Agenda** to **الجلسات / Sessions** (pills:
> `الجلسات القادمة` / `جلسات الفعالية`). Layout + behaviour are unchanged; the API
> route stays `/app/programme/sessions`. The Flutter route constant rename is a
> coordinated follow-up.

## Layout (top → bottom, from the mockup)
1. **App bar** — back chevron + title `الجلسات` *(was `الأجندة`, D-271)*.
2. **Filter pills row** (`ag-tabs`) — two pills *(labels renamed D-271)*:
   - `الجلسات القادمة` (Upcoming) — was `أجندة قادمة`
   - `جلسات الفعالية` (Forum / full) — was `أجندة الفعالية` — the active one filled brass.
3. **Day strip** (`ag-days`) — horizontally scrollable day chips, each = weekday
   abbreviation + date number (SUN 2 … SAT 8); the selected day filled brass.
4. **Search field** (`ag-search`) — leading 🔎 glyph, placeholder "search".
5. **Session list** (`ag-list`) — vertical, each row (`ag-item`):
   - leading **time** column (`8:00 AM`),
   - **index number** chip (`01`, `02`, …) + **title** (`h5`),
   - **short description** line (`p`, 2-line clamp),
   - trailing chevron `←`.
   - The active/next session row uses the **brass background** variant (`ag-item on`).
6. **Bottom nav** — the five-slot bar (Home · **Sessions (active)** · Badge FAB ·
   Map · Media). The active slot's label reads `الجلسات` *(renamed from `الأجندة`,
   D-271)*.

## Data binding
- Bind the list to the **cached `PublicSessions`** (one fetch). The pills / day
  strip / search mutate a **client-side filtered view** of the cache — no refetch.
- Row: `time = StartUtc` (device-local), `index` = a client sequence (or `Code`),
  `title = Title|TitleArabic` per locale, `description = Description|DescriptionArabic`.
- A type chip (when `categoryName` is present) renders the "main session" / type
  tag; the theme colour (`primaryThemeColor`) tints the row accent.
- **Speaker flag + avatar (D-271)** — each cached `speakers[]` entry carries
  `countryId` + `countryNameEn`/`countryNameAr` + `photoRelativePath`. Wherever a
  speaker is shown (the detail-preview / any speaker mini-row), render the
  **country flag from `countryId`** (name as tooltip / fallback) and the **avatar
  from `photoRelativePath`** (placeholder when null). The list is fed from the
  same cache, so the detail (17) draws these with no extra fetch.
- Tapping a row → Session detail (17) — can render immediately from the cached
  item (body + speakers, incl. flag + avatar, are present) while refreshing the
  live seat count.

## States
- **Loading** — skeleton rows while the first (cached-miss) fetch runs.
- **Empty** — "no sessions" placeholder (empty programme, or a day/search with no
  matches).
- **Error** — the one fetch failed and no cache exists → retry affordance.
- **Offline** — renders from the cached programme (the whole point of fetch-once).

## RTL / localization
- Whole screen mirrored RTL; the day strip scrolls right-to-left.
- All text uses the paired AR/EN fields per active locale; times in device tz.
- The active-session highlight + the chevron direction follow RTL.
- The speaker **country name** uses `countryNameAr` / `countryNameEn` per locale;
  the **flag** (from `countryId`) and the **avatar** (`photoRelativePath`) are
  locale-neutral graphics. The renamed title `الجلسات` / `Sessions` and the pills
  follow the active locale (D-271).
