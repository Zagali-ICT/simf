# Profile types — Other · API

The authoritative backend contract serving `/admin/profile-types/other`. The
Visitor and Other CP pages **share these same endpoints** — the page differs only
by the `isVisitor` filter / payload. Grounded in `ProfileTypeEndpoints.cs`,
`ListProfileTypesEndpoint.cs`, `AdminAccount.cs`, the validators, and
`ProfileTypesPickerEndpoint.cs`. All routes carry the global prefix **`api/v1`**
(`config.Endpoints.RoutePrefix = "api/v1"`); responses use the `ApiResult<T>`
envelope.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## How the CP reaches the API
The Razor page calls a same-origin BFF proxy via JS interop
(`simfAccount.postJson` / `putJson` / `deleteJson`) at the `/account/api/admin/profile-types*`
paths, which forward to the API endpoints below. The grid `/list` and the
mutations are the only network calls the page makes.

## Admin endpoints (shared Visitor + Other)

### `POST /api/v1/admin/profile-types/list` — paged grid
- **Endpoint:** `ListAdminProfileTypesEndpoint`
- **Policy:** `PolicyFor(ProfileTypes.View)` + `RequireApprovedAccount`
- **Request:** `GridQuery` (`Skip`, `Top`, `Search`, `Sort`, `SortDescending`,
  `Filters`). The Other page sends `Filters = { userType: "Visitor", isVisitor: "false" }`.
- **Response:** `ApiResult<GridPage<AdminProfileTypeSummary>>`
- **Filters honoured server-side:** `name` (LIKE), `isActive` (bool),
  `isVisitor` (bool → `IsForVisitor`). Sort keys: `name`, `namearabic`, `createdat`.

### `GET /api/v1/admin/profile-types/{id:guid}` — one row
- **Endpoint:** `GetAdminProfileTypeEndpoint`
- **Policy:** `PolicyFor(ProfileTypes.View)` + `RequireApprovedAccount`
- **Response:** `ApiResult<AdminProfileTypeSummary>`; missing id → **404**
  `ProfileTypeNotFound`. (Not called by this list page; part of the contract.)

### `POST /api/v1/admin/profile-types` — create
- **Endpoint:** `CreateAdminProfileTypeEndpoint`
- **Policy:** `PolicyFor(ProfileTypes.Create)` + `RequireApprovedAccount`;
  rate-limited (`auth` bucket).
- **Request:** `AdminCreateProfileTypeRequest` — the Other page sends
  `UserType = "Visitor"`, `IsVisitor = false`, plus `Name`, `NameArabic`,
  `PageColor`, optional `MobileAppRole` (None / Staff / Moderator), `IsActive`.
- **Response:** `ApiResult<AdminProfileTypeSummary>`
- **Errors:** 400 `ProfileTypeInvalidUserType` (non-Visitor scope, or bad/`Visitor`
  mobile-app role); 409 `ProfileTypeNameTaken` (duplicate Name); 401 if `sub` missing.

### `PUT /api/v1/admin/profile-types/{id:guid}` — update
- **Endpoint:** `UpdateAdminProfileTypeEndpoint`
- **Policy:** `PolicyFor(ProfileTypes.Edit)` + `RequireApprovedAccount`;
  rate-limited (`auth` bucket).
- **Route body:** `UpdateAdminProfileTypeRouteRequest` (`Id` from route; `Name`,
  `NameArabic`, `PageColor`, `MobileAppRole?`, `IsActive`, `IsVisitor`). **No
  `UserType`** — scope is immutable. Mapped to `AdminUpdateProfileTypeRequest`.
- **Response:** `ApiResult<AdminProfileTypeSummary>`
- **Errors:** 404 `ProfileTypeNotFound`; 409 `ProfileTypeNameTaken` (name changed
  to a duplicate); 400 on a bad mobile-app role; 401 if `sub` missing.

### `DELETE /api/v1/admin/profile-types/{id:guid}` — deactivate (soft-delete)
- **Endpoint:** `DeactivateAdminProfileTypeEndpoint`
- **Policy:** `PolicyFor(ProfileTypes.Delete)` + `RequireApprovedAccount`;
  rate-limited (`auth` bucket).
- **Response:** `ApiResult<bool>` (`true`). Idempotent.
- **Errors:** 404 `ProfileTypeNotFound`; **409** `ProfileTypeInUse` (any
  `UserProfile` still references the row); 401 if `sub` missing.

### `GET /api/v1/admin/profile-types?userType=…` — flat active list
- **Endpoint:** `ListProfileTypesEndpoint` (`AdminProfileTypeQueryService`) —
  the picker-style read used by the admin create / list pages to populate a
  subtype dropdown (active rows only, ordered by Name). Not the grid `/list`.

## App endpoint (the C5 / D-371 linkage — NOT admin-gated)

### `GET /api/v1/app/account/profile-types?isVisitor=false`
- **Endpoint:** `ProfileTypesPickerEndpoint`
- **Auth:** authenticated (rate-limited `auth` bucket); **not** admin-only,
  **not** approval-gated (the caller is mid-registration).
- **Query:** optional `isVisitor` — `false` → partner rows only. Null → all active.
- **Response:** `ApiResult<ProfileTypePickerListResponse>` of
  `ProfileTypePickerDto(Id, Name, NameArabic, PageColor, IsForVisitor)` — active
  Visitor-scope rows, ordered by Name.
- This is the read [Page 007](../../App/Page_007/README.md) issues under the
  "Other / أخرى" tab (a pick is **required** there per C5 / D-371). Rows
  deactivated on the CP page drop out of this response.

## DTOs (`SIMF.Contracts.Authentication` unless noted)

### `AdminProfileTypeSummary` (record)
`Id` · `Name` · `NameArabic` · `PageColor` · `UserType` (always `"Visitor"`) ·
`MobileAppRole` (enum name `"None"`/`"Staff"`/`"Moderator"`) · `IsActive` ·
`IsVisitor` (audience true / partner false).

### `AdminCreateProfileTypeRequest`
`UserType` (only `"Visitor"` accepted; ≤ 16) · `Name` (1–128, unique) ·
`NameArabic` (1–128) · `PageColor` (1–32) · `MobileAppRole?` (default None) ·
`IsActive` (default true) · `IsVisitor` (default true; this page sends false).

### `AdminUpdateProfileTypeRequest` / `UpdateAdminProfileTypeRouteRequest`
`Name` · `NameArabic` · `PageColor` · `MobileAppRole?` · `IsActive` · `IsVisitor`.
No `UserType` (immutable). The route variant adds `Id`.

### `ProfileTypePickerDto` / `ProfileTypePickerListResponse` (`SIMF.Contracts.UserProfile`)
`ProfileTypePickerDto(Id, Name, NameArabic, PageColor, IsForVisitor)`;
the response wraps the array.

## Error codes (verbatim, bilingual)
| Code | HTTP | EN message |
|------|------|------------|
| `ProfileTypeNotFound` | 404 | The profile type was not found. |
| `ProfileTypeInvalidUserType` | 400 | A profile type may only be created for the Visitor scope. / `'{raw}' is not a valid mobile-app role.` / MobileAppRole.Visitor is resolved from UserType… |
| `ProfileTypeNameTaken` | 409 | A profile type named '{name}' already exists for {userType}. (update: …already exists.) |
| `ProfileTypeInUse` | 409 | The profile type cannot be removed while it is still assigned to one or more accounts. |

## Permissions
`PermissionCatalog.ProfileTypes` — `View` / `Create` / `Edit` / `Delete`
(codes `"ProfileTypes.View"` etc.; `BaselineRoles = AdminOnly`). The page is
gated by `ProfileTypes.View`; each endpoint by its matching action policy.

## Lower-layer tests
`tests/SIMF.Api.Tests/AdminProfileTypeTests.cs` (CRUD, immutable UserType, in-use
409, auth, IsVisitor round-trip + flip audit, partner-side guard) ·
`tests/SIMF.Api.Tests/MobileAppRoleTests.cs` · `tests/SIMF.Api.Tests/ProfileTypePickerTests.cs`
· `tests/SIMF.ControlPanel.Tests/ProfileTypeFormTests.cs`.
