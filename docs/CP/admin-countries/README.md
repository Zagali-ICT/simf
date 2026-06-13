# CP — البلدان · Countries (nationality reference data) — `/admin/countries`

Per-page documentation folder for the Control Panel **Countries / nationalities**
config page. Everything about this CP page lives here.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

This is a Control Panel **reference-data** page. It maintains the single country
list that the mobile app's **Page 007** nationality picker
(`GET /app/account/user-profile/countries`) consumes, and that the visitor
profile (`UserProfile.NationalityId`) and the speaker (`Speaker.CountryId`)
resolve against by bare logical id.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [admin-countries_Function.md](admin-countries_Function.md) | What the admin does — grid, toolbar, per-row actions, the Add/Edit/View/Delete forms, Excel import/export, presentation toggle |
| Logic | [admin-countries_Logic.md](admin-countries_Logic.md) | Business rules — the ISO-numeric primary key, validation, duplicate/uniqueness rules, soft-delete, the app-picker consumption contract |
| API | [admin-countries_API.md](admin-countries_API.md) | The BFF → API endpoints + DTOs (authoritative contract), permissions, error codes |
| Design | [admin-countries_Design.md](admin-countries_Design.md) | CP screen design — banner, `SimfDataGrid` columns, `CrudShell` forms, states, i18n / RTL |

## Identity
| | |
|---|---|
| Route | `/admin/countries` (`@page` in `CountriesList.razor`) |
| Layout | `CpShellLayout` |
| Titles | Banner: resx `Admin.Countries.Title` (AR **البلدان** · EN **Countries**) |
| Section | Reference data / lookups (admin) |
| Nature | **Canonical CRUD over a reference-data lookup** (`SimfDataGrid` + `CrudShell`), with Excel import/export |
| Permission (page gate) | `PermissionCatalog.Countries.View` — `@attribute [RequirePermission(PermissionCatalog.Countries.View)]` |
| Nav item | `Module.AdminCountries` → `/admin/countries`, icon `globe`, `RequiredPermission = Countries.View` (`CpNavigation.cs`) |
| Backed by | `dbo.Countries` (`SimfAppDbContext`). Primary key = **ISO 3166-1 numeric** `int` (manually assigned, NOT IDENTITY) |
| Status | ✅ Real — D-151 / D-155 (CRUD + lookup), D-353 (CrudShell presentation toggle), D-356 (Excel export + import) |

## Source files (verified this session)
| File | Role |
|------|------|
| `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CountriesList.razor` | The page — banner + `SimfDataGrid` + `CrudShell` host + toolbar wiring |
| `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CountryAddEdit.razor` | Reusable Add/Edit form (`CrudAddEditFormBase<AdminCountryDetail>`) |
| `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CountryViewDelete.razor` | Reusable View/Delete form (`CrudViewDeleteFormBase<AdminCountryDetail>`) + `SimfConfirm`-gated deactivate |
| `src/Backend/SIMF.Api/Endpoints/Admin/CountryEndpoints.cs` | List / Get / Create / Update / Deactivate endpoints |
| `src/Backend/SIMF.Api/Endpoints/Admin/CountriesExcelEndpoints.cs` | Export + Import endpoints (D-356) |
| `src/Shared/SIMF.Contracts/Admin/Countries.cs` | `AdminCountrySummary` / `AdminCountryDetail` / `AdminCreateCountryRequest` / `AdminUpdateCountryRequest` |
| `src/Shared/SIMF.Common/PermissionCatalog.cs` | `PermissionCatalog.Countries` codes + `All` registrations |
| `src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs` | `Module.AdminCountries` nav entry |

## App linkage (the reason this page exists)
The mobile app reads this list — verbatim route, verified in
`src/Backend/SIMF.Api/Endpoints/Account/ProfileCountriesEndpoint.cs`:

```
GET /api/v1/app/account/user-profile/countries  →  ApiResult<CountryListResponse>
```

The app endpoint returns **only active rows** (`country.IsActive`), ordered by the
admin-set **`DisplayOrder`** then the **English name** as a stable tiebreaker, and
projects each row to `CountryDto(Code, Name, NameArabic)` — the ISO alpha-2 code
plus the bilingual name. The app's nationality picker (Page 007) shows this list,
defaults the selection to **Saudi Arabia / SA**, and the picked `Code` drives the
visitor's document path (SA → national-ID; else Iqama / Passport).

> Whatever an admin creates / edits / orders / deactivates here is exactly what the
> app's Page 007 picker shows on its next fetch. **Deactivating** a country removes
> it from the picker (the app filters on `IsActive`); the numeric `Id` set here is
> the primary key the picker's selected code resolves against.

## Sources of truth (read first)
- Format template: `docs/App/Page_016/README.md` + `Page_016_Design.md`.
- CP reference doc (content source, verified vs code): `docs/pages/cp/admin-countries.md`.
- E2E catalogue: `docs/tests/e2e/cp-admin-countries.md` (`E2E-CTY-001…020`).
- Consuming app screen: `docs/App/Page_007/` (the nationality picker).
- Decisions: D-151 / D-155 (Country CRUD + lookup), D-157 (Data ↔ Identity
  separation — `Country` is App-side), D-353 (CrudShell presentation toggle),
  D-356 (grid Excel export + import).

## Cross-links
- CP reference doc: [`../../pages/cp/admin-countries.md`](../../pages/cp/admin-countries.md)
- CP E2E catalogue: [`../../tests/e2e/cp-admin-countries.md`](../../tests/e2e/cp-admin-countries.md)
- Consuming app page: [`../../App/Page_007/README.md`](../../App/Page_007/README.md)
  (nationality lookup `E3` in [`../../App/Page_007/Page_007_API.md`](../../App/Page_007/Page_007_API.md))
- Permission catalogue: `docs/SIMF-Permission-Catalogue.md` (`PermissionCatalog.Countries`)
