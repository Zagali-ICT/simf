# Website "Partners & sponsors" — `/partners`

| | |
|--|--|
| **Route** | `/partners` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual, responsive; static (reuses the landing's partners + sponsors bands as single-sourced placeholder content, see §7) |
| **Source** | [`Partners.razor`](../../../src/Website/SIMF.Web/Components/Pages/Partners.razor) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (reused `ln-pghero` / `ln-pband` / `ln-pcard` / `ln-spon` / `ln-scard2`) · [`landing.js`](../../../src/Website/SIMF.Web/wwwroot/js/landing.js) (reused `initSponsors` carousel) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Partners.*` for the hero; reused `Landing.Partners.*` / `Landing.Sponsors.*` for the two bands) |
| **Data** | Single-sourced static — the government partners come from `Landing.PartnerLogos`; the sponsors from `Landing.Sponsors` (both `public static readonly` in [`Landing.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Landing.razor.cs)). No API (see §7). |
| **Figma** | KSA Maritime Forum — Companies / Partners & Sponsors (Desktop AR), node `5866-40017` |
| **E2E** | [`e2e/web-partners.md`](../../tests/e2e/web-partners.md) (`E2E-WPT-*`) |

## 1. Purpose

The forum's **partners & sponsors** page — the first page of the Partners cluster.
A **Blazor SSR** page on the shared `ln-` chrome: the interior photo-hero (no
breadcrumb), then the two showcase bands the landing already carries — the
government-partner grid and the sponsors carousel — given their own dedicated
page. This is the destination the About-cluster nav "Partnerships" item and the
About page CTA now point at (they previously scrolled to the landing's own
`#partners` band).

## 2. Architecture

- **Rendering** — static SSR (no API). Shared chrome via `LandingShell`; the hero is
  the reusable `LandingPageHero` (no breadcrumb — the Partners cluster is a single
  page, like the Programme-cluster heroes) and carries the page's single `<h1>`.
- **Reuse, not copy** — both bands reuse the landing's shared CSS, JS and content:
  - **Partners** — `ln-pband` / `ln-pcard` markup + CSS, walking `Landing.PartnerLogos`
    (the four government entities) so the dedicated page and the landing band never
    drift. Section copy is the reused `Landing.Partners.Title` / `Landing.Partners.Desc`.
  - **Sponsors** — `ln-spon` / `ln-scard2` carousel + the reused `initSponsors`
    prev/next scroll JS, walking `Landing.Sponsors`. Section copy is the reused
    `Landing.Sponsors.Title` / `Landing.Sponsors.Desc`; the tier tag is
    `Landing.Sponsors.Tag`.
  - No new CSS, no new JS, no code-behind, no new assets — only four page-specific
    `Partners.*` hero/meta resx keys.
- **Content** — the hero copy (`Partners.Hero.Title` / `Partners.Hero.Subtitle`) is
  this page's own; everything below is single-sourced from the landing. The hero
  backdrop reuses the shared cluster photo (`assets/figma/about/about-hero.jpg`).

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero (no breadcrumb) | `ln-pghero` (via `LandingPageHero`) | `<h1>` (`Partners.Hero.Title`) + subtitle (`Partners.Hero.Subtitle`) + venue + date pills |
| 2 | Partners grid | `ln-pband` → `ln-pcard` × 4 | Title (`Landing.Partners.Title`) + desc; four government-entity cards (logo + gray label) from `Landing.PartnerLogos`; a gold progress rail |
| 3 | Sponsors carousel | `ln-spon` → `ln-scard2` × N | Title (`Landing.Sponsors.Title`) + desc; a horizontally-scrolled strip of sponsor cards (external-link icon + logo + tier tag) from `Landing.Sponsors`, with prev/next arrows |

## 4. Bilingual model (AR RTL / EN LTR)

- **Hero copy** → `Partners.*` resx; **band copy** → reused `Landing.Partners.*` /
  `Landing.Sponsors.*` resx; all follow the `/culture` switch.
- **Card content** → `Landing.PartnerLogos` / `Landing.Sponsors` `Bilingual` records
  resolve `.For(rtl)` off `CurrentUICulture` (Arabic in RTL, English in LTR).
- **Direction** — the hero photo sits inline-end (left in RTL, right in LTR). The two
  bands' internal strips are `direction: ltr` scroll rails (logos read left-to-right
  in both cultures); their headings/desc are `text-align: start`.

## 5. Responsive

Both bands are internal horizontal-scroll strips (`ln-pband__strip` /
`ln-spon__viewport`, `overflow-x: auto`), so the four 360px partner cards and the
sponsor cards scroll within their band rather than pushing the page wide. Section
padding uses `clamp(16px, 5.5vw, 80px)`. No horizontal overflow at 1440 / 1280 /
1024 / 768 / 390 (`scrollWidth == clientWidth` verified in both languages; zero
elements exceed the viewport outside the two intentional scroll strips).

## 6. Verification (2026-07-19)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/PartnersPageTests.cs` (3, green):
  single-`<h1>` hero with no breadcrumb + two pills; the reused `ln-pband` grid
  (4 cards, real government labels); the reused `ln-spon` carousel (8 cards, arrows)
  with the "View all" CTA omitted.
- **Live render** — visually verified at **AR@1440** and **EN@1440** (correct
  RTL→LTR mirror): the hero, the four government-emblem cards + gold rail, and the
  sponsor carousel. Console clean (only a benign shared-chrome font-preload warning);
  no horizontal overflow; verified stacking at the narrow breakpoint.

## 7. Follow-ups — content & deliberate deviations

This page reuses the landing's two shipped bands, so it inherits their current
**placeholder-data** state. Two deliberate deviations from the Figma frame, and one
live-data follow-up:

1. **Deviation (a) — the sponsors "View all" CTA is omitted.** The Figma frame shows
   the landing's sponsors band verbatim, including the "عرض الكل / View all" button.
   That CTA is the landing's link to *this* page; on the full-listing page itself it
   would be self-referential, so it is dropped. The landing keeps its button.
2. **Deviation (b) — sponsor logos are branded placeholders.** The sponsor cards
   render the shipped placeholder set (a repeated "Host"-tier logo) exactly as the
   landing does. Sponsor / media-partner **logos are not publicly servable today**:
   the public `PublicSponsor` contract (`SIMF.Contracts.Sponsors`) carries no
   "has-logo-asset" flag, and [`SiteContentEndpoints.cs`](../../../src/Website/SIMF.Web/Endpoints/SiteContentEndpoints.cs)
   notes the entity `LogoRelativePath` is not publicly servable (its `MapPartners`
   uses a text placeholder for the same reason). Because it is the full listing (not
   a teaser), the repeated placeholder set reads as several identical sponsors to a
   sighted user and a screen reader — acceptable while it is clearly placeholder, but
   see the wiring below.
3. **Live-data wiring (deferred).** When a public sponsor-logo asset route exists
   (a `HasLogoAsset` flag on `PublicSponsor` + a `/content/assets/SponsorLogo/{id}/image`
   route mirroring the Speaker-photo proxy), bind this page's sponsors band live via
   `SimfPublicClient.GetSponsorsAsync()` — render one tier group per section, each
   card's tag = `TierName`, the external-link = `Url`. Until then it stays on the
   single-sourced placeholder set. This is an additive backend change (honours D-157
   + the append-only mobile wire contract) and needs the owner's call on the public
   logo route.

Minor (shared DRY): the two bands' markup is still copied between `Landing.razor`
and `Partners.razor` (they share CSS + JS + data, not a Razor component). Extracting
`LandingPartnersBand` / `LandingSponsorsBand` shared components belongs in the
deferred DRY pass ([`about.md`](about.md) §7) — it touches the shipped landing
(anchor `id="partners"` + the carousel), so it is kept out of this page build.

_Last reviewed:_ 2026-07-19 by Claude (Partners & sponsors page — `ln-` Bootstrap SSR, Figma 5866-40017).
