# E2E test catalogue — Media Partners CRUD (`/admin/media-partners`)

| | |
|--|--|
| **Page** | [`cp/admin-media-partners.md`](../../pages/cp/admin-media-partners.md) |
| **Route** | `/admin/media-partners` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page facts grounded in source** (`Components/Pages/Admin/MediaPartnersList.razor`,
> `Endpoints/PublicRelations/MediaPartnerEndpoints.cs`,
> `Infrastructure/PublicRelations/AdminMediaPartnerService.cs`):
> - Required permission: `PermissionCatalog.MediaPartners.View` (`MediaPartners.View`).
> - Toolbar action: **Add media partner** (`Admin.MediaPartners.New`).
> - Surface is a `SimfDataGrid` (D-256 raw-table→grid conversion): per-page size
>   `Top = 20`, `Multiselect="true"` (select-all + per-row checkboxes are present
>   but there is **no bulk-action toolbar button** — selection is cosmetic here).
> - Grid columns: Name (English) `nameen`, Name (Arabic) `namear`, Logo path
>   `logo`, Link `url`, Display order `displayorder`, Active `isActive` (rendered
>   as a `SimfPill` — "Active" on / "Inactive" off, not `✓`/`—`).
> - **Per-column grid filters** (`Filterable="true"`): **Name (English)** `nameen`
>   and **Name (Arabic)** `namear` only. Sortable columns: `nameen`, `namear`,
>   `displayorder`. The backend (`AdminMediaPartnerService.ListAllAsync`) honours
>   `GridQuery.Filters["nameen"]`/`["namear"]` (case-sensitive `Contains`) and
>   `Sort=nameen|namear|displayorder` with `SortDescending`.
> - Row actions are quiet **icon** buttons inside the grid's `RowActions` — Edit
>   (pencil, `OnEditOne`) and Delete (trash, `OnDeleteOne`) — not filled text
>   buttons.
> - Add/Edit modal fields: NameEn (max 256), NameAr (max 256), Logo path
>   (max 512), Link (max 512), Display order (number, min 0 max 99999),
>   **Active** checkbox (`SimfCheckbox`). The Active checkbox is shown for both
>   Add and Edit, but Create always persists `IsActive = true` server-side
>   regardless (the create request carries no IsActive); Edit honours it.
> - **Add / Edit / View / Delete** are now hosted by `CrudShell` (D-353) — popup or
>   full page per the toolbar `CrudPresentationToggle` (`PageKey = "media-partners"`,
>   persisted in `localStorage` "simf.cp.prefs.media-partners" via `CpPreferences`).
>   `MediaPartnerAddEdit` carries Add/Edit; `MediaPartnerViewDelete` carries the
>   read-only **Details/View** path (a view path the old inline form never had) and,
>   when `IsDelete=true`, the Delete button.
> - **Delete** is a soft-deactivate (HTTP `DELETE` → `IsActive = false`), guarded
>   by a `SimfConfirm` dialog inside `MediaPartnerViewDelete` (title
>   `Admin.MediaPartners.Delete.Title`, message `Admin.MediaPartners.Delete.Message`
>   formatted with the partner name) — **not** a native JS `confirm()` (the inline
>   `SimfModal` form + `confirm()` it used to carry are gone, D-353). A deactivated
>   row is **dropped from the public list** but, because the admin list is
>   unfiltered, it still appears in the admin grid with the "Inactive" pill.
> - BFF passthroughs (`AccountEndpoints.cs`): `POST /account/api/admin/media-partners/list`,
>   `POST /account/api/admin/media-partners`, `PUT /account/api/admin/media-partners/{id}`,
>   `DELETE /account/api/admin/media-partners/{id}`, plus the D-356 Excel pair
>   `POST /account/api/admin/media-partners/export` (AdminGridExportRequest { Ids, Query })
>   and `POST /account/api/admin/media-partners/import` (multipart .xlsx; export + import
>   both capped at 5000 rows; a non-.xlsx upload is rejected with HTTP 400).
> - API error codes: `VALIDATION_FAILED` (400, name length / logo / url / order),
>   `MEDIA_PARTNER_NAME_DUPLICATE` (409, English name clash, case-insensitive),
>   `NOT_FOUND` (404, unknown id).
> - Audit event keys: `MediaPartnerCreated`, `MediaPartnerUpdated`,
>   `MediaPartnerDeactivated`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MPR-001 | Full CRUD round-trip — Add → Edit → Deactivate (Delete) | happy | P0 | _to author_ |
| E2E-MPR-002 | Add media partner — optional fields (logo + link) persisted | happy | P1 | _to author_ |
| E2E-MPR-003 | Edit — toggle Active flag off then on | happy | P1 | _to author_ |
| E2E-MPR-004 | Delete — confirm dialog cancelled leaves row untouched | happy | P1 | _to author_ |
| E2E-MPR-005 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-MPR-006 | Auth gate: signed-in admin lacking `MediaPartners.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-MPR-007 | Client validation: blank English/Arabic name → bilingual toast, no POST | error | P1 | _to author_ |
| E2E-MPR-008 | Server validation: name > 256 chars → 400 bilingual error | error | P2 | _to author_ |
| E2E-MPR-009 | Conflict: duplicate English name → 409 + bilingual message | error | P1 | _to author_ |
| E2E-MPR-010 | Server 500 on `/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-MPR-011 | RTL/Arabic render: page + modal mirror | i18n | P1 | _to author_ |
| E2E-MPR-012 | Per-column filter narrows the grid (`nameen`/`namear`) | grid | P1 | _to author_ |
| E2E-MPR-013 | Column sort toggles (`displayorder`/`nameen`) | grid | P2 | _to author_ |
| E2E-MPR-014 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-MPR-015 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-MPR-016 | Delete confirmation gate: ViewDelete + SimfConfirm — Cancel = no DELETE, confirm = one DELETE (D-353) | error | P0 | _to author_ |
| E2E-MPR-017 | Excel export: toolbar Export → POST /export (whole grid vs selected rows) (D-356) | happy | P1 | _to author_ |
| E2E-MPR-018 | Excel import: upload a workbook → rows created + result modal "N created…" (D-356) | happy | P1 | _to author_ |
| E2E-MPR-019 | Excel import: non-.xlsx / wrong-sheet upload → 400 + bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |
| E2E-MPR-020 | Logo via the unified media-asset pipeline — upload then external link (D-357) | happy | P1 | _to author_ |

## Scenarios

### E2E-MPR-001 — Full CRUD round-trip

```gherkin
Feature: Media Partners CRUD round-trip
  As an Administrator
  I want to manage the public media-partner grid ("شركاء النجاح", Mockup page 31)
  So that the app's media-partner section stays accurate for the event

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator holding the MediaPartners.View/Create/Edit/Delete permissions
    has signed in via /login + /login/totp using superadmin@zagali-ict.com and a Get-Totp code
  And they have landed on /admin/media-partners

Scenario: Create, edit, then deactivate one media partner
  Given the grid currently shows {N} rows
  When the administrator clicks "Add media partner"
  Then the Add modal opens titled "Add media partner"
  And it shows fields: Name (English), Name (Arabic), Logo path, Link, Display order, and an "Active" checkbox (ticked)
  When they fill Name (English)="Naval Times"
  And they fill Name (Arabic)="أوقات البحرية"
  And they fill Display order="100"
  And they click "Save"
  Then a POST /account/api/admin/media-partners returns 200
  And the modal closes
  And a green toast reads "Media partner saved." / "تم حفظ الشريك الإعلامي."
  And the grid reloads and shows {N + 1} rows
  And a row exists with Name (English)="Naval Times", Display order=100, and the Active pill reads "Active"

  When the administrator clicks the "Naval Times" row's Edit (pencil) action
  Then the Edit modal opens titled "Edit media partner" with the row's values pre-filled
  And the "Active" checkbox is ticked
  When they change Display order to "5"
  And they fill Link="https://navaltimes.example"
  And they click "Save"
  Then a PUT /account/api/admin/media-partners/{id} returns 200
  And the modal closes
  And a green toast reads "Media partner saved." / "تم حفظ الشريك الإعلامي."
  And the "Naval Times" row now shows Display order=5 and Link="https://navaltimes.example"

  When the administrator clicks the "Naval Times" row's Delete (trash) action
  Then the MediaPartnerViewDelete form opens (CrudShell) showing the read-only details and a red "Delete" button
  When they click "Delete" and then confirm in the SimfConfirm dialog (which names "Naval Times")
  Then a DELETE /account/api/admin/media-partners/{id} returns 200
  And a green toast reads "Media partner deleted." / "تم حذف الشريك الإعلامي."
  And the "Naval Times" row remains in the admin grid but its Active pill now reads "Inactive"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-media-partners-001-before.png`
- Screenshot after add: `docs/screenshots/cp-admin-media-partners-001-add.png`
- Screenshot after edit: `docs/screenshots/cp-admin-media-partners-001-edit.png`
- Screenshot after delete: `docs/screenshots/cp-admin-media-partners-001-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/media-partners/*` call returns 200
- Audit rows: `OperationLog`/`AuditEntry` rows with `Event = 'MediaPartnerCreated'`,
  `'MediaPartnerUpdated'`, `'MediaPartnerDeactivated'`, each carrying the actor's id.

### E2E-MPR-002 — Add with optional fields

```gherkin
Scenario: Logo path and Link are persisted when supplied
  Given the Add modal is open
  When the administrator fills Name (English)="Gulf Maritime Press"
  And fills Name (Arabic)="صحافة الخليج البحرية"
  And fills Logo path="media-partners/gulf-press.png"
  And fills Link="https://gulfpress.example"
  And fills Display order="200"
  And clicks "Save"
  Then a POST /account/api/admin/media-partners returns 200
  And the new grid row shows Logo path="media-partners/gulf-press.png" and Link="https://gulfpress.example"
  And rows with blank logo/link instead render "—" in those columns
```

### E2E-MPR-003 — Toggle Active off then on

```gherkin
Scenario: Editing the Active checkbox flips the row's Active pill
  Given a media partner "Gulf Maritime Press" exists with its Active pill reading "Active"
  When the administrator clicks that row's Edit (pencil) action
  And unticks the "Active" checkbox
  And clicks "Save"
  Then a PUT /account/api/admin/media-partners/{id} returns 200
  And the row's Active pill now reads "Inactive"
  When the administrator clicks that row's Edit (pencil) action again
  And ticks the "Active" checkbox
  And clicks "Save"
  Then the row's Active pill reads "Active" again
```

### E2E-MPR-004 — Delete confirm cancelled

```gherkin
Scenario: Cancelling the confirm dialog makes no API call
  Given a media partner "Gulf Maritime Press" exists in the grid
  When the administrator clicks that row's Delete (trash) action
  Then the MediaPartnerViewDelete form opens (CrudShell) and they click "Delete" to raise the SimfConfirm dialog
  When they cancel the SimfConfirm dialog
  Then no DELETE /account/api/admin/media-partners/{id} request fires
  And the row is unchanged (its Active pill still reads "Active")
  And no toast appears
```

### E2E-MPR-005 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no MediaPartner rows
  When the administrator opens /admin/media-partners
  Then the grid body renders the SimfEmptyState component
  And the empty state shows the bilingual copy "No media partners yet." / "لا يوجد شركاء إعلاميون بعد."
  And the toolbar still shows the "Add media partner" button
```

### E2E-MPR-006 — Auth gate

```gherkin
Scenario: Signed-in admin lacking MediaPartners.View is denied
  Given a signed-in Control Panel user whose roles do NOT grant the MediaPartners.View permission
  When they navigate to /admin/media-partners
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/media-partners/list request fires
  And the "Media Partners" item is hidden from the CP nav rail (RequiredPermission gate)
```

### E2E-MPR-007 — Client-side validation (blank name)

```gherkin
Scenario: Blank English or Arabic name shows bilingual error without calling the API
  Given the Add modal is open
  When the administrator leaves Name (English) blank
  And fills Name (Arabic)="اسم عربي صالح"
  And clicks "Save"
  Then a SimfAlert error appears reading "Both the English and Arabic names are required." / "الاسم بالإنجليزية والعربية مطلوبان."
  And the modal stays open
  And no POST /account/api/admin/media-partners request fires

  When they fill Name (English)="Valid Name"
  And clear Name (Arabic)
  And click "Save"
  Then the same bilingual "Both the English and Arabic names are required." error appears
  And still no POST request fires
```

### E2E-MPR-008 — Server-side validation (name too long)

```gherkin
Scenario: English name longer than 256 chars returns a 400 with bilingual error
  Given the Add modal is open
  When the administrator fills Name (English) with a 300-character string
  And fills Name (Arabic)="اسم صالح"
  And clicks "Save"
  Then the BFF forwards POST /account/api/admin/media-partners
  And the API returns HTTP 400 with ApiResult.Error.Code = "VALIDATION_FAILED"
  And the modal stays open
  And the error toast surfaces "Media partner English name must be between 1 and 256 characters." / "يجب أن يتراوح الاسم الإنجليزي للشريك الإعلامي بين 1 و 256 حرفاً."
```

> Note: the modal `SimfTextField` caps NameEn/NameAr at `MaxLength="256"`, so this
> path is normally hit via the API integration test or by pasting past the cap;
> the catalogue keeps it to prove the server guard, not only the UI cap.

### E2E-MPR-009 — Duplicate English name

```gherkin
Scenario: Duplicate English name returns 409 with bilingual server message
  Given a media partner with Name (English)="Naval Times" already exists
  When the administrator opens the Add modal
  And fills Name (English)="naval times" (case-insensitive clash)
  And fills Name (Arabic)="أوقات بحرية أخرى"
  And fills Display order="0"
  And clicks "Save"
  Then the BFF forwards POST /account/api/admin/media-partners
  And the API returns HTTP 409 with ApiResult.Error.Code = "MEDIA_PARTNER_NAME_DUPLICATE"
  And the modal stays open
  And the error toast surfaces "A media partner named 'naval times' already exists." / "يوجد شريك إعلامي بالاسم 'naval times' بالفعل."
```

### E2E-MPR-010 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/media-partners/list (e.g. DB down)
  When the administrator opens /admin/media-partners
  Then the page shows the "Loading media partners…" indicator
  And then a red toast appears reading "Could not load media partners. Please try again." / "تعذّر تحميل الشركاء الإعلاميين. يُرجى المحاولة مرة أخرى."
  And no rows render
```

### E2E-MPR-011 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/media-partners in English
  When they switch the UI language to Arabic ("العربية")
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "شركاء النجاح"
  And the nav rail mirrors to the right with Arabic labels
  And the grid headers read "الاسم (إنجليزي)", "الاسم (عربي)", "مسار الشعار", "الرابط", "ترتيب العرض", "نشط"
  And the toolbar button reads "إضافة شريك إعلامي"

  When they click "إضافة شريك إعلامي"
  Then the Add modal opens in RTL titled "إضافة شريك إعلامي"
  And the field labels are Arabic ("الاسم (إنجليزي)", "الاسم (عربي)", "مسار الشعار", "الرابط", "ترتيب العرض", "نشط")
  And the footer buttons read "إلغاء" (Cancel) and "حفظ" (Save) in reverse order
```

### E2E-MPR-012 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a column filter input narrows the grid via GridQuery.Filters
  Given the grid shows several media partners including "Naval Times" and "Gulf Maritime Press"
  When the administrator types "Naval" into the "Filter column Name (English)" input
  Then a POST /account/api/admin/media-partners/list fires
  And its GridQuery body carries Filters["nameen"]="Naval" with Skip reset to 0
  And the grid narrows to only rows whose English name contains "Naval" (e.g. "Naval Times")
  And "Gulf Maritime Press" is no longer shown

  When the administrator clears the "Name (English)" filter
  And types "الخليج" into the "Filter column Name (Arabic)" input
  Then a POST /account/api/admin/media-partners/list fires with Filters["namear"]="الخليج" and Skip=0
  And the grid narrows to rows whose Arabic name contains "الخليج" (e.g. "صحافة الخليج البحرية")
```

> Note: only **Name (English)** (`nameen`) and **Name (Arabic)** (`namear`) carry
> `Filterable="true"`; Logo path, Link, Display order and Active have no per-column
> filter input. The backend `AdminMediaPartnerService.ListAllAsync` matches these
> with a case-sensitive `Contains`, so the filter value casing must match the data.

### E2E-MPR-013 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles ascending/descending
  Given the grid is loaded with the default order (Display order asc, then Name (Arabic) asc)
  When the administrator clicks the "Display order" column header
  Then a POST /account/api/admin/media-partners/list fires with Sort="displayorder" and SortDescending=false
  And the rows are ordered by Display order ascending
  When the administrator clicks the "Display order" header again
  Then a POST fires with Sort="displayorder" and SortDescending=true
  And the rows are ordered by Display order descending

  When the administrator clicks the "Name (English)" column header
  Then a POST fires with Sort="nameen" and SortDescending=false
  And the rows are ordered by English name A→Z
```

> Note: only `nameen`, `namear` and `displayorder` are `Sortable="true"`; the Logo,
> Link and Active columns are not sortable.

### E2E-MPR-014 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/media-partners with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle ("Open as full page", maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.media-partners" holds {"v":1,"presentation":"page"}
  When they reload /admin/media-partners
  Then OnInitializedAsync reads the preference (Prefs.GetPresentationAsync("media-partners"))
  And the toggle still reads "Open as dialog"
  And opening "Add media partner" now renders the full-page frame (not a popup)
```

### E2E-MPR-015 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page"
  When the administrator clicks "Add media partner"
  Then the grid + banner are hidden (GridHidden) and the CrudShell renders as a full page
  And the page shows the title "Add media partner" with a close header and the MediaPartnerAddEdit form
  And there is no modal backdrop
  When they fill Name (English)="Naval Times", Name (Arabic)="أوقات البحرية", Display order="100"
  And they click "Save"
  Then a POST /account/api/admin/media-partners returns 200
  And the page frame closes and the grid re-appears with the new row
  And a green toast reads "Media partner saved." / "تم حفظ الشريك الإعلامي."
  When they click the new row's Edit (pencil) action and then the frame's "Close" button
  Then the form closes and the grid re-appears unchanged (no PUT fires)
```

### E2E-MPR-016 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Deactivate opens the ViewDelete form and SimfConfirm gates the call
  Given a media partner "Naval Times" exists in the grid
  When the administrator clicks that row's Delete (trash) action
  Then the MediaPartnerViewDelete form opens (CrudShell, dialog by default)
  And it shows the row's read-only details (Name En/Ar, Logo, Link, Display order, Active)
  And a red "Delete" button is visible
  When they click "Delete"
  Then a SimfConfirm dialog appears titled "Delete media partner"
  And its message names the partner ("Naval Times")
  And it shows a danger "Delete" confirm button and a "Cancel" button
  When they click "Cancel"
  Then no DELETE /account/api/admin/media-partners/{id} request fires
  And the form stays open and the row is unchanged
  When they click "Delete" again and then the confirm "Delete" button
  Then exactly one DELETE /account/api/admin/media-partners/{id} returns 200
  And the form closes
  And a green toast reads "Media partner deleted." / "تم حذف الشريك الإعلامي."
  And the "Naval Times" row's Active pill now reads "Inactive"
```

### E2E-MPR-017 — Excel export (D-356)

```gherkin
Scenario: Export the media-partner grid to an XLSX workbook
  Given the administrator is on /admin/media-partners with at least two media partners
  When they click the toolbar "Export" action with no rows selected
  Then a POST /account/api/admin/media-partners/export fires carrying AdminGridExportRequest
    with an empty Ids list and the current Query (the whole filtered grid)
  And the browser saves a media-partners .xlsx workbook with a header row
  When they instead select two rows then click "Export"
  Then the POST carries those two ids in Ids (and no Query) and the workbook contains exactly those two rows
```

> Note: the page wires `OnExport=OnExportAsync`, which calls
> `_excel.ExportAsync(selected ids, _query)` on the `<CrudGridExcel Resource="media-partners" />`.
> The API caps export at 5000 rows.

### E2E-MPR-018 — Excel import (D-356)

```gherkin
Scenario: Import media partners from a workbook and see the per-row outcome
  Given the administrator is on /admin/media-partners
  When they click the toolbar "Import" action
  Then the hidden file input "media-partners-import-input" (accept=".xlsx") opens the file picker
  When they choose an .xlsx whose sheet has rows for two new media partners
  Then a POST /account/api/admin/media-partners/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped."
  And a green toast reads the shared Grid.Import.Done copy
  And the grid reloads and lists both new media partners
  When they import a workbook containing one duplicate English name and one new name
  Then the modal shows 1 created and a per-row error naming the duplicate row
```

> Note: the page wires `OnImport=OnImportAsync` → `_excel.TriggerImportAsync()`;
> `OnImported` fires `OnImportedAsync`, which raises the `Grid.Import.Done` toast and
> reloads the grid. The API caps import at 5000 rows.

### E2E-MPR-019 — Excel import rejection (bad / wrong-sheet upload) (D-356)

```gherkin
Scenario: A bad upload is rejected without creating anything
  Given the administrator is on /admin/media-partners
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check, or exceeds the 5MB gate)
  Then the request returns HTTP 400 and the page shows a bilingual error toast (CrudGridExcel OnError → OnExcelError)
  And no media partner is created
  When they import a workbook whose sheet is not the expected media-partners sheet
  Then the request returns HTTP 400 with the bilingual wrong-worksheet message
  And nothing is created
```

---

## Implementation notes

- **Manual smoke as canonical-source-of-truth today.** Until Playwright is
  adopted, the canonical "run" of these scenarios is a Chrome DevTools MCP
  session — sign in via the Background steps, walk each scenario, and capture
  screenshots into `docs/screenshots/cp-admin-media-partners-{scenario}.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be
  created) + a step-definition class. The Gherkin shape is already
  runner-agnostic.
- **API integration tests** at `tests/SIMF.Api.Tests/AdminMediaPartnersTests.cs`
  cover the same surface at a lower layer (no browser): create→list round-trip,
  get-by-id, 409 duplicate English name (`MEDIA_PARTNER_NAME_DUPLICATE`),
  update-persists, 404 unknown id (`NOT_FOUND`), 400 blank English name
  (`VALIDATION_FAILED`), and the non-admin caller → 403 forbidden gate. The
  public anonymous read is covered by `tests/SIMF.Api.Tests/MediaPartnersTests.cs`.
  E2E-MPR-006/007/008/009 mirror those at the browser layer (including the
  client-only NameRequired guard, which has no API equivalent).

### E2E-MPR-020 — Logo via the unified media-asset pipeline (D-357)

```gherkin
Scenario: Upload logo, then switch it to an external link
  Given an Administrator is editing a media partner
  When they open the "Image" control, choose "Upload file", pick a PNG and click Upload
  Then a success message shows and the preview thumbnail refreshes
  And GET /account/api/admin/assets/MediaPartnerLogo/{ownerId}/image returns the bytes (200)
  And /admin/media-library lists it as MediaPartnerLogo - this entity - Image - Uploaded file - active
  When they switch to "External link", enter https://cdn.example/x.jpg and click Save link
  Then the asset Source becomes "External link" and GET /app/assets/MediaPartnerLogo/{ownerId}/image 302s to that URL
  And the same-origin /content/assets/MediaPartnerLogo/{ownerId}/image proxy serves it for any public page that renders this entity
```

**Evidence:** the Asset DB row + the out-of-row file (or stored link); the Media Library row;
0 console errors; audit `AssetUploaded` then `AssetLinked`. Validation: a non-image / over-5 MB /
video upload is 400; deactivate->restore round-trips; restoring when a live (category,owner) asset
already exists is 409 (covered by `tests/SIMF.Api.Tests/AssetEndpointsTests.cs`).

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle; appended E2E-MPR-014..019).
Earlier: 2026-06-03 (E2E catalogue rebuild, D-256/D-257 grid affordances reconciled).
