# E2E test catalogue — `Session moderation` (`sessionModerate`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the
> moderator (محاور) per-session Q&A desk (D-405, Figma 758:5307). Reached from a
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

> **Backend-faithful subset (D-405):** the API supports only hide / push /
> reorder. There is **no** distinct "answered (تمت الإجابة)" or "rejected"
> *status*, so the chips are **الكل / جديد / يتم الإجابة** and the actions are
> **مرفوض** (hide) + **يتم الإجابة** (push). The frame's answered state is
> flagged for backend follow-up.

---

### E2E-MOBMOD-001 — Golden moderation path

```gherkin
Scenario: A granted moderator works the queue
  Given a moderator opens a session they are granted to moderate
  When they tap the "إدارة الأسئلة" app-bar action on session detail
  Then the أسئلة الجلسة desk opens with the محاوِر pill
  And the approved questions list with the الكل / جديد / يتم الإجابة chips
  When they tap "يتم الإجابة" on a question
  Then PUT …/{id}/push is called and the question shows on-stage (gold)
  When they tap "مرفوض" on a question
  Then PUT …/{id}/hide {isHidden:true} is called and it drops from the queue
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
  Then only the not-yet-pushed questions show

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

---

_Last reviewed:_ `2026-06-14` by `SIMF Team`.
