# Session moderators — `/admin/session-moderators`

| | |
|--|--|
| **Route** | `/admin/session-moderators` |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(SessionModerators.View)]` (page) + per-action `SessionModerators.Assign` / `SessionModerators.Revoke` / `SessionModerators.Export` + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (assign / revoke / export) |
| **Pattern** | D-169 (gap doc G6, PDF §2.7.2) admin desk for per-session moderator **grants**. D-256 raw-table → `SimfDataGrid` conversion. Cross-DB resolve-on-read (D-157). |
| **Status** | ✅ Real |
| **Backend endpoints** | BFF `POST /account/api/admin/session-moderators/list`, `POST /account/api/admin/session-moderators`, `DELETE /account/api/admin/session-moderators/{sessionId}/{userId}`, `POST /account/api/admin/session-moderators/export` → API `POST /admin/session-moderators/list`, `POST /admin/session-moderators`, `DELETE /admin/session-moderators/{sessionId:guid}/{userId:guid}`, `POST /admin/session-moderators/export` |
| **Source** | [`SessionModeratorsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionModeratorsList.razor), [`SessionModeratorEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionModeratorEndpoints.cs), [`SessionModeratorsExcelEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionModeratorsExcelEndpoints.cs), [`AdminSessionModeratorService`](../../../src/Backend/SIMF.Infrastructure/SessionQuestions/AdminSessionModeratorService.cs), [`IAdminSessionModeratorService`](../../../src/Backend/SIMF.Application/SessionQuestions/Abstractions/IAdminSessionModeratorService.cs), [`SessionModerators` contracts](../../../src/Shared/SIMF.Contracts/Admin/SessionModerators.cs) |
| **Backed by** | `dbo.SessionModerators` (`SimfAppDbContext`) — composite key `(SessionId, UserId)`. Moderator + assigner identity lives in `SIMF_Identity` and is resolved on read (D-157). |
| **Tests** | [`docs/tests/e2e/cp-admin-session-moderators.md`](../../tests/e2e/cp-admin-session-moderators.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

The admin desk for **per-session moderator grants** — assigning a specific
user to moderate a specific session's Q&A (D-169, gap doc G6, PDF §2.7.2).
A grant is a composite-key `(SessionId, UserId)` row in `dbo.SessionModerators`;
the page lists every existing grant, lets an admin **assign** a new one (by raw
`SessionId` + `UserId` GUIDs) and **revoke** an existing one from the row.

This is deliberately distinct from two adjacent things and the source comments
call out the collision:

- It is **not** the moderator's own live Q&A / comments desk
  (`/sessions/{id}/moderate`, gated by `SessionModeration.Moderate`).
- The grant is **not** the mobile `MobileAppRole.Moderator` (PDF §4.2
  naming-collision rule) — it is a per-resource permission, not an in-app role.

Admins assign; moderators do not self-promote (the assign/revoke endpoints are
`AdministratorOnly` via the permission catalogue).

## 4. UI

- `SimfBanner` titled `Admin.SessionModerators.Title`, hosted in a
  `simf-page-wide` / `simf-surface` shell.
- A single **`SimfDataGrid`** (`TItem="AdminSessionModeratorRow"`, multiselect,
  page size `Top = 20`) of existing grants. Row key is `"{SessionId}/{UserId}"`
  and the row label is the moderator's display name.
- Grid columns:
  - **Session** — `{Code} — {Title}` (or `{Code} — {TitleArabic}` under Arabic);
    **sortable** and per-column **filterable**.
  - **Moderator** — `{ModeratorDisplayName} (ModeratorEmail ?? "—")`.
  - **Assigned by** — `AssignedByDisplayName`.
  - **Assigned** — `AssignedAt` rendered `yyyy-MM-dd HH:mm 'UTC'`; **sortable**.
- **Assign moderator** toolbar button (grid `OnAdd`) opens a `SimfModal`
  (`Admin.SessionModerators.Add.Title`) with two `SimfTextField` inputs —
  **Session id** and **Moderator user id** — plus Cancel / Submit footer buttons.
- Per-row **Revoke** quiet icon action (`link-off` icon) in `RowActions`,
  wrapped in `<AuthorizedAction Permission="SessionModerators.Revoke">`.
- `SimfEmptyState` (`Admin.SessionModerators.None`) when there are no grants.
- A `SimfAlert` toast surface for success / error feedback.
- **Excel export only (D-356):** the grid exposes an `OnExport` toolbar action
  (`Grid.Export` label). It posts an `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/session-moderators/export` via the
  `simfAccount.downloadXlsx` JS proxy and streams back an `.xlsx`. **There is no
  Import** — the grid wires `OnExport` but not `OnImport`, and the service has no
  import path (grants are created in place via assign/revoke on the composite
  key). See §5.

## 4.5 Form fields (Assign modal)

| Field | Required | Type | Validation |
|-------|----------|------|------------|
| Session id | yes | GUID text | client-side `Guid.TryParse`; must resolve to an active session (server) |
| Moderator user id | yes | GUID text | client-side `Guid.TryParse`; must resolve to an **Approved** Identity user (server) |

The page takes **raw GUIDs**, not pickers — there is no in-page lookup, so the
admin captures the GUIDs from the Sessions grid / Users list beforehand. A blank
or non-GUID value fails the client guard and surfaces
`Admin.SessionModerators.Required` **inside the dialog** with no POST fired
(BUG-004 — see §6).

## 5. Data flow + endpoints

- **List** — `OnInitializedAsync` / `OnQueryChanged` POSTs the `GridQuery` to the
  BFF `/account/api/admin/session-moderators/list`, which forwards to the API
  `ListSessionModeratorsEndpoint` (`service.ListAllAsync`). The service joins
  `SessionModerators` to `Sessions` for the session Code/Title/TitleArabic
  (filterable/sortable), pages server-side, then resolves the moderator and
  assigner **display name + email** from `SimfIdentityDbContext.Users` in a
  single second query keyed by the page's user ids — **no cross-DB JOIN** (D-157).
  Because those names come from the Identity DB on read, the **Moderator** and
  **Assigned by** columns are not server-sortable/filterable.
- **Assign** — the modal POSTs `AssignSessionModeratorRequest { SessionId, UserId }`
  to `/account/api/admin/session-moderators` → API `AssignSessionModeratorEndpoint`
  (`service.AssignAsync(actorId, …)`, actor from the `sub` claim). On success the
  modal closes, a success toast shows, and the grid reloads.
- **Revoke** — the row action DELETEs
  `/account/api/admin/session-moderators/{sessionId}/{userId}` → API
  `RevokeSessionModeratorEndpoint` (`service.RevokeAsync`). Idempotent (a no-op
  when the grant is already gone), so re-revoking a stale row still returns 200.
- **Export (D-356)** — `OnExport` posts `AdminGridExportRequest`
  (`Ids` = selected rows' `UserId` values, or empty + the current `Query` when
  nothing is selected) through the generic BFF `MapGridExport("session-moderators")`
  proxy to the API `ExportSessionModeratorsEndpoint`. That endpoint extends the
  generic `AdminGridExportEndpoint<AdminSessionModeratorRow>`, delegates listing
  to the same `service.ListAllAsync`, and renders the workbook (see §7 for the
  sheet / columns / cap). The grant's `UserId` is the row's selectable identity
  (`IdOf`) for a selected-rows export.

Both assign, revoke and export carry `RequireRateLimiting("auth")`. Every
successful assign / revoke writes an `OperationLog` audit row
(`SessionModerator.Assigned` / `SessionModerator.Revoked`) carrying the acting
admin as actor, the moderator as subject, and `Detail = "sessionId=<guid>"`.

## 6. Validation + error handling

Server-side in `AdminSessionModeratorService.AssignAsync` (verified order):

- **Unknown session** → 404 `SESSION_NOT_FOUND` ("The session was not found." /
  "لم يتم العثور على الجلسة.").
- **Inactive session** (`IsActive = false`) → 400 `SESSION_INVALID` ("Cannot
  assign a moderator to an inactive session." / "لا يمكن تعيين مشرف لجلسة غير
  مفعّلة.").
- **Unknown moderator user** → 404 `ADMIN_USER_NOT_FOUND` ("The moderator user
  was not found." / "لم يتم العثور على المستخدم المُشرف.").
- **Un-approved moderator** (`AccountState != Approved`) → 400
  `AUTH_ACCOUNT_NOT_APPROVED` ("Moderator must be an approved account." / "يجب
  أن يكون المُشرف حساباً معتمداً.").
- **Duplicate grant** `(SessionId, UserId)` → 409
  `SESSION_MODERATOR_ALREADY_ASSIGNED` ("This user is already a moderator of the
  session." / "هذا المستخدم مشرف على الجلسة بالفعل.").

Client-side, a blank or non-GUID `Session id` / `Moderator user id` fails the
page's `Guid.TryParse` guard and shows `Admin.SessionModerators.Required` ("A
session id and a user id are both required, and each must be a valid id." /
"معرّف الجلسة ومعرّف المستخدم مطلوبان معاً، ويجب أن يكون كل منهما معرّفاً
صحيحاً.") with no POST.

**Where the message renders (BUG-004).** The page-level `_toast` `SimfAlert`
lives inside `.simf-surface`, which sits **under** the modal backdrop
(`.simf-modal { position: fixed; inset: 0; z-index: 100 }`). While the Assign
dialog is open a toast is therefore invisible, and the submit read as a dead
button. The dialog now carries its own `_error`, rendered as a
`SimfAlert Variant="error"` in the dialog body — the same shape the canonical
CRUD forms (e.g. `SessionCategoriesAddEdit`) use. Every server rejection above
lands there too while the dialog is open; `_error` is cleared when the dialog is
re-opened. A failed `/list` (e.g. server 500) happens with no dialog open, so it
still shows the bilingual `LoadFailed` toast on the page.

## 7. Excel export (D-356) — export only

- **Endpoint:** `ExportSessionModeratorsEndpoint` (`POST /admin/session-moderators/export`),
  gated by `SessionModerators.Export` + `RequireApprovedAccount`, rate-limited
  on the `auth` policy.
- **Worksheet:** `SheetName = "SessionModerators"`; downloaded file prefix
  `simf-session-moderators` (the BFF names the saved file
  `simf-session-moderators-{yyyyMMddHHmmss}.xlsx`).
- **Columns (verified header row, in order):** `SessionCode`, `SessionTitle`,
  `SessionTitleArabic`, `Moderator` (display name), `Email` (moderator email,
  may be blank), `AssignedBy` (assigning admin display name), `AssignedAt`
  (`yyyy-MM-dd HH:mm 'UTC'`, invariant culture).
- **Selection vs whole-grid:** when rows are selected the request sends their
  `UserId` values in `Ids` (and `Query = null`); when nothing is selected it
  sends the current `GridQuery` and an empty `Ids`. The base lists the rows
  (capped) then, if `Ids` is non-empty, filters to the wanted ids.
- **Row cap:** the whole-grid export is capped at `MaxExportRows = 5000` (the
  base resets `Skip = 0` and forces `Top = 5000`).
- **No import:** there is deliberately **no** `/import` route or `OnImport`
  wiring for this page — grants are managed in place via assign/revoke against
  the composite key, so import would have no insert semantics. This is the
  point on which it differs from the converted CRUD pages (which carry both
  Export and Import).

## 7b. Edge cases + known limitations

- **Raw-GUID entry, no pickers.** Assign requires hand-entered GUIDs; mistyped
  values are caught client-side (format) then server-side (existence/approval).
- **Revoke is idempotent.** Revoking a grant another admin already removed still
  returns 200 with the success toast.
- **Identity columns not server-sortable/filterable.** Moderator and
  "Assigned by" names are resolved on read from `SIMF_Identity` (D-157), so only
  the **Session** and **Assigned** columns sort, and only **Session** filters.
- **Streamed download, not a `CrudGridExcel` component.** Unlike the converted
  CRUD pages, export streams the workbook via the `simfAccount.downloadXlsx` JS
  proxy.

## 8. i18n + RTL

`Admin.SessionModerators.*` resx keys (title, column headers, modal title +
field labels, Assign/Cancel/Submit, Revoke action title, empty state, loading,
and the success/failure toasts) with EN ↔ AR parity. Under Arabic the page
renders RTL, the Session column uses `SessionTitleArabic`, and the modal mirrors.
Shared grid chrome uses the `Grid.*` keys (Export, Filter, paging, Select-all,
Actions, etc.). _(Exact Arabic strings are descriptive here; the resx files are
the source of truth.)_

## 10. Use cases

- Assign a user as moderator of a session (golden path).
- Revoke a session-moderator grant.
- Export the current grant set (or selected rows) to `.xlsx`.

## 11. E2E

See [`docs/tests/e2e/cp-admin-session-moderators.md`](../../tests/e2e/cp-admin-session-moderators.md):
E2E-SMD-001 golden assign → revoke round-trip, 002 empty state, 003 auth gate,
004–005 assign modal open / cancel, 006 client non-GUID guard, 007 unknown
session 404, 008 inactive session 400, 009 unknown user 404, 010 un-approved
user 400, 011 duplicate 409, 012 revoke idempotency, 013 list 500 fallback,
014 pager summary, 015 RTL, 016 per-column filter, 017 column sort,
**018 Excel export (D-356)**.

## 12. Related docs

- E2E catalogue: `docs/tests/e2e/cp-admin-session-moderators.md`.
- Decisions: D-169 (admin moderator-grants desk), D-256 (raw-table → grid),
  D-157 (Data ↔ Identity separation), D-356 (grid Excel export wave).
- Authority: PDF §2.7.2 (admin desk), §4.2 (moderator naming-collision rule).
- Adjacent: the live moderation desk `/sessions/{id}/moderate`
  (`SessionModeration.Moderate`); the Scientific-Committee Q&A queue
  `/admin/question-queue` (`Questions.View`).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| — | D-169 | Original — admin session-moderator grants desk (list + assign modal + per-row revoke), composite-key `(SessionId, UserId)`, cross-DB name resolution on read (D-157). |
| — | D-256 | Raw `<table>` → `SimfDataGrid` conversion: per-column Session filter, Session/Assigned sort, `Top = 20` paging, `SimfEmptyState`. |
| 2026-06-11 | D-356 | Excel **export only** added — toolbar Export → `POST /account/api/admin/session-moderators/export` (sheet "SessionModerators", 7-column workbook, 5000-row cap) via `simfAccount.downloadXlsx`. New `SessionModerators.Export` permission (AdminOnly). No import (grant lifecycle is assign/revoke). E2E catalogue extended with E2E-SMD-018. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 Excel export-only documentation pass).
_Last reviewed:_ 2026-07-26 by Claude (BUG-004 — the Assign dialog's validation message moved from the hidden page toast into the dialog body).
