# E2E test catalogue — `Session moderation` (`sessionModerate`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the
> moderator (محاور) per-session Q&A desk (D-405 / D-509, Figma 1461:12227). Reached from a
> moderator-only action on the session-detail app bar. Backend already shipped
> (`/app/sessions/{id}/questions/moderate|push|hide`); backend tests in
> `tests/SIMF.Api.Tests/SessionQuestionsTests.cs`. App tests:
> `src/Mobile/simf_app/test/features/moderation/`.

| | |
|--|--|
| **Page** | mobile moderator desk (Figma 758:5307) |
| **Route** | app screen #104 `/sessions/:sessionId/moderate` |
| **Surface** | Mobile (Flutter) |
| **Role/gate** | App: `AppRole.moderator`+ (router role-gate, D-405). Server: per-session `SessionModerator` grant or Administrator (403 otherwise) |
| **Test runner** | Flutter widget/unit test |

> **D-509 re-skin (Figma 1461:12227):** the desk shows the **five** filter
> chips — **الكل / جديد / الأسئلة المقبولة / تمت الإجابة / مرفوض** — and three
> per-question actions: **مرفوض** (reject), **تمت الإجابة** (answered) and
> **يتم الإجابة** (on stage). Owner directive: reject is the moderator's tool
> for an invalid / not-in-hall question.
>
> **DEF-MOD-001 / DEF-MOD-002 (2026-07-26) — every chip is now server-backed.**
> `QuestionStatus` gained an additive `Answered = 3` (int column, no check
> constraint, **no migration**) and a new
> `PUT /app/sessions/{id}/questions/{qid}/answered {isAnswered}` endpoint with the
> same per-session gate as `hide` / `push`. The desk list takes an optional
> `?status=` filter: omitted returns the **working desk** (Approved + Answered),
> `?status=Hidden` returns the desk's own **rejected** rows so a mis-click can be
> **restored**. Nothing lives in screen state any more — the marks survive leaving
> the screen, an app restart and a co-moderator on another device. Each action
> updates the row **optimistically** and rolls it back if the call fails.

---

### E2E-MOBMOD-001 — Golden moderation path

```gherkin
Scenario: A granted moderator works the queue
  Given a moderator opens a session they are granted to moderate
  When they tap the "إدارة الأسئلة" app-bar action on session detail
  Then the أسئلة الجلسة desk opens with the محاوِر pill
  And the approved questions list with the five chips
    (الكل / جديد / الأسئلة المقبولة / تمت الإجابة / مرفوض)
  When they tap "يتم الإجابة" on a question
  Then PUT …/{id}/push is called and the question shows on-stage (amber)
  When they tap "تمت الإجابة" on a question
  Then PUT …/{id}/answered {isAnswered:true} is called, the row's persisted
    status becomes Answered and it moves to the تمت الإجابة tab
  When they tap "مرفوض" on a question
  Then PUT …/{id}/hide {isHidden:true} is called, it drops from the working
    desk, and it lists under the مرفوض tab (read back with ?status=Hidden)
```

### E2E-MOBMOD-006 — DEF-MOD-001 the answered mark PERSISTS

```gherkin
Scenario: The answered mark survives leaving the desk
  Given a moderator has marked question Q "تمت الإجابة"
  When they leave the desk and re-open it (or the app restarts, or a
    co-moderator opens the same session on another device)
  Then GET …/moderate returns Q with status = Answered
  And Q is listed under the تمت الإجابة tab, not under جديد
  # app: session_moderate_screen_test.dart
  #      "DEF-MOD-001: answered … survives a reload of the screen"
  # api: ModeratorDeskStateTests.Answered_persists_stays_on_the_desk_and_round_trips

Scenario: Un-marking returns the question to the live queue
  When the moderator taps "تمت الإجابة" again on an answered question
  Then PUT …/{id}/answered {isAnswered:false} is called
  And the persisted status returns to Approved

Scenario: A failed answered call rolls the row back
  Given the answered call returns 500
  When the moderator taps "تمت الإجابة"
  Then the moderatorActionFailed toast shows
  And the row is back where it was (still under جديد, not under تمت الإجابة)
  # app: session_moderate_screen_test.dart
  #      "DEF-MOD-001: a failed answered call rolls the row back"

Scenario: Only an approved question can be marked answered
  Given a question that is still Pending (awaiting the Committee) or Hidden
  When PUT …/{id}/answered {isAnswered:true} is attempted
  Then the API returns 400 "SESSION_QUESTION_INVALID"
  # api: ModeratorDeskStateTests.Answered_on_a_pending_question_is_rejected
```

### E2E-MOBMOD-007 — DEF-MOD-002 a reject is RECOVERABLE

```gherkin
Scenario: A rejected question is still reachable after leaving the desk
  Given a moderator rejected question Q earlier (its status is Hidden)
  When they re-open the desk and tap the مرفوض chip
  Then the desk reads GET …/moderate?status=Hidden
  And Q is listed there
  # app: session_moderate_screen_test.dart
  #      "DEF-MOD-002: a rejected question survives a reload and can be restored"
  # api: ModeratorDeskStateTests.Rejected_questions_are_retrievable_and_restorable_by_the_desk

Scenario: A mis-clicked reject can be restored
  When the moderator acts on a rejected row (تمت الإجابة / يتم الإجابة)
  Then PUT …/{id}/hide {isHidden:false} is called first
  And Q returns to the working desk

Scenario: A hidden question never leaks to an attendee
  Given question Q is Hidden
  When an ordinary approved attendee calls GET …/moderate or
    GET …/moderate?status=Hidden
  Then the API returns 403 on both — the status filter is only reachable
    through the per-session moderator gate
  # api: ModeratorDeskStateTests.A_hidden_question_never_reaches_an_attendee
```

### E2E-MOBMOD-002 — Role gate (app)

```gherkin
Scenario: A non-moderator cannot open the desk
  Given a signed-in visitor
  When navigation hits /sessions/{id}/moderate
  Then the router redirects home (D-405 role gate)
  And the session-detail app bar shows NO "إدارة الأسئلة" action for a visitor
```

### E2E-MOBMOD-003 — Not granted for this session (403)

```gherkin
Scenario: A moderator without the per-session grant
  Given the user holds AppRole.moderator but no SessionModerator grant
  When the desk loads (GET …/moderate → 403)
  Then it shows "لست محاوِرًا لهذه الجلسة" (not a moderator for this session)
  And no questions are shown
```

### E2E-MOBMOD-004 — Chips / empty / error / RTL

```gherkin
Scenario: Chip filters the queue
  Given the queue has new and on-stage questions
  When the user taps "جديد"
  Then only the not-yet-pushed (and not answered/rejected) questions show
  When the user taps "الأسئلة المقبولة"
  Then only the on-stage (pushed) questions show

Scenario: Empty queue
  Given GET …/moderate returns an empty list
  Then the "لا توجد أسئلة معتمدة بعد" empty state shows

Scenario: Load failure (non-403)
  When GET …/moderate returns 500
  Then the error + Retry surface shows; Retry re-fetches

Scenario: Action failure
  When a push/hide call fails
  Then the moderatorActionFailed toast shows; the queue is unchanged

Scenario: RTL
  Given the app language is Arabic
  Then the desk, chips and cards render right-to-left
```

### E2E-MOBMOD-005 — Push / hide guards (S-8)

```gherkin
Scenario: Only an approved question can be pushed on stage
  Given a question that is still Pending (awaiting the Committee) or Hidden
  When a push is attempted (PUT …/{id}/push)
  Then the API returns 400 "SESSION_QUESTION_INVALID"
  And the question never appears on stage
  # backend: SessionQuestionCommitteeTests.Pushing_a_pending_question_is_400,
  #          .Pushing_a_hidden_question_is_400

Scenario: Rejecting a pushed question drops it from the on-stage state
  Given an approved question that has been pushed on stage (IsPushed)
  When the moderator rejects it (PUT …/{id}/hide {isHidden:true})
  Then the persisted row has IsPushed = false and PushedAt = null
  And it drops off the approved desk
  # backend: SessionQuestionsTests.Hiding_a_pushed_question_clears_the_pushed_marker
```

---

_Last reviewed:_ `2026-07-11` by `Claude` (S-8 — push-only-approved + hide-clears-push guards, MOBMOD-005).
_Last reviewed:_ `2026-07-26` by `Claude` (DEF-MOD-001/002 — persisted `QuestionStatus.Answered` + `?status=` desk filter; added MOBMOD-006/007, rewrote MOBMOD-001).
