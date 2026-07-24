# Website "Partners & sponsors" — `/partners`

| | |
|--|--|
| **Route** | `/partners` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual, responsive; static government-partners band + a **live-backend** sponsors marquee (shared `SponsorsMarquee`, see §7) |
| **Source** | [`Partners.razor`](../../../src/Website/SIMF.Web/Components/Pages/Partners.razor) + [`Partners.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Partners.razor.cs) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`SponsorsMarquee.razor`](../../../src/Website/SIMF.Web/Components/Layout/SponsorsMarquee.razor) + [`SponsorsFeed.cs`](../../../src/Website/SIMF.Web/Content/SponsorsFeed.cs) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (reused `ln-pghero` / `ln-pband` / `ln-pcard` / `ln-spon` / `ln-scard2`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Partners.*` for the hero; reused `Landing.Partners.*` / `Landing.Sponsors.*` for the two bands) |
| **Data** | Government partners: single-sourced static `Landing.PartnerLogos` ([`Landing.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Landing.razor.cs)). Sponsors: **live** from `GET /api/v1/app/sponsors` via `SponsorsFeed.LoadAsync` (`SimfPublicClient`), flattened highest-tier-first; an empty/unreachable roster hides the band. |
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

## 7. Follow-ups — deliberate deviations & live data

1. **Deviation (a) — the sponsors "View all" CTA is omitted.** The Figma frame shows
   the landing's sponsors band verbatim, including the "عرض الكل / View all" button.
   That CTA is the landing's link to *this* page; on the full-listing page itself it
   would be self-referential, so it is dropped (`ViewAllHref` left null on the shared
   `<SponsorsMarquee>`). The landing keeps its button (which points here).
2. **Sponsors read live from the backend (was: STARTIME placeholder).** The sponsors
   band now binds the live roster from `GET /api/v1/app/sponsors` via
   `SponsorsFeed.LoadAsync` (`SimfPublicClient`), flattened highest-tier-first, and is
   rendered by the shared `<SponsorsMarquee>` as name-wordmark cards with a bilingual
   tier pill. An empty / unreachable roster hides the band (never a placeholder). This
   replaced the old repeated "Host"-tier **STARTIME** placeholder set (owner ruling:
   never ship the design-agency logo). Sponsor **logo images** are still text
   wordmarks: the public `PublicSponsor` contract carries no "has-logo-asset" flag
   yet, so when CP-uploaded `SponsorLogo` assets exist a follow-up can prefer the
   `/content/assets/SponsorLogo/{id}/image` proxy (additive `HasLogoAsset` on
   `PublicSponsor`, honours D-157 + the append-only mobile wire contract).
3. **Shared DRY (done).** The sponsors band is now the shared `SponsorsMarquee`
   component ([`Components/Layout`](../../../src/Website/SIMF.Web/Components/Layout/SponsorsMarquee.razor))
   used by both the landing and this page, so the two no longer drift. The
   government-partners band markup is still copied (a future `LandingPartnersBand`
   component could fold it in — deferred DRY pass, [`about.md`](about.md) §7).

_Last reviewed:_ 2026-07-22 by Claude (Partners & sponsors — live-backend sponsors marquee, STARTIME removed).
