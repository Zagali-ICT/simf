# Audience comments moderation — `/admin/comments-moderation`

| | |
|--|--|
| **Route** | `/admin/comments-moderation` |
| **Audience** | Administrator (and any role granted the `Comments.*` permissions) |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.Comments.View)]`. API list gated on `Comments.View`; status + delete gated on `Comments.Moderate`; export gated on `Comments.Export`. All require `RequireApprovedAccount`; mutations + export `RequireRateLimiting("auth")`. |
| **Pattern** | D-199 (Mockup page 28 — "Audience comments" / تعليقات الجمهور). Session-scoped **moderation/review desk** (approve / hide / soft-delete), **not** full CRUD — comments originate from the public app, so there is no Add/Create and no import. Raw `<table>` → canonical `SimfDataGrid` (D-256). |
| **Status** | ✅ Real (D-199; grid conversion D-256; Excel export D-356) |
| **Backend endpoints (BFF `/account/api/admin/*` → API)** | List `POST /account/api/admin/sessions/{sessionId}/comments/list` → API `POST /admin/sessions/{sessionId:guid}/comments/list` (`Comments.View`). Set status `PUT .../comments/{commentId}/status` → API `PUT /admin/sessions/{sessionId:guid}/comments/{commentId:guid}/status` (`Comments.Moderate`). Delete `DELETE .../comments/{commentId}` → API `DELETE /admin/sessions/{sessionId:guid}/comments/{commentId:guid}` (`Comments.Moderate`). Export `POST /account/api/admin/comments-moderation/export` → API `POST /admin/comments-moderation/export` (`Comments.Export`). The session picker is filled from the existing `POST /account/api/admin/sessions/list`. |
| **Source** | [`CommentsModerationList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CommentsModerationList.razor), [`AdminSessionCommentEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Sessions/AdminSessionCommentEndpoints.cs), [`ExportCommentsEndpoint`](../../../src/Backend/SIMF.Api/Endpoints/Admin/CommentsExcelEndpoints.cs), [`AdminSessionCommentService`](../../../src/Backend/SIMF.Infrastructure/SessionComments/AdminSessionCommentService.cs), [`IAdminSessionCommentService`](../../../src/Backend/SIMF.Application/SessionComments/Abstractions/IAdminSessionCommentService.cs), [`SessionComments` contracts](../../../src/Shared/SIMF.Contracts/Sessions/SessionComments.cs), [`SessionCommentStatus`](../../../src/Shared/SIMF.Common/Enums/SessionCommentStatus.cs) |
| **Backed by** | `dbo.SessionComments` (`SimfAppDbContext`, D-199 `EventModules` migration; like-count column added D-223). Cross-DB author lookup against `SIMF_Identity.Users` (resolve-on-read; no FK — D-157). |
| **Tests** | [`docs/tests/e2e/cp-admin-comments-moderation.md`](../../tests/e2e/cp-admin-comments-moderation.md); API integration `tests/SIMF.Api.Tests/SessionCommentsTests.cs`; export `tests/SIMF.Api.Tests/CommentsExcelTests.cs` |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

The moderation desk over a session's **audience comments** (SIMF-FDS / Mockup
page 28). Comments are submitted by the public app; this page lets an
administrator clear them for display, withhold them, or remove them. The admin
first picks **one live session** from a `<select>` that sits **above** the grid
(comments are session-scoped — the picker is not a grid column), then sees every
active comment for that session (Pending / Approved / Hidden) in the canonical
`SimfDataGrid`, each row showing the author (+ email), the comment body, the
moderation status, the AI-filter verdict, and the submitted time.

This is a **review surface, not a CRUD page**: there is no create path (comments
come from the public app) and no Excel **import** (D-356 ships **export only**).
The three actions are Approve, Hide, and soft-Delete.

## 4. UI

- `SimfBanner` titled `Admin.Comments.Title`, a hint line (`Admin.Comments.Hint`),
  and the session `<select>` (label `Admin.Comments.Pick`, empty option
  `Admin.Comments.PickNone`). Options read `{Code} — {Title}`, sorted by Code,
  filtered to active sessions only.
- When no active sessions exist, the `<select>` is replaced by a `SimfEmptyState`
  (`Admin.Comments.NoSessions`); no list call fires.
- Once a session is picked, the `SimfDataGrid` renders (`Top = 20` page size,
  `Multiselect="true"`). Switching sessions clears any stale toast and resets the
  query (`new GridQuery { Top = 20 }`); selecting the empty option clears the grid.
- A page-level toast (`SimfAlert`) surfaces success / error messages above the grid.
- There are **no modals** — status changes are inline icon actions; Delete fires a
  native `confirm()` dialog. The select-all + per-row checkboxes exist (grid
  default) but there is **no bulk-action toolbar**; selection feeds Excel export only.

### 4.5 Grid columns + moderation actions

**Columns** (`SimfDataGrid` / `SimfDataGridColumn`):

| Key | Header (i18n) | Sortable | Filterable | Cell |
|-----|---------------|----------|------------|------|
| `author` | `Admin.Comments.Col.Author` | no | no | Author display name + `<small>(email)</small>` (email is PII — admin-only). |
| `body` | `Admin.Comments.Col.Body` | no | **yes** | The comment text. Its per-column filter maps to the backend `Search` field. |
| `status` | `Admin.Comments.Col.Status` | no | no | `SimfPill`: Approved → `on`, Hidden → `off`, Pending → `admin`. |
| `aiVerdict` | `Admin.Comments.Col.AiVerdict` | no | no | The AI-filter verdict text, or `—` when null. |
| `created` | `Admin.Comments.Col.Created` | **yes** | no | `CreatedAt.ToLocalTime()` (`g`). The only server-honoured sort key (`created`). |

**Per-row actions** — quiet icon affordances, no filled text buttons:

- **Approve** (`check-circle`, `Admin.Comments.Approve`) — rendered only when the
  row is **not** already Approved. `PUT .../status` with `Status = Approved (1)`.
- **Hide** (`eye-off`, `Admin.Comments.Hide`) — rendered only when the row is
  **not** already Hidden. `PUT .../status` with `Status = Hidden (2)`.
- Both Approve and Hide live inside `<AuthorizedAction Permission="PermissionCatalog.Comments.Moderate">`,
  so a `Comments.View`-only admin does not even see them.
- **Delete** (trash, `OnDeleteOne` / `Admin.Comments.Delete`) — soft-delete. Fires a
  native `confirm()` (`Admin.Comments.Delete.Confirm`); on accept, `DELETE .../comments/{id}`.

On success each action shows a green toast (`Admin.Comments.StatusSaved` /
`Admin.Comments.Deleted`) and reloads the grid.

### Excel export (D-356 — export only)

- The grid wires `OnExport` only (no `OnImport`): comments originate from the
  public app, so there is **no import path** for this desk.
- The toolbar **Export** action (`Grid.Export`) calls `simfAccount.downloadXlsx`
  against `POST /account/api/admin/comments-moderation/export` (BFF → API
  `POST /admin/comments-moderation/export`), passing an `AdminGridExportRequest`:
  - `Ids` — the currently selected comment ids (empty ⇒ the whole picked-session set).
  - `Query` — a `GridQuery` carrying the current `Sort` / `SortDescending`, the
    on-screen body filter mapped to `Search`, and crucially
    `Filters["sessionId"] = {picked session id}`.
- The export endpoint (`ExportCommentsEndpoint`) reads `Query.Filters["sessionId"]`
  to scope the export to the picked session, then delegates to the same
  `IAdminSessionCommentService.ListAsync` the list endpoint uses, with `status = null`
  — so the workbook covers **every** status (Pending + Approved + Hidden) for that
  session, mirroring the desk's default view. With no (or an unparseable) `sessionId`
  the endpoint exports **nothing** (empty set) rather than dumping every session.
- Worksheet **"Comments"**, downloaded as `simf-comments-{yyyyMMddHHmmss}.xlsx`.
  Header row / columns: **`Author | Email | Body | Status | AiVerdict | Created`**
  (Status rendered as the text "Pending" / "Approved" / "Hidden").
- The base `AdminGridExportEndpoint` caps the whole-set export at **5000 rows**
  (`MaxExportRows`; `Skip` reset to 0, `Top` set to 5000) and applies the `Ids`
  filter after listing.

## 5. Data flow + endpoints

1. **Sessions** — on init, `OnInitializedAsync` → `LoadSessionsAsync` posts
   `POST /account/api/admin/sessions/list` (`GridQuery { Top = 200 }`); active
   sessions are kept, sorted by Code.
2. **List** — selecting a session (or changing the body filter / sort / page) posts
   `POST /account/api/admin/sessions/{sessionId}/comments/list` with a body of
   `Skip / Top / Search / Sort / SortDescending` (the API route composes these into
   a `GridQuery` because `GridQuery` is sealed). The CP maps `GridQuery.Filters["body"]`
   → the body `Search`. Status filter is left `null` (all statuses) from the desk.
   Returns `ApiResult<GridPage<SessionCommentModerationRow>>`.
3. **Set status** — `PUT .../comments/{commentId}/status` with
   `SetSessionCommentStatusRequest { Status }`. Returns the updated
   `SessionCommentModerationRow`.
4. **Delete** — `DELETE .../comments/{commentId}` → `ApiResult<bool>` (Data = true).
5. **Export** — see §4.5.

`SessionCommentModerationRow` carries `Id, SessionId, UserId, AuthorDisplayName,
AuthorEmail, Body, Status, AiFilterVerdict, CreatedAt, ModeratedByUserId,
ModeratedAt`. The author display name + email are projected via a cross-DB lookup
against `SIMF_Identity.Users` (resolve-on-read, no FK — D-157).

## 6. Validation + error handling

- **Server paging clamp** — `Top` is clamped to `[1, 200]` (default 25); `Skip`
  floored at 0. The export base resets `Skip = 0` and sets `Top = 5000`.
- **Not found** — acting on a missing / already soft-deleted comment throws
  `ApiException(ErrorCodes.SessionCommentNotFound, 404, …)` →
  `SESSION_COMMENT_NOT_FOUND` (bilingual: "The comment was not found on this
  session." / "لم يتم العثور على التعليق على هذه الجلسة.").
- **Forbidden** — a `Comments.View`-only caller hitting a status/delete endpoint
  gets HTTP 403 (the action gate is `Comments.Moderate`); the CP surfaces the
  bilingual error toast.
- **Idempotent status** — `SetStatusAsync` returns early (unchanged row, no audit
  row) when the requested status already equals the current status.
- **Client fallback toasts** — list/sessions failures fall back to
  `Admin.Comments.LoadFailed` / `Admin.Comments.SessionsLoadFailed` when the
  envelope carries no error message.

## 7. Edge cases + known limitations

- **No create / no import.** Comments are authored by the public app; this desk
  only moderates. D-356 added **export only** — there is deliberately no import.
- **Soft-delete only.** Delete sets `IsActive = false` (row retained for audit);
  it does not hard-delete. Deleted comments drop from both the desk and the public
  feed immediately.
- **Status pills vs. the mockup's two-state badge.** The mockup shows the audience
  a "waiting / answered" badge; the moderation model behind it is the three-state
  `SessionCommentStatus` (Pending / Approved / Hidden). "Answered" is a separate
  speaker-workflow display concern, not a moderation state.
- **Single Filterable / Sortable column.** Only `body` is filterable (→ `Search`)
  and only `created` is sortable (server-honoured key `created`).
- **Checkboxes are export-only.** Select-all + per-row checkboxes exist but there
  is no bulk Approve/Hide/Delete; selection only narrows the Excel export.
- **Export defends the session scope.** A request without a parseable `sessionId`
  filter exports an empty workbook rather than every session's comments.

## 8. i18n + RTL

`Admin.Comments.*` keys (Title, Hint, Pick, PickNone, NoSessions, None, Loading,
Col.Author/Body/Status/AiVerdict/Created, Status.Pending/Approved/Hidden, Approve,
Hide, Delete, Delete.Confirm, StatusSaved, Deleted, LoadFailed,
SessionsLoadFailed) plus the shared `Grid.*` keys. EN ↔ AR parity; the banner,
hint, picker, grid headers, status pills and icon tooltips all mirror under
`dir="rtl"`.

## 10. Use cases

- Moderate a session's audience comments — approve for display, hide from the
  public feed, or soft-delete (Mockup page 28 / D-199).
- Export a session's comments (every status, or selected rows) to XLSX for offline
  review (D-356).

## 11. E2E

See [`docs/tests/e2e/cp-admin-comments-moderation.md`](../../tests/e2e/cp-admin-comments-moderation.md):
E2E-CMT-001 golden (pick → Approve → Hide → Delete), 002 session picker round-trip,
003 no-sessions empty state, 004 no-comments empty state, 005 Approve, 006 Hide,
007 Delete confirmed, 008 Delete cancelled, 009 idempotent status, 010 auth gate
(no `Comments.View`), 011 permission split (`View` without `Moderate` → 403),
012 not-found (`SESSION_COMMENT_NOT_FOUND`), 013/014 server-500 fallback toasts,
015 RTL, 016 body filter → `Search`, 017 `created` sort toggle, 018 Excel export
(whole set vs selected rows; session-scoped; "Comments" sheet header
`Author | Email | Body | Status | AiVerdict | Created`).

## 12. Related docs

- Decisions: D-199 (event modules + this desk), D-223 (comment likes — additive
  column), D-256 (raw-table → `SimfDataGrid`), D-356 (Excel export).
- CP nav: `CpNavigation` item `Module.Moderation` → `/admin/comments-moderation`,
  `RequiredPermission = PermissionCatalog.Comments.View`.
- Permission catalogue: `Comments.View`, `Comments.Moderate`, `Comments.Export`
  (all `AdminOnly` baseline) — `src/Shared/SIMF.Common/PermissionCatalog.cs`.
- Public counterpart: the audience submission / feed service
  (`ISessionCommentService`) consumed by the Flutter app.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-30 | D-199 | Original — session-scoped audience-comments moderation desk (list + Approve/Hide/soft-Delete), `SessionComments` table, `SessionCommentStatus` enum, three `Comments.*` permissions. |
| (P5) | D-256 | Raw `<table>` converted to the canonical `SimfDataGrid` (select-all + per-row checkboxes, body filter, `created` sort, quiet icon row actions). |
| 2026-06-10 | D-356 | Excel **export only** added — toolbar Export → `POST /admin/comments-moderation/export` (gated on `Comments.Export`), session-scoped via `Query.Filters["sessionId"]`, all statuses, "Comments" sheet `Author | Email | Body | Status | AiVerdict | Created`, capped at 5000 rows, `simf-comments-{timestamp}.xlsx`. No import path. E2E extended with E2E-CMT-018. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 — reference doc backfill; Excel export only).
