# E2E test catalogue — Session moderators (`/admin/session-moderators`)

| | |
|--|--|
| **Page** | [`cp/admin-session-moderators.md`](../../pages/cp/admin-session-moderators.md) |
| **Route** | `/admin/session-moderators` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **What this page does.** D-169 (gap doc G6, PDF §2.7.2) admin desk for
> per-session moderator **grants**. It is *not* the moderator's own live
> Q&A desk (`/sessions/{id}/moderate`) and the grant is distinct from the
> mobile `MobileAppRole.Moderator`. The page is a single **`SimfDataGrid`**
> of existing grants (D-256 raw-table→grid conversion) plus an **Assign
> moderator** modal (raw `SessionId` + `UserId` GUID text fields) and a
> per-row **Revoke** quiet icon action. After the conversion the **Session**
> column is per-column **filterable**, the **Session** and **Assigned**
> columns are **sortable**, and the grid page size is `Top = 20`.
> `RequiredPermission = PermissionCatalog.SessionModerators.View`; assign
> needs `SessionModerators.Assign`, revoke needs `SessionModerators.Revoke`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SMD-001 | Golden round-trip — Assign a moderator then Revoke it | happy | P0 | _to author_ |
| E2E-SMD-002 | Empty list renders `SimfEmptyState` ("No moderators assigned yet.") | happy | P1 | _to author_ |
| E2E-SMD-003 | Auth gate — signed-in admin lacking `SessionModerators.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SMD-004 | "Assign moderator" opens the modal (SessionId + UserId fields) | function | P1 | _to author_ |
| E2E-SMD-005 | Cancel closes the modal without a POST | function | P2 | _to author_ |
| E2E-SMD-006 | Client validation — non-GUID id → bilingual error **inside the dialog**, no POST (BUG-004) | error | P1 | _to author_ |
| E2E-SMD-019 | Client validation — empty submit → bilingual error **inside the dialog**, no POST (BUG-004) | error | P1 | _to author_ |
| E2E-SMD-007 | Server validation — unknown SessionId → `SESSION_NOT_FOUND` (404) | error | P1 | _to author_ |
| E2E-SMD-008 | Server validation — inactive session → `SESSION_INVALID` (400) | error | P1 | _to author_ |
| E2E-SMD-009 | Server validation — unknown moderator user → `ADMIN_USER_NOT_FOUND` (404) | error | P1 | _to author_ |
| E2E-SMD-010 | Server validation — un-approved moderator → `AUTH_ACCOUNT_NOT_APPROVED` (400) | error | P1 | _to author_ |
| E2E-SMD-011 | Conflict / duplicate — already a moderator → `SESSION_MODERATOR_ALREADY_ASSIGNED` (409) | error | P1 | _to author_ |
| E2E-SMD-012 | Revoke is idempotent — re-revoking a gone grant still succeeds | function | P2 | _to author_ |
| E2E-SMD-013 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-SMD-014 | Pager summary — "Showing 1–N of T" reflects grid + Top=20 page size | function | P2 | _to author_ |
| E2E-SMD-015 | RTL / Arabic render mirrors page + Assign modal | i18n | P1 | _to author_ |
| E2E-SMD-016 | Per-column filter — typing in "Filter column Session" narrows the grid | function | P1 | _to author_ |
| E2E-SMD-017 | Column sort — Session / Assigned headers toggle asc↔desc | function | P2 | _to author_ |
| E2E-SMD-018 | Excel export — toolbar Export downloads an .xlsx of the grants (whole grid vs selected rows) (D-356) | happy | P1 | _to author_ |

## Scenarios

### E2E-SMD-001 — Golden round-trip (Assign → Revoke)

```gherkin
Feature: Session-moderator grant round-trip
  As an Administrator
  I want to assign a moderator to a specific session and later revoke it
  So that the right people moderate the right session Q&A

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator holding SessionModerators.View/Assign/Revoke has signed in
      via /login + /login/totp (TOTP from the Get-Totp helper)
  And they have landed on /admin/session-moderators
  And an active session exists with Code="S-01" and Title="Opening Plenary"
  And an Approved user exists with DisplayName="Sara Q." and Email="sara.q@example.com"

Scenario: Assign a moderator, see the new row, then revoke it
  Given the grid currently shows {N} grant rows
  When the administrator clicks "Assign moderator"
  Then the "Assign session moderator" modal opens with two fields:
       "Session id" and "Moderator user id"
  When they fill Session id="<the S-01 session GUID>"
  And they fill Moderator user id="<Sara Q. user GUID>"
  And they click "Assign"
  Then the BFF forwards POST /account/api/admin/session-moderators
  And the API returns HTTP 200 with ApiResult.Success = true
  And the modal closes
  And a green toast reads "Moderator assigned." / "تم تعيين المشرف."
  And the grid shows {N + 1} rows
  And a new row shows Session="S-01 — Opening Plenary",
      Moderator="Sara Q. (sara.q@example.com)",
      "Assigned by"=the acting admin's display name,
      and an "Assigned" timestamp in "yyyy-MM-dd HH:mm UTC" format

  When the administrator clicks that row's Revoke (link-off icon) action
       in the grid's RowActions column
  Then the BFF forwards DELETE /account/api/admin/session-moderators/{sessionId}/{userId}
  And the API returns HTTP 200 with ApiResult.Success = true
  And a green toast reads "Moderator grant revoked." / "تم إلغاء تعيين المشرف."
  And the grid reloads and the row is gone ({N} rows again)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-session-moderators-golden-before.png`
- Screenshot after assign: `docs/screenshots/cp-admin-session-moderators-golden-assigned.png`
- Screenshot after revoke: `docs/screenshots/cp-admin-session-moderators-golden-revoked.png`
- Console errors: 0 expected
- Network: `/account/api/admin/session-moderators/list`, `/account/api/admin/session-moderators` (POST) and `/account/api/admin/session-moderators/{sessionId}/{userId}` (DELETE) each return 200
- Audit rows: one `OperationLog` row `Event = 'SessionModerator.Assigned'` and one `Event = 'SessionModerator.Revoked'`, both carrying the acting admin's id as actor, Sara's id as subject, and `Detail = "sessionId=<S-01 GUID>"`

### E2E-SMD-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no SessionModerator grant rows
  When the administrator opens /admin/session-moderators
  Then the grid body renders the SimfEmptyState component
  And the empty state title reads "No moderators assigned yet." / "لا توجد تعيينات بعد."
  And the toolbar still shows the "Assign moderator" button
  And no error toast appears
```

### E2E-SMD-003 — Auth gate

```gherkin
Scenario: Signed-in admin lacking the View permission is denied
  Given a signed-in Control Panel user whose role does NOT grant
        SessionModerators.View (Administrator = "*" would pass; use a
        scoped role without it)
  When they navigate to /admin/session-moderators
  Then the [RequirePermission(SessionModerators.View)] gate redirects them
       to /not-permitted with HTTP 200
  And no /account/api/admin/session-moderators/list request fires
```

### E2E-SMD-004 — Assign modal opens

```gherkin
Scenario: "Assign moderator" opens the modal
  Given the administrator is on /admin/session-moderators
  When they click "Assign moderator"
  Then a SimfModal titled "Assign session moderator" opens
  And it shows exactly two SimfTextField inputs: "Session id" and "Moderator user id"
  And both fields start empty
  And the footer shows "Cancel" and "Assign" buttons
```

### E2E-SMD-005 — Cancel closes the modal

```gherkin
Scenario: Cancel discards the modal without calling the API
  Given the Assign modal is open with Session id and Moderator user id partly typed
  When the administrator clicks "Cancel"
  Then the modal closes
  And no /account/api/admin/session-moderators POST request fires
  And no toast appears
  And re-opening the modal shows both fields cleared
```

### E2E-SMD-006 — Client validation (non-GUID)

```gherkin
Scenario: A non-GUID id is rejected client-side with no POST
  Given the Assign modal is open
  When the administrator fills Session id="not-a-guid"
  And fills Moderator user id="<a valid GUID>"
  And clicks "Assign"
  Then the page's Guid.TryParse guard fails
  And a red SimfAlert renders INSIDE the dialog body (.simf-modal__body), not on
      the page behind the backdrop, reading
      "A session id and a user id are both required, and each must be a valid id." /
      "معرّف الجلسة ومعرّف المستخدم مطلوبان معاً، ويجب أن يكون كل منهما معرّفاً صحيحاً."
  And the modal stays open
  And no /account/api/admin/session-moderators POST request fires
```

> **BUG-004 (as-built).** The page-level toast is rendered inside
> `.simf-surface`, which sits under the modal backdrop
> (`.simf-modal { position: fixed; inset: 0; z-index: 100 }`), so the old
> `_toast` assignment was invisible while the dialog was open and the submit
> read as a dead button. The message is now a dedicated `_error` rendered in the
> dialog body — the same shape the canonical CRUD forms use. Server rejections
> (SMD-007..SMD-011) surface in the same place while the dialog is open.

### E2E-SMD-019 — Client validation (empty submit)

```gherkin
Scenario: Submitting the Assign dialog with both fields empty reports, and creates nothing
  Given the Assign modal is open with both fields empty
  When the administrator clicks "Assign" without typing anything
  Then a red SimfAlert renders INSIDE the dialog body reading
      "A session id and a user id are both required, and each must be a valid id." /
      "معرّف الجلسة ومعرّف المستخدم مطلوبان معاً، ويجب أن يكون كل منهما معرّفاً صحيحاً."
  And the modal stays open
  And no /account/api/admin/session-moderators POST request fires
  And the grid row count is unchanged
  And closing and re-opening the dialog clears the message
```

### E2E-SMD-007 — Unknown session

```gherkin
Scenario: Assigning against an unknown SessionId returns 404 SESSION_NOT_FOUND
  Given the Assign modal is open
  When the administrator fills Session id="<a random unused GUID>"
  And fills Moderator user id="<an approved user GUID>"
  And clicks "Assign"
  Then the BFF forwards POST /admin/session-moderators
  And the API returns HTTP 404 with ApiResult.Error.Code = "SESSION_NOT_FOUND"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture()
      "The session was not found." / "لم يتم العثور على الجلسة."
```

### E2E-SMD-008 — Inactive session

```gherkin
Scenario: Assigning to an inactive (soft-deleted) session returns 400 SESSION_INVALID
  Given an inactive session exists (IsActive = false)
  And the Assign modal is open
  When the administrator fills Session id="<the inactive session GUID>"
  And fills Moderator user id="<an approved user GUID>"
  And clicks "Assign"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_INVALID"
  And the error toast reads "Cannot assign a moderator to an inactive session." /
      "لا يمكن تعيين مشرف لجلسة غير مفعّلة."
  And the modal stays open
```

### E2E-SMD-009 — Unknown moderator user

```gherkin
Scenario: Assigning an unknown user returns 404 ADMIN_USER_NOT_FOUND
  Given the Assign modal is open with a valid active Session id
  When the administrator fills Moderator user id="<a random unused GUID>"
  And clicks "Assign"
  Then the API returns HTTP 404 with ApiResult.Error.Code = "ADMIN_USER_NOT_FOUND"
  And the error toast reads "The moderator user was not found." /
      "لم يتم العثور على المستخدم المُشرف."
  And the modal stays open
```

### E2E-SMD-010 — Un-approved moderator

```gherkin
Scenario: Assigning a not-yet-approved user returns 400 AUTH_ACCOUNT_NOT_APPROVED
  Given a user exists whose AccountState is not Approved (e.g. PendingApproval)
  And the Assign modal is open with a valid active Session id
  When the administrator fills Moderator user id="<that pending user GUID>"
  And clicks "Assign"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "AUTH_ACCOUNT_NOT_APPROVED"
  And the error toast reads "Moderator must be an approved account." /
      "يجب أن يكون المُشرف حساباً معتمداً."
  And the modal stays open
```

### E2E-SMD-011 — Conflict / duplicate

```gherkin
Scenario: Re-assigning the same user to the same session returns 409
  Given a grant already exists for Session id="<S-01 GUID>" + Moderator user id="<Sara Q. GUID>"
  When the administrator opens the Assign modal
  And fills the identical Session id + Moderator user id
  And clicks "Assign"
  Then the API returns HTTP 409 with ApiResult.Error.Code = "SESSION_MODERATOR_ALREADY_ASSIGNED"
  And the error toast reads "This user is already a moderator of the session." /
      "هذا المستخدم مشرف على الجلسة بالفعل."
  And the modal stays open
  And the grid row count is unchanged
```

### E2E-SMD-012 — Revoke idempotency

```gherkin
Scenario: Revoking a grant that no longer exists still succeeds
  Given a grant row for Session="S-01 — Opening Plenary" / Moderator="Sara Q." is visible
  And that grant has already been removed out-of-band (e.g. a second admin revoked it)
  When the administrator clicks the stale row's Revoke (link-off icon) action
  Then the BFF forwards DELETE /account/api/admin/session-moderators/{sessionId}/{userId}
  And the API returns HTTP 200 with ApiResult.Success = true (the service is idempotent)
  And a green toast reads "Moderator grant revoked." / "تم إلغاء تعيين المشرف."
  And after the reload the row is absent and no error toast appears
```

### E2E-SMD-013 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on /admin/session-moderators/list (e.g. DB down)
  When the administrator opens /admin/session-moderators
  Then the page first shows "Loading moderators…" / "جارٍ تحميل المشرفين…"
  And then a red toast appears reading
      "The session moderators could not be loaded." / "تعذّر تحميل مشرفي الجلسات."
  And no grant rows render
```

### E2E-SMD-014 — Pager summary

```gherkin
Scenario: The grid footer summary reflects the page window
  Given more than one grant exists (Top page size is 20, ordered by AssignedAt desc)
  When the administrator opens /admin/session-moderators
  Then the footer reads "Showing 1–{count} of {total}" /
      "عرض 1–{count} من {total}"
  And {count} equals the number of rows rendered on the first page
  And {total} equals the total grant count
```

### E2E-SMD-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Assign modal
  Given the administrator is on /admin/session-moderators in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "مشرفو الجلسات"
  And the column headers read "الجلسة", "المشرف", "من قام بالتعيين", "تاريخ التعيين"
  And the grid's Add toolbar button reads "تعيين مشرف"
  And the per-row Revoke (link-off icon) action carries the title "إلغاء التعيين"

  When they click "تعيين مشرف"
  Then the Assign modal opens in RTL titled "تعيين مشرف للجلسة"
  And the field labels read "معرّف الجلسة" and "معرّف المستخدم المشرف"
  And the footer buttons read "إلغاء" and "تعيين" in reverse order
```

### E2E-SMD-016 — Per-column filter narrows the grid

```gherkin
Scenario: Typing a value in the Session column filter narrows the grid
  Given the administrator is on /admin/session-moderators
  And several grants exist across sessions Code="S-01"/"S-02"/"S-03"
  And the grid is showing the first page (Skip = 0) of all grants
  When they open the "Filter column" picker on the Session column
  And they type "S-01" into the "Filter column Session" input
  Then the BFF forwards POST /account/api/admin/session-moderators/list
       carrying GridQuery.Filters["session"] = "S-01"
  And the request resets GridQuery.Skip to 0
  And the API matches the value against the session Code / Title / TitleArabic
      (server-side Contains)
  And the grid re-renders showing only the S-01 grant rows
  And the footer summary "Showing 1–{count} of {total}" reflects the
      narrowed total
  And clearing the filter input re-fires /list with no "session" key and
      restores the full list
```

### E2E-SMD-017 — Column sort toggles

```gherkin
Scenario: Sorting the Session column toggles ascending then descending
  Given the administrator is on /admin/session-moderators with more than one grant
  And the grid is in its default order (most-recently AssignedAt first)
  When they click the sortable "Session" column header
  Then the BFF forwards POST /account/api/admin/session-moderators/list
       carrying GridQuery.Sort = "session" and GridQuery.SortDescending = false
  And the rows re-order ascending by session Code then Title
  When they click the "Session" header again
  Then /list re-fires with GridQuery.Sort = "session" and
       GridQuery.SortDescending = true
  And the rows re-order descending

Scenario: The Assigned column is also sortable
  Given the administrator is on /admin/session-moderators
  When they click the sortable "Assigned" column header
  Then /list re-fires with GridQuery.Sort = "assignedAt" and
       GridQuery.SortDescending = false
  And the rows re-order oldest-assigned first
  And the Moderator and "Assigned by" columns are NOT sortable
      (Identity-DB names are resolved on read, not server-sortable — D-157)
```

### E2E-SMD-018 — Excel export (D-356)

```gherkin
Scenario: Export the grant grid to an XLSX workbook
  Given the administrator is on /admin/session-moderators with at least two grant rows
  And no rows are selected
  When they click the grid toolbar "Export" action
  Then the page calls simfAccount.downloadXlsx against
       /account/api/admin/session-moderators/export
  And the request body is an AdminGridExportRequest with an empty Ids list
      and the current GridQuery (Query is sent only because no rows are selected)
  And the API caps the export at 5000 rows
  And the browser saves an .xlsx workbook whose header row carries the grant
      columns Session | Moderator | AssignedBy | AssignedAt
  And the body rows mirror the on-screen grants (session Code/Title, moderator
      display name + email, assigning admin, and the UTC AssignedAt timestamp)

Scenario: Export only the selected grant rows
  Given the administrator is on /admin/session-moderators
  And they tick the row checkboxes for two specific grant rows
  When they click the toolbar "Export" action
  Then simfAccount.downloadXlsx posts an AdminGridExportRequest whose Ids list
       holds exactly those two rows' UserId values
  And Query is null (selection overrides the filter)
  And the saved workbook contains exactly those two grant rows
```

**Evidence captured:**
- Screenshot of the toolbar Export action firing → `docs/screenshots/cp-admin-session-moderators-export.png`
- Network: a single POST to `/account/api/admin/session-moderators/export` returns 200 with the `.xlsx` content type
- Console errors: 0 expected
- Note: this page is **export-only** — there is no Import action (the grid wires
  `OnExport` but not `OnImport`), and unlike the converted CRUD pages it streams
  the workbook via the `simfAccount.downloadXlsx` JS proxy rather than rendering
  a `CrudGridExcel` component.

---

## Implementation notes

- **Lower-layer coverage.** `tests/SIMF.Api.Tests/AdminSessionModeratorsTests.cs`
  already covers this surface without a browser:
  `Assign_returns_row_with_session_and_moderator_projection` (golden assign →
  SMD-001), `Assign_with_unknown_session_is_SESSION_NOT_FOUND` (SMD-007),
  `Assign_duplicate_returns_SESSION_MODERATOR_ALREADY_ASSIGNED` (SMD-011),
  `Revoke_removes_grant_and_is_idempotent` (SMD-012) and
  `Non_admin_caller_is_forbidden_on_assignment` (the API-layer twin of the
  SMD-003 auth gate). The inactive-session (SMD-008), unknown-user (SMD-009)
  and un-approved-user (SMD-010) branches are exercised by the service
  guards in `AdminSessionModeratorService.AssignAsync` but do **not** yet have
  a dedicated API test — they are E2E-only here.
- **Manual smoke is canonical today.** Until Playwright is adopted, the
  canonical run is a Chrome DevTools MCP session: sign in per the Background,
  walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-session-moderators-*.png`.
- **Convert to Playwright** when the runner lands: each Gherkin block copies
  into a `.feature` under `tests/SIMF.E2E.Tests/` (project to be created) with
  a step-definition class. The steps are already runner-agnostic.
- **Data note.** The page takes raw GUIDs (`Session id`, `Moderator user id`)
  rather than pickers — capture the real GUIDs from the Sessions grid / Users
  list before running, since there is no in-page lookup. The DB-down (SMD-013)
  and forced server-error scenarios require a controllable API (stub/fault
  injection or a stopped SQL Server).

---

_Last reviewed:_ 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle): added E2E-SMD-018 (Excel export); toggle/import/CrudShell-delete confirmed NOT present (export-only page).
_Last reviewed:_ 2026-07-26 by Claude (BUG-004): the Assign dialog's validation message now renders inside the dialog body instead of behind the backdrop; reworded E2E-SMD-006 and added E2E-SMD-019 (empty submit).
