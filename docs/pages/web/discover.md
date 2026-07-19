# Website "Discover Saudi Arabia" — `/discover`

| | |
|--|--|
| **Route** | `/discover` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual, responsive; static (reuses the landing's destinations band, single-sourced) |
| **Source** | [`Discover.razor`](../../../src/Website/SIMF.Web/Components/Pages/Discover.razor) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (reused `ln-pghero` / `ln-discover` / `ln-dcard`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Discover.*` for the hero; reused `Landing.Discover.Title` / `Landing.Discover.Desc` for the band) |
| **Data** | Single-sourced static — the six destinations come from `Landing.DiscoverCards` (`public static readonly` in [`Landing.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Landing.razor.cs)). No API. |
| **Figma** | KSA Maritime Forum — Discover Saudi (Desktop AR), node `5867-29747` — **hero + cards are placeholders** (see §7) |
| **E2E** | [`e2e/web-discover.md`](../../tests/e2e/web-discover.md) (`E2E-WDS-*`) |

## 1. Purpose

The forum's **Discover Saudi Arabia** page — the visitor-facing page that showcases
the destinations across the Kingdom to explore around the event. A **Blazor SSR**
page on the shared `ln-` chrome: the interior photo-hero (no breadcrumb), then the
landing's destinations band — six place cards (photo + name + driving distance from
the Riyadh venue + region) — given their own dedicated page.

## 2. Architecture

- **Rendering** — static SSR (no API). Shared chrome via `LandingShell`; the hero is
  the reusable `LandingPageHero` (no breadcrumb — Discover is a single-page cluster,
  like the Programme-cluster heroes) and carries the page's single `<h1>`.
- **Reuse, not copy** — the destinations band reuses the landing's `ln-discover` /
  `ln-dcard` markup + CSS, walking the single-sourced `Landing.DiscoverCards` (the
  six destinations) so the dedicated page and the landing band never drift. The
  section title + description reuse `Landing.Discover.Title` / `Landing.Discover.Desc`.
  No new CSS, no new JS, no code-behind, no new assets — only four page-specific
  `Discover.*` hero/meta resx keys.
- **Content** — the hero copy (`Discover.Hero.Title` / `Discover.Hero.Subtitle`) is
  this page's own; the destinations band is single-sourced from the landing. The hero
  backdrop reuses the shared cluster photo (`assets/figma/about/about-hero.jpg`).

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero (no breadcrumb) | `ln-pghero` (via `LandingPageHero`) | `<h1>` (`Discover.Hero.Title`) + subtitle (`Discover.Hero.Subtitle`) + venue + date pills |
| 2 | Destinations grid | `ln-discover` → `ln-dcard` × 6 | Title (`Landing.Discover.Title`, `<h2>`) + desc; six destination cards (photo + `<h3>` name + distance + region) from `Landing.DiscoverCards` |

## 4. Bilingual model (AR RTL / EN LTR)

- **Hero copy** → `Discover.*` resx; **band copy** → reused `Landing.Discover.*` resx;
  all follow the `/culture` switch.
- **Card content** → `Landing.DiscoverCards` `Bilingual` records resolve `.For(rtl)`
  off `CurrentUICulture` (Arabic in RTL, English in LTR); the distance string is a
  shared numeric label.
- **Direction** — the hero photo sits inline-end (left in RTL, right in LTR); the
  card grid flows in the reading direction; headings/desc are `text-align: start`.

## 5. Responsive

The `ln-discover__grid` is a CSS grid: 3 columns ≥1000px, 2 columns ≤1000px, 1 column
≤640px (`landing.css`). Section padding uses `clamp(16px, 5.5vw, 80px)`. No horizontal
overflow at 1440 / 1280 / 1024 / 768 / 390 (`scrollWidth == clientWidth` verified in
both languages; zero elements exceed the viewport).

## 6. Verification (2026-07-19)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/DiscoverPageTests.cs` (3, green):
  single-`<h1>` hero with no breadcrumb + two pills; the reused `ln-discover` band
  (6 cards, real destination labels, h1→h2→h3 order); the "Explore" CTA omitted.
- **Live render** — visually verified at **AR@1440** and **EN@1440** (correct RTL→LTR
  mirror): the hero + the six destination cards (photo + name + distance + region).
  Console clean; no horizontal overflow; grid collapses 3→2→1 columns cleanly.

## 7. Follow-ups — content & deliberate deviations

The Figma frame `5867-29747` ships **placeholder content** — a generic "المعرض"
(exhibition) hero with the standard forum blurb, and six identical "800 Km / جدة"
cards. This page replaces both with real content:

1. **Deviation (a) — a real hero, distinct from the section title.** The placeholder
   Figma hero is replaced by a real `Discover.Hero.Title` ("Welcome to the Kingdom of
   Saudi Arabia") kept distinct from the section `<h2>` ("Discover Saudi Arabia") so
   the page keeps a clean `h1 → h2 → h3` heading order.
2. **Deviation (b) — the self-referential "Explore Saudi Arabia" CTA is omitted.** The
   landing's destinations band carries an "Explore Saudi Arabia" button (its link to
   *this* page); on the dedicated page itself it would be self-referential, so it is
   dropped. The landing keeps its button.
3. **Deviation (c) — real destinations.** The six placeholder cards are replaced by
   the landing's six real destinations (`Landing.DiscoverCards`: AlUla, Historic
   Diriyah, Historic Jeddah, NEOM, The Red Sea, Edge of the World) with real driving
   distances from the Riyadh venue.

**Nav wiring (owner decision).** The "Discover Saudi Arabia" mega-menu
(`LandingChrome.cs`) has four sub-items — About Saudi Arabia, Invest in Saudi Arabia,
Saudi spirit, Made in Saudi Arabia — all Saudi-nation-brand placeholders (`#`), and
"Made in Saudi Arabia" currently points at the landing `#discover` anchor. **None
semantically means "discover destinations,"** so the nav is left untouched: wiring a
menu entry to `/discover` (and whether the menu should also cover the About/Invest/
Spirit pages as they are built) is an owner decision, not a guess. The page is
reachable by direct URL today.

**Nit (shared band).** The card image `alt` repeats the card `<h3>` name, and the
hero photo is the shared forum-hall image rather than a Saudi-destination photo — both
are inherited verbatim from the shared landing band and are kept byte-identical rather
than diverged on this page alone; address them once on the shared band (or with a
destination hero asset) in a future pass.

_Last reviewed:_ 2026-07-19 by Claude (Discover Saudi Arabia page — `ln-` Bootstrap SSR, Figma 5867-29747).
