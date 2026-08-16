# Halls — API (`/admin/halls`)

Authoritative backend contract for the CP Halls page. Inherits the
`ApiResult<T>` envelope, headers, error model and auth from SIMF-API-001 §3–§4.
All five endpoints live in
[`HallEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/HallEndpoints.cs)
(FastEndpoints) and are served by
[`AdminHallService`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminHallService.cs).
Verified against code this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> **Path prefix.** Admin routes are under **`/api/v1/admin/*`** (App↔CP split,
> D-247) — so the routes below are `POST /api/v1/admin/halls/list` etc. The
> Control Panel never calls the API directly: it posts to the **BFF proxy**
> `/account/api/admin/halls/*` in
> [`AccountEndpoints.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs)
> (lines 1015–1057), which forwards the authenticated call to the API. The JSON
> bodies below are the same on both hops.
>
> **Auth (all five).** `Policies(PermissionCatalog.PolicyFor(<perm>),
> nameof(AuthorizationPolicies.RequireApprovedAccount))` — a signed-in,
> approved admin whose role grants the permission (or `Administrator = "*"`). The
> three mutations also carry `Options(rb => rb.RequireRateLimiting("auth"))`.
> `Tags("Admin")`.

## E1 — `POST /admin/halls/list` (grid list) — `Halls.View`
| | |
|---|---|
| Full route | `POST /api/v1/admin/halls/list` |
| Permission | `PermissionCatalog.Halls.View` (`"Halls.View"`) |
| Request | `GridQuery` — `{ Skip, Top, Search?, Sort?, SortDescending, Filters: { } }` |
| Returns | `ApiResult<GridPage<AdminHallSummary>>` |

`AdminHallSummary` (record, `SIMF.Contracts/Admin/Halls.cs`):
```jsonc
{
  "id":         "guid",
  "code":       "string",
  "name":       "string",
  "nameArabic": "string",
  "capacity":   0,            // int
  "floor":      "string?",
  "isActive":   true,
  "createdAt":  "2026-05-28T00:00:00Z",  // DateTimeOffset
  "purpose":    0             // int — HallPurpose (D-248; 0 = General). Append-only (D-219)
}
```
- **Search** (`GridQuery.Search`) → SQL `LIKE %term%` over `Code`, `Name`,
  `NameArabic`.
- **Per-column filters** (`GridQuery.Filters`, D-255) — `code`, `name`,
  `namearabic`, `floor` (substring `Contains`; `floor` guards null). `isActive`
  (parsed bool) filters status. Unknown keys ignored.
- **Sort** — `code` (default), `name`, `namearabic`, `capacity` (asc/desc).
- **Paging** — `Skip ≥ 0`; `Top` clamped to `[1,200]`, default `25` when unset
  (the CP page sends `Top = 20`).

## E2 — `GET /admin/halls/{id}` (detail) — `Halls.View`
| | |
|---|---|
| Full route | `GET /api/v1/admin/halls/{id:guid}` |
| Permission | `PermissionCatalog.Halls.View` |
| Returns | `ApiResult<AdminHallDetail>`; **404 `HALL_NOT_FOUND`** when missing |

`AdminHallDetail` (record):
```jsonc
{
  "id":                   "guid",
  "code":                 "string",
  "name":                 "string",
  "nameArabic":           "string",
  "capacity":             0,
  "floor":                "string?",
  "facilityNotes":       "string?",
  "isActive":             true,
  "createdAt":            "DateTimeOffset",
  "updatedAt":            "DateTimeOffset?",
  // P5.1 — D-240 optional GPS geofence (all null when not configured; append-only D-219)
  "geofenceCenterLat":    null,   // double?
  "geofenceCenterLon":    null,   // double?
  "geofenceRadiusMeters": null    // double?
}
```
The detail carries `facilityNotes` + the geofence triple that the grid summary
(E1) omits — the CP Edit / Details / Deactivate forms fetch it per id.

## E3 — `POST /admin/halls` (create) — `Halls.Create`
| | |
|---|---|
| Full route | `POST /api/v1/admin/halls` |
| Permission | `PermissionCatalog.Halls.Create` (`"Halls.Create"`) + `RequireRateLimiting("auth")` |
| Request | `AdminCreateHallRequest` |
| Returns | `ApiResult<AdminHallDetail>` (the created hall) |

`AdminCreateHallRequest`:
```jsonc
{
  "code":                 "string",   // 2–16; uppercased server-side; unique
  "name":                 "string",   // 1–128
  "nameArabic":           "string",   // 1–128
  "capacity":             0,          // int ≥ 0
  "floor":                "string?",  // ≤ 32
  "facilityNotes":       "string?",  // ≤ 1024
  "geofenceCenterLat":    null,       // double? — all three together, or all null
  "geofenceCenterLon":    null,
  "geofenceRadiusMeters": null
}
```
Sets `IsActive = true`, `CreatedAt = now`; writes audit `Hall.Created`. The actor
is the `sub` claim (401 if unparseable).

## E4 — `PUT /admin/halls/{id}` (update) — `Halls.Edit`
| | |
|---|---|
| Full route | `PUT /api/v1/admin/halls/{id:guid}` |
| Permission | `PermissionCatalog.Halls.Edit` (`"Halls.Edit"`) + `RequireRateLimiting("auth")` |
| Request | `UpdateHallRequest` (endpoint body) → mapped to `AdminUpdateHallRequest` |
| Returns | `ApiResult<AdminHallDetail>`; **404** when missing |

`UpdateHallRequest` body = the create fields **plus** `isActive` (bool, default
true) and the geofence triple. Re-validates everything; re-checks the unique
`Code` only when it changed (case-insensitive); sets `UpdatedAt = now`; writes
audit `Hall.Updated`. Re-activation is via `isActive = true` on this endpoint
(there is no separate activate route).

## E5 — `DELETE /admin/halls/{id}` (soft-delete) — `Halls.Delete`
| | |
|---|---|
| Full route | `DELETE /api/v1/admin/halls/{id:guid}` |
| Permission | `PermissionCatalog.Halls.Delete` (`"Halls.Delete"`) + `RequireRateLimiting("auth")` |
| Returns | `ApiResult<bool>` (`true`); **404** when missing |

**Soft-delete only** — sets `IsActive = false`, `UpdatedAt = now`, writes audit
`Hall.Deactivated`. Idempotent: an already-inactive hall returns without a second
write. The row is **not** physically removed.

## Excel endpoints (generic D-356 grid layer)
| Route | Permission | Notes |
|-------|------------|-------|
| `POST /api/v1/admin/halls/export` | `Halls.Export` | `AdminGridExportRequest { Ids, Query }` → `.xlsx`; header `Code | Name | NameArabic | Capacity | Floor | IsActive`; ≤ 5000 rows |
| `POST /api/v1/admin/halls/import` | `Halls.Import` | multipart `.xlsx`; per-row create/update; non-`.xlsx` / oversized / wrong-sheet → 400 |

(These are wired generically by `MapGridExcel(group, "halls")` —
`AccountEndpoints.cs` line 541 — and the matching API registration. The CP calls
them through `CrudGridExcel Resource="halls"`.)

## Error responses (`ErrorCodes.cs`)
| HTTP | `ApiResult.Error.Code` | When |
|------|------------------------|------|
| 400 | `HALL_INVALID` | Code length ∉ [2,16]; Name/Arabic length ∉ [1,128]; Capacity < 0; Floor > 32; Equipment notes > 1024 |
| 400 | `HALL_GEOFENCE_INVALID` | partial geofence (not all three) ; lat ∉ [−90,90] / lon ∉ [−180,180]; radius ≤ 0 or > 100000 m |
| 404 | `HALL_NOT_FOUND` | get / update / delete on a missing id |
| 409 | `HALL_CODE_DUPLICATE` | create / update to a `Code` already used (case-insensitive) |
| 409 | `HALL_IN_USE` | delete — or an update clearing `IsActive` — on a hall that active sessions still use; the message names the count (A37) |

Every server message is **bilingual** (`ApiException(code, status, en, ar)`);
the CP surfaces `MessageForCurrentCulture()`.

## How the same `Hall` reaches the app
The app never calls these admin endpoints. The hall surfaces on the **public**
session read consumed by [App Page 016](../../App/Page_016/Page_016_API.md):
`GET /api/v1/app/programme/sessions` projects, per `PublicSessionListItem`,
`hallId = session.HallId`, `hallName = session.Hall!.Name`,
`hallNameArabic = session.Hall!.NameArabic`
([`ProgrammeSessionService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/ProgrammeSessionService.cs)
lines 59–61, `.Include(row => row.Hall)`). The hall's `Capacity` backs the
session booking cap (`CapacityOverride ?? Hall.Capacity`, line 194). The venue
map (Page 015) reads the hall via `VenueMapNode.HallId` (a separate real FK,
restrict-delete). See [Logic](admin-halls_Logic.md) L-8/L-9.

## Audit + tests
- Audit events (`SIMF.Application/Auditing/AuditEvents.cs`): `Hall.Created`,
  `Hall.Updated`, `Hall.Deactivated` — each with the actor user id + a detail
  string (`id=…; code=…`).
- Lower-layer tests this session: `tests/SIMF.Api.Tests/AdminHallGeofenceTests.cs`
  (geofence parse + persistence). The endpoint + service both carry a
  `// Tests: SIMF.Api.Tests/AdminHallsTests.cs` header for an intended CRUD suite
  that **does not exist yet** — E2E-HAL-001..009 are the current regression
  record for hall CRUD/validation/conflict (see
  [`cp-admin-halls.md`](../../tests/e2e/cp-admin-halls.md)).
