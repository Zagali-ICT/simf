# Website speakers listing — `/speakers`

| | |
|--|--|
| **Route** | `/speakers` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual (AR RTL / EN LTR), responsive; live speaker data via `SimfPublicClient` |
| **Source** | [`Speakers.razor`](../../../src/Website/SIMF.Web/Components/Pages/Speakers.razor) · [`Speakers.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Speakers.razor.cs) · [`LandingShell.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingShell.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (`ln-pagehero` / `ln-spklist` / `ln-spkcard` / `ln-ico`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Speakers.*` keys) |
| **Data** | `GET /api/v1/speakers` (anonymous) → `PublicSpeakers.Items` of `PublicSpeakerSummary` |
| **Figma** | KSA Maritime Forum — Speakers (Desktop AR), node `5840-26779` (event band `5840:26981`; card `5840:26994`; card content `5840:26996`) |
| **E2E** | [`e2e/web-speakers.md`](../../tests/e2e/web-speakers.md) (`E2E-WSPK-*`) |

## 1. Purpose

The public **speakers & participants** listing for SIMF 2026, delivered as a
**Blazor SSR** Razor page on the shared `ln-` marketing chrome. It reproduces the
Figma Speakers frame (`5840-26779`): a white **event page-title band** (logo
lockup + theme + date/time/venue) over a responsive **grid of speaker cards**,
each a ringed gradient portrait with the speaker's name, a gold role pill and a
country row — bound to live data from the anonymous public API.

## 2. Architecture

- **Rendering** — static SSR (no interactive circuit). Speaker data is fetched
  **server-side** during render in `OnInitializedAsync`; there is no client
  round-trip. All interactivity is the shared progressive `landing.js`
  (page-loader fade, reveal-on-scroll). Fully readable with JS disabled.
- **Shared chrome** — wrapped in [`LandingShell`](../../../src/Website/SIMF.Web/Components/Layout/LandingShell.razor),
  so the nav header, footer, `<HeadContent>` asset injection (culture-chosen
  Bootstrap sheet + `landing.css`/`landing.js`), splash loader and `.landing`
  scope are one shared copy — **not** duplicated per page. The page supplies only
  its two `<section>`s and a `Title` / `Description`. It renders exactly one
  `<h1>` (the band theme), which `Routes.razor`'s `FocusOnNavigate` focuses.
- **Scoping** — every style is scoped under `.landing` and prefixed `ln-`. The
  band (`ln-pagehero`), grid (`ln-spklist` / `ln-spklist-grid`) and card
  (`ln-spkcard`) are shared `ln-` families in `landing.css`, reused by the other
  public pages that show the same event band.
- **Data** — `[Inject] SimfPublicClient` → `GetSpeakersAsync()` →
  `GET /api/v1/speakers` (anonymous, no bearer token). `SpeakerList = result?.Items ?? []`:
  a `null` result (failed envelope / unreachable service) maps to an **empty
  list**. This page has **no error state** — zero speakers renders the empty-state
  paragraph, exactly like a successful empty list.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Event page-title band | `ln-pagehero` | Logo lockup + `<h1>` theme (`Speakers.Band.Theme`) + three meta rows date/time/venue (`Speakers.Band.Date` / `.Time` / `.Venue`), each with a navy `.ln-ico` |
| 2 | Speaker grid | `ln-spklist` → `ln-spklist-grid` | Section header (`Speakers.Section.Title` + `.Desc`) then a `@foreach` of `ln-spkcard`, or the `Speakers.Empty` paragraph when the list is empty |

**Per card** (`ln-spkcard`):

| Element | Class | Rule |
|---------|-------|------|
| Photo | `ln-spkcard__photo` | 296px ringed gradient box (`box-shadow 0 0 0 5px white`, gradient `#c7dceb→#f8ebce`). Real portrait `<img>` when a photo exists, else the gradient backdrop only |
| Name | `ln-spkcard__name` | Almarai ExtraBold 18px, navy `--ink-display` (#1f2a37), centred; Arabic-preferred in RTL |
| Role pill | `ln-spkcard__role` | Full-width gold pill (`--gold-light` bg, `--gold` border, `--gold-dark` 12px text) — **only when `Rank` is present** |
| Location | `ln-spkcard__loc` | Gray pin (`.ln-ico`, #545555) + country, Almarai Bold 12px — **only when the country is present** |

**Photo source** (`PhotoUrl`): `HasPhotoAsset` ⇒ `/content/assets/SpeakerPhoto/{id}/image`
(same-origin media route, mirrors `SiteContentEndpoints.MapSpeakers`); else the
legacy `PhotoRelativePath`; else empty (gradient-only card).

## 4. Bilingual model (AR RTL / EN LTR)

- **Band + chrome + section headers** → resx `IStringLocalizer<Strings>`
  (`Speakers.*` keys), following the request culture and the `/culture` switch.
- **Speaker fields** → `DisplayName` is Arabic-preferred in RTL (`NameArabic` when
  present, else `Name`) and English-preferred in LTR; `LocationName` is the
  country only (`CountryNameAr` in RTL / `CountryNameEn` in LTR — the public API
  exposes country, not city).
- **Direction** — `<html dir/lang>` from `App.razor`; the CSS is
  direction-agnostic via logical properties.

## 5. Recolorable icons (`.ln-ico`)

The DGA line-glyphs (`secondnav/icon-calendar.svg`, `icon-clock.svg`,
`icon-location.svg`) ship as **white** strokes (built for the dark nav bar), so on
the white band / light card they rendered invisible. The shared `.ln-ico` reuses
the same glyph as an **alpha `mask`** painted with a token colour — **navy**
(`#001640`) on the band, **gray** (`#545555`) on the card pin — so one asset
serves both colours. Reused by every `ln-` page that shows the event band.
(Fix: commit `9ccb9542`.)

## 6. Responsive

`ln-spklist-grid` steps **4 → 3 → 2 → 1** columns at 1440 / ≤1100 / ≤860 / ≤560px.
Below 860px the band stacks (`ln-pagehero__inner` → `flex-direction: column-reverse`,
logo above the theme block); below 560px the band + grid padding tighten and the
type down-sizes. No horizontal overflow at 1440 / 1024 / 768 / 390
(`scrollWidth == clientWidth` verified in both languages).

## 7. Verification (2026-07-15)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/SpeakersPageTests.cs` (5, green):
  populated grid, empty state, failure-degrades-to-empty, media-asset photo route,
  conditional role-pill / location.
- **Live render (prod data)** — 32 real speakers + portraits from the prod API;
  visually verified against Figma at **AR@1440**, **EN@1440** and **mobile-390**:
  band (navy meta icons + logo lockup), cards (ringed gradient photo, navy name,
  gold pill, gray country pin), gradient placeholder for photoless speakers.
  Console clean (only the shared-chrome hero-font preload hint); no horizontal
  overflow.

## 8. Follow-ups (not blockers)

- The public API exposes **country** only; a city-level location field would need
  an additive backend change (tracked, not required for parity).
- The band meta strings (`Speakers.Band.*`) are static resx copy (theme / dates /
  venue). If the event details become CMS-editable they can move to a
  `ContentBlocks` feed like the landing `hero.*` keys.
- Shared follow-up (all `ln-` pages): the hero-font `<link rel="preload">` href is
  fingerprinted while the `@font-face src` url is not, so the 700-weight file is
  fetched-but-unused on cold load — align the two URLs in `LandingShell` (see
  [`landing-rebuild.md`](landing-rebuild.md) §6).

_Last reviewed:_ 2026-07-15 by Claude (Speakers page — `ln-` Bootstrap SSR, Figma 5840-26779).
