# Website "The exhibition" — `/programme/exhibition`

| | |
|--|--|
| **Route** | `/programme/exhibition` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual, responsive; static (the floor plan is the exported diagram image) |
| **Source** | [`Exhibition.razor`](../../../src/Website/SIMF.Web/Components/Pages/Exhibition.razor) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (`ln-exhibit` + reused `ln-pghero` / `ln-fsection`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Exhibition.*`; reuses `About.Hero.Subtitle`) |
| **Data** | None — static; the map is a diagram image (`assets/figma/exhibition/exhibition-map.png`) |
| **Figma** | KSA Maritime Forum — Exhibition (Desktop AR), node `5867-23560` (hero `5867:23562`; map card `5867:28574`) |
| **E2E** | [`e2e/web-exhibition.md`](../../tests/e2e/web-exhibition.md) (`E2E-WEXH-*`) |

## 1. Purpose

The accompanying **exhibition floor plan** — the third page of the Programme
cluster. A **Blazor SSR** page on the shared `ln-` chrome: the interior photo-hero
(no breadcrumb), then the exhibition map (numbered stands + zones).

## 2. Architecture

- **Rendering** — static SSR (no API). Shared chrome via `LandingShell`; the hero is
  the reusable `LandingPageHero` (no breadcrumb) and carries the page's single `<h1>`.
- **Floor-plan map** — the Figma map is a **dense booth grid** (hundreds of numbered
  stands + MST/F zones, coffee areas, entrance/exit). Rebuilding it in HTML/CSS would
  be impractical and it is not interactive, so it is rendered as the **exported diagram
  image** (`exhibition-map.png`, 1280×1748) inside a bordered white card (`ln-exhibit`)
  wrapped in a horizontally scrollable container (`ln-exhibit__scroll`). On desktop the
  image fits the card (`width:1232px; max-width:100%`); below 720px it keeps a legible
  `width:900px` and the card scrolls horizontally (the page itself never overflows).
- **Reuse** — the section chrome reuses `ln-fsection` (right-aligned header). New CSS is
  just the small `ln-exhibit` map card. The hero tagline reuses `About.Hero.Subtitle`.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero (no breadcrumb) | `ln-pghero` (via `LandingPageHero`) | `<h1>` (`Exhibition.Hero.Title`) + subtitle (`About.Hero.Subtitle`) + venue + date pills |
| 2 | Floor plan | `ln-fsection` → `ln-exhibit` | Title (`Exhibition.Section.Title`) + sub + the map image (`Exhibition.Map.Alt`) in a scrollable card |

## 4. Bilingual model (AR RTL / EN LTR)

- **Hero + section headers + map alt** → resx (`Exhibition.*` + reused `About.Hero.Subtitle`),
  following the `/culture` switch.
- **Direction** — the reused `ln-fsection` + the centred map are direction-agnostic.

## 5. Responsive

The map card sits within the 1280 content band; below 720px the image keeps a legible
900px width and the `ln-exhibit__scroll` card scrolls horizontally (the page body never
overflows). No horizontal **page** overflow at 1440 / 1024 / 768 / 390
(`scrollWidth == clientWidth` verified in both languages).

## 6. Verification (2026-07-18)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/ExhibitionPageTests.cs` (2, green):
  single-`<h1>` with no breadcrumb; the map image (correct src + accessible alt) in the
  scrollable card.
- **Live render** — visually verified against Figma at **AR@1440** and **EN@1440**: the
  hero + the full floor-plan map (booth grid, zones, coffee/entrance) rendered crisply in
  the card. Console clean; no horizontal page overflow (the map image rendered ~1218px
  inside the card).

## 7. Follow-ups (not blockers)

- **Map is a static image with baked-in Arabic zone labels** (e.g. "المعرض"); the EN view
  shows the same diagram. An **EN/SVG variant** (or an interactive booth map wired to the
  app's `VenueMapNodes`) is a follow-up if per-stand interactivity / bilingual labels are
  wanted. Booth numbers are language-neutral.
- The Figma section subtitle was a lorem placeholder; replaced with real copy.

_Last reviewed:_ 2026-07-18 by Claude (Exhibition page — `ln-` Bootstrap SSR, Figma 5867-23560).
