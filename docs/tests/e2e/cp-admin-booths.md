# E2E test catalogue — Exhibition Booths CRUD (`/admin/booths`)

| | |
|--|--|
| **Page** | [`cp/admin-booths.md`](../../pages/cp/admin-booths.md) |
| **Route** | `/admin/booths` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Surface map (verified against source).** Page:
> `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BoothsList.razor`
> (`@page "/admin/booths"`, `@attribute [RequirePermission(PermissionCatalog.Booths.View)]`).
> Required permission to load: **`Booths.View`**; the grid is roles-only gated and
> `Administrator = "*"` (wildcard) always passes. The page is a single
> `SimfDataGrid` plus the reusable **CrudShell** that frames a `BoothsAddEdit`
> (Add/Edit) form, a `BoothsViewDelete` (View/Delete) form, and one
> `<CrudGridExcel @ref="_excel" Resource="booths" …>` host (D-353 + D-356 — the
> old inline `SimfModal` form + native `confirm()` delete are gone). The grid
> (D-256 raw-table→grid conversion) carries per-column filter inputs, sortable
> headers, select-all/row checkboxes (`Multiselect="true"`, cosmetic — there is
> **no** bulk-action toolbar), quiet icon row-actions (Add / Edit pencil /
> Details / Delete trash via `OnAdd`/`OnEditOne`/`OnDetailsOne`/`OnDeleteOne`),
> a toolbar **Export** + **Import** action (`OnExport`/`OnImport`), a
> `<CrudPresentationToggle PageKey="booths" @bind-Value="_presentation" />` in
> the `<CustomToolbar>`, and a pager loading `Top=20` rows per page. BFF
> passthroughs live in
> `AccountEndpoints.cs` lines 2089–2122; the API lives in
> `src/Backend/SIMF.Api/Endpoints/Admin/BoothEndpoints.cs`; the service +
> validation in `src/Backend/SIMF.Infrastructure/Exhibition/AdminBoothService.cs`.
>
> **Every action on the page (from the `.razor`, post D-353/D-356):**
> 1. `Add booth` grid toolbar button → `OnAddAsync` opens the empty
>    `BoothsAddEdit` form inside CrudShell.
> 2. Per-row Edit (pencil) icon action → `OnEditAsync` GETs the full detail then
>    opens the pre-filled `BoothsAddEdit` form
>    (`GET /account/api/admin/booths/{id}`).
> 3. Per-row Details icon action → `OnDetailsAsync` GETs the detail and opens
>    `BoothsViewDelete` read-only (no Delete button).
> 4. Per-row Delete (trash) icon action → `OnDeleteAsync` GETs the detail and
>    opens `BoothsViewDelete` with a red **Delete** button gated by a
>    `SimfConfirm` dialog (NOT a native `confirm()`); confirm →
>    `simfAccount.deleteJson` `DELETE /account/api/admin/booths/{id}`.
> 5. `BoothsAddEdit` `Save` → `HandleSubmitAsync`: client guard on
>    Code/NameEn/NameAr (`Admin.Booths.Required`), then
>    `POST /account/api/admin/booths` (create) or `PUT .../{id}` (edit);
>    `OnSuccess` closes the shell + `Admin.Booths.Saved` toast.
> 6. `Cancel` / shell close (`CloseLabel`) → discards the form (`CloseForm`).
> 7. Toolbar **Export** → `OnExportAsync` → `_excel.ExportAsync(ids, _query)` →
>    `POST /account/api/admin/booths/export` (D-356).
> 8. Toolbar **Import** → `OnImportAsync` → `_excel.TriggerImportAsync()` (file
>    picker `#booths-import-input`, `accept=".xlsx"`) →
>    `POST /account/api/admin/booths/import` (D-356).
> 9. `<CrudPresentationToggle>` in the `<CustomToolbar>` flips
>    `_presentation` between dialog (popup) and full page, persisted in
>    localStorage `simf.cp.prefs.booths` via `CpPreferences` (D-353).
> 10. Form fields (`BoothsAddEdit`): Code, Name (English), Name (Arabic),
>    **Exhibitor company** (`<select>`, active `Exhibitor` companies only),
>    Booth officer name / phone / email, an optional **Contact** link
>    (`ContactPicker`, SIMF-FDS-014 / D-283), Sector (English/Arabic),
>    Description (English/Arabic) textareas, **Hall** (`<select>`, active halls
>    only), Map X / Map Y numeric inputs, and **Active** checkbox (Edit only).
>
> **Backing endpoints / error codes (verified):**
> - `POST /account/api/admin/booths/list` → `ApiResult<GridPage<AdminBoothSummary>>` (`Booths.View`)
> - `GET /account/api/admin/booths/{id}` → `ApiResult<AdminBoothDetail>` (`Booths.View`; 404 `BOOTH_NOT_FOUND`)
> - `POST /account/api/admin/booths` → create (`Booths.Create`; rate-limit policy `auth`)
> - `PUT /account/api/admin/booths/{id}` → update (`Booths.Edit`; rate-limit policy `auth`)
> - `DELETE /account/api/admin/booths/{id}` → soft-delete (`Booths.Delete`; rate-limit policy `auth`)
> - `POST /account/api/admin/booths/export` → `AdminGridExportRequest { Ids, Query }`
>   → `.xlsx` download (`Booths.Export`); selected `Ids` win, else `Query`; 5000-row cap (D-356).
> - `POST /account/api/admin/booths/import` → multipart `.xlsx` upload (`Booths.Import`);
>   insert-only; per-row `Created/Updated/Skipped` + error list; 5000-row cap; non-`.xlsx`
>   (ZIP-magic + 5 MB gate) or wrong sheet → HTTP 400 (D-356).
> - Error codes: `BOOTH_INVALID` (400), `BOOTH_NOT_FOUND` (404),
>   `BOOTH_CODE_DUPLICATE` (409). Server validation (the source of truth):
>   Code 2–16 chars (upper-cased + trimmed), NameEn/NameAr 1–128, officer
>   email must contain `@`, HallId must be an active hall, ExhibitorId must be
>   an **active Exhibitor** company.
> - Audit events: `Booth.Created`, `Booth.Updated`, `Booth.Deactivated`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-BTH-001 | Golden path — Add → Edit → Deactivate one booth round-trip | happy | P0 | _to author_ |
| E2E-BTH-002 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-BTH-003 | Add booth — open modal, fill all fields, link company + hall + map position | happy | P1 | _to author_ |
| E2E-BTH-004 | Edit booth — pre-fill from detail GET, change fields, save | happy | P1 | _to author_ |
| E2E-BTH-005 | Delete booth — confirm dialog → soft-delete, row drops from list | happy | P1 | _to author_ |
| E2E-BTH-006 | Cancel modal — discards edits, no request fires | happy | P2 | _to author_ |
| E2E-BTH-007 | Exhibitor company dropdown lists only active Exhibitor companies | happy | P1 | _to author_ |
| E2E-BTH-008 | Hall dropdown lists only active halls; grid resolves the hall name | happy | P2 | _to author_ |
| E2E-BTH-009 | Auth gate — signed-in admin lacking `Booths.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-BTH-010 | Client validation — blank Code/Name → in-modal error, no POST | error | P1 | _to author_ |
| E2E-BTH-011 | Server validation — Code length / bad officer email → 400 `BOOTH_INVALID` | error | P1 | _to author_ |
| E2E-BTH-012 | Conflict — duplicate Code → 409 `BOOTH_CODE_DUPLICATE` | error | P1 | _to author_ |
| E2E-BTH-013 | Not found — edit a deleted booth id → 404 `BOOTH_NOT_FOUND` | error | P2 | _to author_ |
| E2E-BTH-014 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-BTH-015 | RTL / Arabic render mirrors page + Add modal | i18n | P1 | _to author_ |
| E2E-BTH-016 | Per-column filter narrows the grid (Code / Sector EN) | happy | P1 | _to author_ |
| E2E-BTH-017 | Column sort toggles (Code header) | happy | P2 | _to author_ |
| E2E-BTH-018 | Presentation toggle persists across reload (Page/Popup) (D-353) | happy | P1 | _to author_ |
| E2E-BTH-019 | Full-page mode round-trip — Add/Edit/View take over the content area (D-353) | happy | P1 | _to author_ |
| E2E-BTH-020 | Delete confirmation gate — CrudShell ViewDelete + SimfConfirm (D-353) | error | P0 | _to author_ |
| E2E-BTH-021 | Excel export — whole filtered grid vs selected rows (D-356) | happy | P1 | _to author_ |
| E2E-BTH-022 | Excel import — workbook → per-row outcome + FK resolution (D-356) | happy | P1 | _to author_ |
| E2E-BTH-023 | Excel import rejection — non-.xlsx / wrong sheet → 400, nothing created (D-356) | i18n | P1 | _to author_ |

## Scenarios

### E2E-BTH-001 — Golden path (Add → Edit → Deactivate round-trip)

```gherkin
Feature: Exhibition Booths CRUD round-trip
  As an Administrator
  I want to manage the public exhibition booth list + 2D venue map
  So that the visitor-facing exhibition page and map stay accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the Website is reachable on http://localhost:5115
  And an Administrator signs in as superadmin@zagali-ict.com using a TOTP from the Get-Totp helper
  And they have landed on /admin/booths
  And the page issued POST /account/api/admin/booths/list and rendered the grid (or the SimfEmptyState)

Scenario: Create, edit, then deactivate one booth
  Given the grid currently shows {N} rows
  When the administrator clicks "Add booth"
  Then the Add modal opens titled "Add booth"
  And it shows the fields: Code, Name (English), Name (Arabic), Exhibitor company,
      Booth officer name, Booth officer phone, Booth officer email,
      Sector (English), Sector (Arabic), Description (English), Description (Arabic),
      Hall, Map X position, Map Y position, and an Active checkbox

  When they fill Code="A-12"
  And they fill Name (English)="Naval Systems Pavilion"
  And they fill Name (Arabic)="جناح الأنظمة البحرية"
  And they select an active Exhibitor company in "Exhibitor company"
  And they fill Booth officer name="Capt. Rashed Al-Otaibi"
  And they fill Booth officer phone="+966500000000"
  And they fill Booth officer email="officer@example.com"
  And they fill Sector (English)="Naval Defence"
  And they fill Sector (Arabic)="الدفاع البحري"
  And they fill Map X position="120"
  And they fill Map Y position="240"
  And they click "Save"
  Then the BFF forwards POST /account/api/admin/booths and the API returns HTTP 200 with Success=true
  And the modal closes
  And a green SimfAlert toast reads "Booth saved." / "تم حفظ الجناح."
  And the grid reloads via POST /account/api/admin/booths/list and shows {N + 1} rows
  And a row exists with Code="A-12" (stored upper-cased) and Name (English)="Naval Systems Pavilion" and the Active column shows "✓"

  When the administrator clicks the "A-12" row's Edit (pencil) icon action
  Then the BFF forwards GET /account/api/admin/booths/{id} and returns HTTP 200
  And the Edit modal opens titled "Edit booth" with every field pre-filled (Code, both names, company, officer, sector, map X/Y, Active ticked)
  When they change Sector (English) to "Maritime Logistics"
  And they click "Save"
  Then the BFF forwards PUT /account/api/admin/booths/{id} and returns HTTP 200
  And the modal closes and the toast reads "Booth saved." / "تم حفظ الجناح."
  And reopening Edit shows Sector (English)="Maritime Logistics"

  When the administrator clicks the "A-12" row's Delete (trash) icon action
  Then the BoothsViewDelete form opens (CrudShell) showing the booth's read-only details and a red "Delete" button
  When they click "Delete"
  Then a SimfConfirm dialog appears reading "Delete booth “A-12”? It will be removed from the public exhibition list and the venue map immediately." / "هل تريد حذف الجناح ”A-12“؟ ستتم إزالته من قائمة المعرض العامة وخريطة الموقع فوراً."
  When they confirm
  Then the BFF forwards DELETE /account/api/admin/booths/{id} and returns HTTP 200
  And a green toast reads "Booth deleted." / "تم حذف الجناح."
  And the grid reloads and the "A-12" row no longer appears (soft-deleted: IsActive=false, filtered from the public list)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-booths-golden-before.png`
- Screenshot add-modal: `docs/screenshots/cp-admin-booths-add-modal.png`
- Screenshot edit-modal: `docs/screenshots/cp-admin-booths-edit-modal.png`
- Screenshot after: `docs/screenshots/cp-admin-booths-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/booths/*`, `/account/api/admin/halls/list`,
  and `/account/api/admin/exhibitors/list` call returns 200
- Audit rows: one `Booth.Created`, one `Booth.Updated`, one `Booth.Deactivated`
  row with the actor's id (the signed-in admin)

### E2E-BTH-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Booth rows
  When the administrator opens /admin/booths
  Then POST /account/api/admin/booths/list returns Total=0 with an empty Items array
  And the grid body renders the SimfEmptyState component titled "No booths yet" / "لا توجد أجنحة بعد"
  And the "Add booth" toolbar button is still visible
  And no error toast appears
```

### E2E-BTH-003 — Add booth (full field set, with company + hall + map position)

```gherkin
Scenario: Add a fully-populated booth
  Given the administrator is on /admin/booths
  And at least one active Exhibitor company and one active Hall exist
  When they click "Add booth"
  Then the Add modal opens with an empty form (Active checkbox ticked by default)
  When they fill Code="B-07"
  And they fill Name (English)="Sea Drones Hub"
  And they fill Name (Arabic)="مركز الطائرات البحرية المسيّرة"
  And they choose an exhibitor in "Exhibitor company"
  And they fill Description (English)="Showcasing autonomous surface vessels."
  And they fill Description (Arabic)="عرض المركبات السطحية ذاتية القيادة."
  And they choose a hall in "Hall"
  And they fill Map X position="55.5"
  And they fill Map Y position="88.25"
  And they click "Save"
  Then POST /account/api/admin/booths is sent with ExhibitorId, HallId, and MapX/MapY populated
  And the API returns HTTP 200 and the grid shows the new "B-07" row
  And the row's Company column resolves the chosen company's English name
  And the row's Hall column resolves the chosen hall's name
  And the toast reads "Booth saved." / "تم حفظ الجناح."
```

### E2E-BTH-004 — Edit booth (pre-fill from detail GET)

```gherkin
Scenario: Edit pre-fills from the detail endpoint and persists a change
  Given a booth with Code="C-01" exists
  When the administrator clicks the "C-01" row's Edit (pencil) icon action
  Then the BFF forwards GET /account/api/admin/booths/{id}
  And the Edit modal opens with Code, Name (English), Name (Arabic), Exhibitor company,
      Booth officer fields, Sector, Description, Hall, Map X/Y, and the Active checkbox pre-filled from AdminBoothDetail
  When they untick the Active checkbox
  And they click "Save"
  Then PUT /account/api/admin/booths/{id} is sent with IsActive=false
  And the API returns HTTP 200 and the modal closes
  And the "C-01" row's Active column changes from "✓" to "—"
  And the toast reads "Booth saved." / "تم حفظ الجناح."
```

### E2E-BTH-005 — Delete booth (confirm → soft-delete)

```gherkin
Scenario: Deleting a booth requires confirmation then soft-deletes it
  Given a booth with Code="D-09" exists in the grid
  When the administrator clicks the "D-09" row's Delete (trash) icon action
  Then the BoothsViewDelete form opens (CrudShell) with a red "Delete" button
  When they click "Delete"
  Then a SimfConfirm dialog appears with the bilingual delete-confirm copy naming "D-09"
  When they click "Cancel"
  Then no DELETE request fires and the row stays in the grid

  When they re-open the "D-09" row's Delete form, click "Delete", and confirm
  Then DELETE /account/api/admin/booths/{id} is sent and returns HTTP 200
  And the toast reads "Booth deleted." / "تم حذف الجناح."
  And the grid reloads and the "D-09" row is gone
  And an audit row Booth.Deactivated is written
```

### E2E-BTH-006 — Cancel modal discards edits

```gherkin
Scenario: Cancelling the modal makes no change
  Given the administrator opened the Add modal and typed Code="X-99"
  When they click "Cancel"
  Then the modal closes
  And no POST /account/api/admin/booths request fires
  And the grid row count is unchanged
  And reopening "Add booth" shows an empty form (the prior input is discarded)
```

### E2E-BTH-007 — Exhibitor company dropdown is filtered

```gherkin
Scenario: Company dropdown only offers active Exhibitor companies
  Given an active Exhibitor company "Maritime Tech LLC" exists
  And an active Sponsor company "Gulf Bank" exists
  And an inactive Exhibitor company "Old Yard Co" exists
  When the administrator opens the Add modal and expands "Exhibitor company"
  Then "Maritime Tech LLC" appears in the list (label "{NameEn} — {NameAr}")
  And "— No company —" is the first option
  And "Gulf Bank" does NOT appear (Sponsor type filtered client-side)
  And "Old Yard Co" does NOT appear (inactive filtered client-side)
```

### E2E-BTH-008 — Hall dropdown filtered + grid resolves hall name

```gherkin
Scenario: Hall dropdown only offers active halls and the grid resolves the name
  Given an active hall "Hall A" exists and an inactive hall "Hall Z" exists
  When the administrator opens the Add modal and expands "Hall"
  Then "— No hall —" is the first option and "Hall A" appears
  And "Hall Z" does NOT appear (inactive filtered client-side)
  When they create a booth assigned to "Hall A"
  Then the booth's row Hall column reads "Hall A" (resolved from the cached halls list)
```

### E2E-BTH-009 — Auth gate

```gherkin
Scenario: A signed-in admin lacking Booths.View is denied
  Given a signed-in admin whose role does NOT grant the "Booths.View" permission
    (and is not the wildcard Administrator)
  When they navigate to /admin/booths
  Then they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/booths/list request fires
  And the "Module.Booths" item is hidden from the nav rail (RequiredPermission = Booths.View)
```

### E2E-BTH-010 — Client validation (blank required fields)

```gherkin
Scenario: Submitting with a blank Code or name shows an in-modal error, no POST
  Given the Add modal is open
  When the administrator leaves Code blank (or Name English / Name Arabic blank)
  And clicks "Save"
  Then a red SimfAlert toast appears reading
      "Code and both names (English and Arabic) are required." /
      "الرمز والاسمان (الإنجليزي والعربي) مطلوبة."
  And the modal stays open
  And no POST /account/api/admin/booths request fires (client guard short-circuits)
```

### E2E-BTH-011 — Server validation (400 BOOTH_INVALID)

```gherkin
Scenario: Server rejects a too-short Code or a malformed officer email
  Given the Add modal is open with Name (English)/Name (Arabic) filled
  When the administrator fills Code="A" (1 char, below the 2–16 range)
  And clicks "Save"
  Then POST /account/api/admin/booths returns HTTP 400 with ApiResult.Error.Code = "BOOTH_INVALID"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture()
      "Booth code must be between 2 and 16 characters." /
      "يجب أن يتراوح طول رمز الجناح بين 2 و 16 حرفاً."

  When instead they fill a valid Code="A-01" but Booth officer email="not-an-email"
  And click "Save"
  Then the API returns HTTP 400 BOOTH_INVALID reading
      "Booth officer email is not a valid email address." / "بريد مسؤول الجناح غير صالح."
```

### E2E-BTH-012 — Conflict (duplicate Code → 409)

```gherkin
Scenario: A duplicate booth Code returns 409 with a bilingual server message
  Given a booth with Code="A-12" already exists
  When the administrator opens the Add modal
  And fills Code="a-12" (the server upper-cases + trims to "A-12") + Name (English) + Name (Arabic)
  And clicks "Save"
  Then POST /account/api/admin/booths returns HTTP 409 with ApiResult.Error.Code = "BOOTH_CODE_DUPLICATE"
  And the modal stays open
  And the error toast reads "A booth with code 'A-12' already exists." /
      "يوجد جناح بالرمز 'A-12' بالفعل."
  And the grid row count is unchanged
```

### E2E-BTH-013 — Not found (edit/delete a stale id → 404)

```gherkin
Scenario: Editing a booth that was deleted in another session returns 404
  Given the grid shows a booth row whose id was just hard-removed server-side
  When the administrator clicks that row's Edit (pencil) icon action
  Then GET /account/api/admin/booths/{id} returns HTTP 404 with ApiResult.Error.Code = "BOOTH_NOT_FOUND"
  And the modal does NOT open
  And a red toast surfaces the bilingual message
      "The booth was not found." / "لم يتم العثور على الجناح."
```

### E2E-BTH-014 — Server 500 on /list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return HTTP 500 on /admin/booths/list (e.g. DB down)
  When the administrator opens /admin/booths
  Then the grid first shows "Loading booths…" / "جارٍ تحميل الأجنحة…"
  And then a red toast appears reading
      "Could not complete the request. Please try again." /
      "تعذّر إكمال الطلب. يُرجى المحاولة مرة أخرى."
  And no rows render
```

### E2E-BTH-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/booths in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "أجنحة المعرض"
  And the grid headers read الرمز / الاسم (إنجليزي) / الاسم (عربي) / الشركة / القطاع / القاعة / نشط
  And the "Add booth" button reads "إضافة جناح"

  When they click "إضافة جناح"
  Then the Add modal opens in RTL titled "إضافة جناح"
  And the field labels are Arabic (الرمز / الاسم (إنجليزي) / الشركة العارضة / اسم مسؤول الجناح / موضع الخريطة س …)
  And the Save / Cancel actions read "حفظ" / "إلغاء" and appear in reversed order
```

### E2E-BTH-016 — Per-column filter narrows the grid

```gherkin
Scenario: Typing in a per-column filter narrows the grid via the list endpoint
  Given the grid lists several booths including Code="A-12" (Sector (English)="Naval Defence")
    and Code="B-07" (Sector (English)="Maritime Logistics")
  When the administrator types "A-12" into the "Filter column Code" input on the Code column
  Then a POST /account/api/admin/booths/list fires with GridQuery.Filters["code"]="A-12" and Skip reset to 0
  And the grid narrows to only the rows whose Code contains "A-12"
  And the "Showing {0}–{1} of {2}" summary updates to the filtered count

  When they clear the Code filter and type "Maritime" into the "Filter column Sector (English)" input
  Then a POST /account/api/admin/booths/list fires with GridQuery.Filters["sector"]="Maritime" and Skip reset to 0
  And only the "B-07" row (Sector (English)="Maritime Logistics") remains
  And the Company and Hall columns expose NO filter input (they are client-resolved, not server-filterable)
```

### E2E-BTH-017 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles ascending / descending order
  Given the grid lists booths Code="A-12", "B-07", "C-01" (default order is Code ascending)
  When the administrator clicks the Code column header
  Then a POST /account/api/admin/booths/list fires with GridQuery.Sort="code" and SortDescending=false
  And the rows render in ascending Code order (A-12, B-07, C-01)

  When they click the Code header again
  Then a POST /account/api/admin/booths/list fires with GridQuery.Sort="code" and SortDescending=true
  And the rows render in descending Code order (C-01, B-07, A-12)
  And the Company and Hall columns expose NO sort affordance (client-resolved, not server-sortable)
```

### E2E-BTH-018 — Presentation toggle persists across reload (D-353)

```gherkin
Scenario: Switch between Popup and full Page and the choice persists
  Given the administrator is on /admin/booths with the default "dialog" (popup) presentation
  And the grid toolbar (CustomToolbar) shows the CrudPresentationToggle (PageKey="booths")
  When they click the toggle to choose "Open as full page"
  Then localStorage key "simf.cp.prefs.booths" holds {"v":1,"presentation":"page"}
  When they reload /admin/booths
  Then OnInitializedAsync calls Prefs.GetPresentationAsync("booths") and reads back "page"
  And opening "Add booth" now renders the full-page CrudShell frame (not a popup with a backdrop)
  When they switch the toggle back to "Open as dialog"
  Then localStorage key "simf.cp.prefs.booths" holds {"v":1,"presentation":"dialog"}
  And after a reload opening "Add booth" renders the popup dialog again
```

### E2E-BTH-019 — Full-page mode round-trip (D-353)

```gherkin
Scenario: In full-page mode the Add/Edit/View forms take over the content area
  Given the presentation for /admin/booths is set to "page"
  When the administrator clicks the grid toolbar "Add booth" action
  Then the grid + SimfBanner are hidden (GridHidden = FormOpen && presentation == Page)
  And the CrudShell renders full-page with the title "Add booth" and a Close ("Close") header
  And there is no modal backdrop
  When they fill Code="E-11", Name (English)="Hydrographic Survey", Name (Arabic)="المسح الهيدروغرافي"
  And they click "Add booth"
  Then POST /account/api/admin/booths returns HTTP 200
  And the CrudShell closes and the grid + banner re-appear with the new "E-11" row
  And a green toast reads "Booth saved." / "تم حفظ الجناح."
  When they click the "E-11" row's Details action
  Then the BoothsViewDelete form opens full-page in read-only mode (IsDelete=false, no Delete button)
      showing Code, both names, Exhibitor, officer name/phone/email, sector EN/AR,
      description EN/AR, Hall, Map X, Map Y and Active
  When they click the CrudShell close (X) / "Close"
  Then the form closes and the grid re-appears unchanged
```

### E2E-BTH-020 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Delete is gated by SimfConfirm inside the ViewDelete form, not window.confirm
  Given a booth "F-22" exists and is Active
  And the administrator is on /admin/booths
  When they click the "F-22" row's Delete (trash) action
  Then the page first GETs /account/api/admin/booths/{id} to load the full detail
  And the BoothsViewDelete form opens (in a CrudShell popup or full page per the toggle)
      showing the read-only details and a red "Delete" button
  When they click the red "Delete" button
  Then a SimfConfirm dialog appears (Danger=true) with the message
       Admin.Booths.Delete.Message formatted with the booth's English name "F-22"
       (NOT a native browser window.confirm)
  When they click the confirm "Cancel"
  Then no DELETE request fires and the row is unchanged (still Active = "✓")
  When they re-open Delete and click the confirm "Delete"
  Then exactly one DELETE /account/api/admin/booths/{id} fires and returns HTTP 200
  And the CrudShell closes
  And a green toast reads "Booth deleted." / "تم حذف الجناح."
  And the grid reloads and the soft-deactivated "F-22" row drops from the list
  And an audit row Booth.Deactivated records the actor id
```

**Evidence captured:**
- The delete now flows through `BoothsViewDelete.razor` → `SimfConfirm` → `simfAccount.deleteJson`;
  there is **no** `window.confirm` / `handle_dialog` step any more (the inline list `confirm()` was
  removed in D-353).
- Network: exactly one `DELETE /account/api/admin/booths/{id}` on confirm, zero on Cancel.

### E2E-BTH-021 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid (or just selected rows) to an XLSX workbook
  Given the administrator is on /admin/booths with at least two booths
  When they click the toolbar "Export" action with no rows selected
  Then OnExportAsync calls _excel.ExportAsync(empty Ids, current Query)
  And a POST /account/api/admin/booths/export fires carrying
      AdminGridExportRequest { Ids: [], Query: <current GridQuery> }
  And the API caps the set at 5000 rows and returns an XLSX
      (Content-Type application/vnd.openxmlformats-officedocument.spreadsheetml.sheet)
  And the browser saves a file named simf-booths-{yyyyMMddHHmmss}.xlsx
  And the workbook's "Booths" sheet header row reads
      Code | Name | NameArabic | Exhibitor | Sector | Hall | IsActive
  And the Exhibitor column is written as the exhibitor's English name and the Hall column
      as the hall's Code (the two human-readable natural keys the import resolves back), so the
      workbook round-trips through import
  When they instead select two rows then click "Export"
  Then the export request carries those two Ids in AdminGridExportRequest.Ids (and Query is null)
  And the workbook contains exactly those two rows
```

### E2E-BTH-022 — Excel import (D-356)

```gherkin
Scenario: Import booths from a workbook and see the per-row outcome + FK resolution
  Given the administrator is on /admin/booths
  And an active Exhibitor company named "Maritime Tech LLC" exists
  And an active Hall with Code "HALL-A" exists
  When they click the toolbar "Import" action
  Then OnImportAsync calls _excel.TriggerImportAsync(), opening the file picker
      on the hidden <input id="booths-import-input" accept=".xlsx">
  When they choose an .xlsx whose "Booths" sheet has the required headers
      Code, Name, NameArabic (and the optional Exhibitor, Sector, Hall columns)
      and two new rows, e.g.
      | G-01 | Naval Robotics | الروبوتات البحرية | Maritime Tech LLC |        | HALL-A |
      | G-02 | Coastal Radar  | الرادار الساحلي   |                   | Radar  |        |
  Then a POST /account/api/admin/booths/import fires as multipart form data (field "file")
  And the import-result modal shows "2 created, 0 updated, 0 skipped." with an empty error list
      (import is insert-only — Created is the only success kind)
  And the "G-01" row resolves its Exhibitor link from the English name "Maritime Tech LLC"
      and its Hall link from the code "HALL-A" (case-insensitive, active only)
  And a green toast reads the shared Grid.Import.Done key ("Import complete.")
  And OnImportedAsync reloads the grid (LoadAsync) so both new booths appear
  And note: the officer name/phone/email, the optional shared-Contact link (SIMF-FDS-014 / D-283)
      and the Map X / Map Y position are NOT importable — import always leaves them unset; an
      admin sets them afterwards via Edit
  When they import a workbook whose Code cell is 1 character (below the 2–16 range)
  Then that row is reported in the modal's error list
      ("Code must be between 2 and 16 characters." / "يجب أن يكون الرمز بين حرفين و16 حرفًا.")
      while the valid rows still create (one bad row never aborts the batch)
  When they import a workbook whose Exhibitor cell names no active exhibitor
  Then that row errors with
      "No active exhibitor named '{value}' was found." / "لم يتم العثور على عارض مفعّل باسم '{value}'."
  And a row whose Hall cell matches no active hall errors with
      "No active hall with code '{value}' was found." / "لم يتم العثور على قاعة مفعّلة بالرمز '{value}'."
```

### E2E-BTH-023 — Excel import rejection (D-356)

```gherkin
Scenario: A bad / wrong-sheet upload is rejected and nothing is created
  Given the administrator is on /admin/booths
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check 50 4B 03 04)
  Then POST /account/api/admin/booths/import returns HTTP 400 (DataValidationException)
  And CrudGridExcel.OnFileSelectedAsync raises OnError → OnExcelError surfaces a red toast
      reading the bilingual "not a valid Excel workbook" message
  And no booth is created
  When they import an .xlsx larger than 5 MB
  Then the request is rejected (the 5 MB upload gate) and a red toast appears, nothing created
  When they import a workbook whose worksheet is not named "Booths" (or is missing one of the
      required headers Code / Name / NameArabic)
  Then the parse fails and the request returns 400 with the bilingual worksheet/header message
  And no booth is created
```

---

## Implementation notes

- **API integration tests at a lower layer.** `tests/SIMF.Api.Tests/AdminBoothsTests.cs`
  (`AdminBoothsTests : IClassFixture<SimfApiFactory>`, 10 `[Fact]` cases) exercises
  the same admin surface without a browser — list/get/create/update/deactivate plus
  the duplicate-code (409 `BOOTH_CODE_DUPLICATE`), validation (400 `BOOTH_INVALID`),
  and not-found (404 `BOOTH_NOT_FOUND`) paths. The public read side is covered by
  `tests/SIMF.Api.Tests/PublicBoothsTests.cs`. When an E2E scenario above is
  automated, the matching `Api.Tests` case can usually be retired — but keep both
  during the transition.
- **Manual smoke is the canonical run today.** Until Playwright is adopted, the
  canonical "run" of these scenarios is a Chrome DevTools MCP session: sign in per
  the Auth setup, walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-booths-{scenario}.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin block into
  a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus a
  step-definition class. The Gherkin shape is already runner-agnostic.
- **Permission gate is enforced in two places.** `[RequirePermission(PermissionCatalog.Booths.View)]`
  on the page and `PermissionCatalog.PolicyFor(...)` on each API endpoint
  (`Booths.View`/`Create`/`Edit`/`Delete`). `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` fail the build if a gate
  is missing.
- **Details view shipped with D-353.** The page now hosts a read-only Details
  view (`OnDetailsOne` → `BoothsViewDelete` with no Delete button) in addition to
  Add/Edit — both framed by CrudShell as a popup or a full page per the toolbar
  toggle. (Earlier waves of this file noted "no Details modal"; that is no longer
  true.) The grid (D-256) loads `Top=20` rows per page with a pager
  (Prev/Next/First/Last) and the "Showing {0}–{1} of {2}" summary line.
- **No bulk action.** The grid is `Multiselect="true"` (select-all + per-row
  checkboxes) but has no `<CustomToolbar>` bulk-action button — the checkboxes are
  cosmetic here. Do not author a bulk-action scenario.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle). Added E2E-BTH-018..023
(D-353 Page↔Popup toggle + full-page round-trip + CrudShell/SimfConfirm delete gate; D-356 Excel
export/import + import rejection), grounded in `BoothsList.razor` + `BoothsAddEdit.razor` +
`BoothsViewDelete.razor` + `BoothsExcelEndpoints.cs` (export columns Code/Name/NameArabic/
Exhibitor[English name]/Sector/Hall[code]/IsActive; import resolves the Exhibitor by English
name + the Hall by code, insert-only, officer/Contact/MapX/MapY omitted).
Prior: 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
