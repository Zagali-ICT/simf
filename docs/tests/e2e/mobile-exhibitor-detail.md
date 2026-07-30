# E2E test catalogue — `Exhibitor detail` (`exhibitorDetail`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> data-driven from `GET /app/booths/{id}` (`PublicBoothDetail`, anonymous),
> extended in Wave 3 with the exhibitor's **Website** + **Tier** and the **City**
> (resolved Exhibitor → Contact). Built to KSA Figma frame **`1439:11881`**
> (العارض). Opened by tapping a booth in the exhibition list (#22), replacing the
> earlier description bottom sheet. The shared `EntityDetailScaffold` (also used by
> the sponsor detail, 11826). Tested in
> `src/Mobile/simf_app/test/features/booths/booths_screen_test.dart`
> (`tapping a booth navigates to the exhibitor detail`); backend in
> `tests/SIMF.Api.Tests/PublicBoothsTests.cs`
> (`Public_booth_detail_carries_the_exhibitor_website_city_and_tier`).

| | |
|--|--|
| **Page** | app screen #220 `exhibitorDetail` |
| **Route** | `/exhibitors/:boothId` (`GET /app/booths/{id}`) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1439:11881` |
| **Auth setup** | **None** — `GET /app/booths/{id}` is anonymous (public exhibition content). |
| **Last reviewed** | 2026-06-26 |

## Layout

- **Header**: back chevron + centred title **العارض**.
- **Identity card** (navy): the exhibitor logo (CompanyLogo asset via the
  exhibitor's Contact, initials fallback); the name; the **City، Country** line
  (gold, with the country flag); the bordered **tier pill** ("عارض بريميوم", shown
  only when the exhibitor has a tier); and the **stand-code → map** row (the gold
  code over "موقع الجناح على الخريطة", a place icon + chevron → the booth-focused
  venue map).
- **About card**: "نبذة عن العارض" header over the description paragraph (shown
  only when a description exists).
- **Website row**: "الموقع الإلكتروني" over the gold URL, a globe icon + chevron →
  opens the site in the browser (shown only when a website exists).
- **States**: spinner while loading; retry surface on a wire error.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB220-001 | Tapping a booth opens the exhibitor detail | happy | P0 | authored ✓ (screen `tapping a booth navigates to the exhibitor detail`) |
| E2E-MOB220-002 | `GET /app/booths/{id}` carries website + city + tier | happy | P0 | authored ✓ (API `Public_booth_detail_carries_the_exhibitor_website_city_and_tier`) |
| E2E-MOB220-003 | Stand-code row → booth-focused venue map | nav | P1 | covered (the `onMap` pushes `boothMap`; map nav tested in mobile-booths) |
| E2E-MOB220-004 | Website row opens the browser | nav | P1 | covered (`launchExternalUri`, best-effort per D-369) |
| E2E-MOB220-005 | Missing tier / city / about / website hide their elements | data | P1 | covered (each element is conditional on a non-empty value) |
| E2E-MOB220-006 | Unknown booth id → 404 | error | P1 | authored ✓ (API `Public_detail_unknown_id_returns_404`) |
| E2E-MOB220-007 | RTL — Arabic name / city / about | rtl | P2 | covered (models `localized*` getters) |
| E2E-MOB220-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB220-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

```gherkin
Feature: Exhibitor detail (public, Figma 1439:11881, GET /app/booths/{id})

Scenario: The detail surfaces the Wave 3 fields
  Given an exhibitor with website "https://aramco.com", tier Premium, and a Contact city "Dhahran"
  And a booth linked to that exhibitor
  When a client GETs /api/v1/app/booths/{boothId}
  Then it returns website "https://aramco.com", city "Dhahran", tier Premium

Scenario: Tapping a booth opens its exhibitor detail
  Given the exhibition list shows the booth "SAMI"
  When the user taps it
  Then the exhibitor detail screen for that booth opens

Scenario: The detail shows the exhibitor's OWN logo (D-764)
  Given an exhibitor with an uploaded ExhibitorLogo (exhibitorId "ex1")
  When the exhibitor detail opens
  Then the logo tile loads {base}/app/assets/ExhibitorLogo/ex1/image
  And it falls back to the legacy CompanyLogo/{exhibitorContactId} if that 404s
  And then to the exhibitor initials if neither logo loads

Scenario: An exhibitor with no own logo yet keeps its existing company logo
  Given an exhibitor with no ExhibitorLogo but a linked Contact CompanyLogo
  Then the logo tile loads the CompanyLogo (existing data does not regress)
```

**Evidence:** screen tests (`exhibitor_detail_screen_test` — own ExhibitorLogo primary,
CompanyLogo fallback when no own logo id) + API tests (website/city/tier on the detail,
unknown→404, `Upload_exhibitor_logo_then_public_app_image_streams`). The wire adds the
append-only `PublicBoothDetail.ExhibitorId` (the ExhibitorLogo owner).

---

_Last reviewed:_ `2026-06-26` by `SIMF Team`.

## See also — the logo box (owner 2026-07-26)

The 108x108 identity logo on this page renders through the shared
`SimfLogoImage`: the mark FITS the box (`BoxFit.contain`, replacing the crop)
and pressing it opens the logo full size in `SimfImageViewer` (pinch-zoom,
named, close / back to dismiss). Those rules + their scenarios live once in
[`mobile-logo-viewer.md`](mobile-logo-viewer.md) (E2E-LOGO-001..008).
