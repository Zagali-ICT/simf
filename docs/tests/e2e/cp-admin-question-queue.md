# E2E test catalogue — Question queue (`/admin/question-queue`)

| | |
|--|--|
| **Page** | [`cp/admin-question-queue.md`](../../pages/cp/admin-question-queue.md) |
| **Route** | `/admin/question-queue` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **What this page is.** P3.3 / D-234 — the Scientific-Committee central Q&A
> queue (stage 2). It lists `Pending` questions across **all** sessions and lets
> the committee **Approve** (→ flows to the per-session moderator desk, stage 3),
> **Hide** (drops off the pipeline), or **Escalate** to a role. It is a
> **read-only triage table** — there is no Add / Edit / Details / inline-edit on
> this page. Page permission is `Questions.View`; Approve + Hide are gated by
> `Questions.Moderate`; Escalate by `Questions.Escalate`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-QQU-001 | Golden round-trip — load queue → Approve a pending question → it leaves the queue + green toast | happy | P0 | _to author_ |
| E2E-QQU-002 | Hide action — Hide a pending question → leaves the queue + "Question hidden." toast | happy | P0 | _to author_ |
| E2E-QQU-003 | Escalate action — open modal, fill Role, submit → modal closes, "Question escalated." toast, queue reloads | happy | P0 | _to author_ |
| E2E-QQU-004 | Escalate cancel — open modal, click Cancel → modal closes, no PUT fires | happy | P2 | _to author_ |
| E2E-QQU-005 | Empty state — no pending questions renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-QQU-006 | Auth gate (View) — signed-in admin lacking `Questions.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-QQU-007 | Action gate (Moderate / Escalate) — `Questions.View` only → row action buttons hidden | auth | P0 | _to author_ |
| E2E-QQU-008 | Escalate validation — blank / >64-char Role → 400 `SessionQuestionInvalid` bilingual error, modal stays open | error | P1 | _to author_ |
| E2E-QQU-009 | Stale row conflict — Approve a question already actioned elsewhere → 404 `SessionQuestionNotFound` bilingual toast | error | P1 | _to author_ |
| E2E-QQU-010 | Server 500 on `/queue` load → bilingual fallback toast, no rows render | resilience | P2 | _to author_ |
| E2E-QQU-011 | RTL / Arabic render — page + columns + escalate modal mirror | i18n | P1 | _to author_ |
| E2E-QQU-012 | AI-verdict + Phase rendering — verdict shows or em-dash; Phase localised Pre/Live | happy | P2 | _to author_ |

## Scenarios

### E2E-QQU-001 — Golden round-trip (Approve)

```gherkin
Feature: Question queue — approve a pending question
  As a Scientific-Committee member with Questions.Moderate
  I want to approve a vetted question
  So that it flows to the per-session moderator desk for the live push

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (Get-Totp helper)
  And at least one Pending question exists (submitted from the app / seeded)
  And they have landed on /admin/question-queue

Scenario: Approve one pending question
  Given the page issued GET /account/api/admin/questions/queue and it returned 200
  And the table shows columns: Session, Question, Submitter, Phase, AI verdict
  And a row exists with Question text "When will the naval drone demo run?"
    and Submitter "Visitor One" and Phase "Live"
  When the administrator clicks "Approve" on that row
  Then a PUT /account/api/admin/questions/{id}/approve fires with an empty body
  And it returns ApiResult.Success = true
  And a green SimfAlert reads "Question approved." (toast variant=success)
  And the page re-issues GET /account/api/admin/questions/queue
  And the approved row no longer appears (its Status moved Pending → Approved, off the Pending queue)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-question-queue-golden-before.png`
- Screenshot after: `docs/screenshots/cp-admin-question-queue-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/questions/...` call returns 200; the
  approve PUT and the follow-up `/queue` GET both 200
- Audit row: `OperationLog` / `RowAudit` row with `EventType = 'SessionQuestion.Approved'`,
  `ActorUserId` = the signed-in admin, `SubjectUserId` = the submitter, and
  `Detail` containing `questionId=...; sessionId=...`

### E2E-QQU-002 — Hide action

```gherkin
Scenario: Hide one pending question
  Given the queue shows a Pending row with Question "off-topic spam text"
  When the administrator clicks "Hide" on that row
  Then a PUT /account/api/admin/questions/{id}/hide fires with an empty body
  And it returns ApiResult.Success = true
  And a green SimfAlert reads "Question hidden."
  And the page reloads the queue and the row no longer appears
    (Status moved Pending → Hidden)
  And an audit row with EventType = 'SessionQuestion.Hidden' is written
```

### E2E-QQU-003 — Escalate action

```gherkin
Scenario: Escalate a question to a role
  Given the queue shows a Pending row with Question "Technical detail for the engineers?"
  When the administrator clicks "Escalate" on that row
  Then the Escalate modal opens titled "Escalate to a role"
  And it shows a single text field "Role" and the "Escalate" / "Cancel" buttons
  When they fill Role = "ScientificCommittee"
  And they click "Escalate" (the modal submit)
  Then a PUT /account/api/admin/questions/{id}/escalate fires with body { "Role": "ScientificCommittee" }
  And it returns ApiResult.Success = true
  And the modal closes
  And a green SimfAlert reads "Question escalated."
  And the page reloads the queue
  And an audit row with EventType = 'SessionQuestion.Escalated' and Detail ending "; role=ScientificCommittee" is written
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-question-queue-escalate-modal.png`
- Network: the escalate PUT returns 200; the follow-up `/queue` GET returns 200
- Note: Escalate sets `AssignedToRole` but does NOT change `Status`, so the row
  stays `Pending` and re-appears in the queue after reload — assert it is still
  present (now carrying the assigned role).

### E2E-QQU-004 — Escalate cancel

```gherkin
Scenario: Cancel the escalate modal without submitting
  Given the Escalate modal is open for a row
  When the administrator clicks "Cancel"
  Then the modal closes (_escalateId cleared)
  And no PUT /account/api/admin/questions/{id}/escalate request fires
  And the queue is unchanged
```

### E2E-QQU-005 — Empty state

```gherkin
Scenario: No pending questions renders SimfEmptyState
  Given the database has no questions in Status = Pending
  When the administrator opens /admin/question-queue
  Then GET /account/api/admin/questions/queue returns 200 with an empty Data array
  And the page renders the SimfEmptyState component
  And the empty title reads "No questions awaiting review." / "لا توجد أسئلة بانتظار المراجعة."
  And no table renders and no error toast appears
```

### E2E-QQU-006 — Auth gate (page permission)

```gherkin
Scenario: Signed-in admin without Questions.View is denied
  Given a signed-in admin user whose role does NOT grant Questions.View
  When they navigate to /admin/question-queue
  Then the [RequirePermission(PermissionCatalog.Questions.View)] attribute denies access
  And they land on /not-permitted with HTTP 200
  And no GET /account/api/admin/questions/queue request fires
```

### E2E-QQU-007 — Action gate (Moderate / Escalate)

```gherkin
Scenario: View-only committee member sees no row action buttons
  Given a signed-in user granted Questions.View but NOT Questions.Moderate or Questions.Escalate
  When they open /admin/question-queue with at least one Pending row
  Then the queue table renders normally
  But the "Approve" and "Hide" buttons are absent (AuthorizedAction Permission=Questions.Moderate)
  And the "Escalate" button is absent (AuthorizedAction Permission=Questions.Escalate)
  And the actions cell is empty for every row
```

### E2E-QQU-008 — Escalate validation

```gherkin
Scenario: Invalid escalation role is rejected with a bilingual error
  Given the Escalate modal is open for a Pending row
  When the administrator leaves Role blank (or enters more than 64 characters)
  And clicks "Escalate"
  Then a PUT /account/api/admin/questions/{id}/escalate fires
  And the API returns HTTP 400 with ApiResult.Error.Code = "SessionQuestionInvalid"
  And the error toast surfaces the bilingual MessageForCurrentCulture():
    "The escalation role must be between 1 and 64 characters."
    / "يجب أن يتراوح طول دور التصعيد بين 1 و 64 حرفاً."
  And the modal stays open (_escalateId still set)
  And the queue is not reloaded
```

### E2E-QQU-009 — Stale row conflict

```gherkin
Scenario: Approving an already-actioned question returns 404
  Given a row is shown in the queue (loaded snapshot)
  And that same question was approved/hidden by another committee member meanwhile
  When the administrator clicks "Approve" on the now-stale row
  Then PUT /account/api/admin/questions/{id}/approve is forwarded
  And if the question id no longer resolves the API returns HTTP 404
    with ApiResult.Error.Code = "SessionQuestionNotFound"
  And a red SimfAlert surfaces the bilingual message
    "The question was not found." / "لم يتم العثور على السؤال."
  Note: a re-action of an already-Approved row is idempotent at the service
  (the status guard skips the write) and still returns 200 — only a genuinely
  missing id raises the 404.
```

### E2E-QQU-010 — Server 500 on load

```gherkin
Scenario: API 500 on /queue shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/questions/queue (e.g. DB down)
  When the administrator opens /admin/question-queue
  Then the "Loading…" / "جارٍ التحميل…" text shows briefly
  And then a red SimfAlert appears reading
    "Could not load the question queue." / "تعذّر تحميل قائمة الأسئلة."
    (the envelope Error message if present, else Admin.QuestionQueue.LoadFailed)
  And no table rows render
```

### E2E-QQU-011 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page, columns and escalate modal
  Given the administrator is on /admin/question-queue in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "قائمة الأسئلة"
  And the column headers read الجلسة / السؤال / مقدّم السؤال / المرحلة / حكم الذكاء الاصطناعي
  And the row action buttons read اعتماد / إخفاء / تصعيد
  And the Phase cell renders "قبل" (Pre) or "مباشر" (Live)

  When they click "تصعيد" on a row
  Then the Escalate modal opens in RTL titled "التصعيد إلى دور"
  And the field label reads "الدور"
  And the footer buttons read "تصعيد" / "إلغاء" in reverse order
```

### E2E-QQU-012 — AI-verdict and Phase rendering

```gherkin
Scenario: AI verdict and Phase columns render the projected values
  Given a Pending row whose AiFilterVerdict is null
  Then its "AI verdict" cell renders the em-dash "—"
  Given another Pending row whose AiFilterVerdict = "clean"
  Then its "AI verdict" cell renders "clean"
  And a row with Phase = Pre renders the localised "Pre" / "قبل"
  And a row with Phase = Live renders the localised "Live" / "مباشر"
```

---

## Implementation notes

- **Read-only triage page.** Unlike the CRUD pages (e.g. `/admin/interests`),
  this page has **no Add / Edit / Details / inline grid edit**. Every action is a
  single PUT against a question id (`approve`, `hide`, `escalate`) plus the GET
  `/queue` load. Scenarios are scoped to exactly those actions — do not invent
  create/delete UI.
- **Two-stage pipeline.** Approve here only changes `Status` Pending → Approved;
  the approved question then surfaces on the **per-session moderator desk**
  (`SessionModerationDesk.razor`, stage 3) where push/reorder/hide happen. Escalate
  sets `AssignedToRole` without changing `Status`, so an escalated row stays in
  this queue.
- **BFF passthroughs** live in `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`
  (`/account/api/admin/questions/queue|{id}/approve|{id}/hide|{id}/escalate`) →
  `SimfAdminClient` → API `/api/v1/admin/questions/*`. The approve/hide/escalate
  API endpoints also carry the `auth` rate-limit policy.
- **API integration tests** at `tests/SIMF.Api.Tests/SessionQuestionCommitteeTests.cs`
  cover the same surface at a lower layer (no browser): pending-appears-in-queue,
  approve→moderator-desk, hide→off-desk, escalate-to-a-role, the empty-role guard
  (the QQU-008 equivalent), and the non-committee rejection (the QQU-006/007
  equivalent). When E2E covers a scenario you can usually retire the matching
  `Api.Tests` case — keep both during the transition.
- **Audit keys** (assert in the audit log): `SessionQuestion.Approved`,
  `SessionQuestion.Hidden`, `SessionQuestion.Escalated`
  (`src/Backend/SIMF.Application/Auditing/AuditEvents.cs`).

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
