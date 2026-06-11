# Countries (reference lookup) — `/admin/countries`

| | |
|--|--|
| **Route** | `/admin/countries` |
| **Audience** | Administrator + any role granted `Countries.*` |
| **Auth** | Page gate `@attribute [RequirePermission(PermissionCatalog.Countries.View)]`; mutating API endpoints gate `Countries.Create` / `Countries.Edit` / `Countries.Delete` + `RequireApprovedAccount`; create / update / deactivate / import also carry `RequireRateLimiting("auth")` |
| **Pattern** | D-132 canonical CRUD over a reference-data lookup, rendered through `SimfDataGrid`. D-353 CrudShell (popup ↔ full page). D-356 Excel export + import. |
| **Status** | ✅ Real (D-151 / D-155) |
| **Backend endpoints** | BFF `/account/api/admin/countries/*` → API. `POST /admin/countries/list`, `GET /admin/countries/{id:int}`, `POST /admin/countries`, `PUT /admin/countries/{id:int}`, `DELETE /admin/countries/{id:int}`, plus `POST /admin/countries/export` and `POST /admin/countries/import` (D-356). |
| **Source** | [`CountriesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CountriesList.razor), [`CountryAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CountryAddEdit.razor), [`CountryViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CountryViewDelete.razor), [`CountryEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/CountryEndpoints.cs), [`CountriesExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/CountriesExcelEndpoints.cs), [`AdminCountryService.cs`](../../../src/Backend/SIMF.Infrastructure/Common/AdminCountryService.cs), [`IAdminCountryService.cs`](../../../src/Backend/SIMF.Application/Common/Abstractions/IAdminCountryService.cs), [`Countries.cs`](../../../src/Shared/SIMF.Contracts/Admin/Countries.cs) |
| **Backed by** | `dbo.Countries` (`SimfAppDbContext`). Primary key is the **ISO 3166-1 numeric** `int` (manually assigned, NOT IDENTITY). Joined by logical id from `UserProfile.NationalityId` and `Speaker.CountryId`. |
| **Tests** | [`docs/tests/e2e/cp-admin-countries.md`](../../tests/e2e/cp-admin-countries.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

Countries is the **reference lookup** that backs the nationality picker (visitor
profiles via `UserProfile.NationalityId`) and the speaker country field
(`Speaker.CountryId`). Each row carries the ISO 3166-1 numeric id (the primary
key), the ISO 3166-1 alpha-2 `Code`, a bilingual name (`Name` / `NameArabic`),
an optional dial-code `PhonePrefix`, a `DisplayOrder`, and the soft-delete
`IsActive` flag. Both consuming joins resolve by bare id (D-157 cross-context
rule does not apply here — `Country` lives in `SimfAppDbContext` alongside its
consumers).

The page follows the D-132 canonical CRUD shape (`AdminCountryService` mirrors
`AdminThemeService` / `AdminHallService`), the difference being the **manually
assigned `int` primary key** instead of an auto-generated `Guid`.

## 4. UI

- `SimfBanner` titled from `Admin.Countries.Title`, inside a `simf-page-wide`
  surface. A `SimfAlert` toast surfaces success / error messages.
- A `SimfDataGrid` (`TItem="AdminCountrySummary"`, `Multiselect="true"`,
  `RowKey = r.Id.ToString()`) with the canonical toolbar (Select all + Add +
  Export + Import) and per-row icon actions (Edit, Details, Deactivate).
- Grid columns: **Id** (ISO numeric, sortable), **Code** (sortable + filterable),
  **Name (English)** (sortable), **Name (Arabic)**, **Dial code / phone prefix**
  (renders `—` when null), **Display order** (sortable), **Active** (a `SimfPill`
  `on`/`off` — Yes / No). The Name (Arabic), Dial code and Active columns are not
  sortable.
- Empty result renders `SimfEmptyState` (`Admin.Countries.None`).
- Add / Edit host the reusable `CountryAddEdit` form; Details / Deactivate host
  the reusable `CountryViewDelete` form. Both are framed by `CrudShell`.
- **Excel export + import (D-356):** the toolbar carries **Export** and **Import**
  actions wired to a `CrudGridExcel` component (`Resource="countries"`). Export
  (`OnExportAsync`) always calls `ExportAsync(Array.Empty<Guid>(), _query)` — it
  sends an **empty ids list plus the current grid query**, so it always exports
  the current filtered set rather than a per-row selection. This is deliberate:
  country ids are `int` (ISO numeric), not the `Guid` the generic export contract
  carries (see the `IdOf` note in `CountriesExcelEndpoints.cs`). Import
  (`OnImportAsync` → `TriggerImportAsync`) uploads an `.xlsx`; on success the page
  shows a success toast (`Grid.Import.Done`) and reloads the grid, and the
  `CrudGridExcel` result UI reports the per-row outcome. On error, `OnExcelError`
  surfaces a bilingual error toast.
- **Page ↔ Popup presentation toggle (D-353):** the toolbar hosts a
  `CrudPresentationToggle` bound to `PageKey = "countries"`. The admin can host
  Add / Edit / View / Delete as a dialog or as a full page; the choice is loaded
  and persisted through `CpPreferences` (`Prefs.GetPresentationAsync(PageKey)`).
  In full-page mode the grid is hidden (`GridHidden`) while the form is open.

## 4.5 Form fields

Source: `CountryAddEdit.razor` (client guards) + `AdminCountryService.Validate`
(server). Field labels/helpers are resx keys under `Admin.Countries.Field.*`.

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Id (ISO 3166-1 numeric) | yes (Add only) | n/a (`number`) | integer 1–999 client-side; server requires `> 0`. **Editable on Add, disabled/read-only on Edit** (the helper switches from `IdHint` to `IdReadOnly`). |
| Code (ISO alpha-2) | yes | 2 | exactly 2 chars; trimmed + upper-cased client- and server-side; unique |
| Name (English) | yes | 128 | 1–128 chars, trimmed |
| Name (Arabic) | yes | 128 | 1–128 chars, trimmed |
| Phone prefix (dial code) | no | 8 | ≤ 8 chars; blank stored as `null` |
| Display order | yes | n/a (`number`) | integer ≥ 0 |
| Active | (Edit only) | bool | shown only in Edit mode; toggles `IsActive` (reactivation path) |

Add mode posts `AdminCreateCountryRequest`; Edit mode puts
`AdminUpdateCountryRequest` (no `Id` in the body — the route id is authoritative).

## 5. Data flow + endpoints

The CP page never talks to the API directly. It calls the same-origin BFF
passthroughs under `/account/api/admin/countries/*` (mapped in
`AccountEndpoints.cs`), which attach the access token and forward to the API via
`SimfAdminClient`; the API endpoints live in `CountryEndpoints.cs`.

| Action | CP call (`simfAccount.*`) | BFF route | API endpoint | API permission |
|--------|---------------------------|-----------|--------------|----------------|
| List | `postJson` `/account/api/admin/countries/list` | `POST /admin/countries/list` | `ListCountriesEndpoint` | `Countries.View` |
| Get one | `getJson` `/account/api/admin/countries/{id}` | `GET /admin/countries/{id:int}` | `GetCountryEndpoint` | `Countries.View` |
| Create | `postJson` `/account/api/admin/countries` | `POST /admin/countries` | `CreateCountryEndpoint` | `Countries.Create` |
| Update | `putJson` `/account/api/admin/countries/{id}` | `PUT /admin/countries/{id:int}` | `UpdateCountryEndpoint` | `Countries.Edit` |
| Deactivate | `deleteJson` `/account/api/admin/countries/{id}` | `DELETE /admin/countries/{id:int}` | `DeactivateCountryEndpoint` | `Countries.Delete` |
| Export | via `CrudGridExcel` | `POST /admin/countries/export` | `ExportCountriesEndpoint` | `Countries.Export` |
| Import | via `CrudGridExcel` | `POST /admin/countries/import` | `ImportCountriesEndpoint` | `Countries.Import` |

All responses use the `ApiResult<T>` envelope. List returns
`GridPage<AdminCountrySummary>`; Get / Create / Update return
`AdminCountryDetail`; Deactivate returns `ApiResult<bool>`.

**Permissions** (`PermissionCatalog.Countries`): `View`, `Create`, `Edit`,
`Delete`, `Export`, `Import` — all `Countries.*` codes. The nav item
`Module.AdminCountries` (`/admin/countries`, icon `globe`) sets
`RequiredPermission = Countries.View`.

**Export shape (`ExportCountriesEndpoint`):** sheet `Countries`, file prefix
`simf-countries`, columns `Id | Code | Name | NameArabic | PhonePrefix |
DisplayOrder | IsActive`. The base lists rows via `service.ListAllAsync(query)`
and caps the whole-grid export at **5,000 rows** (`MaxExportRows`).

**Import shape (`ImportCountriesEndpoint`, insert-only):** sheet `Countries`,
required headers `Id | Code | Name | NameArabic`; the per-row key (for the error
list) is the `Code` cell. Each row binds to `AdminCreateCountryRequest`
(`Id`, `Code`, `Name`, `NameArabic`, optional `PhonePrefix`, `DisplayOrder`
defaulting to `0`) and calls `service.CreateAsync`. A row whose id is missing /
non-positive, or whose `Code` / `Name` / `NameArabic` is blank, throws a
`DataValidationException` that the base records as a **per-row error** rather than
aborting the batch. The shared base (`AdminGridImportEndpoint`) enforces a
**5 MB** upload cap, a ZIP-magic (`50 4B 03 04`) `.xlsx` check, the required-sheet
/ required-header check, and a **5,000-row** parse cap (`MaxImportRows`), and
aggregates the result as `AdminGridImportResult(created, updated, skipped,
errors)`. Because the country importer only ever calls `CreateAsync`, in
practice it reports created or per-row errors (a duplicate id/code is a per-row
error, never an update).

## 6. Validation + error handling

Server validation lives in `AdminCountryService.Validate` (mirrored by client
guards in `CountryAddEdit.razor`):

- **Id** — `> 0` server-side (client also caps at ≤ 999); else `400 COUNTRY_INVALID`.
- **Code** — trimmed + upper-cased, must be exactly 2 chars; else `400 COUNTRY_INVALID`.
- **Name / NameArabic** — each 1–128 chars; else `400 COUNTRY_INVALID`.
- **PhonePrefix** — optional; if present must be ≤ 8 chars; else `400 COUNTRY_INVALID`.
- **DisplayOrder** — ≥ 0; else `400 COUNTRY_INVALID`.
- **Duplicate id** — on create, `409 COUNTRY_ID_DUPLICATE` (message names the id).
- **Duplicate code** — on create, or on update when the code changes to one held
  by another row, `409 COUNTRY_CODE_DUPLICATE` (message names the code).
- **Not found** — get / update / deactivate of a missing id → `404 COUNTRY_NOT_FOUND`.

Error codes are defined in `ErrorCodes.cs`: `COUNTRY_INVALID`,
`COUNTRY_NOT_FOUND`, `COUNTRY_CODE_DUPLICATE`, `COUNTRY_ID_DUPLICATE`, and the
reserved `COUNTRY_IN_USE` (not yet wired). Every thrown `ApiException` carries a
bilingual message; the CP surfaces `Error.MessageForCurrentCulture()` in the form
alert or page toast, falling back to a descriptive resx string
(`Admin.Countries.Fallback` / `Admin.Countries.LoadFailed`) when none is present.

Each successful mutation writes an audit entry through `IAuditLog`:
`AuditEvents.CountryCreated` / `CountryUpdated` / `CountryDeactivated`, with the
actor id and a `Detail` string (e.g. `id=116; code=KH; name=Cambodia`).

## 7. Edge cases + known limitations

- **Id is immutable after create.** The ISO numeric id is the primary key and is
  manually assigned; the Edit form disables it and the update contract has no
  `Id` field. To "change" an id you must create a new row.
- **Deactivate is a soft-delete and is unconditional.** `DeactivateAsync` sets
  `IsActive = false`; a second deactivate of an already-inactive row is a no-op
  (returns early). There is no in-use guard yet — `COUNTRY_IN_USE` is reserved
  but not enforced, so a country can be deactivated even while profiles/speakers
  reference it (those consumers resolve by bare id). Reactivation is via Edit
  (tick the Active checkbox → `IsActive = true`).
- **Code is case-insensitive.** Stored upper-cased; "sa" and "SA" collide on the
  duplicate-code check. Display preserves the canonical upper form.
- **Export ignores row selection.** Selecting rows does not narrow the export —
  it always covers the current filtered grid (empty ids + current query), because
  country ids are `int`, not the `Guid` the generic export contract carries.
- **Import is insert-only.** It cannot update existing rows; a row whose id or
  code already exists is reported as a per-row error.
- **Missing dial code** renders as `—` in the grid and the details list.

## 8. i18n + RTL

All visible strings are resx keys under `Admin.Countries.*` (titles, columns,
field labels/helpers/validation, pager, actions, toasts) plus the shared
`Grid.Export` / `Grid.Import` / `Grid.Import.Done` keys. EN ↔ AR parity is
expected; server validation, conflict and not-found messages are themselves
bilingual (English + Arabic) and the CP renders the culture-appropriate side via
`MessageForCurrentCulture()`. The page mirrors to RTL under the Arabic locale
(grid headers, toolbar, pager arrows, and the hosted Add/Edit/View forms).

## 10. Use cases

- Maintain the country reference list that feeds the visitor nationality picker
  and the speaker country field: create / edit / view / deactivate / reactivate.
- Bulk-seed or top-up the list via Excel import; export the current list for
  review or backup.

## 11. E2E

See [`docs/tests/e2e/cp-admin-countries.md`](../../tests/e2e/cp-admin-countries.md):
the catalogue covers the full CRUD round-trip, empty state, the
`Countries.View` auth gate, search/filter, sortable columns, pager, details
modal, reactivation via Edit, the validation paths (id / code / names / dial
code / display order), the duplicate-id and duplicate-code 409s, not-found and
server-500 resilience, RTL, the D-353 presentation toggle + full-page mode +
SimfConfirm-gated deactivate, and the D-356 Excel export, import, and import
rejection scenarios (`E2E-CTY-018`…`020`).

## 12. Related docs

- Decisions: D-151 / D-155 (Country CRUD + reference lookup), D-157
  (Data ↔ Identity separation — `Country` is App-side), D-353 (CrudShell
  presentation toggle), D-356 (grid Excel export + import).
- Permission catalogue: `PermissionCatalog.Countries` (`docs/SIMF-Permission-Catalogue.md`).
- Sibling reference-lookup pages: [`admin-themes.md`](admin-themes.md),
  Halls, Gates, Interests, Profile types, Organisations.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-29 | D-151 / D-155 | Original — `Country` lookup + admin CRUD page (`SimfDataGrid` + `CountryForm`), ISO 3166-1 numeric primary key, joined by `UserProfile.NationalityId` and `Speaker.CountryId`. |
| 2026-06-09 | D-353 | CRUD forms split into reusable `CountryAddEdit` + `CountryViewDelete` hosted by `CrudShell` with a `SimfConfirm`-gated Deactivate and a Page↔Popup presentation toggle persisted via `CpPreferences` (`PageKey="countries"`). |
| 2026-06-10 | D-356 | Excel export + import added — toolbar Export/Import wired to `CrudGridExcel` (`Resource="countries"`); `ExportCountriesEndpoint` (sheet "Countries", `Countries.Export`) and `ImportCountriesEndpoint` (insert-only, `Countries.Import`); export always covers the filtered grid (int ids). E2E catalogue extended with E2E-CTY-018…020. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 — Countries reference doc, grounded in live source).
