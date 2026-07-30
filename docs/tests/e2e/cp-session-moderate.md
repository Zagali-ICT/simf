# E2E test catalogue — Session Q&A moderation desk (`/sessions/{id}/moderate`)

| | |
|--|--|
| **Page** | [`cp/session-moderate.md`](../../pages/cp/session-moderate.md) |
| **Route** | `/sessions/{SessionId:guid}/moderate` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Authorization model — read first.** Unlike the other admin CRUD pages,
> `SessionModerationDesk.razor` carries only `@attribute [Authorize]` — it is
> **NOT** gated by a `PermissionCatalog` code, so there is no `RequiredPermission`
> and no `/not-permitted` redirect on the page itself. Authorization is enforced
> by the **API**, per-session, in `SessionModeratorAuth.ResolveAuthorizedUserAsync`:
> a caller is authorized when they hold the **`Administrator`** role **OR** they
> have a row in `SessionModerators` for that exact `SessionId`. Anyone else gets
> **HTTP 403 Forbidden** from `/account/api/sessions/{id}/questions/moderate`, which
> the page surfaces as a red error toast (the grid never loads). The auth-gate
> scenario below therefore asserts the **403 → toast** path, not a route redirect.
>
> **What the desk shows.** Per D-212 the desk lists the **`QuestionStatus.Approved`
> set**, ordered by `Order` then `CreatedAt`. Owner 2026-07-19 (two-path Q&A): the
> Approved set is now two kinds of question — **PRE** questions the Scientific
> Committee approved, **and LIVE** questions that auto-approved straight onto the
> desk (a live question skips the AI filter + committee entirely). Both are moderated
> here identically (Hide / Show / Push). Pending PRE questions still await the
> Committee queue; rejected ones are `Hidden`. Recovery of a hidden question is via
> the Committee queue (PRE) or Show here.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOD-001 | Golden path — load queue → Hide → Show → Push to speaker round-trip | happy | P0 | _to author_ |
| E2E-MOD-002 | Refresh button reloads the queue in place | happy | P2 | _to author_ |
| E2E-MOD-003 | Hide a queued question (PUT `/hide` isHidden=true → "Hidden" state) | happy | P1 | _to author_ |
| E2E-MOD-004 | Show a hidden question (PUT `/hide` isHidden=false → "Queued" state) | happy | P1 | _to author_ |
| E2E-MOD-005 | Push to speaker (PUT `/push` → "Pushed" state, Push button + Hide hidden) | happy | P0 | _to author_ |
| E2E-MOD-006 | Empty state — no approved questions renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-MOD-007 | Auth gate — granted per-session moderator (no Admin role) can moderate | auth | P0 | _to author_ |
| E2E-MOD-008 | Auth gate — non-admin / non-moderator → API 403 → error toast, no rows | auth | P0 | _to author_ |
| E2E-MOD-009 | Idempotency — Push an already-pushed question returns the same row, no dup | error | P2 | _to author_ |
| E2E-MOD-010 | Not-found — Hide/Push a question id absent on the session → 404 toast | error | P1 | _to author_ |
| E2E-MOD-011 | Server 500 on `/moderate` load → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-MOD-012 | RTL / Arabic render — page, table and action buttons mirror | i18n | P1 | _to author_ |
| E2E-MOD-013 | Two-path — a LIVE question appears on the desk directly, with no Committee step | happy | P0 | authored ✓ (API `SessionQuestionsTests.Live_question_skips_AI_and_lands_directly_on_the_moderator_desk`) |
| E2E-MOD-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOD-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOD-001 — Golden path (Hide → Show → Push round-trip)

```gherkin
Feature: Session Q&A moderation desk round-trip
  As a moderator of a live session
  I want to hide, re-show and push audience questions to the speaker
  So that only vetted questions reach the stage in the right order

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (TOTP from Get-Totp)
  And a session exists with a known SessionId (e.g. 1f2e3d4c-... "Naval CPS Resilience panel")
  And that session has at least two Committee-approved questions:
    | order | submitter           | question text                          |
    | 0     | Visitor One         | What about CPS resilience under DoS?   |
    | 1     | Visitor Two         | How is the geofence boundary derived?  |
  And the administrator has landed on /sessions/{SessionId}/moderate

Scenario: Load the queue, then hide, re-show and push the top question
  When the page initialises
  Then a GET /account/api/sessions/{SessionId}/questions/moderate fires and returns 200
  And the SimfBanner reads "Session Q&A moderation"
  And the simf-table renders two rows ordered by the "#" column 0,1
  And each row shows Submitter "DisplayName (email)", the Question text, and State "Queued"
  And each queued row exposes a "Hide" button and a "Push to speaker" button

  When the administrator clicks "Hide" on the row with question "What about CPS resilience under DoS?"
  Then a PUT /account/api/sessions/{SessionId}/questions/{questionId}/hide fires with body { "isHidden": true } and returns 200
  And the queue reloads (a fresh GET .../moderate fires)
  And that row's State column now reads "Hidden"
  And the row now exposes a "Show" button (the "Push to speaker" button is gone while hidden)

  When the administrator clicks "Show" on that row
  Then a PUT .../{questionId}/hide fires with body { "isHidden": false } and returns 200
  And after the reload the row's State reads "Queued"
  And the "Hide" and "Push to speaker" buttons are back

  When the administrator clicks "Push to speaker" on that row
  Then a PUT /account/api/sessions/{SessionId}/questions/{questionId}/push fires with body {} and returns 200
  And after the reload the row's State reads "Pushed"
  And that row no longer offers a "Push to speaker" button (only "Hide" remains)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-session-moderate-golden-before.png` (queue with two "Queued" rows)
- Screenshot after: `docs/screenshots/cp-session-moderate-golden-after.png` (top row "Pushed")
- Console errors: 0 expected
- Network: every `/account/api/sessions/{id}/questions/...` call returns 200
- Audit rows: `OperationLog` rows for `session-question.hidden`, `session-question.unhidden`,
  `session-question.pushed` (from `AuditEvents.SessionQuestionHidden / Unhidden / Pushed`)
  carrying the actor's user id and `Detail = "sessionId=...; questionId=..."`

### E2E-MOD-002 — Refresh button

```gherkin
Scenario: The Refresh button reloads the queue in place
  Given the administrator is on /sessions/{SessionId}/moderate with the queue rendered
  When a new question is approved on the session out-of-band (e.g. via the Committee queue)
  And the administrator clicks "Refresh"
  Then a fresh GET /account/api/sessions/{SessionId}/questions/moderate fires and returns 200
  And the new approved question appears as a new "Queued" row
  And the "Refresh" button is disabled while _loading is true and re-enabled afterwards
```

### E2E-MOD-003 — Hide a queued question

```gherkin
Scenario: Hiding a queued question moves it to the Hidden state
  Given the queue shows a "Queued" row for question "How is the geofence boundary derived?"
  When the administrator clicks "Hide" on that row
  Then a PUT .../{questionId}/hide fires with { "isHidden": true } and returns 200
  And the API sets QuestionStatus = Hidden (Status is the single source of truth, D-212)
  And after the reload the row's State reads "Hidden"
  And the row offers only a "Show" button
```

### E2E-MOD-004 — Show a hidden question

```gherkin
Scenario: Showing a hidden question returns it to the Queued state
  Given the queue shows a "Hidden" row
  When the administrator clicks "Show" on that row
  Then a PUT .../{questionId}/hide fires with { "isHidden": false } and returns 200
  And the API sets QuestionStatus = Approved
  And after the reload the row's State reads "Queued"
  And the "Hide" and "Push to speaker" buttons are restored
```

### E2E-MOD-005 — Push to speaker

```gherkin
Scenario: Pushing a question marks it Pushed and removes the Push button
  Given the queue shows a "Queued" row for question "What about CPS resilience under DoS?"
  When the administrator clicks "Push to speaker" on that row
  Then a PUT .../{questionId}/push fires with body {} and returns 200
  And the API sets IsPushed = true and PushedAt = now (one-way)
  And after the reload the row's State reads "Pushed"
  And the row offers a "Hide" button but no "Push to speaker" button
  And an OperationLog row 'session-question.pushed' is written for the actor
```

### E2E-MOD-006 — Empty state

```gherkin
Scenario: A session with no Committee-approved questions renders SimfEmptyState
  Given the session has zero questions in QuestionStatus.Approved
    (it may still have Pending or Hidden questions — those are NOT on this desk)
  When the administrator opens /sessions/{SessionId}/moderate
  Then GET .../questions/moderate returns 200 with an empty data array
  And the page renders the SimfEmptyState component titled "No questions yet." / "لا توجد أسئلة بعد."
  And no simf-table is rendered
  And no error toast appears
```

### E2E-MOD-007 — Auth gate: granted per-session moderator (no Admin role)

```gherkin
Scenario: A user assigned as a session moderator can moderate without the Administrator role
  Given a non-admin user is assigned a row in SessionModerators for {SessionId}
  And that user is signed into the Control Panel
  When they navigate to /sessions/{SessionId}/moderate
  Then the page is NOT redirected to /not-permitted (it carries only [Authorize])
  And GET /account/api/sessions/{SessionId}/questions/moderate returns 200
    (SessionModeratorAuth resolves them via the SessionModerators row)
  And the approved questions render and Hide / Show / Push all succeed
```

### E2E-MOD-008 — Auth gate: non-admin / non-moderator → 403 → toast

```gherkin
Scenario: A signed-in user who is neither Administrator nor a moderator of the session is denied
  Given a signed-in user with no Administrator role and no SessionModerators row for {SessionId}
  When they navigate to /sessions/{SessionId}/moderate
  Then the [Authorize] attribute lets the page render (no /not-permitted redirect)
  And GET /account/api/sessions/{SessionId}/questions/moderate returns 403 Forbidden
  And the page shows a red SimfAlert error toast (env.Success = false)
  And no simf-table rows are rendered
```

### E2E-MOD-009 — Idempotent Push

```gherkin
Scenario: Pushing an already-pushed question is a no-op that returns the same row
  Given the queue shows a "Pushed" row (IsPushed already true)
  And the row currently exposes no "Push to speaker" button via the UI
  When a PUT .../{questionId}/push is issued for that question (e.g. a stale tab)
  Then the API returns 200 with the unchanged row (early idempotent return, no second PushedAt write)
  And no duplicate OperationLog 'session-question.pushed' row is written
```

### E2E-MOD-010 — Not-found question id

```gherkin
Scenario: Hiding or pushing a question id that is not on the session returns 404
  Given the administrator is on /sessions/{SessionId}/moderate
  When a PUT .../questions/{unknownQuestionId}/hide (or /push) is issued for an id absent on the session
  Then the API returns HTTP 404 with ApiResult.Error.Code = "SessionQuestionNotFound"
  And the bilingual message is "The question was not found on this session." /
    "لم يتم العثور على السؤال على هذه الجلسة."
  And the page surfaces the error via the red SimfAlert toast (MessageForCurrentCulture())
```

### E2E-MOD-011 — Server 500 on load

```gherkin
Scenario: API 500 on the moderate queue load shows a bilingual fallback toast
  Given the API is configured to return 500 on /sessions/{SessionId}/questions/moderate (e.g. DB down)
  When the administrator opens /sessions/{SessionId}/moderate
  Then the page shows the "Loading queue…" / "جارٍ تحميل القائمة…" indicator briefly
  And then a red SimfAlert toast appears (env.Success = false)
  And the toast text is the server Error.MessageForCurrentCulture() when present,
    otherwise the fallback "Loading queue…" / "جارٍ تحميل القائمة…" string
  And no simf-table rows render
```

### E2E-MOD-012 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the desk
  Given the administrator is on /sessions/{SessionId}/moderate in English
  When they switch the UI to Arabic via the header language link
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "إدارة أسئلة الجلسات"
  And the table headers read "#", "المرسل", "السؤال", "الحالة", "الإجراءات"
  And the State pills read "في الانتظار" (Queued), "مخفي" (Hidden), "تم العرض" (Pushed)
  And the action buttons read "إخفاء" (Hide), "إظهار" (Show), "عرض للمتحدّث" (Push to speaker), "تحديث" (Refresh)
  And the table columns and action buttons mirror right-to-left
```

### E2E-MOD-013 — Two-path: a live question lands on the desk directly

```gherkin
Scenario: A live audience question reaches the moderator desk without the Committee
  Given a session that is already live
  And an approved attendee submits a question while it is live
  Then the question is stored Approved (Live phase, no AI verdict) — no Committee step
  When the moderator opens /sessions/{SessionId}/moderate
  Then the live question is already a "Queued" row on the desk
  And the moderator can Hide (reject) or Push to speaker (accept) it as usual
```

**Evidence:** API `SessionQuestionsTests.Live_question_skips_AI_and_lands_directly_on_the_moderator_desk`
(the row is Approved + on the desk immediately). PRE questions still require the
Committee (`cp-admin-question-queue.md`) before they appear here.

---

## Implementation notes

- **Entry point (D-646).** The desk is per-session, so it is **not** a nav item —
  it is reached from the **Sessions grid** (`/admin/sessions`) via the **Moderate**
  (gavel) row action, gated by `<AuthorizedAction Permission="Questions.Moderate">`
  (see `cp-admin-sessions.md` E2E-SES-031). `CpNavigation` documents the same
  ("reached from the Sessions grid, not the nav").
- **API integration tests cover this surface at a lower layer** (no browser),
  under `tests/SIMF.Api.Tests/`:
  - `SessionQuestionsTests.cs` —
    `Moderator_queue_lists_committee_approved_questions_with_submitter_projection`,
    `Submit_with_Host_recipient_round_trips_in_moderator_queue`,
    `Non_admin_non_moderator_caller_is_forbidden_on_moderator_queue` (the 403 gate,
    E2E-MOD-008), and `Granted_moderator_can_read_queue_without_admin_role` (the
    per-session grant, E2E-MOD-007).
  - `SessionQuestionCommitteeTests.cs` — the Committee → Approved → desk pipeline
    for PRE questions (D-212/D-234). Owner 2026-07-19 (two-path): a LIVE question
    lands Approved on the desk directly, verified by
    `SessionQuestionsTests.Live_question_skips_AI_and_lands_directly_on_the_moderator_desk`.
  These exercise the same endpoints the desk drives
  (`GET/PUT /sessions/{id}/questions/moderate|hide|push`); the E2E layer adds the
  browser round-trip (button → BFF `/account/api/...` → API → reload → state pill).
- **BFF routes** are forwarded 1:1 in
  `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`
  (`MapGet .../questions/moderate`, `MapPut .../hide`, `MapPut .../push`) via
  `SimfAdminClient.ListModeratorQueueAsync / HideQuestionAsync / PushQuestionAsync`.
- **No reorder UI on this page.** The API exposes `PUT .../questions/reorder`
  (full-list contract, `SessionQuestionInvalid` 400 on a partial/duplicate list),
  but `SessionModerationDesk.razor` has no drag/reorder control, so no E2E row is
  authored for it here — cover reorder at the API-test layer.
- **Manual smoke is canonical today.** Until Playwright is adopted, run these as a
  Chrome DevTools MCP session: sign in per the Auth setup, seed a session with
  approved questions, then walk each scenario capturing screenshots into
  `docs/screenshots/cp-session-moderate-*.png`.

---

_Last reviewed:_ 2026-07-19 by Claude — **Two-path Q&A (owner): the desk's Approved
set now includes LIVE questions that auto-approve straight onto the desk (skipping AI
+ Committee), alongside Committee-approved PRE questions; E2E-MOD-013.** _Prior:_
2026-06-02 by Claude (E2E catalogue rebuild).
