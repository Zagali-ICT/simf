# E2E test catalogue — `Rate` (`rate`)

> **Authority:** SIMF E2E template (D-133). **Dynamic, config-driven rating
> (D-496).** The screen fetches a server-defined form for a rating type (App or
> Session) and renders the overall star bar + grouped/flat questions + comment box
> per the type's config. Widget tests in
> `src/Mobile/simf_app/test/features/feedback/rate_screen_test.dart`; API tests in
> `tests/SIMF.Api.Tests/FeedbackRatingsTests.cs` + `RatingConfigTests.cs`.

| | |
|--|--|
| **Page** | [`Page_040`](../../App/Page_040/README.md) |
| **Route** | `GET /api/v1/app/feedback/form` + `POST /api/v1/app/feedback/submit` · app screen #40 `/rate?code=&ratingTypeId=&targetId=` (auth-gated) |
| **Auth setup** | An **approved Visitor** token (the page is login-only; route 40 is in the auth gate). |
| **Last reviewed** | 2026-06-25 (D-496 dynamic ratings) |

## Wire contract (D-496)

- `GET /app/feedback/form?code=App|Session|Day|Event|Exhibition&ratingTypeId=&targetId=` → `RatingFormView`
  `{ ratingTypeId, code, name, scope, hasOverallStars, allowComment, commentLabel,
  targetId, groups[{ name, questions[{ id, text, isRequired }] }], ungroupedQuestions[],
  existing{ overallStars, comment, answers[{ questionId, stars }] } }`.
- `POST /app/feedback/submit` body `{ ratingTypeId|code, targetId?, overallStars?,
  comment?, answers[{ questionId, stars }] }` → `RatingSubmissionView` (upsert; one
  per user per (type, target)).
- **App** entry: the More menu opens the App (global) rating; the end-of-session
  **notification** (`kind = SessionRatingRequest`, `relatedEntityId` = session id)
  deep-links to `code=Session&targetId={id}`.
- **Prompt codes (D-679):** the seeded system types `Day` (PerDay — one per
  programme day, `targetId` = `ProgrammeDay.Id`) and `Event` / `Exhibition`
  (global) back the `ProgrammeRatingPromptWorker` notifications, which deep-link to
  `code=Day&targetId={id}` (end of each day, to that day's checked-in attendees)
  and `code=Event|Exhibition|App` (end of the programme). The screen is code-agnostic
  — the same form/submit endpoints serve every code.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB040-001 | App form loads (overall + comment, no questions) | happy | P0 | authored ✓ (screen + API `App_form_exposes_overall_and_comment_config`) |
| E2E-MOB040-002 | Submitting with no overall stars prompts for a rating | validation | P0 | authored ✓ (screen `submitting with no overall stars…`) |
| E2E-MOB040-003 | Pick overall → "{n} of 5 · {word}" summary → submit → thank-you | happy | P0 | authored ✓ (screen + API `Visitor_can_submit_app_rating`) |
| E2E-MOB040-004 | A per-question score rides the submission's answers[] | happy | P0 | authored ✓ (screen + API `Required_question_must_be_answered_to_submit`) |
| E2E-MOB040-005 | Resubmitting upserts + the form prefills from the existing submission | happy | P1 | authored ✓ (API `Resubmitting_upserts_the_single_row_and_prefills`) |
| E2E-MOB040-006 | Out-of-range overall (6) → 400 | validation | P1 | authored ✓ (API `Out_of_range_overall_is_rejected_with_400`) |
| E2E-MOB040-007 | Required question unanswered → 400 (blocked client + server) | validation | P1 | authored ✓ (screen `a session rating with an unanswered required question cannot be saved` — the client refuses to send it + API) |
| E2E-MOB040-008 | Submit wire failure → error toast | resilience | P1 | authored ✓ (screen `a submit failure shows the error toast`) |
| E2E-MOB040-009 | Per-session form without a target → 400 | validation | P1 | authored ✓ (API `Per_session_form_without_a_target_is_400`) |
| E2E-MOB040-010 | Session deep-link from the "rate this session" notification opens the form | happy | P0 | authored ✓ (worker `SessionRatingPromptWorkerTests` + `notifications_screen_test` deep-link regression; **D-507** — the card must stay tappable after the inbox auto-marks it read) |
| E2E-MOB040-011 | Guest → redirected to sign-in (route auth-gated) | auth | P0 | covered (route 40 in the authenticated set) |
| E2E-MOB040-012 | Global `Event` / `Exhibition` form needs no target and submits | happy | P1 | authored ✓ (API `DynamicRatingFormTests.Global_rating_form_needs_no_target_and_is_submittable`) |
| E2E-MOB040-013 | `Day` form without a target → 400; submit for an unknown day → 404; a real `ProgrammeDay` → 200 (the new PerDay branch) | validation | P1 | authored ✓ (API `DynamicRatingFormTests` Day cases) |
| E2E-MOB040-014 | End-of-day / end-of-programme prompts deep-link to `code=Day\|Event\|Exhibition\|App` | happy | P1 | authored ✓ (worker `ProgrammeRatingPromptWorkerTests`; clickUrl via `NotificationKindCatalog`) |

## Scenarios

```gherkin
Scenario: The App rating form loads from config
  Given an approved visitor opens /rate from the More menu (defaults to code=App)
  Then GET /app/feedback/form?code=App returns hasOverallStars=true, allowComment=true, no questions
  And the overall star bar + the notes box render (no per-element rows)

Scenario: Overall stars are required
  When the visitor taps "Submit rating" with no overall stars selected
  Then a "Please pick a star rating" prompt is shown and no submit request is sent

Scenario: An overall rating with the dynamic descriptor is submitted
  Given the visitor taps the 4th overall star (RTL bar fills from the inline start)
  Then the summary line reads "4 of 5 · Very good" ("4 من 5 · جيد جداً" in Arabic)
  When they submit
  Then POST /app/feedback/submit carries overallStars=4 and answers=[]
  And a "Thanks for your rating" toast is shown

Scenario: A per-question score rides in answers[]
  Given the form has an ungrouped question "Organization" (id q-org)
  And the visitor picks 3 overall stars and 5 stars for "Organization"
  When they submit
  Then the request carries overallStars=3 and answers=[{ questionId: q-org, stars: 5 }]

Scenario: A required question must be answered
  Given the form has a required question
  When the visitor submits without scoring it
  Then the client shows "Please answer all required questions" and the server rejects with 400

Scenario: Resubmitting prefills the form
  Given the visitor already submitted overall=4 / comment="Better"
  When they reopen /rate for the same type
  Then the form prefills the overall bar to 4 and the notes box to "Better"

Scenario: A session rating is reached from the notification
  Given a session has ended and the visitor received a "Rate this session" notification
    (kind=SessionRatingRequest, relatedEntityId = the session id)
  And opening the inbox has already auto-marked the notification read (D-507)
  When they tap the (now read) notification
  Then it navigates to /rate?code=Session&targetId={sessionId}
  And the Session rating form loads for that session (overall + Speaker/Sound/Light)

Scenario: A guest cannot reach the page
  Given a signed-out user navigates to /rate
  Then the auth gate redirects to sign-in (route 40 is authenticated)

Scenario: The end-of-day prompt opens the Day rating form (D-679)
  Given a programme day has ended and the visitor checked in that day
  When the ProgrammeRatingPromptWorker fires a DayRatingRequest and the visitor taps it
  Then it navigates to /rate?code=Day&targetId={programmeDayId}
  And the Day rating form loads; submitting for a real day persists, an unknown day → 404

Scenario: The end-of-programme prompts open the overall rating forms (D-679)
  Given the whole 3-day programme has ended
  When the worker fires the Event + Exhibition + App prompts to every checked-in attendee
  Then each opens its global form (code=Event|Exhibition|App, no target) which submits with 200
```

**Evidence:** `rate_screen_test.dart` (4 widget tests, all green) + `FeedbackRatingsTests`
(form/submit/upsert/validation/per-session) + `RatingConfigTests` (required-question) +
`SessionRatingPromptWorkerTests` (the end-of-session prompt that drives the deep-link) +
`notifications_screen_test.dart` (the deep-link tap-routing regression, D-507). On-device
verified vs the local API (TXZ_W09): both the App form and the Session form (Speaker/Sound/
Light) render, submit, and persist; the notification deep-link opens the Session form.
The CP `/admin/ratings` grid surfaces the responses (see `cp-admin-ratings.md`); the
config lives on `/admin/rating-config` (see `cp-admin-rating-config.md`).

---

_Last reviewed:_ `2026-07-07` by Claude (D-679/D-680 — Day/Event/Exhibition prompt codes).
