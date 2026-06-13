# Venue map nodes — `/admin/venue-map` (CP config page)

Per-page documentation folder for the Control Panel **Venue map** editor. Everything
about this CP config page lives here. The page is the Logistics team's editor for
the **2D venue-map nodes** (halls, zones, booths, points of interest) that the
Flutter app's venue-map screen renders. Each node is a bilingual label + a `Kind`
+ an `(X, Y)` design-space position + an **optional** link to a Hall **or** a
Booth.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Design | [admin-venue-map_Design.md](admin-venue-map_Design.md) | The as-built CP screen — `SimfDataGrid` list, the four columns, the per-column Label filter, the row actions, the `VenueMapAddEdit` form (Kind dropdown + X/Y + Hall/Booth pickers), the `VenueMapViewDelete` read-only/`SimfConfirm` soft-delete, the `CrudGridExcel` import/export, bilingual (AR/EN, RTL), empty/loading/error states, the D-353 page⇄dialog toggle |
| API | [admin-venue-map_API.md](admin-venue-map_API.md) | The admin endpoints the page calls (verb, full `/api/v1/admin/venue-map/...` route, permission policy, request/response DTOs, error codes) + the export/import grid endpoints + the public `GET /app/venue-map` the app reads the same nodes from |
| Function | [admin-venue-map_Function.md](admin-venue-map_Function.md) | What the operator does — each CRUD action, the golden add→edit(move)→soft-delete path, the Hall/Booth picker, validation rules, permission gating, bilingual toast text, Excel round-trip |
| Logic | [admin-venue-map_Logic.md](admin-venue-map_Logic.md) | State/data model — the `VenueMapNode` entity, `VenueMapNodeKind` enum, `IsActive` soft-delete + idempotency, audit events, list filtering/sort, the bare-Guid Hall/Booth links (D-157), and how the active nodes reach the app (resolve-on-read) |

## Identity
| | |
|---|---|
| Surface | **Control Panel** (Blazor Server) |
| Route | `/admin/venue-map` (`VenueMapList.razor`, `@page "/admin/venue-map"`) |
| Layout | `CpShellLayout` |
| Page permission | `PermissionCatalog.VenueMap.View` (`@attribute [RequirePermission(PermissionCatalog.VenueMap.View)]`) — value `"VenueMap.View"` |
| Action permissions | `VenueMap.Create` / `.Edit` / `.Delete` / `.Export` / `.Import` — every entry baseline `AdminOnly` (`PermissionCatalog.cs` `VenueMap` nested class lines 391–399 + `All` entries lines 786–791); `Administrator = "*"` sees all |
| Nav item | `CpNavigation` `new("Module.VenueMap", "/admin/venue-map", RequiredPermission: PermissionCatalog.VenueMap.View, Icon: "map")` (`CpNavigation.cs` line 129, under the reference-data group) |
| Title | `Admin.VenueMap.Title` → EN **Venue map** / AR resx pair (`SimfBanner`) |
| Pattern | D-230 2D venue-map node CRUD · D-255 `SimfDataGrid` list-page standard · D-353 `CrudShell` form split + presentation toggle + `SimfConfirm` delete · D-356 generic-grid Excel **export + import** |
| Backed by | `dbo.VenueMapNodes` table on `SimfAppDbContext` (migration `App/D230`, `VenueMapNodeKind` enum) |
| Status | ✅ Real / shipped (verified in code this session) |

## Source files (verified this session)
- CP page: [`VenueMapList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VenueMapList.razor)
- Add/Edit form: [`VenueMapAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VenueMapAddEdit.razor)
- View/Delete form: [`VenueMapViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VenueMapViewDelete.razor)
- Admin CRUD endpoints: [`VenueMapEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/VenueMapEndpoints.cs)
- Excel export/import endpoints: [`VenueMapExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/VenueMapExcelEndpoints.cs)
- Public read endpoint: [`PublicVenueMapEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Public/PublicVenueMapEndpoints.cs)
- Service: [`VenueMapService.cs`](../../../src/Backend/SIMF.Infrastructure/Venue/VenueMapService.cs)
- Admin contracts: [`VenueMap.cs`](../../../src/Shared/SIMF.Contracts/Admin/VenueMap.cs) (`AdminVenueMapNodeSummary` / `…Detail` / `AdminCreate…Request` / `AdminUpdate…Request`)
- Public contract: [`PublicVenueMap.cs`](../../../src/Shared/SIMF.Contracts/Programme/PublicVenueMap.cs) (`PublicVenueMapNode`)
- Enum: [`VenueMapNodeKind.cs`](../../../src/Shared/SIMF.Common/Enums/VenueMapNodeKind.cs) (`Hall=0 · Zone=1 · Booth=2 · PointOfInterest=3`)
- Permission catalogue: [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs) (`VenueMap` nested class lines 391–399 + `All` entries lines 786–791)
- Nav: [`CpNavigation.cs`](../../../src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs) (line 129)
- CP strings: `Resources/Strings.resx` + `Strings.ar.resx` (`Admin.VenueMap.*` + shared `Grid.*`)

## Related app page(s)
The CP page curates the data that the **app venue-map screen** renders:
- **[App Page 015](../../App/Page_015/README.md)** — الخريطة · Venue map. Its **E1**
  read `GET /api/v1/app/venue-map` returns the **active** nodes as
  `PublicVenueMapNode` (`id`, `label`, `labelArabic`, `kind`, `x`, `y`, `hallId?`,
  `boothId?`); the app draws each node at `(x, y)` on its 2D canvas and links a
  `kind=Booth` node to the booth detail sheet (Page 015 **E2**/**E3**,
  `GET /app/booths` + `/app/booths/{id}`). See
  [App Page 015 API](../../App/Page_015/Page_015_API.md) §E1. The CP **Hall** /
  **Booth** picker writes only the bare `HallId` / `BoothId` guid — **no hall or
  booth name** ships on the node (D-157 resolve-on-read; the app resolves a booth
  via its own booth read).

## Related existing docs (cross-links)
- Existing CP reference doc: [`docs/pages/cp/admin-venue-map.md`](../../pages/cp/admin-venue-map.md) (richest legacy description — mined and verified against code for this set).
- E2E catalogue: [`docs/tests/e2e/cp-admin-venue-map.md`](../../tests/e2e/cp-admin-venue-map.md) (E2E-VMP-001 … 024).
- Sibling CP config sets: [`admin-organisations`](../admin-organisations/README.md) · [`admin-countries`](../admin-countries/README.md). Sibling admin pages whose data this page links to: `docs/pages/cp/admin-booths.md` (booth lookup) and the halls admin page (hall lookup).
- Permission guide: `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`; catalogue `docs/SIMF-Permission-Catalogue.md`.
- Lower-layer API tests: `tests/SIMF.Api.Tests/VenueMapTests.cs`, `tests/SIMF.Api.Tests/VenueMapExcelTests.cs`.
- Decisions: D-230 (venue-map nodes + `VenueMapNodeKind` + `App/D230` migration), D-255 (`SimfDataGrid` list-page standard), D-353 (`CrudShell` form split + presentation toggle + `SimfConfirm` delete), D-356 (generic-grid Excel export + import), D-157 (Data ↔ Identity / resolve-on-read).
