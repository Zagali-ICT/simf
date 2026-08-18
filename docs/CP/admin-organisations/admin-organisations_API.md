# Organisations — API (`/admin/organisations`)

Authoritative backend contract for the endpoints this CP page calls. The page
never calls the API directly: it calls the **BFF passthroughs** under
`/account/api/admin/organisations/*` (`AccountEndpoints.cs`) via the
`simfAccount.*` JS interop (`postJson` / `getJson` / `putJson` / `deleteJson` /
`uploadFile` / `downloadXlsx`), which forward (with the bearer token) to the
FastEndpoints in `OrganisationEndpoints.cs` + `OrganisationExcelEndpoints.cs`.
All responses use the `ApiResult<T>` envelope. Routes/permissions/DTOs verified
against code this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Design](admin-organisations_Design.md) ·
> [Function](admin-organisations_Function.md) · [Logic](admin-organisations_Logic.md).

## Endpoint map (admin)

| Action | BFF call (CP) | API endpoint | Method + full route | Permission policy | Extra |
|--------|---------------|--------------|---------------------|-------------------|-------|
| List (paged grid) | `POST /account/api/admin/organisations/list` | `ListOrganisationsEndpoint` | `POST /api/v1/admin/organisations/list` | `Organisations.View` + `RequireApprovedAccount` | — |
| Get one (detail) | `GET /account/api/admin/organisations/{id}` | `GetOrganisationEndpoint` | `GET /api/v1/admin/organisations/{id:guid}` | `Organisations.View` + `RequireApprovedAccount` | — |
| Create | `POST /account/api/admin/organisations` | `CreateOrganisationEndpoint` | `POST /api/v1/admin/organisations` | `Organisations.Create` + `RequireApprovedAccount` | `RequireRateLimiting("auth")` |
| Update | `PUT /account/api/admin/organisations/{id}` | `UpdateOrganisationEndpoint` | `PUT /api/v1/admin/organisations/{id:guid}` | `Organisations.Edit` + `RequireApprovedAccount` | `RequireRateLimiting("auth")` |
| Deactivate (soft-delete) | `DELETE /account/api/admin/organisations/{id}` | `DeactivateOrganisationEndpoint` | `DELETE /api/v1/admin/organisations/{id:guid}` | `Organisations.Delete` + `RequireApprovedAccount` | `RequireRateLimiting("auth")` |
| Import (gov-Excel upsert) | `POST /account/api/admin/organisations/import` | `ImportOrganisationsEndpoint` | `POST /api/v1/admin/organisations/import` | `Organisations.Import` + `RequireApprovedAccount` | `RequireRateLimiting("auth")`, `AllowFileUploads()` |
| Export (grid → XLSX) | `POST /account/api/admin/organisations/export` | `ExportOrganisationsEndpoint` | `POST /api/v1/admin/organisations/export` | `Organisations.Export` (via `AdminGridExportEndpoint<TRow>` base) | — |

> Permission policy is applied via `Policies(PermissionCatalog.PolicyFor(
> PermissionCatalog.Organisations.X), nameof(AuthorizationPolicies.
> RequireApprovedAccount))`. The route GUID on `PUT` is read via
> `Route<Guid>("id")` (the contract `UpdateOrganisationRequest` carries no id).
> The actor is resolved from the `sub` claim (`User.FindFirstValue("sub")`);
> create/update/delete/import return 401 if `sub` is absent.

## DTOs (real field names — `OrganisationContracts.cs`)

### `AdminOrganisationSummary` (grid row — `data.Items[]`)
```jsonc
{ "id": "guid", "nameAr": "string", "nameEn": "string?",
  "commercialRegistration": "string?", "sector": "string?",
  "city": "string?", "isActive": true }
```
Returned wrapped as `ApiResult<GridPage<AdminOrganisationSummary>>`.
**Omits** Phone / Email / Website (loaded only on the per-id detail).

### `AdminOrganisationDetail` (view/edit form — `data`)
```jsonc
{ "id": "guid", "nameAr": "string", "nameEn": "string?",
  "commercialRegistration": "string?", "sector": "string?",
  "city": "string?", "phone": "string?", "email": "string?",
  "website": "string?", "isActive": true,
  "createdAt": "2026-06-13T00:00:00+00:00",
  "updatedAt": "2026-06-13T00:00:00+00:00" }  // updatedAt nullable
```
Returned by Get / Create / Update as `ApiResult<AdminOrganisationDetail>`.

### `CreateOrganisationRequest` (POST body)
```jsonc
{ "nameAr": "string (required)", "nameEn": "string?",
  "commercialRegistration": "string?", "sector": "string?",
  "city": "string?", "phone": "string?", "email": "string?",
  "website": "string?" }
```

### `UpdateOrganisationRequest` (PUT body)
Same as create **plus** `"isActive": true` (the only field create lacks). No
`id` in the body — it comes from the route.

### `OrganisationImportResult` (import response — `data`)
```jsonc
{ "rowsRead": 0, "inserted": 0, "updated": 0, "skipped": 0,
  "errors": ["Row 2: Arabic name is required."] }  // errors capped at 50
```

### `AdminGridExportRequest` (export body — shared grid layer)
```jsonc
{ "ids": ["guid"], "query": { /* GridQuery, or null when ids non-empty */ } }
```
Response is a binary `.xlsx` stream (direct download), **not** an `ApiResult`
envelope. Sheet "Organisations", header `NameAr | NameEn |
CommercialRegistration | Sector | City | IsActive`. Whole-grid export is capped
at 5,000 rows.

### `GridQuery` (list/export request)
The standard CP grid query — `Skip`, `Top` (clamped 1–200 on the server; the
page sends `Top = 20`), `Search`, `Sort`, `SortDescending`, `Filters`
(per-column dictionary). See the SimfDataGrid standard.

## Error codes (`ApiResult<T>.Error`, source `ErrorCodes.cs`)

| Code | HTTP | When | Bilingual surface (verbatim from service/endpoint) |
|------|------|------|----------------------------------------------------|
| `ORGANISATION_INVALID` | 400 | a field fails validation (Arabic name not 1–150; any optional field over its cap) | EN "Organisation Arabic name must be between 1 and 150 characters." / AR "يجب أن يتراوح طول الاسم العربي للمنظمة بين 1 و 150 حرفاً." (and per-field variants) |
| `ORGANISATION_INVALID` | 409 | duplicate commercial registration (on create, or on update when the CR changes) | EN "An organisation with commercial registration '{cr}' already exists." / AR "توجد منظمة بالسجل التجاري '{cr}' بالفعل." |
| `ORGANISATION_NOT_FOUND` | 404 | get/update/delete on a missing id | EN "The organisation was not found." / AR "لم يتم العثور على المنشأة." (endpoint) / "لم يتم العثور على المنظمة." (service) |
| `ORGANISATION_IMPORT_FAILED` | 413 | import upload over 5 MB | EN "The Excel file is too large. The maximum is 5 MB." / AR "ملف Excel كبير جدًا. الحد الأقصى 5 ميغابايت." |
| `ORGANISATION_IMPORT_FAILED` | 400 | import workbook unparseable | EN "The uploaded file could not be read as an Excel workbook." / AR "تعذّرت قراءة الملف المرفوع كمصنّف Excel." |
| (`DataValidationException`) | 400 | import: no file, or file bytes are not the ZIP/xlsx magic `50 4B 03 04` | EN "An Excel file is required." / "The file is not a valid Excel workbook." + AR pairs |

> The two endpoint-level `NotFound` strings differ slightly in Arabic ("المنشأة"
> in `GetOrganisationEndpoint` vs "المنظمة" in the service) — noted, not changed.

## Import upload guards (`ImportOrganisationsEndpoint`)

1. A file is required (`Files.GetFile("file")`, multipart field **`file`**).
2. Size cap **5 MB** → 413 `ORGANISATION_IMPORT_FAILED`.
3. First four bytes must be the ZIP magic `50 4B 03 04` (defence-in-depth vs a
   zip-bomb workbook) → `DataValidationException` otherwise.
4. An unparseable-but-valid-magic workbook → 400 `ORGANISATION_IMPORT_FAILED`.
   Per-row failures (e.g. blank Arabic name) are counted under "Skipped" and
   returned in `errors` (capped at 50) — a bad row is **not** a batch abort.

> **Upsert semantics.** The import **fills** columns; it never clears them. A
> cell the sheet leaves blank is "not supplied", so a partial-update workbook
> carrying only the Arabic name updates nothing else and leaves the commercial
> registration, the English name and the contact columns as they were. Clearing
> a field is the explicit Edit endpoint's job (`PUT`, which does assign every
> field from the request body).

## App-consumption note (`/app/organisations`)

The app reads the **same** organisation data from a **separate, sign-in-only**
endpoint — it is **not** part of this CP page and is **not** admin-gated:

| | |
|---|---|
| API endpoint | `OrganisationPickerSearchEndpoint` |
| Full route | `GET /api/v1/app/organisations?search={text}&top={n}` |
| Auth | `Tags("Account")` + `RequireRateLimiting("auth")`, **no `Policies(...)`** — signed-in but not admin-gated, not approval-gated; **not** `AllowAnonymous` |
| Service | `IPublicOrganisationService` (`PublicOrganisationService`) — `AsNoTracking().Where(o => o.IsActive)`, LIKE over Arabic name / English name / city, ordered by Arabic name, `top` clamped 1–50 (default 20) |
| Returns | `ApiResult<IReadOnlyList<OrganisationPickerItem>>` |

```jsonc
// OrganisationPickerItem — the bilingual picker row the app's الجهة field uses
[ { "id": "guid", "nameAr": "string", "nameEn": "string?", "city": "string?" } ]
```

Only **active** organisations reach the picker, so a CP Deactivate immediately
removes a row from the app's الجهة list. Documented end-to-end on
**[App Page 007 API](../../App/Page_007/Page_007_API.md) §E6** (the picker) and
stored back as `organisationId` (bare Guid, D-221) on the profile upsert.
