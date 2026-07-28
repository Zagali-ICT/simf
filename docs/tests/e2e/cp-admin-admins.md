# E2E test catalogue — Admins CRUD (`/admin/admins`)

| | |
|--|--|
| **Page** | [`cp/admin-admins.md`](../../pages/cp/admin-admins.md) |
| **Route** | `/admin/admins` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` / `[REDACTED - supply via SIMF_SuperAdmin__TempPassword]` + TOTP via the `Get-Totp` helper |
| **Page permission** | `PermissionCatalog.Admins.View` (`@attribute [RequirePermission(PermissionCatalog.Admins.View)]`) |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> This is the **gold-standard D-117 CRUD reference page**. It carries the full
> canonical toolbar (Add / Edit-roles / Details / Delete / Duplicate / Copy /
> Paste / Import / Export), multiselect, the full pager, and six modals. Each
> distinct action is its own scenario below. Per-action API gates differ from
> the page gate: list/details = `Admins.View`, Add + Duplicate = `Admins.Create`,
> Delete (single + bulk) = `Admins.Delete`, Edit-roles = `Admins.AssignRoles`,
> Export = `Admins.Export`, Import = `Admins.Import`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-USR-001 | Golden path — Add admin → invited row appears (PendingApproval) | happy | P0 | _to author_ |
| E2E-USR-002 | Add validation — blank email / short display name → in-modal SimfAlert, no POST | error | P1 | _to author_ |
| E2E-USR-003 | Add conflict — duplicate email → 409 `ADMIN_EMAIL_ALREADY_REGISTERED`, modal stays open | error | P1 | _to author_ |
| E2E-USR-004 | Edit roles — open modal, toggle role, Save → "Roles updated for {email}." | happy | P0 | _to author_ |
| E2E-USR-005 | Details — read-only modal renders 5 fields, Close dismisses | happy | P1 | _to author_ |
| E2E-USR-006 | Per-row Delete — reason gate (10–500) + confirm → "1 deleted, 0 skipped." | happy | P0 | _to author_ |
| E2E-USR-007 | Bulk Delete — select 3, type reason, submit → "3 deleted, 0 skipped." + reload | happy | P0 | _to author_ |
| E2E-USR-008 | Self-delete guard — select own row → "0 deleted, 1 skipped." | auth | P1 | _to author_ |
| E2E-USR-009 | Bulk Delete with no selection → "Select at least one row first." error toast | error | P2 | _to author_ |
| E2E-USR-010 | Duplicate — clone with new email → "Created {email}." new row appears | happy | P1 | _to author_ |
| E2E-USR-011 | Duplicate validation — Submit disabled until email contains `@` | error | P2 | _to author_ |
| E2E-USR-012 | Export — select all → Export downloads an `.xlsx` | happy | P1 | _to author_ |
| E2E-USR-013 | Import — pick a `.xlsx` → result modal "{created} created, {skipped} skipped" + error rows | happy | P1 | _to author_ |
| E2E-USR-014 | Copy / Copy selected — info toast (stub) | happy | P2 | _to author_ |
| E2E-USR-015 | Paste — empty clipboard → "The clipboard is empty." / non-empty → "Paste-to-add…" stub | happy | P2 | _to author_ |
| E2E-USR-016 | Reset 2FA row-action link → navigates to `/admin/reset-2fa?email=…` | happy | P2 | _to author_ |
| E2E-USR-017 | Filter + sort + pager round-trip on the grid | happy | P1 | _to author_ |
| E2E-USR-018 | Empty list renders `SimfEmptyState` ("No accounts yet.") | happy | P1 | _to author_ |
| E2E-USR-019 | Auth gate — signed-in admin lacking `Admins.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-USR-020 | Server 500 on `/list` → empty grid, no crash | resilience | P2 | _to author_ |
| E2E-USR-021 | RTL / Arabic render — page + Add modal mirror | i18n | P1 | _to author_ |
| E2E-USR-022 | Presentation toggle persists across reload (`simf.cp.prefs.admins`) (D-353) | happy | P1 | _to author_ |
| E2E-USR-023 | Full-page mode round-trip — Add/Details take over the content area, Save/Close returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-USR-024 | Excel import rejection — non-`.xlsx` / oversized / wrong-sheet upload → 400 + bilingual toast, nothing created (D-356) | error | P1 | _to author_ |
| E2E-USR-025 | Name column renders the admin's profile-photo thumbnail (SimfIdentityCell) when `HasAvatar`, else an initials tile; no avatar request when none; broken bytes fall back to the placeholder, not a broken glyph (D-357) | happy | P2 | _to author_ |

## Scenarios

### E2E-USR-001 — Golden path (Add admin)

```gherkin
Feature: Admins CRUD golden path
  As an Administrator
  I want to invite a new administrator
  So that a new operator can be onboarded to the Control Panel

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (Get-Totp)
  And they have landed on /admin/admins
  And the grid header reads "Admins" (EN) and shows columns: Email, Display name, State, Role, 2FA

Scenario: Invite a new administrator via the Add modal
  Given the grid currently shows {N} rows
  When the administrator clicks the "Add" toolbar button
  Then the Add modal opens titled "Add user"
  And it hosts the CreateAdminForm with fields: Email address, Display name, and a Roles checkbox group
  When they fill Email address="naval.ops@simf.test"
  And they fill Display name="Naval Operations Lead"
  And they tick the "Administrator" role checkbox
  And they click "Create user"
  Then the BFF posts /account/api/admin/admins with { Email, DisplayName, Roles:["Administrator"] }
  And the API returns HTTP 200 with ApiResult.Data.Email="naval.ops@simf.test"
  And the modal closes
  And a green toast reads "Account created for naval.ops@simf.test. The invitation email has been queued."
  And the grid reloads and shows {N + 1} rows
  And a row exists with Email="naval.ops@simf.test", State="PendingApproval", the "Administrator" role pill, and the grey "Off" 2FA pill
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-admins-golden-before.png`
- Screenshot after (toast + new row): `docs/screenshots/cp-admin-admins-golden-after.png`
- Screenshot of the Add modal: `docs/screenshots/cp-admin-admins-add-modal.png`
- Console errors: 0 expected
- Network: `/account/api/admin/admins` (create) returns 200, then `/account/api/admin/admins/list` (reload) returns 200; every `/account/api/admin/admins/*` call is 200
- Audit row: an Identity `RowAudit` Insert row for the new `SimfUser`; a `PasswordReset` AccountCode is minted (7-day invite). New account lands `AccountState = PendingApproval`.

### E2E-USR-002 — Add validation failure

```gherkin
Scenario: Blank email / short display name show in-modal errors and fire no POST
  Given the Add modal is open
  When the administrator leaves Email address blank
  And sets Display name="X" (1 char)
  And clicks "Create user"
  Then a SimfAlert error renders inside the form
  And the Email field shows "Enter a valid email address."
  And the Display name field shows "Display name must be 2–128 characters."
  And the modal stays open
  And no /account/api/admin/admins POST request fires (client-side guard in CreateAdminForm.HandleSubmitAsync)
```

### E2E-USR-003 — Add conflict (duplicate email)

```gherkin
Scenario: Duplicate email returns 409 with the bilingual server message
  Given an admin account with Email="naval.ops@simf.test" already exists
  When the administrator opens the Add modal
  And fills Email address="naval.ops@simf.test" + Display name="Duplicate Attempt"
  And clicks "Create user"
  Then the BFF forwards POST /account/api/admin/admins
  And the API returns HTTP 409 with ApiResult.Error.Code = "ADMIN_EMAIL_ALREADY_REGISTERED"
  And the form renders a SimfAlert error with the bilingual MessageForCurrentCulture()
  And the modal stays open
  And the grid row count is unchanged
```

### E2E-USR-004 — Edit roles

```gherkin
Scenario: Toggle an admin's RBAC roles and save
  Given the grid shows a row for Email="naval.ops@simf.test"
  When the administrator clicks the "Edit" row action on that row
  Then the Edit-roles modal opens titled "Edit roles — naval.ops@simf.test"
  And it shows the intro copy and one SimfCheckbox per assignable role
  And the roles the user currently holds are pre-ticked
    (GET /account/api/admin/admins/{id}/roles supplies the current set;
     POST /account/api/admin/roles/list supplies the catalogue)
  When they tick an additional role "Content Editor"
  And they click "Save roles"
  Then the BFF sends PUT /account/api/admin/admins/{id}/roles with { Roles:[...] }
  And the API returns HTTP 200 (gated by Admins.AssignRoles)
  And the modal closes
  And a green toast reads "Roles updated for naval.ops@simf.test."
  And the grid reloads
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-admins-edit-roles-modal.png`
- Network: `GET .../{id}/roles` = 200, `POST .../roles/list` = 200, `PUT .../{id}/roles` = 200
- Note: if the caller lacks `Roles.View` the catalogue comes back empty and the modal shows "No roles are available to assign." with Save disabled.

### E2E-USR-005 — Details modal

```gherkin
Scenario: Details opens a read-only modal with no extra fetch
  Given the grid shows a row for Email="naval.ops@simf.test"
  When the administrator clicks the "Details" row action
  Then a read-only modal opens titled "User details — naval.ops@simf.test"
  And a description list renders five fields: Email, Display name, State, Role, Two-factor
  And the values match the row (no /account/api/admin call fires — Details reads AdminUserSummary directly)
  When they click "Close"
  Then the modal closes
```

### E2E-USR-006 — Per-row Delete (reason gate)

```gherkin
Scenario: Delete a single admin via the per-row delete with a required reason
  Given the grid shows a row for Email="naval.ops@simf.test"
  When the administrator clicks the "Delete" row action
  Then the bulk-delete modal opens titled "Delete users"
  And the body reads "This will disable 1 account(s). Sessions are revoked and the users are notified by email."
  And a "Reason" textarea is shown with helper "10–500 characters, audited."
  And the "Delete" submit button is disabled
  When they type Reason="Operator left the programme office." (>= 10 chars)
  Then the "Delete" button enables
  When they click "Delete"
  Then the BFF posts /account/api/admin/admins/bulk-delete with { Ids:[id], Reason }
  And the API returns HTTP 200 (gated by Admins.Delete)
  And the modal closes
  And a green toast reads "1 deleted, 0 skipped."
  And the grid reloads
```

**Evidence captured:**
- Screenshot of the reason modal: `docs/screenshots/cp-admin-admins-delete-reason-modal.png`
- Network: `/account/api/admin/admins/bulk-delete` = 200
- Audit row: an `OperationLog` / `RowAudit` row with `Event = 'Admin.UserDeleted'` and the actor's id, one per deleted subject.

### E2E-USR-007 — Bulk Delete

```gherkin
Scenario: Select three rows and bulk-delete with one reason
  Given the grid shows at least three deletable (non-self) admin rows
  When the administrator ticks three row checkboxes
  And clicks the toolbar "Delete" button
  Then the bulk-delete modal opens with body "This will disable 3 account(s). ..."
  When they type Reason="Quarterly access review — accounts decommissioned."
  And click "Delete"
  Then POST /account/api/admin/admins/bulk-delete fires with three Ids
  And the API returns HTTP 200
  And a green toast reads "3 deleted, 0 skipped."
  And the grid reloads with three fewer rows
```

### E2E-USR-008 — Self-delete guard

```gherkin
Scenario: The actor's own row is silently skipped
  Given the signed-in administrator's own row (superadmin@zagali-ict.com) is in the grid
  When they tick only their own row
  And click the toolbar "Delete" button, type a valid reason, and submit
  Then the API returns HTTP 200 with ApiResult.Data { Deleted:0, Skipped:1 }
  And a green toast reads "0 deleted, 1 skipped."
  And the actor's own row remains in the grid (self-delete protection, server-side guard)
```

### E2E-USR-009 — Bulk Delete with no selection

```gherkin
Scenario: Clicking the toolbar Delete with nothing selected shows a guard toast
  Given no rows are ticked
  When the administrator clicks the toolbar "Delete" button
  Then no modal opens
  And a red toast reads "Select at least one row first."
  And no /account/api/admin/admins/bulk-delete request fires
```

### E2E-USR-010 — Duplicate

```gherkin
Scenario: Clone an existing admin under a new email
  Given the grid shows a row for Email="naval.ops@simf.test"
  When the administrator clicks the "Duplicate" row action
  Then the Duplicate modal opens titled "Duplicate user"
  And the body reads "Create a copy of naval.ops@simf.test under a new email address."
  And a "New email address" field is shown with helper "The new user will receive an invitation here."
  When they fill New email address="naval.ops.deputy@simf.test"
  And click "Duplicate"
  Then the BFF posts /account/api/admin/admins/duplicate with { SourceId, NewEmail }
  And the API returns HTTP 200 (gated by Admins.Create)
  And the modal closes
  And a green toast reads "Created naval.ops.deputy@simf.test."
  And the grid reloads with the new row carrying the same role as the source
```

**Evidence captured:**
- Screenshot of the duplicate modal: `docs/screenshots/cp-admin-admins-duplicate-modal.png`
- Network: `/account/api/admin/admins/duplicate` = 200

### E2E-USR-011 — Duplicate validation

```gherkin
Scenario: The Duplicate submit button stays disabled until the email looks valid
  Given the Duplicate modal is open for source Email="naval.ops@simf.test"
  When the New email address field is empty
  Then the "Duplicate" button is disabled
  When they type "not-an-email" (no '@')
  Then the "Duplicate" button is still disabled (IsLikelyEmail guard: must contain '@', <= 256 chars)
  When they type "naval.ops.deputy@simf.test"
  Then the "Duplicate" button enables
```

### E2E-USR-012 — Export

```gherkin
Scenario: Export the admins grid to an XLSX
  Given the grid shows {N} rows and the administrator holds Admins.Export
  When they click "Select all"
  And click the toolbar "Export to Excel" button
  Then the browser issues POST /account/api/admin/admins/export with { Ids:[…] }
  And the API returns HTTP 200 with an XLSX body (workbook bytes, not a JSON envelope)
  And the browser saves the .xlsx file
  When nothing is selected and Export is clicked instead
  Then the request carries { Ids:[], Query:_query } and exports the full current query
```

**Evidence captured:**
- Screenshot before download: `docs/screenshots/cp-admin-admins-export.png`
- Network: `/account/api/admin/admins/export` = 200, `Content-Type` is the XLSX MIME type

### E2E-USR-013 — Import

```gherkin
Scenario: Import admins from an XLSX and read the result modal
  Given the administrator holds Admins.Import
  When they click the toolbar "Import from Excel" button
  Then the hidden file input (#users-import-input, accept=".xlsx") opens the OS file picker
  When they choose a valid 5-row .xlsx (<= 5 MB, valid ZIP magic)
  Then the BFF posts the multipart upload to /account/api/admin/admins/import
  And the API returns HTTP 200 with { Created, Skipped, Errors[] }
  And the Import result modal opens titled "Import result"
  And the body reads "5 created, 0 skipped." (or the real counts)
  And any bad rows are listed as "Row {n} ({email}): {reason}"
  And the grid reloads
  When they click "Close"
  Then the result modal closes
```

**Evidence captured:**
- Screenshot of the result modal: `docs/screenshots/cp-admin-admins-import-result-modal.png`
- Network: `/account/api/admin/admins/import` = 200

### E2E-USR-014 — Copy / Copy selected (stub)

```gherkin
Scenario: Copy actions show an info toast (clipboard stub)
  Given the grid shows a row for Email="naval.ops@simf.test"
  When the administrator clicks the "Copy" row action
  Then an info toast reads "Copied naval.ops@simf.test to the clipboard."
  And no /account/api/admin call fires
  When they tick three rows and click the toolbar "Copy" (copy selected)
  Then an info toast reads "Copied 3 rows to the clipboard."
```

### E2E-USR-015 — Paste (stub)

```gherkin
Scenario: Paste handles empty and non-empty clipboards as a stub
  Given the administrator clicks the toolbar "Paste" button with an empty clipboard
  Then a red toast reads "The clipboard is empty."
  When they paste non-empty clipboard text
  Then an info toast reads "Paste-to-add will land with the User Management module."
  And no /account/api/admin call fires
```

### E2E-USR-016 — Reset 2FA row-action link

```gherkin
Scenario: The Reset 2FA link only renders for a non-admin row with 2FA on
  Given the grid contains a row whose Role pill is "User" and 2FA pill is "On"
  Then that row shows a "Reset 2FA" link (RowActions, only when !IsAdministrator && TwoFactorEnabled)
  When the administrator clicks "Reset 2FA"
  Then the browser navigates to /admin/reset-2fa?email={url-encoded email}
  And rows that are Administrators OR have 2FA off show no Reset 2FA link
```

### E2E-USR-017 — Filter + sort + pager round-trip

```gherkin
Scenario: Grid query controls round-trip to the list endpoint
  Given the grid shows more than one page of rows
  When the administrator types "naval" into the Email column filter
  Then POST /account/api/admin/admins/list fires with Filters { email:"naval" }
  And only matching rows render
  When they click the "Email" column header to sort
  Then the list request carries Sort="email" and rows are ordered ascending
  When they click "Next" / a numbered page / "Last page"
  Then the list request carries the new Skip and the pager summary reads "{from}-{to} of {total}"
  When they change the page-size ("Show")
  Then Top changes and the multiselect selection clears (D-045 H1 hardening)
```

### E2E-USR-018 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no admin rows visible to the query
  When the administrator opens /admin/admins
  Then the grid body renders the SimfEmptyState component
  And it shows the bilingual copy "No accounts yet." / "لا توجد حسابات بعد."
  And the toolbar still shows the "Add" button
  And no error toast appears
```

### E2E-USR-019 — Auth gate

```gherkin
Scenario: A signed-in admin lacking Admins.View is denied
  Given a signed-in Control Panel user whose role does NOT grant the Admins.View permission
  When they navigate to /admin/admins
  Then the [RequirePermission(PermissionCatalog.Admins.View)] attribute blocks the page
  And they land on /not-permitted with HTTP 200
  And no /account/api/admin/admins/list request fires
  And the "Admins" nav item is hidden for them (CpNavigation RequiredPermission = Admins.View)
```

### E2E-USR-020 — Server 500 on list

```gherkin
Scenario: API 500 on /list degrades to an empty grid without crashing
  Given the API is configured to return 500 on /admin/admins/list (e.g. DB down)
  When the administrator opens /admin/admins
  Then the grid shows the loading indicator, then settles
  And the grid renders an empty page (GridPage.Of(empty) fallback when the envelope is not Success)
  And the page does not throw an unhandled exception
  And no rows render
```

### E2E-USR-021 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/admins in English
  When they switch the UI language to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "المسؤولون"
  And the nav rail and toolbar mirror (Arabic labels, reversed order)
  And the pager arrows reverse
  When they click "إضافة" (Add)
  Then the Add modal opens in RTL with Arabic field labels
  And the form actions appear in reverse order
  When the empty state shows, it reads "لا توجد حسابات بعد."
```

### E2E-USR-022 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/admins with the default "dialog" presentation
  And the grid toolbar (CustomToolbar) shows the CrudPresentationToggle "Open as full page" control (maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.admins" holds {"v":1,"presentation":"page"}
    (PageKey = "admins"; persisted by CpPreferences)
  When they reload /admin/admins
  Then OnInitializedAsync rehydrates the choice via Prefs.GetPresentationAsync("admins")
  And the toggle still reads "Open as dialog"
  And opening the "Add" toolbar action now renders the full-page CrudShell frame (not a popup)
```

**Evidence captured:**
- Screenshot of the toolbar toggle in both states: `docs/screenshots/cp-admin-admins-toggle-{dialog,page}.png`
- Application tab: localStorage `simf.cp.prefs.admins` = `{"v":1,"presentation":"page"}`
- Console errors: 0 expected

### E2E-USR-023 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add / Details take over the content area; Save / Close returns to the grid
  Given the presentation is set to "full page" (GridHidden = FormOpen && page mode)
  When the administrator clicks the "Add" toolbar button
  Then the grid + SimfBanner are replaced by the CrudShell frame (title "Add user" + close header + the hosted UsersAddEdit form)
  And there is no modal backdrop
  When they fill Email address="naval.ops@simf.test" + Display name="Naval Operations Lead", tick "Administrator", and click "Create user"
  Then the CrudShell frame closes (CloseForm)
  And the grid re-appears with the new row and the green success toast "Account created for naval.ops@simf.test…"
  When they click the "Details" row action on a row
  Then the full-page frame opens titled "User details — {email}" hosting the read-only UsersViewDelete form (IsDelete=false)
  When they click the frame's close (X) / "Close" button
  Then the form closes and the grid re-appears unchanged (no /account/api/admin call fired for Details)
```

### E2E-USR-024 — Excel import rejection (D-356)

```gherkin
Scenario: A bad upload is rejected without creating anything
  Given the administrator is on /admin/admins and holds Admins.Import
  When they click the toolbar "Import" action
  Then the hidden file input #users-import-input (accept=".xlsx") opens the OS file picker
  When they choose a file that is not a valid .xlsx (fails the ZIP-magic check)
  Then simfAccount.uploadFile posts the multipart upload to /account/api/admin/admins/import
  And the API returns HTTP 400 (ZIP-magic + 5 MB gate)
  And the page shows a red bilingual error toast (envelope Error.MessageForCurrentCulture(), falling back to "Admin.Users.Import.Fallback")
  And the import-result modal does NOT open and no admin account is created
  When they instead upload a file larger than 5 MB
  Then the request is rejected with HTTP 400 and the same bilingual error toast, nothing created
  When they upload a workbook whose worksheet is not the expected admins sheet
  Then the request returns 400 with the bilingual "worksheet" message and nothing is created
```

**Evidence captured:**
- Screenshot of the error toast: `docs/screenshots/cp-admin-admins-import-rejected.png`
- Network: `/account/api/admin/admins/import` returns 400; no follow-up `/list` create occurs
- Console errors: 0 expected (the rejection is handled, not thrown)

### E2E-USR-025 — Admins-list profile-photo thumbnail (D-357)

```gherkin
Scenario: the name column renders a photo thumbnail when the admin has an avatar
  Given the administrator is on /admin/admins
  And admin "A" has a profile photo (avatar) set and admin "B" has none
  When the admins grid loads
  Then admin A's name cell shows a photo thumbnail beside the display name
       (img src "/account/api/admin/admins/{A.id}/avatar", the CP proxy to the
        Admins.View-gated GET /api/v1/admin/admins/{id}/avatar)
  And admin B's name cell shows a tinted initials tile (never a broken image)
  And no avatar request is issued for admin B (the URL is only built when HasAvatar)

Scenario: a missing StoredFile falls back to the placeholder, not a broken glyph
  Given admin "A" has HasAvatar true but the avatar bytes are missing from the store
  When the admins grid loads and the avatar request for A returns 404
  Then A's cell shows the placeholder icon (SimfImageThumb onerror adds
       .simf-img-thumb--broken), not the browser's broken-image glyph
```

**Evidence captured:**
- Screenshot of the grid with a thumbnail + an initials tile: `docs/screenshots/cp-admin-admins-thumbnails.png`
- Network: one `/account/api/admin/admins/{A}/avatar` (200) for A; none for B
- Backend: `tests/SIMF.Api.Tests/AdminCreateUserTests.cs` →
  `Admins_list_row_reports_HasAvatar_once_a_photo_is_set` asserts the list row's
  `HasAvatar` flips with the `AvatarRelativePath` sentinel (the same projection
  backs the visitors/others lists)

---

## Implementation notes

- **Manual smoke is the canonical run today.** Until Playwright is adopted, the
  canonical "run" of these scenarios is a Chrome DevTools MCP session driven by
  the [SIMF smoke template](../../dev/SIMF_TABLE_PATTERN.md) — sign in via the
  Background steps, walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-admins-*.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be
  created) plus a step-definition class. The Gherkin shape is already
  runner-agnostic.
- **API integration tests cover the same surface at a lower layer (no browser):**
  - `tests/SIMF.Api.Tests/AdminCreateUserTests.cs` — create (200), duplicate
    email (409 `ADMIN_EMAIL_ALREADY_REGISTERED`), non-admin forbidden (403),
    list pagination/sort/filter, invite-code flow, PendingApproval landing
    state. Covers E2E-USR-001/-003/-017/-019 at the API layer.
  - `tests/SIMF.Api.Tests/AdminGridV2Tests.cs` — bulk-delete endpoint behaviour
    (self / Administrator targets silently skipped). Covers E2E-USR-006/-007/-008.
  - `tests/SIMF.Api.Tests/AdminUserRolesTests.cs` — read + replace an admin's
    RBAC roles (gated by `Admins.AssignRoles`). Covers E2E-USR-004.
  - `tests/SIMF.Api.Tests/AdminBulkAdminTests.cs` — admin-queue bulk
    approve/reject (sibling endpoints, not the delete path).
  - The bulk-delete reason gate (10–500 chars) is enforced both client-side
    (the modal disables Submit) and server-side
    (`AdminBulkDeleteRequestValidator`) — E2E-USR-006 exercises the client gate.
- **Permission coverage guards** — `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` fail the build if the
  page or an endpoint is not gated; the page gate is `Admins.View` and the
  per-action gates are listed in the matrix preamble. E2E-USR-019 is the
  in-browser proof of the page gate.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle).
