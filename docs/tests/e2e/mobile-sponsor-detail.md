# E2E test catalogue — `Sponsor detail` (`sponsorDetail`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> data-driven from the new `GET /app/sponsors/{id}` (`PublicSponsorDetail`,
> anonymous, Wave 3): the sponsor's **About** paragraph + **City** (from the
> linked Contact) added to the tier / website / country the list already carries.
> Built to KSA Figma frame **`1439:11826`** (الراعي) — the owner: reuse the
> exhibitor template (11881). The shared `EntityDetailScaffold`. Opened by tapping
> a sponsor on the sponsors screen (#23). Tested in
> `src/Mobile/simf_app/test/features/sponsors/sponsor_detail_models_test.dart`;
> backend in `tests/SIMF.Api.Tests/SponsorsTests.cs`
> (`Public_sponsor_detail_returns_about_tier_and_website`,
> `Public_sponsor_detail_for_an_unknown_id_returns_404`).

| | |
|--|--|
| **Page** | app screen #221 `sponsorDetail` |
| **Route** | `/sponsors/:sponsorId` (`GET /app/sponsors/{id}`) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1439:11826` |
| **Auth setup** | **None** — `GET /app/sponsors/{id}` is anonymous (public sponsor content). |
| **Last reviewed** | 2026-06-26 |

## Layout

- **Header**: back chevron + centred title **الراعي**.
- **Identity card** (navy): the sponsor logo (SponsorLogo asset, initials
  fallback); the name; the **City، Country** line (gold, with the country flag);
  and the bordered **tier pill** ("رعاية بريميوم"). No stand-code row (sponsors are
  not on the venue map).
- **About card**: "نبذة عن الراعي" header over the about paragraph (shown only when
  set).
- **Website row**: "الموقع الإلكتروني" over the gold URL, a globe icon + chevron →
  opens the site (shown only when a website exists).
- **States**: spinner while loading; retry surface on a wire error.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB221-001 | `GET /app/sponsors/{id}` returns about + tier + website | happy | P0 | authored ✓ (API `Public_sponsor_detail_returns_about_tier_and_website`) |
| E2E-MOB221-002 | Detail model decodes about / city / tier / website / country | data | P0 | authored ✓ (model `decodes the about, city, tier, website and country`) |
| E2E-MOB221-003 | Tapping a sponsor opens its detail | nav | P0 | covered (both `_SponsorCard` + grid tile push `sponsorDetail`) |
| E2E-MOB221-004 | Website row opens the browser | nav | P1 | covered (`launchExternalUri`, best-effort per D-369) |
| E2E-MOB221-005 | Missing about / city / website hide their elements | data | P1 | authored ✓ (model `optional fields fall back / decode to null`) |
| E2E-MOB221-006 | Unknown sponsor id → 404 | error | P1 | authored ✓ (API `Public_sponsor_detail_for_an_unknown_id_returns_404`) |
| E2E-MOB221-007 | RTL — Arabic name / city / about | rtl | P2 | covered (model `localized*` getters) |
| E2E-MOB221-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB221-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

```gherkin
Feature: Sponsor detail (public, Figma 1439:11826, GET /app/sponsors/{id})

Scenario: The detail surfaces the about, tier and website
  Given an admin created the sponsor "Aramco" at the Platinum tier
  And it has the about "A global energy company." and website "https://aramco.com"
  When a client GETs /api/v1/app/sponsors/{id}
  Then it returns the about, tier Platinum and the website

Scenario: An unknown sponsor
  When a client GETs /api/v1/app/sponsors/{a random id}
  Then it returns 404
```

**Evidence:** model tests (2 — full decode + null fallbacks); API tests (2 —
about/tier/website detail, unknown→404).

---

_Last reviewed:_ `2026-06-26` by `SIMF Team`.

## See also — the logo box (owner 2026-07-26)

The 108x108 identity logo on this page renders through the shared
`SimfLogoImage`: the mark FITS the box (`BoxFit.contain`, replacing the crop)
and pressing it opens the logo full size in `SimfImageViewer` (pinch-zoom,
named, close / back to dismiss). Those rules + their scenarios live once in
[`mobile-logo-viewer.md`](mobile-logo-viewer.md) (E2E-LOGO-001..008).
