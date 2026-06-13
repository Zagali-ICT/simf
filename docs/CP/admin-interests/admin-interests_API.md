# CP — Interests — API (`/admin/interests`)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
standard headers, error model and auth from SIMF-API-001. The page calls the API
**through the Control Panel BFF** (`/account/api/admin/interests/*`), which forwards
to the API (`/api/v1/admin/interests/*`) with the signed-in admin's bearer token.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

> **Status:** ✅ **BUILT.** CRUD endpoints in
> `src/Backend/SIMF.Api/Endpoints/Admin/InterestEndpoints.cs`; Excel export/import in
> `src/Backend/SIMF.Api/Endpoints/Admin/InterestExcelEndpoints.cs` (D-356). Covered by
> `tests/SIMF.Api.Tests/InterestTests.cs` + `InterestExcelTests.cs`.
>
> **Path-prefix note:** API routes are under **`/api/v1/*`**; the CP page calls the
> BFF mirror **`/account/api/admin/interests/*`** (the JS interop helpers
> `simfAccount.postJson` / `putJson` / `deleteJson` / `downloadXlsx` / `uploadFile`).
> The two paths below are the BFF route (what the page calls) and the API route (what
> the BFF forwards to).

## Permissions (per-action — D-207/D-208)
Each endpoint gates on a per-action permission policy
(`Policies(PermissionCatalog.PolicyFor(code), nameof(AuthorizationPolicies.RequireApprovedAccount))`).
`Administrator` carries the wildcard `*` and so passes all of them.

| Action | Permission code | Notes |
|--------|-----------------|-------|
| List | `Interests.View` | also gates the page (`[RequirePermission]`) + the nav item |
| Get one | `Interests.View` | |
| Create | `Interests.Create` | + `RequireRateLimiting("auth")` |
| Update | `Interests.Edit` | + `RequireRateLimiting("auth")` |
| Deactivate | `Interests.Delete` | + `RequireRateLimiting("auth")` |
| Export | `Interests.Export` | D-356 |
| Import | `Interests.Import` | D-356 |

Every endpoint additionally requires `RequireApprovedAccount` (the admin's
`AccountState` must be `Approved` — a pending admin is blocked).

## E1 — List (page init + every grid query)
| | |
|---|---|
| BFF route | `POST /account/api/admin/interests/list` |
| API route | `POST /api/v1/admin/interests/list` |
| Permission | `Interests.View` |
| Request | `GridQuery { Top, Skip, Sort, SortDescending, Search, Filters }` (page default `Top=20`) |
| Returns | `ApiResult<GridPage<AdminInterestSummary>>` |

Server clamps `Top` to `[1,200]` (default 25 when unset) and `Skip` to `≥0`. The
repository supports `Search` (substring over `Name` + `NameArabic`), per-column
filters (`name`, `nameArabic`, `isActive`), and sort keys `name` / `nameArabic` /
`displayOrder` / `createdAt`. Natural (default) order is `DisplayOrder` then `Name` —
the same order the visitor picker uses.

```jsonc
// AdminInterestSummary (record)
{
  "id":           "guid",
  "name":         "string",          // English label
  "nameArabic":   "string",          // Arabic label
  "displayOrder": 0,                  // int >= 0
  "isActive":     true,
  "createdAt":    "2026-06-13T00:00:00Z"  // DateTimeOffset
}
```

## E2 — Get one (not used by the page, contract-complete)
| | |
|---|---|
| API route | `GET /api/v1/admin/interests/{id:guid}` |
| Permission | `Interests.View` |
| Returns | `ApiResult<AdminInterestSummary>`; **404** `INTEREST_NOT_FOUND` when missing |

The CP page does **not** call this — the grid row already carries every editable
field, so Edit/Details open straight from the in-memory row (no extra GET). The
endpoint exists for completeness / external callers.

## E3 — Create (toolbar Add → submit)
| | |
|---|---|
| BFF route | `POST /account/api/admin/interests` |
| API route | `POST /api/v1/admin/interests` |
| Permission | `Interests.Create` (+ `auth` rate-limit bucket) |
| Request | `AdminCreateInterestRequest { Name, NameArabic, DisplayOrder }` |
| Returns | `ApiResult<AdminInterestSummary>` (the created row) |

```jsonc
// AdminCreateInterestRequest
{ "name": "Naval Engineering", "nameArabic": "الهندسة البحرية", "displayOrder": 10 }
```

Server validates (`AdminCreateInterestRequestValidator`), rejects a duplicate `Name`
with **409** `INTEREST_NAME_DUPLICATE`, creates the row (`IsActive = true`,
`Id = Guid.NewGuid()`, `CreatedAt = now`), and writes one audit entry
(`AuditEvents.InterestCreated`).

## E4 — Update (per-row Edit → submit)
| | |
|---|---|
| BFF route | `PUT /account/api/admin/interests/{id}` |
| API route | `PUT /api/v1/admin/interests/{id:guid}` |
| Permission | `Interests.Edit` (+ `auth` rate-limit bucket) |
| Request | `AdminUpdateInterestRequest { Name, NameArabic, DisplayOrder, IsActive }` (route id + body merged by FastEndpoints) |
| Returns | `ApiResult<AdminInterestSummary>` (the updated row); **404** `INTEREST_NOT_FOUND` when missing |

A rename that collides with another row returns **409** `INTEREST_NAME_DUPLICATE`
(the collision check skips when the name is unchanged). `IsActive` here is how an
admin **re-activates** a previously deactivated interest. Audited
(`AuditEvents.InterestUpdated`).

## E5 — Deactivate (per-row Deactivate → confirm)
| | |
|---|---|
| BFF route | `DELETE /account/api/admin/interests/{id}` |
| API route | `DELETE /api/v1/admin/interests/{id:guid}` |
| Permission | `Interests.Delete` (+ `auth` rate-limit bucket) |
| Request | — (id in the route) |
| Returns | `ApiResult<bool>` (`true`); **404** `INTEREST_NOT_FOUND` when missing |

**Soft delete** — sets `IsActive = false` (the row stays). **Idempotent**: a row that
is already inactive returns success with no further write. Audited
(`AuditEvents.InterestDeactivated`). There is no hard delete.

## E6 — Export (toolbar Export — D-356)
| | |
|---|---|
| BFF route | `POST /account/api/admin/interests/export` |
| API route | `POST /api/v1/admin/interests/export` (subclass of `AdminGridExportEndpoint<AdminInterestSummary>`) |
| Permission | `Interests.Export` |
| Request | `AdminGridExportRequest { Ids, Query }` — selected `Ids`, or (when none selected) the current `Query` |
| Returns | an **`.xlsx`** download, sheet **`Interests`**, file prefix **`simf-interests`** |

Columns (in order): **`Name` · `NameArabic` · `DisplayOrder` · `IsActive`**. When
rows are selected the workbook holds exactly those; otherwise it holds the current
filtered grid (the API caps the row count).

## E7 — Import (toolbar Import — D-356)
| | |
|---|---|
| BFF route | `POST /account/api/admin/interests/import` (multipart) |
| API route | `POST /api/v1/admin/interests/import` (subclass of `AdminGridImportEndpoint`) |
| Permission | `Interests.Import` |
| Request | an uploaded `.xlsx` whose sheet is named **`Interests`** with required headers **`Name`, `NameArabic`** |
| Returns | `ApiResult<AdminGridImportResult> { Created, Updated, Skipped, Errors[] }` |

**Insert-only.** Each row binds to `AdminCreateInterestRequest`
(`Name`, `NameArabic`, `DisplayOrder` — parsed, default 0). A blank `Name` or a
duplicate is a **per-row error** (row number + key + reason), not a batch abort — the
other rows still import. A non-`.xlsx` upload (fails the ZIP-magic check) or a
wrong-sheet workbook is rejected **400** with the bilingual message and nothing is
created.

## Error responses
| HTTP | Code | When |
|------|------|------|
| 400 | `VALIDATION_*` / validation envelope | empty/over-128 Name or NameArabic, negative DisplayOrder |
| 400 | (import) bilingual upload error | not a valid `.xlsx`, or sheet not named `Interests` |
| 404 | `INTEREST_NOT_FOUND` | Get / Update / Deactivate on a missing id |
| 409 | `INTEREST_NAME_DUPLICATE` | Create or Update collides with an existing `Name` (unique index on `Interest.Name`) |
| 401 | — | the `sub` claim is absent on a mutating call (`Send.UnauthorizedAsync`) |

> The duplicate-name code is **`INTEREST_NAME_DUPLICATE`** (constant
> `ErrorCodes.InterestNameDuplicate`) — verified in `ErrorCodes.cs` and `InterestService.cs`.
> The older `docs/pages/cp/admin-interests.md` calls it `InterestNameNotUnique`; that
> string does not exist in the code.

## App contract (consuming endpoint — read-only here)
| | |
|---|---|
| Route | `GET /api/v1/app/account/interests` |
| Access | **auth required** (access-token gated; not anonymous) + `RequireRateLimiting("auth")` |
| Returns | `ApiResult<InterestListResponse>` where `InterestListResponse(IReadOnlyList<InterestDto> Interests)` |
| Row shape | `InterestDto(Guid Id, string Name, string NameArabic, int DisplayOrder)` |
| Filter / order | **active only**, ordered by `DisplayOrder` then `Name` |

This is what the app's interests step (Page 007‑01) fetches; it is the read side of
the same `Interests` table this CP page writes. See
[`../../App/Page_007-01/Page_007-01_API.md`](../../App/Page_007-01/Page_007-01_API.md).

## Build dependencies
None outstanding. The table is App-side (`SimfAppDbContext`, D-157) with no
cross-database relation; the visitor link is the EF-generated `UserProfileInterests`
join. Export/import reuse the generic D-356 grid base classes.
