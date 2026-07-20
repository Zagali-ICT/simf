# Website "Programme" agenda — `/programme`

| | |
|--|--|
| **Route** | `/programme` - Blazor SSR Razor page (static render, live-data read) |
| **Surface** | Website (public marketing site - `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None - anonymous public read |
| **Status** | ✅ Real - bilingual, responsive; **live** day-grouped agenda + best-effort speakers strip. Re-skinned from the legacy `Simf*` / `MainLayout` page onto the shared `ln-` marketing kit (D-199 content; the data flow is unchanged). |
| **Source** | [`Programme.razor`](../../../src/Website/SIMF.Web/Components/Pages/Programme.razor) + [`Programme.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Programme.razor.cs) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (reused `ln-pghero` / `ln-fsection` / `ln-fsection--dark`; new `ln-agenda*` band) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Programme.*`) |
| **Data** | **Live** - `SimfPublicClient.GetProgrammeSessionsAsync()` (`PublicSessions`) drives the day-grouped agenda; `GetSpeakersAsync()` (`PublicSpeakers`) drives the best-effort speakers strip. Both are anonymous public reads, server-side SSR. A failed sessions read shows the error state; a failed speakers read just omits the strip. |
| **Figma** | No dedicated website frame (all 15 programme-site frames are the sub-pages; Mockup "Agenda" is the Flutter app screen). This is an `ln-`-idiom rebuild reusing the kit (see §7). |
| **E2E** | [`e2e/web-programme.md`](../../tests/e2e/web-programme.md) (`E2E-WPG-*`) |

## 1. Purpose

The forum's **full agenda** - every published session grouped by day, with hall
and local time window, plus a best-effort list of the forum's speakers. A
**Blazor SSR** page on the shared `ln-` chrome that reads the live public API
server-side and degrades gracefully (error / empty / populated) so it never
blanks. It is the canonical live schedule that `/programme/sessions` (the static
plenary teaser) and the Session-Detail "back" link point at.

## 2. Architecture

- **Rendering** - static SSR; the day sections + speakers strip are read from the
  anonymous public API during pre-render (`Programme.razor.cs`), like
  `Speakers.razor` / `Archive.razor`. Shared chrome via `LandingShell`; the hero
  is the reusable `LandingPageHero` (no breadcrumb - the Programme cluster omits
  it) and carries the page's single `<h1>`.
- **Data flow** (unchanged by the re-skin):
  - `GetProgrammeSessionsAsync()` -> `GET /api/v1/app/programme/sessions` ->
    `PublicSessions`. A `null` result (failed envelope / unreachable) sets
    `_error` and the page renders the `ln-agenda__msg` error block, then returns
    before the speakers read.
  - otherwise `BuildDays(...)` groups `Items` by the **event-local (+03:00) date**
    of `StartUtc` (`EventTime.Local(StartUtc).Date`), ordered by day then start
    time, so days bucket in Riyadh time regardless of the server timezone (shared
    with Session Detail). Zero days renders the empty state.
  - `GetSpeakersAsync()` -> `GET /api/v1/app/speakers` is **best-effort**: a
    `null` result leaves the strip empty and never flips the page into error.
  - Both are anonymous reads (no bearer) - the same wire contract the Flutter app
    decodes (D-219; field names/types must not change).
- **Reuse** - the page reuses the shared `ln-fsection` section chrome for all
  three states and `ln-fsection--dark` for the speakers band. Only the row/chip
  elements are page-specific (`ln-agenda*`), added as one additive band in
  `landing.css` (same pattern as `ln-visa` / `ln-gallery`). The day-group spacing
  is the shared `ln-fsection__inner` 32px gap (no override).

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero (no breadcrumb) | `ln-pghero` (via `LandingPageHero`) | `<h1>` (`Programme.Banner.Title`) + subtitle (`Programme.Banner.Subtitle`) + venue + date pills |
| 2 | Live agenda | `ln-fsection` -> `ln-agenda__day` (× N days) | Per day: an `<h2>` day heading + a list of session rows. Each `ln-agenda__row` = time window (`ln-agenda__time`) · title `<h3>` + hall (`ln-agenda__main`) · a conditional neutral theme pill (`ln-agenda__pill`) |
| - | Empty state | `ln-fsection` -> `ln-fsection__head` | `Programme.Empty.Title` + `Programme.Empty.Text` when no sessions are published |
| - | Error state | `ln-fsection` -> `ln-agenda__msg` (`role="alert"`) | `Programme.Error` when the sessions read fails |
| 3 | Speakers strip (navy, best-effort) | `ln-fsection ln-fsection--dark` -> `ln-agenda__spk` (× N) | `Programme.Speakers.Title` + chips: speaker name (`ln-agenda__spkname`) + optional English rank pill (`ln-agenda__spkrank`, `lang="en"`) |

## 4. Bilingual model (AR RTL / EN LTR)

- **Chrome / section copy** -> `Programme.*` resx; follows the `/culture` switch.
- **Session + speaker content** -> the shared `SIMF.Web.Content.LocalizedText.Pick(en, ar)`
  helper: RTL prefers the Arabic value (falls back to English when blank), LTR
  prefers English (falls back to Arabic when blank). The theme name uses
  `PickOrNull`, so a session with no theme in either language renders no pill,
  and a theme named in only one language falls back to that language. Day
  headings + time windows render in event-local (+03:00) time via the shared
  `EventTime` helper.
- **Speaker rank** is stored English-only; it is tagged `lang="en"` so an Arabic
  screen reader pronounces it correctly.
- **Direction** - the reused bands are direction-agnostic (`text-align: start`,
  logical spacing); the hero photo sits inline-end.

## 5. Responsive

The agenda rows are a single-column list; below 640px each row wraps so the time
window takes its own full-width line and the theme pill drops to the start. The
speakers chips wrap at every width. Section padding uses `clamp(16px, 5.5vw,
80px)`. No horizontal overflow at 1440 / 1280 / 1024 / 768 / 390
(`scrollWidth == clientWidth` verified in both languages).

## 6. Verification (2026-07-20)

- **Build** - `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** - `tests/SIMF.Web.Tests/ProgrammePageTests.cs` (3, green):
  the populated agenda + speakers strip (asserts `ln-pghero` / `ln-agenda__row` /
  `ln-agenda__spk`), the empty state (`ln-fsection`), and the API-failure error
  block (`ln-agenda__msg`), with the culture pinned so `Pick()` is deterministic.
- **Live render** - visually verified at **AR@1440** and **EN@1440** with **live
  local data** (3 day groups Fri-Sun 20-22 Nov 2026, 5 sessions, 32 speakers):
  the hero, the day-grouped agenda rows, and the navy speakers chip strip. RTL
  mirrors correctly (hero photo to the left, right-aligned headings, Arabic
  titles/halls/speakers). Console clean (only the site-wide `favicon.ico` 404 and
  the allowed shared-chrome font-preload warning); no horizontal overflow at
  1440/1280/1024/768/390 in both languages.
- **Reachability** - added a "Full agenda" item (`Landing.Nav.Programs.Agenda`) as
  the first entry of the Programs mega-menu, pointing at `/programme`.

## 7. Follow-ups - deferred scope & flags

1. **`PrimaryThemeColor` is unused (intentional).** The theme pill uses the static
   `var(--bg-gray)` neutral rather than the session's `PrimaryThemeColor`.
   Colouring the pill from an arbitrary DB hex would need an inline `style` and
   risks failing text contrast on a dark theme colour, so it is left neutral. A
   future token-mapped theme palette could wire it.
2. **No dedicated Figma frame.** There is no website agenda frame; the band is an
   `ln-`-idiom rebuild reusing the kit. If a design lands later, re-measure.
_(The earlier follow-ups - the code-behind's private `Pick`/time-format
duplication and the server-local grouping, plus the legacy `MainLayout` public
link - were resolved in the 2026-07-20 follow-up; see the changelog.)_

## 8. Changelog

- 2026-07-20 (follow-up) - `Programme.razor.cs` now uses the shared
  `SIMF.Web.Content.LocalizedText.Pick`/`PickOrNull` and a new shared
  `SIMF.Web.Content.EventTime` helper (event-local +03:00 day grouping +
  `HH:mm – HH:mm` window), single-sourced with Session Detail (which was
  refactored onto the same helper). Removed the vestigial legacy `MainLayout`
  public-nav links (`/programme`, `/visit`) + their now-dead `Nav.*` resx keys.
- 2026-07-20 - re-skinned from the legacy `Simf*` / `MainLayout` chrome onto the
  shared `ln-` marketing kit (`LandingShell` + `LandingPageHero` + a new
  `ln-agenda` band); added a "Full agenda" nav item; strengthened the bUnit tests
  to guard the `ln-` DOM. Data flow, wire contract, route and resx keys unchanged.
- 2026-07-06 (D-628) - C# moved to a `Programme.razor.cs` code-behind partial
  (Website clean-code, Phase 5); added bUnit coverage. No wire change.

_Last reviewed:_ 2026-07-20 by Claude (Programme agenda page - `ln-` Bootstrap SSR re-skin; live agenda + speakers strip).
