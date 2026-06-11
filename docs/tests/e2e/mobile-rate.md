# E2E test catalogue — `Rate` (`rate`)

> **Authority:** SIMF E2E template (D-133). The feedback endpoint is built +
> approved-only. **Flutter screen built (D-310)** — widget tests in
> `src/Mobile/simf_app/test/features/feedback/rate_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_040`](../../App/Page_040/README.md) |
| **Route** | `POST /api/v1/app/feedback/rate` · app screen #40 `/rate` (auth-gated) |
| **Auth setup** | An **approved Visitor** token (the page is login-only; the route is in the auth gate). |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB040-001 | Submitting with no stars prompts for a rating | validation | P0 | authored ✓ (screen `submitting with no stars prompts for a rating`) |
| E2E-MOB040-002 | Pick 1–5 stars + comment → POST → thank-you | happy | P0 | authored ✓ (screen `picking stars + submit sends the rating`) |
| E2E-MOB040-003 | Wire failure → error toast | resilience | P1 | authored ✓ (screen `a failure shows the error toast`) |
| E2E-MOB040-004 | Guest → redirected to sign-in (route auth-gated) | auth | P0 | covered (route 40 in the authenticated set) |

## Scenarios

```gherkin
Scenario: Stars are required
  When the visitor taps Submit with no stars selected
  Then a "pick a star rating" prompt is shown and no request is sent

Scenario: A rating is submitted
  Given an approved visitor picks 4 stars and a comment
  When they submit
  Then POST /api/v1/app/feedback/rate is called with stars=4
  And a thank-you toast is shown

Scenario: A guest cannot reach the page
  Given a signed-out user navigates to /rate
  Then the auth gate redirects to sign-in (route 40 is authenticated)
```

**Evidence:** `rate_screen_test.dart` (3) + `FeedbackTests` (API).

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
