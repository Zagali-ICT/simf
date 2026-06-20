# E2E test catalogue — `Rate` (`rate`)

> **Authority:** SIMF E2E template (D-133). The feedback endpoint is built +
> approved-only. **Flutter screen built (D-310); re-skinned to Figma `1116:16894`
> + four per-element scores added (D-463).** Widget tests in
> `src/Mobile/simf_app/test/features/feedback/rate_screen_test.dart`; API tests in
> `tests/SIMF.Api.Tests/FeedbackRatingsTests.cs`.

| | |
|--|--|
| **Page** | [`Page_040`](../../App/Page_040/README.md) |
| **Route** | `POST /api/v1/app/feedback/rate` · app screen #40 `/rate` (auth-gated) |
| **Figma** | `1116:16894` |
| **Auth setup** | An **approved Visitor** token (the page is login-only; the route is in the auth gate). |
| **Last reviewed** | 2026-06-20 |

## Wire contract (D-463)

`POST /app/feedback/rate` body: `stars` (1–5, required), `comment` (≤2000, optional)
and the four **optional** per-element scores `organizationStars`, `contentStars`,
`appStars`, `venueStars` (each 1–5 when present; omitted/null when the element is
unscored). The response `RatingView` echoes the same fields. Existing
overall-only callers stay valid (fields are appended, defaulted-null).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB040-001 | Submitting with no overall stars prompts for a rating | validation | P0 | authored ✓ (screen `submitting with no stars prompts for a rating`) |
| E2E-MOB040-002 | Pick overall stars → dynamic "{n} من 5 · {word}" summary → POST → thank-you | happy | P0 | authored ✓ (screen `picking the overall stars + submit sends the rating`) |
| E2E-MOB040-003 | A per-element score (التنظيم…) rides the request next to the overall stars | happy | P0 | authored ✓ (screen `a per-element score is sent alongside the overall stars`) + API `Visitor_can_submit_per_element_scores` |
| E2E-MOB040-004 | Untouched element scores are sent as null | edge | P1 | covered (screen asserts `lastOrganization` null) + API `Element_scores_are_optional_and_default_to_null` |
| E2E-MOB040-005 | An out-of-range element score (6) → 400 | validation | P1 | authored ✓ (API `Out_of_range_element_score_is_rejected_with_400`) |
| E2E-MOB040-006 | Wire failure → error toast | resilience | P1 | authored ✓ (screen `a failure shows the error toast`) |
| E2E-MOB040-007 | Guest → redirected to sign-in (route auth-gated) | auth | P0 | covered (route 40 in the authenticated set) |

## Scenarios

```gherkin
Scenario: Overall stars are required
  When the visitor taps "إرسال التقييم" with no overall stars selected
  Then a "pick a star rating" prompt is shown and no request is sent

Scenario: An overall rating with the dynamic descriptor is submitted
  Given an approved visitor taps the 4th overall star (RTL bar fills from the inline start)
  Then the summary line reads "4 من 5 · جيد جداً"
  When they submit
  Then POST /api/v1/app/feedback/rate is called with stars=4
  And a thank-you toast is shown

Scenario: A per-element score rides alongside the overall stars
  Given the visitor picks 3 overall stars and 5 stars for التنظيم
  When they submit
  Then the request carries stars=3 and organizationStars=5
  And the other element scores are null

Scenario: An out-of-range element score is rejected
  When organizationStars=6 is posted
  Then the API responds 400 (FluentValidation, before the handler runs)

Scenario: A guest cannot reach the page
  Given a signed-out user navigates to /rate
  Then the auth gate redirects to sign-in (route 40 is authenticated)
```

**Evidence:** `rate_screen_test.dart` (4) + `FeedbackRatingsTests` (9, incl. 3 element-score cases).
The CP `/admin/ratings` grid surfaces the four element columns (covered by `cp-ratings` if present).

---

_Last reviewed:_ `2026-06-20` by `SIMF Team`.
