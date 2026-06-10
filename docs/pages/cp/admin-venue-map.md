# Venue map — `/admin/venue-map`

| | |
|--|--|
| **Route** | `/admin/venue-map` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(PermissionCatalog.VenueMap.View)]` (page) + `RequireApprovedAccount`; mutations + Excel additionally `RequireRateLimiting("auth")` |
| **Pattern** | D-230 2D venue-map editor + **D-353 CrudShell dialog/full-page framing + SimfConfirm delete** + **D-356 uniform CRUD Excel export + import** |
| **Status** | ✅ Real (D-230; D-353 framing + D-356 Excel, 2026-06-10) |
| **Implements use case(s)** | FR-605 / SIMF-FDS-006 §5.3, §7 — place the 2D venue-map nodes the Flutter app renders |
| **Backend endpoints** | `POST /admin/venue-map/list`, `GET /admin/venue-map/{id}`, `POST /admin/venue-map`, `PUT /admin/venue-map/{id}`, `DELETE /admin/venue-map/{id}`, `POST /admin/venue-map/export`, `POST /admin/venue-map/import` (public read: `GET /app/venue-map`) |
| **Source file** | [`VenueMapList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VenueMapList.razor), [`VenueMapAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VenueMapAddEdit.razor), [`VenueMapViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VenueMapViewDelete.razor) |
| **Backed by** | `dbo.VenueMapNodes` (migration `App/D230`, `VenueMapNodeKind` enum). API: [`VenueMapEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/VenueMapEndpoints.cs), [`VenueMapExcelEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/VenueMapExcelEndpoints.cs), [`VenueMapService`](../../../src/Backend/SIMF.Infrastructure/Venue/VenueMapService.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-venue-map.md`](../../tests/e2e/cp-admin-venue-map.md); API: `tests/SIMF.Api.Tests/VenueMapTests.cs`, `tests/SIMF.Api.Tests/VenueMapExcelTests.cs` |
| **Last reviewed** | 2026-06-10 |

---

## 1. Purpose

This is the Logistics team's editor for the **2D venue map** the Flutter app
renders (SIMF-FDS-006 §5.3/§7, FR-605). Each grid row is a **node**: a bilingual
label, a `Kind` (Hall / Zone / Booth / PointOfInterest), a 2D position (`X`, `Y`
as `double`, step `0.1`) and an **optional** link to a Hall **or** a Booth. The
table **ships empty** — the team places the nodes — so the empty-state path is the
default first render. The app reads the active nodes through the public
`GET /app/venue-map` and draws them on its 2D canvas. As of D-353 the Add / Edit /
View / Delete forms are framed by `CrudShell` (popup or full page, the admin's
choice), and as of D-356 the grid carries Excel **export + import**.

## 2. Audience + permissions

- **Who can reach it:** an admin whose role carries `VenueMap.View` (or the
  wildcard Administrator `"*"`). The nav-rail item (`Module.VenueMap`,
  `CpNavigation.cs`) sets `RequiredPermission: PermissionCatalog.VenueMap.View`.
- **Who can write on it:** `VenueMap.Create` (Add / import), `VenueMap.Edit`
  (Edit), `VenueMap.Delete` (Delete), `VenueMap.Export` (Export), `VenueMap.Import`
  (Import) — all `AdminOnly` baseline in `PermissionCatalog.All`.
- **Authorisation gates:**
  - Page: `@attribute [RequirePermission(PermissionCatalog.VenueMap.View)]`.
  - API: each endpoint is `Policies(PermissionCatalog.PolicyFor(VenueMap.{View|Create|Edit|Delete|Export|Import}), nameof(AuthorizationPolicies.RequireApprovedAccount))`; create/update/delete + export/import also `RequireRateLimiting("auth")`.
  - BFF: the Control Panel reaches the API through the `/account/api/admin/venue-map/*` passthroughs in `AccountEndpoints.cs` (`MapGridExcel(group, "venue-map")` wires both `/export` and `/import`).
- **What an unauthenticated/under-permissioned user sees:** an admin lacking
  `VenueMap.View` is routed to `/not-permitted`; the nav item is hidden. A
  view-only admin sees the grid but the Add / Edit / Details / Delete / Export /
  Import affordances gated by the missing action permission are not rendered.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-venue-map-default.png` | _to capture_ |
| Empty state | `docs/screenshots/cp-admin-venue-map-empty.png` | _to capture_ |
| Add form (dialog) | `docs/screenshots/cp-admin-venue-map-add-modal.png` | _to capture_ |
| Add form (full page) | `docs/screenshots/cp-admin-venue-map-add-page.png` | _to capture_ |
| Delete confirm (SimfConfirm) | `docs/screenshots/cp-admin-venue-map-delete-confirm.png` | _to capture_ |
| Import result modal | `docs/screenshots/cp-admin-venue-map-import-result.png` | _to capture_ |
| RTL (Arabic) | `docs/screenshots/cp-admin-venue-map-rtl.png` | _to capture_ |

## 4. UI affordances

### 4.1 Banner / page header
`<SimfBanner Title="@L["Admin.VenueMap.Title"]" />` ("Venue map" / "خريطة المكان").
The banner + grid are hidden when a form is open in full-page mode
(`GridHidden => FormOpen && _presentation == CrudPresentation.Page`).

### 4.2 Toolbar
| Affordance | Wired callback | Calls | Notes |
|--------|----------------|-------|-------|
| Select all / row checkbox | `Multiselect="true"` | — | `RowKey = Id`, `RowLabel = Label` |
| Add ("New node") | `OnAdd` → `OnAddAsync` | opens `CrudShell` → `VenueMapAddEdit` (IsEdit=false) | gated `VenueMap.Create` |
| Edit (per-row) | `OnEditOne` → `OnEditAsync` | `GET /{id}` then `VenueMapAddEdit` (IsEdit=true) | gated `VenueMap.Edit` |
| Details (per-row) | `OnDetailsOne` → `OnDetailsAsync` | `GET /{id}` then `VenueMapViewDelete` (IsDelete=false) | read-only |
| Delete (per-row) | `OnDeleteOne` → `OnDeleteAsync` | `GET /{id}` then `VenueMapViewDelete` (IsDelete=true) → `DELETE /{id}` | gated `VenueMap.Delete`; SimfConfirm |
| Export | `OnExport` → `OnExportAsync` | `_excel.ExportAsync(ids, _query)` → `POST /export` | gated `VenueMap.Export` |
| Import | `OnImport` → `OnImportAsync` | `_excel.TriggerImportAsync()` → `POST /import` | gated `VenueMap.Import` |
| Presentation toggle | `CrudPresentationToggle PageKey="venue-map" @bind-Value="_presentation"` | localStorage | D-353; in `<CustomToolbar>` |

`<CrudGridExcel @ref="_excel" Resource="venue-map" OnImported="OnImportedAsync" OnError="OnExcelError" />`
renders below the grid and owns the file `<input>` (id `venue-map-import-input`,
`accept=".xlsx"`) and the import-result modal.

### 4.3 Grid columns
| Column | Source field | Sortable | Filterable | Notes |
|--------|--------------|----------|------------|-------|
| Label | `r.Label` | yes | yes | per-column filter `Filters["label"]` |
| Kind | `r.Kind` | yes | no | `VenueMapNodeKind` enum name |
| Position (x, y) | `r.X`, `r.Y` | no | no | rendered `X("0.#"), Y("0.#")` |
| Active | `r.IsActive` | no | no | green "Active" / grey "Inactive" `SimfPill` |

Empty state: `<SimfEmptyState Title="@L["Admin.VenueMap.None"]" />`
("No venue-map nodes yet." / "لا توجد عقد على الخريطة بعد.").

### 4.5 Form fields (`VenueMapAddEdit`, inherits `CrudAddEditFormBase<AdminVenueMapNodeDetail>`)
| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| Label (English) | text | yes | 128 | 1–128 chars (client + server) | `Admin.VenueMap.Field.Label` |
| Label (Arabic) | text | yes | 128 | 1–128 chars | `Admin.VenueMap.Field.LabelArabic` |
| Kind | `<select>` | yes | — | one of the four `VenueMapNodeKind` (default `Hall`=0) | `Admin.VenueMap.Field.Kind` |
| X position | number (step 0.1) | no | — | parsed double (defaults 0) | `Admin.VenueMap.Field.X` |
| Y position | number (step 0.1) | no | — | parsed double (defaults 0) | `Admin.VenueMap.Field.Y` |
| Linked hall (optional) | `<select>` | no | — | a Hall guid or "— None —" | `Admin.VenueMap.Field.Hall` |
| Linked booth (optional) | `<select>` | no | — | a Booth guid or "— None —" | `Admin.VenueMap.Field.Booth` |
| Active | checkbox | (Edit only) | — | bool | `Admin.VenueMap.Field.IsActive` |

The Hall + Booth pickers are loaded at mount via
`POST /account/api/admin/halls/list` and `POST /account/api/admin/booths/list`
(`Top=500`). `VenueMapViewDelete` (inherits `CrudViewDeleteFormBase`) renders the
detail list read-only and resolves the Hall/Booth link names on demand (it only
fetches the lookup that the node actually links to).

## 5. Data flow

```
Toolbar / row action → VenueMapList event handler → JS interop (simfAccount.*)
   → BFF /account/api/admin/venue-map/* → API /admin/venue-map/* (FastEndpoints)
   → VenueMapService → SimfAppDbContext (dbo.VenueMapNodes)
   → ApiResult<T> envelope → grid reload + SimfAlert toast
```

| When | Method + path (BFF) | Request body | Response shape |
|------|---------------------|--------------|----------------|
| OnInit / query change | `POST /account/api/admin/venue-map/list` | `GridQuery` | `ApiResult<GridPage<AdminVenueMapNodeSummary>>` |
| Edit / Details / Delete open | `GET /account/api/admin/venue-map/{id}` | — | `ApiResult<AdminVenueMapNodeDetail>` |
| Add Save | `POST /account/api/admin/venue-map` | `AdminCreateVenueMapNodeRequest` | `ApiResult<AdminVenueMapNodeDetail>` |
| Edit Save | `PUT /account/api/admin/venue-map/{id}` | `AdminUpdateVenueMapNodeRequest` (+ `IsActive`) | `ApiResult<AdminVenueMapNodeDetail>` |
| Delete confirm | `DELETE /account/api/admin/venue-map/{id}` | — | `ApiResult<bool>` |
| Export | `POST /account/api/admin/venue-map/export` | `AdminGridExportRequest { Ids, Query }` | binary `.xlsx` (`simf-venue-map-{ts}.xlsx`) |
| Import | `POST /account/api/admin/venue-map/import` | multipart (`file`) | `ApiResult<AdminGridImportResult>` |
| Picker mount | `POST /account/api/admin/halls/list`, `POST /account/api/admin/booths/list` | `GridQuery { Top = 500 }` | `ApiResult<GridPage<Admin{Hall|Booth}Summary>>` |

**Export workbook** — sheet `VenueMap`, header row:
`Label | LabelArabic | Kind | X | Y | Hall | Booth | IsActive`. Hall/Booth are
written as their human-readable **code** (resolved once per request from the
active hall/booth lists; an unresolved/deactivated link yields an empty cell).
With no rows selected the export covers the whole filtered grid; with rows
selected only those Ids. Capped at **5000** rows (`MaxExportRows`).

**Import** is **insert-only** — required headers `Label`, `LabelArabic`, `Kind`;
Kind is parsed from its enum name (or raw int); Hall/Booth resolve by code (blank
= unset). Each applied row counts as **Created**; the result modal shows
"{Created} created, {Updated} updated, {Skipped} skipped." plus a per-row error
list. Capped at **5000** rows (`MaxImportRows`).

## 6. Validation + error handling

- **Client-side guards (`VenueMapAddEdit`):** both labels required
  (`Admin.VenueMap.Required` — "Both labels are required."); the request trims
  labels and parses the optional Hall/Booth guids.
- **Server-side (`VenueMapService`):** `ValidateLabels` enforces 1–128 chars on
  both labels; `EnsureReferencesAsync` rejects a Hall/Booth link that is not an
  active record.
- **Error codes:**
  - `400 VENUE_MAP_NODE_INVALID` — blank/over-length labels or an unknown/inactive Hall or Booth link ("The referenced hall was not found." / "لم يتم العثور على القاعة المرتبطة.").
  - `404 VENUE_MAP_NODE_NOT_FOUND` — `GET`/`PUT`/`DELETE` on a missing node ("The venue-map node was not found." / "لم يتم العثور على عقدة الخريطة.").
- **Import upload defence (`AdminGridImportEndpoint`):** `400` on a non-`.xlsx`
  (ZIP-magic `50 4B 03 04` fail) or a wrong/missing `VenueMap` sheet / missing
  required header; `413 AdminImportEmpty` on a file over 5 MB; one bad row is
  recorded as a per-row error and never aborts the batch.
- **Toast strategy:** success → `Admin.VenueMap.Saved` / `.Deleted`,
  import → `Grid.Import.Done` ("Import complete." / "اكتمل الاستيراد.");
  load failure → `Admin.VenueMap.LoadFailed`; form failure → `Admin.VenueMap.Fallback`;
  Excel error → `OnExcelError` red toast.

## 7. Edge cases + known limitations

- **Ships empty.** The table is empty until the Logistics team places nodes, so
  the `SimfEmptyState` is the first render.
- **Soft delete.** `DELETE` calls `node.Deactivate()` (sets `IsActive=false`); a
  deactivated node drops out of the public `GET /app/venue-map`. A second delete
  on an already-inactive node still returns `200` (idempotent).
- **Stale link.** A Hall/Booth deactivated after the picker loaded is rejected on
  save with `400 VENUE_MAP_NODE_INVALID`; on export it renders an empty Hall/Booth
  cell rather than a dangling id.
- **Edit/Details/Delete fetch the full detail** (`LoadDetailAsync`) before opening
  the form — editing from a summary-only model would be lossy.
- **Presentation persistence (D-353).** The dialog/full-page choice is stored per
  page in `localStorage["simf.cp.prefs.venue-map"]` as `{"V":1,"Presentation":"page"}`;
  it defaults to dialog when unset. Pure browser storage — no server state, no
  schema (respects the D-110 freeze).
- **Import is insert-only.** It cannot update or deactivate an existing node;
  there is no dedup against existing labels (each row is a Created). Use Edit /
  Delete for changes.
- **EN resx gap (defect, out of scope here).** Several D-353 keys
  (`Admin.VenueMap.Delete.Title`, `.Delete.Message`, `.Details.Title`,
  `.Details.Close`, `.New.Submit`, `.New.Submitting`, `.Edit.Submit`,
  `.Edit.Submitting`, `.Fallback`, `.Col.Hall`, `.Col.Booth`) exist in
  `Strings.ar.resx` but are **missing from `Strings.resx` (EN)** — the EN UI falls
  back to the resource key name for those. Reported for a follow-up fix; not
  changed here.

## 8. i18n + RTL

All visible strings come from `Admin.VenueMap.*` + `Grid.*` keys via
`IStringLocalizer<Strings> L`. The `العربية` / `English` header link toggles
culture; RTL sets `<html dir="rtl" lang="ar">`, mirrors the nav rail, flips the
table headers (التسمية / النوع / الموضع / مُفعّل) and reverses the form action
order. (See the EN-gap caveat in §7.)

## 9. Accessibility

- Keyboard: the grid filter/sort headers and per-row actions are focusable;
  `CrudShell` traps focus while a form is open and the SimfConfirm requires an
  explicit Confirm/Cancel (no backdrop dismiss).
- Screen reader: `SimfDataGrid` carries `Caption="@L["Admin.VenueMap.Title"]"`;
  sortable headers expose `aria-sort`.
- Colour contrast + focus ring: WCAG AA via `theme.tokens.css` / `--focus-ring`.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| FR-605 | Place 2D venue-map nodes | SIMF-FDS-006 §5.3/§7; rendered by the app's 2D canvas |

## 11. Related E2E test scenarios

| Scenario | File | Coverage |
|----------|------|----------|
| Full CRUD round-trip | [`cp-admin-venue-map.md`](../../tests/e2e/cp-admin-venue-map.md) | E2E-VMP-001 |
| Empty state | same | E2E-VMP-009 |
| Auth + action gates | same | E2E-VMP-010, E2E-VMP-011 |
| Validation + server errors | same | E2E-VMP-012..015 |
| RTL render | same | E2E-VMP-016 |
| Presentation toggle persists (D-353) | same | E2E-VMP-019 |
| Full-page mode round-trip (D-353) | same | E2E-VMP-020 |
| Delete confirmation gate — CrudShell + SimfConfirm (D-353) | same | E2E-VMP-021 |
| Excel export (D-356) | same | E2E-VMP-022 |
| Excel import + per-row error (D-356) | same | E2E-VMP-023 |
| Excel import rejection (D-356) | same | E2E-VMP-024 |

## 12. Related docs

- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md)
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md)
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `ApiResult<T>` envelope + error model
- Auth/permissions: [`SIMF-Auth-Permissions-Dev-Guide.md`](../../manuals/SIMF-Auth-Permissions-Dev-Guide.md)
- Decisions: D-230 (venue-map nodes), D-353 (CrudShell dialog/full-page + SimfConfirm), D-356 (uniform CRUD Excel)
- Source: [`VenueMapList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VenueMapList.razor)

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-02 | D-230 (P2.5) | Original — 2D venue-map node CRUD page (`dbo.VenueMapNodes` + `VenueMapNodeKind`). |
| 2026-06-09 | D-353 | Add/Edit/View/Delete moved into `CrudShell` (dialog or full page, persisted per page); native `confirm()` delete replaced by `VenueMapViewDelete` + `SimfConfirm`. |
| 2026-06-10 | D-356 | Excel export + import wired via `CrudGridExcel` (sheet `VenueMap`, 5000-row caps); reference doc created. |

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5).
