# E2E test catalogue — Role permissions editor (`/admin/roles/{id}/permissions`)

| | |
|--|--|
| **Page** | [`cp/admin-roles.md`](../../pages/cp/admin-roles.md) (parent Roles module — the per-role grant editor ships in the Issue-1 follow-up; no dedicated page doc yet) |
| **Route** | `/admin/roles/{RoleId:guid}/permissions` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page permission:** `[RequirePermission(PermissionCatalog.Roles.AssignPermissions)]`
> (`"Roles.AssignPermissions"`). A signed-in admin lacking that permission
> lands on `/not-permitted`.
>
> **Backing API:** `GET /account/api/admin/roles/{id}/permissions` (BFF) →
> `GET /api/v1/admin/roles/{id}/permissions` (gated `Roles.View`) and
> `PUT /account/api/admin/roles/{id}/permissions` (BFF) →
> `PUT /api/v1/admin/roles/{id}/permissions` (gated `Roles.AssignPermissions`
> + `RequireApprovedAccount` + rate-limit `"auth"`). The PUT body is
> `AdminSetRolePermissionsRequest { Codes: string[] }` and the server
> **replaces** the role's whole grant set with exactly the codes sent
> (diff-apply: it removes what's missing and adds what's new — it does not
> append). Response is `ApiResult<bool>`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-RPM-001 | Golden round-trip — load custom role → tick/untick codes → Save → reload reflects exactly the saved set | happy | P0 | _to author_ |
| E2E-RPM-002 | Load custom role with no grants — every checkbox unticked, no error toast | happy | P1 | _to author_ |
| E2E-RPM-003 | Toggle a single checkbox on then off then Save — set is the difference (no duplicate insert error) | happy | P1 | _to author_ |
| E2E-RPM-004 | "Save permissions" persists the selected set (green "Permissions saved." toast) | happy | P0 | _to author_ |
| E2E-RPM-005 | "Back to roles" button returns to `/admin/roles` without saving | happy | P2 | _to author_ |
| E2E-RPM-006 | Baseline role (Administrator) — info notice, all checkboxes disabled, no Save button | guard | P0 | _to author_ |
| E2E-RPM-007 | PUT replaces (does not append) — clearing all then Save leaves the role with zero grants | happy | P1 | _to author_ |
| E2E-RPM-008 | Auth gate — signed-in admin without `Roles.AssignPermissions` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-RPM-009 | Role not found — unknown/garbage `{id}` → load-failed error toast | error | P1 | _to author_ |
| E2E-RPM-010 | Baseline edit refused at the API — hand-crafted PUT on a baseline role → 409 `RoleIsBaseline` | error | P1 | _to author_ |
| E2E-RPM-011 | Unknown permission code — hand-crafted PUT with a junk code → 400 `ValidationFailed` | error | P1 | _to author_ |
| E2E-RPM-012 | Server 500 on save → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-RPM-013 | RTL / Arabic render — page mirrors, headings + checkboxes + buttons in Arabic | i18n | P1 | _to author_ |

## Scenarios

### E2E-RPM-001 — Golden round-trip

```gherkin
Feature: Role permissions editor round-trip
  As an Administrator with Roles.AssignPermissions
  I want to grant and revoke per-page/per-action permissions on a custom role
  So that holders of that role get exactly the access I intend

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (Get-Totp helper)
  And a custom (non-baseline) role named "Programme Editor" exists
  And the administrator has opened /admin/roles/{ProgrammeEditorId}/permissions

Scenario: Grant a set of codes, save, and confirm it round-trips
  Then the page title reads "Permissions — Programme Editor"
  And the intro text reads "Select the pages and actions this role can use. Changes apply the next time a holder of the role signs in."
  And the catalogue renders as cards grouped by page (e.g. "Roles", "Interests", "Sessions", "Themes", "Halls")
  And every checkbox reflects the role's current grants (all unticked for a brand-new role)

  When the administrator ticks "View sessions" (Sessions.View)
  And ticks "Edit themes" (Themes.Edit)
  And ticks "View halls" (Halls.View)
  And clicks "Save permissions"
  Then a PUT /account/api/admin/roles/{ProgrammeEditorId}/permissions fires with Codes = ["Sessions.View","Themes.Edit","Halls.View"]
  And the API returns HTTP 200 with ApiResult.Success = true
  And a green toast reads "Permissions saved." / "تم حفظ الصلاحيات."

  When the administrator reloads /admin/roles/{ProgrammeEditorId}/permissions
  Then exactly "View sessions", "Edit themes" and "View halls" are ticked
  And every other checkbox is unticked
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-roles-permissions-golden-before.png` (all unticked)
- Screenshot after: `docs/screenshots/cp-admin-roles-permissions-golden-after.png` (3 ticked + green toast)
- Console errors: 0 expected
- Network: the `GET` + `PUT` `/account/api/admin/roles/{id}/permissions` calls return 200
- Audit row: `OperationLog` / audit row with `Event = 'Role.PermissionsUpdated'`, the actor's id, and `Detail = "id={ProgrammeEditorId}; granted=3"`

### E2E-RPM-002 — No grants (empty selection state)

```gherkin
Scenario: A custom role with no grants renders all checkboxes unticked
  Given a custom role "Empty Role" exists with zero RolePermission rows
  When the administrator opens /admin/roles/{EmptyRoleId}/permissions
  Then the full catalogue still renders (one card per page group)
  And every checkbox is unticked
  And no error toast appears
  And the "Save permissions" and "Back to roles" buttons are both enabled
```

### E2E-RPM-003 — Toggle on then off in one session

```gherkin
Scenario: Ticking then unticking a code before save yields the difference
  Given the administrator is on /admin/roles/{ProgrammeEditorId}/permissions
  And "View sessions" (Sessions.View) is currently ticked (already granted)
  When they untick "View sessions"
  And tick "Create sessions" (Sessions.Create)
  And untick "Create sessions" again
  And click "Save permissions"
  Then the PUT body Codes contains neither Sessions.View nor Sessions.Create
  And the API returns HTTP 200 (the diff-apply removes Sessions.View, adds nothing — no duplicate-insert error on the composite key)
  And a green toast reads "Permissions saved."
```

### E2E-RPM-004 — Save persists the selected set

```gherkin
Scenario: Save permissions button writes the set and shows success
  Given the administrator is on /admin/roles/{ProgrammeEditorId}/permissions
  When they tick "View roles" (Roles.View)
  And click "Save permissions"
  Then the button shows the "Saving…" loading label while in flight
  And on success a green toast reads "Permissions saved." / "تم حفظ الصلاحيات."
  And the button returns to its idle "Save permissions" label
```

### E2E-RPM-005 — Back to roles (no save)

```gherkin
Scenario: Back button navigates away without persisting
  Given the administrator is on /admin/roles/{ProgrammeEditorId}/permissions
  And they have ticked "View halls" (Halls.View) but NOT clicked Save
  When they click "Back to roles"
  Then the browser navigates to /admin/roles
  And no PUT /account/api/admin/roles/{id}/permissions request fires
  And the unsaved "View halls" tick is discarded (re-opening the editor shows it unticked)
```

### E2E-RPM-006 — Baseline role is read-only

```gherkin
Scenario: A built-in (baseline) role cannot be edited from the UI
  Given the seeded baseline role "Administrator" exists (IsBaseline = true, wildcard "*")
  When the administrator opens /admin/roles/{AdministratorRoleId}/permissions
  Then an info SimfAlert reads "This is a built-in role; its permissions are managed by the system and cannot be edited here." / "هذا دور أساسي؛ تُدار صلاحياته بواسطة النظام ولا يمكن تعديلها هنا."
  And the catalogue still renders but every checkbox is disabled
  And NO "Save permissions" button is shown (only "Back to roles")
```

### E2E-RPM-007 — PUT replaces (clear-all leaves zero grants)

```gherkin
Scenario: Saving an empty selection clears all grants
  Given a custom role "Programme Editor" currently grants Sessions.View + Themes.Edit + Halls.View
  When the administrator opens /admin/roles/{ProgrammeEditorId}/permissions
  And unticks all three
  And clicks "Save permissions"
  Then the PUT body Codes is the empty array []
  And the API returns HTTP 200
  And a green toast reads "Permissions saved."
  When they reload the page
  Then every checkbox is unticked
  And the parent /admin/roles grid shows Permissions = 0 for "Programme Editor"
```

### E2E-RPM-008 — Auth gate

```gherkin
Scenario: A signed-in admin lacking Roles.AssignPermissions is denied
  Given a signed-in CP user whose roles do NOT include the "Roles.AssignPermissions" permission
  And whose JWT permission claims therefore omit "Roles.AssignPermissions" (and is not the Administrator wildcard "*")
  When they navigate to /admin/roles/{anyRoleId}/permissions
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/roles/{id}/permissions request fires
```

### E2E-RPM-009 — Role not found

```gherkin
Scenario: Loading an unknown role id surfaces the load-failed toast
  Given the administrator navigates to /admin/roles/00000000-0000-0000-0000-000000000000/permissions
  When the page calls GET /account/api/admin/roles/{id}/permissions
  Then the API returns HTTP 404 with ApiResult.Error.Code = "RoleNotFound"
  And a red toast surfaces the bilingual message "The role was not found." / "لم يتم العثور على الدور."
  And no catalogue cards render (the EditForm is not shown)
```

### E2E-RPM-010 — Baseline edit refused at the API

```gherkin
Scenario: A hand-crafted PUT on a baseline role is refused
  Given a baseline role "Administrator" (IsBaseline = true)
  When a PUT /account/api/admin/roles/{AdministratorRoleId}/permissions is sent with Codes = ["Roles.View"]
  Then the API returns HTTP 409 with ApiResult.Error.Code = "RoleIsBaseline"
  And the bilingual message reads "Baseline roles' permissions cannot be edited." / "لا يمكن تعديل صلاحيات الأدوار الأساسية."
  And the role's grants are unchanged
```

### E2E-RPM-011 — Unknown permission code

```gherkin
Scenario: A PUT containing a code that is not in the catalogue is rejected
  Given a custom role "Programme Editor"
  When a PUT /account/api/admin/roles/{ProgrammeEditorId}/permissions is sent with Codes = ["Sessions.View","Bogus.Code"]
  Then the API returns HTTP 400 with ApiResult.Error.Code = "ValidationFailed"
  And the message reads "Unknown permission code(s): Bogus.Code." / "رمز صلاحية واحد أو أكثر غير معروف."
  And NO grant is persisted (the whole request is rejected, valid codes included)
```

### E2E-RPM-012 — Server 500 on save

```gherkin
Scenario: API 500 on the PUT shows the fallback bilingual toast
  Given the administrator has a selection ready on /admin/roles/{ProgrammeEditorId}/permissions
  And the API is configured to return 500 on PUT /admin/roles/{id}/permissions (e.g. DB down)
  When they click "Save permissions"
  Then a red toast appears reading "The permissions could not be saved. Please try again." / "تعذّر حفظ الصلاحيات. حاول مرة أخرى."
  And the page stays on the editor with the selection intact
  And the "Save permissions" button returns to its idle state (not stuck on "Saving…")
```

### E2E-RPM-013 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the editor
  Given the administrator is on /admin/roles/{ProgrammeEditorId}/permissions in English
  When they switch the language to "العربية"
  Then the page reloads with <html dir="rtl" lang="ar">
  And the page title reads "الصلاحيات — Programme Editor"
  And the intro text reads "حدّد الصفحات والإجراءات التي يمكن لهذا الدور استخدامها. تُطبَّق التغييرات عند تسجيل دخول حامل الدور في المرة التالية."
  And the per-page card headings + checkbox labels appear in Arabic where localised
  And the action buttons read "حفظ الصلاحيات" and "العودة إلى الأدوار" in reverse (RTL) order
```

---

## Implementation notes

- **Manual smoke is canonical today.** Until Playwright is adopted, the
  canonical "run" of these scenarios is a Chrome DevTools MCP session: sign
  in per the Auth setup, create/seed a custom role via `/admin/roles`, then
  walk each scenario above and drop screenshots under
  `docs/screenshots/cp-admin-roles-permissions-*.png`.
- **API integration tests cover the same surface at a lower layer (no
  browser):** [`tests/SIMF.Api.Tests/RolePermissionsEndpointsTests.cs`](../../../tests/SIMF.Api.Tests/RolePermissionsEndpointsTests.cs)
  — `Put_then_get_round_trips_and_replaces_the_grant_set` (E2E-RPM-001 / -007),
  `Put_on_a_baseline_role_is_refused` (E2E-RPM-010), plus the unknown-code
  rejection (E2E-RPM-011). The browser scenarios add the CP rendering,
  baseline disable/no-Save behaviour (E2E-RPM-006), the auth gate
  (E2E-RPM-008), toast text, and RTL.
- **Permission gate.** The page is `[RequirePermission(PermissionCatalog.Roles.AssignPermissions)]`
  and `CpNavigationPermissionTests` / `PermissionEnforcementTests` fail the
  build if the gate is missing. The API GET is gated `Roles.View`; the PUT is
  gated `Roles.AssignPermissions`.
- **Diff-apply, not replace-by-delete.** `AdminRoleService.SetPermissionsAsync`
  computes `toRemove` / `toAdd` against the existing `RolePermission` rows so an
  already-granted code is not deleted-and-reinserted in one unit of work (which
  would trip the EF change tracker on the composite key). E2E-RPM-003 exercises
  this path.
- **Convert to Playwright** later: copy each Gherkin scenario into a `.feature`
  file under `tests/SIMF.E2E.Tests/` (project to be created) + step
  definitions. The Gherkin shape is already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
