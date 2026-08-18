# E2E test catalogue — Question queue (`/admin/question-queue`)

| | |
|--|--|
| **Page** | [`cp/admin-question-queue.md`](../../pages/cp/admin-question-queue.md) |
| **Route** | `/admin/question-queue` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-08-18 (the queue moved onto the shared grid seam) |

> **What this page is.** P3.3 / D-234 — the Scientific-Committee central Q&A
> queue (stage 2). It lists `Pending` questions across **all** sessions and lets
> the committee **Approve** (→ flows to the per-session moderator desk, stage 3),
> **Hide** (drops off the pipeline), or **Escalate** to a role. **Owner 2026-07-19
> (two-path Q&A):** this queue now receives **PRE questions only** — a question
> asked while the session is LIVE auto-approves straight to the moderator desk
> (skipping the AI filter + this committee stage), so it never lands `Pending`
> here. (Legacy `Pending` rows created before the change may still carry the Live
> phase; the Phase column renders both.) It is a
> **read-only triage grid** — there is no Add / Edit / Details / inline-edit on
> this page; the three actions are quiet per-row **icon** buttons in the grid's
> `RowActions` slot (Approve = check-circle, Hide = eye-off, Escalate = share).
> Page permission is `Questions.View`; Approve + Hide are gated by
> `Questions.Moderate`; Escalate by `Questions.Escalate`.
>
> **Grid note.** The page renders the canonical `SimfDataGrid` (`Top = 20`,
> page-size options 10/20/50/100). The backend read is **server-paged on the
> shared grid seam**: `POST /account/api/admin/questions/list` binds a `GridQuery`
> and returns one `GridPage`, so filtering, sorting and paging all happen in SQL
> over the whole queue rather than over a fetched prefix, and every gesture is a
> round-trip. It replaced a `GET /admin/questions/queue` that returned the whole
> Pending queue in one array with `status` and `sessionId` as query-string
> parameters; those two are now grid **filter keys**.
>
> **The declared contract**, quoted so the scenarios read without opening the
> service: sortable and filterable keys `session` (searchable), `question`
> (searchable), `phase`, `ai` (searchable), `status`, `sessionId`, `createdAt`;
> natural order `createdAt` ascending, because the committee works the head of the
> queue; tiebreak `Id`; page size falls back to 25 and is capped at 200. The page
> exposes a subset of those as controls: filter inputs on Session, Question and AI
> verdict, sort on Session, Question and Phase. **Submitter is display-only** and
> is deliberately neither sortable nor filterable, because that name lives in the
> Identity database and is resolved after the page is chosen (D-157 forbids the
> cross-database join that would make it a key). A request naming no status gets
> the default Pending bucket. `Multiselect` renders select-all + per-row
> checkboxes, but there is **no bulk toolbar action** on this page: the
> checkboxes are cosmetic, so there is no bulk scenario.

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
| E2E-QQU-013 | Per-column filter narrows the grid: type into the Question filter input → a server round-trip carrying `filters.question`, Skip → 0 | happy | P1 | _to author_ |
| E2E-QQU-014 | Column sort toggles: click the Session header → asc → desc → a server round-trip carrying `sort`, Skip → 0 | happy | P2 | _to author_ |
| E2E-QQU-015 | Excel export — toolbar Export downloads an .xlsx of the Pending queue; selected rows export just those (D-356) | happy | P1 | _to author_ |
| E2E-QQU-016 | **Moderator-desk guards (S-8)** — a question that is Pending (still in this queue) or Hidden cannot be pushed on stage (400 `SESSION_QUESTION_INVALID`); rejecting a pushed question clears its on-stage marker. Cross-ref: `docs/tests/e2e/mobile-session-moderate.md` MOBMOD-005 | error | P1 | authored ✓ (`SessionQuestionCommitteeTests.Pushing_a_pending_question_is_400` + `.Pushing_a_hidden_question_is_400` + `SessionQuestionsTests.Hiding_a_pushed_question_clears_the_pushed_marker`) |
| E2E-QQU-017 | The queue returns one server page and the true Pending total: 60 pending, `top` 20 gives 20 rows and `total` 60 | happy | P0 | authored |
| E2E-QQU-018 | The default bucket is Pending: no status filter returns Pending only, and `"status": ""` still returns Pending only, never every bucket at once | security | P0 | authored |
| E2E-QQU-019 | An undeclared sort key is a 400 `GRID_SORT_KEY_INVALID`; sorting on the display-only Submitter column is the same 400 | validation | P0 | authored |
| E2E-QQU-020 | The pager walks the queue oldest-first: Next carries `skip` 20, no question repeats or is dropped across a `createdAt` tie | correctness | P0 | authored |
| E2E-QQU-021 | An explicit `status` filter parses by enum NAME and a `sessionId` filter narrows to one session; `total` reports the filtered count, not the whole queue | happy | P1 | authored |
| E2E-QQU-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-QQU-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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
  Given the page issued POST /account/api/admin/questions/list with {"Top":20} and it returned 200
  And the grid shows columns: Session, Question, Submitter, Phase, AI verdict
  And a row exists with Question text "When will the naval drone demo run?"
    and Submitter "Visitor One" and Phase "Pre"
    (a live question would have auto-approved to the desk, not landed here)
  When the administrator clicks the row's Approve (check-circle) icon action — tooltip "Approve"
  Then a PUT /account/api/admin/questions/{id}/approve fires with an empty body
  And it returns ApiResult.Success = true
  And a green SimfAlert reads "Question approved." (toast variant=success)
  And the page re-issues POST /account/api/admin/questions/list
  And the approved row no longer appears (its Status moved Pending → Approved, off the Pending queue)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-question-queue-golden-before.png`
- Screenshot after: `docs/screenshots/cp-admin-question-queue-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/questions/...` call returns 200; the
  approve PUT and the follow-up `/list` POST both 200
- Audit row: `OperationLog` / `RowAudit` row with `EventType = 'SessionQuestion.Approved'`,
  `ActorUserId` = the signed-in admin, `SubjectUserId` = the submitter, and
  `Detail` containing `questionId=...; sessionId=...`

### E2E-QQU-002 — Hide action

```gherkin
Scenario: Hide one pending question
  Given the queue shows a Pending row with Question "off-topic spam text"
  When the administrator clicks the row's Hide (eye-off) icon action — tooltip "Hide"
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
  When the administrator clicks the row's Escalate (share) icon action — tooltip "Escalate"
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
  Then POST /account/api/admin/questions/list returns 200 with an empty data.items and data.total 0
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
  And no POST /account/api/admin/questions/list request fires
```

### E2E-QQU-007 — Action gate (Moderate / Escalate)

```gherkin
Scenario: View-only committee member sees no row action icons
  Given a signed-in user granted Questions.View but NOT Questions.Moderate or Questions.Escalate
  When they open /admin/question-queue with at least one Pending row
  Then the queue grid renders normally
  But the Approve (check-circle) and Hide (eye-off) icon actions are absent (AuthorizedAction Permission=Questions.Moderate)
  And the Escalate (share) icon action is absent (AuthorizedAction Permission=Questions.Escalate)
  And the grid's RowActions cell is empty for every row
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
  When the administrator clicks the Approve (check-circle) icon action on the now-stale row
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
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/questions/list (e.g. DB down)
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
  And the per-row icon actions carry the tooltips اعتماد / إخفاء / تصعيد (check-circle / eye-off / share)
  And the Phase cell renders "قبل" (Pre) or "مباشر" (Live)

  When they click the Escalate (share) icon action (tooltip "تصعيد") on a row
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

### E2E-QQU-013 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a column filter re-queries the server
  Given the queue holds 12 Pending rows, loaded via POST /account/api/admin/questions/list
  And the grid shows the per-column filter row under the headers
    (an <input type="search"> under Session, Question and AI verdict; Phase and
     Submitter have no filter input)
  When the administrator types "drone" into the Question filter input
    (aria-label "Filter column Question", placeholder "Search")
  Then after the 300 ms debounce a NEW POST /account/api/admin/questions/list fires
    carrying filters.question = "drone" and skip reset to 0
  And it returns 200
  And only rows whose Question contains "drone" come back, matched in SQL
  And data.total is the narrowed count, so the pager summary reads "Showing 1-2 of 2"

  When they additionally type "Al-Bahr" into the Session filter input
    (aria-label "Filter column Session")
  Then the next request carries both filters.question = "drone" and
    filters.session = "Al-Bahr"
  And the grid shows only rows matching BOTH filters (filters are AND-combined)

  When they clear the Question filter input
  Then filters.question is dropped from the request and the grid widens back to the
    Session-only match
```

**Evidence captured:**
- The filter row only renders inputs for Filterable columns: Session, Question and
  AI verdict carry a search box; **Phase and Submitter do not**. Phase is sortable
  but not filterable on the page; Submitter is display-only in both directions,
  because its value is resolved from the Identity database after the page is chosen.
- One `/account/api/admin/questions/list` POST per settled gesture is logged, and
  its body is captured. Zero requests during filtering was the old client-side
  contract and is now the failure: it would mean the grid narrowed a fetched prefix
  and called it the queue.

### E2E-QQU-014 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header re-queries the server
  Given the queue loaded with its default order (oldest-first by CreatedAt)
  When the administrator clicks the "Session" column header (a sortable header button)
  Then a POST /account/api/admin/questions/list fires with
    sort = "session", sortDescending = false, skip = 0
  And the rows come back ordered by session title ascending, sorted in SQL
  And the header's sort arrow shows ▲ (aria-sort="ascending")

  When they click the "Session" header again
  Then the next request carries sortDescending = true
  And the rows come back by session title descending and the arrow shows ▼ (aria-sort="descending")

  When they click the "Phase" header
  Then the next request carries sort = "phase", sortDescending = false, skip = 0
  And the previous "Session" header returns to the neutral ↕ arrow (aria-sort="none")
```

**Evidence captured:**
- Sortable headers on the page: Session, Question, Phase. **AI verdict renders a
  filter box but no sort button, and Submitter renders neither** (a sort on
  `submitter` would be a 400, so the page must not offer it: see E2E-QQU-019).
- Each settled gesture logs exactly one `/list` POST, and the rows change order
  because the server re-ordered them, not because the page re-projected a list it
  already had.

### E2E-QQU-015 — Excel export (D-356)

```gherkin
Scenario: Export the Pending question queue to an XLSX workbook
  Given the administrator is on /admin/question-queue with at least three Pending
    rows loaded via POST /account/api/admin/questions/list
  And the grid toolbar shows the "Export" action (OnExport wired, label "Export")
  When they click "Export" with no rows selected
  Then the browser issues POST /account/api/admin/questions/export (via simfAccount.downloadXlsx)
  And the request body is an AdminGridExportRequest with an empty Ids list and the
    current Query (the page sends Query only when the selection is empty, so the
    whole filtered Pending queue is exported)
  And the API authorises the call against Questions.Export
  And the browser saves a file named simf-questions-{timestamp}.xlsx
  And the workbook's "Questions" sheet header row reads
    Session | Question | Submitter | Email | Phase | Status | AiVerdict | AssignedToRole | Created

  When they instead tick the select-all / per-row checkboxes for exactly two rows
    then click "Export"
  Then the AdminGridExportRequest carries those two row Ids and a null Query
  And the workbook contains exactly those two rows (header + 2 data rows)

  Note: this page is export-only — questions are audience-submitted and moderated
  in place (approve / hide / escalate), so there is NO Import action and no import
  file picker on the toolbar. The export uses the direct simfAccount.downloadXlsx
  proxy, not the CrudGridExcel @ref helper. The server caps an export at 5000 rows.
  Since the queue moved onto the grid seam the export reads through the SAME paged
  list call, walking the pages up to that cap, so it keeps filter, search and sort
  parity with the grid by construction rather than by a second implementation.
```

**Evidence captured:**
- Network: a single POST `/account/api/admin/questions/export` returns 200 with an
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` body; no
  `/import` request exists (export-only page).
- The toolbar shows an "Export" action but **no "Import" action** — assert the
  import affordance is absent (this is not a full CrudShell conversion).
- Console errors: 0 expected.

### E2E-QQU-017 - The queue returns one server page and the true Pending total

```gherkin
Feature: One page of the committee queue
  As a Scientific-Committee member with Questions.View
  I want the queue to return the window I asked for and the true size of the bucket
  So that the pager is honest about how much triage is left

Background:
  Given the API is reachable and backed by a REAL SQL Server database
  And an administrator holding Questions.View has signed in
  And 60 questions across four sessions are in Status = Pending
  And a further 15 questions are Approved and 5 are Hidden

Scenario: The first page carries 20 rows and a total of 60
   When "POST /admin/questions/list" is called with
        """
        { "skip": 0, "top": 20 }
        """
   Then the response is 200 with "success": true
    And "data.items" holds 20 rows
    And "data.total" is 60, counted on the server BEFORE Skip and Take
    And "data.skip" is 0 and "data.top" is 20
    And the rows come back oldest first, the natural order createdAt ascending,
        so the committee works the head of the queue
    And no Approved and no Hidden question appears
    # 60 not 80: the total counts the DEFAULT Pending scope, which is composed onto
    # the source before the grid's own predicates. A total of 80 would mean the
    # count was taken over the whole table and the scope applied only to the page.

Scenario: A top above the cap is clamped to the resource's maximum
   When the call sends "top": 5000
   Then "data.items" holds at most 200 rows, the declared maximum
    And "data.total" is still 60
```

**Evidence captured:**
- `data.total` is compared against a separate `SELECT COUNT(*) FROM SessionQuestions
  WHERE Status = Pending`. A total equal to the number of rows returned, on a set of
  60, is the defect this asserts against.

### E2E-QQU-018 - The default bucket is Pending, and a blank status does not widen it

```gherkin
Feature: A queue that cannot silently list every bucket
  As a Scientific-Committee member
  I want a request that names no status to mean Pending
  So that the desk never quietly starts showing already-actioned questions

Background:
  Given 60 questions are Pending, 15 Approved and 5 Hidden

Scenario: No status filter means the Pending bucket
   When "POST /admin/questions/list" is called with
        """
        { "skip": 0, "top": 20 }
        """
   Then "data.total" is 60
    And every row returned has Status = Pending

Scenario: A status key with a BLANK value still means the Pending bucket
   When "POST /admin/questions/list" is called with
        """
        { "filters": { "status": "" } }
        """
   Then the response is 200
    And "data.total" is 60, not 80
    And every row returned has Status = Pending
    # This is the trap the guard exists for. The seam validates a blank-valued key
    # and then builds no predicate from it, so keying the default scope on the
    # PRESENCE of a status key would let status="" fall through both the scope and
    # the filter and return every bucket at once. The guard tests for a status with
    # a VALUE. The query-string parameter this replaced could not express the shape
    # at all, so nothing before now had to answer the question.

Scenario: An explicit status still selects exactly that bucket
   When "POST /admin/questions/list" is called with
        """
        { "filters": { "status": "Hidden" } }
        """
   Then "data.total" is 5
    And every row returned has Status = Hidden
```

### E2E-QQU-019 - The queue refuses keys it does not declare

```gherkin
Scenario: An undeclared sort key is a 400 that lists the real ones
   When "POST /admin/questions/list" is called with
        """
        { "sort": "notAColumn" }
        """
   Then the response is 400
    And "error.code" is "GRID_SORT_KEY_INVALID"
    And the message names notAColumn and then lists the sortable columns:
        session, question, phase, ai, status, sessionId, createdAt

Scenario: Sorting on the display-only Submitter column is the same 400
   When "POST /admin/questions/list" is called with
        """
        { "sort": "submitter" }
        """
   Then the response is 400 with "error.code" "GRID_SORT_KEY_INVALID"
    # The submitter's name lives in the Identity database and is resolved after the
    # page is chosen, because D-157 forbids the cross-database join that would make
    # it sortable. A 400 is the honest answer; the page therefore renders that
    # column with no sort button, so a user can never provoke this from the UI.

Scenario: An undeclared filter key is a 400, never a widened result set
   When "POST /admin/questions/list" is called with
        """
        { "filters": { "submitter": "Visitor One" } }
        """
   Then the response is 400
    And "error.code" is "GRID_FILTER_KEY_INVALID"
    And no rows are returned
    # An ignored filter would hand back the whole cross-session queue to a caller
    # who asked for one person's questions.

Scenario: An unparseable status value is a 400
   When "POST /admin/questions/list" is called with
        """
        { "filters": { "status": "2" } }
        """
   Then the response is 400 with "error.code" "GRID_FILTER_VALUE_INVALID"
    And the message says the value must be one of the QuestionStatus names
    # Enum filters parse by NAME only: an ordinal silently re-points at a different
    # member the day a value is appended to the enum.
```

### E2E-QQU-020 - The pager walks the queue without repeating a question

```gherkin
Scenario: Next carries the following 20 questions
  Given the 60 Pending questions include seven submitted within the same second,
        so the createdAt sort column ties
   When "POST /admin/questions/list" is called with skip 0 and top 20
    And the call is repeated with skip 20 and top 20
    And once more with skip 40 and top 20
   Then the three pages hold 20, 20 and 20 rows
    And "data.total" is 60 on all three
    And the union of the three pages is exactly the 60 Pending questions,
        each appearing exactly once
    And no question appears on two pages and none is skipped between them
    # The Id tiebreak is what makes this hold. Seven questions sharing a createdAt
    # have no defined order without it, so one can be returned on both page one and
    # page two while another is never returned at all, and a committee that works
    # the queue page by page would simply never see it.

Scenario: The page controls drive the same windows
  Given the administrator is on /admin/question-queue showing "Showing 1-20 of 60"
   When they click Next
   Then a POST /account/api/admin/questions/list fires with skip 20 and top 20
    And the summary reads "Showing 21-40 of 60"
    And the page label reads "Page 2 of 3"
   When they change the page size to 50
   Then the next request carries top 50 and skip 0
    And the summary reads "Showing 1-50 of 60"
   When they click Last
   Then the request carries skip 50 and the summary reads "Showing 51-60 of 60"
```

### E2E-QQU-021 - Filtering by status and by session narrows the total

```gherkin
Scenario: A sessionId filter scopes the queue to one session
  Given session "Autonomous Naval Systems" holds 18 of the 60 Pending questions
   When "POST /admin/questions/list" is called with
        """
        { "skip": 0, "top": 20, "filters": { "sessionId": "{that session id}" } }
        """
   Then the response is 200
    And "data.items" holds 18 rows, all from that session
    And "data.total" is 18, the size of the FILTERED set
    And "data.total" is not 60 (the whole Pending queue) and not 20 (the window)

Scenario: Status and session combine
   When the filters carry both "status": "Approved" and that same sessionId
   Then only Approved questions of that session come back
    And "data.total" is their count
    # Filters are AND-combined, and naming a status with a value suppresses the
    # default Pending scope, so this really does read the Approved bucket.

Scenario: A free-text search covers the session title, the question and the verdict
   When "POST /admin/questions/list" is called with
        """
        { "search": "drone" }
        """
   Then only Pending questions whose session title, question text or AI verdict
        contain "drone" come back
    And "data.total" is their count
    And the submitter's name is NOT searched, because it is not a searchable column

Scenario: A search term containing a percent sign matches literally
   When the search term is "100%"
   Then it is matched as the literal text "100%", not as a wildcard
    And a question reading "the 100% figure" matches while an unrelated one does not
```

---

## Implementation notes

- **Read-only triage page.** Unlike the CRUD pages (e.g. `/admin/interests`),
  this page has **no Add / Edit / Details / inline grid edit**. Every action is a
  single PUT against a question id (`approve`, `hide`, `escalate`) plus the
  `/list` POST that loads the page. Scenarios are scoped to exactly those actions,
  do not invent create/delete UI.
- **SimfDataGrid, server-paged.** The page renders the canonical `SimfDataGrid`
  (`Top = 20`) and its `OnQueryChanged` handler round-trips: every filter, sort and
  page gesture is a fresh `POST /admin/questions/list` carrying the `GridQuery`.
  Declared keys, all sortable and filterable: `session` (searchable), `question`
  (searchable), `phase`, `ai` (searchable), `status`, `sessionId`, `createdAt`.
  `submitter` is NOT a key in either direction: it is resolved from the Identity
  database after the page is chosen, so the page renders it as a plain column with
  no sort button and no filter box, and a request naming it is a 400. The old
  `status` / `sessionId` query-string parameters are now filter keys, and a request
  naming no status still gets the Pending bucket. **No bulk toolbar action**: the
  grid sets `Multiselect="true"` (select-all + checkboxes) but wires no
  `OnApproveSelected`/`OnDeleteSelected`/`CustomToolbar`, so the checkboxes are
  cosmetic; do not author a bulk scenario.
- **Two-stage pipeline (PRE questions).** Approve here only changes `Status`
  Pending → Approved; the approved question then surfaces on the **per-session
  moderator desk** (`SessionModerationDesk.razor`, stage 3) where push/reorder/hide
  happen. Escalate sets `AssignedToRole` without changing `Status`, so an escalated
  row stays in this queue.
- **Two-path Q&A (owner 2026-07-19).** This committee stage applies to **PRE**
  questions only. A **LIVE** question (asked after the session starts) skips the AI
  filter + this queue and auto-approves straight onto the moderator desk — verified
  by `SessionQuestionsTests.Live_question_skips_AI_and_lands_directly_on_the_moderator_desk`.
  The committee `Api.Tests` seed a future (pre) session so submissions land Pending
  here (`SessionQuestionCommitteeTests.SeedPreSessionAsync`).
- **BFF passthroughs** live in `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`
  (`/account/api/admin/questions/list|{id}/approve|{id}/hide|{id}/escalate`) →
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

_Last reviewed:_ 2026-08-18 by Claude: **the queue moved onto the shared grid
seam**: `GET /admin/questions/queue` became `POST /admin/questions/list` binding a
`GridQuery`, the `status` and `sessionId` query-string parameters became filter
keys, filtering and sorting stopped being an in-memory re-projection, and
E2E-QQU-017 to -021 were added for the paged contract. E2E-QQU-013 and -014 were
rewritten: they asserted that no request fires, which is now exactly backwards.
Prior: 2026-07-19 by Claude. **Two-path Q&A (owner): this queue now
receives PRE questions only; LIVE questions auto-approve straight to the moderator
desk, skipping the AI filter + this committee stage (golden example row is now
Phase "Pre").** Prior: 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle); export-only (no import, no presentation toggle, no delete-confirm on this read-only triage grid). Prior: 2026-06-03 (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
