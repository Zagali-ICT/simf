# CP — الاهتمامات · Interests (profile-interests reference data) — `/admin/interests`

Per-page documentation folder for the Control Panel **Interests** config page.
Everything about this CP page lives here.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

This is a Control Panel **reference-data** page, and the **canonical `SimfDataGrid`
list-page exemplar** (the CP list-page standard reference implementation — D-117 /
D-132 / D-353 / D-356). It maintains the single interest-topics list that the mobile
app's **interests step (Page 007‑01)** picker (`GET /app/account/interests`) consumes,
and that a visitor's profile links to many-to-many (the EF join table
`UserProfileInterests`).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [admin-interests_Function.md](admin-interests_Function.md) | What the admin does — grid, toolbar, per-row actions, the Add/Edit/View/Delete forms, Excel import/export, the presentation toggle |
| Logic | [admin-interests_Logic.md](admin-interests_Logic.md) | Business rules — the unique English name, validation, duplicate/uniqueness rules, soft-delete, ordering, the app-picker consumption contract |
| API | [admin-interests_API.md](admin-interests_API.md) | The BFF → API endpoints + DTOs (authoritative contract), permissions, error codes |
| Design | [admin-interests_Design.md](admin-interests_Design.md) | CP screen design — banner, `SimfDataGrid` columns, `CrudShell` forms, states, i18n / RTL |

## Identity
| | |
|---|---|
| Route | `/admin/interests` (`@page` in `InterestsList.razor`) |
| Layout | `CpShellLayout` |
| Titles | Banner: resx `Admin.Interests.Title` (AR **الاهتمامات** · EN **Interests**) |
| Section | Reference data / lookups (admin) |
| Nature | **Canonical CRUD over a reference-data lookup** (`SimfDataGrid` + `CrudShell`), with Excel import/export |
| Permission (page gate) | `PermissionCatalog.Interests.View` — `@attribute [RequirePermission(PermissionCatalog.Interests.View)]` |
| Nav item | `Module.AdminInterests` → `/admin/interests`, icon `tag`, `RequiredPermission = Interests.View`, under group `Nav.ReferenceData` (`CpNavigation.cs`) |
| Backed by | `dbo.Interests` (`SimfAppDbContext`). Primary key = `Guid` (`UserInterest.Id`, assigned `Guid.NewGuid()` on create) |
| Status | ✅ Real — D-050 (CRUD + lookup, P9), D-132 (popup CRUD pattern), D-209 (repository split), D-353 (CrudShell presentation toggle), D-356 (Excel export + import) |

## Source files (verified this session)
| File | Role |
|------|------|
| `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestsList.razor` | The page — banner + `SimfDataGrid` + `CrudShell` host + toolbar wiring + `CrudGridExcel` |
| `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestAddEdit.razor` | Reusable Add/Edit form (`CrudAddEditFormBase<AdminInterestSummary>`) |
| `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestViewDelete.razor` | Reusable View/Delete form (`CrudViewDeleteFormBase<AdminInterestSummary>`) + `SimfConfirm`-gated deactivate |
| `src/Backend/SIMF.Api/Endpoints/Admin/InterestEndpoints.cs` | List / Get / Create / Update / Deactivate endpoints |
| `src/Backend/SIMF.Api/Endpoints/Admin/InterestExcelEndpoints.cs` | Export + Import endpoints (D-356; subclass the generic `AdminGridExportEndpoint<T>` / `AdminGridImportEndpoint`) |
| `src/Backend/SIMF.Api/Endpoints/Admin/Validators/AdminInterestValidators.cs` | FluentValidation create/update validators |
| `src/Backend/SIMF.Api/Endpoints/Account/InterestsListEndpoint.cs` | The **app** lookup endpoint `GET /app/account/interests` |
| `src/Application/.../IdentityAccess/InterestService.cs` + `InterestRepository.cs` | Orchestration (validation/audit/log) + EF persistence over `SimfAppDbContext` |
| `src/Shared/SIMF.Contracts/Admin/Interests.cs` | `AdminInterestSummary` / `AdminCreateInterestRequest` / `AdminUpdateInterestRequest` |
| `src/Shared/SIMF.Contracts/UserProfile/Interest.cs` | `InterestDto` / `InterestListResponse` (the app wire shape) |
| `src/Backend/SIMF.Domain/Profiles/UserInterest.cs` | The entity (`BaseAuditEntity`; `Name` / `NameArabic` / `DisplayOrder`) |
| `src/Backend/SIMF.Infrastructure/Persistence/Configurations/App/InterestConfiguration.cs` | EF config — table `Interests`, 128-char caps, unique index on `Name`, `(IsActive, DisplayOrder)` index |
| `src/Shared/SIMF.Common/PermissionCatalog.cs` | `PermissionCatalog.Interests` codes (`View`/`Create`/`Edit`/`Delete`/`Export`/`Import`) + `All` registrations |
| `src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs` | `Module.AdminInterests` nav entry |

## App linkage (the reason this page exists)
The mobile app reads this list — verbatim route, verified in
`src/Backend/SIMF.Api/Endpoints/Account/InterestsListEndpoint.cs`:

```
GET /api/v1/app/account/interests  →  ApiResult<InterestListResponse>
```

The app endpoint returns **only active rows** (`interest.IsActive`), ordered by the
admin-set **`DisplayOrder`** then the **English name** as a stable tiebreaker, and
projects each row to `InterestDto(Id, Name, NameArabic, DisplayOrder)`. The interests
step (Page 007‑01) renders these as selectable pills and requires the user to pick
**1–10**; the selected `Id`s ride the single profile upsert (`POST /app/account/user-profile`)
and become rows in the `UserProfileInterests` many-to-many join.

> Whatever an admin creates / edits / orders / deactivates here is exactly what the
> app's Page 007‑01 picker shows on its next fetch. **Deactivating** an interest
> removes it from the picker (the app filters on `IsActive`) but leaves existing
> visitor links intact — a visitor who already chose it keeps the link; the picker
> just stops offering it to new visitors. Re-activating restores it. The `Guid` `Id`
> set here is the primary key the picker's selection resolves against.

## Sources of truth (read first)
- Format template: `docs/App/Page_016/README.md` + `Page_016_Design.md`.
- CP reference doc (content source, verified vs code): `docs/pages/cp/admin-interests.md`.
- E2E catalogue: `docs/tests/e2e/cp-admin-interests.md` (`E2E-INT-001…013`).
- Consuming app screen: `docs/App/Page_007-01/` (the interests picker, 1–10).
- Sibling CP reference-data sets: `docs/CP/admin-countries/`, `docs/CP/admin-organisations/`.
- Decisions: D-050 (Interest CRUD + lookup, P9), D-132 (popup CRUD pattern),
  D-157 (Data ↔ Identity separation — `Interest` is App-side), D-209 (repository
  split), D-353 (CrudShell presentation toggle), D-356 (grid Excel export + import).

## Cross-links
- CP reference doc: [`../../pages/cp/admin-interests.md`](../../pages/cp/admin-interests.md)
- CP E2E catalogue: [`../../tests/e2e/cp-admin-interests.md`](../../tests/e2e/cp-admin-interests.md)
- Consuming app page: [`../../App/Page_007-01/README.md`](../../App/Page_007-01/README.md)
  (interests lookup in [`../../App/Page_007-01/Page_007-01_API.md`](../../App/Page_007-01/Page_007-01_API.md))
- Permission catalogue: `docs/SIMF-Permission-Catalogue.md` (`PermissionCatalog.Interests`)

> **Drift note (report-only, no code change):** the older `docs/pages/cp/admin-interests.md`
> states the auth gate as `[Authorize(Roles = "Administrator")]` and the duplicate-name
> error code as `InterestNameNotUnique`. The **code** gates the page with
> `@attribute [RequirePermission(PermissionCatalog.Interests.View)]` and the per-action
> permission policies (`Interests.View/Create/Edit/Delete`), and the duplicate code is
> `ErrorCodes.InterestNameDuplicate` (`INTEREST_NAME_DUPLICATE`). This set documents the
> as-built code; the older doc predates the permission system (D-207/D-208) and is stale
> on those two points.
