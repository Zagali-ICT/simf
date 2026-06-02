# E2E test catalogue — Roles & permissions (`/admin/roles`)

| | |
|--|--|
| **Page** | [`cp/admin-roles.md`](../../pages/cp/admin-roles.md) |
| **Route** | `/admin/roles` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper. The page is gated by `PermissionCatalog.Roles.View`; the per-row "Permissions" link is gated by `PermissionCatalog.Roles.AssignPermissions`. |
| **Last reviewed** | 2026-06-02 |

> **Page gate (verified in source).** `RolesList.razor` carries
> `@attribute [RequirePermission(PermissionCatalog.Roles.View)]` — the
> per-page permission, NOT the older `[Authorize(Roles="Administrator")]`
> the page reference doc still shows. The API endpoints in
> `RoleEndpoints.cs` gate per action: list/get/get-permissions → `Roles.View`,
> create → `Roles.Create`, update → `Roles.Edit`, delete → `Roles.Delete`,
> set-permissions → `Roles.AssignPermissions`. All also require the
> `RequireApprovedAccount` policy; every write requires
> `RequireRateLimiting("auth")`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-ROL-001 | Full CRUD round-trip — Add → Details → Edit (rename) → Delete a custom role | happy | P0 | _to author_ |
| E2E-ROL-002 | Add role opens the modal and creates a Custom role (Custom pill, 0 users, 0 permissions) | happy | P1 | _to author_ |
| E2E-ROL-003 | Edit (rename) a custom role | happy | P1 | _to author_ |
| E2E-ROL-004 | Edit a baseline role shows the read-only notice (no form) | error | P1 | _to author_ |
| E2E-ROL-005 | Details modal renders the four-field description list | happy | P2 | _to author_ |
| E2E-ROL-006 | Delete a custom unused role | happy | P1 | _to author_ |
| E2E-ROL-007 | Filter + sort the grid by Name and by Type (Built-in/Custom) | happy | P2 | _to author_ |
| E2E-ROL-008 | Pager — page size + next/prev/first/last | happy | P2 | _to author_ |
| E2E-ROL-009 | Per-row "Permissions" link navigates to the per-role permission editor | happy | P1 | _to author_ |
| E2E-ROL-010 | Details modal "Edit permissions" button navigates to the permission editor | happy | P2 | _to author_ |
| E2E-ROL-011 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-ROL-012 | Auth gate — signed-in admin lacking `Roles.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-ROL-013 | Validation — submit a blank / over-64-char Name → in-modal error, no POST | error | P1 | _to author_ |
| E2E-ROL-014 | Conflict — duplicate role name → 409 `RoleNameDuplicate`, bilingual server message | error | P1 | _to author_ |
| E2E-ROL-015 | Delete a baseline role blocked → 409 `RoleIsBaseline` | error | P1 | _to author_ |
| E2E-ROL-016 | Delete an in-use role blocked → 409 `RoleInUse` (holder count in toast) | error | P1 | _to author_ |
| E2E-ROL-017 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-ROL-018 | RTL render — Arabic toggle mirrors page + Add modal | i18n | P1 | _to author_ |

## Scenarios

### E2E-ROL-001 — Full CRUD round-trip (golden)

```gherkin
Feature: Roles CRUD round-trip
  As an Administrator
  I want to create, inspect, rename and delete a custom RBAC role
  So that the Control Panel's role catalogue matches the event team's structure

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they hold the Roles.View, Roles.Create, Roles.Edit and Roles.Delete permissions
  And they have landed on /admin/roles
  And the grid loaded via POST /account/api/admin/roles/list returning HTTP 200

Scenario: Create, view, rename, then delete one custom role
  Given the grid shows the baseline rows (e.g. "Administrator" with the "Built-in" pill)
  And the grid currently shows {N} rows

  # --- Create ---
  When the administrator clicks "Add role"
  Then the "Add role" modal opens with a single field "Role name"
  And the helper text reads "1–64 characters; must be unique."
  When they fill Role name="Press Office"
  And they click "Create role"
  Then the modal closes
  And POST /account/api/admin/roles fires with body { "name": "Press Office" } and returns HTTP 200
  And the grid reloads and shows {N + 1} rows
  And a green toast reads 'Role "Press Office" was created.'
  And a row exists with Name="Press Office", Type pill "Custom", Users=0, Permissions=0

  # --- Details ---
  When the administrator clicks the "Details" icon on the "Press Office" row
  Then a read-only "Role details" modal opens
  And it renders a description list with Name="Press Office", Type="Custom", Users="0", Permissions="0"
  And no extra network request fires (the modal reads the row directly)
  When they click "Close"
  Then the modal closes

  # --- Edit (rename) ---
  When the administrator clicks the "Edit" icon on the "Press Office" row
  Then the "Edit role" modal opens with Role name pre-filled to "Press Office"
  When they change Role name to "Public Relations Office"
  And they click "Save changes"
  Then the modal closes
  And PUT /account/api/admin/roles/{id} fires with body { "name": "Public Relations Office" } and returns HTTP 200
  And a green toast reads 'Role "Public Relations Office" was updated.'
  And the row's Name column now reads "Public Relations Office"

  # --- Delete ---
  When the administrator clicks the "Delete" icon on the "Public Relations Office" row
  Then DELETE /account/api/admin/roles/{id} fires and returns HTTP 200
  And a green toast reads 'Role "Public Relations Office" was deleted.'
  And the grid reloads and the row no longer appears
  And the grid shows {N} rows again
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-roles-golden-before.png` (grid with baseline rows)
- Screenshot after add: `docs/screenshots/cp-admin-roles-add-modal.png`
- Screenshot after details: `docs/screenshots/cp-admin-roles-details-modal.png`
- Screenshot after edit: `docs/screenshots/cp-admin-roles-edit-modal.png`
- Screenshot after delete: `docs/screenshots/cp-admin-roles-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/roles/...` call returns 200 (one POST list per reload, one POST create, one PUT update, one DELETE)
- Audit rows: `AuditEntry` rows with `EventType = AuditEvents.RoleCreated`, then `RoleUpdated`, then `RoleDeleted`, each carrying the actor's user id and `Detail = "id=...; name=..."`

### E2E-ROL-002 — Add role creates a Custom role

```gherkin
Scenario: Adding a role always creates a custom (non-baseline) role
  Given the administrator is on /admin/roles
  When they click "Add role"
  And they fill Role name="Logistics Crew"
  And they click "Create role"
  Then POST /account/api/admin/roles returns ApiResult.Success = true
  And the returned AdminRoleSummary has IsBaseline = false, UserCount = 0, PermissionCount = 0
  And the new row shows the neutral "Custom" pill (never the "Built-in" admin-variant pill)
  And a green toast reads 'Role "Logistics Crew" was created.'
```

### E2E-ROL-003 — Rename a custom role

```gherkin
Scenario: Rename an existing custom role
  Given a custom role "Logistics Crew" exists in the grid
  When the administrator clicks its "Edit" icon
  Then the "Edit role" modal opens with the RoleForm (Initial = the row) and Role name pre-filled
  When they change Role name to "Logistics Team"
  And they click "Save changes"
  Then PUT /account/api/admin/roles/{id} returns HTTP 200
  And a green toast reads 'Role "Logistics Team" was updated.'
  And the row's Name column reads "Logistics Team"
```

### E2E-ROL-004 — Edit baseline role shows read-only notice

```gherkin
Scenario: Editing a built-in role offers no rename form
  Given the grid contains the baseline "Administrator" role (Type pill "Built-in")
  When the administrator clicks the "Edit" icon on the "Administrator" row
  Then the "Edit role" modal opens
  And it shows an info SimfAlert reading "This is a built-in role and cannot be renamed."
  And NO Role name field is rendered (the RoleForm is not shown)
  And only a "Close" button is offered
  And no PUT request fires
  When they click "Close"
  Then the modal closes and the row is unchanged
```

### E2E-ROL-005 — Details modal

```gherkin
Scenario: Details modal renders the four-field description list
  Given a role "Public Relations" exists with 3 users and 7 permissions
  When the administrator clicks the "Details" icon on that row
  Then a "Role details" modal opens
  And the description list shows Name="Public Relations", Type="Custom", Users="3", Permissions="7"
  And (when the admin holds Roles.AssignPermissions) an "Edit permissions" primary button is visible in the footer
  And a "Close" secondary button is visible
  When they click "Close"
  Then the modal closes with no network call
```

### E2E-ROL-006 — Delete a custom unused role

```gherkin
Scenario: Delete a custom role that no user holds
  Given a custom role "Temp Reviewers" exists with Users=0
  When the administrator clicks its "Delete" icon
  Then DELETE /account/api/admin/roles/{id} returns ApiResult.Success = true
  And a green toast reads 'Role "Temp Reviewers" was deleted.'
  And the grid reloads and the "Temp Reviewers" row is gone
  And an AuditEntry row with EventType = RoleDeleted is written for the actor
```

### E2E-ROL-007 — Filter and sort the grid

```gherkin
Scenario: Filter by name and sort by Name / Type
  Given the grid shows multiple roles
  When the administrator types "admin" in the filter box
  Then POST /account/api/admin/roles/list fires with Search="admin"
  And only rows whose Name contains "admin" remain (server-side EF.Functions.Like)
  When they clear the filter and click the "Name" column header
  Then the list re-requests with Sort="name", SortDescending=false and rows sort A→Z
  When they click the "Name" header again
  Then SortDescending=true and rows sort Z→A
  When they click the "Type" column header
  Then the list re-requests with Sort="baseline" and rows group by Built-in / Custom
```

### E2E-ROL-008 — Pager

```gherkin
Scenario: Page through the roles grid
  Given more than one page of roles exists at page size 20
  Then the summary reads "Showing 1–20 of {total}"
  And the pager reads "Page 1 of {pages}"
  When the administrator clicks "Next"
  Then POST /account/api/admin/roles/list fires with the next Skip and the summary advances
  When they click "Last page"
  Then the last page renders
  When they click "First page"
  Then the first page renders
  When they change the "Show" page size selector
  Then the list re-requests with the new Top value (server clamps Top to 1..200)
```

### E2E-ROL-009 — Per-row Permissions link

```gherkin
Scenario: The per-row Permissions link opens the permission editor
  Given the administrator holds Roles.AssignPermissions
  And a custom role "Press Office" exists
  When they click the "Permissions" link in that row's RowActions
  Then the browser navigates to /admin/roles/{id}/permissions
  And the per-role permission editor page loads

Scenario: The Permissions link is hidden without Roles.AssignPermissions
  Given the administrator holds Roles.View but NOT Roles.AssignPermissions
  When they view the grid
  Then no "Permissions" link is rendered in any row's RowActions
  (the link is wrapped in <AuthorizedAction Permission="Roles.AssignPermissions">)
```

### E2E-ROL-010 — Details "Edit permissions" navigation

```gherkin
Scenario: The Details modal's Edit permissions button navigates to the editor
  Given the administrator holds Roles.AssignPermissions
  And the "Role details" modal is open for the custom role "Press Office"
  When they click the "Edit permissions" primary button
  Then the browser navigates to /admin/roles/{id}/permissions (OpenPermissionEditor)
```

### E2E-ROL-011 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no SimfRole rows visible to the query (edge case — baseline roles normally seed at least one)
  When the administrator opens /admin/roles
  Then the grid body renders the SimfEmptyState component
  And the empty state title reads "No roles yet." / "لا توجد أدوار بعد."
  And the toolbar still shows the "Add role" button
  And no error toast appears
```

### E2E-ROL-012 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Roles.View is denied
  Given a signed-in CP user whose roles do NOT grant the Roles.View permission
  When they navigate to /admin/roles
  Then the RequirePermission attribute denies the page
  And they land on /not-permitted with HTTP 200
  And no /account/api/admin/roles/list request fires

Scenario: Unauthenticated visitor is challenged
  Given no CP auth cookie is present
  When the browser requests /admin/roles
  Then the cookie challenge redirects to /login
```

### E2E-ROL-013 — Validation failure

```gherkin
Scenario: Blank or too-long Role name is rejected in the modal
  Given the "Add role" modal is open
  When the administrator leaves Role name blank
  And clicks "Create role"
  Then a SimfAlert error appears inside the modal
  And it reads "Role name must be between 1 and 64 characters." / "يجب أن يتراوح طول اسم الدور بين 1 و 64 حرفاً."
  And the modal stays open
  And NO POST /account/api/admin/roles request fires (client-side guard in RoleForm.HandleSubmitAsync, length ∈ [1..64])

Scenario: Server-side guard rejects an over-long name that slips past the client
  Given a hand-crafted POST /account/api/admin/roles with a 65-character name
  Then the API returns HTTP 400 with ApiResult.Error.Code = "RoleInvalid"
  And the bilingual message reads "The role name must be between 1 and 64 characters." / "يجب أن يتراوح طول اسم الدور بين 1 و 64 حرفاً."
```

### E2E-ROL-014 — Duplicate name conflict

```gherkin
Scenario: Duplicate role name returns 409 RoleNameDuplicate
  Given a role named "Press Office" already exists
  When the administrator opens the "Add role" modal
  And fills Role name="Press Office"
  And clicks "Create role"
  Then the BFF forwards POST /admin/roles to the API
  And the API returns HTTP 409 with ApiResult.Error.Code = "RoleNameDuplicate"
  And the in-modal SimfAlert surfaces the bilingual MessageForCurrentCulture():
      "A role named 'Press Office' already exists." / "يوجد دور بالاسم 'Press Office' بالفعل."
  And the modal stays open
  And no new row is added

Scenario: Renaming a custom role onto an existing name returns 409
  Given custom roles "Press Office" and "Logistics Team" both exist
  When the administrator edits "Logistics Team" and sets Role name="Press Office"
  And clicks "Save changes"
  Then PUT /admin/roles/{id} returns HTTP 409 with Code = "RoleNameDuplicate"
  And the in-modal error surfaces the bilingual message and the modal stays open
```

### E2E-ROL-015 — Delete baseline blocked

```gherkin
Scenario: Deleting a built-in role is refused
  Given the grid contains the baseline "Administrator" role (Type pill "Built-in")
  When the administrator clicks its "Delete" icon
  Then DELETE /account/api/admin/roles/{id} returns HTTP 409 with Code = "RoleIsBaseline"
  And a red toast surfaces the bilingual server message:
      "Baseline roles cannot be deleted." / "لا يمكن حذف الأدوار الأساسية."
  And the "Administrator" row remains in the grid
```

### E2E-ROL-016 — Delete in-use role blocked

```gherkin
Scenario: Deleting a role that users still hold is refused with the holder count
  Given a custom role "Reviewers" exists and 2 admin users currently hold it
  When the administrator clicks its "Delete" icon
  Then DELETE /account/api/admin/roles/{id} returns HTTP 409 with Code = "RoleInUse"
  And a red toast surfaces the bilingual server message with the count interpolated:
      "The role cannot be deleted while 2 user(s) hold it." / "لا يمكن حذف الدور طالما يحمله 2 مستخدم(مستخدمين)."
  And the "Reviewers" row remains in the grid
```

### E2E-ROL-017 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on POST /admin/roles/list (e.g. DB down)
  When the administrator opens /admin/roles
  Then the grid shows the "Loading roles…" indicator
  And then a red toast appears reading "The roles could not be loaded." / "تعذّر تحميل الأدوار."
  And no rows render
```

### E2E-ROL-018 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/roles in English
  When they switch the UI culture to Arabic (العربية)
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الأدوار والصلاحيات"
  And the grid column headers read "الاسم" / "النوع" and flip to the right
  And the "Built-in" / "Custom" pills read "مدمج" / "مخصص"
  And the nav rail mirrors with Arabic labels
  And the pager arrows reverse

  When they click "Add role" (Arabic label)
  Then the "Add role" modal opens in RTL
  And the Role name field label reads "اسم الدور" with helper "من 1 إلى 64 حرفاً؛ يجب أن يكون فريداً."
  And the form action buttons appear in reverse order
```

---

## Implementation notes

- **Manual smoke is the canonical run today.** Until Playwright is adopted, the
  canonical execution of these scenarios is a Chrome DevTools MCP session: sign
  in per the Auth setup (`superadmin@zagali-ict.com` + `Get-Totp`), walk each
  scenario, and capture screenshots into `docs/screenshots/cp-admin-roles-*.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin block
  into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created)
  plus a step-definition class. The shape is already runner-agnostic.
- **Lower-layer API integration tests.** The `// Tests:` header in
  `AdminRoleService.cs` references `SIMF.Api.Tests/AdminRolesTests.cs`, which does
  **not** currently exist in the repo. The closest existing API-layer coverage of
  this surface is:
  - `tests/SIMF.Api.Tests/RolePermissionsEndpointsTests.cs` — the role→permission
    grant endpoints (`GET`/`PUT /admin/roles/{id}/permissions`, `Roles.AssignPermissions`).
  - `tests/SIMF.Api.Tests/AdminUserRolesTests.cs` — assigning roles to admin users.
  - `tests/SIMF.Api.Tests/MobileAppRoleTests.cs` — the mobile app-privilege role surface.
  There is **no** dedicated lower-layer test for the list/create/rename/delete
  CRUD on this page (the `RoleInvalid` / `RoleNameDuplicate` / `RoleIsBaseline` /
  `RoleInUse` guards in `AdminRoleService`). E2E-ROL-013..016 are therefore the
  primary coverage for those guards until an `AdminRolesTests.cs` lands — flag for
  follow-up.
- **Permission gate.** The page is gated by `PermissionCatalog.Roles.View`
  (`RequirePermission` attribute), not the role-name `[Authorize]` shown in the
  older page reference doc. E2E-ROL-012 asserts the real `/not-permitted`
  behaviour for a signed-in admin who lacks `Roles.View`.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
