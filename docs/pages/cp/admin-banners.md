# Banners — `/admin/banners`

| | |
|--|--|
| **Route** | `/admin/banners` |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(PermissionCatalog.Banners.View)]` (CP page) + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (API mutations) |
| **Pattern** | D-173 dynamic CMS (time-windowed banners). D-256 `SimfDataGrid` migration. D-353 `CrudShell` framing + `SimfConfirm` delete. D-356 Excel export + import. |
| **Status** | ✅ Real (D-173; grid D-256; forms D-353; Excel D-356) |
| **Backend endpoints** | **BFF** (`/account/api`): `POST /admin/banners/list`, `GET /admin/banners/{id}`, `POST /admin/banners`, `PUT /admin/banners/{id}`, `DELETE /admin/banners/{id}`, `POST /admin/banners/export`, `POST /admin/banners/import` → **API** (`/api/v1`): `POST /admin/banners/list`, `GET /admin/banners/{id:guid}`, `POST /admin/banners`, `PUT /admin/banners/{id:guid}`, `DELETE /admin/banners/{id:guid}`, `POST /admin/banners/export`, `POST /admin/banners/import` |
| **Source** | [`BannersList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BannersList.razor), [`BannersAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BannersAddEdit.razor), [`BannersViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BannersViewDelete.razor), [`CmsEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/CmsEndpoints.cs), [`BannersExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/BannersExcelEndpoints.cs), [`AdminCmsService`](../../../src/Backend/SIMF.Infrastructure/Cms/AdminCmsService.cs), [`Cms.cs` contracts](../../../src/Shared/SIMF.Contracts/Admin/Cms.cs) |
| **Backed by** | `dbo.Banners` table (`Banner` entity on `SimfAppDbContext`). |
| **Tests** | [`docs/tests/e2e/cp-admin-banners.md`](../../tests/e2e/cp-admin-banners.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

Dynamic CMS for time-windowed banners / announcements (D-173, gap doc G8,
PDF §1). The administrator publishes banners with a bilingual title + body, an
optional image and click-through link, a start/end UTC window and a display
order; the public Website and Flutter app read only the **active** banners
whose current time falls inside the window. Each banner carries a bilingual
`Title`/`TitleArabic`, `Body`/`BodyArabic`, optional `ImageUrl` + `LinkUrl`,
`Start`, `End`, `DisplayOrder` and an `IsActive` flag.

The page follows the owner-mandated `SimfDataGrid` list standard (D-256) and
the centralised `CrudShell` form framing (D-353); the D-356 wave added the
Excel export + import pair shared across every grid.

## 4. UI

- `SimfBanner` heading + `SimfDataGrid` (`Multiselect="true"`, server-paged,
  `GridQuery { Top = 20 }`).
- Grid columns: **Title** (bilingual — `TitleLabel` shows the Arabic title
  when the culture is `ar`), **Start (Saudi time)** (`yyyy-MM-dd HH:mm`), **End (Saudi time)**
  (`yyyy-MM-dd HH:mm`), **Display order**, **Active** (`SimfPill` on/off). All
  five columns are sortable; **Title** is the only filterable column.
- Toolbar actions: **Add**, plus a `CustomToolbar` hosting the
  `CrudPresentationToggle`. Per-row quiet icon actions: **Edit** (pencil),
  **Details** (eye), **Delete** (trash) — wired to `OnAdd`, `OnEditOne`,
  `OnDetailsOne`, `OnDeleteOne`. The grid also exposes `OnExport` / `OnImport`.
- Empty list renders `SimfEmptyState` (`Admin.Banners.None`).
- Add/Edit hosts the reusable **`BannersAddEdit`** form; Details/Delete hosts
  **`BannersViewDelete`** (read-only details, including an `<img>` preview of
  the optional image asset).
- **Excel export + import (D-356):** the grid's **Export** and **Import**
  actions are wired to the reusable **`CrudGridExcel`** component
  (`Resource="banners"`). Export posts the selected row ids (or none → the whole
  filtered grid) with the current `GridQuery` to `/account/api/admin/banners/export`
  and downloads an `.xlsx` named `simf-banners-{yyyyMMddHHmmss}.xlsx` whose sheet
  **"Banners"** header row is `Title | TitleArabic | Start | End |
  DisplayOrder | IsActive`. Import (insert-only) posts an `.xlsx` to
  `/account/api/admin/banners/import` and shows a result of created/updated/skipped
  counts plus per-row errors; a green `Grid.Import.Done` toast follows and the
  grid reloads. The import required headers are `Title | TitleArabic | Body |
  BodyArabic | Start | End` (`ImageUrl`, `LinkUrl`, `DisplayOrder` are
  optional). Export is capped at **5000** rows; import is capped at **5000**
  rows with a **5 MB** upload limit and a ZIP-magic `.xlsx` check.
- **Page ↔ Popup presentation toggle (D-353):** the toolbar
  `CrudPresentationToggle` (`PageKey="banners"`) lets the admin host
  Add/Edit/View/Delete as a dialog or a full page; the choice persists in
  `localStorage` (read back via `CpPreferences.GetPresentationAsync("banners")`
  in `OnInitializedAsync`, defaulting to `Dialog`). In full-page mode the grid
  + banner are hidden (`GridHidden = FormOpen && presentation == Page`).

## 4.5 Form fields

`BannersAddEdit` renders these fields in order; `IsEdit=false` runs Create
(POST), `IsEdit=true` runs Edit (PUT). Validation limits below are enforced
server-side in `AdminCmsService.ValidateBanner`.

| Field | Required | Limit | Validation |
|-------|----------|-------|------------|
| Title (English) | yes | 1–256 | non-blank; ≤ 256 chars |
| Title (Arabic) | yes | 1–256 | non-blank; ≤ 256 chars |
| Body (English) | yes | 1–2000 | non-blank; ≤ 2000 chars |
| Body (Arabic) | yes | 1–2000 | non-blank; ≤ 2000 chars |
| Click-through URL | no | — | optional; `NullIfBlank` |
| Start (Saudi time) | yes | n/a | `datetime-local`; defaults to now |
| End (Saudi time) | yes | n/a | `datetime-local`; defaults to now + 1 day; must be **after** Start |
| Display order | yes | n/a | `number`; integer ≥ 0 (client coerces a bad/negative parse to 0; server rejects negatives) |
| Active | (Edit only) | bool | `IsActive` checkbox, shown only when `IsEdit` |

The Submit button label is `Admin.Banners.Submit`. The client form does a
light pre-check (blank EN/AR title or unparseable dates → `Admin.Banners.Required`)
and otherwise relies on the server validation below.

## 5. Data flow + endpoints

The CP page calls the BFF through `simfAccount.postJson` / `getJson` /
`putJson` / `deleteJson`; the BFF (`AccountEndpoints.cs`, `MapGridExcel(group,
"banners")` for the Excel pair) forwards to the API `CmsEndpoints`. Each leg
returns the standard `ApiResult<T>` envelope.

- `POST /admin/banners/list` → `ApiResult<GridPage<AdminBannerSummary>>`
  (gated `Banners.View`). Default order: `DisplayOrder` asc, then `Start`
  asc; per-column **Title** filter matches `TitleEn || TitleAr`.
- `GET /admin/banners/{id}` → `ApiResult<AdminBannerDetail>` (gated
  `Banners.View`); 404 `BANNER_NOT_FOUND` when missing.
- `POST /admin/banners` → `ApiResult<AdminBannerDetail>` (gated `Banners.Create`).
- `PUT /admin/banners/{id}` → `ApiResult<AdminBannerDetail>` (gated `Banners.Edit`).
- `DELETE /admin/banners/{id}` → `ApiResult<bool>` (gated `Banners.Delete`);
  soft-delete via `DeactivateBannerAsync` (`IsActive = false`).
- `POST /admin/banners/export` (gated `Banners.Export`) — `AdminGridExportRequest
  { Ids, Query }` → binary `.xlsx`.
- `POST /admin/banners/import` (gated `Banners.Import`) — multipart `.xlsx` →
  `ApiResult<AdminGridImportResult>`.

Each create/update/deactivate writes an audit event: `Banner.Created`,
`Banner.Updated`, `Banner.Deactivated` (carrying the actor's user id).

## 6. Validation + error handling

Server-side in `AdminCmsService.ValidateBanner` (shared by create + update):

- **Title (EN + AR):** non-blank and ≤ 256 chars, else 400 `BANNER_INVALID`
  ("Banner title (EN + AR) must be between 1 and 256 characters." /
  "يجب أن يتراوح طول العنوان (إنجليزي + عربي) بين 1 و 256 حرفاً.").
- **Body (EN + AR):** non-blank and ≤ 2000 chars, else 400 `BANNER_INVALID`
  ("Banner body (EN + AR) must be between 1 and 2000 characters." / Arabic).
- **Time window:** `End` must be strictly after `Start`, else 400
  `BANNER_INVALID_TIME_WINDOW` ("Banner end must be after its start." /
  "يجب أن تكون نهاية البانر بعد بدايته.").
- **Display order:** must be ≥ 0, else 400 `BANNER_INVALID` ("Display order
  must be zero or a positive integer." / Arabic).
- **Not found:** 404 `BANNER_NOT_FOUND` ("Banner not found." /
  "لم يتم العثور على البانر.") on get/update/delete of a missing id.
- **Import per-row errors** (`ImportBannersEndpoint`): each of Title,
  TitleArabic, Body, BodyArabic must be non-blank and Start/End must
  parse, else a bilingual `DataValidationException` is recorded as a per-row
  error (the row is skipped, the batch continues). The same `ValidateBanner`
  rules then run inside `CreateBannerAsync`.
- **Import upload defence** (`AdminGridImportEndpoint`): empty file →
  `DataValidationException`; > 5 MB → 413 `ADMIN_IMPORT_EMPTY`; non-ZIP-magic
  bytes → `DataValidationException` ("The file is not a valid Excel workbook.");
  a wrong sheet name / missing required header is rejected by the importer.

## 7. Edge cases + known limitations

- **Public read filters the window.** The admin grid lists all banners
  (active + inactive); the public surface (`PublicCmsEndpoints`) shows only
  active banners inside the current `Start`/`End` window.
- **Delete is a soft-delete (deactivate).** `DeactivateBannerAsync` sets
  `IsActive = false`; an already-inactive banner stays in the admin grid but
  drops out of the active public read.
- **Client display-order coercion.** `BannersAddEdit` coerces a non-integer or
  negative `Display order` to `0` before posting; an explicit negative therefore
  never reaches the server through the form, but the server guard still rejects
  one (e.g. via the API or import).
- **Excel import is insert-only.** Every imported row is created; there is no
  update-by-key path, so a `RowKey` (the Title) only labels per-row errors.

## 8. i18n + RTL

`Admin.Banners.*` resx keys (Title, None, Loading, LoadFailed, Saved, Deleted,
Summary, `Col.*`, `Field.*`, `Add.Title`, `Edit.Title`, `Details.Title`,
`Details.Close`, `Delete.Title`, `Delete.Message`, `Action.Deactivate`,
`Submit`, `Cancel`, `Required`) plus the shared `Grid.*` keys. English ↔ Arabic
parity is maintained; the grid Title column and the delete-confirm message use
the culture-appropriate title. The Arabic toast/label phrasing is provided
through the resx resources and renders RTL when the UI culture is `ar`.

## 10. Use cases

- UC-BNR-CREATE-001, UC-BNR-EDIT-001, UC-BNR-DEACTIVATE-001 — publish, amend
  and retire a time-windowed announcement (per FR coverage of the dynamic CMS,
  PDF §1 / gap doc G8).

## 11. E2E

See [`docs/tests/e2e/cp-admin-banners.md`](../../tests/e2e/cp-admin-banners.md):
E2E-BNR-001 full CRUD round-trip, 002 empty list, 003 auth gate, 004–008
validation (blank title / blank body / End ≤ Start / negative order / bad-date
client guard), 009 edit read-back, 010 delete, 011 edit not-found, 012
display-order sort, 013 server-500 fallback, 014 RTL, 015 Title filter, 016
column sort, 017 presentation toggle persists (D-353), 018 full-page round-trip
(D-353), 019 delete confirmation gate (`CrudShell` + `SimfConfirm`, D-353),
020 Excel export, 021 Excel import, 022 Excel import rejection (D-356).

## 12. Related docs

- E2E catalogue: `docs/tests/e2e/cp-admin-banners.md`.
- Permission catalogue: `PermissionCatalog.Banners.*`
  (View/Create/Edit/Delete/Export/Import), all `AdminOnly`.
- Navigation: `CpNavigation` item `Module.Banners`
  (`RequiredPermission = PermissionCatalog.Banners.View`, icon `image`).
- Decisions: D-173 (dynamic CMS), D-256 (`SimfDataGrid` migration),
  D-353 (`CrudShell` + `SimfConfirm` + presentation toggle), D-356 (Excel
  export + import).
- Sibling CMS / grid pages: Content blocks, Media partners, News.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-08-14 | D-889 | **The free-text Image URL is gone; the hero image is a typed key.** `Banner.ImageUrl` becomes `Guid? ImageFileId` with a real foreign key into `StoredFiles`. A pasted URL used to load straight into the Flutter app's home hero carrying no media type, no sensitivity tier and no permission entry; an externally hosted image is now a `StoredFile` of source type `ExternalLink` instead. The workbook loses its ImageUrl column and ignores a stale one. |
| 2026-06-11 | D-356 / D-353 | Reference doc authored. Documents the shipped Banners page: `SimfDataGrid` list (D-256), `CrudShell`-hosted `BannersAddEdit` / `BannersViewDelete` forms with a `SimfConfirm`-gated soft-delete and a Page↔Popup presentation toggle persisted in `localStorage` (D-353), and the Excel export + import pair (D-356, sheet "Banners", 5000-row cap, 5 MB upload limit). |

_Last reviewed:_ 2026-06-11 by Claude (D-356 ref-doc backfill).
