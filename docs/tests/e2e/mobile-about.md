# E2E test catalogue — `About the forum` (`about`)

> **Authority:** SIMF E2E template (D-133). The content read is built + anonymous
> (D-173). **Flutter screen built (D-311)** — widget tests in
> `src/Mobile/simf_app/test/features/about/about_screen_test.dart`. Reuses the
> shipped `ContentRepository` (key `about`).

| | |
|--|--|
| **Page** | [`Page_037`](../../App/Page_037/README.md) |
| **Route** | `GET /api/v1/app/content/about` · app screen #37 `/about` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB037-001 | Guest reads the about content (localized body) | happy | P0 | authored ✓ (screen `renders the localized body`) |
| E2E-MOB037-002 | Unseeded key (404) → "content coming soon" (not an error) | edge | P1 | authored ✓ (screen `a 404 … shows the coming-soon state`) |
| E2E-MOB037-003 | Server error → error + retry | resilience | P0 | authored ✓ (screen `a server error shows error + retry`) |

## Scenarios

```gherkin
Scenario: The about content renders without a token
  When the app calls GET /api/v1/app/content/about
  Then it returns 200 and the localized body is shown as selectable text

Scenario: An unseeded key shows coming-soon, not an error
  Given the 'about' content block is not seeded
  When the read returns 404
  Then the screen shows "Content coming soon"

Scenario: A server error offers retry
  Given the content read fails (5xx)
  Then the screen shows the error + a Retry that re-reads
```

**Evidence:** `about_screen_test.dart` (3) + `ContentBlocksTests` (API).

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
