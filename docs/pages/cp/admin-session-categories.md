# Session categories — `/admin/session-categories`

| | |
|--|--|
| **Route** | `/admin/session-categories` |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.SessionCategories.View)]` (page) + per-action `Create` / `Edit` / `Delete` / `Export` / `Import` policies + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (mutations) |
| **Pattern** | B9b (D-226) dynamic lookup CRUD. D-256 `SimfDataGrid`; D-353 `CrudShell` framing; D-356 Excel export + import. |
| **Status** | ✅ Real (D-226; ships empty pending the client's category list — OI-2) |
| **Backend endpoints** | BFF `/account/api/admin/session-categories/*` → API: `POST /admin/session-categories/list`, `GET /admin/session-categories/{id}`, `POST /admin/session-categories`, `PUT /admin/session-categories/{id}`, `DELETE /admin/session-categories/{id}`, `POST /admin/session-categories/export`, `POST /admin/session-categories/import` |
| **Source** | [`SessionCategoriesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionCategoriesList.razor), [`SessionCategoriesAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionCategoriesAddEdit.razor), [`SessionCategoriesViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionCategoriesViewDelete.razor), [`SessionCategoryEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionCategoryEndpoints.cs), [`SessionCategoriesExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionCategoriesExcelEndpoints.cs), [`AdminSessionCategoryService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminSessionCategoryService.cs), [`SessionCategory.cs`](../../../src/Backend/SIMF.Domain/Programme/SessionCategory.cs) |
| **Backed by** | **New** `dbo.SessionCategories` table (migration `D226_AddSessionCategory`, 2026-06-01) + nullable `Session.CategoryId` FK. |
| **Tests** | [`docs/tests/e2e/cp-admin-session-categories.md`](../../tests/e2e/cp-admin-session-categories.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

The session category is a **dynamic lookup** per SIMF-FDS-004 §5.4 ("a dynamic
Category, for example a main session") — a small bilingual list a `Session`
optionally points at through `Session.CategoryId`. Each row has an English name,
an Arabic name, a display order (the picker / grid sort key), and an active flag
(soft-delete).

This page is the admin CRUD over that lookup. It is modelled on the Organisation
lookup: in-service validation, soft-delete via `IsActive`, one audit row per
mutation. The value list is **the client's to confirm (open item OI-2)** — the
table **ships empty** and the team seeds it later, so the empty-state path is the
default first render and nothing is invented in code.

## 4. UI

- Banner (`SimfBanner`, title `Admin.SessionCategories.Title`) + the canonical
  `SimfDataGrid` (owner-mandated list-page standard).
- Grid columns: Name (English), Name (Arabic), Order, Active (`SimfPill` on/off
  badge). Multiselect renders select-all / per-row checkboxes, but there is **no
  bulk-action toolbar button** (selection is cosmetic — no bulk endpoint).
- Server-paged with a numbered pager (`GridQuery { Top = 20 }`); per-column filter
  inputs on **Name (English)** (`name`) and **Name (Arabic)** (`namearabic`); column
  sort on all four columns (`name` / `namearabic` / `order` / `isActive`).
- Toolbar **Add** + per-row **Edit** (pencil) / **Details** (eye) / **Delete**
  (trash) are quiet grid affordances (`OnAdd` / `OnEditOne` / `OnDetailsOne` /
  `OnDeleteOne`), not filled text buttons.
- Add / Edit / View / Delete are hosted by `CrudShell` framing the reusable
  `SessionCategoriesAddEdit` (Add/Edit) and `SessionCategoriesViewDelete`
  (View/Delete) forms. The Details (eye) action opens `SessionCategoriesViewDelete`
  read-only (`IsDelete=false`, no Deactivate button); Edit re-fetches the row via
  `GET …/{id}` to pre-fill.
- Per-row Delete is a soft-delete (Deactivate) behind a `SimfConfirm` gate (D-353):
  the trash action opens `SessionCategoriesViewDelete` (`IsDelete=true`) whose red
  Deactivate button raises a `SimfConfirm` dialog naming the row — the old native
  `confirm()` on the list is gone.
- **Excel export + import (D-356):** the grid toolbar carries **Export** and
  **Import** actions wired through a shared
  `<CrudGridExcel @ref="_excel" Resource="session-categories">`. Export posts
  `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/session-categories/export` (selected row ids, else empty
  `Ids` + the current `Query` = whole filtered grid) and downloads an `.xlsx` with
  the sheet **"SessionCategories"** and header row
  `Name | NameArabic | DisplayOrder | IsActive`. Import (insert-only) posts an
  `.xlsx` multipart to `/account/api/admin/session-categories/import` (required
  headers `Name | NameArabic`, row key = `Name`) and shows a result modal
  ("N created, N updated, N skipped" + per-row errors) plus the shared
  `Grid.Import.Done` success toast, then reloads the grid. A non-`.xlsx` upload is
  rejected with HTTP 400 surfaced via `OnExcelError`.
- **Page ↔ Popup presentation toggle (D-353):** the toolbar `CustomToolbar` carries
  `<CrudPresentationToggle PageKey="session-categories" @bind-Value="_presentation">`
  to host Add/Edit/View/Delete as a dialog or a full page; the choice persists in
  `localStorage` under `simf.cp.prefs.session-categories` (via `CpPreferences`) and
  is restored in `OnInitializedAsync` (`Prefs.GetPresentationAsync("session-categories")`).
  In full-page mode the grid + banner are hidden (`GridHidden`).

## 4.5 Form fields

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Name (English) | yes | 128 | 1–128 chars (client guard + server) |
| Name (Arabic) | yes | 128 | 1–128 chars (client guard + server) |
| Display order | yes | n/a | integer ≥ 0; non-numeric coerces to 0 (`int.TryParse` fallback) |
| Active | (Edit only) | bool | shown only when `IsEdit=true`; ticked by default on Add |

## 5. Data flow + endpoints

BFF passthroughs live in `AccountEndpoints.cs` under
`/account/api/admin/session-categories/*` and `Forward` to `SimfAdminClient`,
which calls the API. The export + import routes are wired by the shared
`MapGridExcel(group, "session-categories")` helper.

| Verb + BFF route | API endpoint | Permission policy | Notes |
|------------------|--------------|-------------------|-------|
| `POST …/list` | `POST /admin/session-categories/list` | `SessionCategories.View` | `GridQuery` in → `GridPage<AdminSessionCategorySummary>` |
| `GET …/{id}` | `GET /admin/session-categories/{id:guid}` | `SessionCategories.View` | `AdminSessionCategoryDetail`; 404 `SESSION_CATEGORY_NOT_FOUND` |
| `POST …` | `POST /admin/session-categories` | `SessionCategories.Create` | create; rate-limited (`auth`) |
| `PUT …/{id}` | `PUT /admin/session-categories/{id:guid}` | `SessionCategories.Edit` | update; id from route, request carries no id |
| `DELETE …/{id}` | `DELETE /admin/session-categories/{id:guid}` | `SessionCategories.Delete` | soft-delete (`Deactivate`); returns `ApiResult<bool>` |
| `POST …/export` | `POST /admin/session-categories/export` | `SessionCategories.Export` | grid → `.xlsx` (`ExportSessionCategoriesEndpoint`) |
| `POST …/import` | `POST /admin/session-categories/import` | `SessionCategories.Import` | `.xlsx` → insert-only (`ImportSessionCategoriesEndpoint`) |

All seven permission codes (`View` / `Create` / `Edit` / `Delete` / `Export` /
`Import`, baseline `AdminOnly`) are defined on the `PermissionCatalog.SessionCategories`
nested class and registered in `PermissionCatalog.All`. The nav item
`Module.SessionCategories` carries `RequiredPermission: SessionCategories.View`.

## 6. Validation + error handling

- **Client guard (`SessionCategoriesAddEdit.HandleSubmitAsync`):** blank or
  over-128 English/Arabic name → in-form `SimfAlert`
  (`Admin.SessionCategories.Required`); no request fires. Display order is parsed
  with `int.TryParse` and coerced to 0 when blank/non-numeric/negative.
- **Server-side `AdminSessionCategoryService.ValidateAndNormalise`:** trims each
  name and length-gates both to 1–128; over/under → 400
  `SESSION_CATEGORY_INVALID` with the bilingual English-name or Arabic-name
  message.
- **Not found:** 404 `SESSION_CATEGORY_NOT_FOUND` (GET / PUT / DELETE on a missing
  id).
- **No uniqueness constraint.** This lookup has no duplicate-name guard, so there
  is no 409/conflict path (deliberately absent — unlike Themes' `Code` uniqueness).
- **Import per-row errors:** a blank `Name` row raises a `DataValidationException`
  ("The English name is required." / "الاسم بالإنجليزية مطلوب.") aggregated as a
  per-row error by the base import endpoint, not a batch abort.

## 7. Edge cases + known limitations

- **Ships empty (OI-2).** The table is seeded by the team once the client confirms
  the category list; the empty grid renders `SimfEmptyState`
  (`Admin.SessionCategories.None`) as the default first render.
- **Deactivate is idempotent.** `DeactivateAsync` returns early when the row is
  already inactive (no error, no audit row).
- **No active filter on the list.** The page sends `GridQuery { Top = 20 }` with no
  default active filter, so a soft-deleted row stays visible — its Active column
  flips from the on pill to the off pill rather than disappearing. (The service
  honours an `isActive` filter key, but the UI does not surface a filter input for
  it.)
- **`Session.CategoryId` FK is RESTRICT.** A category referenced by a session
  cannot be hard-deleted at the DB level; the page only ever soft-deletes, so this
  is not surfaced as a guarded error code here.
- **Display-order coercion** — invalid input resolves to 0 on both the client and
  the server side (the create request defaults `DisplayOrder` to 0).

## 8. i18n + RTL

`Admin.SessionCategories.*` keys (title, column headers, field labels, action
labels, toasts, empty/loading states) plus the shared `Grid.*` keys. EN ↔ AR
parity is maintained across both resx locales; the page mirrors under
`<html dir="rtl">` when Arabic is active. (Exact resx phrasing per the `Strings`
resource files — descriptive here, not quoted verbatim.)

## 10. Use cases

- Manage the session-category lookup that backs the CP session form's category
  picker (create / edit / soft-delete), and bulk-seed it via Excel import once the
  client confirms the list (OI-2).

## 11. E2E

See [`docs/tests/e2e/cp-admin-session-categories.md`](../../tests/e2e/cp-admin-session-categories.md):
E2E-SCT-001 full CRUD round-trip, 002 empty list, 003 auth gate, 004 Add modal,
005 Edit pre-fill, 006 delete via ViewDelete + SimfConfirm, 007 delete cancelled,
008 cancel closes, 009 client blank-names, 010 server over-128 (400), 011
display-order coercion, 012 action-level gating, 013 server-500, 014 RTL, 015
per-column filter, 016 column sort, 017/018 presentation toggle (D-353), 019 Excel
export, 020 Excel import, 021 import rejection (D-356).

## 12. Related docs

- Per-page CP documentation set (4-aspect + README, D-380):
  [`../../CP/admin-session-categories/README.md`](../../CP/admin-session-categories/README.md)
  (Function / Logic / API / Design).
- Authority spec: SIMF-FDS-004 §5.4 (dynamic Category) + §7.
- Decisions: D-226 (built as a team-seeded lookup, NOT a fixed enum; ships empty
  pending the client's list, OI-2); D-256 raw-table → `SimfDataGrid`; D-353
  `CrudShell` / `SimfConfirm` framing + presentation toggle; D-356 Excel export +
  import.
- Sibling lookups: Organisation lookup, Themes (`admin-themes.md`).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-01 | D-226 | Original — `SessionCategory` dynamic lookup + EF migration `D226_AddSessionCategory` (+ `Session.CategoryId` FK) + admin CRUD page. Team-seeded, ships empty (OI-2). |
| 2026-06-03 | D-256 | Raw-table list converted to the canonical `SimfDataGrid` (per-column filter on names, sort on all four columns, select-all). |
| 2026-06-11 | D-356 / D-353 | Excel **export + import** added (toolbar Export/Import → `.xlsx`, sheet "SessionCategories", required import headers `Name | NameArabic`, row key `Name`, 5000-row cap, non-`.xlsx` → 400); CRUD split into `SessionCategoriesAddEdit` + `SessionCategoriesViewDelete` hosted by `CrudShell` with a `SimfConfirm`-gated Deactivate and a Page↔Popup presentation toggle persisted in `localStorage`. E2E catalogue extended with E2E-SCT-017…021. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 — Excel export + import + D-353 CrudShell/SimfConfirm toggle).
