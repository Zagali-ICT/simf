# CP Content blocks — API (`/admin/content-blocks`)

Authoritative backend contract for this page. All admin responses use the
`ApiResult<T>` envelope (SIMF-API-001). Routes carry the global prefix
`api/v1` (`Program.cs`: `config.Endpoints.RoutePrefix = "api/v1"`). The CP page
calls the BFF passthroughs at `/account/api/admin/content-blocks/*`, which
forward to the API with the admin's access token.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Admin endpoints — `CmsEndpoints.cs`

All admin endpoints carry `Tags("Admin")`, the per-action permission policy
(`PermissionCatalog.PolicyFor(...)`), and `nameof(AuthorizationPolicies.RequireApprovedAccount)`.
Mutations also carry `Options(rb => rb.RequireRateLimiting("auth"))`.

| Verb + route (under `api/v1`) | Permission policy | Request → Response |
|-------------------------------|-------------------|--------------------|
| `POST /admin/content-blocks/list` | `ContentBlocks.View` | `GridQuery` → `ApiResult<GridPage<AdminContentBlockSummary>>` |
| `GET /admin/content-blocks/{key}` | `ContentBlocks.View` | route `key` → `ApiResult<AdminContentBlockSummary>` (404 if absent) |
| `PUT /admin/content-blocks` | `ContentBlocks.Edit` | `UpsertContentBlockRequest` → `ApiResult<AdminContentBlockSummary>` · rate-limited |
| `DELETE /admin/content-blocks/{key}` | `ContentBlocks.Delete` | route `key` → `ApiResult<bool>` (soft deactivate) · rate-limited |

> **Excel export/import** (`POST /admin/content-blocks/export`,
> `POST /admin/content-blocks/import`) are registered by the shared grid-Excel
> helper (`Resource="content-blocks"`, gated `ContentBlocks.Export` /
> `ContentBlocks.Import`) — wired in the page but **not** declared in
> `CmsEndpoints.cs`; their endpoint definitions live in the shared
> `MapGridExcel`/`ContentBlocksExcelEndpoints` surface (not re-read this session;
> see `docs/pages/cp/admin-content-blocks.md` §5 for the as-built columns).

### Permission codes (`PermissionCatalog.ContentBlocks`)
```
ContentBlocks.View    = "ContentBlocks.View"     // BaselineRoles: AdminOnly
ContentBlocks.Edit    = "ContentBlocks.Edit"     // AdminOnly
ContentBlocks.Delete  = "ContentBlocks.Delete"   // AdminOnly
ContentBlocks.Export  = "ContentBlocks.Export"   // AdminOnly
ContentBlocks.Import  = "ContentBlocks.Import"   // AdminOnly
```
`Administrator = "*"` (wildcard) satisfies all of them.

## DTOs

### `AdminContentBlockSummary` (`SIMF.Contracts.Admin`)
The grid row **and** the form-bind shape — it carries every field the forms
need, so no detail fetch is required.
```jsonc
// record
{
  "id":                  "guid",
  "key":                 "string",          // stable slug (normalised lower-case)
  "content":             "string",          // English body
  "contentArabic":       "string",          // Arabic body
  "isActive":            true,
  "lastUpdatedAt":       "2026-09-01T00:00:00Z",  // DateTimeOffset
  "lastUpdatedByUserId": "guid"             // logical FK to SimfUser (Identity DB), resolved on read
}
```

### `UpsertContentBlockRequest` (`SIMF.Contracts.Admin`)
```jsonc
// class — Key identifies the row (create if absent, update in place if present)
{
  "key":           "home.welcome.title",  // required; normalised Trim()+ToLowerInvariant() server-side
  "content":       "string",              // defaults to ""
  "contentArabic": "string",              // defaults to ""
  "isActive":      true                   // defaults to true
}
```

### `GridQuery` / `GridPage<T>`
Standard CP grid query (`Skip`, `Top`, `Search`, `Filters`, `Sort`,
`SortDescending`) → paged result. The list service:
- `Skip` clamped to ≥ 0; `Top` clamped to `[1, 200]` (default 25 when ≤ 0 — the
  page sends `Top = 20`).
- `Search` → `LIKE %term%` across `Key`, `Content`, `ContentArabic`.
- `Filters` honoured: **`key`** (`Key.Contains`), **`content`** (`Content.Contains`),
  **`isactive`** (`bool.Parse` → `IsActive ==`). Unknown columns ignored.
- `Sort` honoured: **`key`**, **`content`**, **`lastupdatedat`** (asc/desc).
  Default order **`Key` ascending**. The `isActive` column is **not** sortable.

## Behaviour + validation (`AdminCmsService`)

### Upsert (`UpsertContentBlockAsync`)
1. Normalise `Key` = `Trim().ToLowerInvariant()`; `Content`/`ContentArabic`
   default to `""` when null.
2. **Key length** must be `2..128` → else **400 `CONTENT_BLOCK_INVALID`**
   ("Content block key must be between 2 and 128 characters." /
   "يجب أن يتراوح طول مفتاح المحتوى بين 2 و 128 حرفاً.").
3. **Content / ContentArabic** ≤ 8000 chars → else **400 `CONTENT_BLOCK_INVALID`**
   ("Content cannot exceed 8000 characters." / "لا يمكن أن يتجاوز المحتوى 8000 حرف.").
4. Look up the row by normalised key: **absent → create** (new `Id`,
   `CreatedAt = LastUpdatedAt = now`); **present → update in place** (same `Id`,
   overwrite Content/ContentArabic/IsActive, bump `LastUpdatedAt`).
5. Stamp `LastUpdatedByUserId` = actor (`sub` claim).
6. Write an audit entry `AuditEvents.ContentBlockUpserted`, `Detail = "key=<key>"`.
7. Return the `AdminContentBlockSummary`.

> **Upsert is keyed, not id-based.** The same `PUT` serves create and edit.
> Because the **Key field is disabled on Edit**, a key collision is reachable
> only from the New-block path — and it does **not** error: it silently upserts
> onto the existing row (no `CONTENT_BLOCK_KEY_DUPLICATE` raised).

### Delete (`DeactivateContentBlockAsync`)
- Normalise key; look up the row — **absent → 404 `CONTENT_BLOCK_NOT_FOUND`**
  ("Content block not found." / "لم يتم العثور على المحتوى.").
- Already inactive → **idempotent no-op** (returns; the endpoint still answers
  HTTP 200 `Data = true`).
- Active → set `IsActive = false`, bump `LastUpdatedAt`, stamp actor; write
  audit `AuditEvents.ContentBlockDeactivated`, `Detail = "key=<key>"`.
- Soft-delete only — the row is never hard-removed.

### Get-one (`GetContentBlockAsync`) / endpoint
- Normalise key; return the summary or **404 `CONTENT_BLOCK_NOT_FOUND`**.

### Auth failure on mutations
- `UpsertContentBlockEndpoint` / `DeleteContentBlockEndpoint` parse the `sub`
  claim into `actorId`; an unparseable `sub` → `Send.UnauthorizedAsync` (401).

## Public read side (the contract the app consumes) — `PublicCmsEndpoints.cs`

The same blocks this page writes are read **anonymously** by the Flutter app +
Website. `SIMF.Contracts.Cms`.

| Verb + route (under `api/v1`) | Auth | Request → Response |
|-------------------------------|------|--------------------|
| `GET /app/content/{key}` | `AllowAnonymous` | route `key` → `ApiResult<PublicContentBlock>` (404 if absent/inactive); `If-Modified-Since` → 304 |
| `POST /app/content/batch` | `AllowAnonymous` | `PublicContentBlockBatchRequest { Keys }` → `ApiResult<PublicContentBlockBatch>` |
| `GET /app/banners` | `AllowAnonymous` | → `ApiResult<PublicBanners>` (sibling Banners surface) |

```jsonc
// PublicContentBlock (SIMF.Contracts.Cms) — served by GET /api/v1/app/content/{key}
{
  "key":           "terms",
  "content":       "string",   // English body
  "contentArabic": "string",   // Arabic body
  "lastUpdatedAt": "2026-09-01T00:00:00Z"
}
```

### Conditional GET (D-173)
`GET /app/content/{key}` truncates `lastUpdatedAt` to the second, emits it as a
**`Last-Modified`** header, and returns **`304 Not Modified`** (no body) when the
request's `If-Modified-Since` is at or after that instant. The public read also
**hides inactive blocks** (an inactive/absent key → 404) — see Logic.

> **Route-prefix drift (report-only):** the `ContentBlock` entity XML doc and
> the existing `docs/pages/cp/admin-content-blocks.md` cite the public read as
> `GET /api/v1/content/{key}`. The **registered route is `/app/content/{key}`**
> (→ `GET /api/v1/app/content/{key}`), and the app docs (Page_009/Page_013) use
> the `/app/` form. The `/api/v1/content/{key}` citations are stale (missing the
> `/app` segment). Reported, not changed.

## App linkage (block keys)
| Block key | App page | App read route |
|-----------|----------|----------------|
| `terms` | Page 009 — الشروط والأحكام · Terms | `GET /api/v1/app/content/terms` |
| `about` | Home / static About (Page 013 group) | `GET /api/v1/app/content/about` |
| `cyber.*` | App cybersecurity-policy screen | seeded by `SIMF_App_ContentBlocks.sql` — Flutter **wire contract** |

`terms` + `about` are seeded by `docs/migrations/2026/SIMF_App_ContentBlocks.sql`
(D-377, moved out of C# by D-950) **per absent key**. Note that a fresh
environment no longer BOOTS with them: nothing seeds on start-up, so the T&C and
About screens are empty until `Run_All_App_Seeds.sql` has been run. An admin editing them here updates the live app copy with no redeploy.

## Tests
- API integration: `tests/SIMF.Api.Tests/CmsTests.cs` (per `// Tests:` headers on
  `CmsEndpoints.cs`, `PublicCmsEndpoints.cs`, `AdminCmsService.cs`).
- E2E catalogue: `docs/tests/e2e/cp-admin-content-blocks.md` (E2E-CNT-001…020).
