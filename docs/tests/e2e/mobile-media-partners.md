# E2E test catalogue — `Media partners` (`media-partners`)

> **Authority:** SIMF E2E template (D-133). The media-partners read is built +
> anonymous (D-199; API `tests/SIMF.Api.Tests/MediaPartnersTests.cs`). **Flutter
> screen rebuilt to the KSA-Project Figma frame 958:2246 (Figma exact-parity
> P1)** — navy "التغطية الإعلامية" shell + the shared three-tab media-coverage
> hub + a two-column partner grid whose logo is the partner's uploaded asset,
> served by the existing **anonymous** D-357 route
> `GET /api/v1/app/assets/MediaPartnerLogo/{id}/image` (no new endpoint — the
> unified media-asset pipeline already serves it and the CP already uploads it),
> with an initials fall-back. Widget tests in
> `src/Mobile/simf_app/test/features/media_partners/media_partners_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_031`](../../App/Page_031/README.md) |
| **Route** | `GET /api/v1/app/media-partners` (list) · `GET /api/v1/app/assets/MediaPartnerLogo/{id}/image` (logo) · app screen #31 `/media-partners` |
| **Auth setup** | **None** — both reads are `AllowAnonymous`. |
| **Last reviewed** | 2026-06-16 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB031-001 | Guest loads the media-partner grid (logo + name) | happy | P0 | authored ✓ (screen `renders the coverage header, the three tabs and the partners`) |
| E2E-MOB031-002 | Empty → empty state | edge | P1 | authored ✓ (screen `empty shows the empty state`) |
| E2E-MOB031-003 | Read failure → error + retry | resilience | P0 | authored ✓ (screen `a read failure shows the error + retry`) |
| E2E-MOB031-004 | Each card builds its logo from the anonymous asset route | happy | P0 | authored ✓ (screen `builds each logo from the public asset route`) |
| E2E-MOB031-005 | No uploaded logo / fetch fails → initials fall-back | edge | P1 | authored ✓ (logo `errorBuilder` → initials tile) |
| E2E-MOB031-006 | Tab hub: tapping News / Gallery navigates to that screen | nav | P1 | authored ✓ (screen `tapping the News/Gallery tab navigates…`) |
| E2E-MOB031-007 | Arabic/RTL: tabs lay out gallery (right) → partners → news (left) | rtl | P0 | authored ✓ (screen `lays the tabs gallery→partners→news right-to-left in Arabic`) |

## Scenarios

```gherkin
Scenario: Media partners render without a token
  When the app calls GET /api/v1/app/media-partners
  Then it returns 200 with items[] (id, name/nameArabic, url)
  And the screen shows each partner on a navy card: its uploaded logo
    (fetched from GET /api/v1/app/assets/MediaPartnerLogo/{id}/image) over its name

Scenario: A partner with no uploaded logo
  Given a partner whose MediaPartnerLogo asset route 404s
  Then its card shows the partner's initials on the gold tile (no broken image)

Scenario: Three-tab media-coverage hub
  Given the media-partners tab is active (gold pill)
  When the guest taps "الأخبار" (News) or "معرض الصور والفيديوهات" (Gallery)
  Then the app navigates to that screen

Scenario: Empty → placeholder; failed read → error + retry
  Given no partners (or a failed read)
  Then the screen shows the "No media partners" placeholder / the error message + Retry

Scenario: Arabic right-to-left tab order
  Given the app locale is Arabic
  Then the tab row reads, right→left, gallery · partners · news (frame 958:2256)
```

**Evidence:** `media_partners_screen_test.dart` (8) + `MediaPartnersTests` (API list) +
`AssetEndpointsTests` (the anonymous logo serve).

---

_Last reviewed:_ `2026-06-16` by `SIMF Team`.
