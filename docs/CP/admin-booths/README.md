# Exhibition booths — `/admin/booths` (CP config page)

Per-page documentation folder for the Control Panel **Exhibition booths** page.
Everything about this CP config page lives here. The page is where an
administrator curates the bilingual exhibition-booth list that the app's **2D
venue map** booth nodes + booth-detail sheet (App Page 015) and the public
website Exhibition screen (Mockup page 22) read.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Design | [admin-booths_Design.md](admin-booths_Design.md) | The as-built CP screen — `SimfDataGrid` list, columns, filters, row actions, the `BoothsAddEdit` form, the `BoothsViewDelete` read-only / soft-delete confirm, the Excel export/import host, the Page↔Popup toggle, bilingual (AR/EN, RTL), empty/loading/error states |
| API | [admin-booths_API.md](admin-booths_API.md) | The admin endpoints the page calls (method, full `/api/v1/admin/...` route, permission policy, request/response DTOs, error codes) + the three public `/app/...` reads (`/app/booths`, `/app/booths/{id}`, `/app/venue-map`) the app consumes the same data from |
| Function | [admin-booths_Function.md](admin-booths_Function.md) | What the operator does — each CRUD action, the golden Add→Edit→Deactivate path, the Excel export/import flow, validation rules, permission gating, bilingual toast text |
| Logic | [admin-booths_Logic.md](admin-booths_Logic.md) | State/data model — the `Booth` entity, the Exhibitor / Hall / Contact logical FKs, `IsActive` soft-delete, audit stamping, list filter/sort, Code uniqueness, how the data reaches the app (resolve-on-read) |

## Identity
| | |
|---|---|
| Surface | **Control Panel** (Blazor Server) |
| Route | `/admin/booths` (`BoothsList.razor`, `@page "/admin/booths"`) |
| Layout | `CpShellLayout` |
| Page permission | `PermissionCatalog.Booths.View` (`@attribute [RequirePermission(PermissionCatalog.Booths.View)]`) — value `"Booths.View"` |
| Action permissions | `Booths.Create` / `.Edit` / `.Delete` / `.Export` / `.Import` (`PermissionCatalog.cs` lines 370–378); `Administrator = "*"` sees all |
| Nav item | `CpNavigation` `new("Module.Booths", "/admin/booths", RequiredPermission: PermissionCatalog.Booths.View, Icon: "store")` (`CpNavigation.cs` line 125, under the Exhibition group) |
| Title | `Admin.Booths.Title` → EN **Exhibition booths** / AR **أجنحة المعرض** (`SimfBanner`) |
| Pattern | D-199 Exhibition-module CRUD · D-222 Booth→Exhibitor company + booth-officer contact · D-255/D-256 `SimfDataGrid` list-page standard · D-353 `CrudShell` form split + Page↔Popup presentation toggle + `SimfConfirm` delete · D-356 generic-grid Excel export + insert-only import |
| Backed by | `dbo.Booths` table on `SimfAppDbContext` (D-199 additive migration; D-222 added the Exhibitor FK + booth-officer fields) |
| Status | ✅ Real / shipped (verified in code this session) |

## Source files (verified this session)
- CP page: [`BoothsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BoothsList.razor)
- Add/Edit form: [`BoothsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BoothsAddEdit.razor)
- View/Delete form: [`BoothsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BoothsViewDelete.razor)
- Admin endpoints: [`BoothEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/BoothEndpoints.cs)
- Excel export/import endpoints: [`BoothsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/BoothsExcelEndpoints.cs)
- Public reads: [`PublicBoothEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Public/PublicBoothEndpoints.cs)
- Admin service + validation: [`AdminBoothService.cs`](../../../src/Backend/SIMF.Infrastructure/Exhibition/AdminBoothService.cs)
- Contracts (admin + public DTOs): [`BoothContracts.cs`](../../../src/Shared/SIMF.Contracts/Exhibition/BoothContracts.cs)
- Permission catalogue: [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs) (`Booths` nested class, lines 370–378)
- Nav: [`CpNavigation.cs`](../../../src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs) (line 125)
- BFF passthroughs: [`AccountEndpoints.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs) (CRUD lines 2290–2318; Excel via `MapGridExcel(group, "booths")`)
- CP strings: `Resources/Strings.resx` + `Strings.ar.resx` (`Admin.Booths.*` + shared `Grid.*` keys)

## Related app page(s)
The CP page curates the data the **app** reads:
- **[App Page 015](../../App/Page_015/README.md)** — الخريطة · Venue map. The 2D map
  loads `GET /api/v1/app/venue-map` (positioned nodes) **and**
  `GET /api/v1/app/booths` (the booth lookup for the bottom info card) in
  parallel; tapping a **booth** node shows the info card (name · exhibitor ·
  sector · code) and its **عرض التفاصيل** action opens the detail sheet, which
  lazily fetches `GET /api/v1/app/booths/{id}` for the **description** paragraph.
  See [App Page 015 API](../../App/Page_015/Page_015_API.md) §E2/§E3.
- **Field linkage (what the app shows):** the booth `Code`, bilingual
  `Name`/`NameArabic`, the resolved `ExhibitorName`/`ExhibitorNameArabic`, the
  bilingual `Sector`/`SectorArabic`, the `HallId` (a **bare Guid** — no hall name
  ships) and the booth's own `MapX`/`MapY`. The detail read adds
  `Description`/`DescriptionArabic` (the lazy paragraph). The booth **officer**
  fields, the optional shared-`Contact` link and the **active** flag are
  **CP-internal** — they are NOT exposed on the public DTOs. A booth logo is
  **decoration only** (no `LogoUrl` field exists, D11).

## Related existing docs (cross-links)
- Existing CP reference doc: [`docs/pages/cp/admin-booths.md`](../../pages/cp/admin-booths.md) (richest legacy description — mined and verified against code for this set).
- E2E catalogue: [`docs/tests/e2e/cp-admin-booths.md`](../../tests/e2e/cp-admin-booths.md) (E2E-BTH-001 … 023).
- Sibling CP config sets: [`admin-exhibitors`](../../pages/cp/admin-exhibitors.md) (the Exhibitor company a booth links to), [`admin-venue-map`](../../pages/cp/admin-venue-map.md) (the 2D node placement), [`admin-sponsors`](../../pages/cp/admin-sponsors.md).
- App page: [App Page 015 — Venue map](../../App/Page_015/README.md).
- Permission guide: `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`; catalogue `docs/SIMF-Permission-Catalogue.md`.
- Lower-layer API tests: `tests/SIMF.Api.Tests/AdminBoothsTests.cs`, `tests/SIMF.Api.Tests/PublicBoothsTests.cs`, `tests/SIMF.Api.Tests/BoothsExcelTests.cs`.
- Decisions: D-199 (Booths module), D-222 (Booth → Exhibitor company + booth-officer contact), D-281/D-283 (shared `Contact` link), D-255/D-256 (`SimfDataGrid` list-page standard), D-353 (CrudShell form split + Page↔Popup toggle + SimfConfirm delete), D-356 (Excel export/import).
