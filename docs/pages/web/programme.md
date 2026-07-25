# Website "Programme" agenda — `/programme`

| | |
|--|--|
| **Route** | `/programme` - Blazor SSR Razor page (static render, live-data read) |
| **Surface** | Website (public marketing site - `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None - anonymous public read |
| **Status** | ✅ Real - bilingual, responsive; **live** day-grouped agenda with a day strip, a type filter and timeline cards, plus a best-effort speakers strip. |
| **Source** | [`Programme.razor`](../../../src/Website/SIMF.Web/Components/Pages/Programme.razor) + [`Programme.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Programme.razor.cs) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`EventTime.cs`](../../../src/Website/SIMF.Web/Content/EventTime.cs) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (`ln-pghero` + reworked `ln-agenda*` on a dark `ln-fsection--dark` band) · [`landing.js`](../../../src/Website/SIMF.Web/wwwroot/js/landing.js) (`initAgenda`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Programme.*`) |
| **Data** | **Live** - `SimfPublicClient.GetProgrammeSessionsAsync()` (`PublicSessions`) drives the day strip + timeline cards; `GetSpeakersAsync()` (`PublicSpeakers`) drives the best-effort speakers strip. Both are anonymous public reads, server-side SSR. A failed sessions read shows the error state; a failed speakers read just omits the strip. |
| **Design** | Adapted from the app "Programme schedule" (Figma `883-2308`, app file `PSXHhY0UVTAPSaIOf9uNKd`) into the `ln-` kit for desktop + RTL. There is **no dedicated website agenda frame** (see §7). |
| **E2E** | [`e2e/web-programme.md`](../../tests/e2e/web-programme.md) (`E2E-WPG-*`) |

## 1. Purpose

The forum's **full agenda** - every published session grouped by day, presented
as an app-style schedule: a **day strip** switches the visible day, an optional
**type filter** narrows by session kind, and each session is a **timeline card**
(event-local time column + gold category chip + title + hall + description). A
best-effort speakers strip follows. A **Blazor SSR** page on the shared `ln-`
chrome that reads the live public API server-side and degrades gracefully. It is
the canonical live schedule that `/programme/sessions` and the Session-Detail
"back" link point at.

## 2. Architecture

- **Rendering** - static SSR; the sessions + speakers are read from the anonymous
  public API during pre-render (`Programme.razor.cs`), like `Speakers.razor`.
  Shared chrome via `LandingShell`; the hero is the reusable `LandingPageHero`
  (no breadcrumb) and carries the page's single `<h1>`.
- **Day grouping (event-local)** - `BuildDays` groups the sessions by the
  **event-local (+03:00 Riyadh) date** of `Start` (`EventTime.Local(...).Date`,
  shared with Session Detail), ordered by day then start time. Each day gets a
  stable index id (the day strip + JS target it), the localized weekday + date
  number the pill shows, and its ordered sessions.
- **Type filter (data-driven)** - `_types` = the distinct non-null `SessionType`
  values across the sessions. The filter tabs render **only** when at least one
  session carries a type; otherwise only the day strip shows.
- **Progressive enhancement** - `landing.js` `initAgenda` wires the day pills
  (switch the active day) and the type tabs (hide non-matching cards), toggling
  `is-active`/`is-hidden`/`is-empty` + `aria-pressed`, then adds `is-enhanced` to
  the band. The single-day view + the filter are gated on `.is-enhanced`, so with
  **no JS every day and card stays visible and reachable** (SSR/a11y-safe).
- **Bilingual** - session/speaker text uses the shared `LocalizedText.Pick` /
  `PickOrNull`; times/day labels use the shared `EventTime`.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero (no breadcrumb) | `ln-pghero` (via `LandingPageHero`) | `<h1>` (`Programme.Banner.Title`) + subtitle + venue + date pills |
| 2 | Day strip | `ln-agenda__daystrip` (`role="group"`) -> `ln-agenda__daypill` (`aria-pressed`) | One pill per event day: weekday + date number; the active day is gold-accented |
| 3 | Type filter (optional) | `ln-agenda__tabs` (`role="group"`) -> `ln-agenda__tab` (`aria-pressed`) | "All" + one pill per distinct `SessionType` (Workshops / Sessions / Events); renders only when types exist |
| 4 | Schedule label | `ln-agenda__label` (`<h2>`) | `Programme.Agenda.Title` ("Schedule" / "المواعيد") |
| 5 | Per-day timeline | `ln-agenda__day` -> `ln-agenda__card` × N | Per day (`<h3>` date heading): session cards. Each card = a time column (`ln-agenda__when`, aria-hidden: start / gold connector / end) + content (optional gold category chip `ln-agenda__cat`, `<h4>` title, hall, a visually-hidden `ln-agenda__time-sr` window, optional description). A `ln-agenda__none` note replaces an empty day when the filter clears it |
| 6 | Speakers strip (navy, best-effort) | `ln-fsection--dark` -> `ln-agenda__spk` × N | `Programme.Speakers.Title` + chips: name + optional English rank pill (`lang="en"`) |
| - | Empty state | `ln-fsection` -> `ln-fsection__head` | `Programme.Empty.Title` + `.Text` when no sessions are published |
| - | Error state | `ln-fsection` -> `ln-agenda__msg` (`role="alert"`) | `Programme.Error` when the sessions read fails |

## 4. Bilingual model (AR RTL / EN LTR)

- **Chrome / labels** -> `Programme.*` resx (Schedule, day-strip/filter aria, All,
  Type.Workshop/Session/Event, filter-empty), following the `/culture` switch.
- **Session + speaker content** -> the shared `LocalizedText.Pick(en, ar)` (RTL
  prefers Arabic, LTR prefers English; each falls back to the other when blank);
  the category chip + description use `PickOrNull`, so an absent value renders
  nothing. Times + day labels come from the shared `EventTime` (event-local).
- **Speaker rank** is stored English-only; it is tagged `lang="en"`.
- **Direction** - logical properties throughout; the day strip, the tabs and the
  timeline time-column mirror correctly (time column sits inline-start).

## 5. Responsive

The day strip scrolls horizontally if the days overflow; the filter tabs wrap.
Below 640px each timeline card stacks (the time column goes horizontal). Section
padding uses `clamp(16px, 5.5vw, 80px)`. No horizontal overflow at 1440 / 1280 /
1024 / 768 / 390 (`scrollWidth == clientWidth` verified in both languages).

## 6. Verification (2026-07-20)

- **Build** - `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** - `tests/SIMF.Web.Tests/ProgrammePageTests.cs` (4, green):
  the golden agenda (hero + day strip + timeline card + speakers + the event-local
  `12:00 – 13:30` window); a **multi-day, typed** case pinning the day ids, the
  `data-agenda-type`/`data-agenda-cardtype` filter contract and the category chip;
  the empty state; the API-failure error block.
- **Live render** - verified at **AR@wide** and **EN@wide** and **mobile 390**
  with enriched local data (3 days, categories, descriptions, 3 types): the day
  strip (active day gold), the All/Workshops/Sessions/Events filter, the timeline
  cards (gold chip + time column + description), and the RTL mirror (day strip +
  time column to the right). The **day switch** and **type filter** work (Sat ->
  Saturday sessions; Workshops -> only the workshop card); an emptied day shows
  the `ln-agenda__none` note. Console clean (only the allowed shared-chrome
  font-preload warning); no horizontal overflow; the day pills use `aria-pressed`
  (no `role="tab"`).

## 7. Follow-ups — deferred scope & flags

1. **No dedicated website Figma frame.** The design is adapted from the **app**
   "Programme schedule" (`883-2308`); there is no website agenda frame. If one
   lands later, re-measure against it.
2. **Per-day "تفاصيل اليوم" banner (deferred).** The app agenda shows a per-day
   title + banner image. The backend supports it (`PublicProgrammeDay.HasImage`
   via `GET /app/programme/days` + the `ProgrammeDayImage` asset route), but this
   page reads the flat `GetProgrammeSessionsAsync` list and groups client-side, so
   the per-day banner is not shown. Switch to `GetProgrammeDaysAsync` (add it to
   `SimfPublicClient`) to wire the day title + banner live.
3. **Category chip + description + type are data-driven.** The gold chip
   (category, else primary theme), the description and the type filter render only
   when the CP has populated those fields; the seeded sessions are otherwise bare.
   No page change needed - they appear when the data exists.

## 8. Changelog

- 2026-07-20 (redesign) - rebuilt the plain day-grouped list into an app-style
  agenda (day strip + optional type filter + timeline cards on a dark `ln-`
  band), adapted from app Figma `883-2308`; added `initAgenda` (progressive
  enhancement) + the `EventTime.Time` accessor. Live data flow, route and wire
  contract unchanged.
- 2026-07-20 (follow-up) - shared `LocalizedText.Pick`/`PickOrNull` + shared
  `EventTime` (event-local +03:00), single-sourced with Session Detail; removed
  the vestigial legacy `MainLayout` public-nav links + dead resx keys.
- 2026-07-20 - re-skinned from the legacy `Simf*` / `MainLayout` chrome onto the
  shared `ln-` kit (`LandingShell` + `LandingPageHero`).
- 2026-07-06 (D-628) - C# moved to a `Programme.razor.cs` code-behind partial.

_Last reviewed:_ 2026-07-20 by Claude (Programme agenda - `ln-` SSR, app-style day strip + type filter + timeline cards, live data).
