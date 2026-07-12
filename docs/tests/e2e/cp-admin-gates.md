# E2E test catalogue — Gates CRUD (`/admin/gates`)

| | |
|--|--|
| **Page** | [`cp/admin-gates.md`](../../pages/cp/admin-gates.md) _(reference doc not yet authored)_ |
| **Route** | `/admin/gates` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page facts (read from source, do not invent):**
> - Page: `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GatesList.razor`
>   (D-148 — Gate Module CRUD list, mirrors `HallsList`).
> - Forms (D-353): the Add/Edit/View/Delete forms are hosted by **`CrudShell`**
>   (popup or full page per the toolbar toggle, persisted in localStorage via
>   `CpPreferences`). `GatesAddEdit.razor` is the shared Add/Edit form (the
>   **Active** checkbox only renders in Edit); `GatesViewDelete.razor` shows the
>   read-only details and, when deleting, gates the soft-delete behind a
>   `SimfConfirm` dialog (it is **not** a one-click delete on the list row).
> - Presentation toggle (D-353): `<CrudPresentationToggle PageKey="gates" />` in the
>   grid `CustomToolbar`; `Prefs.GetPresentationAsync("gates")` seeds it in
>   `OnInitializedAsync`; the choice persists under localStorage
>   `simf.cp.prefs.gates` = `{"v":1,"presentation":"page"|"dialog"}`.
> - Excel (D-356): the grid wires `OnExport` + `OnImport` and renders
>   `<CrudGridExcel Resource="gates" />`. BFF: `POST /account/api/admin/gates/export`
>   and `POST /account/api/admin/gates/import`. Export permission `Gates.Export`,
>   import permission `Gates.Import`. Export sheet **"Gates"**, file prefix
>   `simf-gates`, header row `Code | Name | NameArabic | DirectionMode |
>   AllowedProfileTypeCount | AssignedOperatorCount | IsActive | Description |
>   DescriptionArabic` (D-506 appended the two bilingual description columns so
>   the workbook round-trips them; `AllowedProfileTypeIds` / `AssignedOperatorUserIds`
>   stay out — they are FK collections, not flat cells). Import is **insert-only**,
>   required headers `Code | Name | NameArabic` (the parser also reads optional
>   `Description`, `DescriptionArabic`, `DirectionMode`); a duplicate code or
>   invalid field is a per-row error, not a batch abort.
> - Required permission: **`Gates.Manage`** (`@attribute [RequirePermission(PermissionCatalog.Gates.Manage)]`).
> - Nav item: `Module.Gates` → `/admin/gates`, `RequiredPermission = PermissionCatalog.Gates.Manage` (`CpNavigation.cs`).
> - BFF passthroughs (`AccountEndpoints.cs`): `POST /account/api/admin/gates/list`,
>   `GET /account/api/admin/gates/{id}`, `POST /account/api/admin/gates`,
>   `PUT /account/api/admin/gates/{id}`, `DELETE /account/api/admin/gates/{id}`.
>   The form also primes its pickers from `POST /account/api/admin/profile-types/list`
>   and `POST /account/api/admin/admins/list`.
> - API endpoints (`src/Backend/SIMF.Api/Endpoints/Admin/GateEndpoints.cs`), all gated by
>   `Gates.Manage` + `RequireApprovedAccount`; create/update/delete are rate-limited (`"auth"` limiter).
> - Grid columns: **Code, Name, Name (Arabic), Direction, Allowed types** (`0 → "All"`),
>   **Operators** (active-assignment count), **Status** (Active/Inactive pill).
> - Form fields: **Code** (2–16, uppercased+unique), **Name (English)** (1–128),
>   **Name (Arabic)** (1–128), **Description (English)** (≤1024, optional),
>   **Description (Arabic)** (≤1024, optional), **Direction policy** (In / Out / Both),
>   **Allowed profile types** (multi-select; empty = all), **Assigned operators** (multi-select),
>   **Active** (Edit only).
> - Error codes (`ErrorCodes.cs` / `AdminGateService.cs`): `GATE_INVALID` (400),
>   `GATE_NOT_FOUND` (404), `GATE_CODE_DUPLICATE` (409), `GATE_PROFILE_TYPE_INVALID` (400),
>   `GATE_ASSIGNMENT_INVALID` (400).
> - Audit events (`AuditEvents.cs`): `Gate.Created`, `Gate.Updated`, `Gate.Deactivated`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-GAT-001 | Full CRUD round-trip — Add → Edit → Details → Deactivate | happy | P0 | _to author_ |
| E2E-GAT-002 | Add a restricted gate with allowed profile types + assigned operators | happy | P1 | _to author_ |
| E2E-GAT-003 | Direction policy column reflects In / Out / Both selection | happy | P2 | _to author_ |
| E2E-GAT-004 | Search / filter the grid by code or name | happy | P2 | _to author_ |
| E2E-GAT-005 | Sort grid by Code / Name / Direction column | happy | P2 | _to author_ |
| E2E-GAT-006 | Paging — Next / Prev / First / Last + page size | happy | P2 | _to author_ |
| E2E-GAT-007 | Details modal renders all fields read-only and Close dismisses it | happy | P2 | _to author_ |
| E2E-GAT-008 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-GAT-009 | Auth gate — signed-in admin lacking `Gates.Manage` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-GAT-010 | Client validation — short code / blank names blocked in the modal | error | P1 | _to author_ |
| E2E-GAT-011 | Server validation — `GATE_INVALID` (400) surfaced bilingually | error | P1 | _to author_ |
| E2E-GAT-012 | Duplicate code — `GATE_CODE_DUPLICATE` (409) surfaced bilingually | error | P1 | _to author_ |
| E2E-GAT-013 | Server 500 / list failure → bilingual load-failed toast | resilience | P2 | _to author_ |
| E2E-GAT-014 | RTL / Arabic render mirrors page + modal | i18n | P1 | _to author_ |
| E2E-GAT-015 | Per-column filter narrows the grid (Code / Name / Name (Arabic)) | happy | P1 | _to author_ |
| E2E-GAT-016 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-GAT-017 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-GAT-018 | Delete confirmation: Deactivate opens View/Delete → SimfConfirm gates the call (D-353) | error | P0 | _to author_ |
| E2E-GAT-019 | Excel export: toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |
| E2E-GAT-020 | Excel import: upload a workbook → rows created + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-GAT-021 | Excel import: a non-workbook / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |
| E2E-GAT-022 | Excel round-trip: the bilingual Description survives export → import (D-506) | happy | P1 | _to author_ |

## Scenarios

### E2E-GAT-001 — Full CRUD round-trip

```gherkin
Feature: Gates CRUD round-trip
  As an Administrator with Gates.Manage
  I want to create, edit, view and deactivate a gate
  So that the venue access-control gates stay accurate for the event

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Gates.Manage permission has signed in
    via /login + /login/totp using superadmin@zagali-ict.com and a Get-Totp code
  And they have landed on /admin/gates

Scenario: Create, edit, view, deactivate one gate
  Given the grid currently shows {N} rows
  When the administrator clicks "Add gate"
  Then the Add modal opens titled "Add gate"
  And it shows the fields: Code, Name (English), Name (Arabic),
    Description (English), Description (Arabic), Direction policy,
    Allowed profile types, Assigned operators
  And the "Active" checkbox is NOT shown (Add mode)
  And the Direction policy defaults to "Both (inferred direction)"
  When they fill Code="G-MAIN-1"
  And they fill Name (English)="Main Entrance"
  And they fill Name (Arabic)="المدخل الرئيسي"
  And they leave Allowed profile types empty
  And they click "Create gate"
  Then the BFF forwards POST /account/api/admin/gates and the API returns 200
  And the modal closes
  And the grid shows {N + 1} rows
  And a green toast reads 'Gate "Main Entrance" was created.'
  And a row exists with Code="G-MAIN-1", Name="Main Entrance",
    Direction="Both", Allowed types="All", Operators="0",
    and the green "Active" pill

  When the administrator clicks the "Edit" icon on that row
  Then a GET /account/api/admin/gates/{id} fires
  And the Edit modal opens titled "Edit gate" with the row's values pre-filled
  And the "Active" checkbox is visible and ticked
  When they change Name (English) to "Main Gate" and Direction policy to "In (check-in only)"
  And they click "Save changes"
  Then the BFF forwards PUT /account/api/admin/gates/{id} and the API returns 200
  And the modal closes
  And a green toast reads 'Gate "Main Gate" was updated.'
  And the row's Name reads "Main Gate" and Direction reads "In"

  When the administrator clicks the "Details" icon on that row
  Then a read-only modal titled "Gate details" opens
  And it renders Code, Name, Name (Arabic), Direction, Allowed types,
    Operators and Status in a description list
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Deactivate" icon on that row
  Then the View/Delete form opens (dialog by default) showing the read-only
    details and a red "Deactivate" button (D-353 — no longer a one-click delete)
  When they click "Deactivate" and confirm the SimfConfirm dialog naming the gate
  Then a DELETE /account/api/admin/gates/{id} fires and the API returns 200
  And a green toast reads 'Gate "Main Gate" was deactivated.'
  And the row's Status pill changes to the grey "Inactive" pill
  (the confirmation gate itself is exercised in full by E2E-GAT-018)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-gates-crud-before.png`
- Screenshot add modal: `docs/screenshots/cp-admin-gates-add-modal.png`
- Screenshot edit modal: `docs/screenshots/cp-admin-gates-edit-modal.png`
- Screenshot details modal: `docs/screenshots/cp-admin-gates-details-modal.png`
- Screenshot after deactivate: `docs/screenshots/cp-admin-gates-crud-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/gates/*` call returns 200
- Audit rows: `Gate.Created`, then `Gate.Updated`, then `Gate.Deactivated`
  (`AuditEvents`) each with the actor's id and `Detail` carrying `code=G-MAIN-1`.

### E2E-GAT-002 — Add a restricted gate with allowed types + operators

```gherkin
Scenario: Create a gate restricted to chosen profile types and assigned operators
  Given at least one active profile type and one active admin exist
  And the administrator opens the "Add gate" modal
  Then the "Allowed profile types" list is primed from
    POST /account/api/admin/profile-types/list (isActive=true)
  And the "Assigned operators" list is primed from
    POST /account/api/admin/admins/list (shows "{email} — {display name}")
  When they fill Code="VIP-1", Name (English)="VIP Lounge", Name (Arabic)="صالة كبار الزوار"
  And they select one or more profile types in "Allowed profile types"
  And they select one operator in "Assigned operators"
  And they click "Create gate"
  Then the API returns 200
  And a green toast reads 'Gate "VIP Lounge" was created.'
  And the new row shows the count of selected types under "Allowed types" (not "All")
  And the "Operators" column shows the count of assigned operators
```

**Evidence captured:**
- Screenshot of the restricted-gate row → `docs/screenshots/cp-admin-gates-restricted-row.png`
- Network: `profile-types/list` + `admins/list` + `POST gates` all return 200
- Audit row: `Gate.Created` with `Detail` carrying `code=VIP-1`.

### E2E-GAT-003 — Direction policy reflected in the grid

```gherkin
Scenario Outline: The Direction column reflects the chosen policy
  Given the administrator opens the "Add gate" modal
  When they fill Code="<code>" + Name (English)="<name>" + Name (Arabic)="<ar>"
  And they pick Direction policy "<choice>"
  And they click "Create gate"
  Then the new row's Direction column reads "<column>"

Examples:
  | code  | name        | ar          | choice                    | column |
  | DIR-IN  | In Gate   | بوابة دخول   | In (check-in only)        | In     |
  | DIR-OUT | Out Gate  | بوابة خروج   | Out (check-out only)      | Out    |
  | DIR-BO  | Both Gate | بوابة مزدوجة | Both (inferred direction) | Both   |
```

### E2E-GAT-004 — Filter the grid by code

```gherkin
Scenario: Filtering by code narrows the grid
  Given gates "G-MAIN-1" (Main Gate) and "VIP-1" (VIP Lounge) exist
  When the administrator types "VIP" into the per-column "Filter column Code" input
    in the grid filter row
  Then a POST /account/api/admin/gates/list fires with Filters["code"]="VIP" and Skip=0
  And the grid shows only the "VIP-1" row
  When they clear the "Filter column Code" input
  Then the grid shows all rows again (server substring-matches gate.Code; the
    Name and Name (Arabic) columns have their own filter inputs — see E2E-GAT-015)
```

### E2E-GAT-005 — Sort the grid

```gherkin
Scenario: Sorting toggles ascending / descending on a sortable column
  Given the grid shows several gates
  When the administrator clicks the "Code" column header
  Then a POST /account/api/admin/gates/list fires with Sort="code", SortDescending=false
  And the rows render in ascending Code order
  When they click the "Code" header again
  Then the request flips to SortDescending=true and rows render in descending order
  And the same toggle works for the "Name" and "Direction" headers
```

### E2E-GAT-006 — Paging

```gherkin
Scenario: Pager controls move through pages and change page size
  Given more than one page of gates exist (page size 20)
  Then the summary reads "Showing 1–20 of {total}"
  When the administrator clicks "Next"
  Then the grid loads the next page and the summary updates
  When they click "Previous" / "First page" / "Last page"
  Then the grid loads the matching page each time
  When they change the "Show" page size
  Then a new /list request fires with the new Top and the grid re-renders
```

### E2E-GAT-007 — Details modal read-only

```gherkin
Scenario: Details modal is read-only and Close dismisses it
  Given a gate row exists
  When the administrator clicks the "Details" icon on it
  Then a GET /account/api/admin/gates/{id} fires
  And a modal titled "Gate details" opens with a description list of
    Code, Name, Name (Arabic), Direction, Allowed types (or "All"),
    Operators count and Status (Active / Inactive)
  And the modal has no editable inputs — only a "Close" button
  When they click "Close"
  Then the modal closes and the grid is unchanged
```

### E2E-GAT-008 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Gate rows (or all are filtered out)
  When the administrator opens /admin/gates
  Then the grid body renders the SimfEmptyState component titled "No gates yet."
    (Arabic: "لا توجد بوابات بعد.")
  And no error toast appears
  And the toolbar still shows the "Add gate" button
```

### E2E-GAT-009 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Gates.Manage is denied
  Given a signed-in Control Panel user whose roles do NOT grant Gates.Manage
    (and who is not the wildcard Administrator)
  When they navigate to /admin/gates
  Then the [RequirePermission(PermissionCatalog.Gates.Manage)] gate redirects
    them to /not-permitted with HTTP 200
  And no POST /account/api/admin/gates/list request fires
  And the "Gates" nav item is hidden for them (RequiredPermission = Gates.Manage)
```

### E2E-GAT-010 — Client-side validation

```gherkin
Scenario: Short code and blank names are blocked before any POST
  Given the "Add gate" modal is open
  When the administrator fills Code="G" (1 character) and clicks "Create gate"
  Then a SimfAlert error appears in the modal reading
    "Code must be between 2 and 16 characters." (Arabic: "يجب أن يتراوح الرمز بين 2 و 16 حرفاً.")
  And no POST /account/api/admin/gates request fires

  When they fix Code="G-OK" but leave Name (English) blank and click "Create gate"
  Then the alert reads "English name is required (1–128 characters)."
    (Arabic: "الاسم الإنجليزي مطلوب (1–128 حرفاً).")
  When they fill Name (English) but leave Name (Arabic) blank and click "Create gate"
  Then the alert reads "Arabic name is required (1–128 characters)."
  And the modal stays open throughout and no POST fires
```

### E2E-GAT-011 — Server-side validation (GATE_INVALID)

```gherkin
Scenario: Server rejects an invalid gate with GATE_INVALID 400 surfaced bilingually
  Given a payload that passes the client guard but fails the server guard
    (e.g. a description over 1024 characters, or a code the server rejects)
  When the administrator submits the "Add gate" modal
  Then the BFF forwards POST /account/api/admin/gates
  And the API returns HTTP 400 with ApiResult.Error.Code = "GATE_INVALID"
  And the modal stays open
  And the SimfAlert surfaces Error.MessageForCurrentCulture()
    (English e.g. "Gate code must be between 2 and 16 characters.";
     Arabic e.g. "يجب أن يتراوح رمز البوابة بين 2 و 16 حرفاً.")
```

### E2E-GAT-012 — Duplicate code (GATE_CODE_DUPLICATE)

```gherkin
Scenario: Duplicate code returns 409 with the bilingual server message
  Given a gate with Code="G-MAIN-1" already exists
  When the administrator opens the "Add gate" modal
  And fills Code="g-main-1" (the server upper-cases to "G-MAIN-1")
    + Name (English)="Duplicate Gate" + Name (Arabic)="بوابة مكررة"
  And clicks "Create gate"
  Then the BFF forwards POST /account/api/admin/gates
  And the API returns HTTP 409 with ApiResult.Error.Code = "GATE_CODE_DUPLICATE"
  And the modal stays open
  And the SimfAlert surfaces MessageForCurrentCulture()
    (English: "A gate with code 'G-MAIN-1' already exists.";
     Arabic: "توجد بوابة بالرمز 'G-MAIN-1' بالفعل.")
  And the same 409 path is exercised on Edit when changing a code to a value
    already held by another gate
```

### E2E-GAT-013 — Server 500 / list failure

```gherkin
Scenario: API failure on /list shows the bilingual load-failed toast
  Given the API is configured to fail /admin/gates/list (e.g. DB down → 500,
    or the envelope returns Success=false)
  When the administrator opens /admin/gates
  Then the grid shows the loading indicator first
  And then a red SimfAlert toast appears reading "The gates could not be loaded."
    (Arabic: "تعذّر تحميل البوابات.")
  And no rows render
```

### E2E-GAT-014 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/gates in English
  When they switch the UI to Arabic from the header language switch
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "البوابات"
  And the grid column headers and action labels render in Arabic
  And the toolbar buttons and pager arrows mirror

  When they click "إضافة بوابة" (Add gate)
  Then the Add modal opens in RTL with Arabic field labels
    (Direction options read In / Out / Both in Arabic, "Both (inferred direction)" first)
  And the form actions appear in reverse order
```

### E2E-GAT-015 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a per-column grid filter narrows the grid server-side
  Given gates "G-MAIN-1" (Main Gate / المدخل الرئيسي) and
    "VIP-1" (VIP Lounge / صالة كبار الزوار) exist
  And the grid filter row is shown (the page has Filterable columns:
    Code, Name, Name (Arabic))
  When the administrator types "VIP" into the "Filter column Code" input
  Then a POST /account/api/admin/gates/list fires with Filters["code"]="VIP"
    and Skip reset to 0
  And the grid narrows to the single "VIP-1" row
  And the summary updates to "Showing 1–1 of 1"

  When they clear the "Filter column Code" input
  And they type "Lounge" into the "Filter column Name" input
  Then a POST /account/api/admin/gates/list fires with Filters["name"]="Lounge"
    and Skip=0
  And the grid narrows to the single "VIP Lounge" row

  When they clear the "Filter column Name" input
  And they type "المدخل" into the "Filter column Name (Arabic)" input
  Then a POST /account/api/admin/gates/list fires with Filters["nameArabic"]="المدخل"
    and Skip=0
  And the grid narrows to the single "المدخل الرئيسي" row
  And the Direction / Allowed types / Operators / Status columns have no filter input
    (those columns are not Filterable)
```

**Evidence captured:**
- Screenshot of the narrowed grid → `docs/screenshots/cp-admin-gates-column-filter.png`
- Network: each keystroke (debounced) issues one `POST /account/api/admin/gates/list`
  carrying the typed value under `Filters["code"|"name"|"nameArabic"]` with `Skip=0`.
- Server: `AdminGateService` substring-matches `gate.Code` / `gate.Name` /
  `gate.NameArabic` for keys `code` / `name` / `namearabic` (case-insensitive key);
  unknown filter keys are ignored.

### E2E-GAT-016 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/gates with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle "Open as full page"
    (maximize icon) — PageKey "gates"
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.gates" holds {"v":1,"presentation":"page"}
  When they reload /admin/gates
  Then OnInitializedAsync seeds the toggle from Prefs.GetPresentationAsync("gates")
  And the toggle still reads "Open as dialog"
  And opening "Add gate" now renders the full-page CrudShell frame (not a popup)
```

**Evidence captured:**
- Screenshot of the toolbar toggle in each state → `docs/screenshots/cp-admin-gates-toggle-{dialog,page}.png`
- DevTools Application → localStorage shows `simf.cp.prefs.gates` = `{"v":1,"presentation":"page"}`

### E2E-GAT-017 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation for /admin/gates is set to "full page"
    (localStorage simf.cp.prefs.gates {"v":1,"presentation":"page"})
  When the administrator clicks "Add gate"
  Then the grid + SimfBanner are hidden (GridHidden) and the CrudShell renders the
    full-page frame: title "Add gate", a Close header, and the GatesAddEdit form
  And there is no modal backdrop
  When they fill Code="G-FP-1" + Name (English)="Full Page Gate"
    + Name (Arabic)="بوابة الصفحة الكاملة" and click "Create gate"
  Then the page frame closes and the grid re-appears with the new row
    and the green toast 'Gate "Full Page Gate" was created.'
  When they click the "Edit" icon on a row and then the frame's Close (X) button
  Then GatesAddEdit closes via OnCancel and the grid re-appears unchanged
    (no PUT fired)
  When they click the "Details" icon
  Then the read-only GatesViewDelete form takes over the content area (no backdrop)
  And clicking "Close" returns to the grid
```

### E2E-GAT-018 — Delete confirmation gate (D-353)

```gherkin
Scenario: Deactivate requires explicit SimfConfirm confirmation
  Given the administrator is on /admin/gates and a gate "G-MAIN-1" (Main Gate) exists
  When they click the "Deactivate" icon on that row
  Then a GET /account/api/admin/gates/{id} fires to load the detail
  And the GatesViewDelete form opens (dialog by default) showing the read-only
    description list (Code, Name, Name (Arabic), Direction, Allowed types,
    Operators, Status) and a red "Deactivate" button
  And NO DELETE request has fired yet (this replaced the old one-click delete)
  When they click the form's "Deactivate" button
  Then a SimfConfirm dialog appears with the message
    "Deactivate gate \"Main Gate\"?" (Arabic from Admin.Gates.Delete.Message)
    and a danger "Deactivate" confirm + "Cancel" button
  When they click "Cancel"
  Then the confirm closes, NO DELETE /account/api/admin/gates/{id} fires,
    and the row is unchanged
  When they click "Deactivate" again and then the confirm "Deactivate" button
  Then exactly one DELETE /account/api/admin/gates/{id} fires (simfAccount.deleteJson)
  And the form closes and a green toast reads 'Gate "Main Gate" was deactivated.'
  And the row's Status pill turns grey "Inactive"
```

**Evidence captured:**
- Network: confirm-cancel path fires zero DELETE; confirm path fires exactly one.
- Screenshot of the SimfConfirm dialog → `docs/screenshots/cp-admin-gates-delete-confirm.png`

### E2E-GAT-019 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid (and a selection) to an XLSX workbook
  Given the administrator is on /admin/gates with at least two gates
  And they hold the Gates.Export permission
  When they click the toolbar "Export" action with no rows selected
  Then a POST /account/api/admin/gates/export fires carrying
    AdminGridExportRequest { Ids: [], Query: <current grid query> }
    (an empty Ids list means the whole filtered grid is exported)
  And the browser saves a file named simf-gates-{timestamp}.xlsx
  And the workbook's "Gates" sheet has the header row
    Code | Name | NameArabic | DirectionMode | AllowedProfileTypeCount |
    AssignedOperatorCount | IsActive | Description | DescriptionArabic
    (D-506 appended the two description columns)
  When they instead tick two row checkboxes then click "Export"
  Then the POST carries those two row Ids (Query omitted) and the workbook
    contains exactly those two gates
  And the API caps the export at 5000 rows
```

**Evidence captured:**
- Network: `POST /account/api/admin/gates/export` returns 200 with an
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` body.
- Saved file opened: header row matches the nine columns above (the seven grid
  columns plus the D-506 Description / DescriptionArabic round-trip columns).

### E2E-GAT-020 — Excel import (D-356)

```gherkin
Scenario: Import gates from a workbook and see the per-row outcome
  Given the administrator is on /admin/gates and holds the Gates.Import permission
  When they click the toolbar "Import" action
  Then the hidden file input "gates-import-input" (accept=".xlsx") opens the picker
  When they choose an .xlsx whose "Gates" sheet has the required headers
    Code | Name | NameArabic and two new rows
    (G-IMP-1 / Import Gate 1 / بوابة مستوردة ١ and G-IMP-2 / Import Gate 2 / بوابة مستوردة ٢)
  Then a POST /account/api/admin/gates/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped."
  And the shared "Grid.Import.Done" success toast appears
  And the grid reloads (LoadAsync) and lists both new gates
  When they re-import a workbook containing one duplicate code (G-IMP-1) and one
    new code (G-IMP-3)
  Then the modal shows 1 created and one per-row error naming the duplicate row
    (import is insert-only — a duplicate code is a per-row error, not a batch abort)
```

**Evidence captured:**
- Network: `POST /account/api/admin/gates/import` (multipart) returns 200 with the
  created/updated/skipped counts + per-row error list.
- Audit rows: one `Gate.Created` per successfully imported row.

### E2E-GAT-021 — Excel import rejection (D-356)

```gherkin
Scenario: A bad / wrong-sheet upload is rejected without creating anything
  Given the administrator is on /admin/gates with the Gates.Import permission
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check)
    or exceeds the 5 MB upload gate
  Then the request returns HTTP 400 and OnError surfaces a bilingual error toast
  And no gate is created
  When they import a workbook whose sheet is NOT named "Gates"
    (or is missing one of the required headers Code / Name / NameArabic)
  Then the request returns HTTP 400 with the bilingual "worksheet named 'Gates'"
    / required-headers message
  And the grid is unchanged and no Gate.Created audit row is written
```

### E2E-GAT-022 — Excel round-trips the bilingual Description (D-506)

```gherkin
Scenario: The bilingual Description survives an export then re-import
  Given the administrator is on /admin/gates with the Gates.Export + Gates.Import permissions
  And a gate "G-DESC-1" exists with Description="Main north entrance."
    and Description (Arabic)="المدخل الشمالي الرئيسي."
  When they click the toolbar "Export" action
  Then a POST /account/api/admin/gates/export returns 200
  And the "Gates" sheet header row carries the appended Description and
    DescriptionArabic columns (D-506)
  And the G-DESC-1 row's Description cell reads "Main north entrance."
    and its DescriptionArabic cell reads "المدخل الشمالي الرئيسي."

  When they import a workbook whose "Gates" sheet carries
    Code | Name | NameArabic | Description | DescriptionArabic
    with one new row (G-DESC-2 / Gate 2 / بوابة ٢ / "Service door." / "باب الخدمة.")
  Then a POST /account/api/admin/gates/import returns 200 with 1 created, 0 errors
  And the created gate's detail (and the grid summary) carries the imported
    bilingual Description — the field is no longer dropped at the IO boundary
  And the FK collections (Allowed profile types, Assigned operators) are NOT
    expressed in the workbook (they are FK lists, set afterwards via Edit)
```

**Evidence captured:**
- Lower-layer proof: `tests/SIMF.Api.Tests/GatesExcelTests.cs` →
  `Export_includes_the_description_columns` (export header + cell) and
  `Import_round_trips_the_description` (import → list summary carries it).
- Network: `POST /account/api/admin/gates/export` then `.../import` both return 200.

---

## Implementation notes

- **Manual smoke as canonical-source-of-truth today.** Until a Playwright project
  exists, the canonical "run" of these scenarios is a Chrome DevTools MCP session:
  sign in via the Background steps, walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-gates-*.png`.
- **Convert to Playwright** when the runner is adopted: each Gherkin block maps to a
  `.feature` scenario under `tests/SIMF.E2E.Tests/` (project to be created) plus a
  step-definition class. The steps are already runner-agnostic.
- **API integration tests** at `tests/SIMF.Api.Tests/AdminGatesTests.cs` cover the
  same admin CRUD surface at a lower layer (no browser) — create / duplicate-code /
  validation / not-found / deactivate / assignments. Related lower-layer suites:
  `GateScanTests.cs`, `GateVisitorsListTests.cs`, `GateFailureCircuitTests.cs`
  (operator-scan + reports paths, not driven by this page). When an E2E scenario
  reliably covers a case, the matching `Api.Tests` case can be trimmed during the
  transition — keep both until then.
- **Permission gate** is enforced in two places that the build guards:
  `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` (nav item carries
  `Gates.Manage`) and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` (every
  gate endpoint is policy-gated). E2E-GAT-009 is the browser-level proof of the same.

---

## On-site remediation (W4 — X-1 / CHAIN-1 hall-door gate)

| Id | Scenario | Category | Priority | Status |
|----|----------|----------|----------|--------|
| E2E-GAT-023 | Create a hall-door gate (pick a Hall) vs a perimeter gate (Hall = None); the Hall picker persists and round-trips on edit | crud | P1 | _to author_ |
| E2E-GAT-024 | Create/Update with an unknown or inactive Hall → `GATE_HALL_INVALID` (400) | validation | P1 | _to author_ |

### E2E-GAT-023 — hall-door binding round-trips

```gherkin
Scenario: a gate can be bound to a hall (hall-door gate) or left as a perimeter gate
  Given the admin opens the Gate add form
  When they set Hall = "Majlis A" and save
  Then the gate persists with that HallId
  And re-opening the Edit form pre-selects "Majlis A" in the Hall picker
  When they change the Hall picker back to "None — perimeter gate" and save
  Then the gate persists with HallId = null (a perimeter gate)
  # A hall-door gate feeds HallAttendance on an allowed check-in; a perimeter
  # gate records only a GateScan.
```

### E2E-GAT-024 — invalid hall is rejected

```gherkin
Scenario: binding a gate to a missing/inactive hall is a clean 400
  Given a hall exists but is deactivated (IsActive = false)
  When the admin submits a gate create/update bound to that hall id
  Then the API responds 400 with error code GATE_HALL_INVALID
  And the message reads "The selected hall was not found or is inactive." /
      "القاعة المحددة غير موجودة أو غير نشطة."
```

---

_Last reviewed:_ 2026-07-11 by Claude (W4 on-site remediation — X-1 hall-door gate binding; E2E-GAT-023/024). Prior: 2026-06-26 by Claude (D-506 — appended the Description /
DescriptionArabic Excel round-trip columns; added E2E-GAT-022 and corrected the
stale export header list in the page facts + E2E-GAT-019).
_Previously:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle; added
E2E-GAT-016..021, corrected the stale GateForm/one-click-delete page facts).
