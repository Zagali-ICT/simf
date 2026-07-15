# Website "Objectives" — `/about/objectives`

| | |
|--|--|
| **Route** | `/about/objectives` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual (AR RTL / EN LTR), responsive; static marketing content (no API) |
| **Source** | [`Objectives.razor`](../../../src/Website/SIMF.Web/Components/Pages/Objectives.razor) · [`Objectives.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Objectives.razor.cs) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`LandingShell.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingShell.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (`ln-fsection` + reused `ln-pghero` / `ln-fcard` [`--raised`]) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Objectives.*` + reused shared `PageHero.Home` / `About.Breadcrumb`) |
| **Data** | None — static marketing content (the six objectives live in `Objectives.razor.cs`) |
| **Figma** | KSA Maritime Forum — Objectives (Desktop AR), node `5865-34626` (hero `5865:34628`; six-objectives section `5865:38988`; card `◆W/ThemeCard`) |
| **E2E** | [`e2e/web-objectives.md`](../../tests/e2e/web-objectives.md) (`E2E-WOBJ-*`) |

## 1. Purpose

The forum's **strategic objectives** overview — the second page of the About
cluster. A **Blazor SSR** page on the shared `ln-` chrome that reproduces the
Figma Objectives frame (`5865-34626`): the shared interior photo-hero, then a
single section listing the forum's **six objectives** as feature cards.

## 2. Architecture

- **Rendering** — static SSR (no interactive circuit, no API call). All
  interactivity is the shared progressive `landing.js`. Fully readable with JS off.
- **Shared chrome + hero** — wrapped in [`LandingShell`](../../../src/Website/SIMF.Web/Components/Layout/LandingShell.razor);
  the hero is the reusable [`LandingPageHero`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor)
  with a **3-level breadcrumb** (Home / About / Objectives) via its optional
  `ParentLabel` / `ParentHref` params (the "About" level links to `/about`). The
  hero carries the page's single `<h1>` (Routes.razor focuses it).
- **Feature-card kit** — the six objectives render as the shared `ln-fcard`
  feature card (gold-tint square icon chip + navy title + gray desc), in the
  `ln-fsection` layout (a light section: right-aligned header + a responsive
  CSS-grid of cards, 3-up → 2 → 1). The Objectives cards use the `ln-fcard--raised`
  variant (a soft `--shadow-card` drop-shadow, per the Figma).
- **Content** — the six objectives are `Bilingual` records in `Objectives.razor.cs`
  (one model, either language via `.For(rtl)`); section headers are `Objectives.*`
  resx keys. The hero backdrop reuses the About cluster's shared photo.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero | `ln-pghero` (via `LandingPageHero`) | Breadcrumb Home / About / Objectives, `<h1>` (`Objectives.Hero.Title`), subtitle, venue + date pills |
| 2 | Six objectives | `ln-fsection` → 6× `ln-fcard ln-fcard--raised` | Title (`Objectives.Section.Title`) + sub (`Objectives.Section.Sub`) + a 3×2 grid of cards: maritime security / supply-chain resilience / energy security / infrastructure protection / digital transformation / international cooperation, each with its own icon |

## 4. Bilingual model (AR RTL / EN LTR)

- **Hero + section headers** → resx `IStringLocalizer<Strings>` (`Objectives.*` +
  reused `PageHero.Home` / `About.Breadcrumb`), following the `/culture` switch.
- **Card content** → `Bilingual` records resolved `.For(rtl)` (Arabic-preferred in RTL).
- **Direction** — `<html dir/lang>` from `App.razor`; CSS is direction-agnostic via
  logical properties, with the hero gradient's explicit `[dir=ltr]` flip.

## 5. Responsive

`ln-fsection__grid` steps **3 → 2 → 1** columns at ≤900 / ≤560px; the hero block
goes full-width below 720px. No horizontal overflow at 1440 / 1024 / 768 / 390
(`scrollWidth == clientWidth` verified in both languages).

## 6. Verification (2026-07-15)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/ObjectivesPageTests.cs` (3, green):
  single-`<h1>` + 3-level breadcrumb (parent link → `/about`); the six raised
  feature cards; each objective's distinct icon.
- **Live render** — visually verified against Figma at **AR@1440**, **EN@1440**
  (correct RTL→LTR mirror; breadcrumb "Home / About / Objectives") and the grid
  reflow; console clean; no horizontal overflow. The `/about` 2-level breadcrumb
  re-checked unchanged after the additive `LandingPageHero` extension.

## 7. Follow-ups (not blockers)

- **DRY (tracked).** `ln-fsection` is the generalised light-section-with-feature-cards
  block (CSS-grid, any card count); the committed About page's `ln-pillars` (flat
  flex row of 3 `ln-fcard`) should migrate onto it in the deferred cluster DRY pass
  (see [`about.md`](about.md) §7). The interior-hero scaffold shares with
  `ln-hero2` / `ln-sesshero` (same deferred pass).
- **Hero backdrop** — reuses the About cluster's shared photo behind the gradient
  (decorative, ~49% width, heavily overlaid); per-page distinct backdrops are a
  trivial swap if wanted.

_Last reviewed:_ 2026-07-15 by Claude (Objectives page — `ln-` Bootstrap SSR, Figma 5865-34626).
