# E2E test catalogue — `Sponsors` (`sponsors`)

> **Authority:** SIMF E2E template (D-133). The sponsors read is built + anonymous
> (D-199; API `tests/SIMF.Api.Tests/SponsorsTests.cs`). **Flutter screen built
> (D-305)** — widget tests in `src/Mobile/simf_app/test/features/sponsors/sponsors_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_023`](../../App/Page_023/README.md) |
| **Route** | `GET /api/v1/app/sponsors` · app screen #23 `/sponsors` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB023-001 | Guest loads sponsors grouped by tier (section header per tier) | happy | P0 | authored ✓ (screen `renders tier headers + sponsor cards`) |
| E2E-MOB023-002 | Empty / all-empty groups → empty state | edge | P1 | authored ✓ (screen `empty groups show the empty state`) |
| E2E-MOB023-003 | Read failure → error state | resilience | P0 | authored ✓ (screen `error shows the error state`) |
| E2E-MOB023-004 | Sponsor name binds the real wire names (`nameEn`/`nameAr`, not name/nameArabic) | contract | P0 | covered (model `Sponsor.fromJson` + screen render) |

## Scenarios

```gherkin
Scenario: Sponsors render grouped by tier without a token
  When the app calls GET /api/v1/app/sponsors
  Then it returns 200 with groups[] (tier, tierName, sponsors[])
  And the screen shows a section header per tier with the sponsor cards

Scenario: An empty programme shows the empty state
  Given no sponsors are configured
  Then the screen shows the "No sponsors" placeholder

Scenario: A failed read shows the error state
  Given the sponsors read fails
  Then the screen shows the error message
```

**Evidence:** `sponsors_screen_test.dart` (3) + `SponsorsTests` (API).

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
