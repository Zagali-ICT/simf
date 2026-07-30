# E2E test catalogue — Organisations lookup CRUD (`/admin/organisations`)

| | |
|--|--|
| **Page** | [`cp/admin-organisations.md`](../../pages/cp/admin-organisations.md) _(reference doc not yet authored)_ |
| **Route** | `/admin/organisations` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page permission:** the page is gated by `@attribute [RequirePermission(PermissionCatalog.Organisations.View)]`.
> The toolbar / row actions are individually gated by `Organisations.Create`,
> `Organisations.Edit`, `Organisations.Delete`, `Organisations.Import` and
> `Organisations.Export` (`AdminOnly` baseline). `Administrator = "*"` therefore
> sees every action.
> The CP page calls the BFF passthroughs under `/account/api/admin/organisations/*`
> (`AccountEndpoints.cs`), which forward to the API endpoints in
> `OrganisationEndpoints.cs`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-ORG-001 | Golden round-trip — Add → search → Edit (detail prefill) → Deactivate | happy | P0 | _to author_ |
| E2E-ORG-002 | Empty list / no-match search renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-ORG-003 | Search box reloads the grid server-side (`GridQuery.Search`) | function | P1 | _to author_ |
| E2E-ORG-004 | Excel import — pick `.xlsx`, upload, row tallies + per-row errors | function | P0 | _to author_ |
| E2E-ORG-005 | Auth gate — admin lacking `Organisations.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-ORG-006 | Action gate — admin lacking `Organisations.Create` sees no "New" button | auth | P1 | _to author_ |
| E2E-ORG-007 | Validation — blank Arabic name → bilingual error toast, no POST | error | P1 | _to author_ |
| E2E-ORG-008 | Server validation — Arabic name > 256 chars → 400 `ORGANISATION_INVALID` | error | P2 | _to author_ |
| E2E-ORG-009 | Conflict — duplicate Commercial registration → 409 `ORGANISATION_INVALID` | error | P1 | _to author_ |
| E2E-ORG-010 | Delete confirm cancelled — `confirm` returns false → no DELETE | function | P2 | _to author_ |
| E2E-ORG-011 | Import rejects a non-`.xlsx` / bad-magic file → bilingual import-failed toast | error | P2 | _to author_ |
| E2E-ORG-012 | Server 500 on `/list` → bilingual `LoadFailed` toast, no rows | resilience | P2 | _to author_ |
| E2E-ORG-013 | RTL / Arabic render mirrors page, grid, both modals | i18n | P1 | _to author_ |
| E2E-ORG-014 | Per-column filter narrows the grid (`GridQuery.Filters`) | function | P1 | _to author_ |
| E2E-ORG-015 | Column sort toggles (`Sort` + `SortDescending`) | function | P2 | _to author_ |
| E2E-ORG-016 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-ORG-017 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-ORG-018 | Delete confirmation gate — View/Delete + SimfConfirm (Cancel = no DELETE; confirm = one DELETE) (D-353) | error | P0 | _to author_ |
| E2E-ORG-019 | Excel export: toolbar Export downloads an `.xlsx` of the filtered grid (whole grid vs selected rows) (D-356) | happy | P1 | _to author_ |
| E2E-ORG-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-ORG-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-ORG-001 — Golden round-trip

```gherkin
Feature: Organisations lookup CRUD round-trip
  As an Administrator
  I want to create, search, edit and deactivate a Saudi-companies lookup row
  So that the visitor "الجهة" picker stays accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (TOTP from Get-Totp)
  And they have landed on /admin/organisations
  And the grid has loaded (POST /account/api/admin/organisations/list returned 200)

Scenario: Create, search, edit, deactivate one organisation
  Given the grid summary reads "Showing 1–{N} of {N}" (or the SimfEmptyState if empty)
  When the administrator clicks "New organisation"
  Then the Add modal opens titled "Add organisation"
  And it shows nine inputs: Name (Arabic), Name (English), Commercial registration,
      Sector, City, Phone, Email, Website, and the "Active" checkbox (ticked)
  When they fill Name (Arabic)="شركة البحرية للأنظمة"
  And they fill Name (English)="Naval Systems Co."
  And they fill Commercial registration="1010567890"
  And they fill Sector="Defence"
  And they fill City="Riyadh"
  And they fill Phone="+966112345678"
  And they fill Email="info@navalsystems.sa"
  And they fill Website="https://navalsystems.sa"
  And they click "Save"
  Then the BFF forwards POST /account/api/admin/organisations and the API returns 200
  And the modal closes
  And a green SimfAlert reads "Organisation saved." / "تم حفظ الجهة."
  And a row exists with Name (Arabic)="شركة البحرية للأنظمة", Name (English)="Naval Systems Co.",
      CR="1010567890", Sector="Defence", City="Riyadh" and the Active column showing "✓"

  When the administrator types "1010567890" into the Search box
  And clicks the "Search" button
  Then POST /account/api/admin/organisations/list fires with Search="1010567890" and Skip=0
  And the grid shows only the matching row

  When the administrator clicks the row's Edit (pencil) action in the grid
  Then GET /account/api/admin/organisations/{id} fires (the summary omits Phone/Email/Website)
  And the Edit modal opens titled "Edit organisation" with every field pre-filled,
      including Phone="+966112345678", Email="info@navalsystems.sa", Website="https://navalsystems.sa"
  When they change City to "Jeddah"
  And they click "Save"
  Then the BFF forwards PUT /account/api/admin/organisations/{id} and the API returns 200
  And the modal closes
  And a green toast reads "Organisation saved." / "تم حفظ الجهة."
  And the row's City column reads "Jeddah"

  When the administrator clicks the row's Delete (trash) action in the grid
  Then the View/Delete form opens (in CrudShell — dialog by default) showing the
      row's read-only details (incl. Phone/Email/Website) and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog appears reading
      "Deactivate “شركة البحرية للأنظمة”? It will be removed from the public lookup."
      / "تعطيل «شركة البحرية للأنظمة»؟ ستُزال من قائمة البحث العامة."
  When they click the confirm "Deactivate" button
  Then DELETE /account/api/admin/organisations/{id} fires and the API returns 200
  And a green toast reads "Organisation deactivated." / "تم تعطيل الجهة."
  And on reload the row no longer shows in the active-default grid
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-organisations-golden-before.png`
- Screenshot after (add): `docs/screenshots/cp-admin-organisations-golden-add.png`
- Screenshot after (edit modal prefill): `docs/screenshots/cp-admin-organisations-golden-edit.png`
- Screenshot after (deactivated): `docs/screenshots/cp-admin-organisations-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/organisations/*` call returns 200
- Audit rows: `OperationLog`/audit rows with `Event = 'organisation.created'`, then
  `'organisation.updated'`, then `'organisation.deactivated'`, each carrying the actor's id

### E2E-ORG-002 — Empty list / no-match search

```gherkin
Scenario: Empty grid renders SimfEmptyState
  Given the database has no active Organisation rows (or the search matches none)
  When the administrator opens /admin/organisations
  Then POST /account/api/admin/organisations/list returns 200 with an empty Items list
  And the grid body renders the SimfEmptyState titled "No organisations found" / "لا توجد جهات"
  And no error SimfAlert appears
  And the toolbar still shows the "New organisation" and "Import Excel" buttons
```

### E2E-ORG-003 — Search reloads grid server-side

```gherkin
Scenario: Search box drives GridQuery.Search and resets to the first page
  Given the grid shows multiple organisations spanning more than one page
  And the administrator has paged past the first page
  When they type "Riyadh" into the Search field
  And click the "Search" button
  Then POST /account/api/admin/organisations/list fires with Search="Riyadh" and Skip=0
  And only rows matching name / CR / sector / city by LIKE are shown
  When they clear the Search field and click "Search" again
  Then the request fires with Search=null and the full grid returns
```

### E2E-ORG-004 — Excel import (golden)

```gherkin
Scenario: Import a government .xlsx and see the row tallies
  Given the administrator is on /admin/organisations
  When they click "Import Excel"
  Then the Import modal opens titled "Import organisations from Excel"
  And it shows the hint "Upload a government .xlsx sheet. Existing rows are matched
      by commercial registration and updated; new rows are inserted."
  And the file input accepts ".xlsx" only
  And the "Upload" button is disabled until a file is picked
  When they pick a valid workbook "organisations-seed.xlsx" with 3 data rows
  Then the picked file name appears under the input
  And the "Upload" button enables
  When they click "Upload"
  Then POST /account/api/admin/organisations/import fires (multipart, field "file") and returns 200
  And the result panel reads "Rows read: 3 · Inserted: 3 · Updated: 0 · Skipped: 0"
  And a green toast reads "Import complete — 3 inserted, 0 updated, 0 skipped."
      / "اكتمل الاستيراد — 3 مُضافة، 0 مُحدَّثة، 0 متجاهَلة."
  And the grid reloads to include the imported rows
  When they re-upload the same workbook
  Then the result panel reports "Inserted: 0 · Updated: 3" (upsert by commercial registration)
  When they upload a workbook whose row 2 has a blank Arabic name
  Then that row appears in the result panel's error list as "Row 2: Arabic name is required."
      and is counted under "Skipped"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-organisations-import-result.png`
- Network: `/account/api/admin/organisations/import` returns 200 with the `OrganisationImportResult` envelope
- Audit row: `Event = 'organisation.imported'` with `Detail` carrying `read=…; inserted=…; updated=…; skipped=…`

### E2E-ORG-005 — Auth gate (page permission)

```gherkin
Scenario: Admin lacking Organisations.View is denied
  Given a signed-in admin whose roles do not grant Organisations.View
  When they navigate to /admin/organisations
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/organisations/list request fires
```

### E2E-ORG-006 — Action gate (Create permission)

```gherkin
Scenario: Admin with View but not Create cannot add
  Given a signed-in admin whose roles grant Organisations.View but not Organisations.Create
  When they open /admin/organisations
  Then the grid loads normally
  But the "New organisation" button is not rendered (AuthorizedAction hides it)
  And if Organisations.Import is also missing, the "Import Excel" button is hidden
  And if Organisations.Edit / .Delete are missing, the per-row Edit (pencil) /
      Delete (trash) icon actions in the grid are hidden
```

### E2E-ORG-007 — Client validation (blank Arabic name)

```gherkin
Scenario: Blank Arabic name shows a bilingual error before any request
  Given the Add modal is open
  When the administrator leaves Name (Arabic) blank
  And clicks "Save"
  Then a red SimfAlert appears reading "Arabic name is required." / "الاسم بالعربية مطلوب."
  And the modal stays open
  And no POST /account/api/admin/organisations request fires
```

### E2E-ORG-008 — Server validation (Arabic name too long)

```gherkin
Scenario: Arabic name over 256 characters returns 400 ORGANISATION_INVALID
  Given the Add modal is open
  When the administrator fills Name (Arabic) with 257 characters
  And clicks "Save"
  Then POST /account/api/admin/organisations is forwarded
  And the API returns HTTP 400 with ApiResult.Error.Code = "ORGANISATION_INVALID"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture()
      "Organisation Arabic name must be between 1 and 256 characters."
      / "يجب أن يتراوح طول الاسم العربي للمنظمة بين 1 و 256 حرفاً."
```

> Note: the UI field carries `MaxLength="256"`, so reproducing this typically requires
> programmatically setting the value past the cap (e.g. via `evaluate_script`) to exercise
> the server guard rather than relying on keyboard entry.

### E2E-ORG-009 — Conflict (duplicate commercial registration)

```gherkin
Scenario: Duplicate commercial registration returns 409 ORGANISATION_INVALID
  Given an organisation with Commercial registration="1010567890" already exists
  When the administrator opens the Add modal
  And fills Name (Arabic)="منشأة مكررة" and Commercial registration="1010567890"
  And clicks "Save"
  Then the BFF forwards POST /admin/organisations
  And the API returns HTTP 409 with ApiResult.Error.Code = "ORGANISATION_INVALID"
  And the modal stays open
  And the error toast reads the bilingual MessageForCurrentCulture()
      "An organisation with commercial registration '1010567890' already exists."
      / "توجد منظمة بالسجل التجاري '1010567890' بالفعل."
```

### E2E-ORG-010 — Delete confirm cancelled

```gherkin
Scenario: Cancelling the SimfConfirm does not deactivate
  Given the grid shows at least one organisation
  When the administrator clicks the row's Delete (trash) action in the grid
  Then the View/Delete form opens (CrudShell) with a red "Deactivate" button
  When they click "Deactivate" and then click "Cancel" on the SimfConfirm dialog
  Then no DELETE /account/api/admin/organisations/{id} request fires
  And the row remains unchanged and active
  And no toast appears
```

### E2E-ORG-011 — Import rejects an invalid file

```gherkin
Scenario: A file that is not a valid .xlsx is rejected
  Given the Import modal is open
  And the administrator has picked a file whose bytes are not a ZIP/xlsx workbook
  When they click "Upload"
  Then POST /account/api/admin/organisations/import returns a non-success ApiResult
  And the API responds with Error.Code = "ORGANISATION_IMPORT_FAILED" (or VALIDATION_FAILED)
  And a red toast reads "Excel import failed." / the bilingual server message
  And no rows are inserted

Scenario: An oversized workbook (> 5 MB) is rejected
  Given the Import modal is open with a 6 MB .xlsx picked
  When they click "Upload"
  Then the API returns HTTP 413 with Error.Code = "ORGANISATION_IMPORT_FAILED"
  And the error toast surfaces "The Excel file is too large. The maximum is 5 MB."
      / "ملف Excel كبير جدًا. الحد الأقصى 5 ميغابايت."
```

### E2E-ORG-012 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is forced to return 500 on /admin/organisations/list (e.g. DB unavailable)
  When the administrator opens /admin/organisations
  Then the grid shows the loading text "Loading…" / "جارٍ تحميل الجهات…"
  And then a red toast reads "Could not load organisations." / fallback bilingual message
  And no rows render
```

### E2E-ORG-013 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page, grid and both modals
  Given the administrator is on /admin/organisations in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الجهات"
  And the toolbar Search button reads "بحث" and the actions appear in reverse order
  And the grid column headers read "الاسم (عربي)", "الاسم (إنجليزي)", "السجل التجاري",
      "القطاع", "المدينة", "نشط"

  When they click "إضافة جهة"
  Then the Add modal opens in RTL titled "إضافة جهة"
  And the field labels render in Arabic (e.g. "الاسم (عربي)")
  And the footer "إلغاء" / "حفظ" buttons appear in reverse order

  When they click "استيراد Excel"
  Then the Import modal opens in RTL with the Arabic hint and "رفع" upload button
```

### E2E-ORG-014 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a per-column filter input reloads the grid server-side
  Given the grid shows multiple organisations
  And every data column except "Active" carries a per-column filter input
      (the columns "Name (Arabic)", "Name (English)", "CR", "Sector" and "City"
       are Filterable; "Active" is not)
  When the administrator types "Riyadh" into the "Filter column City" input
  Then after the ~300 ms debounce POST /account/api/admin/organisations/list fires
      with GridQuery.Filters["city"]="Riyadh" and Skip reset to 0
  And the grid narrows to only the rows whose City matches "Riyadh"
  And the row-selection (select-all / row checkboxes) is cleared

  When they also type "Defence" into the "Filter column Sector" input
  Then the next /list request carries both Filters["city"]="Riyadh"
      and Filters["sector"]="Defence" (the filters accumulate)
  When they clear the "Filter column City" input
  Then the request fires again with Filters no longer carrying the "city" key
      and the broader result set returns
```

> Note: these per-column filter inputs are independent of the toolbar Search box —
> Search drives `GridQuery.Search` (LIKE across name / CR / sector / city), while a
> column filter drives `GridQuery.Filters["{key}"]` for that one column. Both reset
> `Skip` to 0.

### E2E-ORG-015 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles ascending / descending
  Given the grid shows multiple organisations
  And the "Name (Arabic)", "City" and "Active" headers are sortable
      (the "Name (English)", "CR" and "Sector" headers are not)
  When the administrator clicks the "City" column header
  Then POST /account/api/admin/organisations/list fires with Sort="city",
      SortDescending=false and Skip reset to 0
  And the header renders aria-sort="ascending"
  When they click the "City" header again
  Then the request fires with Sort="city", SortDescending=true
      and the header renders aria-sort="descending"
  When they instead click the "Name (Arabic)" header
  Then the request fires with Sort="name", SortDescending=false
      (switching column resets the direction to ascending)
```

### E2E-ORG-016 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/organisations with the default "dialog" presentation
  And the grid toolbar (CustomToolbar) shows the "Open as full page" toggle (maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.organisations" holds {"v":1,"presentation":"page"}
  When they reload /admin/organisations
  Then OnInitializedAsync reads the preference back (Prefs.GetPresentationAsync("organisations"))
  And the toggle still reads "Open as dialog"
  And opening "New organisation" now renders the full-page frame (not a popup)
```

### E2E-ORG-017 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page"
  When the administrator clicks "New organisation"
  Then the grid + SimfBanner are hidden (GridHidden) and the CrudShell renders the
      OrganisationAddEdit form as a full page (title + close header, no modal backdrop)
  When they fill Name (Arabic)="هيئة الموانئ" and click "Save"
  Then PUT/POST /account/api/admin/organisations returns 200
  And the page frame closes and the grid re-appears with the new row and the
      green "Organisation saved." / "تم حفظ الجهة." toast
  When they click the row's Edit (pencil) action and then the frame's close (X) button
  Then GET /account/api/admin/organisations/{id} fired to prefill, the form closes,
      and the grid re-appears unchanged (no PUT)
  When they click the row's Details (eye) action
  Then the OrganisationViewDelete form opens read-only as a full page (no "Deactivate" button)
  When they click "Close"
  Then the form closes and the grid re-appears
```

### E2E-ORG-018 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Deactivate requires explicit SimfConfirm — Cancel skips, confirm fires exactly one DELETE
  Given the administrator is on /admin/organisations with at least one organisation
      (e.g. Name (Arabic)="شركة البحرية للأنظمة")
  When they click the row's Delete (trash) action in the grid
  Then GET /account/api/admin/organisations/{id} fires to load the full detail
  And the OrganisationViewDelete form opens in CrudShell showing the read-only details
      (incl. Phone/Email/Website) and a red "Deactivate" button — NOT a native window.confirm()
  When they click "Deactivate"
  Then a SimfConfirm dialog appears titled "Deactivate organisation" / "تعطيل الجهة"
  And its message reads "Deactivate “شركة البحرية للأنظمة”? It will be removed from the public lookup."
      / "تعطيل «شركة البحرية للأنظمة»؟ ستُزال من قائمة البحث العامة."
  When they click "Cancel" on the SimfConfirm
  Then no DELETE request fires and the form stays open with the row unchanged
  When they click "Deactivate" again and then the confirm "Deactivate" button
  Then exactly one DELETE /account/api/admin/organisations/{id} fires and returns 200
  And the form closes, the grid reloads, and a green toast reads
      "Organisation deactivated." / "تم تعطيل الجهة."
  And the row's Active pill turns grey "Inactive"
```

### E2E-ORG-019 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid to an XLSX workbook (whole grid vs selected rows)
  Given the administrator is on /admin/organisations with at least two organisations
  When they click the toolbar "Export" / "تصدير" action with no rows selected
  Then a POST /account/api/admin/organisations/export fires carrying
      AdminGridExportRequest with an empty Ids list and the current GridQuery
      (the request sends Query only when no rows are selected)
  And the browser saves a file named simf-organisations-{timestamp}.xlsx
  And the workbook's "Organisations" sheet has the header row
      NameAr | NameEn | CommercialRegistration | Sector | City | IsActive
  And the rows match the current filtered / searched grid
  When they instead select two rows (row checkboxes) then click "Export"
  Then the request carries those two Ids and a null Query
  And the workbook contains exactly those two organisations
```

> Note: Organisations is **export-only** for the generic D-356 grid Excel
> (`ExportOrganisationsEndpoint`, gated by `Organisations.Export`); there is no
> generic grid-import endpoint. The page's separate "Import Excel" button drives
> the bespoke government-workbook upload modal already covered by E2E-ORG-004
> (golden import) and E2E-ORG-011 (bad-file rejection). The export uses
> `simfAccount.downloadXlsx` (direct browser download), not a `CrudGridExcel`
> component. The API caps export at 5000 rows.

---

## Implementation notes

- **Manual smoke is canonical today.** Until Playwright is adopted, the canonical
  run is a Chrome DevTools MCP session: sign in per the Auth setup, walk each
  scenario, and capture screenshots into `docs/screenshots/cp-admin-organisations-*.png`.
  Keep the Gherkin runner-agnostic so each scenario copies cleanly into a
  `.feature` + step-definition class under a future `tests/SIMF.E2E.Tests/`.
- **Lower-layer API coverage already exists** at
  [`tests/SIMF.Api.Tests/OrganisationTests.cs`](../../../tests/SIMF.Api.Tests/OrganisationTests.cs):
  `Create_then_get_then_list_contains_the_organisation`,
  `Deactivate_marks_the_organisation_inactive`,
  `Non_admin_caller_is_forbidden_on_create`,
  `Import_inserts_new_rows_and_re_importing_upserts_by_commercial_registration`, and
  `Public_picker_search_returns_the_matching_organisation`. These exercise the same
  service surface (`AdminOrganisationService`) without a browser; E2E-ORG-001/004/005/009
  are the browser-level mirror. During the transition keep both layers.
- **Error-code source of truth:** `ErrorCodes.OrganisationInvalid = "ORGANISATION_INVALID"`
  (400 validation + 409 duplicate CR), `ORGANISATION_NOT_FOUND` (404),
  `ORGANISATION_IMPORT_FAILED` (400/413). Audit event keys:
  `organisation.created` / `.updated` / `.deactivated` / `.imported`.
- **No reference page doc yet** — `docs/pages/cp/admin-organisations.md` is not authored;
  the `OrganisationsList.razor` header comment + this catalogue are the working description.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle; added E2E-ORG-016..019, corrected the now-stale native-`confirm()` delete copy in ORG-001/ORG-010 to the shipped CrudShell + SimfConfirm gate).
