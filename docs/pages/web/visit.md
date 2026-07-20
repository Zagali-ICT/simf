# Website "Visiting & travel" — `/visit`

| | |
|--|--|
| **Route** | `/visit` — Blazor SSR Razor page (static render). **Supersedes** the old MudBlazor "visit & entry" page at the same route. |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual, responsive; static (reuses the landing destinations band + real Saudi eVisa copy) |
| **Source** | [`Visit.razor`](../../../src/Website/SIMF.Web/Components/Pages/Visit.razor) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (reused `ln-pghero` / `ln-discover` / `ln-dcard` / `ln-about`; new `ln-discover--dark` / `ln-visa*`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Visit.*`; the "why visit" band reuses `Landing.DiscoverCards`) |
| **Data** | Single-sourced static — the "why visit" destinations come from `Landing.DiscoverCards` (`public static readonly` in [`Landing.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Landing.razor.cs)). No API. |
| **Figma** | KSA Maritime Forum — Visits (Desktop AR), node `5867-24636` |
| **E2E** | [`e2e/web-visit.md`](../../tests/e2e/web-visit.md) (`E2E-WVS-*`) |

## 1. Purpose

The forum's **Visiting & travel** page — why to visit the Kingdom around the event
and how to travel to and enter it. A **Blazor SSR** page on the shared `ln-` chrome:
the interior photo-hero (no breadcrumb), a "why visit" destinations band, and a
"travel & visa" section with the Saudi eVisa summary. This page **supersedes** the
old MudBlazor visit-entry page (see §7).

## 2. Architecture

- **Rendering** — static SSR (no API). Shared chrome via `LandingShell`; the hero is
  the reusable `LandingPageHero` (no breadcrumb — single-page cluster) and carries the
  page's single `<h1>`.
- **"Why visit" band** — reuses the landing's `ln-discover` / `ln-dcard` markup +
  the single-sourced `Landing.DiscoverCards` (the same six destinations as `/discover`),
  on a navy background via the new `ln-discover--dark` modifier (mirrors the existing
  `ln-fsection--dark` dark-section pattern). Section copy is `Visit.Why.*`.
- **"Travel & visa" band** — the page's real content: the Saudi eVisa summary
  (transcribed from the Figma) in the reused `ln-about` 2-column media+content layout.
  Only the band header + the eligible-countries callout are new CSS (`ln-visa*`); the
  2-col grid is reused. The "view eligible countries" CTA is a documented placeholder
  (see §7).
- **Content** — all copy is `Visit.*` resx (bilingual). The hero + visa image reuse
  shared marketing photos. No code-behind.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero (no breadcrumb) | `ln-pghero` (via `LandingPageHero`) | `<h1>` (`Visit.Hero.Title`) + subtitle + venue + date pills |
| 2 | Why visit (navy) | `ln-discover ln-discover--dark` → `ln-dcard` × 6 | Title (`Visit.Why.Title`, `<h2>`) + desc; six destination cards from `Landing.DiscoverCards` |
| 3 | Travel & visa | `ln-visa` → `ln-about__inner` | Band title (`Visit.Visa.Title`, `<h2>`) + sub; a photo + the tourist-visa heading (`<h3>`), two paragraphs (`Visit.Visa.Body1/2`) and an eligible-countries callout with a placeholder CTA |

## 4. Bilingual model (AR RTL / EN LTR)

- **All copy** → `Visit.*` resx; the "why visit" card content is `Landing.DiscoverCards`
  `Bilingual` records resolved `.For(rtl)`; all follow the `/culture` switch.
- **Direction** — the hero photo sits inline-end; the visa 2-col mirrors (photo on the
  opposite side per culture); the destination grid flows in the reading direction.

## 5. Responsive

The `ln-discover__grid` collapses 3→2→1 columns (`landing.css`). The visa 2-col
(`ln-about__inner`) stacks to one column ≤980px; the `ln-visa-cta` callout wraps.
Section padding uses `clamp(16px, 5.5vw, 80px)`. No horizontal overflow at
1440 / 1280 / 1024 / 768 / 390 (`scrollWidth == clientWidth` verified both languages;
zero elements exceed the viewport).

## 6. Verification (2026-07-19)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/VisitPageTests.cs` (3, green):
  single-`<h1>` hero with no breadcrumb; the reused navy `ln-discover--dark` band
  (6 cards); the travel & visa section (heading + copy + the placeholder CTA callout).
- **Live render** — visually verified at **AR@1440** and **EN@1440** (correct RTL→LTR
  mirror): the hero, the navy why-visit grid, and the 2-col visa section. Console
  clean; no horizontal overflow; the visa 2-col stacks at 768.

## 7. Supersede + deliberate deviations

**Supersede (owner decision, Wave 4).** This page replaces the old MudBlazor
"visit & entry" page (a `SimfBanner` + four `simf-card` logistics sections: getting
here / entry & badges / opening hours / accessibility) at the same `/visit` route.
The rewrite is in place (route unchanged, reversible via git); the old `Visit.Banner.*`
/ `Visit.GettingHere.*` / `Visit.Entry.*` / `Visit.Hours.*` / `Visit.Accessibility.*`
resx keys were removed and replaced with the new `Visit.*` set. The old
attendee-logistics copy (venue map, QR-badge entry, hours, accessibility) is retired —
if that practical info is still wanted, it can return as a third section in a later pass.
The nav "Visit" item (Programme mega-menu) was repointed from `#` to `/visit`.

1. **Deviation (a) — "Why visit" reuses the Discover destination cards.** The owner's
   choice: the Figma's placeholder "why visit" card grid is realised with the same six
   `Landing.DiscoverCards` destinations as `/discover` (the Figma used the same card
   component). The grid therefore overlaps `/discover`; the visa section below is this
   page's unique value.
2. **Deviation (b) — the visa CTA is a documented placeholder.** The Figma's
   "view eligible countries" button renders, but has **no target** — the official Saudi
   eVisa portal URL is not hardcoded (project rule: no invented links). **Owner to-do:
   confirm the eVisa portal URL and wire the button** (open in a new tab). Until then
   it is a placeholder (`aria-describedby` makes it announce "Details, view the list of
   countries").
3. **Placeholder image.** The visa section reuses the shared `about-card-1.jpg`
   marketing photo (kept distinct from the six grid photos above); a Riyadh / visa-
   specific image should replace it when available. (It is also large ~8 MB — a shared
   asset shared with the About page; optimise in a future asset pass.)

_Last reviewed:_ 2026-07-19 by Claude (Visiting & travel page — `ln-` Bootstrap SSR, Figma 5867-24636; supersedes the old MudBlazor visit-entry page).
