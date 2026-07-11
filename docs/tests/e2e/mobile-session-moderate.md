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

> **D-509 re-skin (Figma 1461:12227):** the desk now shows the **five** filter
> chips — **الكل / جديد / الأسئلة المقبولة / تمت الإجابة / مرفوض** — and three
> per-question actions: **مرفوض** (reject), **تمت الإجابة** (answered) and
> **يتم الإجابة** (on stage). Backend-faithful mapping: **reject** (`hide`) and
> **on-stage** (`push`) hit the real endpoints; the API has no distinct
> "answered" status and a hidden row drops out of the approved queue, so
> **answered** and the **rejected list** are **moderator-session-local** (the
> reject still persists server-side via `hide`). Owner directive: reject is the
> moderator's tool for an invalid / not-in-hall question.

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
  Then it is marked answered (session-local) and moves to the تمت الإجابة tab
  When they tap "مرفوض" on a question
  Then PUT …/{id}/hide {isHidden:true} is called, it drops from the approved
    queue, and it still lists under the مرفوض tab for the rest of the session
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
