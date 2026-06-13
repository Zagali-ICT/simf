# Halls — `/admin/halls` (CP config page)

Per-page documentation folder for the Control Panel **Halls** reference-data
page. Everything about this CP config page lives here. The page manages the
venue halls / rooms that the app's **Sessions** screen names (hall name on each
session) and that the **Venue map** marks (a map node may point at a hall).

Last updated: 2026-06-13 — CP config-page documentation (D-380).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Design | [admin-halls_Design.md](admin-halls_Design.md) | The as-built CP screen — `SimfDataGrid` list, columns, per-column filters, row actions, the `CrudShell`-hosted Add/Edit form (incl. the optional geofence triple), the read-only View/Delete soft-delete confirm, the Excel import/export, the Page↔Popup toggle, bilingual (AR/EN, RTL), empty/loading/error states |
| API | [admin-halls_API.md](admin-halls_API.md) | The five admin endpoints the page calls (method, full `/api/v1/admin/halls...` route, permission policy, request/response DTOs, error codes) + how the same `Hall` rows surface on the app's session reads |
| Function | [admin-halls_Function.md](admin-halls_Function.md) | What the operator does — each CRUD action, golden create→edit→details→deactivate path, validation rules, permission gating, bilingual toast text |
| Logic | [admin-halls_Logic.md](admin-halls_Logic.md) | State/data model — the `Hall` entity, `IsActive` soft-delete, audit stamping, list filtering/sorting/paging, unique uppercased `Code`, geofence rule, and how the hall reaches the app's session DTO (real FK) + the venue map node (real FK) |

## Identity
| | |
|---|---|
| Surface | **Control Panel** (Blazor Server) |
| Route | `/admin/halls` (`HallsList.razor`, `@page "/admin/halls"`) |
| Layout | `CpShellLayout` |
| Page permission | `PermissionCatalog.Halls.View` (`@attribute [RequirePermission(PermissionCatalog.Halls.View)]`) — value `"Halls.View"` |
| Action permissions | `Halls.Create` (POST) / `Halls.Edit` (PUT) / `Halls.Delete` (DELETE) / `Halls.Export` / `Halls.Import` — every entry baseline `AdminOnly` (`PermissionCatalog.cs` lines 225–233 + `All` entries 699–704); `Administrator = "*"` sees all |
| Nav item | `CpNavigation` `new("Module.Halls", "/admin/halls", RequiredPermission: PermissionCatalog.Halls.View, Icon: "building")` (under the Programme group, line 83) |
| Title | `Admin.Halls.Title` → EN **Halls & seating** / AR **القاعات والمقاعد** (`SimfBanner`) |
| Pattern | D-134 Sprint B venue halls (SIMF-FDS-004 §5.2) · D-255 SimfDataGrid list-page standard · D-353 CrudShell form split + Page↔Popup toggle · D-356 generic-grid Excel export + import · D-240 optional GPS geofence triple |
| Backed by | `dbo.Halls` table on `SimfAppDbContext` (migration `AddHalls`, 2026-05-28) |
| Status | ✅ Real / shipped (verified in code this session) |

## Source files (verified this session)
- CP page: [`HallsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallsList.razor)
- Add/Edit form: [`HallsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallsAddEdit.razor)
- View/Delete form: [`HallsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallsViewDelete.razor)
- Admin endpoints: [`HallEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/HallEndpoints.cs)
- Admin service: [`AdminHallService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminHallService.cs)
- Public read (hall reaches the app session DTO): [`ProgrammeSessionService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/ProgrammeSessionService.cs)
- Contracts: [`Halls.cs`](../../../src/Shared/SIMF.Contracts/Admin/Halls.cs)
- Entity: [`Hall.cs`](../../../src/Backend/SIMF.Domain/Programme/Hall.cs)
- EF config: [`HallConfiguration.cs`](../../../src/Backend/SIMF.Infrastructure/Persistence/Configurations/App/HallConfiguration.cs)
- Permission catalogue: [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs) (`Halls` nested class, lines 225–233 + `All` entries 699–704)
- Error codes: [`ErrorCodes.cs`](../../../src/Shared/SIMF.Common/ErrorCodes.cs) (`HallInvalid`, `HallNotFound`, `HallCodeDuplicate`, `HallInUse`, `HallGeofenceInvalid`)
- BFF passthroughs: [`AccountEndpoints.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs) (`/account/api/admin/halls/*`, lines 1015–1057)
- CP strings: `Resources/Strings.resx` + `Strings.ar.resx` (`Admin.Halls.*`)

## Related app page(s)
The CP page curates the rooms that two app screens render:
- **[App Page 016](../../App/Page_016/README.md)** — الأجندة · Sessions (agenda). Each session names its hall: the public read
  `GET /api/v1/app/programme/sessions` projects `hallId`, `hallName`, `hallNameArabic`
  onto every `PublicSessionListItem` (from `Session.Hall`, a real FK — see
  [Page 016 API](../../App/Page_016/Page_016_API.md) E1). The hall's `Capacity` also
  drives the session's booking cap (`CapacityOverride ?? Hall.Capacity`).
- **[App Page 015](../../App/Page_015/README.md)** — خريطة الموقع · Venue map. A `VenueMapNode`
  with `Kind = Hall` carries an optional real FK `HallId` (restrict-delete) to the
  hall it marks on the 2D map (`VenueMapNode.cs`, D-230). Halls and map nodes are
  curated separately — the map node points at a hall, the hall does not know its node.

## Related existing docs (cross-links)
- Existing CP reference doc: [`docs/pages/cp/admin-halls.md`](../../pages/cp/admin-halls.md) (legacy description — mined and reconciled against code for this set; see the drift note in [admin-halls_Logic.md](admin-halls_Logic.md)).
- E2E catalogue: [`docs/tests/e2e/cp-admin-halls.md`](../../tests/e2e/cp-admin-halls.md) (E2E-HAL-001 … 022).
- Sibling CP config sets: [`admin-sessions` (cp ref)](../../pages/cp/admin-sessions.md) (the page that assigns sessions to halls) · [`admin-venue-map` (cp ref)](../../pages/cp/admin-venue-map.md) (the page that pins hall nodes on the map).
- Permission guide: `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`; catalogue `docs/SIMF-Permission-Catalogue.md`.
- Lower-layer API tests: `tests/SIMF.Api.Tests/AdminHallGeofenceTests.cs` (geofence parse/persist). The endpoint's `// Tests:` header references an intended `AdminHallsTests.cs` CRUD suite that does not yet exist this session.
- Decisions: D-134 Sprint B / D-135 (Halls entity + `AddHalls` migration + canonical CRUD), D-240 (optional GPS geofence triple), D-248 (`HallPurpose`), D-255 (SimfDataGrid list-page standard), D-353 (CrudShell form split + Page↔Popup toggle), D-356 (generic-grid Excel export/import).
