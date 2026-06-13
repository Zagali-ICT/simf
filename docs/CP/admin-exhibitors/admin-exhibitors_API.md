# CP page — API (العارضون · Exhibitors)

Authoritative backend contract for `/admin/exhibitors`. Inherits the
`ApiResult<T>` envelope, headers, error model and auth from SIMF-API-001.
**The confirmed backing endpoint file is**
`src/Backend/SIMF.Api/Endpoints/Exhibitors/ExhibitorEndpoints.cs` (CRUD +
account provisioning); the D-356 Excel pair lives in
`src/Backend/SIMF.Api/Endpoints/Admin/ExhibitorsExcelEndpoints.cs`. The service
is `AdminExhibitorService`; DTOs are in `SIMF.Contracts.Exhibitors`.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> **Path-prefix note.** The CP calls a **BFF passthrough** under
> `/account/api/admin/exhibitors/*` (`AccountEndpoints.cs`), which forwards to
> the API under **`/api/v1/admin/exhibitors/*`**. The endpoint `Configure()`
> registers the routes as `/admin/exhibitors/*` (the `/api/v1` prefix is applied
> globally). Both forms are listed below.

## Auth
- Every endpoint is policy-gated via
  `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Exhibitors.X),
  nameof(AuthorizationPolicies.RequireApprovedAccount))`.
- Mutations additionally carry `Options(rb => rb.RequireRateLimiting("auth"))`.
- `Tags("Admin")` on all.

## Endpoint table

| # | Route (Configure) | Verb | Policy | Rate-limited | Endpoint class | Request → Response |
|---|-------------------|------|--------|:---:|----------------|--------------------|
| E1 | `/admin/exhibitors/list` | POST | `Exhibitors.View` | — | `ListExhibitorsEndpoint` | `GridQuery` → `ApiResult<GridPage<AdminExhibitorSummary>>` |
| E2 | `/admin/exhibitors/{id:guid}` | GET | `Exhibitors.View` | — | `GetExhibitorEndpoint` | route id → `ApiResult<AdminExhibitorDetail>` |
| E3 | `/admin/exhibitors` | POST | `Exhibitors.Create` | ✅ auth | `CreateExhibitorEndpoint` | `CreateExhibitorRequest` → `ApiResult<AdminExhibitorDetail>` |
| E4 | `/admin/exhibitors/{id:guid}` | PUT | `Exhibitors.Edit` | ✅ auth | `UpdateExhibitorEndpoint` | `UpdateExhibitorRequest` (+ route id) → `ApiResult<AdminExhibitorDetail>` |
| E5 | `/admin/exhibitors/{id:guid}` | DELETE | `Exhibitors.Delete` | ✅ auth | `DeleteExhibitorEndpoint` | route id → `ApiResult<bool>` (soft-deactivate) |
| E6 | `/admin/exhibitors/{id:guid}/accounts` | GET | `Exhibitors.View` | — | `ListExhibitorAccountsEndpoint` | route id → `ApiResult<IReadOnlyList<ExhibitorAccountSummary>>` |
| E7 | `/admin/exhibitors/{id:guid}/accounts` | POST | `Exhibitors.Create` | ✅ auth | `ProvisionExhibitorAccountEndpoint` | `ProvisionExhibitorAccountRequest` (+ route id) → `ApiResult<ExhibitorAccountSummary>` |
| E8 | `/admin/exhibitors/export` | POST | `Exhibitors.Export` | ✅ auth | `ExportExhibitorsEndpoint : AdminGridExportEndpoint<AdminExhibitorSummary>` | `AdminGridExportRequest` → XLSX |
| E9 | `/admin/exhibitors/import` | POST | `Exhibitors.Import` | ✅ auth | `ImportExhibitorsEndpoint : AdminGridImportEndpoint` | multipart "file" → result |

> **Identity actor.** The mutating endpoints (E3/E4/E5/E7) read the actor from
> the `sub` claim (`User.FindFirstValue("sub")`) and 401 if it is not a Guid.

## DTOs (`SIMF.Contracts.Exhibitors`)

### `AdminExhibitorSummary` (grid row)
```csharp
record AdminExhibitorSummary(
    Guid Id, string NameEn, string NameAr,
    string? ContactEmail, string? ContactPhone, string? Website,
    int AccountCount, bool IsActive, DateTimeOffset CreatedAt);
```
- `AccountCount` = count of **active** `ExhibitorMembership` rows (a computed
  sub-query — not sortable / not server-filterable).

### `AdminExhibitorDetail` (full detail)
```csharp
record AdminExhibitorDetail(
    Guid Id, string NameEn, string NameAr,
    string? ContactEmail, string? ContactPhone, string? Website,
    bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
    Guid? ContactId);
```
- The grid summary **omits** `ContactId` and `UpdatedAt`; Edit/Details/Delete
  GET the full detail first.

### `CreateExhibitorRequest`
`NameEn` (1–256), `NameAr` (1–256), `ContactEmail?` (≤320), `ContactPhone?`
(≤32), `Website?` (≤512), `ContactId?` (optional active-Contact link). Create
sets `IsActive = true` server-side — there is **no** `IsActive` on the create
request.

### `UpdateExhibitorRequest`
Same as create **plus** `IsActive` (bool, soft-delete/restore). Not sealed: the
endpoint binds `{id}` + body via the derived `UpdateExhibitorRoute`.

### `ExhibitorAccountSummary`
```csharp
record ExhibitorAccountSummary(
    Guid Id, Guid UserId, string ContactName, string Email,
    string? RoleLabel, bool IsActive, DateTimeOffset CreatedAt);
```
- `UserId` is the **SimfUser id on the Identity database** (logical FK, D-157);
  `Email` is resolved cross-context on read (no cross-DB JOIN).

### `ProvisionExhibitorAccountRequest`
`ContactName` (1–256, used as the account display name), `Email` (1–320, must not
already be registered), `RoleLabel?` (≤128). Not sealed; bound via
`ProvisionExhibitorAccountRoute`.

## Validation + error model
`AdminExhibitorService.Validate` + the provisioning guards throw
`ApiException(code, http, en, ar)` (bilingual). The error codes are the
`ErrorCodes.Exhibitor*` constants:

| HTTP | Code | When |
|------|------|------|
| 400 | `EXHIBITOR_INVALID` | NameEn/NameAr not 1–256, ContactEmail >320, ContactPhone >32, Website >512, **or** a `ContactId` that does not reference an existing **active** Contact |
| 404 | `EXHIBITOR_NOT_FOUND` | unknown exhibitor id (GET / PUT / DELETE / list-accounts / provision) |
| 409 | `EXHIBITOR_INACTIVE` | provisioning an account under an **inactive** exhibitor |
| 400 | `EXHIBITOR_ACCOUNT_INVALID` | ContactName not 1–256, Email not 1–320, or RoleLabel >128 |

A duplicate / already-registered account email surfaces from the reused
`CreateVisitorAsync` provisioning pipeline as its own `ApiException`.

> Excel error envelopes (E8/E9): non-`.xlsx` (fails ZIP-magic `50 4B 03 04`) →
> 400; oversize (>5 MB) → 413 `AdminImportEmpty`; wrong sheet / missing required
> header → 400 (bilingual). A blank-name import row is a **per-row** error, not a
> batch abort. Details in `cp-admin-exhibitors.md` §6 + the E2E catalogue
> E2E-EXH-021…023.

## List query behaviour (E1)
- `Skip` floored at 0; `Top` clamped 1–200 (default 25 when ≤0).
- **Search** matches `Name` or `NameArabic` (EF `Like %term%`).
- **Filters** (lower-cased keys): `nameen`, `namear`, `isactive` (bool). Unknown
  columns ignored.
- **Sort** keys: `nameen`, `namear`, `isactive`; default `NameArabic` ascending.

## Audit (write-side)
`AdminExhibitorService` writes an `AuditEntry` on each mutation:
`AuditEvents.ExhibitorCreated`, `ExhibitorUpdated`, `ExhibitorDeactivated`,
`ExhibitorAccountProvisioned` — each carrying the `ActorUserId`; provisioning
also carries `SubjectUserId` + `SubjectEmail`. Deactivate is **idempotent**
(returns early with no second audit row when already inactive).

## Tests
- `tests/SIMF.Api.Tests/ExhibitorsTests.cs` — CRUD + account provisioning.
- `tests/SIMF.Api.Tests/ExhibitorsExcelTests.cs` — D-356 export/import engine,
  incl. `Non_admin_caller_is_forbidden_from_export` (the per-action API gate).
- The endpoint file header declares `// Tests: SIMF.Api.Tests/ExhibitorsTests.cs`.
</content>
