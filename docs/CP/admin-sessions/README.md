# Programme sessions — `/admin/sessions` (CP config page)

Per-page documentation folder for the Control Panel **Programme sessions** page.
Everything about this CP config page lives here. The page is the admin catalogue
for the event's programme **sessions** — the line-up the app's agenda/sessions
screen (App Page 016) and the home next-session surface (App Page 013) consume.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Design | [admin-sessions_Design.md](admin-sessions_Design.md) | The as-built CP screen — `SimfDataGrid` list, columns, filter, row actions + presentation toggle, the Add/Edit form (Hall/Category pickers, reorderable speaker roster, theme chips, live URLs), the View/Delete form (read-only `<dl>`, broadcast-lifecycle footer, recording uploader, soft-delete confirm), the Excel export/import, bilingual (AR/EN, RTL), empty/loading/error states |
| API | [admin-sessions_API.md](admin-sessions_API.md) | The admin endpoints the page calls (verb, full `/api/v1/admin/...` route, permission policy, request/response DTOs, error codes) + the picker reads + the `/app/programme/sessions` reads the app consumes the same data from |
| Function | [admin-sessions_Function.md](admin-sessions_Function.md) | What the operator does — each CRUD action, the golden create→edit→view→soft-delete path, the broadcast lifecycle + recording, the Excel round-trip, validation rules, permission gating, bilingual toast text |
| Logic | [admin-sessions_Logic.md](admin-sessions_Logic.md) | State/data model — the `Session` entity, the `SessionStatus` lifecycle state machine, `IsActive` soft-delete, effective-capacity resolution, the speaker/theme M-to-M sets, audit stamping, how the catalogue reaches the app (resolve-on-read), the cross-DB rule |

## Identity
| | |
|---|---|
| Surface | **Control Panel** (Blazor Server) |
| Route | `/admin/sessions` (`SessionsList.razor`, `@page "/admin/sessions"`) |
| Layout | `CpShellLayout` |
| Page permission | `PermissionCatalog.Sessions.View` (`@attribute [RequirePermission(PermissionCatalog.Sessions.View)]`) — value `"Sessions.View"` |
| Action permissions | `Sessions.Create` / `.Edit` / `.Delete` (CRUD); `Sessions.Publish` (broadcast lifecycle **and** recording upload/remove); `Sessions.Export` / `.Import` (Excel). All in `PermissionCatalog.Sessions` (lines 206–218); `Administrator = "*"` sees all |
| Nav item | `CpNavigation` `new("Module.Sessions", "/admin/sessions", RequiredPermission: PermissionCatalog.Sessions.View, Icon: "calendar")` (under the programme group) |
| Title | `Admin.Sessions.Title` → EN **Sessions** / AR resx pair (`SimfBanner`) |
| Pattern | D-165 Sessions CRUD · D-225 speaker/host role · D-226 session category · D-231 broadcast lifecycle · D-232 recording · D-349 live-stream URLs · D-353 CrudShell form split + presentation toggle · D-356 generic-grid Excel export + import |
| Backed by | `dbo.Sessions` (+ `SessionSpeakers`, `SessionThemes` join sets) on `SimfAppDbContext` |
| Status | ✅ Real / shipped (verified in code this session) |

## Source files (verified this session)
- CP page: [`SessionsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionsList.razor)
- Add/Edit form: [`SessionsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionsAddEdit.razor)
- View/Delete form: [`SessionsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionsViewDelete.razor)
- Admin CRUD + lifecycle endpoints: [`SessionEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionEndpoints.cs)
- Recording endpoints: [`SessionRecordingEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionRecordingEndpoints.cs)
- Excel export/import endpoints: [`SessionsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionsExcelEndpoints.cs)
- Admin service contract: `IAdminSessionService` (`SIMF.Application.Programme.Abstractions`) → impl [`AdminSessionService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminSessionService.cs)
- Public read service: [`AdminSessionSummaryService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminSessionSummaryService.cs) (grid summary projection)
- Admin contracts: [`Sessions.cs`](../../../src/Shared/SIMF.Contracts/Admin/Sessions.cs) (`AdminSessionSummary`, `AdminSessionDetail`, `AdminSessionSpeakerEntry`, `AdminCreateSessionRequest`, `AdminUpdateSessionRequest`, `SetSessionStatusRequest`)
- App contracts: [`PublicSessions.cs`](../../../src/Shared/SIMF.Contracts/Programme/PublicSessions.cs) (`PublicSessions`, `PublicSessionListItem`, `PublicSessionDetail`, `PublicSessionSpeaker`)
- Lifecycle enum: [`SessionStatus.cs`](../../../src/Shared/SIMF.Common/Enums/SessionStatus.cs) (`Scheduled=0`, `Held=1`, `Recorded=2`, `Published=3`)
- Speaker-role enum: [`SessionSpeakerRole.cs`](../../../src/Shared/SIMF.Common/Enums/SessionSpeakerRole.cs) (`Speaker=0`, `Host=1`)
- Permission catalogue: [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs) (`Sessions` nested class, lines 206–218)
- Nav: [`CpNavigation.cs`](../../../src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs) (`Module.Sessions`, line 77)
- CP strings: `Resources/Strings.resx` + `Strings.ar.resx` (`Admin.Sessions.*`)

## Related lookups (sibling CP doc sets / pages)
The session form's relational pickers are each their own CP config page; this page
only references them:
- **Halls** — `/admin/halls` (`Halls.View`); the mandatory **Hall** select +
  hall-derived effective capacity. Loaded via `POST .../halls/list` (Top=500, active).
- **Session categories** — `/admin/session-categories` (`SessionCategories.View`);
  the optional **Category** ("is main session / type" tag, D-226). Loaded via
  `POST .../session-categories/list`.
- **Speakers** — `/admin/speakers` (`Speakers.View`); the reorderable
  **speaker/host roster** (D-225). Loaded via `POST .../speakers/list`.
- **Themes** — `/admin/themes` (`Themes.View`); the multi-pick **theme** set.
  Loaded via `POST .../themes/list`.

A read-only run-of-show view of the same data lives at `/admin/programme/timeline`
(`ProgrammeTimeline.View`) — a separate page, not the management surface.

## Related app page(s)
The CP page curates the programme the **app** reads:
- **[App Page 016](../../App/Page_016/README.md)** — الأجندة · Sessions (agenda).
  It fetches the **whole active programme once** via `GET /api/v1/app/programme/sessions`
  (`PublicSessions`) and caches it, then filters client-side. Each row the agenda
  shows maps to one `Session` curated here: time (`Start`/`End`), `Code`,
  bilingual title/description, `Hall*`, the `Category*` "type" tag, lifecycle
  `Status`, and the ordered `speakers[]` cards. See [App Page 016 API](../../App/Page_016/Page_016_API.md) E1/E2.
- **[App Page 013](../../App/Page_013/README.md)** — الرئيسية · Home (router screen).
  The home surface shows a next/live-session entry derived from the same programme;
  it carries no session data of its own (the live banner is static, D10) — the
  session content originates here. See [App Page 013 API](../../App/Page_013/Page_013_API.md).

## Related existing docs (cross-links)
- Existing CP reference doc: [`docs/pages/cp/admin-sessions.md`](../../pages/cp/admin-sessions.md) (richest legacy description — mined and verified against code for this set).
- E2E catalogue: [`docs/tests/e2e/cp-admin-sessions.md`](../../tests/e2e/cp-admin-sessions.md) (E2E-SES-001 … 024).
- Permission guide: `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`; catalogue `docs/SIMF-Permission-Catalogue.md`.
- Lower-layer API tests: `tests/SIMF.Api.Tests/AdminSessionsTests.cs`, `SessionLifecycleTests.cs`, `SessionRecordingTests.cs`, `SessionsExcelTests.cs`.
- Authority spec: SIMF-FDS-004 §5.3 (+ PDF §2.9).
- Decisions: D-165 (Sessions CRUD), D-225 (speaker/host role), D-226 (category), D-231 (broadcast lifecycle), D-232 (recording), D-349 (live-stream URLs), D-353 (CrudShell form split + presentation toggle), D-356 (generic-grid Excel export + import).
