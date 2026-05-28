# E2E test catalogue — Roles (`/admin/roles`)

| | |
|--|--|
| **Page** | [`cp/admin-roles.md`](../../pages/cp/admin-roles.md) |
| **Surface** | Control Panel |
| **Authored** | D-134 Sprint A (2026-05-29) |
| **Last reviewed** | 2026-05-29 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-ROL-001 | Create a custom role (golden) | P0 |
| E2E-ROL-002 | Duplicate name → 409 RoleNameDuplicate | P0 |
| E2E-ROL-003 | Rename a custom role | P1 |
| E2E-ROL-004 | Edit baseline role → notice (rename blocked) | P0 |
| E2E-ROL-005 | Delete a custom unused role | P1 |
| E2E-ROL-006 | Delete baseline → 409 RoleIsBaseline | P0 |
| E2E-ROL-007 | Delete in-use role → 409 RoleInUse (count interpolated) | P0 |
| E2E-ROL-008 | Auth: non-admin → /not-permitted | P0 |
| E2E-ROL-009 | RTL render | P1 |

## Scenarios

### E2E-ROL-001 — Create custom role

```gherkin
Scenario: Administrator creates a custom role
  Given an Administrator is signed in on /admin/roles
  When they click "Add role"
  And the Add modal opens with one Role-name field
  And they fill Name="Scientific" and click "Create role"
  Then the modal closes
  And a green toast reads "Role \"Scientific\" was created."
  And the grid shows a new row Name="Scientific", Type="Custom",
      Users=0, Permissions=0
  And the audit log records Role.Created with the actor's id
```

### E2E-ROL-002 — Duplicate name

```gherkin
Scenario: Creating a role with a duplicate name returns 409
  Given a role "Administrator" already exists (baseline)
  When the admin opens the Add modal
  And fills Name="Administrator" and clicks "Create role"
  Then the API returns HTTP 409 with ApiResult.Error.Code="ROLE_NAME_DUPLICATE"
  And the bilingual server message surfaces in the modal SimfAlert
  And the modal stays open
```

### E2E-ROL-003 — Rename custom role

```gherkin
Scenario: Administrator renames a custom role
  Given a custom role "Scientific" exists with 0 users
  When the admin clicks the Edit icon on that row
  And the Edit modal opens prefilled with Name="Scientific"
  And they change Name="Scientific Committee"
  And click "Save changes"
  Then the modal closes
  And a green toast reads "Role \"Scientific Committee\" was updated."
  And the grid reflects the new name
  And the audit log records Role.Updated
```

### E2E-ROL-004 — Baseline rename blocked

```gherkin
Scenario: Edit modal on a baseline role shows a read-only notice
  Given the baseline role "Administrator" exists (IsBaseline=true)
  When the admin clicks the Edit icon on that row
  Then the Edit modal opens
  And shows a SimfAlert "This is a built-in role and cannot be renamed."
  And the only action is a Close button
  And no PUT /admin/roles/{id} is fired even if a request is manually crafted
      (server returns 409 ROLE_IS_BASELINE)
```

### E2E-ROL-005 — Delete custom unused role

```gherkin
Scenario: Delete a custom role that no user holds
  Given a custom role "Scientific" exists with UserCount=0
  When the admin clicks the Delete (trash) icon
  Then DELETE /admin/roles/{id} returns 200
  And the row vanishes
  And a green toast reads "Role \"Scientific\" was deleted."
  And the audit log records Role.Deleted
```

### E2E-ROL-006 — Delete baseline blocked

```gherkin
Scenario: Delete on a baseline role returns 409
  Given the baseline role "Administrator" exists
  When the admin clicks the Delete icon
  Then the API returns HTTP 409 with ApiResult.Error.Code="ROLE_IS_BASELINE"
  And the bilingual message reads "Baseline roles cannot be deleted." / "لا يمكن حذف الأدوار الأساسية."
  And the row stays visible
```

### E2E-ROL-007 — Delete in-use role blocked

```gherkin
Scenario: Delete on a role any user holds returns 409 RoleInUse
  Given a custom role "Scientific" exists
  And 3 users hold this role
  When the admin clicks the Delete icon
  Then the API returns HTTP 409 with ApiResult.Error.Code="ROLE_IN_USE"
  And the bilingual message includes the count "3" verbatim
  And the row stays visible
```

### E2E-ROL-008 — Auth gate

```gherkin
Scenario: Non-administrator user is denied
  Given a signed-in Visitor account
  When they navigate to /admin/roles
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/roles/list request fires
```

### E2E-ROL-009 — RTL

```gherkin
Scenario: Arabic toggle mirrors page + modal
  Given the admin is on /admin/roles
  When they click "العربية"
  Then <html dir="rtl" lang="ar"> is set
  And the SimfBanner reads "الأدوار والصلاحيات"
  And column headers + toolbar flip
  When they open the Add modal
  Then the modal renders RTL with Arabic labels + helper text
```

---

_Last reviewed:_ 2026-05-29 by Claude (D-134 Sprint A).
