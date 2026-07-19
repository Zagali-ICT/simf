# Question queue — `/admin/question-queue`

| | |
|--|--|
| **Route** | `/admin/question-queue` |
| **Audience** | Administrator / Scientific-Committee member |
| **Auth** | `[RequirePermission(PermissionCatalog.Questions.View)]` (page); API actions add `RequireApprovedAccount` + `RequireRateLimiting("auth")` on the mutating endpoints |
| **Pattern** | P3.3 / D-234 — read-only **moderation/triage queue** (Approve / Hide / Escalate), **not** CRUD. Canonical `SimfDataGrid` since D-261, but **client-side projection**. |
| **Status** | ✅ Real (P3.3 / D-234) |
| **Permissions** | `Questions.View` (page + queue read), `Questions.Moderate` (Approve + Hide), `Questions.Escalate` (Escalate), `Questions.Export` (Excel export) |
| **Backend endpoints (BFF → API)** | `GET /account/api/admin/questions/queue` → `GET /api/v1/admin/questions/queue`; `PUT /account/api/admin/questions/{id}/approve` → `…/approve`; `PUT …/{id}/hide` → `…/hide`; `PUT …/{id}/escalate` → `…/escalate`; `POST /account/api/admin/questions/export` → `POST /api/v1/admin/questions/export` |
| **Source** | [`QuestionQueueList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/QuestionQueueList.razor), [`AccountEndpoints.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs) (BFF passthroughs), [`SessionQuestionCommitteeEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Sessions/SessionQuestionCommitteeEndpoints.cs) (API), [`QuestionQueueExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/QuestionQueueExcelEndpoints.cs) (export) |
| **Tests** | [`docs/tests/e2e/cp-admin-question-queue.md`](../../tests/e2e/cp-admin-question-queue.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

P3.3 / D-234 (Completion Programme §5.3) — the Scientific-Committee central Q&A
queue (**stage 2** of the question pipeline). It lists **Pending** questions
across **all** sessions and lets a committee member triage each one:

- **Approve** (`Questions.Moderate`) — moves the question `Pending → Approved`; it
  then surfaces on the **per-session moderator desk** (`SessionModerationDesk.razor`,
  stage 3) where push / reorder / hide happen during the live session.
- **Hide** (`Questions.Moderate`) — moves the question `Pending → Hidden`; it drops
  off the pipeline.
- **Escalate** (`Questions.Escalate`) — sets `AssignedToRole` **without changing
  `Status`**, so the row stays `Pending` and re-appears in the queue after reload
  (now carrying the assigned role).

This is a **read-only triage grid** — there is **no Add / Edit / Details / inline
grid edit / delete** on this page. Questions are submitted by the audience from the
app and are moderated in place. The three actions are quiet per-row **icon**
buttons in the grid's `RowActions` slot (Approve = `check-circle`, Hide = `eye-off`,
Escalate = `share`).

> **Two-path Q&A (owner 2026-07-19).** This committee stage receives **PRE
> questions only**. A question asked while the session is **LIVE** skips the AI
> filter **and** this committee queue and auto-approves straight to the per-session
> moderator desk (`SessionModerationDesk.razor`, stage 3). So a freshly-submitted
> live question never lands `Pending` here; only pre-session (`Phase = Pre`)
> questions do. (Legacy `Pending` rows created before the change may still carry the
> Live phase.) The routing lives in `SessionQuestionService.SubmitAsync`.

## 4. UI

- `SimfBanner` titled `Admin.QuestionQueue.Title`, inside `simf-page-wide` /
  `simf-surface`.
- A single `SimfDataGrid` (`TItem = SessionQuestionQueueRow`, `Top = 20`,
  `Multiselect="true"`) renders the Pending queue.
- A transient `SimfAlert` toast (`success` / `error`) renders above the grid after
  each action and after a failed load.
- The grid toolbar carries the canonical **Export** action (`OnExport` wired,
  label `Grid.Export`) — see §4.5. There is **no Import, no Add, no bulk-action**
  toolbar button.
- `Multiselect` renders the select-all + per-row checkboxes, but no bulk toolbar
  handler is wired, so on this page the checkboxes serve **only** to scope the
  Excel export to the ticked rows — there is no bulk Approve/Hide/Delete.
- **Escalate modal** (`SimfModal`, opened by the share icon): titled
  `Admin.QuestionQueue.Escalate.Title`, with a single `SimfTextField`
  (`Admin.QuestionQueue.Escalate.Role`) and `Escalate` / `Cancel` footer buttons.

## 4.5 Queue columns + moderation actions

This is a triage queue, **not a create/edit form** — there are no form fields to
document. The grid surface is:

**Columns** (left → right):

| Key | Header | Sortable | Filterable | Renders |
|-----|--------|----------|------------|---------|
| `session` | Session | yes | yes | `SessionTitle` |
| `question` | Question | yes | yes | `QuestionText` |
| `submitter` | Submitter | yes | yes | `SubmittedByDisplayName` |
| `phase` | Phase | yes | no | `SimfPill` — `Pre` (variant `off`) / `Live` (variant `on`) |
| `ai` | AI verdict | no | yes | `AiFilterVerdict` or `—` when null |

**Per-row actions** (`RowActions` slot, icon buttons, disabled while `_busy`):

| Icon | Tooltip | Permission | Effect |
|------|---------|------------|--------|
| `check-circle` | Approve | `Questions.Moderate` | `PUT …/{id}/approve` (empty body) → `Pending → Approved`, leaves the queue |
| `eye-off` | Hide | `Questions.Moderate` | `PUT …/{id}/hide` (empty body) → `Pending → Hidden`, leaves the queue |
| `share` | Escalate | `Questions.Escalate` | opens the Escalate modal → `PUT …/{id}/escalate` with `{ Role }`; sets `AssignedToRole`, **stays Pending** |

A view-only member (`Questions.View` but not `Moderate`/`Escalate`) sees the grid
with an **empty** `RowActions` cell — each action group is wrapped in
`<AuthorizedAction Permission="…">`.

## 5. Data flow + endpoints

- **Load.** `OnInitializedAsync` → `LoadAsync` calls `simfAccount.getJson`
  `/account/api/admin/questions/queue`. The BFF passthrough in `AccountEndpoints.cs`
  forwards to `SimfAdminClient.ListQuestionQueueAsync` → API
  `GET /api/v1/admin/questions/queue` (`SessionQuestionCommitteeEndpoints`, gated by
  `Questions.View` + `RequireApprovedAccount`). The read is **non-paged**: it returns
  the whole Pending queue once, **oldest-first, capped at 200**.
- **Client-side projection (D-261).** `OnQueryChanged` does **not** round-trip.
  `BuildPage()` filters (case-insensitive `Contains`), sorts and pages the
  in-memory `_rows` list. Filter keys honoured: `session`, `question`, `submitter`,
  `ai`. Sort keys: `session`, `question`, `submitter`, `phase`. Default order =
  the backend's oldest-first `CreatedAt`. So a filter / sort / page gesture
  re-projects the already-fetched list with **no** extra network call.
- **Actions.** Approve / Hide go through `ActAsync` (`simfAccount.putJson`, empty
  body); Escalate through `EscalateAsync` (`putJson` with `EscalateQuestionRequest`).
  Each BFF passthrough forwards to the API committee endpoint, which is gated by
  the respective permission + `RequireApprovedAccount` + `RequireRateLimiting("auth")`.
  On success the page shows a green toast and re-issues the `/queue` GET.

## 6. Validation + error handling

- **Escalate role** — the API guards the role string (1–64 chars). A blank or
  >64-char role returns HTTP 400 `SessionQuestionInvalid`; the bilingual
  `MessageForCurrentCulture()` is shown in an error toast and the modal stays open
  (`_escalateId` still set, queue not reloaded).
- **Stale / missing row** — Approve / Hide / Escalate on an id that no longer
  resolves returns HTTP 404 `SessionQuestionNotFound` (bilingual error toast).
  Re-actioning an already-Approved row is idempotent at the service (the status
  guard skips the write) and still returns 200.
- **Load failure** — a non-success envelope (or a 500) shows the envelope's error
  message if present, else the `Admin.QuestionQueue.LoadFailed` fallback; no rows
  render.
- Every error surfaces the envelope `Error.MessageForCurrentCulture()`, falling
  back to `Admin.QuestionQueue.Fallback` for the action paths.

## 7. Edge cases + known limitations

- **Escalate does not remove the row.** It only sets `AssignedToRole`; `Status`
  stays `Pending`, so the row re-appears after the post-action reload — by design.
- **Checkboxes are not a bulk action.** `Multiselect` is on, but no bulk handler is
  wired; the only consumer of the selection is the Excel export (selected rows →
  exported rows). There is no bulk Approve/Hide.
- **Queue cap.** The backend returns at most the 200 oldest Pending rows. The grid
  pages that set in memory; there is no server-side paging contract to extend.
- **Two-stage pipeline.** Approve here only flips `Status`; the live-session
  push / reorder / hide happen later on the per-session moderator desk (stage 3).

## 8. i18n + RTL

`Admin.QuestionQueue.*` resx keys (Title, Loading, None, LoadFailed, Fallback,
`Col.Session|Question|Submitter|Phase|Ai`, `Phase.Pre|Live`,
`Action.Approve|Hide|Escalate`, `Escalate.Title|Role|Submit|Cancel`, plus the
shared `Grid.*` keys) with EN ↔ AR parity. RTL mirrors the banner, columns, the
per-row tooltips (اعتماد / إخفاء / تصعيد) and the Escalate modal.

## 10. Use cases

- Approve a vetted question so it flows to the moderator desk (`E2E-QQU-001`).
- Hide off-topic / spam questions (`E2E-QQU-002`).
- Escalate a technical question to a role (`E2E-QQU-003`).
- Export the Pending queue for offline committee review (`E2E-QQU-015`).

## 11. E2E

See [`docs/tests/e2e/cp-admin-question-queue.md`](../../tests/e2e/cp-admin-question-queue.md):
E2E-QQU-001 approve golden, 002 hide, 003 escalate, 004 escalate cancel, 005 empty
state, 006 auth gate (View), 007 action gate (Moderate / Escalate), 008 escalate
validation, 009 stale-row 404, 010 server-500 on load, 011 RTL, 012 AI-verdict +
Phase rendering, 013 in-memory column filter, 014 in-memory column sort,
015 Excel export (D-356).

## 12. Excel export (D-356) — export only

The toolbar's **Export** action posts an `AdminGridExportRequest` to
`/account/api/admin/questions/export` via `simfAccount.downloadXlsx` (the generic
proxy, not the `CrudGridExcel` helper) and downloads
`simf-questions-{timestamp}.xlsx`. The API endpoint
(`ExportQuestionQueueEndpoint`, `POST /api/v1/admin/questions/export`) is gated by
`Questions.Export`.

- **Selection scoping.** With no rows selected the page sends the current grid
  `Query` (and an empty `Ids` list), so the whole filtered Pending queue is
  exported. With rows ticked it sends those `Ids` and a null `Query`, so only those
  rows are exported.
- **Session scoping.** The export `ListAsync` mirrors the CP queue's load —
  `service.ListQueueAsync(status: null, sessionId: null)` — i.e. the default
  Pending queue across **all** sessions; the page passes no status / session filter.
- **Sheet + columns.** Sheet name `Questions`; header row:
  `Session | Question | Submitter | Email | Phase | Status | AiVerdict | AssignedToRole | Created`
  (mapping `SessionTitle`, `QuestionText`, `SubmittedByDisplayName`,
  `SubmittedByEmail`, `Phase` → `Pre`/`Live`, `Status` →
  `Pending`/`Approved`/`Hidden`, `AiFilterVerdict`, `AssignedToRole`, `CreatedAt`).
- **Row cap.** The export is capped at 5000 rows (the shared
  `AdminGridExportEndpoint` cap), though the queue list itself is already capped at
  200 oldest-first Pending rows.
- **No import.** This is an **export-only** page — questions are audience-submitted
  and moderated in place, so there is **no Import action, no import file picker, and
  no import endpoint**. (No presentation toggle and no delete-confirm form either —
  this is a read-only triage grid, not a `CrudShell` conversion.)

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05 | D-234 (P3.3) | Original — Scientific-Committee central Q&A queue (stage 2): Approve / Hide / Escalate triage over the cross-session Pending queue. |
| 2026-05 | D-261 | Migrated from the raw `<table>` to the canonical `SimfDataGrid` with **client-side** filter / sort / paging over the non-paged Pending read. |
| 2026-06-11 | D-356 | Excel **export only** added (toolbar Export → `POST /account/api/admin/questions/export`, sheet "Questions", gated by `Questions.Export`); no import. E2E catalogue extended with E2E-QQU-015. |
| 2026-07-19 | Owner (two-path Q&A) | This queue now receives **PRE questions only** — a LIVE question auto-approves straight to the moderator desk, skipping the AI filter + this committee stage. No UI/permission change; the routing is in `SessionQuestionService.SubmitAsync`. |

_Last reviewed:_ 2026-07-19 by Claude — **two-path Q&A: PRE questions only reach
this committee queue; LIVE questions auto-approve to the moderator desk.** Prior:
2026-06-11 by Claude (D-356 — Excel export-only).
