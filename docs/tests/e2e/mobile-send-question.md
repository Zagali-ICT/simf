# E2E test catalogue — `Send a question` (`send-question`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> submission endpoint is already built (D-169 / D-174); API tests in
> `tests/SIMF.Api.Tests/SessionQuestionsTests.cs`. The **Flutter screen is built**
> and widget-tested in
> `src/Mobile/simf_app/test/features/questions/send_question_screen_test.dart`
> (no-id empty state, empty-question inline prompt, submit success + clear,
> 400 not-open toast, generic error toast). It reuses the shipped wire contract
> (no new API).

| | |
|--|--|
| **Page** | [`Page_026`](../../App/Page_026/README.md) |
| **Route** | `POST /api/v1/app/sessions/{sessionId}/questions` · app screen #26 `/live/question?sessionId={id}` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **Approved account** — the route is auth-gated and the endpoint is `RequireApprovedAccount`. Auth-setup via the `Get-Totp` helper for an admin, or a visitor email-OTP session. |
| **Last reviewed** | 2026-06-06 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB026-001 | No `sessionId` → "open from a live session" empty state | edge | P1 | authored ✓ (screen `no session id shows the open-from-a-session empty state`) |
| E2E-MOB026-002 | Pick recipient (Host) + type + Submit → 200, sent toast, field cleared | happy | P0 | authored ✓ (screen `typing + Host + submit sends and shows the sent toast`) |
| E2E-MOB026-003 | Empty question → inline prompt, no call | validation | P0 | authored ✓ (screen `empty question shows the inline prompt, no submit`) |
| E2E-MOB026-004 | 400 `SESSION_NOT_LIVE_FOR_QUESTIONS` (outside window) → not-open toast | edge | P0 | authored ✓ (screen `a 400 SESSION_NOT_LIVE_FOR_QUESTIONS shows the not-open toast`) |
| E2E-MOB026-005 | A server-500 / transport failure → generic error toast | resilience | P0 | authored ✓ (screen `a generic failure shows the generic error toast`) |
| E2E-MOB026-006 | RTL — the screen renders right-to-left in Arabic | i18n | P2 | covered (l10n AR/EN pairs; `Directionality` from the locale) |

## Scenarios

### E2E-MOB026-001 — No session → empty state

```gherkin
Feature: Send a question (live Q&A)
  As an approved attendee
  I want to send a question to the speaker or host
  So that it can be answered on air

Scenario: Opened without a live session
  Given the screen is opened with no sessionId
  Then it shows the "open from a live session" empty state
  And no recipient picker or question field is shown
```

**Evidence:** screen test `no session id shows the open-from-a-session empty state`.

### E2E-MOB026-002 — Submit a question

```gherkin
Scenario: Send a question to the host
  Given the screen is opened with a live session id
  And the recipient is set to Host
  And the question text is "How deep is the reef?"
  When the attendee taps Send question
  Then the app calls POST /api/v1/app/sessions/{id}/questions
  And the body has questionText, isAtVenue=true, recipient=1
  And a "question sent" toast is shown and the field is cleared
```

**Evidence:** screen test `typing + Host + submit sends and shows the sent toast`;
API `SessionQuestionsTests`.

### E2E-MOB026-003 — Empty question / E2E-MOB026-004 — Not open / E2E-MOB026-005 — Generic error

```gherkin
Scenario: An empty question is blocked client-side
  Given the question field is empty
  When the attendee taps Send question
  Then an inline "type your question first" prompt is shown
  And no request is sent

Scenario: Outside the question window
  Given the submit returns 400 SESSION_NOT_LIVE_FOR_QUESTIONS
  When the attendee submits a question
  Then the "questions are only open from 5 minutes before the session until it ends" toast is shown

Scenario: A server / transport failure
  Given the submit fails with a 500 (or transport error)
  When the attendee submits a question
  Then a generic "could not send your question" toast is shown
```

**Evidence:** screen tests `empty question shows the inline prompt, no submit`,
`a 400 SESSION_NOT_LIVE_FOR_QUESTIONS shows the not-open toast`,
`a generic failure shows the generic error toast`. A 404 maps to the same
not-open toast as the 400 (the screen treats both as "not currently open").

### E2E-MOB026-006 — RTL

```gherkin
Scenario: Arabic renders right-to-left
  Given the app locale is Arabic
  Then the title shows "إرسال سؤال"
  And the recipient pills show المتحدث / المضيف
  And the layout is right-to-left
```

**Evidence:** the l10n getters pair AR/EN (`sendQuestion*`); `Directionality`
follows the active locale, as on every shipped mobile screen.

---

_Last reviewed:_ `2026-06-06` by `SIMF Team`.
