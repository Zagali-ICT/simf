# E2E test catalogue — `Media partners` (`media-partners`)

> **Authority:** SIMF E2E template (D-133). The media-partners read is built +
> anonymous (D-199; API `tests/SIMF.Api.Tests/MediaPartnersTests.cs`). **Flutter
> screen built (D-306)** — widget tests in
> `src/Mobile/simf_app/test/features/media_partners/media_partners_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_031`](../../App/Page_031/README.md) |
| **Route** | `GET /api/v1/app/media-partners` · app screen #31 `/media-partners` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB031-001 | Guest loads the media-partner list (name + website) | happy | P0 | authored ✓ (screen `renders the partner list`) |
| E2E-MOB031-002 | Empty → empty state | edge | P1 | authored ✓ (screen `empty shows the empty state`) |
| E2E-MOB031-003 | Read failure → error state | resilience | P0 | authored ✓ (screen `error shows the error state`) |

## Scenarios

```gherkin
Scenario: Media partners render without a token
  When the app calls GET /api/v1/app/media-partners
  Then it returns 200 with items[] (name/nameArabic, url)
  And the screen lists each partner card

Scenario: Empty → placeholder; failed read → error state
  Given no partners (or a failed read)
  Then the screen shows the "No media partners" placeholder / the error message
```

**Evidence:** `media_partners_screen_test.dart` (3) + `MediaPartnersTests` (API).

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
