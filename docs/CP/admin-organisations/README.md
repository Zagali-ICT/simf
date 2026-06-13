# Organisations — `/admin/organisations` (CP config page)

Per-page documentation folder for the Control Panel **Organisations** reference-data
page. Everything about this CP config page lives here. The page manages the
bilingual Saudi-companies lookup that the app's profile form (الجهة picker)
consumes.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Design | [admin-organisations_Design.md](admin-organisations_Design.md) | The as-built CP screen — SimfDataGrid list, columns, filters, row actions, the Add/Edit form, the View/Delete soft-delete confirm, the Excel import/export modals, bilingual (AR/EN, RTL), empty/loading/error states |
| API | [admin-organisations_API.md](admin-organisations_API.md) | The admin endpoints the page calls (method, full `/api/v1/admin/...` route, permission policy, request/response DTOs, error codes) + the `/app/organisations` endpoint the app reads the same data from |
| Function | [admin-organisations_Function.md](admin-organisations_Function.md) | What the operator does — each CRUD action, golden create→edit→soft-delete path, validation rules, permission gating, bilingual toast text |
| Logic | [admin-organisations_Logic.md](admin-organisations_Logic.md) | State/data model — the `Organisation` entity, `IsActive` soft-delete, audit stamping, list filtering, uniqueness/ordering, how the lookup reaches the app (resolve-on-read), seeding |

## Identity
| | |
|---|---|
| Surface | **Control Panel** (Blazor Server) |
| Route | `/admin/organisations` (`OrganisationsList.razor`, `@page "/admin/organisations"`) |
| Layout | `CpShellLayout` |
| Page permission | `PermissionCatalog.Organisations.View` (`@attribute [RequirePermission(PermissionCatalog.Organisations.View)]`) — value `"Organisations.View"` |
| Action permissions | `Organisations.Create` / `.Edit` / `.Delete` / `.Import` / `.Export` — every entry baseline `AdminOnly` (`PermissionCatalog.cs` lines 659–664); `Administrator = "*"` sees all |
| Nav item | `CpNavigation` `new("Module.Organisations", "/admin/organisations", RequiredPermission: PermissionCatalog.Organisations.View, Icon: "building")` (under the reference-data group) |
| Title | `Admin.Organisations.Title` → EN **Organisations** / AR resx pair (`SimfBanner`) |
| Pattern | D-220 reference lookup · D-255 SimfDataGrid list-page standard · D-353 CrudShell form split + presentation toggle · D-356 generic-grid Excel **export only** |
| Backed by | `dbo.Organisations` table on `SimfAppDbContext` (D-220 additive migration) |
| Status | ✅ Real / shipped (verified in code this session) |

## Source files (verified this session)
- CP page: [`OrganisationsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OrganisationsList.razor)
- Add/Edit form: [`OrganisationAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OrganisationAddEdit.razor)
- View/Delete form: [`OrganisationViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OrganisationViewDelete.razor)
- Admin endpoints: [`OrganisationEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/OrganisationEndpoints.cs)
- Excel export endpoint: [`OrganisationExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/OrganisationExcelEndpoints.cs)
- Admin service: [`AdminOrganisationService.cs`](../../../src/Backend/SIMF.Infrastructure/Organisations/AdminOrganisationService.cs)
- Public picker service: [`PublicOrganisationService.cs`](../../../src/Backend/SIMF.Infrastructure/Organisations/PublicOrganisationService.cs)
- Dev seeder: [`OrganisationSeeder.cs`](../../../src/Backend/SIMF.Infrastructure/Organisations/OrganisationSeeder.cs)
- Contracts: [`OrganisationContracts.cs`](../../../src/Shared/SIMF.Contracts/Organisations/OrganisationContracts.cs)
- Entity: [`Organisation.cs`](../../../src/Backend/SIMF.Domain/Organisations/Organisation.cs)
- Permission catalogue: [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs) (`Organisations` nested class, lines 161–169 + `All` entries 659–664)
- BFF passthroughs: [`AccountEndpoints.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs) (`/account/api/admin/organisations/*`)
- CP strings: `Resources/Strings.resx` + `Strings.ar.resx` (`Admin.Organisations.*`)

## Related app page(s)
The CP page curates the data that the **app profile form** reads:
- **[App Page 007](../../App/Page_007/README.md)** — إنشاء حساب · Sign up (profile data). Its **E6** lookup `GET /api/v1/app/organisations` resolves the **الجهة** (organisation) picker against the **active** organisation set. The app sends `search` + `top=20`; each item carries `id`, `nameAr`, `nameEn`, `city` (`OrganisationPickerItem`). The picked `id` is saved on the profile as `organisationId` (a bare Guid, D-221). See [App Page 007 API](../../App/Page_007/Page_007_API.md) §E6.
- **[App Page 005](../../App/Page_005/README.md)** — sign-in / pre-profile entry to the sign-up flow that lands on Page 007 (linkage is via the Page 007 profile form, not a direct organisations read).

## Related existing docs (cross-links)
- Existing CP reference doc: [`docs/pages/cp/admin-organisations.md`](../../pages/cp/admin-organisations.md) (richest legacy description — mined and verified against code for this set).
- E2E catalogue: [`docs/tests/e2e/cp-admin-organisations.md`](../../tests/e2e/cp-admin-organisations.md) (E2E-ORG-001 … 019).
- Permission guide: `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`; catalogue `docs/SIMF-Permission-Catalogue.md`.
- Lower-layer API tests: `tests/SIMF.Api.Tests/OrganisationTests.cs`, `tests/SIMF.Api.Tests/OrganisationExcelTests.cs`.
- Decisions: D-220 (Organisation lookup entity + migration), D-221 (`UserProfile.OrganisationId`), D-255 (SimfDataGrid list-page standard), D-353 (CrudShell form split + presentation toggle), D-356 (generic-grid Excel export).
