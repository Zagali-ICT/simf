# Exhibition booths — API (`/admin/booths`)

Authoritative backend contract for this CP page. Inherits the `ApiResult<T>`
envelope, standard headers, error model and auth from SIMF-API-001. Page rules
are in [admin-booths_Logic.md](admin-booths_Logic.md).

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> **Two hops.** The CP page calls the **BFF** at `/account/api/admin/booths/*`
> (Blazor Server JS interop `simfAccount.{post,get,put,delete}Json`); the BFF
> forwards one-for-one to the **API** at `/api/v1/admin/booths/*`. The CRUD
> passthroughs are in `AccountEndpoints.cs` lines 2290–2318; the two Excel routes
> are wired generically via `MapGridExcel(group, "booths")`. The API endpoints
> below are the source of truth (`Endpoints/Admin/BoothEndpoints.cs` +
> `BoothsExcelEndpoints.cs`).
>
> All admin endpoints also carry
> `nameof(AuthorizationPolicies.RequireApprovedAccount)` and `Tags("Admin")`.
> Mutating + Excel endpoints add `Options(rb => rb.RequireRateLimiting("auth"))`.

## A1 — `POST /admin/booths/list`  (grid page)
| | |
|---|---|
| Full route | `POST /api/v1/admin/booths/list` (BFF `POST /account/api/admin/booths/list`) |
| Source | `ListBoothsEndpoint` (`BoothEndpoints.cs`) |
| Policy | `Booths.View` + `RequireApprovedAccount` |
| Request | `GridQuery` (`Skip`, `Top`, `Search`, `Sort`, `SortDescending`, `Filters`) |
| Returns | `ApiResult<GridPage<AdminBoothSummary>>` |

Server paging: `Top` clamped 1–200 (default 25; the CP sends `Top=20`).
`Search` matches `Code` / `Name` / `NameArabic` (SQL `LIKE`). Per-column
`Filters` keys honoured: `code`, `name`, `namearabic`, `sector`. Sortable keys:
`code` (default), `name`, `sector`, `isactive`. **Exhibitor + Hall are not
server-filterable/sortable** (client-resolved).

```jsonc
// AdminBoothSummary  (src/Shared/SIMF.Contracts/Exhibition/BoothContracts.cs)
{
  "id":          "guid",
  "code":        "string",
  "name":        "string",
  "nameArabic":  "string",
  "exhibitorId": "guid?",   // resolved to a name client-side from the cached list
  "sector":      "string?",
  "hallId":      "guid?",   // resolved to a name client-side from the cached list
  "isActive":    true
}
```

## A2 — `GET /admin/booths/{id}`  (full detail)
| | |
|---|---|
| Full route | `GET /api/v1/admin/booths/{id:guid}` (BFF `GET /account/api/admin/booths/{id}`) |
| Source | `GetBoothEndpoint` |
| Policy | `Booths.View` + `RequireApprovedAccount` |
| Returns | `ApiResult<AdminBoothDetail>` |
| Errors | `404 BOOTH_NOT_FOUND` — id unknown |

The CP re-fetches the detail before **every** Edit / Details / Delete form,
because the summary omits the officer fields, the bilingual sector/description,
the map position and the optional Contact link.

```jsonc
// AdminBoothDetail
{
  "id":                "guid",
  "code":              "string",
  "name":              "string",
  "nameArabic":        "string",
  "exhibitorId":       "guid?",
  "officerName":       "string?",
  "officerPhone":      "string?",
  "officerEmail":      "string?",
  "contactId":         "guid?",   // SIMF-FDS-014 / D-281 shared Contact link
  "sector":            "string?",
  "sectorArabic":      "string?",
  "description":       "string?",
  "descriptionArabic": "string?",
  "hallId":            "guid?",
  "mapX":              0.0,        // double?
  "mapY":              0.0,        // double?
  "isActive":          true
}
```

## A3 — `POST /admin/booths`  (create)
| | |
|---|---|
| Full route | `POST /api/v1/admin/booths` (BFF `POST /account/api/admin/booths`) |
| Source | `CreateBoothEndpoint` |
| Policy | `Booths.Create` + `RequireApprovedAccount`; rate-limit `auth` |
| Request | `AdminCreateBoothRequest` |
| Returns | `ApiResult<AdminBoothDetail>` |
| Errors | `400 BOOTH_INVALID`, `409 BOOTH_CODE_DUPLICATE`, `401` if `sub` claim missing |

```jsonc
// AdminCreateBoothRequest
{
  "code":              "string",  // required, 2–16, trimmed + upper-cased server-side
  "name":              "string",  // required, 1–128
  "nameArabic":        "string",  // required, 1–128
  "exhibitorId":       "guid?",   // must be an active Exhibitor (else 400)
  "officerName":       "string?", // ≤256
  "officerPhone":      "string?", // ≤32
  "officerEmail":      "string?", // ≤320; must contain '@' (else 400)
  "contactId":         "guid?",   // must be an existing active Contact (else 400)
  "sector":            "string?", // ≤128
  "sectorArabic":      "string?", // ≤128
  "description":       "string?", // ≤2048
  "descriptionArabic": "string?", // ≤2048
  "hallId":            "guid?",   // must be an active Hall (else 400)
  "mapX":              0.0,        // double?
  "mapY":              0.0         // double?
}
```
Create always sets `IsActive = true` (no `isActive` field in the create payload).

## A4 — `PUT /admin/booths/{id}`  (update)
| | |
|---|---|
| Full route | `PUT /api/v1/admin/booths/{id:guid}` (BFF `PUT /account/api/admin/booths/{id}`) |
| Source | `UpdateBoothEndpoint` (binds `UpdateBoothRequest` then maps to `AdminUpdateBoothRequest`) |
| Policy | `Booths.Edit` + `RequireApprovedAccount`; rate-limit `auth` |
| Request | `AdminUpdateBoothRequest` (same fields as create **plus** `"isActive": true`) |
| Returns | `ApiResult<AdminBoothDetail>` |
| Errors | `400 BOOTH_INVALID`, `409 BOOTH_CODE_DUPLICATE` (only re-checked when the Code changed), `404 BOOTH_NOT_FOUND`, `401` if `sub` missing |

## A5 — `DELETE /admin/booths/{id}`  (soft-delete)
| | |
|---|---|
| Full route | `DELETE /api/v1/admin/booths/{id:guid}` (BFF `DELETE /account/api/admin/booths/{id}`) |
| Source | `DeactivateBoothEndpoint` |
| Policy | `Booths.Delete` + `RequireApprovedAccount`; rate-limit `auth` |
| Returns | `ApiResult<bool>` (`true`) |
| Errors | `404 BOOTH_NOT_FOUND`, `401` if `sub` missing |

Calls `booth.Deactivate()` (`IsActive=false`) — **idempotent** (an
already-inactive booth returns success without a second audit row).

## A6 — `POST /admin/booths/export`  (Excel)
| | |
|---|---|
| Full route | `POST /api/v1/admin/booths/export` (BFF via `MapGridExcel(group, "booths")`) |
| Source | `ExportBoothsEndpoint` (`BoothsExcelEndpoints.cs`) |
| Policy | `Booths.Export`; rate-limit `auth` |
| Request | `AdminGridExportRequest { Ids, Query }` |
| Returns | `.xlsx` binary (Content-Type `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`) |

Selected `Ids` win; otherwise the whole filtered `Query`. Sheet **"Booths"**,
header `Code | Name | NameArabic | Exhibitor | Sector | Hall | IsActive`; the
Exhibitor is written as its **English name** and the Hall as its **Code** (the
natural keys import resolves back). Capped at 5000 rows. File
`simf-booths-{yyyyMMddHHmmss}.xlsx`.

## A7 — `POST /admin/booths/import`  (Excel, insert-only)
| | |
|---|---|
| Full route | `POST /api/v1/admin/booths/import` (BFF via `MapGridExcel(group, "booths")`) |
| Source | `ImportBoothsEndpoint` (`BoothsExcelEndpoints.cs`) |
| Policy | `Booths.Import`; rate-limit `auth` |
| Request | multipart form-data, field `file` (`.xlsx`) |
| Returns | `ApiResult<AdminGridImportResult>` (`Created` / `Updated` / `Skipped` / `Errors[]`) |

Required headers `Code`, `Name`, `NameArabic`. Optional `Exhibitor` resolves to
an active exhibitor by **English name** (case-insensitive); optional `Hall`
resolves to an active hall by **Code**; a non-blank value that resolves to
nothing is a per-row error. **Insert-only** (Created is the only success kind);
a duplicate Code is a per-row error, not a batch abort. Officer fields, the
`ContactId` link and Map X/Y are **never** imported (set them via Edit). Upload
defence: non-`.xlsx` (ZIP-magic) / >5 MB / wrong sheet / missing header → `400`.

## Errors

| HTTP | Code | When |
|------|------|------|
| 400 | `BOOTH_INVALID` | Code not 2–16, Name/NameArabic not 1–128, an optional field over its max, officer email without `@`, or `HallId`/`ExhibitorId`/`ContactId` not an existing active row |
| 409 | `BOOTH_CODE_DUPLICATE` | Code (upper-cased) already exists — `"A booth with code '{code}' already exists."` / `"يوجد جناح بالرمز '{code}' بالفعل."` |
| 404 | `BOOTH_NOT_FOUND` | GET / PUT / DELETE against an unknown id — `"The booth was not found."` / `"لم يتم العثور على الجناح."` |

Standard transport / envelope errors (network, 500) per SIMF-API-001 apply.
Every error carries `Code` + bilingual `Message` / `MessageArabic`; the CP forms
surface `MessageForCurrentCulture()`.

## Audit events
`Booth.Created`, `Booth.Updated`, `Booth.Deactivated` — each written via
`IAuditLog` carrying the actor user id (the JWT `sub` claim).

## The app (public) reads — same data, different DTOs

The CP curates the rows the app's **Venue map** (App Page 015) reads. These are
**public, read-only** (`AllowAnonymous()`, `Tags("Public")`, no permission code),
under the `/api/v1/app/*` prefix. Source:
`Endpoints/Public/PublicBoothEndpoints.cs` (+ `PublicVenueMapEndpoints.cs`).
Full contract: [App Page 015 API](../../App/Page_015/Page_015_API.md).

| Route | Source | Returns | App use |
|-------|--------|---------|---------|
| `GET /app/booths` | `ListPublicBoothsEndpoint` | `ApiResult<IReadOnlyList<PublicBoothSummary>>` | The booth lookup behind the map's bottom info card |
| `GET /app/booths/{id}` | `GetPublicBoothEndpoint` | `ApiResult<PublicBoothDetail>` (404 `BOOTH_NOT_FOUND`) | The lazy detail sheet — adds the `description` paragraph |
| `GET /app/venue-map` | `PublicVenueMapEndpoint` | `ApiResult<IReadOnlyList<PublicVenueMapNode>>` | The positioned 2D nodes (booth nodes carry `boothId` → `PublicBoothSummary.Id`) |

> **What the public DTOs expose vs. omit.** `PublicBoothSummary` /
> `PublicBoothDetail` carry `Code`, `Name`/`NameArabic`,
> **`ExhibitorName`/`ExhibitorNameArabic`** (the resolved company name — note
> the public DTO ships the **name**, whereas the admin summary ships the bare
> `ExhibitorId`), `Sector`/`SectorArabic`, `HallId` (a **bare Guid** — no hall
> name, D11), `MapX`/`MapY`, and — detail only — `Description`/`DescriptionArabic`.
> The booth **officer** fields, the `ContactId` link and the `IsActive` flag are
> **CP-internal** and are NOT on the public DTOs. There is **no `logoUrl`** field
> (a booth logo is decoration). A soft-deleted booth (`IsActive=false`) drops
> from all three public reads.
