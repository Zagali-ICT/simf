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
| **Last reviewed** | 2026-07-22 (clock-end prompt attendance-gated; no rate prompt off the sessions list/detail) |

## Wire contract (D-496)

- `GET /app/feedback/form?code=App|Session|Day|Event|Exhibition&ratingTypeId=&targetId=` → `RatingFormView`
  `{ ratingTypeId, code, name, scope, hasOverallStars, allowComment, commentLabel,
  targetId, groups[{ name, questions[{ id, text, isRequired }] }], ungroupedQuestions[],
  existing{ overallStars, comment, answers[{ questionId, stars }] },
  targetName?, targetNameArabic?, targetStart?, isEligible }`.
  **D-713 (appended):** `targetName` / `targetNameArabic` / `targetStart` carry the
  rated **session's** title + start time (null for a Global type), for the app's
  "watched at {session} · {date}" header. Append-only (D-219) — the shipped app ignores them.
  **Owner 2026-07-19 (appended):** `isEligible` is `false` when the caller has not
  attended what this type rates; the app keeps the form visible but disables submit and
  shows an "attend to rate" note. Append-only, defaults `true`.
- **Attendance gate (owner 2026-07-19).** A rating may only be **submitted** for
  something the user attended — `POST /app/feedback/submit` returns **403
  `RATING_NOT_ATTENDED`** otherwise. The proof is blended per scope: **Session** =
  an in-hall `HallAttendance` for that session; **Day** = an in-hall check-in on a
  session that event-local day OR a venue-gate Check-In scan that day; **App / Event /
  Exhibition** (global) = any in-hall check-in OR any venue-gate Check-In scan.
- `POST /app/feedback/submit` body `{ ratingTypeId|code, targetId?, overallStars?,
  comment?, answers[{ questionId, stars }] }` → `RatingSubmissionView` (upsert; one
  per user per (type, target)).
- **App** entry: the More menu opens the App (global) rating; the end-of-session
  **notification** (`kind = SessionRatingRequest`, `relatedEntityId` = session id)
  deep-links to `code=Session&targetId={id}`. **Owner 2026-07-22:** that
  end-of-session notification is sent only to users who **checked in** to the hall
  (a `HallAttendance` row for the session) — not to everyone who booked a seat —
  matching the submit gate; and merely viewing a session's detail never opens the
  rate form (rate comes only from watching the stream or this notification). The
  prompt is CP-controllable: deactivating a rating type in RatingConfig silences its
  prompt everywhere — the "Session" type gates the clock-end worker, the scan-out
  prompt and (via `sessionRatingEnabled` on `GET /app/site-settings`) the app's
  live after-watch prompt; the "Day" type gates the end-of-day prompt; and each of
  "Event"/"Exhibition"/"App" gates its slot in the end-of-programme trio.
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
| E2E-MOB040-015 | Leaving a session's hall (departure) fires a SessionRatingRequest once; re-enter+leave does not double; the clock-end worker then skips that attendee (D-713 GAP-A) | happy | P0 | authored ✓ (API `HallAttendanceTests.Departure_fires_a_session_rating_prompt_once` + `SessionRatingPromptWorkerTests.Scan_does_not_resend_when_the_attendee_was_already_prompted_on_departure`) |
| E2E-MOB040-016 | The per-session rate form shows a "Watched {session} · {date}" header; the App (global) form shows none (D-713) | happy | P1 | authored ✓ (screen `a per-session rating shows the watched-at header` / `the global App rating shows no watched-at header` + API `Per_session_form_carries_the_watched_at_context`) |
| E2E-MOB040-017 | Departure with no open attendance row fires no rating prompt | resilience | P2 | authored ✓ (API `HallAttendanceTests.Departure_with_no_open_row_fires_no_rating_prompt`) |
| E2E-MOB040-018 | **Attendance gate (owner 2026-07-19)** — a visitor who did not attend cannot submit: `POST /feedback/submit` → 403 `RATING_NOT_ATTENDED`; after a check-in is recorded the same submit → 200 | auth | P0 | authored ✓ (API `FeedbackRatingsTests.A_visitor_who_did_not_attend_cannot_submit_an_app_rating` + `The_form_reports_ineligible_before_attendance_and_eligible_after`) |
| E2E-MOB040-019 | **Per-session gate** — rating a session requires an in-hall check-in for **that** session (attendance at another session does not unlock it) → 403 then 200 once checked in | auth | P0 | authored ✓ (API `FeedbackRatingsTests.A_per_session_rating_requires_attendance_at_that_session`) |
| E2E-MOB040-020 | **Form eligibility flag** — the form still loads for a non-attendee but carries `isEligible=false`; the app disables submit + shows an "attend to rate" note; `true` after attendance | happy | P1 | authored ✓ (API `The_form_reports_ineligible_before_attendance_and_eligible_after` + widget `an ineligible form shows the attend note + disables submit`) |
| E2E-MOB040-021 | **Blended day/overall signal** — a Day / App / Event / Exhibition rating is unlocked by a venue-gate Check-In scan (not only an in-hall check-in), matching the rating-prompt audience | auth | P1 | authored ✓ (API `FeedbackRatingsTests.A_venue_gate_checkin_alone_unlocks_a_global_rating` + `DynamicRatingFormTests.Day_rating_is_unlocked_by_a_venue_gate_checkin_that_day`) |
| E2E-MOB040-022 | **Clock-end prompt is attendance-gated (owner 2026-07-22)** — when a session ends, `SessionRatingPromptWorker` prompts only users with an in-hall `HallAttendance` for it; a user who booked a seat but never checked in gets **no** prompt (matching the submit gate) | auth | P0 | authored ✓ (API `SessionRatingPromptWorkerTests.Scan_prompts_attendees_of_a_just_ended_session_exactly_once` + `Scan_does_not_prompt_a_booked_but_absent_user`) |
| E2E-MOB040-023 | **No rate prompt from the sessions list/detail (owner 2026-07-22)** — merely opening/leaving an ended session's detail no longer opens the rate form; rate comes only from watching the live stream or the attendance-gated notification | happy | P0 | authored ✓ (app `session_detail_screen_test` group `no rate prompt from the session detail`) |
| E2E-MOB040-024 | **Rating prompt is CP-controllable (owner 2026-07-22)** — deactivating the "Session" rating type in RatingConfig silences BOTH session-rating-prompt producers (the clock-end worker and the hall scan-out) and does not stamp the session; re-activating resumes prompts | auth | P0 | authored ✓ (API `SessionRatingPromptWorkerTests.Scan_sends_nothing_when_the_Session_rating_type_is_deactivated_in_the_CP` + `HallAttendanceTests.Departure_fires_no_prompt_when_the_Session_rating_type_is_deactivated`) |
| E2E-MOB040-025 | **Day & programme-end prompts are CP-controllable (owner 2026-07-22)** — deactivating the "Day" rating type silences the end-of-day prompt (not stamped → re-enabling resumes); deactivating "App" (or Event/Exhibition) drops just that one from the end-of-programme trio | auth | P0 | authored ✓ (API `ProgrammeRatingPromptWorkerTests.Day_scan_sends_nothing_when_the_Day_rating_type_is_deactivated_in_the_CP` + `Program_end_scan_skips_a_deactivated_overall_rating_type`) |
| E2E-MOB040-026 | **Live after-watch prompt honours the CP toggle (owner 2026-07-22)** — the app reads `sessionRatingEnabled` on `GET /app/site-settings` (mirrors the "Session" type); when false, leaving the live stream does NOT open the rate form | auth | P1 | authored ✓ (API `SiteSettingsPublicTests.GET_reflects_the_CP_Session_rating_toggle_in_sessionRatingEnabled` + app `live_broadcast_screen_test` `the after-watch prompt is suppressed when the CP disables session rating`) |

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

Scenario: Only an attendee can submit a rating (owner 2026-07-19)
  Given an approved visitor who has NOT checked in to any session or venue gate
  When they open /rate (code=App)
  Then GET /app/feedback/form returns 200 with isEligible=false
  And the app shows the form with submit disabled and an "attend to rate" note
  When they force a submit anyway
  Then POST /app/feedback/submit returns 403 RATING_NOT_ATTENDED
  Given the visitor is then recorded as checked in (in-hall or a venue-gate scan)
  When they reload /rate
  Then GET /app/feedback/form returns isEligible=true
  And a submit now returns 200

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

Scenario: Leaving a session's hall prompts to rate it, once (D-713 GAP-A)
  Given an approved visitor arrived at a session's hall (an open HallAttendance row)
  When they leave (POST /app/sessions/{id}/departure closes the row)
  Then exactly one SessionRatingRequest notification exists for that (session, visitor)
  When they re-enter and leave the hall again
  Then no second prompt is created (DeduplicateByRelatedEntity)
  And when the clock-end SessionRatingPromptWorker later scans the ended session
  Then it skips that visitor (already prompted) but still stamps the session

Scenario: A departure with no prior arrival prompts nothing (D-713)
  Given the visitor never arrived at the session's hall (no open row)
  When they POST /app/sessions/{id}/departure
  Then the call succeeds (no-op) and no rating prompt is created

Scenario: The clock-end prompt is attendance-gated, not booking-based (owner 2026-07-22)
  Given a session has ended, visitor A checked in to its hall (HallAttendance)
  And visitor B only reserved a seat but never checked in
  When the SessionRatingPromptWorker scans the ended session
  Then visitor A gets exactly one SessionRatingRequest
  And visitor B gets none (a booking is not attendance)
  And the session is stamped RatingPromptSent so it is not re-scanned

Scenario: Viewing a session's detail never opens the rate form (owner 2026-07-22)
  Given an approved visitor opens an ENDED session's detail without attending it
  When they leave the detail screen
  Then the rate form is NOT opened (rate comes only from watching the stream
    or the attendance-gated notification — never off the sessions list/detail)

Scenario: The CP can turn the session rating prompt off (owner 2026-07-22)
  Given an admin deactivates the "Session" rating type on the CP RatingConfig page
  When a session ends with checked-in attendees, or an attendee scans out
  Then no SessionRatingRequest notification is sent by either producer
  And the session is not stamped, so re-activating the type resumes prompts
  And GET /app/site-settings returns sessionRatingEnabled=false, so the app's
    live after-watch prompt is suppressed too

Scenario: The CP controls the day and overall rating prompts (owner 2026-07-22)
  Given an admin deactivates the "Day" rating type
  When a programme day ends with checked-in attendees
  Then no DayRatingRequest is sent and the day is not stamped (re-enabling resumes)
  Given an admin deactivates only the "App" rating type
  When the programme ends
  Then the Event + Exhibition prompts still fire but the App prompt does not

Scenario: The per-session rate form shows the watched-at header (D-713)
  Given the visitor opens /rate?code=Session&targetId={sessionId}
  Then GET /app/feedback/form returns targetName + targetStart for that session
  And the screen shows a "Watched {session} · {date}" context chip above the form
  But the global App form (code=App, no target) shows no such header
```

**Evidence:** `rate_screen_test.dart` (8 widget tests, all green) + `FeedbackRatingsTests`
(form/submit/upsert/validation/per-session) + `RatingConfigTests` (required-question) +
`SessionRatingPromptWorkerTests` (the end-of-session prompt that drives the deep-link) +
`notifications_screen_test.dart` (the deep-link tap-routing regression, D-507). On-device
verified vs the local API (TXZ_W09): both the App form and the Session form (Speaker/Sound/
Light) render, submit, and persist; the notification deep-link opens the Session form.
The CP `/admin/ratings` grid surfaces the responses (see `cp-admin-ratings.md`); the
config lives on `/admin/rating-config` (see `cp-admin-rating-config.md`).

---

_Last reviewed:_ `2026-07-19` by Claude (owner attendance gate — rate only what you attended; blended HallAttendance + venue-gate GateScan; form `isEligible` flag). Earlier: `2026-07-09` by Claude (D-713 — rate-on-hall-departure GAP-A + the watched-at header).
