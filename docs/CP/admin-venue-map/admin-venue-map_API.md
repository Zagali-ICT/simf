# Venue map nodes — API (`/admin/venue-map`)

Authoritative backend contract for this CP config page. Inherits the
`ApiResult<T>` envelope, standard headers and error model from SIMF-API-001. All
admin endpoints live in
[`VenueMapEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/VenueMapEndpoints.cs)
(CRUD) + [`VenueMapExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/VenueMapExcelEndpoints.cs)
(export/import); the public read in
[`PublicVenueMapEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Public/PublicVenueMapEndpoints.cs).
The Control Panel reaches the admin API through the BFF passthroughs under
`/account/api/admin/venue-map/*` (`AccountEndpoints.cs`). Verified against code
this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> **Path prefix.** Admin routes are registered under **`/api/v1/admin/*`**, so the
> routes below resolve to `…/api/v1/admin/venue-map/…`. The CP never calls the API
> directly — it calls the BFF mirror `/account/api/admin/venue-map/…` (shown in the
> CP-call column), which forwards the bearer token. The public read is under
> `/api/v1/app/*`.

## Permission policies (all `AdminOnly` baseline)

Every admin endpoint is gated by
`Policies(PermissionCatalog.PolicyFor(<perm>), nameof(AuthorizationPolicies.RequireApprovedAccount))`
and tagged `"Admin"`. The mutating endpoints (Create/Update/Delete) and the
export/import additionally carry `Options(rb => rb.RequireRateLimiting("auth"))`.

| Endpoint | Permission const | Value |
|----------|------------------|-------|
| List / Get | `PermissionCatalog.VenueMap.View` | `"VenueMap.View"` |
| Create | `PermissionCatalog.VenueMap.Create` | `"VenueMap.Create"` |
| Update | `PermissionCatalog.VenueMap.Edit` | `"VenueMap.Edit"` |
| Delete | `PermissionCatalog.VenueMap.Delete` | `"VenueMap.Delete"` |
| Export | `PermissionCatalog.VenueMap.Export` | `"VenueMap.Export"` |
| Import | `PermissionCatalog.VenueMap.Import` | `"VenueMap.Import"` |

`Administrator = "*"` satisfies all of the above.

---

## A1 — List nodes

| | |
|---|---|
| API route | `POST /api/v1/admin/venue-map/list` |
| CP call (BFF) | `POST /account/api/admin/venue-map/list` |
| Source | `ListVenueMapNodesEndpoint` |
| Permission | `VenueMap.View` + `RequireApprovedAccount` |
| Request | `GridQuery` (`Skip`, `Top`, `Search`, `Sort`, `SortDescending`, `Filters`) |
| Returns | `ApiResult<GridPage<AdminVenueMapNodeSummary>>` |

`AdminVenueMapNodeSummary` (`SIMF.Contracts/Admin/VenueMap.cs`):

```jsonc
{
  "id":          "guid",
  "label":       "string",   // EN label
  "labelArabic": "string",   // AR label
  "kind":        0,          // VenueMapNodeKind: 0 Hall · 1 Zone · 2 Booth · 3 PointOfInterest
  "x":           0.0,        // double
  "y":           0.0,        // double
  "hallId":      "guid?",    // bare Guid — no hall name (D-157)
  "boothId":     "guid?",    // bare Guid — no booth name (D-157)
  "isActive":    true
}
```

Server behaviour (`VenueMapService.ListAsync`): `Top` clamped to `[1, 500]`
(default 50 when ≤ 0); `Filters["label"]` matches `Label` **or** `LabelArabic`
(`Contains`); `Search` matches the same two via `LIKE`; `Sort` accepts
`"label"` / `"kind"` (default Label ascending). Returns **all** rows including
inactive ones (the grid renders the Active pill).

## A2 — Get one node

| | |
|---|---|
| API route | `GET /api/v1/admin/venue-map/{id:guid}` |
| CP call (BFF) | `GET /account/api/admin/venue-map/{id}` |
| Source | `GetVenueMapNodeEndpoint` |
| Permission | `VenueMap.View` + `RequireApprovedAccount` |
| Returns | `ApiResult<AdminVenueMapNodeDetail>` |

`AdminVenueMapNodeDetail` — the summary fields **plus** `createdAt` /
`updatedAt`:

```jsonc
{
  "id":          "guid",
  "label":       "string",
  "labelArabic": "string",
  "kind":        0,
  "x":           0.0,
  "y":           0.0,
  "hallId":      "guid?",
  "boothId":     "guid?",
  "isActive":    true,
  "createdAt":   "2026-06-13T00:00:00Z",   // DateTimeOffset
  "updatedAt":   "2026-06-13T00:00:00Z"    // DateTimeOffset?
}
```

### Errors (A2)
| HTTP | Code | When |
|------|------|------|
| 404 | `VENUE_MAP_NODE_NOT_FOUND` | id unknown — `ApiException(ErrorCodes.VenueMapNodeNotFound, 404, "The venue-map node was not found.", "لم يتم العثور على عقدة الخريطة.")` |

## A3 — Create node

| | |
|---|---|
| API route | `POST /api/v1/admin/venue-map` |
| CP call (BFF) | `POST /account/api/admin/venue-map` |
| Source | `CreateVenueMapNodeEndpoint` (rate-limited `"auth"`) |
| Permission | `VenueMap.Create` + `RequireApprovedAccount` |
| Request | `AdminCreateVenueMapNodeRequest` |
| Returns | `ApiResult<AdminVenueMapNodeDetail>` |

```jsonc
// AdminCreateVenueMapNodeRequest
{
  "label":       "string",   // required, 1–128 (trimmed)
  "labelArabic": "string",   // required, 1–128 (trimmed)
  "kind":        0,          // VenueMapNodeKind
  "x":           0.0,
  "y":           0.0,
  "hallId":      "guid?",    // optional — must be an active Hall when present
  "boothId":     "guid?"     // optional — must be an active Booth when present
}
```

The actor id is read from the `sub` claim (`401` if it cannot be parsed). On
success the node is created `IsActive = true` and an `OperationLog`
`VenueMapNode.Created` audit row is written.

## A4 — Update node (move / relabel / re-link / toggle active)

| | |
|---|---|
| API route | `PUT /api/v1/admin/venue-map/{id:guid}` |
| CP call (BFF) | `PUT /account/api/admin/venue-map/{id}` |
| Source | `UpdateVenueMapNodeEndpoint` (rate-limited `"auth"`) |
| Permission | `VenueMap.Edit` + `RequireApprovedAccount` |
| Request | `AdminUpdateVenueMapNodeRequest` |
| Returns | `ApiResult<AdminVenueMapNodeDetail>` |

```jsonc
// AdminUpdateVenueMapNodeRequest — the create fields + IsActive
{
  "label":       "string",   // required, 1–128
  "labelArabic": "string",   // required, 1–128
  "kind":        0,
  "x":           0.0,
  "y":           0.0,
  "hallId":      "guid?",
  "boothId":     "guid?",
  "isActive":    true        // default true
}
```

Writes an `OperationLog` `VenueMapNode.Updated` audit row.

## A5 — Delete (soft) node

| | |
|---|---|
| API route | `DELETE /api/v1/admin/venue-map/{id:guid}` |
| CP call (BFF) | `DELETE /account/api/admin/venue-map/{id}` |
| Source | `DeleteVenueMapNodeEndpoint` (rate-limited `"auth"`) |
| Permission | `VenueMap.Delete` + `RequireApprovedAccount` |
| Returns | `ApiResult<bool>` (`data: true`) |

Soft delete only: `DeactivateAsync` sets `IsActive = false` and writes an
`OperationLog` `VenueMapNode.Deactivated` audit row. **Idempotent** — a second
delete on an already-inactive node short-circuits and still returns `200` (no
audit row). A deactivated node drops out of the public read (A6).

### Errors (A3 / A4 / A5)
| HTTP | Code | When |
|------|------|------|
| 400 | `VENUE_MAP_NODE_INVALID` | blank/over-length label — `"Both labels are required and must be 1–128 characters." / "كلا الاسمين مطلوبان ويجب أن يتراوح طولهما بين 1 و 128 حرفاً."` |
| 400 | `VENUE_MAP_NODE_INVALID` | `hallId` is not an active hall — `"The referenced hall was not found." / "لم يتم العثور على القاعة المرتبطة."` |
| 400 | `VENUE_MAP_NODE_INVALID` | `boothId` is not an active booth — `"The referenced booth was not found." / "لم يتم العثور على الجناح المرتبط."` |
| 404 | `VENUE_MAP_NODE_NOT_FOUND` | `PUT` / `DELETE` on a missing node |
| 401 | — | `sub` claim missing/unparseable on create/update/delete |

> The blank-label message is **`VENUE_MAP_NODE_INVALID`** with the text **"Both
> labels are required and must be 1–128 characters."** (`VenueMapService.ValidateLabels`).
> The CP form’s *client-side* guard shows a separate `Admin.VenueMap.Required`
> toast and never reaches the server for a fully-blank label.

## Excel — export (E1) / import (E2) (D-356)

| | Export | Import |
|---|--------|--------|
| API route | `POST /api/v1/admin/venue-map/export` | `POST /api/v1/admin/venue-map/import` |
| CP call (BFF) | `POST /account/api/admin/venue-map/export` | `POST /account/api/admin/venue-map/import` |
| Source | `ExportVenueMapEndpoint : AdminGridExportEndpoint<AdminVenueMapNodeSummary>` | `ImportVenueMapEndpoint : AdminGridImportEndpoint` |
| Permission | `VenueMap.Export` | `VenueMap.Import` |
| Request | `AdminGridExportRequest { Ids, Query }` | multipart, field `file` (`.xlsx`) |
| Returns | binary `.xlsx` (`simf-venue-map-{yyyyMMddHHmmss}.xlsx`) | `ApiResult<AdminGridImportResult>` |

- **Export sheet** `VenueMap`, header
  `Label | LabelArabic | Kind | X | Y | Hall | Booth | IsActive`. `Kind` →
  enum name; `Hall` / `Booth` → the linked record's **code** (resolved once per
  request from `IAdminHallService.ListAllAsync` / `IAdminBoothService.ListAllAsync`
  with `Top = 5000`; empty cell when the id no longer resolves). With no `Ids` the
  whole filtered grid is exported (capped at `MaxExportRows = 5000`).
- **Import** sheet `VenueMap`, required headers `Label | LabelArabic | Kind`
  (case-insensitive). Each row binds to `AdminCreateVenueMapNodeRequest` and is a
  **Created** (insert-only — no update/dedup). `Kind` parses from the enum name or
  raw int. `Hall` / `Booth` resolve by **code** against the active lists (blank =
  unset). Per-row failures (blank labels, unknown Kind, unknown Hall/Booth code)
  are collected and reported without aborting the batch.

### Import upload defence (`AdminGridImportEndpoint`)
| HTTP | When |
|------|------|
| 400 | not a valid `.xlsx` (ZIP-magic `50 4B 03 04` fails); worksheet not named `VenueMap`; a required header missing |
| 413 | file over the size cap (`AdminImportEmpty`) |
| per-row error | a single bad row is recorded and skipped; the batch continues (capped at `MaxImportRows = 5000`) |

The bilingual import-row errors thrown by `ImportVenueMapEndpoint` include
`"Both the English and Arabic labels are required." / "كلا التسميتين بالإنجليزية والعربية مطلوبتان."`,
`"The kind must be one of Hall, Zone, Booth or PointOfInterest." / "يجب أن يكون النوع أحد: قاعة أو منطقة أو جناح أو نقطة اهتمام."`,
`"No active hall has the code \"{code}\"." / "لا توجد قاعة نشطة بالرمز \"{code}\"."` and the booth equivalent.

---

## A6 — Public read (the app contract this page configures)

| | |
|---|---|
| API route | `GET /api/v1/app/venue-map` |
| Source | `PublicVenueMapEndpoint` |
| Access | **Public** — `AllowAnonymous()`, `Tags("Public")`. No token, no permission. |
| Returns | `ApiResult<IReadOnlyList<PublicVenueMapNode>>` |

```jsonc
// PublicVenueMapNode  (SIMF.Contracts/Programme/PublicVenueMap.cs)
{
  "id":          "guid",
  "label":       "string",
  "labelArabic": "string",
  "kind":        0,          // VenueMapNodeKind
  "x":           0.0,
  "y":           0.0,
  "hallId":      "guid?",    // bare Guid — NO hall name ships (D-157)
  "boothId":     "guid?"     // bare Guid — matches PublicBoothSummary.Id
}
```

`ListPublicAsync` returns **only `IsActive` nodes**, ordered by `Label`, and
**omits** `createdAt` / `updatedAt` (the public shape is the smaller append-only
wire contract — D-219). This is the exact list the Flutter app's venue-map screen
(App Page 015 §E1) draws on its 2D canvas; a `kind=Booth` node's `boothId` matches
`PublicBoothSummary.Id` from `GET /app/booths` (App Page 015 §E2/§E3) so the app
links the node to the booth detail sheet. The CP page is the **only** writer of
this data.

## Reused / related
- The Hall + Booth pickers (Add/Edit) and the export/import code resolution reuse
  the existing `POST /account/api/admin/halls/list` and
  `/account/api/admin/booths/list` admin lists — no contract change here.
- The public `GET /app/venue-map` is the app-facing counterpart; see
  [App Page 015 API](../../App/Page_015/Page_015_API.md) for how the app consumes
  it alongside the booth reads.
