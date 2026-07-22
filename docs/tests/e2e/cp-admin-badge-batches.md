# E2E — CP Badge batches (`/admin/visitors/badge-batches`)

Persisted bulk-badge batches desk (D-758, #10 Phase 2). Namespace `BBT`.
Auth-setup uses the `Get-Totp` helper — never a literal secret.

## Coverage matrix

| Function | Scenario |
|---|---|
| List (golden) | E2E-BBT-001 |
| Empty state | E2E-BBT-002 |
| Re-email — success | E2E-BBT-003 |
| Re-email — invalid email | E2E-BBT-004 |
| Re-email — unknown batch | E2E-BBT-005 |
| Revoke — success (disables accounts) | E2E-BBT-006 |
| Revoke — status reflects revoked | E2E-BBT-007 |
| Auth gate | E2E-BBT-008 |
| RTL (Arabic) | E2E-BBT-009 |

## Scenarios

```gherkin
Background:
  Given an Administrator is signed in to the Control Panel
  And a bulk-badge batch of "3 VIP + 2 Normal" was generated from the Delegates desk

Scenario: E2E-BBT-001 The batches list shows the generated run
  When the admin opens "/admin/visitors/badge-batches"
  Then the grid shows a row with Contents "VIP × 3 + Normal × 2"
  And its Total is 5
  And its Status is "Active"

Scenario: E2E-BBT-002 Empty state before any batch exists
  Given no bulk-badge batch has been generated
  When the admin opens "/admin/visitors/badge-batches"
  Then an empty state "No badge batches yet" is shown

Scenario: E2E-BBT-003 Re-email the QR pack to an organiser
  Given the admin is on the batches list
  When the admin clicks "Re-email QR pack" on the batch row
  And enters "organiser@simf.test" and clicks Send
  Then a success toast "Emailed 5 badge(s) to organiser@simf.test." is shown
  And one email carrying a ZIP of 5 QR PNGs is queued to organiser@simf.test
  And the batch row's "Emailed to" now shows organiser@simf.test

Scenario: E2E-BBT-004 Re-email rejects an invalid organiser email
  Given the admin opened the Re-email dialog for the batch
  When the admin enters "not-an-email" and clicks Send
  Then a validation error is shown
  And no email is queued

Scenario: E2E-BBT-005 Re-email an unknown batch is not found
  When a re-email request is sent for a non-existent batch id
  Then the API responds 404 ADMIN_USER_NOT_FOUND

Scenario: E2E-BBT-006 Revoke disables every account in the batch
  Given the admin is on the batches list
  When the admin clicks "Revoke batch" and confirms
  Then a success toast "Revoked 5 account(s)." is shown
  And every one of the batch's 5 minted accounts is now Disabled
  And a scan of any of those badge QRs no longer resolves to an approved account

Scenario: E2E-BBT-007 A revoked batch shows Revoked and drops its actions
  Given the batch has been revoked
  When the admin reloads the batches list
  Then the batch row's Status is "Revoked"
  And the Re-email and Revoke actions are not shown on that row

Scenario: E2E-BBT-008 The page and API require the ViewBatches permission
  Given an Administrator-role account WITHOUT "Visitors.ViewBatches"
  When it opens "/admin/visitors/badge-batches"
  Then access is denied (the nav item is hidden and the API returns 403)

Scenario: E2E-BBT-009 The desk renders correctly in Arabic (RTL)
  Given the interface language is Arabic
  When the admin opens "/admin/visitors/badge-batches"
  Then the grid, status pills, and both dialogs render right-to-left
  And there is no horizontal overflow (scrollWidth == clientWidth)
```
