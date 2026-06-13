# CP — Session categories · `/admin/session-categories`

Per-page documentation folder for the Control Panel **session-category** admin
page. Everything about this CP config page lives here. Format mirrors the
Flutter app's per-page folders (e.g. `docs/App/Page_016/`).

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> **What this page is.** B9b (D-226) — admin CRUD over the dynamic
> `SessionCategory` lookup (SIMF-FDS-004 §5.4: *"a dynamic Category, for example
> a main session"*). A small **bilingual** lookup (English name / Arabic name /
> display order / active flag) that a `Session` optionally points at via the
> bare `Session.CategoryId`. It is a **team-seeded lookup, NOT a fixed enum** —
> the table **ships empty** and is seeded once the client confirms the category
> list (open item **OI-2**), so the empty-state path is the default first
> render and nothing is invented in code.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [admin-session-categories_Function.md](admin-session-categories_Function.md) | What the admin does — grid, Add / Edit / Details / Delete, Export / Import, presentation toggle, filter / sort |
| Logic | [admin-session-categories_Logic.md](admin-session-categories_Logic.md) | Field mapping, validation layers, soft-delete idempotence, default order, how `Session.CategoryId` reaches the app, audit |
| API | [admin-session-categories_API.md](admin-session-categories_API.md) | The BFF passthroughs + API endpoints + DTOs + error codes (authoritative contract) |
| Design | [admin-session-categories_Design.md](admin-session-categories_Design.md) | CP screen design — `SimfDataGrid` + `CrudShell` framing, columns, forms, states, RTL |

## Identity

| | |
|---|---|
| Route | `/admin/session-categories` |
| Layout | `CpShellLayout` |
| Permission (page) | `@attribute [RequirePermission(PermissionCatalog.SessionCategories.View)]` |
| Per-action permissions | `SessionCategories.Create` / `.Edit` / `.Delete` / `.Export` / `.Import` (all `AdminOnly` baseline) |
| Nav item | `Module.SessionCategories` → `/admin/session-categories`, `RequiredPermission: SessionCategories.View`, icon `folder` |
| Audience | Administrator (any role granted the `SessionCategories.*` codes; `Administrator = "*"`) |
| Pattern | B9b (D-226) dynamic-lookup CRUD · D-256 `SimfDataGrid` · D-353 `CrudShell` / `SimfConfirm` / presentation toggle · D-356 Excel export + import |
| Status | ✅ Real (D-226). **Ships empty** pending the client's category list — OI-2 |
| Backed by | `dbo.SessionCategories` table (`SimfAppDbContext`, migration `D226_AddSessionCategory`) + nullable `Session.CategoryId` |

## Source files (verified this session)

| Layer | File |
|-------|------|
| CP list page | `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionCategoriesList.razor` |
| CP Add/Edit form | `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionCategoriesAddEdit.razor` |
| CP View/Delete form | `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionCategoriesViewDelete.razor` |
| Nav | `src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs` (`Module.SessionCategories`) |
| BFF passthroughs | `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs` (`/admin/session-categories/*` + `MapGridExcel(group, "session-categories")`) |
| API CRUD endpoints | `src/Backend/SIMF.Api/Endpoints/Admin/SessionCategoryEndpoints.cs` |
| API Excel endpoints | `src/Backend/SIMF.Api/Endpoints/Admin/SessionCategoriesExcelEndpoints.cs` |
| Service | `src/Backend/SIMF.Infrastructure/Programme/AdminSessionCategoryService.cs` |
| Contracts (DTOs) | `src/Shared/SIMF.Contracts/Admin/SessionCategories.cs` |
| Domain entity | `src/Backend/SIMF.Domain/Programme/SessionCategory.cs` |
| EF config | `src/Backend/SIMF.Infrastructure/Persistence/Configurations/App/SessionCategoryConfiguration.cs` |
| Permissions | `src/Shared/SIMF.Common/PermissionCatalog.cs` (`SessionCategories` nested class + `All`) |
| Error codes | `src/Shared/SIMF.Common/ErrorCodes.cs` (`SessionCategoryInvalid` / `SessionCategoryNotFound`) |

## Related docs (cross-links)

- **CP page reference:** [`docs/pages/cp/admin-session-categories.md`](../../pages/cp/admin-session-categories.md)
- **CP E2E catalogue:** [`docs/tests/e2e/cp-admin-session-categories.md`](../../tests/e2e/cp-admin-session-categories.md) (E2E-SCT-001…021)
- **Sibling CP set — sessions:** [`docs/pages/cp/admin-sessions.md`](../../pages/cp/admin-sessions.md) — the CP session form whose category picker consumes this lookup (resolves `CategoryId` → name client-side, like the Hall/Company picker)
- **App consumer — agenda:** [`docs/App/Page_016/`](../../App/Page_016/README.md) — the app agenda (`/sessions`, API `/app/programme/sessions`) carries each session's `categoryId` / `categoryName` / `categoryNameArabic` for the "is-main-session / type" tag (Page_016_Logic L-4 / Page_016_API)
- **Authority spec:** SIMF-FDS-004 §5.4 (dynamic Category) + §7
- **Decisions:** D-226 (built as a team-seeded lookup, ships empty, OI-2); D-256 raw-table → `SimfDataGrid`; D-353 `CrudShell` / `SimfConfirm` + presentation toggle; D-356 Excel export + import

## Sources of truth (read first)

The **code is the source of truth** for this set. Every statement here traces to
the source files listed above, read this session. The controlled spec is
SIMF-FDS-004 §5.4; the binding API conventions are SIMF-API-001
(`ApiResult<T>` envelope, error model, status codes).
