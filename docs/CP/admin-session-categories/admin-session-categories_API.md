# CP Session categories — API (`/admin/session-categories`)

The authoritative contract: the CP BFF passthroughs, the API endpoints they
forward to, the DTOs, and the error model. Every route / verb / permission /
field below is verbatim from the source read this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Call path

```
CP page (SessionCategoriesList.razor)
  → JS interop simfAccount.{postJson|getJson|putJson|deleteJson}
  → BFF passthrough  /account/api/admin/session-categories/*   (AccountEndpoints.cs, cookie → bearer)
  → SimfAdminClient (typed client)
  → API endpoint     /api/v1/admin/session-categories/*        (SessionCategoryEndpoints.cs / …ExcelEndpoints.cs)
  → AdminSessionCategoryService                                 (SimfAppDbContext)
```

The browser holds an auth **cookie**; the BFF resolves it to the access token
and `Forward`s to the API — the token never reaches the browser. All responses
are wrapped in the standard `ApiResult<T>` envelope (SIMF-API-001).

## Endpoint table

| # | BFF route (CP) | API endpoint | Verb | Permission policy | Request → Response |
|---|----------------|--------------|------|-------------------|--------------------|
| 1 | `POST /account/api/admin/session-categories/list` | `/admin/session-categories/list` | POST | `SessionCategories.View` + `RequireApprovedAccount` | `GridQuery` → `ApiResult<GridPage<AdminSessionCategorySummary>>` |
| 2 | `GET /account/api/admin/session-categories/{id}` | `/admin/session-categories/{id:guid}` | GET | `SessionCategories.View` + `RequireApprovedAccount` | route id → `ApiResult<AdminSessionCategoryDetail>`; 404 `SESSION_CATEGORY_NOT_FOUND` |
| 3 | `POST /account/api/admin/session-categories` | `/admin/session-categories` | POST | `SessionCategories.Create` + `RequireApprovedAccount` + rate-limit `auth` | `AdminCreateSessionCategoryRequest` → `ApiResult<AdminSessionCategoryDetail>` |
| 4 | `PUT /account/api/admin/session-categories/{id}` | `/admin/session-categories/{id:guid}` | PUT | `SessionCategories.Edit` + `RequireApprovedAccount` + rate-limit `auth` | `AdminUpdateSessionCategoryRequest` (id from route via `Route<Guid>("id")`; **request carries no id**) → `ApiResult<AdminSessionCategoryDetail>` |
| 5 | `DELETE /account/api/admin/session-categories/{id}` | `/admin/session-categories/{id:guid}` | DELETE | `SessionCategories.Delete` + `RequireApprovedAccount` + rate-limit `auth` | route id → `ApiResult<bool>` (soft-delete; always `true`) |
| 6 | `POST /account/api/admin/session-categories/export` | `/admin/session-categories/export` | POST | `SessionCategories.Export` (+ approved) | `AdminGridExportRequest { Ids, Query }` → `.xlsx` binary (`ExportSessionCategoriesEndpoint`) |
| 7 | `POST /account/api/admin/session-categories/import` | `/admin/session-categories/import` | POST | `SessionCategories.Import` (+ approved) | `.xlsx` multipart → per-row result (`ImportSessionCategoriesEndpoint`, insert-only) |

> The API endpoints are mounted under the versioned base (the Excel endpoint
> doc-comment shows `/api/v1/admin/session-categories/export`); the route
> strings declared in `Configure()` are the relative paths above. The Export +
> Import BFF passthroughs are wired by the shared helper
> `MapGridExcel(group, "session-categories")` in `AccountEndpoints.cs`.

## DTOs (`SIMF.Contracts/Admin/SessionCategories.cs`)

### `AdminSessionCategorySummary` (grid row)
```csharp
record AdminSessionCategorySummary(
    Guid   Id,
    string Name,          // English
    string NameArabic,
    int    DisplayOrder,
    bool   IsActive);
```

### `AdminSessionCategoryDetail` (view / edit prefill)
```csharp
record AdminSessionCategoryDetail(
    Guid            Id,
    string          Name,
    string          NameArabic,
    int             DisplayOrder,
    bool            IsActive,
    DateTimeOffset  CreatedAt,
    DateTimeOffset? UpdatedAt);
```

### `AdminCreateSessionCategoryRequest`
```csharp
class AdminCreateSessionCategoryRequest {
    string Name        = "";   // English
    string NameArabic  = "";
    int    DisplayOrder;       // defaults 0
}
```

### `AdminUpdateSessionCategoryRequest`
```csharp
class AdminUpdateSessionCategoryRequest {
    string Name        = "";
    string NameArabic  = "";
    int    DisplayOrder;
    bool   IsActive    = true; // edit form sends the checkbox state
}
```

The create request **always** sets `IsActive = true` server-side (the
`CreateAsync` service constructs the entity with `IsActive = true`); there is no
`IsActive` field on the create payload. The update request carries `IsActive` so
Edit can reactivate / deactivate.

## Error model

| HTTP | `ApiResult.Error.Code` | When | Message (EN / AR) |
|------|------------------------|------|-------------------|
| 400 | `SESSION_CATEGORY_INVALID` | English name not 1–128 chars (after trim) | "Session category English name must be between 1 and 128 characters." / "يجب أن يتراوح طول الاسم الإنجليزي للتصنيف بين 1 و 128 حرفاً." |
| 400 | `SESSION_CATEGORY_INVALID` | Arabic name not 1–128 chars (after trim) | "Session category Arabic name must be between 1 and 128 characters." / "يجب أن يتراوح طول الاسم العربي للتصنيف بين 1 و 128 حرفاً." |
| 404 | `SESSION_CATEGORY_NOT_FOUND` | GET / PUT / DELETE on a missing id | "The session category was not found." / "لم يتم العثور على تصنيف الجلسة." |
| 401 | — | `sub` claim missing / unparseable on a mutation | `Send.UnauthorizedAsync` |
| 400 | (Excel base) | non-`.xlsx` (ZIP-magic fail), >5MB, or wrong sheet on import | bilingual Excel-base messages; surfaced via `OnExcelError` |

Error codes are constants in `SIMF.Common/ErrorCodes.cs`
(`SessionCategoryInvalid` / `SessionCategoryNotFound`). The CP surfaces
`Error.MessageForCurrentCulture()` (bilingual, locale-aware), falling back to
`Admin.SessionCategories.LoadFailed` when no envelope error is present.

> **No uniqueness / conflict path.** This lookup has **no** duplicate-name
> guard, so there is **no 409**. (Unlike Themes' `Code` uniqueness — the absence
> is deliberate.)

## Excel detail (D-356)

- **Export** (`ExportSessionCategoriesEndpoint`): sheet `SessionCategories`,
  file prefix `simf-session-categories`, columns
  `Name | NameArabic | DisplayOrder | IsActive`. Empty `Ids` + `Query` = whole
  filtered grid; populated `Ids` = just those rows. Capped at the grid-export
  row limit.
- **Import** (`ImportSessionCategoriesEndpoint`, insert-only): sheet
  `SessionCategories`, required headers `Name | NameArabic`, row key = `Name`.
  Each row binds to `AdminCreateSessionCategoryRequest`
  (`DisplayOrder` parsed with `int.TryParse`, else 0) and is **Created**. A
  blank `Name` raises `DataValidationException` ("The English name is
  required." / "الاسم بالإنجليزية مطلوب.") aggregated as a **per-row** error by
  the base import endpoint — not a batch abort.

## How `Session.CategoryId` reaches the app (consumer contract)

This page only writes the lookup. A `Session` references a category by the
**bare `Session.CategoryId`** (logical FK; resolved on read — no cross-context
JOIN beyond the App context). The public agenda endpoint
`GET /app/programme/sessions` (Page_016) projects each session with
`categoryId` / `categoryName` / `categoryNameArabic` so the app shows the
"is-main-session / type" tag without a second call. See
[`docs/App/Page_016/Page_016_API.md`](../../App/Page_016/Page_016_API.md) and
`Page_016_Logic.md` L-4. The CP session form (sibling set
[`docs/pages/cp/admin-sessions.md`](../../pages/cp/admin-sessions.md)) loads the
**active** rows from the list endpoint to populate its category picker,
resolving the name client-side like the Hall / Company picker.

## Cross-links

- What the admin does: [admin-session-categories_Function.md](admin-session-categories_Function.md)
- Validation / audit / data flow: [admin-session-categories_Logic.md](admin-session-categories_Logic.md)
- E2E: [`docs/tests/e2e/cp-admin-session-categories.md`](../../tests/e2e/cp-admin-session-categories.md)
- CP reference: [`docs/pages/cp/admin-session-categories.md`](../../pages/cp/admin-session-categories.md)
