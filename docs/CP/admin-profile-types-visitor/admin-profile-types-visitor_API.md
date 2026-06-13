# CP page — API (أنواع ملفات الزوار · Visitor profile types)

Authoritative backend contract for this page. Inherits the `ApiResult<T>`
envelope, headers, error model and auth from SIMF-API-001. The page (Blazor
Server) calls these through the **BFF proxy** (`simfAccount.*` JS interop →
`/account/api/admin/profile-types*`), which forwards to the API on `:5175`
under the `/api/v1` prefix.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

> **Shared backend.** These five endpoints back **both** the Visitor and the
> Other CP pages — the audience-vs-partner split rides entirely on the
> `IsVisitor` flag (the `ProfileType.IsForVisitor` column), not on separate
> routes. Source:
> `src/Backend/SIMF.Api/Endpoints/Admin/ProfileTypeEndpoints.cs` +
> `AdminProfileTypeCommandService.cs`.
>
> **Path-prefix note.** Admin routes live under **`/api/v1/admin/*`**; the BFF
> proxies them under `/account/api/admin/*`. Routes below show the API path.

## How the page calls the API (BFF proxy)
| Page action | JS interop call | Proxied path |
|-------------|-----------------|--------------|
| Load list | `simfAccount.postJson(…)` | `POST /account/api/admin/profile-types/list` (body = `GridQuery` with `Filters.userType="Visitor"`, `Filters.isVisitor="true"`) |
| Create | `simfAccount.postJson(…)` | `POST /account/api/admin/profile-types` (`AdminCreateProfileTypeRequest`, `UserType="Visitor"`, `IsVisitor=true`) |
| Update | `simfAccount.putJson(…)` | `PUT /account/api/admin/profile-types/{id}` (`AdminUpdateProfileTypeRequest`) |
| Deactivate | `simfAccount.deleteJson(…)` | `DELETE /account/api/admin/profile-types/{id}` |

The page does **not** use the Get-by-id or the legacy `GET /admin/profile-types`
endpoints; they are documented below for completeness (E2 / E6).

## E1 — `POST /admin/profile-types/list`  (paged grid)
| | |
|---|---|
| Full route | `POST /api/v1/admin/profile-types/list` |
| Endpoint | `ListAdminProfileTypesEndpoint` |
| Policy | `PermissionCatalog.ProfileTypes.View` + `RequireApprovedAccount` |
| Body | `GridQuery` (`Skip`, `Top`, `Search`, `Sort`, `SortDescending`, `Filters`) |
| Returns | `ApiResult<GridPage<AdminProfileTypeSummary>>` |

Server filter handling (`AdminProfileTypeCommandService.ListAllAsync`):
- `Top` clamped to **1–200** (default 25 when ≤ 0); `Skip` floored at 0.
- `Search` → `LIKE %term%` over **Name OR NameArabic**.
- `Filters["name"]` → `LIKE %…%` over **Name**.
- `Filters["isActive"]` (bool) → `IsActive ==`.
- `Filters["isVisitor"]` (bool) → **`IsForVisitor ==`** (the page sets `true`).
- Sort keys: `name`, `namearabic`, `createdat` (asc/desc); default order by `Name`.

> **`userType` pin:** the page always sends `Filters["userType"]="Visitor"`,
> but the **server list query does not branch on `userType`** — after the
> D-186 collapse every non-admin row is `UserType.Visitor`, and the
> audience/partner split is `isVisitor`. The summary's `UserType` field is
> hard-set to `"Visitor"` for every row. So the **`isVisitor` filter** is what
> actually scopes this page's list; the `userType` pin is a structural belt-
> and-braces (and matters for Create — E3).

```jsonc
// AdminProfileTypeSummary  (SIMF.Contracts/Authentication/AdminAccount.cs)
{
  "id":          "guid",
  "name":        "string",
  "nameArabic":  "string",
  "pageColor":   "string",   // hex / 3-digit hex / "var(--…)" CSS variable
  "userType":    "Visitor",  // always "Visitor" post-D-186
  "mobileAppRole":"None",     // enum name: "None" | "Staff" | "Moderator"
  "isActive":    true,
  "isVisitor":   true         // audience (true) vs partner (false)
}
```

## E2 — `GET /admin/profile-types/{id}`  (single row)  *(not used by the page)*
| | |
|---|---|
| Full route | `GET /api/v1/admin/profile-types/{id:guid}` |
| Endpoint | `GetAdminProfileTypeEndpoint` |
| Policy | `ProfileTypes.View` + `RequireApprovedAccount` |
| Returns | `ApiResult<AdminProfileTypeSummary>`; **404 `PROFILE_TYPE_NOT_FOUND`** when missing |

The Details modal renders from the **row already in the grid**, so the page
never calls this; it exists for direct/API consumers.

## E3 — `POST /admin/profile-types`  (create)
| | |
|---|---|
| Full route | `POST /api/v1/admin/profile-types` |
| Endpoint | `CreateAdminProfileTypeEndpoint` |
| Policy | `ProfileTypes.Create` + `RequireApprovedAccount` · rate-limit bucket **`auth`** |
| Body | `AdminCreateProfileTypeRequest` |
| Returns | `ApiResult<AdminProfileTypeSummary>` |

```jsonc
// AdminCreateProfileTypeRequest
{
  "userType":     "Visitor",   // the page hard-codes "Visitor"
  "name":         "string",    // 1–128
  "nameArabic":   "string",    // 1–128
  "pageColor":    "string",    // 1–32
  "mobileAppRole":null,         // the Visitor page sends null (picker hidden)
  "isActive":     true,
  "isVisitor":    true          // = !IsPartnerForm → true on this page
}
```

Server rules (`CreateAsync`):
- **UserType must parse to `Visitor`** — anything else (incl. `Admin`,
  `Other`) → **400 `PROFILE_TYPE_INVALID_USER_TYPE`** ("A profile type may
  only be created for the Visitor scope." / "لا يمكن إنشاء نوع ملف شخصي إلا ضمن نطاق الزائر.").
- **Per-name uniqueness** across the table (case-insensitive via SQL Server
  collation) → **409 `PROFILE_TYPE_NAME_TAKEN`** ("A profile type named
  '{name}' already exists for Visitor." / "يوجد نوع ملف شخصي بالاسم '{name}' لـ Visitor بالفعل.").
- `MobileAppRole` parsed from the string; unknown value **or** `Visitor`
  → **400 `PROFILE_TYPE_INVALID_USER_TYPE`** (Visitor is resolved from
  UserType, never assigned per row). The page sends `null` → defaults to `None`.
- Audits `AuditEvents.ProfileTypeCreated`.

## E4 — `PUT /admin/profile-types/{id}`  (update)
| | |
|---|---|
| Full route | `PUT /api/v1/admin/profile-types/{id:guid}` |
| Endpoint | `UpdateAdminProfileTypeEndpoint` |
| Policy | `ProfileTypes.Edit` + `RequireApprovedAccount` · rate-limit **`auth`** |
| Body | `AdminUpdateProfileTypeRequest` (**no UserType** — it is not updatable) |
| Returns | `ApiResult<AdminProfileTypeSummary>` |

```jsonc
// AdminUpdateProfileTypeRequest
{
  "name":         "string",    // 1–128
  "nameArabic":   "string",    // 1–128
  "pageColor":    "string",    // 1–32
  "mobileAppRole":null,         // Visitor page sends null
  "isActive":     true,
  "isVisitor":    true          // the form preserves Initial.IsVisitor
}
```

Server rules (`UpdateAsync`):
- **404 `PROFILE_TYPE_NOT_FOUND`** when the id is unknown.
- On a name change, the same uniqueness check → **409
  `PROFILE_TYPE_NAME_TAKEN`** ("A profile type named '{name}' already exists." /
  "يوجد نوع ملف شخصي بالاسم '{name}' بالفعل.").
- **`IsVisitor` IS mutable** (D-186 audience↔partner re-routing). A flip is
  audited with the old/new flag **and the count of linked accounts** (SOC trail).
- Audits `AuditEvents.ProfileTypeUpdated`.

## E5 — `DELETE /admin/profile-types/{id}`  (soft-delete)
| | |
|---|---|
| Full route | `DELETE /api/v1/admin/profile-types/{id:guid}` |
| Endpoint | `DeactivateAdminProfileTypeEndpoint` |
| Policy | `ProfileTypes.Delete` + `RequireApprovedAccount` · rate-limit **`auth`** |
| Returns | `ApiResult<bool>` (`Data=true`) |

Server rules (`DeactivateAsync`):
- **404 `PROFILE_TYPE_NOT_FOUND`** when unknown.
- **In-use guard:** if any `UserProfile.ProfileTypeId == id` → **409
  `PROFILE_TYPE_IN_USE`** ("The profile type cannot be removed while it is
  still assigned to one or more accounts." / "لا يمكن إزالة نوع الملف الشخصي طالما لا يزال مُسنداً إلى حساب واحد أو أكثر.").
  The CP red-toasts the server message verbatim (falling back to
  `Admin.ProfileTypes.Delete.InUse` only if the server message is empty).
- **Idempotent:** already-inactive rows return success without a second write.
- Soft-delete = `IsActive=false`; the active-filtered list drops it. Audits
  `AuditEvents.ProfileTypeDeactivated`.

## E6 — `GET /admin/profile-types?userType=…`  (legacy flat list)  *(not used by this page)*
| | |
|---|---|
| Full route | `GET /api/v1/admin/profile-types?userType=Visitor` |
| Endpoint | `ListProfileTypesEndpoint` |
| Policy | `ProfileTypes.View` + `RequireApprovedAccount` |
| Returns | `ApiResult<IReadOnlyList<AdminProfileTypeSummary>>` (active rows for the UserType; unknown UserType → empty list) |

Drives the **CP create-account subtype dropdown** (D-048), **not** this grid
page. Listed so the route inventory is complete.

## E7 — `GET /app/account/profile-types?isVisitor={bool}`  (app picker — consumer)
| | |
|---|---|
| Full route | `GET /api/v1/app/account/profile-types?isVisitor=true` (D-190) |
| Endpoint | `ProfileTypesPickerEndpoint` |
| Access | **Authenticated** (not admin-only, not approval-gated — the caller is mid-registration); rate-limit **`auth`** |
| Returns | `ApiResult<ProfileTypePickerListResponse>` — active rows (`id, name, nameArabic, pageColor, isVisitor`), `OrderBy(Name)`; `?isVisitor=true` → audience rows only |

This is **how the app reads the rows this page manages.** The visitor sign-up
profile form ([Page_007](../../App/Page_007/)) loads `?isVisitor=true`; per
**C5 (D-371)** the Visitor tab auto-locks to the single seeded **"Normal" /
"عادي"** row (no picker), while the Other tab loads `?isVisitor=false` and
shows the partner picker. Soft-deleted rows never appear (the endpoint filters
`IsActive`).

## Error codes (summary)
| Code | HTTP | When |
|------|------|------|
| `PROFILE_TYPE_INVALID_USER_TYPE` | 400 | Create with a non-Visitor UserType, or an invalid / `Visitor` MobileAppRole |
| `PROFILE_TYPE_NAME_TAKEN` | 409 | Duplicate Name (create, or rename on update) |
| `PROFILE_TYPE_IN_USE` | 409 | Deactivate a row still referenced by a `UserProfile` |
| `PROFILE_TYPE_NOT_FOUND` | 404 | Get / update / delete an unknown id |
| (field-shape) | 400 | FluentValidation: Name / NameArabic ≤128, PageColor ≤32, all required, UserType required ≤16 |

## Validation alignment (per CLAUDE.md §7)
`Name`/`NameArabic` **1–128**, `PageColor` **1–32** — the CP client guard
(`ProfileTypeForm.HandleSubmitAsync`), the FluentValidation validators
(`AdminProfileTypeRequestValidators`) and the EF column maxes all agree.

## Build dependencies
None outstanding — all five endpoints + the app picker are **BUILT** (D-115,
D-118, D-161, D-186, D-190). Covered by
`tests/SIMF.Api.Tests/AdminProfileTypeTests.cs` (CRUD, uniqueness, in-use
guard, UserType guard, IsVisitor round-trip, forbidden non-admin) and
`tests/SIMF.Api.Tests/ProfileTypePickerTests.cs` (the app picker).
