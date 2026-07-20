# Website "About US" — `/about`

| | |
|--|--|
| **Route** | `/about` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual (AR RTL / EN LTR), responsive; static marketing content (no API) |
| **Source** | [`About.razor`](../../../src/Website/SIMF.Web/Components/Pages/About.razor) · [`About.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/About.razor.cs) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`LandingShell.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingShell.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (`ln-pghero` / `ln-values` / `ln-vcard` / `ln-pillars` / `ln-fcard`, + reused `ln-about` / `ln-stats` / `ln-btn`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`About.*` + shared `PageHero.Home`; reuses `Landing.About.*` / `Landing.Stats.*` / `Landing.Hero.Venue` / `Landing.Subnav.Date`) |
| **Data** | None — static marketing prose. The intro block + participation counters reuse the landing's own content (`Landing.Stats`) so those event facts stay single-sourced |
| **Figma** | KSA Maritime Forum — About US (Desktop AR), node `5865-33963` (hero `5865:33965`; values `5963:8168`; stats `6226:8513`; pillars `5865:37439`) |
| **E2E** | [`e2e/web-about.md`](../../tests/e2e/web-about.md) (`E2E-WABT-*`) |

## 1. Purpose

The public **About the Forum** overview for SIMF 2026 — the first page of the
About cluster (About · Objectives · Key Themes · Organizer · Venue). A **Blazor
SSR** page on the shared `ln-` chrome that reproduces the Figma About frame
(`5865-33963`): a blue-gradient photo **interior hero**, a two-column **intro**,
a four-card **values** strip, the **participation-stats** band, and a three-card
**pillars** row.

## 2. Architecture

- **Rendering** — static SSR (no interactive circuit, no API call). All
  interactivity is the shared progressive `landing.js` (loader fade,
  reveal-on-scroll). Fully readable with JS disabled.
- **Shared chrome** — wrapped in [`LandingShell`](../../../src/Website/SIMF.Web/Components/Layout/LandingShell.razor)
  (nav + footer + `<HeadContent>` + `.landing` scope, one copy). The page supplies
  its sections and a `Title` / `Description`, and renders exactly one `<h1>` (the
  hero title) which `Routes.razor`'s `FocusOnNavigate` focuses.
- **Reusable interior hero** — [`LandingPageHero`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor)
  is a presentational component (params `Title` / `Subtitle` / `Crumb` /
  `ImageSrc`) that the whole About cluster reuses. It mirrors the landing's
  `ln-hero2` photo/gradient technique (photo on the inline-END side, blue brand
  gradient with a `[dir=ltr]` flip) and adds the breadcrumb + two event pills.
- **DRY reuse** — the intro (`ln-about`) and stats (`ln-stats`) sections reuse the
  landing's existing markup **and** content (`Landing.About.*` / `Landing.Stats.*`
  resx + the `Landing.Stats` counter list), so the shared event facts have a
  single source of truth. New shared families added to `landing.css`: `ln-pghero`
  (interior hero), `ln-vcard` (values card), `ln-fcard` (white feature card — the
  cluster's "pillars"-style card).

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero | `ln-pghero` | Photo + blue gradient; breadcrumb (Home / About), `<h1>` (`About.Hero.Title`), subtitle (`About.Hero.Subtitle`), two gold-tint pills (venue `Landing.Hero.Venue` + date `Landing.Subnav.Date`) |
| 2 | Intro (2-col) | `ln-about` (reused) | Eyebrow/title/lead (`Landing.About.*`) + a "Partnerships" CTA → the landing partners band (`/#partners`; the dedicated `/partners` page lands in Wave 4), and the forum-hall photo |
| 3 | Values | `ln-values` → `ln-vcard` | Title (`About.Values.Title`) + four cards (Innovation / Integration & communication / Sustainability / Responsibility), each a gold-tint icon circle + centred label |
| 4 | Participation stats | `ln-stats` (reused) | Eyebrow/title/lead (`Landing.Stats.*`) + a navy band of the four `Landing.Stats` counters (2×2) |
| 5 | Forum pillars | `ln-pillars` → `ln-fcard` | Title (`About.Pillars.Title`) + sub (`About.Pillars.Sub`) + three feature cards (Strategic dialogue / Global partnerships / Foreseeing the future), each a gold-tint square icon chip + navy title + gray desc |

**Pillar icons** are three distinct DGA stroke SVGs (`icon-globe.svg` /
`icon-anchor.svg` / `icon-chip.svg`, blue `#244A77`) — not a shared placeholder.
**Values icons** are the shared check-circle glyph (the Figma value cards use an
identical placeholder icon; a per-value glyph set is a follow-up if the client
supplies one).

## 4. Bilingual model (AR RTL / EN LTR)

- **Hero + section headers** → resx `IStringLocalizer<Strings>` (`About.*` +
  shared `PageHero.Home`), following the request culture and the `/culture` switch.
- **Card content** (values, pillars) → `Bilingual` records in `About.razor.cs`
  resolved with `.For(rtl)` for the active culture (Arabic-preferred in RTL).
- **Direction** — `<html dir/lang>` from `App.razor`; the CSS is
  direction-agnostic via logical properties, with an explicit `[dir=ltr]` flip on
  the genuinely mirrored hero gradient art.

## 5. Responsive

`ln-values__grid` / `ln-pillars__grid` step **4/3 → 2 → 1** and **3 → 2 → 1**
columns at ≤900 / ≤520px; the intro stacks below 980px (shared `ln-about` rule);
the hero block goes full-width below 720px. No horizontal overflow at 1440 / 1024
/ 768 / 390 (`scrollWidth == clientWidth` verified in both languages).

## 6. Verification (2026-07-15)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/AboutPageTests.cs` (5, green):
  single-`<h1>` hero + breadcrumb + two pills; the four value cards; the reused
  participation-stats band; the three pillars with their distinct icons; the
  reused landing intro block (CTA → `/#partners`).
- **Live render** — visually verified against Figma at **AR@1440**, **EN@1440**
  (correct RTL→LTR mirror of the hero photo/gradient) and **mobile-390**: hero
  pills, values strip, navy stats band, pillar feature-cards with distinct icons.
  Console clean (only the shared-chrome hero-font preload hint); no horizontal
  overflow.

## 7. Follow-ups (not blockers)

- **DRY refactor (tracked, cross-page).** The intro (`ln-about`) and stats
  (`ln-stats`) sections reuse the landing's data + resx but still copy its section
  **markup**, and `ln-pghero` shares its photo/gradient scaffold with the shipped
  `ln-hero2` (landing) and its breadcrumb/pills idiom with `ln-sesshero` (session
  detail). Extracting shared `<LandingStats>` / `<LandingAboutIntro>` components and
  a single interior-hero base would DRY all three — but each touches **already-shipped**
  pages (their anchor-nav ids, data ownership, hero CSS), so it is deferred to a
  focused refactor pass once the About cluster (pages 2–5) confirms which sections
  and hero variants actually recur. The new `LandingPageHero` component + the data
  reuse (`Landing.Stats`, `Landing.About.*` / `Landing.Stats.*` resx) already capture
  the bulk of the DRY value.
- **Stats count** — the Figma frame shows six counter slots with obvious
  placeholder duplicates (`+500`/`+40` repeated, and دولة/جهة numbers that
  conflict with the homepage). The page renders the **four canonical**
  participation figures from `Landing.Stats` (matching the homepage) rather than
  invent duplicate numbers — confirm the final figure set with the client.
- **Value icons** — the four value cards use the shared check-circle placeholder
  (as the Figma does); swap for a per-value glyph set if the client provides one.
- **Intro photo** — reuses the landing's `about/about-card-1.jpg` (an ~8 MB
  shared asset — a pre-existing landing optimisation, out of scope here).
- Shared follow-up (all `ln-` pages): the hero-font `<link rel="preload">` href is
  fingerprinted while the `@font-face src` url is not (see
  [`speakers.md`](speakers.md) §8 / `landing-rebuild.md` §6).

_Last reviewed:_ 2026-07-15 by Claude (About US page — `ln-` Bootstrap SSR, Figma 5865-33963).
