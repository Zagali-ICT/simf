# E2E test catalogue — Interests CRUD (`/admin/interests`)

| | |
|--|--|
| **Page** | [`cp/admin-interests.md`](../../pages/cp/admin-interests.md) |
| **Route** | `/admin/interests` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` / `[REDACTED - supply via SIMF_SuperAdmin__TempPassword]` + TOTP `[REDACTED - supply via SIMF_SuperAdmin__TotpSecret]` |
| **Last reviewed** | 2026-06-09 (D-353 — dialog/full-page framing + delete confirmation) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-INT-001 | Full CRUD round-trip — Add → Edit → Details → Deactivate | happy | P0 | smoked manually 2026-05-28 |
| E2E-INT-002 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-INT-003 | Auth: non-admin user → `/not-permitted` | auth | P0 | _to author_ |
| E2E-INT-004 | Validation: submit empty Name → bilingual error toast | error | P1 | _to author_ |
| E2E-INT-005 | Conflict: duplicate name → 409 + bilingual server message | error | P1 | _to author_ |
| E2E-INT-006 | Server error: API 500 on `/list` → bilingual fallback | resilience | P2 | _to author_ |
| E2E-INT-007 | RTL render: Arabic toggle mirrors page + modal | i18n | P1 | smoked manually 2026-05-28 |
| E2E-INT-008 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-INT-009 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-INT-010 | Delete confirmation: Deactivate opens View/Delete → SimfConfirm gates the call (D-353) | error | P0 | _to author_ |
| E2E-INT-011 | Excel export: toolbar Export downloads an .xlsx of the filtered grid (D-356) | happy | P1 | _to author_ |
| E2E-INT-012 | Excel import: upload a workbook → rows created + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-INT-013 | Excel import: a non-workbook / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |

## Scenarios

### E2E-INT-001 — Full CRUD round-trip

```gherkin
Feature: Interests CRUD round-trip
  As an Administrator
  I want to manage the visitor-facing Interests picker
  So that the picker stays accurate to the event programme

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp
  And they have landed on /admin/interests

Scenario: Create, edit, view, deactivate one interest
  Given the grid currently shows {N} rows
  When the administrator clicks "Add interest"
  Then the Add modal opens with three fields: Name, Name (Arabic), Display order
  When they fill Name="Naval Engineering"
  And they fill Name (Arabic)="الهندسة البحرية"
  And they fill Display order="10"
  And they click "Create interest"
  Then the modal closes
  And the grid shows {N + 1} rows
  And a green toast reads "Interest \"Naval Engineering\" was created."
  And a row exists with Name="Naval Engineering" and Display order=10 and the green "Active" pill

  When the administrator clicks the "Edit" icon on that row
  Then the Edit modal opens with the row's values pre-filled
  And an additional "Active" checkbox is visible (ticked)
  When they change Display order to "0"
  And they click "Save changes"
  Then the modal closes
  And a green toast reads "Interest \"Naval Engineering\" was updated."
  And the row's Display order column reads "0"

  When the administrator clicks the "Details" icon on that row
  Then a read-only modal opens with all four fields rendered in a description list
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Deactivate" icon on that row
  Then the View/Delete form opens (dialog by default) showing the row's read-only details
  And a red "Deactivate" button is visible
  When they click "Deactivate"
  Then a SimfConfirm dialog asks to confirm, naming the interest
  When they click the confirm "Deactivate" button
  Then the form closes
  And a green toast reads "Interest \"Naval Engineering\" was deactivated."
  And the row remains visible but the pill changes to the grey "Inactive" pill
```

**Evidence captured:**
- Before / After modal screenshots → `docs/screenshots/d132-interests-{canonical,add-modal,details-modal,edit-modal}.png`
- Console errors: 0 expected (the D-132 mid-flight `ValueExpression` bug is fixed at commit `d0dcaa7`)
- Network: every `/account/api/admin/interests/*` call returns 200
- Audit rows: `RowAudit` rows for the Insert + 2 Updates with the actor id

### E2E-INT-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Interest rows
  When the administrator opens /admin/interests
  Then the grid body renders the SimfEmptyState component
  And the empty state shows the bilingual copy "No interests yet." / "لا توجد اهتمامات بعد."
  And the toolbar still shows the "Add interest" button
```

### E2E-INT-003 — Auth gate

```gherkin
Scenario: Non-administrator user is denied
  Given a signed-in user without the Administrator role (e.g. a Visitor)
  When they navigate to /admin/interests
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/interests/list request fires
```

### E2E-INT-004 — Validation failure

```gherkin
Scenario: Empty Name shows the server's bilingual validation reason in the modal
  Given the Add modal is open
  When the administrator leaves Name blank
  And clicks "Create interest"
  Then a /account/api/admin/interests POST request fires and returns VALIDATION_FAILED
  And a SimfAlert error appears at the top of the modal
  And reads "The English name is required." / "الاسم بالإنجليزية مطلوب."
  And the modal stays open
```

### E2E-INT-005 — Duplicate name

```gherkin
Scenario: Duplicate Name returns 409 with bilingual server message
  Given an Interest with Name="Naval Engineering" exists
  When the administrator opens the Add modal
  And fills Name="Naval Engineering" + Name (Arabic)="الهندسة البحرية" + Display order="0"
  And clicks "Create interest"
  Then the BFF forwards POST /admin/interests
  And the API returns HTTP 409 with ApiResult.Error.Code = "InterestNameNotUnique"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture()
```

### E2E-INT-006 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows fallback bilingual toast
  Given the API is configured to return 500 on /admin/interests/list (e.g. DB down)
  When the administrator opens /admin/interests
  Then the grid shows the loading indicator
  And then a red toast appears reading "The interests could not be loaded." / "تعذّر تحميل الاهتمامات."
  And no rows render
```

### E2E-INT-007 — RTL render

```gherkin
Scenario: Arabic toggle mirrors page + Add modal
  Given the administrator is on /admin/interests in English
  When they click the "العربية" link in the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الاهتمامات"
  And the nav rail mirrors (Arabic labels)
  And the toolbar buttons appear in reverse order
  And the pager arrows reverse

  When they click "إضافة اهتمام"
  Then the Add modal opens in RTL
  And the field labels are Arabic
  And the form actions appear in reverse order
```

### E2E-INT-008 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/interests with the default "dialog" presentation
  And the grid toolbar shows the "Open as full page" toggle (maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.interests" holds {"v":1,"presentation":"page"}
  When they reload /admin/interests
  Then the toggle still reads "Open as dialog"
  And opening Add now renders the full-page frame (not a popup)
```

### E2E-INT-009 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page"
  When the administrator clicks "Add interest"
  Then the grid + banner are replaced by the CrudPageFrame (title + close header + the form)
  And there is no modal backdrop
  When they fill the three fields and click "Create interest"
  Then the page frame closes
  And the grid re-appears with the new row and the success toast
  When they click the "Edit" icon and then the frame's close (X) button
  Then the form closes and the grid re-appears unchanged
```

### E2E-INT-010 — Delete confirmation gate (D-353)

```gherkin
Scenario: Deactivate requires explicit confirmation
  Given the administrator is on /admin/interests
  When they click the "Deactivate" icon on a row
  Then the View/Delete form opens showing the row's read-only details and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog appears naming the interest
  And it cannot be dismissed by a backdrop click (RequireExplicitClose)
  When they click "Cancel"
  Then no DELETE request fires and the row is unchanged
  When they re-open and click "Deactivate" then confirm
  Then exactly one DELETE /account/api/admin/interests/{id} fires
  And the success toast appears and the pill turns grey "Inactive"
```

### E2E-INT-011 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid to an XLSX workbook
  Given the administrator is on /admin/interests with at least two interests
  When they click the toolbar "Export" action with no rows selected
  Then a POST /account/api/admin/interests/export fires with an empty Ids list and the current Query
  And the browser saves a file named simf-interests-{timestamp}.xlsx
  And the workbook's "Interests" sheet has the header Name | NameArabic | DisplayOrder | IsActive
  When they instead select two rows then click "Export"
  Then the workbook contains exactly those two rows
```

### E2E-INT-012 — Excel import (D-356)

```gherkin
Scenario: Import interests from a workbook and see the per-row outcome
  Given the administrator is on /admin/interests
  When they click the toolbar "Import" action
  And they choose an .xlsx whose "Interests" sheet has Name/NameArabic rows for two new interests
  Then a POST /account/api/admin/interests/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped."
  And the grid reloads and lists both new interests
  When they import a workbook containing one duplicate name and one new name
  Then the modal shows 1 created and one row error naming the duplicate
```

### E2E-INT-013 — Excel import rejection (D-356)

```gherkin
Scenario: A bad upload is rejected without creating anything
  Given the administrator is on /admin/interests
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check)
  Then the request returns 400 and the page shows a bilingual error toast
  And no interest is created
  When they import a workbook whose sheet is not named "Interests"
  Then the request returns 400 with the bilingual "worksheet named 'Interests'" message
```

---

## Implementation notes

- **Manual smoke as canonical-source-of-truth today.** Until Playwright is
  adopted, the canonical "run" of these scenarios is a Chrome DevTools
  MCP session driven by the [SIMF smoke template](../../dev/SIMF_TABLE_PATTERN.md)
  — sign in via the steps above, walk each scenario, capture screenshots
  into `docs/screenshots/{slug}-{scenario}.png`.
- **Convert to Playwright** when the test runner is adopted: copy each
  Gherkin scenario into a `.feature` file under `tests/SIMF.E2E.Tests/`
  (project to be created) + step-definition class. The Gherkin shape is
  already runner-agnostic.
- **API integration tests** at `tests/SIMF.Api.Tests/AdminInterestsTests.cs`
  cover the same surface at a lower layer (no browser). When E2E covers
  a scenario, you can usually delete the matching `Api.Tests` case — but
  during the transition, keep both.

---

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 7).
