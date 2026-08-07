# E2E — CP Badge batches (`/admin/visitors/badge-batches`)

Persisted bulk-badge batches desk (D-758, #10 Phase 2). Namespace `BBT`.
Auth-setup uses the `Get-Totp` helper — never a literal secret.

## Coverage matrix

> Rewritten into the standard shape on 2026-07-28. This file used a bespoke
> `| Function | Scenario |` table with the id in the **second** column, so
> `tools/testbook/build_testbook.py` — which reads the id from the first cell —
> saw no scenarios here at all and the page contributed **nothing** to the tester
> workbook despite being fully authored. `E2E-BBT-010` was also written up in
> Scenarios below but missing from the matrix entirely.

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-BBT-001 | List (golden) — the batches grid shows the generated run | happy | P0 | _to author_ |
| E2E-BBT-002 | Empty state before any batch exists | happy | P1 | _to author_ |
| E2E-BBT-003 | Re-email the QR pack to an organiser — success | happy | P0 | _to author_ |
| E2E-BBT-004 | Re-email — invalid organiser email is rejected, nothing queued | error | P1 | _to author_ |
| E2E-BBT-005 | Re-email — unknown batch id → 404 `ADMIN_USER_NOT_FOUND` | error | P1 | _to author_ |
| E2E-BBT-006 | Revoke — disables every account in the batch | happy | P0 | _to author_ |
| E2E-BBT-007 | Revoke — a revoked batch reads "Revoked" and drops its actions | function | P1 | _to author_ |
| E2E-BBT-008 | Auth gate — `Visitors.ViewBatches` gates the page, nav item and list API | auth | P0 | _to author_ |
| E2E-BBT-009 | RTL (Arabic) — grid, pills and both dialogs mirror; no horizontal overflow | i18n | P1 | _to author_ |
| E2E-BBT-010 | View-only cannot re-email or revoke (`Visitors.ManageBatches` gate) | auth | P0 | _to author_ |
| E2E-BBT-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-BBT-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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

Scenario: E2E-BBT-008 The page and list API require the ViewBatches permission
  Given an Administrator-role account WITHOUT "Visitors.ViewBatches"
  When it opens "/admin/visitors/badge-batches"
  Then access is denied (the nav item is hidden and the list API returns 403)

Scenario: E2E-BBT-010 View-only cannot re-email or revoke (ManageBatches gate)
  Given an account WITH "Visitors.ViewBatches" but WITHOUT "Visitors.ManageBatches"
  When it opens "/admin/visitors/badge-batches"
  Then the batches list loads and each active row shows NO Re-email or Revoke action
  And calling the re-email API returns 403
  And calling the revoke API returns 403

Scenario: E2E-BBT-009 The desk renders correctly in Arabic (RTL)
  Given the interface language is Arabic
  When the admin opens "/admin/visitors/badge-batches"
  Then the grid, status pills, and both dialogs render right-to-left
  And there is no horizontal overflow (scrollWidth == clientWidth)
```
