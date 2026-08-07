# E2E test catalogue — Halls & seating (`/admin/halls`)

| | |
|--|--|
| **Page** | [`cp/admin-halls.md`](../../pages/cp/admin-halls.md) |
| **Route** | `/admin/halls` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` / `[REDACTED - supply via SIMF_SuperAdmin__TempPassword]` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Permission gate:** the page is `@attribute [RequirePermission(PermissionCatalog.Halls.View)]`
> (`Halls.View`). Add (`Halls.Create`), Edit (`Halls.Edit`) and Deactivate
> (`Halls.Delete`) each gate their own API endpoint. `Administrator = "*"` covers
> all four. The CP nav item `Module.Halls` → `/admin/halls` carries
> `RequiredPermission = PermissionCatalog.Halls.View`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-HAL-001 | Full CRUD round-trip — Add → Edit → Details → Deactivate | happy | P0 | _to author_ |
| E2E-HAL-002 | Empty list renders `SimfEmptyState` ("No halls yet.") | happy | P1 | _to author_ |
| E2E-HAL-003 | Auth: signed-in admin lacking `Halls.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-HAL-004 | Add a hall with the optional geofence triple (lat/lon/radius) | happy | P1 | _to author_ |
| E2E-HAL-005 | Validation: bad Code (length) → bilingual error in modal | error | P1 | _to author_ |
| E2E-HAL-006 | Validation: blank English / Arabic name → bilingual error | error | P1 | _to author_ |
| E2E-HAL-007 | Validation: negative / non-numeric Capacity → bilingual error | error | P1 | _to author_ |
| E2E-HAL-008 | Validation: partial / out-of-range geofence → bilingual error | error | P1 | _to author_ |
| E2E-HAL-009 | Conflict: duplicate Code → 409 `HALL_CODE_DUPLICATE` | error | P1 | _to author_ |
| E2E-HAL-010 | Details modal renders read-only `<dl>` incl. equipment notes | happy | P2 | _to author_ |
| E2E-HAL-011 | Edit: re-activate a deactivated hall via the `Active` checkbox | happy | P2 | _to author_ |
| E2E-HAL-012 | Grid: sort by Code / Name / Capacity + filter search | happy | P2 | _to author_ |
| E2E-HAL-013 | Grid: pager (page size, first/last/prev/next) round-trip | happy | P2 | _to author_ |
| E2E-HAL-014 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-HAL-015 | RTL render: Arabic toggle mirrors page + Add modal | i18n | P1 | _to author_ |
| E2E-HAL-016 | Grid: per-column filter (code/name/nameArabic/floor) narrows the list | happy | P2 | _to author_ |
| E2E-HAL-017 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-HAL-018 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-HAL-019 | Delete confirmation gate: Deactivate opens View/Delete → SimfConfirm gates the call (D-353) | error | P0 | _to author_ |
| E2E-HAL-020 | Excel export: toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |
| E2E-HAL-021 | Excel import: upload a workbook → rows created/updated + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-HAL-022 | Excel import: a non-.xlsx / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |
| E2E-HAL-023 | Edit preserves a re-sent geofence — regression (D-505) | error | P0 | _to author_ |
| E2E-HAL-024 | Excel round-trip: import a workbook carrying EquipmentNotes / geofence / SeatSelectionMode → fields land on the summary; export header carries them (D-506) | happy | P1 | _to author_ |

> `E2E-HAL-025` and `E2E-HAL-026..030` are catalogued in their own sections
> further down — **On-site remediation (W4)** and **QA B16 — hall occupancy
> view** — each with its own table and written-up scenarios, which is this
> file's convention for a later-appended batch. The five B16 rows had *also*
> been copied into this matrix, so each of them appeared twice in the same
> document under the same id. The copies were removed on 2026-07-28 and the
> sections below are authoritative; all 30 ids remain catalogued.

## Scenarios

### E2E-HAL-001 — Full CRUD round-trip

```gherkin
Feature: Halls CRUD round-trip
  As an Administrator
  I want to manage the venue halls used for Session assignment
  So that the programme can place sessions and seat plans against real rooms

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (TOTP from Get-Totp)
  And they have landed on /admin/halls
  And the grid loaded via POST /account/api/admin/halls/list returning HTTP 200

Scenario: Create, edit, view, then deactivate one hall
  Given the grid currently shows {N} rows
  When the administrator clicks "Add hall"
  Then the Add modal opens titled "Add hall"
  And it shows fields: Code, Name (English), Name (Arabic), Capacity, Floor,
      Equipment + accessibility notes, Geofence centre latitude,
      Geofence centre longitude, Geofence radius (metres)
  And there is NO "Active" checkbox (it only appears in Edit)
  When they fill Code="H1"
  And they fill Name (English)="Main Auditorium"
  And they fill Name (Arabic)="القاعة الرئيسية"
  And they fill Capacity="500"
  And they fill Floor="Ground"
  And they fill Equipment + accessibility notes="Projector, wheelchair ramp"
  And they leave all three geofence fields empty
  And they click "Create hall"
  Then the BFF forwards POST /account/api/admin/halls → API POST /admin/halls (HTTP 200)
  And the Code is normalised to upper-case "H1" before send
  And the modal closes
  And the grid shows {N + 1} rows
  And a green toast reads "Hall \"Main Auditorium\" was created." / "تم إنشاء القاعة \"Main Auditorium\"."
  And a row exists with Code="H1", Name="Main Auditorium", Capacity=500,
      Floor="Ground" and the green "Active" pill

  When the administrator clicks the "Edit" icon on that row
  Then a GET /account/api/admin/halls/{id} fires (HTTP 200)
  And the Edit modal opens titled "Edit hall" with the row's values pre-filled
  And an additional "Active — available for Session assignment" checkbox is visible (ticked)
  When they change Capacity to "650"
  And they click "Save changes"
  Then the BFF forwards PUT /account/api/admin/halls/{id} (HTTP 200)
  And the modal closes
  And a green toast reads "Hall \"Main Auditorium\" was updated." / "تم تحديث القاعة \"Main Auditorium\"."
  And the row's Capacity column reads "650"

  When the administrator clicks the "Details" icon on that row
  Then a GET /account/api/admin/halls/{id} fires (HTTP 200)
  And a read-only modal titled "Hall details" opens with a description list of
      Code, Name, Name (Arabic), Capacity, Floor, Equipment + accessibility notes, Status
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Deactivate" icon on that row
  Then the View/Delete form opens (CrudShell, dialog by default) showing the row's read-only details and a red "Deactivate" button
  When they click "Deactivate" and confirm the SimfConfirm dialog (see E2E-HAL-019)
  Then exactly one DELETE /account/api/admin/halls/{id} fires (HTTP 200)
  And a green toast reads "Hall \"Main Auditorium\" was deactivated." / "تم تعطيل القاعة \"Main Auditorium\"."
  And the row remains visible but the pill changes to the grey "Inactive" pill
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-admin-halls-add-before.png`,
  `docs/screenshots/cp-admin-halls-add-after.png`,
  `docs/screenshots/cp-admin-halls-edit-modal.png`,
  `docs/screenshots/cp-admin-halls-details-modal.png`,
  `docs/screenshots/cp-admin-halls-deactivated.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/halls/*` call returns 200
- Audit rows: `OperationLog`/`AuditEntry` rows with `Event = 'Hall.Created'`,
  `Event = 'Hall.Updated'`, `Event = 'Hall.Deactivated'`, each with the actor's id

### E2E-HAL-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Hall rows
  When the administrator opens /admin/halls
  Then POST /account/api/admin/halls/list returns HTTP 200 with an empty page
  And the grid body renders the SimfEmptyState component
  And the empty state shows the bilingual copy "No halls yet." / "لا توجد قاعات بعد."
  And the toolbar still shows the "Add hall" button
  And no error toast appears
```

### E2E-HAL-003 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Halls.View is denied
  Given a user is signed in to the Control Panel
  And their role does NOT grant PermissionCatalog.Halls.View (and is not Administrator "*")
  When they navigate to /admin/halls
  Then the [RequirePermission(Halls.View)] attribute denies access
  And they land on /not-permitted with HTTP 200
  And no /account/api/admin/halls/list request fires
```

### E2E-HAL-004 — Add with the optional geofence triple

```gherkin
Scenario: Create a hall with a valid geofence (lat + lon + radius together)
  Given the Add modal is open
  When the administrator fills Code="H2"
  And fills Name (English)="GPS Hall"
  And fills Name (Arabic)="قاعة تحديد الموقع"
  And fills Capacity="120"
  And fills Geofence centre latitude="24.7136"
  And fills Geofence centre longitude="46.6753"
  And fills Geofence radius (metres)="50"
  And clicks "Create hall"
  Then POST /account/api/admin/halls is sent with GeofenceCenterLat=24.7136,
      GeofenceCenterLon=46.6753, GeofenceRadiusMeters=50 (invariant-culture parse)
  And the API returns HTTP 200
  And a green toast reads "Hall \"GPS Hall\" was created."
  And opening the new row's Details still shows Status=Active
```

### E2E-HAL-005 — Code length validation

```gherkin
Scenario: Code shorter than 2 / longer than 16 is rejected client-side
  Given the Add modal is open
  When the administrator fills Code="H" (1 char)
  And fills Name (English)="X" and Name (Arabic)="س" and Capacity="10"
  And clicks "Create hall"
  Then a SimfAlert error appears at the top of the modal
  And reads "Code must be between 2 and 16 characters." / "يجب أن يتراوح الرمز بين 2 و 16 حرفاً."
  And the modal stays open
  And no /account/api/admin/halls POST request fires
```

### E2E-HAL-006 — Blank name validation

```gherkin
Scenario: Blank English or Arabic name shows the bilingual error
  Given the Add modal is open with Code="H3" and Capacity="10"
  When the administrator leaves Name (English) blank
  And clicks "Create hall"
  Then a SimfAlert error reads "English name is required (1–128 characters)."
  And the modal stays open and no POST fires

  When they fill Name (English)="Hall 3" but leave Name (Arabic) blank
  And click "Create hall"
  Then a SimfAlert error reads "Arabic name is required (1–128 characters)."
  And the modal stays open and no POST fires
```

### E2E-HAL-007 — Capacity validation

```gherkin
Scenario: Non-numeric or negative Capacity is rejected client-side
  Given the Add modal is open with Code="H4", Name (English)="Hall 4", Name (Arabic)="قاعة"
  When the administrator fills Capacity="-5"
  And clicks "Create hall"
  Then a SimfAlert error reads "Capacity must be zero or a positive integer."
  / "يجب أن تكون السعة صفراً أو عدداً صحيحاً موجباً."
  And the modal stays open and no POST fires
```

### E2E-HAL-008 — Geofence validation

```gherkin
Scenario: Partial or out-of-range geofence is rejected client-side
  Given the Add modal is open with valid Code/Name/Arabic/Capacity
  When the administrator fills Geofence centre latitude="24.71"
  And leaves Geofence centre longitude and radius empty (partial set)
  And clicks "Create hall"
  Then a SimfAlert error reads
      "The geofence needs a valid latitude (−90..90), longitude (−180..180) and
       radius (greater than 0, up to 100000 m) — set all three or leave all empty."
  / the Arabic equivalent (Admin.Halls.Field.GeofenceInvalid)
  And the modal stays open and no POST fires

  When they instead fill latitude="999", longitude="46.6", radius="50" (out of range)
  And click "Create hall"
  Then the same geofence SimfAlert error appears and no POST fires
```

### E2E-HAL-009 — Duplicate Code conflict

```gherkin
Scenario: Duplicate Code returns 409 with the bilingual server message
  Given a hall with Code="H1" already exists
  When the administrator opens the Add modal
  And fills Code="h1" (any case), Name (English)="Dup", Name (Arabic)="مكرر", Capacity="10"
  And clicks "Create hall"
  Then the BFF forwards POST /admin/halls (Code normalised to "H1")
  And the API returns HTTP 409 with ApiResult.Error.Code = "HALL_CODE_DUPLICATE"
  And the modal stays open
  And the SimfAlert surfaces the bilingual MessageForCurrentCulture()
      "A hall with code 'H1' already exists." / "توجد قاعة بالرمز 'H1' بالفعل."
```

### E2E-HAL-010 — Details modal

```gherkin
Scenario: Details modal renders all fields read-only
  Given a hall exists with an Equipment + accessibility notes value and no geofence
  When the administrator clicks the "Details" icon on its row
  Then a GET /account/api/admin/halls/{id} fires (HTTP 200)
  And a modal titled "Hall details" opens
  And it shows a description list with Code, Name, Name (Arabic), Capacity, Floor
      (or "—" when null), Equipment + accessibility notes (or "—"), and Status
  And no editable inputs are present
  When they click "Close"
  Then the modal closes and no write request fires
```

### E2E-HAL-011 — Re-activate via Edit

```gherkin
Scenario: A deactivated hall is re-activated through the Edit modal
  Given a hall with the grey "Inactive" pill exists
  When the administrator clicks the "Edit" icon on its row
  Then the Edit modal opens with the "Active — available for Session assignment"
      checkbox UNticked
  When they tick the "Active" checkbox
  And click "Save changes"
  Then PUT /account/api/admin/halls/{id} is sent with IsActive=true (HTTP 200)
  And a green toast reads "Hall \"{name}\" was updated."
  And the row's pill flips back to the green "Active" pill
```

### E2E-HAL-012 — Sort + filter

```gherkin
Scenario: Grid sorting and search filter round-trip through the list endpoint
  Given the grid shows several halls
  When the administrator clicks the "Code" column header
  Then a POST /account/api/admin/halls/list fires with the sort on "code"
  And rows re-order ascending by Code
  When they click the "Capacity" column header
  Then the list re-queries sorted by "capacity"
  When they type "Main" into the Search box
  Then a POST /account/api/admin/halls/list fires carrying the filter term
  And only halls matching "Main" remain in the grid
```

### E2E-HAL-013 — Pager

```gherkin
Scenario: Pager controls round-trip through the list endpoint
  Given there are more halls than one page (Top=20)
  When the administrator changes "Show" page size to a smaller value
  Then a POST /account/api/admin/halls/list fires with the new Top
  And the summary reads "Showing {0}–{1} of {2}"
  When they click "Last page" then "First page" then "Next" / "Previous"
  Then each click fires a list query with the adjusted Skip
  And the "Page {0} of {1}" indicator updates accordingly
```

### E2E-HAL-014 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/halls/list (e.g. DB down)
  When the administrator opens /admin/halls
  Then the grid first shows the "Loading halls…" indicator
  And then a red toast appears reading "The halls could not be loaded." / "تعذّر تحميل القاعات."
  And no rows render
```

### E2E-HAL-015 — RTL render

```gherkin
Scenario: Arabic toggle mirrors page + Add modal
  Given the administrator is on /admin/halls in English
  When they click the "العربية" link in the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "القاعات والمقاعد"
  And the nav rail mirrors (Arabic labels) and the toolbar buttons reverse order
  And the pager arrows reverse

  When they click "إضافة قاعة"
  Then the Add modal opens in RTL
  And the field labels are Arabic (Code/Name/Capacity/geofence)
  And the form actions ("Create hall" / "Cancel") appear in reverse order
```

### E2E-HAL-016 — Per-column grid filter narrows the list

```gherkin
Scenario: Typing into a per-column filter input re-queries the list endpoint
  Given the SimfDataGrid shows several halls (Top=20)
  And the Code, Name, Name (Arabic) and Floor columns each expose a filter input
      (those four columns are Filterable; Capacity and Active are not)
  When the administrator types "Main" into the "Filter column Name" input
  Then a POST /account/api/admin/halls/list fires carrying
      GridQuery.Filters["name"]="Main" with Skip reset to 0
  And only halls whose Name contains "Main" remain in the grid
  And the summary recomputes to "Showing 1–{matched} of {matched}"

  When they clear the Name filter and type "Ground" into the "Filter column Floor" input
  Then a POST /account/api/admin/halls/list fires carrying
      GridQuery.Filters["floor"]="Ground" with Skip reset to 0
  And only halls on the "Ground" floor remain (rows with a null Floor are excluded)

  When they additionally type "H" into the "Filter column Code" input
  Then the list re-queries with both Filters["floor"]="Ground" and Filters["code"]="H"
      combined (AND), narrowing the grid further
```

### E2E-HAL-017 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/halls with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle "Open as full page" control (maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.halls" holds {"v":1,"presentation":"page"}
  When they reload /admin/halls
  Then OnInitializedAsync re-reads the preference via Prefs.GetPresentationAsync("halls")
  And the toggle still reads "Open as dialog"
  And opening "Add hall" now renders the full-page frame (not a popup)
```

### E2E-HAL-018 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (localStorage "simf.cp.prefs.halls" = page)
  When the administrator clicks "Add hall"
  Then the grid + SimfBanner are hidden (GridHidden) and the CrudShell renders the
      HallsAddEdit form as a full page titled "Add hall" with a close header
  And there is no modal backdrop
  When they fill Code="H9", Name (English)="Annex", Name (Arabic)="الملحق", Capacity="80"
  And they click "Create hall"
  Then POST /account/api/admin/halls fires (HTTP 200)
  And the page frame closes (CloseForm) and the grid re-appears with the new row and the success toast
  When they click the "Edit" icon and then the frame's close (X) button
  Then the form closes and the grid re-appears unchanged (no PUT fires)
```

### E2E-HAL-019 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Deactivate requires explicit SimfConfirm confirmation
  Given the administrator is on /admin/halls
  When they click the "Deactivate" icon on a row for Name="Main Auditorium"
  Then a GET /account/api/admin/halls/{id} fires (HTTP 200)
  And the HallsViewDelete form opens inside the CrudShell showing the read-only <dl>
      (Code, Name, Name (Arabic), Capacity, Floor, Equipment + accessibility notes, Status)
      and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog appears titled "Deactivate hall"
  And its message names the hall: "Deactivate hall \"Main Auditorium\"? It will be hidden from Session assignment."
      / the Arabic equivalent (Admin.Halls.Delete.Message)
  When they click "Cancel"
  Then no DELETE request fires and the row is unchanged
  When they re-open the form, click "Deactivate" then the confirm "Deactivate" button
  Then exactly one DELETE /account/api/admin/halls/{id} fires (HTTP 200)
  And the form closes and a green toast reads "Hall \"Main Auditorium\" was deactivated."
  And the row's pill turns grey "Inactive"
```

### E2E-HAL-020 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid (or just selected rows) to an XLSX workbook
  Given the administrator is on /admin/halls with at least two halls
  When they click the toolbar "Export" action with no rows selected
  Then OnExportAsync calls CrudGridExcel.ExportAsync with an empty Ids list and the current Query
  And a POST /account/api/admin/halls/export fires carrying
      AdminGridExportRequest { Ids: [], Query: <current GridQuery> }
  And the browser saves an .xlsx whose sheet has the header row
      Code | Name | NameArabic | Capacity | Floor | IsActive |
      EquipmentNotes | GeofenceCenterLat | GeofenceCenterLon |
      GeofenceRadiusMeters | SeatSelectionMode
      (the last five appended by D-506 so the export round-trips)
  And the workbook contains every hall in the current filtered grid (capped at 5000 rows)
  When they instead select two rows then click "Export"
  Then the request carries those two Ids (and Query is omitted/ignored for the selection)
  And the workbook contains exactly those two halls
```

### E2E-HAL-021 — Excel import (D-356)

```gherkin
Scenario: Import halls from a workbook and see the per-row outcome
  Given the administrator is on /admin/halls
  When they click the toolbar "Import" action
  Then OnImportAsync calls CrudGridExcel.TriggerImportAsync, opening the file picker
      (input id "halls-import-input", accept=".xlsx")
  When they choose an .xlsx whose sheet has Code/Name/NameArabic/Capacity rows for two new halls
      (and optionally the D-506 EquipmentNotes / GeofenceCenterLat / GeofenceCenterLon /
       GeofenceRadiusMeters / SeatSelectionMode columns — bound by header name)
  Then a POST /account/api/admin/halls/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped."
  And OnImportedAsync raises the shared success toast (Grid.Import.Done) and the grid reloads listing both new halls
  When they import a workbook that updates one existing Code and adds one new Code
  Then the modal shows "1 created, 1 updated, 0 skipped."
  When they import a workbook containing one row that fails validation (e.g. Capacity="-5")
  Then the modal lists that row under the per-row error list and it is not created
```

### E2E-HAL-022 — Excel import rejection (D-356)

```gherkin
Scenario: A bad upload is rejected without creating anything
  Given the administrator is on /admin/halls
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check, or exceeds the 5 MB gate)
  Then POST /account/api/admin/halls/import returns HTTP 400
  And OnExcelError surfaces a bilingual error toast and no hall is created
  When they import a workbook whose sheet is not the expected "halls" sheet
  Then the request returns HTTP 400 with the bilingual wrong-worksheet message
  And nothing is created or updated
```

---

## Implementation notes

- **Manual smoke as canonical source-of-truth today.** Until Playwright is
  adopted, the canonical "run" of these scenarios is a Chrome DevTools MCP
  session driven by the SIMF smoke pattern — sign in via the Background steps,
  walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-halls-{scenario}.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be
  created) plus a step-definition class. The Gherkin shape is already
  runner-agnostic.
- **Backing API.** The CP calls the BFF proxy in
  `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`
  (`/account/api/admin/halls/list|{id}` + POST/PUT/DELETE), which forwards to
  the FastEndpoints API in
  `src/Backend/SIMF.Api/Endpoints/Admin/HallEndpoints.cs`. Permissions:
  `Halls.View` (list/get), `Halls.Create` (POST), `Halls.Edit` (PUT),
  `Halls.Delete` (DELETE). Error codes live in `SIMF.Common/ErrorCodes.cs`
  (`HALL_INVALID`, `HALL_NOT_FOUND`, `HALL_CODE_DUPLICATE`, `HALL_IN_USE` —
  enforced on deactivation since A37, see E2E-HAL-032 —
  `HALL_GEOFENCE_INVALID`). Audit events: `Hall.Created`, `Hall.Updated`,
  `Hall.Deactivated` (`SIMF.Application/Auditing/AuditEvents.cs`).
- **API integration tests** that cover the same surface at a lower layer
  (no browser): `tests/SIMF.Api.Tests/AdminHallGeofenceTests.cs` (geofence
  parse + persistence + the **D-505** `Update_preserves_a_geofence_that_is_resent`
  edit-wipe regression). The hall arrival/attendance chain is covered by
  `tests/SIMF.Api.Tests/HallArrivalScanTests.cs` and
  `tests/SIMF.Api.Tests/HallAttendanceTests.cs` (a different surface — those
  drive the mobile arrival flow, not this CP page). No dedicated
  `AdminHallsTests.cs` CRUD suite exists yet (the endpoint's `// Tests:` header
  references one as the intended home), so E2E-HAL-005..009 also serve as the
  authoritative regression record for hall validation/conflict until that
  xUnit suite lands.

### E2E-HAL-023 — Edit preserves a re-sent geofence (regression D-505)

```gherkin
Scenario: Editing a hall does not wipe its geofence
  # Regression for D-505: the inline UpdateHallRequest bind model omitted the
  # geofence, so FastEndpoints dropped the lat/lon/radius the CP form resends and
  # the service wiped the stored geofence on every edit.
  Given a hall "Geo Hall" exists with geofence lat=24.7136 lon=46.6753 radius=80
  And the administrator opens its Edit form (the geofence fields pre-fill)
  When they change Capacity to 45, leave the geofence fields unchanged, and Save
  Then PUT /account/api/admin/halls/{id} returns HTTP 200
  And the returned hall still has GeofenceCenterLat=24.7136, GeofenceCenterLon=46.6753,
      GeofenceRadiusMeters=80 (the geofence was NOT wiped) and Capacity=45
```

**Evidence:** `tests/SIMF.Api.Tests/AdminHallGeofenceTests.cs` →
`Update_preserves_a_geofence_that_is_resent` (fails before the fix — geofence
returns null; passes after the bind model inherits the contract).

### E2E-HAL-024 — Excel round-trip of the dropped fields (D-506)

```gherkin
Scenario: Import a workbook carrying the extra fields; export carries them too
  # Regression for D-506: EquipmentNotes, the geofence triple and
  # SeatSelectionMode were dropped — neither exported nor imported. The grid
  # summary now carries them and the import binds them by header name.
  Given the administrator is on /admin/halls
  When they import a workbook whose sheet has the columns
      Code | Name | NameArabic | Capacity | Floor |
      EquipmentNotes | GeofenceCenterLat | GeofenceCenterLon |
      GeofenceRadiusMeters | SeatSelectionMode
  And one row sets EquipmentNotes="Projector + PA system",
      GeofenceCenterLat=24.7136, GeofenceCenterLon=46.6753,
      GeofenceRadiusMeters=250, SeatSelectionMode="OpenSeating"
  Then POST /account/api/admin/halls/import returns HTTP 200 with 1 created, 0 errors
  And the new grid row's summary carries EquipmentNotes="Projector + PA system",
      the geofence triple (24.7136 / 46.6753 / 250) and SeatSelectionMode=1 (OpenSeating)
  And SeatSelectionMode also accepts the raw int (0/1); blank → 0 (AssignedSeat)
  And an unknown SeatSelectionMode value, a non-numeric geofence value, or a
      partial geofence (one of the three set) → a per-row 400 error, not a batch abort

  When they then Export the grid
  Then the .xlsx header row also contains EquipmentNotes, GeofenceCenterLat,
      GeofenceCenterLon, GeofenceRadiusMeters and SeatSelectionMode
      (SeatSelectionMode written by display name AssignedSeat/OpenSeating)
```

**Evidence:** `tests/SIMF.Api.Tests/HallsExcelTests.cs` →
`Import_round_trips_the_extra_columns_onto_the_summary` (asserts the five fields
land on the grid summary after import) and `Export_includes_the_extra_columns`
(asserts the export header row carries the five appended columns).

## On-site remediation (W4 — H-3 capacity-shrink guard)

| Id | Scenario | Category | Priority | Status |
|----|----------|----------|----------|--------|
| E2E-HAL-025 | Reducing Capacity below the committed seat-layout total / active reservations → `HALL_CAPACITY_BELOW_USAGE` | validation | P1 | _to author_ |

## QA B16 — hall occupancy view (sessions in this hall)

| Id | Scenario | Category | Priority | Status |
|----|----------|----------|----------|--------|
| E2E-HAL-026 | Hall detail lists the sessions assigned to the hall with local times + status | happy | P1 | _to author_ |
| E2E-HAL-027 | A hall with no sessions shows the schedule empty state | happy | P2 | _to author_ |
| E2E-HAL-028 | The schedule read is gated by `Halls.View` (same as the page) | auth | P1 | _to author_ |
| E2E-HAL-029 | A soft-deleted session is not shown as occupancy (matches the overlap guard) | error | P0 | _to author_ |
| E2E-HAL-030 | A schedule longer than one page says it was capped | error | P2 | _to author_ |

### E2E-HAL-026 — hall occupancy view

```gherkin
Scenario: The hall detail shows what the hall is doing
  # QA B16: before this there was no hall schedule / calendar / session list on
  # any hall surface, so the one-session-per-hall rule only ever surfaced as a
  # 409 SESSION_HALL_TIME_OVERLAP from the Sessions editor.
  Given a hall "Main Auditorium" (Code="H1") exists
  And session "SES-1" / "Opening Plenary" is assigned to it, 11:00 AM–12:00 PM
      on 05-01-2026 Saudi time, status Scheduled
  When the administrator clicks the "Details" icon on the H1 row
  Then a GET /account/api/admin/halls/{id} fires (HTTP 200)
  And a GET /account/api/admin/halls/{id}/schedule fires (HTTP 200)
  And below the read-only <dl> a section headed "Sessions in this hall"
      / "الجلسات في هذه القاعة" renders a table with the columns
      Code | Session | Starts | Ends | Status
  And the row reads SES-1 | Opening Plenary | 05-01-2026 11:00 AM | 05-01-2026 12:00 PM
      and a "Scheduled" status pill
  And every time is Saudi local, 12-hour — no UTC stamp appears anywhere
  And the summary line reads "1 session(s) in this hall."
```

**Evidence:** `tests/SIMF.ControlPanel.Tests/HallsViewDeleteTests.cs` →
`B16_schedule_lists_the_sessions_assigned_to_this_hall` (asserts the schedule URL
+ the rendered row) and `B16_schedule_times_are_local_never_utc` (asserts
`11:00 AM` / `12:00 PM` render and the raw `08:00` UTC hour never does).

### E2E-HAL-027 — unbooked hall

```gherkin
Scenario: A hall with no sessions shows the empty state
  Given a hall "Annex" exists with no session assigned to it
  When the administrator opens its Details form
  Then GET /account/api/admin/halls/{id}/schedule returns HTTP 200 with an empty page
  And the "Sessions in this hall" section renders the SimfEmptyState
      "No sessions are assigned to this hall." / "لا توجد جلسات مسندة إلى هذه القاعة."
  And no table renders
```

**Evidence:** `tests/SIMF.ControlPanel.Tests/HallsViewDeleteTests.cs` →
`B16_schedule_shows_the_empty_state_for_an_unbooked_hall`.

### E2E-HAL-028 — schedule auth gate

```gherkin
Scenario: The hall schedule carries the hall page's own permission
  Given an admin holds PermissionCatalog.Halls.View (and is not Administrator "*")
  When they open a hall's Details form
  Then GET /admin/halls/{id}/schedule returns HTTP 200
  # It deliberately does NOT require Sessions.View: the schedule is part of the
  # hall surface, so whoever can view the hall can see what the hall is doing.

  Given an admin holds no Halls.* permission
  When the same request is made directly against the API
  Then it returns HTTP 403
```

**Evidence:** `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` →
`Every_admin_endpoint_is_permission_and_approval_gated` sweeps every mapped
`/admin/*` route, so an ungated schedule endpoint fails the build.

### E2E-HAL-029 — a deactivated session is not occupancy

```gherkin
Scenario: The occupancy view agrees with the overlap guard
  # The schedule exists to expose SESSION_HALL_TIME_OVERLAP up front, and that
  # guard matches on other.IsActive. The Status column shows the SessionStatus
  # lifecycle (Scheduled/Held/Recorded/Published), NOT IsActive, so a leaked
  # soft-deleted row would be indistinguishable from a live booking.
  Given a hall "Main Auditorium" exists
  And session "SES-1" is assigned to it and is active
  And session "SES-2" is assigned to the same hall and has been deactivated
  When the administrator opens the hall's Details form
  Then the "Sessions in this hall" table lists SES-1
  And it does NOT list SES-2
  And creating a new session in that hall over SES-2's window succeeds
      (no SESSION_HALL_TIME_OVERLAP), so the view and the guard agree

  Given the hall's only session has been deactivated
  When the administrator opens the hall's Details form
  Then the schedule renders the empty state, not a phantom booking
```

**Evidence:** `tests/SIMF.Api.Tests/HallScheduleTests.cs` →
`Schedule_lists_only_the_active_sessions_in_this_hall` and
`Schedule_of_a_hall_whose_only_session_is_deleted_is_empty`.

### E2E-HAL-030 — a schedule longer than one page says it was capped

```gherkin
Scenario: A capped schedule is not shown as if it were complete
  # The endpoint asks for 200 rows, which is also the ClampPage ceiling, so a
  # hall with more active sessions than that WOULD be truncated silently.
  Given a hall has more active sessions than the schedule page holds
  When the administrator opens its Details form
  Then the table renders the first page of rows
  And an info alert reads "Showing the first {shown} of {total} sessions..."
      / "يتم عرض أول {shown} من أصل {total} جلسة..."
  And it points the administrator at the Sessions list filtered by this hall

  Given the hall's schedule fits in one page
  Then no capped notice renders
```

**Evidence:** `tests/SIMF.ControlPanel.Tests/HallsViewDeleteTests.cs` →
`B16_a_capped_schedule_says_so_instead_of_reading_as_complete` and
`B16_a_complete_schedule_shows_no_capped_notice`.

---

### E2E-HAL-025 — capacity cannot drop below committed seats

```gherkin
Scenario: a capacity reduction below the seat-layout total is blocked
  Given the hall "Majlis A" has a 5-row × 10-seat layout (50 seats committed)
  When the admin edits the hall and lowers Capacity to 40 and saves
  Then the API responds 409 with error code HALL_CAPACITY_BELOW_USAGE
  And the message reads "Capacity cannot drop below what this hall already
      commits (50)." / "لا يمكن خفض السعة دون ما تلتزم به هذه القاعة بالفعل (50)."
  # Capacity == committed passes; an increase always passes; a hall with no
  # layout and no active reservations may shrink freely.
```

## Session-lifecycle QA package (A37 — hall in-use guard)

| Id | Scenario | Category | Priority | Status |
|----|----------|----------|----------|--------|
| E2E-HAL-032 | Deactivating a hall that active sessions still use → 409 `HALL_IN_USE` naming the count; the hall stays Active; the same guard runs when the edit form clears Active | validation | P1 | authored ✓ (`SessionLifecycleNoticeTests.A37_Deactivating_a_hall_active_sessions_use_is_rejected`) |

### E2E-HAL-032 — a hall in use cannot be deactivated

```gherkin
Feature: Deactivating a hall does not orphan the sessions inside it
  As an Administrator
  I want the refusal at the moment I make the mistake
  So that it does not resurface later as SESSION_HALL_NOT_FOUND on an unrelated edit

Scenario: the Deactivate action is refused while an active session uses the hall
  Given the hall "Auditorium A" (code "AUD-A") is Active
  And one active session "SES-001" is scheduled in it
  When the admin clicks Deactivate on AUD-A and confirms
  Then the API responds 409 with error code HALL_IN_USE
  And the message reads "This hall is still used by 1 active session(s) - move or
      deactivate them before deactivating the hall." with its Arabic pair
  And AUD-A is still Active in the grid

Scenario: clearing the Active checkbox on the edit form takes the same guard
  When the admin opens Edit on AUD-A, unticks Active and saves
  Then the API responds 409 HALL_IN_USE and AUD-A is still Active

Scenario: re-home the session first and the hall deactivates normally
  When the admin deactivates SES-001 (or moves it to another hall)
  And then deactivates AUD-A
  Then the API responds 200 and AUD-A shows the grey Inactive pill
  # Before A37 the flip always succeeded here; the damage surfaced later, as a
  # 400 SESSION_HALL_NOT_FOUND the next time anyone edited an orphaned session.
```

---

_Last reviewed:_ 2026-07-27 by Claude (QA B16 follow-up — the occupancy view now filters `isActive` so a soft-deleted session no longer reads as a live booking, and a capped page says so; E2E-HAL-029/030). Prior: 2026-07-26 by Claude (QA B16 — hall occupancy view; E2E-HAL-026..028). Prior: 2026-07-11 by Claude (W4 on-site remediation — H-3 capacity-shrink guard; E2E-HAL-025). Prior: 2026-06-26 by Claude (D-506 — Excel export/import field-drop fix: EquipmentNotes + geofence triple + SeatSelectionMode now round-trip; scenario E2E-HAL-024 added, E2E-HAL-020/021 column lists reconciled). Prior: 2026-06-10 (D-356 Phase 5 — Excel export/import + D-353 Page<->Popup toggle scenarios E2E-HAL-017..022; E2E-HAL-001 deactivate step reconciled to the CrudShell + SimfConfirm gate).
_Last reviewed:_ 2026-07-26 by Claude (session-lifecycle QA package — A37 hall in-use deactivation guard, `HALL_IN_USE` now enforced rather than reserved; E2E-HAL-032). Prior: 2026-07-11 by Claude (W4 on-site remediation — H-3 capacity-shrink guard; E2E-HAL-025). Prior: 2026-06-26 by Claude (D-506 — Excel export/import field-drop fix: EquipmentNotes + geofence triple + SeatSelectionMode now round-trip; scenario E2E-HAL-024 added, E2E-HAL-020/021 column lists reconciled). Prior: 2026-06-10 (D-356 Phase 5 — Excel export/import + D-353 Page<->Popup toggle scenarios E2E-HAL-017..022; E2E-HAL-001 deactivate step reconciled to the CrudShell + SimfConfirm gate).

---

## QA A40 — the seat-layout row action

| Id | Scenario | Category | Priority | Status |
|----|----------|----------|----------|--------|
| E2E-HAL-031 | A "Seat layout" row action deep-links each hall to its seat-layout editor; it is hidden from an admin without `SeatLayouts.View` | happy | P0 | authored ✓ (`HallsListSeatLayoutActionTests`) |
| E2E-HAL-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-HAL-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

### E2E-HAL-031 — jump from a hall row to its seat layout

```gherkin
Scenario: The row action opens the seat-layout editor on that hall
  Given the administrator holds "Halls.View" and "SeatLayouts.View"
  And they are on "/admin/halls" with hall "H-01" (Main Hall, cap 120) listed
  When they click the "Seat layout" row action on the "Main Hall" row
  Then the browser navigates to "/admin/halls/seat-layouts?hallId=<H-01 id>"
  And the editor's hall picker already shows "H-01 - Main Hall (cap 120)"
  And H-01's stored rows and per-row seat counts are loaded for editing
  # Before A40 the editor was reachable only from the side-menu item, which opens
  # on a blank picker — there was no route from a hall to its own seat map.

Scenario: The row action is hidden without the seat-layout permission
  Given the administrator holds "Halls.View" but NOT "SeatLayouts.View"
  When they open "/admin/halls"
  Then the hall rows render with Details / Edit / Deactivate
  And no "Seat layout" row action is offered on any row
```

**Evidence captured:**
- bUnit: `tests/SIMF.ControlPanel.Tests/HallsListSeatLayoutActionTests.cs` — the action
  renders, navigates to `?hallId=`, and is absent without the permission.
- The editor side of the same journey is E2E-HSL-024 / 025 in
  [`cp-admin-halls-seat-layouts.md`](cp-admin-halls-seat-layouts.md).

---

_Last reviewed:_ 2026-07-27 by Claude (QA A40 — the "Seat layout" row action on the
Halls grid + its permission gate; E2E-HAL-031).

## D-839 — per-hall arrival grace

| Id | Scenario | Category | Priority | Status |
|----|----------|----------|----------|--------|
| E2E-HAL-033 | A hall's **Arrival grace (minutes)** opens a session the 15-minute default would refuse, without arming the global walk-in capability | happy | P0 | authored ✓ (`ArrivalGraceResolutionTests.A_hall_grace_opens_a_session_the_default_would_refuse`) |
| E2E-HAL-034 | An explicit hall grace of **0** is honoured as zero, not treated as "unset" and silently replaced by the global 15 | validation | P0 | authored ✓ (`ArrivalGraceResolutionTests.A_hall_grace_of_zero_beats_the_global_default`) |
| E2E-HAL-035 | Arrival grace outside 0..240 is refused with 400, and blank stores null (inherit) rather than 0 | validation | P1 | authored ✓ (`ArrivalGraceResolutionTests.A_hall_grace_outside_the_bound_is_refused`) |

### E2E-HAL-033 — a hall opens its doors early for a queue

```gherkin
Feature: A slow-filling hall widens its own arrival window
  As an Administrator preparing a keynote hall
  I want the doors to open before the default 15 minutes
  So that a queue forming 40 minutes early can be scanned in
  # And crucially WITHOUT arming WalkInMode, which is server-access-only
  # because it also relaxes an approval gate. Before D-839 those were the
  # same lever.

  Background:
    Given I am signed in to the Control Panel as an Administrator
    And a hall "GR-KEYNOTE" exists with capacity 100
    And a session "GRS-OPENING" in that hall starts in 40 minutes
    And an approved visitor holds badge QR "ABC123456789"

  Scenario: the default refuses, the hall setting admits
    When an operator scans "ABC123456789" at the hall door
    Then the API responds 409 with error code SESSION_NOT_LIVE
    And the message reads "This session is not open for arrivals right now."
        / "هذه الجلسة ليست مفتوحة لتسجيل الوصول حالياً."

    When I open /admin/halls, edit "GR-KEYNOTE"
    And I set "Arrival grace (minutes)" to 60
    And I save
    Then the hall is saved and the grid shows it

    When the operator scans "ABC123456789" again
    Then the API responds 200
    And the attendee is marked arrived with method QrScan
    And the Hall-Arrivals console now lists "GRS-OPENING" in its session picker
    # The console reads the RESOLVED value off the session row; before D-839 it
    # hard-coded 15 and would still have hidden this session.
```

### E2E-HAL-034 — zero means zero

```gherkin
Feature: An explicit zero arrival grace is not mistaken for "unset"
  As an Administrator locking a hall to exact session times
  I want 0 to mean 0
  So that "no grace" is a setting I can actually express

  Scenario: 0 does not fall through to the global default
    Given a hall "GR-STRICT" has "Arrival grace (minutes)" set to 0
    And a session in that hall starts in 8 minutes
    # 8 minutes is INSIDE the global 15, so the door would open if the
    # explicit 0 were read as "inherit".
    When an operator scans an approved visitor's badge at that hall's door
    Then the API responds 409 with error code SESSION_NOT_LIVE
```

### E2E-HAL-035 — the bound is enforced

```gherkin
Feature: Arrival grace is rejected outside its bound, not clamped
  As an Administrator
  I want to be told when I typed the wrong number
  So that I am not left believing the doors are open four times longer than they are

  Scenario Outline: out of range is refused
    Given I am signed in as an Administrator
    When I create a hall with arrivalGraceMinutes = <value>
    Then the API responds 400
    And the message names the 0 to 240 bound

    Examples:
      | value |
      | -1    |
      | 241   |

  Scenario: blank means inherit, not zero
    When I create a hall leaving "Arrival grace (minutes)" empty
    Then the hall is created with arrivalGraceMinutes = null
    And a session in that hall uses the global WalkInMode value (15 by default)
    # An Excel import of a blank cell behaves the same way; a non-numeric cell
    # is a per-row error rather than a silent 0, which would slam the hall shut
    # the instant a session ended.
```

_Last reviewed:_ 2026-08-04 by Claude (D-839 — per-hall arrival grace; E2E-HAL-033..035).
