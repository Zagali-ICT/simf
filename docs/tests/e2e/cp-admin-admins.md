# E2E test catalogue — Admins CRUD (`/admin/admins`)

| | |
|--|--|
| **Page** | [`cp/admin-admins.md`](../../pages/cp/admin-admins.md) |
| **Surface** | Control Panel |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-USR-001 | Golden: invite admin → row appears Approved | P0 |
| E2E-USR-002 | Bulk-delete with required reason → toast + reload | P0 |
| E2E-USR-003 | Self-delete in batch → silently skipped (audited) | P0 |
| E2E-USR-004 | Duplicate admin to new email → new Approved row | P1 |
| E2E-USR-005 | Import 50-row XLSX → 50 created, 0 errors | P1 |
| E2E-USR-006 | Export selected → XLSX downloads | P1 |
| E2E-USR-007 | Auth: non-admin → /not-permitted | P0 |
| E2E-USR-008 | RTL: Arabic toggle mirrors page + toolbar + pager | P1 |

## Scenarios

### E2E-USR-001 — Invite admin

```gherkin
Scenario: Invite a new administrator
  Given an Administrator is signed in on /admin/admins
  When they click "+ Add"
  And the Add modal opens with CreateAdminForm
  And they fill Email="newadmin@example.com"
  And they fill Display name="New Admin"
  And they fill Password="Aa@123456789012" (meets complexity)
  And the TOTP-on-first-login flag is on
  And they click "Create administrator"
  Then the modal closes
  And a green toast reads "Administrator \"newadmin@example.com\" was created."
  And the grid contains a new row with state "Approved" and role pill "Admin"
  And the audit log shows Admin.UserCreated with the inviter's id
```

### E2E-USR-002 — Bulk-delete

```gherkin
Scenario: Bulk-delete with required reason
  Given the grid has 5 administrator rows besides the current admin
  When the current admin ticks 3 rows
  And clicks toolbar "Delete"
  Then the bulk-delete modal opens
  And the Delete button is disabled until reason length ∈ [10..500]
  When they type "Quarterly access review removed access for these accounts."
  And click "Delete"
  Then the modal closes
  And a toast reads "Deleted 3, skipped 0."
  And the grid reloads without those 3 rows
  And the audit log has 3 Admin.UserDeleted rows with the reason verbatim
```

### E2E-USR-003 — Self-delete silently skipped

```gherkin
Scenario: Self id in batch is silently skipped
  Given the current admin selects their own row + 2 other rows
  When they bulk-delete with a valid reason
  Then the toast reads "Deleted 2, skipped 1."
  And their own row is still present
  And the audit log records Admin.UserSelfDeleteSkipped for the actor
```

### E2E-USR-004 — Duplicate

```gherkin
Scenario: Duplicate an admin to a new email
  Given an admin row "alice@example.com" exists
  When the current admin clicks the Duplicate icon on that row
  And types "alice2@example.com" in the new-email modal
  And clicks "Duplicate"
  Then a new Approved row appears with that email + same Administrator role
  And a fresh QR badge is minted (visible later in Details)
```

### E2E-USR-005 — Import XLSX

```gherkin
Scenario: Import 50-row XLSX → 50 created, 0 errors
  Given a valid XLSX with 50 admin rows
  When the admin clicks toolbar "Import" → picks the file
  Then the import-result modal opens
  And reads "Created: 50, Skipped: 0, Errors: 0"
  And the grid reloads to show 50 new rows
```

### E2E-USR-006 — Export

```gherkin
Scenario: Export selected rows
  Given 3 rows are selected
  When the admin clicks toolbar "Export"
  Then an XLSX file downloads named admins-{yyyy-MM-dd}.xlsx
  And the file contains exactly those 3 rows + header
```

### E2E-USR-007 — Auth gate

```gherkin
Scenario: Non-admin user is denied
  Given a Visitor account is signed in
  When they navigate to /admin/admins
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/admins/list request fires
```

### E2E-USR-008 — RTL

```gherkin
Scenario: Arabic toggle mirrors everything
  Given the admin is on /admin/admins
  When they click "العربية"
  Then <html dir="rtl" lang="ar"> is set
  And the SimfBanner reads "المسؤولون"
  And the toolbar buttons render in reverse order
  And the column headers flip
  And the pager arrows reverse direction
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
