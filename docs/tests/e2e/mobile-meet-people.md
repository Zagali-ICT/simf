# E2E test catalogue — `Meet people` (`meet-people`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> recommendation read is authenticated (`RequireApprovedAccount`). The **Flutter
> screen is built** and widget-tested in
> `src/Mobile/simf_app/test/features/meet/meet_people_screen_test.dart` (list,
> empty, error) with the model decode in
> `src/Mobile/simf_app/test/features/meet/meet_models_test.dart`. It decodes the
> `RecommendationsResponse` envelope via `Recommendation.listFromData` (no
> duplicate model).

| | |
|--|--|
| **Page** | [`Page_035`](../../App/Page_035/README.md) |
| **Route** | `GET /api/v1/app/account/recommendations/meet-like-you` · app screen #35 `/meet` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **Approved account** — `RequireApprovedAccount`. Sign in with `Get-Totp` (never a literal secret); route 35 is auth-gated. |
| **Last reviewed** | 2026-06-06 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB035-001 | Approved visitor loads the matches (name · jobTitle · profileType · chips · count) | happy | P0 | authored ✓ (screen `renders the match cards`) |
| E2E-MOB035-002 | No matches → empty state | edge | P1 | authored ✓ (screen `empty list shows the empty state`) |
| E2E-MOB035-003 | A read failure → error + Retry that re-fetches | resilience | P0 | authored ✓ (screen `error shows the error state`) |
| E2E-MOB035-004 | An unauthenticated / pending account hits the route-35 auth gate | auth-gate | P0 | covered (route 35 in `_authenticatedRoutes`) |
| E2E-MOB035-005 | Arabic locale renders the Arabic names + chips (RTL) | i18n | P2 | covered (`localizedName`/`localizedProfileType`/`MatchedInterest.localizedName` fallback) |

## Scenarios

### E2E-MOB035-001 — Approved visitor loads the matches

```gherkin
Feature: Meet people (networking)
  As an approved visitor
  I want a list of attendees who share my interests
  So that I can find someone like me to meet

Scenario: The recommendations render
  Given I am signed in with an approved account
  When the app calls GET /api/v1/app/account/recommendations/meet-like-you
  Then it returns 200 with the matches
  And each card shows the name, the "jobTitle · profileType" sub-line,
    the shared-interest chips and the "N shared interests" count
```

**Evidence:** screen test `renders the match cards`; model test
`decodes the matches envelope`.

### E2E-MOB035-002 — Empty / E2E-MOB035-003 — Error+retry / E2E-MOB035-004 — Auth gate

```gherkin
Scenario: No matches shows the empty state
  Given the recommendations read returns an empty matches list
  Then the screen shows the "No matches yet" placeholder

Scenario: A failed read offers a retry
  Given the recommendations read fails
  Then an error + Retry are shown, and Retry re-runs the read

Scenario: A signed-out user cannot open the page
  Given I am not signed in (or my account is pending)
  When I navigate to /meet
  Then the route-35 auth gate redirects me to sign in
```

**Evidence:** screen tests `empty list shows the empty state`,
`error shows the error state`; the auth gate is enforced by route 35 in the
router's `_authenticatedRoutes`.

### E2E-MOB035-005 — Arabic locale (RTL)

```gherkin
Scenario: Arabic locale shows the Arabic names
  Given the device locale is Arabic
  Then each card shows the arabicName, the Arabic profile-type label
    and the Arabic shared-interest names, laid out right-to-left
```

**Evidence:** `Recommendation.localizedName` / `localizedProfileType` and
`MatchedInterest.localizedName` pick the Arabic side with an English fallback
(model test `localized name + profile type pick by language with fallback`).

---

_Last reviewed:_ `2026-06-06` by `SIMF Team`.
