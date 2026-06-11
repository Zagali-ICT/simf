# E2E test catalogue — Themes & pillars (`/admin/themes`)

| | |
|--|--|
| **Page** | [`cp/admin-themes.md`](../../pages/cp/admin-themes.md) |
| **Route** | `/admin/themes` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Permission gate:** the page carries `@attribute [RequirePermission(PermissionCatalog.Themes.View)]`
> (`Themes.View`). The API endpoints are gated per-action:
> `Themes.View` (list + get), `Themes.Create` (POST), `Themes.Edit` (PUT),
> `Themes.Delete` (DELETE) — each combined with `RequireApprovedAccount`.
> A signed-in admin lacking the relevant code must be denied at both layers.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-THM-001 | Golden round-trip — Add → Edit → Details → Deactivate one theme | happy | P0 | _to author_ |
| E2E-THM-002 | Add theme (Create action, isolated) — green toast + new row | happy | P1 | _to author_ |
| E2E-THM-003 | Edit theme (Edit action) — pre-filled form, change Order + Active | happy | P1 | _to author_ |
| E2E-THM-004 | Details modal (Details action) — read-only description list, Close | happy | P1 | _to author_ |
| E2E-THM-005 | Deactivate theme (Delete action) — soft-delete → Inactive pill | happy | P1 | _to author_ |
| E2E-THM-006 | Filter by Name + sort Code/Name/Order columns | happy | P2 | _to author_ |
| E2E-THM-007 | Pager — page size, First/Prev/Next/Last, summary text | happy | P2 | _to author_ |
| E2E-THM-008 | Multiselect — select-all + per-row select checkboxes render | happy | P2 | _to author_ |
| E2E-THM-009 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-THM-010 | Auth gate: signed-in admin lacking `Themes.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-THM-011 | Validation: blank Code → bilingual modal error, no POST | error | P1 | _to author_ |
| E2E-THM-012 | Validation: blank English Name → bilingual modal error | error | P1 | _to author_ |
| E2E-THM-013 | Validation: blank Arabic Name → bilingual modal error | error | P1 | _to author_ |
| E2E-THM-014 | Validation: negative Display order → bilingual modal error | error | P1 | _to author_ |
| E2E-THM-015 | Validation: blank Page color → bilingual modal error | error | P1 | _to author_ |
| E2E-THM-016 | Conflict: duplicate Code → 409 `THEME_CODE_DUPLICATE` | error | P0 | _to author_ |
| E2E-THM-017 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-THM-018 | RTL / Arabic render mirrors page + Add modal | i18n | P1 | _to author_ |
| E2E-THM-019 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-THM-020 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-THM-021 | Delete confirmation: Deactivate opens View/Delete → SimfConfirm gates the call (D-353) | error | P0 | _to author_ |
| E2E-THM-022 | Excel export: toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |
| E2E-THM-023 | Excel import: upload a workbook → rows created + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-THM-024 | Excel import: a non-workbook / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |

## Scenarios

### E2E-THM-001 — Golden round-trip

```gherkin
Feature: Themes & pillars CRUD round-trip
  As an Administrator
  I want to manage the programme themes (the top-level agenda grouping)
  So that sessions can be grouped by an accurate, bilingual pillar list

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the Website is reachable on http://localhost:5115
  And an Administrator signs in at /login with superadmin@zagali-ict.com
  And they complete TOTP at /login/totp using the Get-Totp helper
  And they navigate to /admin/themes
  And the page title reads "Themes & pillars · SIMF"

Scenario: Create, edit, view, then deactivate one theme
  Given the grid currently shows {N} rows
  When the administrator clicks "Add theme"
  Then the "Add theme" modal opens hosting the ThemeForm
  And it shows fields: Code, Name (English), Name (Arabic), Description (English),
      Description (Arabic), Display order, Page color
  And the Page color field defaults to "#244A77"
  And no "Active" checkbox is shown (Create mode)
  When they fill Code="DEF"
  And they fill Name (English)="Defence & Security"
  And they fill Name (Arabic)="الدفاع والأمن"
  And they fill Description (English)="Naval defence, security and deterrence."
  And they fill Display order="10"
  And they leave Page color="#244A77"
  And they click "Create theme"
  Then the POST /account/api/admin/themes call returns HTTP 200 with Success=true
  And the Code is persisted uppercased as "DEF"
  And the modal closes
  And the grid shows {N + 1} rows
  And a green toast reads 'Theme "Defence & Security" was created.'
  And a row exists with Code="DEF", Name="Defence & Security", Order=10,
      a color swatch + literal "#244A77", and the green "Active" pill

  When the administrator clicks the "Edit" icon on that row
  Then a GET /account/api/admin/themes/{id} call returns HTTP 200
  And the "Edit theme" modal opens with every field pre-filled from the row
  And an "Active — show in the agenda + the Session editor picker" checkbox is visible and ticked
  When they change Display order to "0"
  And they click "Save changes"
  Then the PUT /account/api/admin/themes/{id} call returns HTTP 200
  And the modal closes
  And a green toast reads 'Theme "Defence & Security" was updated.'
  And the row's Order column reads "0"

  When the administrator clicks the "Details" icon on that row
  Then a read-only "Theme details" modal opens as a description list
  And it shows Code, Name, Name (Arabic), Description (English),
      Description (Arabic), Order, Color, and Status="Active"
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Deactivate" icon on that row
  Then the View/Delete form opens (dialog by default) showing the row's read-only details
      and a red "Deactivate" button (D-353 — was a one-click DELETE)
  When they click "Deactivate" and confirm the SimfConfirm dialog
  Then the DELETE /account/api/admin/themes/{id} call returns HTTP 200
  And a green toast reads 'Theme "Defence & Security" was deactivated.'
  And the row's Status pill changes to the grey "Inactive" pill
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-themes-golden-before.png`
- Screenshots: `docs/screenshots/cp-admin-themes-{add-modal,edit-modal,details-modal,deactivated}.png`
- Screenshot after: `docs/screenshots/cp-admin-themes-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/themes/*` call returns 200
- Audit rows: `OperationLog` rows with `Event = 'Theme.Created'`, `'Theme.Updated'`,
  `'Theme.Deactivated'`, each with `Outcome = Success` and the actor's id
  (Detail carries `id=…; code=DEF; …`)

### E2E-THM-002 — Add theme (isolated Create action)

```gherkin
Scenario: Create a single theme from the Add modal
  Given the administrator is on /admin/themes
  When they click "Add theme"
  And they fill Code="TECH"
  And they fill Name (English)="Naval Technology"
  And they fill Name (Arabic)="التقنية البحرية"
  And they fill Display order="20"
  And they click "Create theme"
  Then the modal closes
  And a green toast reads 'Theme "Naval Technology" was created.'
  And a new row shows Code="TECH" and Order=20 and the green "Active" pill
```

### E2E-THM-003 — Edit theme (isolated Edit action)

```gherkin
Scenario: Edit an existing theme's name and active state
  Given a theme with Code="TECH" exists and is Active
  When the administrator clicks the "Edit" icon on its row
  Then the "Edit theme" modal opens with Code="TECH", Name="Naval Technology" pre-filled
  And the "Active" checkbox is ticked
  When they change Name (English) to "Naval Technology & Innovation"
  And they untick the "Active" checkbox
  And they click "Save changes"
  Then the PUT /account/api/admin/themes/{id} call returns HTTP 200
  And the modal closes
  And a green toast reads 'Theme "Naval Technology & Innovation" was updated.'
  And the row Name reads "Naval Technology & Innovation"
  And the Status pill is the grey "Inactive" pill
```

### E2E-THM-004 — Details modal (isolated Details action)

```gherkin
Scenario: Details modal renders every field read-only
  Given a theme with Code="DEF" exists with a bilingual description
  When the administrator clicks the "Details" icon on its row
  Then a GET /account/api/admin/themes/{id} call returns HTTP 200
  And the "Theme details" modal opens
  And the description list shows Code, Name, Name (Arabic),
      Description (English), Description (Arabic), Order, Color, Status
  And an empty optional description renders as "—"
  When they click "Close"
  Then the modal closes and no mutation request fires
```

### E2E-THM-005 — Deactivate theme (isolated Delete action)

```gherkin
Scenario: Deactivate soft-deletes the theme
  Given a theme with Code="DEF" exists and is Active
  When the administrator clicks the "Deactivate" icon on its row
  Then the View/Delete form opens with the row's read-only details and a red "Deactivate" button
  When they click "Deactivate" and confirm the SimfConfirm dialog (D-353 — was a one-click DELETE)
  Then the DELETE /account/api/admin/themes/{id} call returns HTTP 200 (Success=true)
  And a green toast reads 'Theme "Defence & Security" was deactivated.'
  And the row stays visible with the grey "Inactive" pill
  And an OperationLog row with Event = 'Theme.Deactivated' is written
```

### E2E-THM-006 — Filter + sort

```gherkin
Scenario: Filter by Name and sort the sortable columns
  Given the grid shows several themes
  When the administrator types "Naval" into the filter (filter column = Name)
  Then the grid shows only rows whose Name contains "Naval"
  When they clear the filter
  And they click the "Code" column header
  Then the rows reorder ascending by Code
  When they click the "Name" column header
  Then the rows reorder by Name
  When they click the "Order" column header
  Then the rows reorder ascending by Display order
  And each /account/api/admin/themes/list call returns HTTP 200
```

### E2E-THM-007 — Pager

```gherkin
Scenario: Paging walks the pages and updates the summary
  Given the database has more than one page of themes (page size 20)
  When the administrator opens /admin/themes
  Then the summary reads "Showing 1–20 of {total}"
  When they click "Next"
  Then the summary advances (e.g. "Showing 21–40 of {total}")
  And the pager shows "Page 2 of {N}"
  When they click "Last page"
  Then the final page renders
  When they click "First page"
  Then the first page renders again
  And the page-size "Show" control changes the rows per page
```

### E2E-THM-008 — Multiselect checkboxes

```gherkin
Scenario: Multiselect select-all and per-row selection render
  Given the grid shows at least two themes
  Then a "Select all" checkbox is present in the header
  And each row has a "Select row" checkbox
  When the administrator ticks "Select all"
  Then every visible row checkbox becomes ticked
  When they untick one row
  Then "Select all" reflects the partial selection
```

### E2E-THM-009 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Theme rows
  When the administrator opens /admin/themes
  Then the grid body renders the SimfEmptyState component
  And it shows the bilingual copy "No themes yet." / "لا توجد محاور بعد."
  And the toolbar still shows the "Add theme" button
  And no error toast appears
```

### E2E-THM-010 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Themes.View is denied
  Given a signed-in Control Panel user whose role does NOT include the
      Themes.View permission (and is not the wildcard Administrator "*")
  When they navigate to /admin/themes
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/themes/list request fires
  And separately, calling POST /admin/themes/list on the API with a token
      missing Themes.View returns HTTP 403
```

### E2E-THM-011 — Validation: blank Code

```gherkin
Scenario: Blank Code shows the bilingual modal error, no POST
  Given the "Add theme" modal is open
  When the administrator leaves Code blank
  And fills Name (English)="X", Name (Arabic)="س", Display order="0"
  And clicks "Create theme"
  Then a SimfAlert error appears at the top of the modal
  And it reads "Code must be between 2 and 16 characters." /
      "يجب أن يتراوح الرمز بين 2 و 16 حرفاً."
  And the modal stays open
  And no POST /account/api/admin/themes request fires
```

### E2E-THM-012 — Validation: blank English Name

```gherkin
Scenario: Blank English Name shows the bilingual modal error
  Given the "Add theme" modal is open
  When the administrator fills Code="DEF" and leaves Name (English) blank
  And clicks "Create theme"
  Then a SimfAlert error reads "English name is required (1–128 characters)." /
      "الاسم الإنجليزي مطلوب (من 1 إلى 128 حرفاً)."
  And the modal stays open and no POST fires
```

### E2E-THM-013 — Validation: blank Arabic Name

```gherkin
Scenario: Blank Arabic Name shows the bilingual modal error
  Given the "Add theme" modal is open
  When the administrator fills Code="DEF" and Name (English)="Defence"
  And leaves Name (Arabic) blank
  And clicks "Create theme"
  Then a SimfAlert error reads the Arabic-name-required message
      ("Arabic name is required (1–128 characters).")
  And the modal stays open and no POST fires
```

### E2E-THM-014 — Validation: negative Display order

```gherkin
Scenario: Negative Display order shows the bilingual modal error
  Given the "Add theme" modal is open
  When the administrator fills Code="DEF", Name (English)="Defence",
      Name (Arabic)="الدفاع"
  And sets Display order="-1"
  And clicks "Create theme"
  Then a SimfAlert error reads "Display order must be zero or a positive integer."
  And the modal stays open and no POST fires
```

### E2E-THM-015 — Validation: blank Page color

```gherkin
Scenario: Blank Page color shows the bilingual modal error
  Given the "Add theme" modal is open
  When the administrator clears the Page color field (the default #244A77 removed)
  And fills the other required fields validly
  And clicks "Create theme"
  Then a SimfAlert error reads "Page color is required (1–32 characters)."
  And the modal stays open and no POST fires
```

### E2E-THM-016 — Conflict: duplicate Code

```gherkin
Scenario: Duplicate Code returns 409 with bilingual server message
  Given a theme with Code="DEF" already exists
  When the administrator opens the "Add theme" modal
  And fills Code="def" (lower case), Name (English)="Defence 2",
      Name (Arabic)="الدفاع ٢", Display order="0", Page color="#244A77"
  And clicks "Create theme"
  Then the BFF forwards POST /admin/themes
  And the API uppercases "def" → "DEF" and detects the case-insensitive clash
  And returns HTTP 409 with ApiResult.Error.Code = "THEME_CODE_DUPLICATE"
  And the modal stays open
  And the SimfAlert surfaces the bilingual MessageForCurrentCulture()
      ("A theme with code 'DEF' already exists." /
       "يوجد محور بالرمز 'DEF' بالفعل.")
```

### E2E-THM-017 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to fail on POST /admin/themes/list (e.g. DB down)
  When the administrator opens /admin/themes
  Then the grid first shows the "Loading themes…" indicator
  And then a red toast reads "The themes could not be loaded." /
      "تعذّر تحميل المحاور."
  And no rows render
```

### E2E-THM-018 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/themes in English
  When they switch the language to Arabic
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "المحاور والمواضيع"
  And the grid headers, toolbar, and pager arrows mirror (RTL)
  And the "Add theme" button reads "إضافة محور"

  When they click "إضافة محور"
  Then the "Add theme" modal ("إضافة محور") opens in RTL
  And the field labels are Arabic
  And the form actions appear in reverse order
```

### E2E-THM-019 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/themes with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle ("Open as full page", maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.themes" holds {"v":1,"presentation":"page"}
  When they reload /admin/themes
  Then the page reads the persisted preference via Prefs.GetPresentationAsync("themes")
  And the toggle still reads "Open as dialog"
  And opening "Add theme" now renders the full-page frame (not a popup)
```

### E2E-THM-020 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page"
  When the administrator clicks "Add theme"
  Then the grid + banner are hidden (GridHidden) and the CrudShell renders ThemesAddEdit
      as a full page (title "Add theme" + close header + the form)
  And there is no modal backdrop
  When they fill Code="OPS", Name (English)="Naval Operations", Name (Arabic)="العمليات البحرية",
      Display order="30", Page color="#244A77"
  And they click "Create theme"
  Then the page frame closes
  And the grid re-appears with the new "OPS" row and the success toast
      'Theme "Naval Operations" was created.'
  When they click the "Edit" icon and then the CrudShell close (X) button
  Then the form closes and the grid re-appears unchanged (no PUT fired)
```

### E2E-THM-021 — Delete confirmation gate (D-353)

```gherkin
Scenario: Deactivate requires explicit confirmation via SimfConfirm
  Given the administrator is on /admin/themes
  When they click the "Deactivate" icon on a theme row (Code="OPS")
  Then a GET /account/api/admin/themes/{id} loads the detail
  And the ThemesViewDelete form opens (CrudShell) showing the row's read-only
      details (Code, Name, Name (Arabic), Description, Description (Arabic),
      Order, Page color, Status) and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog appears reading
      'Deactivate the theme "Naval Operations"? It will be hidden from the agenda
       and the Session editor picker. You can reactivate it later by editing it.' /
      'تعطيل المحور "Naval Operations"؟ سيُخفى من جدول الأعمال ومنتقي محرر الجلسة.
       يمكنك إعادة تفعيله لاحقاً بتعديله.'
  When they click "Cancel" on the SimfConfirm
  Then no DELETE request fires and the row is unchanged
  When they re-open Deactivate, click "Deactivate", then confirm
  Then exactly one DELETE /account/api/admin/themes/{id} fires
  And the success toast 'Theme "Naval Operations" was deactivated.' appears
  And the row's pill turns grey "Inactive"
```

### E2E-THM-022 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid / selected rows to an XLSX workbook
  Given the administrator is on /admin/themes with at least two themes
  When they click the toolbar "Export" action with no rows selected
  Then a POST /account/api/admin/themes/export fires with an
      AdminGridExportRequest of an empty Ids list and the current Query
      (whole filtered grid)
  And the browser saves a file named simf-themes-{timestamp}.xlsx
  And the workbook's "Themes" sheet header row reads
      Code | Name | NameArabic | DisplayOrder | PageColor | IsActive
  When they instead select two rows then click "Export"
  Then the request carries those two Ids (Query omitted) and the workbook
      contains exactly those two themes
  And the export is capped at 5000 rows server-side
```

### E2E-THM-023 — Excel import (D-356)

```gherkin
Scenario: Import themes from a workbook and see the per-row outcome
  Given the administrator is on /admin/themes
  When they click the toolbar "Import" action
  Then the hidden file input "themes-import-input" (accept=".xlsx") opens the picker
  When they choose an .xlsx whose "Themes" sheet has the required headers
      Code | Name | NameArabic | PageColor and two new theme rows
  Then a POST /account/api/admin/themes/import fires as multipart form data
  And the import-result modal "Import results" shows "2 created, 0 updated, 0 skipped."
  And the shared success toast reads "Import complete." / "اكتمل الاستيراد."
  And the grid reloads and lists both new themes
  When they import a workbook with one duplicate Code and one new Code
  Then the modal shows 1 created and one per-row error
      ("Row {n} ({Code}): …") naming the duplicate code
      (import is insert-only — a duplicate is a row error, not a batch abort)
```

### E2E-THM-024 — Excel import rejection (D-356)

```gherkin
Scenario: A bad / wrong-sheet upload is rejected without creating anything
  Given the administrator is on /admin/themes
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check)
      or exceeds the 5 MB cap
  Then the request returns HTTP 400 and the page shows a bilingual error toast
  And no theme is created
  When they import a workbook whose sheet is not named "Themes"
      (or is missing a required header: Code / Name / NameArabic / PageColor)
  Then the request returns HTTP 400 with the bilingual rejection
      ("worksheet named 'Themes'" / required-header message)
  And the import is capped at 5000 rows server-side
```

---

## Implementation notes

- **Manual smoke is canonical today.** Until a Playwright project exists, the
  authoritative "run" of these scenarios is a Chrome DevTools MCP session:
  sign in via the Background, walk each row, and capture screenshots into
  `docs/screenshots/cp-admin-themes-{scenario}.png`.
- **Convert to Playwright later.** Each Gherkin scenario maps 1:1 into a
  `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus a
  step-definition class. The steps are deliberately runner-agnostic.
- **Lower-layer coverage.** `ThemeEndpoints.cs` carries a
  `// Tests: SIMF.Api.Tests/AdminThemesTests.cs` header, but **that file does
  not yet exist** under `tests/SIMF.Api.Tests/` — there is currently no
  dedicated API-integration test for the Themes CRUD surface, so these E2E
  scenarios are the only end-to-end coverage. The permission-gate dimension
  (E2E-THM-010) IS exercised at the unit layer by
  `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs`, which fails the build
  if an admin endpoint is missing its `PermissionCatalog` policy.
- **Backing data model.** Themes persist to the new `dbo.Themes` table (the
  first D-135 freeze-lift module). Code is uppercased server-side and is
  unique case-insensitively; the conflict path (E2E-THM-016) depends on that
  normalisation in `AdminThemeService.ValidateAndNormalise`.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel export + import + D-353 Page↔Popup toggle).
