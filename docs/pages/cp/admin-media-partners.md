# Media partners — `/admin/media-partners`

| | |
|--|--|
| **Route** | `/admin/media-partners` |
| **Audience** | Administrator (any role granted `MediaPartners.*`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.MediaPartners.View)]` (CP page) + per-action API policies (`MediaPartners.View/Create/Edit/Delete/Export/Import`) + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (mutations, export, import) |
| **Pattern** | D-199 admin CRUD over the public media-partner grid. D-256 raw-table → `SimfDataGrid`. D-353 `CrudShell` Page↔Popup framing. D-356 Excel export + import. |
| **Status** | ✅ Real (D-199; D-256 grid; D-353 framing; D-356 Excel) |
| **Backend endpoints** | BFF `/account/api/admin/media-partners/*` → API `/api/v1/admin/media-partners/*`: `POST .../list`, `GET .../{id}`, `POST .../` (create), `PUT .../{id}`, `DELETE .../{id}`, plus the D-356 pair `POST .../export` and `POST .../import`. Public read: anonymous `GET /api/v1/app/media-partners`. |
| **Source** | [`MediaPartnersList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/MediaPartnersList.razor), [`MediaPartnerEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/PublicRelations/MediaPartnerEndpoints.cs), [`MediaPartnersExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/MediaPartnersExcelEndpoints.cs), [`AdminMediaPartnerService`](../../../src/Backend/SIMF.Infrastructure/PublicRelations/AdminMediaPartnerService.cs), [`MediaPartner`](../../../src/Backend/SIMF.Domain/PublicRelations/MediaPartner.cs) |
| **Backed by** | `dbo.MediaPartners` on `SimfAppDbContext` (additive D-199 table). |
| **Tests** | [`docs/tests/e2e/cp-admin-media-partners.md`](../../tests/e2e/cp-admin-media-partners.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

Admin CRUD over the public list of **media partners** ("شركاء النجاح" /
"شركاء وسائل الإعلام", Mockup page 31) — the partner cards the mobile app
renders in its media-partner grid. Each partner carries a bilingual name
(English + Arabic), an optional logo path, an optional outbound link, a
display-order sort key, an optional shared-`Contact` link (`ContactId`,
SIMF-FDS-014 / D-260), and the standard active flag.

The public list (`GET /app/media-partners`, anonymous) returns only **active**
partners ordered by `DisplayOrder` ascending, tie-broken by `NameArabic`
ascending. Admins manage every field here; the public projection prefers the
linked `Contact` when one is set.

## 4. UI

- `SimfBanner` titled `Admin.MediaPartners.Title` + a `SimfDataGrid`
  (`Top = 20`, `Multiselect="true"` — select-all + per-row checkboxes are
  present; selection drives Export but there is no bulk-delete toolbar button).
- Grid columns (live keys): **Name** `name`, **Name (Arabic)** `namearabic`,
  **Logo** `logo` (path text, "—" when blank), **Link** `url`
  ("—" when blank), **Display order** `displayorder`, **Active** `isActive`
  (rendered as a `SimfPill` — `Grid.Active` "Active" / `Grid.Inactive`
  "Inactive", not a tick/dash).
- **Filterable columns** (`Filterable="true"`): **Name** `name` and
  **Name (Arabic)** `namearabic` only. **Sortable columns** (`Sortable="true"`):
  `name`, `namearabic`, `displayorder`. Logo, Link and Active are neither
  filterable nor sortable.
- Toolbar actions are the grid's standard set wired on the grid: Add
  (`OnAdd`), Export (`OnExport`), Import (`OnImport`); per-row quiet **icon**
  actions are Edit (`OnEditOne`), Details (`OnDetailsOne`) and Delete
  (`OnDeleteOne`). Labels use the shared `Grid.*` resources
  (`Grid.Add`, `Grid.Edit`, `Grid.Details`, `Grid.Delete`, `Grid.Export`,
  `Grid.Import`).
- Empty grid renders `SimfEmptyState` titled `Admin.MediaPartners.None`.
- **Page ↔ Popup presentation toggle (D-353):** the grid's `<CustomToolbar>`
  hosts a `CrudPresentationToggle` (`PageKey = "media-partners"`) that lets the
  admin host Add/Edit/View/Delete as a dialog or a full page; the choice is
  read on init via `Prefs.GetPresentationAsync("media-partners")` and persists
  in `localStorage` under `simf.cp.prefs.media-partners` via `CpPreferences`.
  In Page mode the grid + banner are hidden (`GridHidden`) while the form takes
  over the content area.
- **CrudShell forms (D-353):** Add/Edit/View/Delete are framed by `CrudShell`
  (popup or full page per the toggle), hosting two reusable forms —
  `MediaPartnerAddEdit` (`IsEdit` true for edit) and `MediaPartnerViewDelete`
  (read-only **Details** when `IsDelete=false`; the gated **Delete** when
  `IsDelete=true`). The old inline `SimfModal` form + native `confirm()` it used
  to carry are gone (D-353). Edit/View/Delete first load the **full**
  `AdminMediaPartnerDetail` (`GET .../{id}`) because the grid summary omits
  `ContactId` (D-283), so editing from a summary-only row would wipe an existing
  Contact link.
- **Excel export + import (D-356):** the grid toolbar carries **Export** and
  **Import** actions wired to a `<CrudGridExcel Resource="media-partners" />`
  helper. Export (`OnExportAsync`) posts `AdminGridExportRequest { Ids, Query }`
  to `/account/api/admin/media-partners/export` — the selected row ids, else the
  whole filtered grid — and downloads `simf-media-partners-{yyyyMMddHHmmss}.xlsx`
  with the sheet **"MediaPartners"** and header row
  `Name | NameArabic | LogoRelativePath | Url | DisplayOrder | IsActive`. Import
  (`OnImportAsync` → `TriggerImportAsync`, insert-only) posts an `.xlsx` to
  `/account/api/admin/media-partners/import`; on success it raises the shared
  `Grid.Import.Done` toast and reloads the grid. A per-row duplicate English
  name is a per-row error, not a batch abort.

## 4.5 Form fields

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Name (English) | yes | 256 | 1–256 chars; unique (case-insensitive) |
| Name (Arabic) | yes | 256 | 1–256 chars |
| Logo path | no | 512 | relative path; "—" when blank |
| Link (URL) | no | 512 | optional; "—" when blank |
| Display order | yes | n/a | integer ≥ 0 |
| Active | (Edit only) | bool | Create always persists `IsActive = true`; Edit honours the flag |

> `ContactId` (the optional shared-`Contact` link, D-260) rides on the
> Update request and the detail projection; it is not a free-typed field on the
> grid.

## 5. Data flow + endpoints

Canonical D-256 grid shape. The CP page calls the BFF passthroughs in
`AccountEndpoints.cs` (each attaches the caller's `access_token` and forwards via
`SimfAdminClient`); the BFF forwards to the API:

| CP action | BFF route | API route | API gate |
|-----------|-----------|-----------|----------|
| List | `POST /account/api/admin/media-partners/list` | `POST /api/v1/admin/media-partners/list` | `MediaPartners.View` |
| Get one | `GET /account/api/admin/media-partners/{id}` | `GET /api/v1/admin/media-partners/{id}` | `MediaPartners.View` |
| Create | `POST /account/api/admin/media-partners` | `POST /api/v1/admin/media-partners` | `MediaPartners.Create` |
| Update | `PUT /account/api/admin/media-partners/{id}` | `PUT /api/v1/admin/media-partners/{id}` | `MediaPartners.Edit` |
| Deactivate | `DELETE /account/api/admin/media-partners/{id}` | `DELETE /api/v1/admin/media-partners/{id}` | `MediaPartners.Delete` |
| Export | `POST /account/api/admin/media-partners/export` | `POST /api/v1/admin/media-partners/export` | `MediaPartners.Export` |
| Import | `POST /account/api/admin/media-partners/import` | `POST /api/v1/admin/media-partners/import` | `MediaPartners.Import` |

The Excel pair is wired by `MapGridExcel(group, "media-partners")` in
`AccountEndpoints.cs`. The API service (`AdminMediaPartnerService.ListAllAsync`)
clamps `Top` to 1–500 (default 50; the page asks for 20), honours
`GridQuery.Search` (`Name`/`NameArabic` `LIKE`), per-column `Filters`
(`name`/`namearabic` `Contains`; `isactive` boolean), and
`Sort=name|namearabic|displayorder` with `SortDescending` (default
`DisplayOrder` asc, then `NameArabic` asc).

## 6. Validation + error handling

- **Server-side `AdminMediaPartnerService.Validate`:** trims English name
  (1–256), Arabic name (1–256), logo path (≤ 512, null when blank), URL
  (≤ 512, null when blank); `DisplayOrder` ≥ 0. Each failure → 400
  `VALIDATION_FAILED` with a bilingual message (e.g. "Media partner English
  name must be between 1 and 256 characters." / "يجب أن يتراوح الاسم الإنجليزي
  للشريك الإعلامي بين 1 و 256 حرفاً.").
- **Duplicate English name:** 409 `MEDIA_PARTNER_NAME_DUPLICATE`
  (case-insensitive; surfaces the name — "A media partner named '{name}' already
  exists." / "يوجد شريك إعلامي بالاسم '{name}' بالفعل."). Checked on Create and
  on an Update that changes the name.
- **Not found:** 404 `NOT_FOUND` ("The media partner was not found." / "لم يتم
  العثور على الشريك الإعلامي.") on Get/Update/Deactivate of an unknown id.
- **Invalid Contact link:** an Update/Create carrying a `ContactId` that does
  not exist or is inactive → 400 `VALIDATION_FAILED` (clean 400 instead of a DB
  FK 500, SIMF-FDS-014 / D-281).
- **Import upload defence (`AdminGridImportEndpoint`):** a missing file → 400
  bilingual "An Excel file is required."; over 5 MB → 413
  `ADMIN_IMPORT_EMPTY`-coded bilingual "too large" message; a non-`.xlsx`
  payload failing the ZIP-magic check → 400 bilingual "not a valid Excel
  workbook."; a wrong/missing worksheet or missing required header → the
  importer's bilingual parse error. Each individual bad data row is caught and
  recorded as a per-row error (the batch continues).
- **Load failure:** a non-success list/detail envelope surfaces a red toast
  (`Admin.MediaPartners.LoadFailed`, or the server error message when present).
  Save/Delete success raise `Admin.MediaPartners.Saved` / `.Deleted`.

## 7. Edge cases + known limitations

- **Create always sets `IsActive = true`.** The create request carries no
  active flag (`AdminCreateMediaPartnerRequest`), so a new partner is always
  active regardless of the Add form's Active checkbox; only Edit (Update)
  honours the flag.
- **Deactivate is a soft delete.** `DELETE` flips `IsActive = false` and writes
  a `MediaPartnerDeactivated` audit row; an already-inactive row is a no-op
  (returns without re-auditing). A deactivated partner is **dropped from the
  public list** but, because the admin grid is unfiltered, it stays visible with
  the "Inactive" pill. There is no hard delete and no in-use guard.
- **Delete is confirmation-gated.** The Delete icon opens
  `MediaPartnerViewDelete`; its Delete button raises a `SimfConfirm` dialog
  (titled `Admin.MediaPartners.Delete.Title`, message
  `Admin.MediaPartners.Delete.Message` naming the partner). Cancel fires no
  `DELETE`; only the confirm button does — exactly one call.
- **Selection is for Export only.** Select-all + per-row checkboxes feed the
  Export "selected ids" path; there is no other bulk action on this grid.
- **Filter casing.** Per-column `name`/`namearabic` filters match with EF
  `Contains` (case-sensitive on a case-sensitive collation) — the filter value
  casing must match the stored data.
- **Excel caps.** Export is capped at 5000 rows (`MaxExportRows`), and the
  import workbook at 5000 data rows (`MaxImportRows`) and 5 MB.
- **Import is insert-only.** `ImportMediaPartnersEndpoint` only ever creates
  (`GridRowApplyKind.Created`); it never updates an existing row. A blank
  English or Arabic name in a row → a per-row `DataValidationException`; a
  duplicate English name → the service's 409 captured as a per-row error.

## 8. i18n + RTL

`Admin.MediaPartners.*` keys (Title, Col.* headers, None, Loading, Summary,
Add/Edit/Details/Delete titles + close/message, Saved/Deleted/LoadFailed) plus
the shared `Grid.*` toolbar/label keys, in both `en` and `ar` resx. EN ↔ AR
parity preserved; the page mirrors to RTL under the Arabic locale (the exact
Arabic phrasing of each resx string is descriptive here, not quoted from this
review). The banner title in Arabic reads "شركاء النجاح".

## 10. Use cases

- UC-MPR-CREATE-001 (add a media partner), UC-MPR-EDIT-001 (edit / toggle
  active), UC-MPR-DEACTIVATE-001 (soft-delete), UC-MPR-EXPORT-001 /
  UC-MPR-IMPORT-001 (Excel round-trip). _(Formal UCS detail entries to be
  authored under the Public-Relations module UCS expansion.)_

## 11. E2E

See [`docs/tests/e2e/cp-admin-media-partners.md`](../../tests/e2e/cp-admin-media-partners.md):
E2E-MPR-001 full CRUD round-trip, 002 optional fields, 003 toggle Active,
004 delete-confirm cancelled, 005 empty list, 006 auth gate, 007 blank-name
client validation, 008 server name-too-long 400, 009 duplicate-name 409,
010 list 500 fallback, 011 RTL, 012 per-column filter, 013 column sort,
014 presentation toggle persists (D-353), 015 full-page round-trip (D-353),
016 delete confirmation gate (D-353), 017 Excel export (D-356), 018 Excel
import (D-356), 019 Excel import rejection (D-356), 020 logo via the unified
media-asset pipeline (D-357).

## 12. Related docs

- Page index: `docs/pages/PAGE-INDEX.md` (route `/admin/media-partners`).
- Permission catalogue: `PermissionCatalog.MediaPartners` (View/Create/Edit/
  Delete/Export/Import) — `docs/SIMF-Permission-Catalogue.md`.
- Decisions: D-199 (module), D-256 (raw-table → `SimfDataGrid`), D-260/D-281/
  D-283 (shared `Contact` link), D-353 (`CrudShell` Page↔Popup + `SimfConfirm`
  delete), D-356 (grid Excel export + import), D-357 (unified media-asset logo).
- Authority spec: SIMF-FDS-004 (programme/public-relations surface), Mockup
  page 31 ("شركاء النجاح").
- Public read: `GET /app/media-partners` (anonymous), covered by
  `tests/SIMF.Api.Tests/MediaPartnersTests.cs`.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-30 | D-199 | Original — `MediaPartner` entity + additive `dbo.MediaPartners` table + admin CRUD page (inline `SimfModal` form + native `confirm()` delete) + anonymous public list. |
| 2026-06-03 | D-256 | Raw `<table>` → `SimfDataGrid` (select-all + per-row checkboxes + quiet per-row icon actions). |
| 2026-06-10 | D-356 / D-353 | Excel export + import added (toolbar Export/Import → `.xlsx`, sheet "MediaPartners"); CRUD forms split into `MediaPartnerAddEdit` + `MediaPartnerViewDelete` hosted by `CrudShell` with a `SimfConfirm`-gated Delete and a Page↔Popup presentation toggle persisted in `localStorage`. New `MediaPartners.Export` / `MediaPartners.Import` permissions. E2E catalogue extended with E2E-MPR-014…019. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 ref-doc backfill).
