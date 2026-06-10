# Exhibition booths — `/admin/booths`

| | |
|--|--|
| **Route** | `/admin/booths` |
| **Layout** | `CpShellLayout` |
| **Audience** | Administrator (admins holding the `Booths.*` permissions) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Booths.View)]` (page) + per-action API policies (`Booths.Create` / `Edit` / `Delete` / `Export` / `Import`) + `RequireApprovedAccount`; mutating + Excel endpoints also `RequireRateLimiting("auth")` |
| **Pattern** | D-199 event-module CRUD on the uniform CRUD shell — **D-353** `CrudShell` Add/Edit + View/Delete with the Page ↔ Popup `CrudPresentationToggle`, **D-356** Excel export + import via `CrudGridExcel` |
| **Status** | ✅ Real (D-199; D-222 Booth→Exhibitor + officer; D-353 toggle/CrudShell + D-356 Excel, 2026-06-10) |
| **Backend endpoints** | `POST /account/api/admin/booths/list`, `GET /account/api/admin/booths/{id}`, `POST /account/api/admin/booths`, `PUT /account/api/admin/booths/{id}`, `DELETE /account/api/admin/booths/{id}`, `POST /account/api/admin/booths/export`, `POST /account/api/admin/booths/import` |
| **Source** | [`BoothsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BoothsList.razor), [`BoothsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BoothsAddEdit.razor), [`BoothsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BoothsViewDelete.razor), [`BoothEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/BoothEndpoints.cs), [`BoothsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/BoothsExcelEndpoints.cs), [`AdminBoothService.cs`](../../../src/Backend/SIMF.Infrastructure/Exhibition/AdminBoothService.cs) |
| **Backed by** | `dbo.Booths` table on `SimfAppDbContext` (D-199; D-222 added the Exhibitor FK + booth-officer fields). |
| **Tests** | [`docs/tests/e2e/cp-admin-booths.md`](../../tests/e2e/cp-admin-booths.md); API: `tests/SIMF.Api.Tests/AdminBoothsTests.cs`, `tests/SIMF.Api.Tests/PublicBoothsTests.cs`, `tests/SIMF.Api.Tests/BoothsExcelTests.cs` |
| **Last reviewed** | 2026-06-10 |

## 1. Purpose

The public website Exhibition screen (Mockup page 22) and the 2D venue map list the
event's exhibition booths. This Control Panel page is where an administrator
maintains that list: add a booth with a stable **Code**, bilingual name, the
**Exhibitor company** that staffs it, a booth-officer contact (name / phone / email
and optionally a link to a shared Contact-directory record), a bilingual sector and
description, an optional **Hall**, the booth's 2D **Map X / Map Y** position, and the
active flag; edit any of those; view the read-only detail; and soft-delete
(deactivate) a booth so it drops off the public exhibition list and the venue map.

A booth is **linked to a Company** through `ExhibitorId` (D-222) — the company must
be an **active Exhibitor**. The link is a one-way FK to the Exhibitor table on the
same `SimfAppDbContext`; the optional shared-Contact link (`ContactId`) is the
SIMF-FDS-014 / D-283 booth-officer directory record. D-353 moved every form onto the
uniform `CrudShell` (popup or full page, per the admin's saved preference) and
replaced the old inline `SimfModal` form + native `confirm()` delete with a
`SimfConfirm`-gated View/Delete form. D-356 added Excel export and insert-only import
so the list can be bulk-managed from a spreadsheet.

## 4. UI

- **Banner + surface.** `SimfBanner` titled `Admin.Booths.Title` ("Exhibition
  booths" / "أجنحة المعرض") above a `SimfDataGrid` in `simf-page-wide` /
  `simf-surface`. A `SimfAlert` toast (success / error) renders above the grid. When
  a form is open in **full-page** mode the banner + grid are hidden (`GridHidden`);
  in popup mode they stay behind the dialog.
- **Grid (D-256 raw-table → grid).** `Multiselect="true"` (select-all + per-row
  checkboxes — cosmetic; there is **no** bulk-action toolbar button), per-column
  filter inputs and sortable headers where applicable, quiet icon row-actions
  (Add / Edit pencil / Details / Delete trash), a toolbar **Export** + **Import**
  action, and a pager (First / Prev / Next / Last + page size) with the
  "Showing X–Y of Z" summary. Default page size `Top = 20`.
- **Grid columns:** Code (sortable, filterable), Name English (sortable, filterable),
  Name Arabic (filterable), **Exhibitor** (client-resolved from the cached exhibitor
  list — neither sortable nor filterable server-side), Sector English (sortable,
  filterable), **Hall** (client-resolved from the cached hall list — neither sortable
  nor filterable), Active (sortable; `SimfPill` on/off). Empty list renders
  `SimfEmptyState` (`Admin.Booths.None`).
- **Row actions:** Add → `OnAddAsync` opens an empty `BoothsAddEdit`; Edit →
  `OnEditAsync` GETs the full detail then opens a pre-filled `BoothsAddEdit`; Details
  → `OnDetailsAsync` opens `BoothsViewDelete` read-only (`IsDelete=false`, no Delete
  button); Delete → `OnDeleteAsync` opens `BoothsViewDelete` in delete mode
  (`IsDelete=true`, red Delete button gated by `SimfConfirm`). Edit / Details /
  Delete always re-fetch the **detail** first because the grid summary omits the
  bilingual sector/description, the officer fields, the map position and the optional
  Contact link.
- **Excel export + import (D-356):** the toolbar carries **Export** and **Import**.
  Export (`OnExportAsync` → `_excel.ExportAsync`) posts
  `AdminGridExportRequest { Ids, Query }` to `/account/api/admin/booths/export`
  (selected rows win, else the whole filtered grid) and downloads
  `simf-booths-{timestamp}.xlsx`. Import (`OnImportAsync` → `_excel.TriggerImportAsync()`)
  opens the hidden file `<input id="booths-import-input" accept=".xlsx">` and posts
  an `.xlsx` to `/account/api/admin/booths/import`, then shows a result modal
  ("N created, N updated, N skipped" + per-row errors). Both are wired through one
  `CrudGridExcel @ref="_excel" Resource="booths"` host below the grid.
- **Page ↔ Popup presentation toggle (D-353):** the `<CustomToolbar>` carries a
  `CrudPresentationToggle PageKey="booths"`; the choice persists in `localStorage`
  under `simf.cp.prefs.booths` via `CpPreferences` and is restored on load
  (`OnInitializedAsync` calls `Prefs.GetPresentationAsync("booths")`).

## 4.5 Form fields (`BoothsAddEdit`)

| Field | Type | Required | MaxLength | Validation | Locale key |
|-------|------|----------|-----------|------------|------------|
| Code | text | yes | 16 | 2–16 chars; trimmed + uppercased server-side; unique | `Admin.Booths.Field.Code` |
| Name (English) | text | yes | 128 | 1–128 chars | `Admin.Booths.Field.NameEn` |
| Name (Arabic) | text | yes | 128 | 1–128 chars | `Admin.Booths.Field.NameAr` |
| Exhibitor company | select | no | — | must be an active Exhibitor (else 400); first option "— No company —" | `Admin.Booths.Field.Exhibitor` |
| Booth officer name | text | no | 256 | ≤256 chars | `Admin.Booths.Field.OfficerName` |
| Booth officer phone | text | no | 32 | ≤32 chars | `Admin.Booths.Field.OfficerPhone` |
| Booth officer email | text | no | 320 | ≤320 chars; must contain `@` (else 400) | `Admin.Booths.Field.OfficerEmail` |
| Contact | `ContactPicker` | no | — | must be an existing active Contact (SIMF-FDS-014 / D-283) | — |
| Sector (English) | text | no | 128 | ≤128 chars | `Admin.Booths.Field.SectorEn` |
| Sector (Arabic) | text | no | 128 | ≤128 chars | `Admin.Booths.Field.SectorAr` |
| Description (English) | textarea | no | 2048 | ≤2048 chars | `Admin.Booths.Field.DescriptionEn` |
| Description (Arabic) | textarea | no | 2048 | ≤2048 chars | `Admin.Booths.Field.DescriptionAr` |
| Hall | select | no | — | must be an active Hall (else 400); first option "— No hall —" | `Admin.Booths.Field.HallId` |
| Map X position | number | no | — | optional double (invariant-culture parse) | `Admin.Booths.Field.MapX` |
| Map Y position | number | no | — | optional double | `Admin.Booths.Field.MapY` |
| Active | checkbox | Edit only | — | bool (Create always active) | `Admin.Booths.Field.IsActive` |

The form runs Create (`POST`) when `IsEdit=false` and Edit (`PUT` against
`Initial.Id`) when `IsEdit=true`; only Edit shows the Active checkbox. The exhibitor
+ hall pickers load active rows via a `Top=500` round-trip each; the exhibitor option
label is `{NameEn} — {NameAr}` and the hall option label is `{Name} — {NameArabic}`.
Blank Code / Name (English) / Name (Arabic) are guarded client-side before any
request (`Admin.Booths.Required`).

### View / Delete form (`BoothsViewDelete`)

Read-only `<dl>` of Code, both names, Exhibitor (resolved name), officer
name/phone/email, sector EN/AR, description EN/AR, Hall (resolved name), Map X, Map Y,
Active. In delete mode a red Delete button opens a `SimfConfirm` (Danger) whose
message is `Admin.Booths.Delete.Message` formatted with the booth's English name; only
the confirm fires `DELETE` (`simfAccount.deleteJson`). The old inline list `confirm()`
was removed in D-353.

## 5. Data flow + endpoints

```
Admin action → BoothsList handler → JS interop (simfAccount.{post,get,put,delete}Json)
            → CP BFF /account/api/admin/booths/* → API /api/v1/admin/booths/*
            → IAdminBoothService / Excel endpoints → SIMF_App DB (dbo.Booths)
            → ApiResult<T> envelope → grid reload + toast
```

| When | Method + path (BFF → API) | Request body | Response | Policy |
|------|---------------------------|--------------|----------|--------|
| OnInit / query change | `POST /account/api/admin/booths/list` | `GridQuery` | `ApiResult<GridPage<AdminBoothSummary>>` | `Booths.View` |
| Edit / Details / Delete click | `GET /account/api/admin/booths/{id}` | — | `ApiResult<AdminBoothDetail>` (404 `BOOTH_NOT_FOUND`) | `Booths.View` |
| Add save | `POST /account/api/admin/booths` | `AdminCreateBoothRequest` | `ApiResult<AdminBoothDetail>` | `Booths.Create` (rate-limit `auth`) |
| Edit save | `PUT /account/api/admin/booths/{id}` | `AdminUpdateBoothRequest` | `ApiResult<AdminBoothDetail>` | `Booths.Edit` (rate-limit `auth`) |
| Confirm delete | `DELETE /account/api/admin/booths/{id}` | — | `ApiResult<bool>` | `Booths.Delete` (rate-limit `auth`) |
| Export | `POST /account/api/admin/booths/export` | `AdminGridExportRequest { Ids, Query }` | XLSX binary | `Booths.Export` (rate-limit `auth`) |
| Import | `POST /account/api/admin/booths/import` | multipart `file` (.xlsx) | `ApiResult<AdminGridImportResult>` | `Booths.Import` (rate-limit `auth`) |

The CP BFF passes the CRUD routes through one-for-one (`AccountEndpoints.cs` ~2290–2318)
and wires the two generic Excel routes via `MapGridExcel(group, "booths")`
(`AccountEndpoints.cs` ~557 → `/admin/booths/export` + `/admin/booths/import`). The
page also loads two cached lookups at mount (`POST /account/api/admin/halls/list` and
`POST /account/api/admin/exhibitors/list`, `Top=500`) so the grid's Hall + Exhibitor
columns and the form pickers can resolve names from the ids the summary carries.

### 5.1 Excel export columns

`ExportBoothsEndpoint` writes a sheet named **"Booths"** with the header row
`Code | Name | NameArabic | Exhibitor | Sector | Hall | IsActive`. The two foreign
keys are exported by a human-readable natural key so the workbook round-trips back
through import: the **Exhibitor as its English name** and the **Hall as its Code**
(both maps are built once per request). File name: `simf-booths-{yyyyMMddHHmmss}.xlsx`.
With selected rows the export honours `AdminGridExportRequest.Ids`; with none, it
exports the whole filtered set (`Query`). Capped at 5000 rows.

### 5.2 Excel import

`ImportBoothsEndpoint` is **insert-only** (Created is the only success kind). Required
headers: `Code`, `Name`, `NameArabic`. The optional `Exhibitor` cell resolves to an
active exhibitor by **English name** (case-insensitive) and the optional `Hall` cell
to an active hall by **Code** (case-insensitive); a non-blank value that resolves to
nothing is a per-row error. The booth-officer fields, the optional shared-Contact link
(`ContactId`) and the Map X / Map Y position **cannot** be expressed safely as plain
text, so import always leaves them unset — an admin sets them afterwards via Edit. A
duplicate Code is a per-row error, not a batch abort. The result
`AdminGridImportResult { Created, Updated, Skipped, Errors[] }` drives the modal; the
success toast is the shared `Grid.Import.Done` key.

## 6. Validation + error handling

- **Client-side guards** (`BoothsAddEdit.HandleSubmitAsync`): blocks the request when
  Code or Name (English) or Name (Arabic) is blank and shows `Admin.Booths.Required`.
- **Server-side validation** (`AdminBoothService.ValidateAndNormalise`): trims +
  upper-cases Code (case-insensitive uniqueness); Code 2–16 chars; Name 1–128;
  NameArabic 1–128; officer name ≤256, phone ≤32, email ≤320 and must contain `@`;
  sector EN/AR ≤128; description EN/AR ≤2048. A supplied `HallId` must be an **active
  hall**, `ExhibitorId` an **active Exhibitor**, and `ContactId` an existing active
  Contact. Every failure throws `ApiException(ErrorCodes.BoothInvalid, 400, …)`.
- **Duplicate guard:** a booth whose Code already exists (after upper-casing) →
  `ApiException(ErrorCodes.BoothCodeDuplicate, 409, …)`
  ("A booth with code '{code}' already exists." / "يوجد جناح بالرمز '{code}' بالفعل.").
  Update only re-checks when the Code actually changed.
- **Not found:** `GET` / `PUT` / `DELETE` against a missing id → `BoothNotFound` (404)
  ("The booth was not found." / "لم يتم العثور على الجناح.").
- **Error codes:** `BOOTH_INVALID` (400), `BOOTH_NOT_FOUND` (404),
  `BOOTH_CODE_DUPLICATE` (409).
- **Import upload defence (D-356):** non-`.xlsx` (fails the ZIP-magic check) → 400;
  file > 5 MB → rejected by the 5 MB upload gate; wrong sheet name or a missing
  required header → 400. The page surfaces `CrudGridExcel.OnError` → `OnExcelError`
  as a red toast; nothing is created.
- **Error envelope:** standard `ApiResult<T>.Error` with `Code` + bilingual
  `Message` / `MessageArabic`; the forms surface `MessageForCurrentCulture()`.
- **Toast strategy:** success → `Admin.Booths.Saved` / `Admin.Booths.Deleted` (green)
  and `Grid.Import.Done` after import; list-load failure → `Admin.Booths.LoadFailed`;
  lookup-load failure → `Admin.Booths.HallsLoadFailed` / `Admin.Booths.ExhibitorsLoadFailed`;
  form-level errors render in the form's `SimfAlert`.
- **Audit events:** `Booth.Created`, `Booth.Updated`, `Booth.Deactivated` (via
  `IAuditLog`, each carrying the actor user id).

## 7. Edge cases + known limitations

- **Soft-delete only.** `DELETE` calls `booth.Deactivate()` (`IsActive=false`),
  idempotent; the booth drops off the public exhibition list and the venue map, and
  the admin grid reload no longer renders it.
- **FK display is client-resolved.** The summary carries only `ExhibitorId` / `HallId`,
  so the Exhibitor + Hall grid columns are neither server-sortable nor
  server-filterable; the page resolves the names from cached lookups.
- **Detail re-fetch before every form** so the officer fields, the bilingual
  sector/description, the map position and the optional Contact link are never wiped
  when editing from the summary-only grid.
- **Import never sets officer / Contact / map position** — those are omitted by
  design; set them afterwards via Edit. Import is insert-only (no upsert), so a
  re-imported duplicate Code is a per-row 409-style error.
- **Map X / Map Y are free doubles** consumed by the public 2D venue map; the server
  does not range-check them.
- **Action buttons are not `<AuthorizedAction>`-gated** — an admin with `Booths.View`
  but not Create/Edit/Delete/Export/Import sees the buttons, but the API rejects the
  call (403). That API policy is the per-action enforcement point.

## 8. i18n + RTL

All visible strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR) via
`IStringLocalizer<Strings> L` (`Admin.Booths.*` keys + the shared `Grid.*` keys for
the toolbar / pager / Excel actions). Banner title `أجنحة المعرض`; grid headers
mirror to Arabic (الرمز / الاسم (إنجليزي) / الاسم (عربي) / الشركة / القطاع / القاعة /
نشط). The `العربية` / `English` toggle sets `<html dir="rtl" lang="ar">`; the nav rail
mirrors, the toolbar + pager reverse, and the `CrudShell` form mirrors with the
form-action buttons in reversed order. The exact resx literals for each key are owned
by the resource files, not duplicated here.

## 10. Use cases

- Maintain the public Exhibition booths list + 2D venue map (Mockup page 22) per
  SIMF-FDS-004 / D-199; D-222 added the Booth → Exhibitor company link + booth-officer
  contact. _(UCS detail entries to be authored under the UCS expansion follow-up.)_

## 11. E2E

See [`docs/tests/e2e/cp-admin-booths.md`](../../tests/e2e/cp-admin-booths.md):

| Coverage | Scenario ids |
|----------|--------------|
| Golden CRUD round-trip (Add → Edit → Deactivate) | E2E-BTH-001 |
| Empty / Add / Edit / Delete / Cancel | E2E-BTH-002…006 |
| Exhibitor + Hall dropdown filtering + grid resolution | E2E-BTH-007/008 |
| Auth gate (page) | E2E-BTH-009 |
| Client / server validation, duplicate, not-found, server-500 | E2E-BTH-010…014 |
| RTL render | E2E-BTH-015 |
| Per-column filter + column sort | E2E-BTH-016/017 |
| Presentation toggle persists (D-353) | E2E-BTH-018 |
| Full-page round-trip (D-353) | E2E-BTH-019 |
| Delete confirmation gate — CrudShell + SimfConfirm (D-353) | E2E-BTH-020 |
| Excel export (D-356) | E2E-BTH-021 |
| Excel import + FK resolution + import rejection (D-356) | E2E-BTH-022/023 |

## 12. Related docs

- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) (CRUD pages).
- Sibling reference docs: [`admin-sponsors.md`](admin-sponsors.md), [`admin-themes.md`](admin-themes.md).
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `ApiResult<T>` envelope + admin grid endpoints.
- Authority spec: SIMF-FDS-004 (Exhibition module), SIMF-FDS-014 / D-283 (shared Contact link).
- Decisions log: D-199 (Booths module), D-222 (Booth → Exhibitor + booth-officer contact),
  D-281/D-283 (shared Contact link), D-353 (uniform CrudShell + Page/Popup toggle +
  SimfConfirm delete), D-356 (Excel export/import) in
  [`DECISIONS_LOG.md`](../../decisions/DECISIONS_LOG.md).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-10 | D-356 / D-353 | Reference doc created. Documents the D-353 `CrudShell` Add/Edit (`BoothsAddEdit`) + View/Delete (`BoothsViewDelete`) forms with the Page ↔ Popup `CrudPresentationToggle` (PageKey `booths`, persisted in `localStorage`) and the `SimfConfirm`-gated delete (replacing the old inline modal + native `confirm()`), plus the D-356 Excel export (`POST /export`, columns Code/Name/NameArabic/Exhibitor[English name]/Sector/Hall[Code]/IsActive) and insert-only import (`POST /import`, Exhibitor-by-name + Hall-by-code resolution) via `CrudGridExcel`. |
| 2026-06-02 (orig) | D-199 / D-222 | Booths admin CRUD shipped (Mockup page 22 + 2D venue map); D-222 added the Booth → Exhibitor company link + booth-officer name/phone/email. |

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5).
