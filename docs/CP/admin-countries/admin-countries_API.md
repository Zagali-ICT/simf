# CP — API (البلدان · Countries `/admin/countries`)

Authoritative backend contract for this page. All responses use the `ApiResult<T>`
envelope (SIMF-API-001). API routes are prefixed **`/api/v1`**
(`config.Endpoints.RoutePrefix = "api/v1"`, `Program.cs`). The CP never calls the
API directly — it calls same-origin BFF passthroughs under
`/account/api/admin/countries/*`, which attach the access token and forward to the
API. Verified against `CountryEndpoints.cs`, `CountriesExcelEndpoints.cs`,
`Countries.cs`, `PermissionCatalog.cs`.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

## Endpoint map (CP call → BFF → API → permission)
| Action | CP call (`simfAccount.*`) | BFF route | API route (`/api/v1`-prefixed) | API class | Permission policy |
|--------|---------------------------|-----------|--------------------------------|-----------|-------------------|
| List | `postJson` `/account/api/admin/countries/list` | `POST /admin/countries/list` | `POST /api/v1/admin/countries/list` | `ListCountriesEndpoint` | `Countries.View` |
| Get one | `getJson` `/account/api/admin/countries/{id}` | `GET /admin/countries/{id:int}` | `GET /api/v1/admin/countries/{id:int}` | `GetCountryEndpoint` | `Countries.View` |
| Create | `postJson` `/account/api/admin/countries` | `POST /admin/countries` | `POST /api/v1/admin/countries` | `CreateCountryEndpoint` | `Countries.Create` |
| Update | `putJson` `/account/api/admin/countries/{id}` | `PUT /admin/countries/{id:int}` | `PUT /api/v1/admin/countries/{id:int}` | `UpdateCountryEndpoint` | `Countries.Edit` |
| Deactivate | `deleteJson` `/account/api/admin/countries/{id}` | `DELETE /admin/countries/{id:int}` | `DELETE /api/v1/admin/countries/{id:int}` | `DeactivateCountryEndpoint` | `Countries.Delete` |
| Export | via `CrudGridExcel` | `POST /admin/countries/export` | `POST /api/v1/admin/countries/export` | `ExportCountriesEndpoint` | `Countries.Export` |
| Import | via `CrudGridExcel` | `POST /admin/countries/import` | `POST /api/v1/admin/countries/import` | `ImportCountriesEndpoint` | `Countries.Import` |

**Auth shape (all CRUD endpoints):** `Policies(PermissionCatalog.PolicyFor(<code>),
nameof(AuthorizationPolicies.RequireApprovedAccount))` + `Tags("Admin")`. Create /
Update / Deactivate additionally carry `Options(rb => rb.RequireRateLimiting("auth"))`.
The actor id is read from the `sub` claim (`Guid.TryParse(User.FindFirstValue("sub"))`);
a missing / unparsable `sub` → `401`. The body never carries a user id.

## Permissions (`PermissionCatalog.Countries`)
Codes (verbatim, `PermissionCatalog.cs`):

```
Countries.View    Countries.Create    Countries.Edit
Countries.Delete  Countries.Export    Countries.Import
```

All six are registered in `PermissionCatalog.All` with `BaselineRoles = AdminOnly`.
Page gate: `Countries.View`. Nav item `Module.AdminCountries` sets
`RequiredPermission = Countries.View`.

## DTOs (`SIMF.Contracts.Admin.Countries`)
```csharp
// Grid row
public sealed record AdminCountrySummary(
    int Id, string Code, string Name, string NameArabic,
    string? PhonePrefix, int DisplayOrder, bool IsActive,
    DateTimeOffset CreatedAt);

// Details + Edit pre-fill
public sealed record AdminCountryDetail(
    int Id, string Code, string Name, string NameArabic,
    string? PhonePrefix, int DisplayOrder, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

// Create body (Add)
public sealed class AdminCreateCountryRequest {
    public int Id { get; set; }            // ISO 3166-1 numeric — manually assigned (e.g. 682 = SA)
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameArabic { get; set; }
    public string? PhonePrefix { get; set; }
    public int DisplayOrder { get; set; }
}

// Update body (Edit) — note: NO Id (route id is authoritative)
public sealed class AdminUpdateCountryRequest {
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameArabic { get; set; }
    public string? PhonePrefix { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
```

> The API `UpdateCountryEndpoint` binds an `UpdateCountryRequest`, which since
> D-844 **inherits** `AdminUpdateCountryRequest` and adds only the route `Id`.
> The bound request is passed straight to the service — there is no longer a
> hand-written field-by-field mapping that could silently drop a field on PUT
> (the D-842 / D-843 defect class). The CP's PUT body matches
> `AdminUpdateCountryRequest`.

## Request / response shapes
- **List** — request `GridQuery` (Search / Sort / SortDescending / Skip / Top);
  response `ApiResult<GridPage<AdminCountrySummary>>`.
- **Get** — response `ApiResult<AdminCountryDetail>`; missing id →
  `404 COUNTRY_NOT_FOUND`.
- **Create** — request `AdminCreateCountryRequest`; response
  `ApiResult<AdminCountryDetail>`.
- **Update** — request body `AdminUpdateCountryRequest` (route `{id}`); response
  `ApiResult<AdminCountryDetail>`.
- **Deactivate** — response `ApiResult<bool>` (`Ok(true)`); soft-delete.

## Excel (D-356)
- **Export** (`ExportCountriesEndpoint : AdminGridExportEndpoint<AdminCountrySummary>`):
  sheet `Countries`, file prefix `simf-countries`, columns
  `Id | Code | Name | NameArabic | PhonePrefix | DisplayOrder | IsActive`. Lists rows
  via `service.ListAllAsync(query)`; whole-grid export capped at `MaxExportRows`.
  Country ids are `int`, not the `Guid` the generic export contract carries, so
  `IdOf` returns `Guid.Empty` and the CP always sends an **empty Ids list + the
  current query** (the filtered grid), never a per-row selection.
- **Import** (`ImportCountriesEndpoint : AdminGridImportEndpoint`, insert-only):
  sheet `Countries`, required headers `Id | Code | Name | NameArabic`; per-row key
  for the error list = the `Code` cell. Each row binds to `AdminCreateCountryRequest`
  and calls `service.CreateAsync`. A row whose id is missing / non-positive, or whose
  `Code` / `Name` / `NameArabic` is blank, throws a `DataValidationException` recorded
  as a **per-row error** rather than aborting the batch. The shared base enforces the
  upload size cap, the ZIP-magic `.xlsx` check, the required-sheet / required-header
  check, and the row cap, and aggregates `AdminGridImportResult(created, updated,
  skipped, errors)`. Because the importer only ever calls `CreateAsync`, in practice
  it reports created or per-row errors (a duplicate id/code is a per-row error, never
  an update).

## Error codes (envelope `ApiResult<T>.Error`)
Defined in `ErrorCodes.cs`; messages are bilingual; the CP surfaces
`Error.MessageForCurrentCulture()`.

| Code | HTTP | When |
|------|------|------|
| `COUNTRY_INVALID` | 400 | id `≤ 0`; code not exactly 2 chars; name/nameArabic out of 1–128; phone prefix > 8; display order < 0 |
| `COUNTRY_NOT_FOUND` | 404 | get / update / deactivate of a missing id |
| `COUNTRY_ID_DUPLICATE` | 409 | create with an id another row already holds |
| `COUNTRY_CODE_DUPLICATE` | 409 | create, or update changing the code to one held by another row |
| `COUNTRY_IN_USE` | — | **reserved, not yet wired** (no in-use guard on deactivate) |
| `Auth.Unauthorized` | 401 | missing / unparsable `sub` claim |
| `RateLimit.Exceeded` | 429 | `auth` bucket exceeded on Create / Update / Deactivate |

`GetCountryEndpoint` throws the not-found `ApiException` inline with the bilingual
message `"The country was not found." / "لم يتم العثور على البلد."`.

## App consumption — the linked read (Page 007)
The mobile app reads the same `Country` table through its own, **separate**, app
endpoint (verified in `ProfileCountriesEndpoint.cs`):

| | |
|---|---|
| Full route | `GET /api/v1/app/account/user-profile/countries` |
| Class | `ProfileCountriesEndpoint : EndpointWithoutRequest<ApiResult<CountryListResponse>>` |
| Auth | Signed-in (own `sub`); `Tags("Account")`; no role / no permission policy |
| Returns | `ApiResult<CountryListResponse>` |
| Source query | `appDb.Countries.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)` |
| Projection | `CountryDto(country.Code, country.Name, country.NameArabic)` |

```jsonc
// CountryListResponse
{ "countries": [ { "code": "SA", "name": "Saudi Arabia", "nameArabic": "السعودية" } ] }
```

Contract (`SIMF.Contracts.UserProfile`):
```csharp
public sealed record CountryDto(string Code, string Name, string NameArabic);
public sealed record CountryListResponse(IReadOnlyList<CountryDto> Countries);
```

**Linkage notes (verbatim from code):**
- The app picker shows **only `IsActive` rows** — deactivating a country here
  removes it from the app picker.
- Ordering is `DisplayOrder` then English `Name` — the CP **Display order** field
  controls the picker order.
- The app sees the **alpha-2 `Code`** + bilingual name; it does **not** receive the
  ISO numeric `Id` or the `PhonePrefix` on this endpoint.
- Page 007 defaults the nationality to **Saudi Arabia (SA)** and uses the picked
  `Code` to drive the document path (SA → national-ID; else Iqama / Passport) — see
  `docs/App/Page_007/Page_007_API.md` (E3) and `Page_007_Logic.md`.

> **Drift flagged (not fixed):** the XML doc-comment on `CountryListResponse` in
> `src/Shared/SIMF.Contracts/UserProfile/UserProfile.cs` says the body of
> `GET /api/v1/account/profile/countries` — a **stale route**. The actual,
> registered route (in `ProfileCountriesEndpoint.Configure`) is
> `GET /api/v1/app/account/user-profile/countries`, which is also what
> `Page_007_API.md` documents. Reported per the read-only rule; no code changed.
