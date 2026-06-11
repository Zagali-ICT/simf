# News — `/admin/news`

| | |
|--|--|
| **Route** | `/admin/news` |
| **Audience** | Administrator / PublicRelations |
| **Auth** | `[RequirePermission(PermissionCatalog.News.View)]` (CP page) + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (API mutations) |
| **Pattern** | D-199 event module (Mockup screen 29 / 29b authoring) — canonical SimfDataGrid CRUD + D-353 CrudShell forms + D-356 Excel + D-357 media-asset image. |
| **Status** | ✅ Real (D-199) |
| **Backend endpoints** | BFF `/account/api/admin/news/*` (Control-Panel passthrough) → API: `POST /admin/news/list`, `GET /admin/news/{id}`, `POST /admin/news`, `PUT /admin/news/{id}`, `DELETE /admin/news/{id}`, `POST /admin/news/export`, `POST /admin/news/import` |
| **Source** | [`NewsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/NewsList.razor), [`NewsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/NewsAddEdit.razor), [`NewsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/NewsViewDelete.razor), [`AdminNewsEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/News/AdminNewsEndpoints.cs), [`NewsExcelEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/NewsExcelEndpoints.cs), [`AdminNewsService`](../../../src/Backend/SIMF.Infrastructure/PublicRelations/AdminNewsService.cs), [`News`](../../../src/Backend/SIMF.Domain/PublicRelations/News.cs) |
| **Backed by** | `dbo.News` table on `SimfAppDbContext` (D-199 event-module wave). |
| **Tests** | [`docs/tests/e2e/cp-admin-news.md`](../../tests/e2e/cp-admin-news.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

Admin CRUD over the public News / Media-Centre feed (SIMF-FDS / Mockup screen
29 card list + 29b article). PR / marketing editors own the bilingual title,
excerpt, body, category kicker, hero image, publish date and display order.
The public feed (Website + Flutter app) is sorted newest-first by `PublishedAt`
then `DisplayOrder`, and an article is publicly visible only once
`PublishedAt <= now`. The **admin grid shows every row** regardless of
`IsActive` / publish window, so editors can manage drafts and reactivate
soft-deleted items.

The page follows the canonical `SimfDataGrid` CRUD shape: a paged, filterable,
sortable grid with per-row Edit / Details / Delete icon actions and a toolbar
that carries Add, Export, Import and the D-353 presentation toggle. The Add /
Edit / Details / Delete forms are reusable components hosted by `CrudShell`
(D-353), and the cover image is wired to the unified media-asset pipeline
(D-357).

## 4. UI

- `SimfBanner` titled `Admin.News.Title` + the canonical `SimfDataGrid` toolbar
  (Select-all, Add, Export, Import) plus a `CustomToolbar` slot holding the
  D-353 `CrudPresentationToggle`.
- Grid columns: Title (English) [sortable, filterable], Title (Arabic)
  [filterable], Category (English) [filterable], Category (Arabic) [filterable],
  Publish date (`yyyy-MM-dd`) [sortable], Display order [sortable], Active
  (`SimfPill` on/off). The Arabic title/category and the Active column expose no
  filter input; only Title (EN), Publish date and Display order are sortable.
- Multiselect grid (row checkbox + select-all) keyed on the row id.
- Empty grid renders `SimfEmptyState` (title `Admin.News.None`).
- Per-row **Edit** opens the `NewsAddEdit` form (Initial = the detail fetched by
  `GET /account/api/admin/news/{id}`) with the IsActive checkbox shown.
- Per-row **Details** opens the `NewsViewDelete` form read-only (IsDelete=false).
- Per-row **Delete** opens the `NewsViewDelete` form (IsDelete=true) whose red
  Delete button is gated by a `SimfConfirm` dialog — a soft-delete, never a
  one-click row removal (D-353; the old native `confirm()` is gone).
- **Excel export + import (D-356):** the toolbar carries **Export** and
  **Import** actions wired through the reusable `CrudGridExcel` component
  (`Resource="news"`). Export posts an `AdminGridExportRequest { Ids, Query }`
  to `/account/api/admin/news/export` — the selected row ids when rows are
  selected, otherwise an empty `Ids` + the current `GridQuery` (whole filtered
  grid) — and downloads `simf-news-{yyyyMMddHHmmss}.xlsx` with the sheet "News"
  and header row `Title | TitleArabic | Category | CategoryArabic | PublishedAt
  | DisplayOrder | IsActive`. Import (insert-only) posts an `.xlsx` to
  `/account/api/admin/news/import` and shows the shared import-result modal
  ("N created, N updated, N skipped" + per-row errors) then a green
  `Grid.Import.Done` toast and a grid reload. A duplicate English title is a
  per-row error, not a batch abort. Both are capped at 5000 rows.
- **Page ↔ Popup presentation toggle (D-353):** the toolbar
  `CrudPresentationToggle` (PageKey "news") lets the admin host the
  Add/Edit/View/Delete `CrudShell` as a dialog or a full page; the choice
  persists in `localStorage` under `simf.cp.prefs.news` via `CpPreferences` and
  is restored on load (`Prefs.GetPresentationAsync("news")` in
  `OnInitializedAsync`). In full-page mode the grid + banner are hidden
  (`GridHidden`) while the form frame is open.

## 4.5 Form fields

`NewsAddEdit` (Add hides the Active checkbox; Edit shows it):

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Title (English) | yes | 200 | 1–200 chars; unique English title |
| Title (Arabic) | yes | 200 | 1–200 chars |
| Category (English) | yes | 100 | 1–100 chars |
| Category (Arabic) | yes | 100 | 1–100 chars |
| Excerpt (English) | no | 500 | optional |
| Excerpt (Arabic) | no | 500 | optional |
| Body (English) | yes | 8000 | 1–8000 chars |
| Body (Arabic) | yes | 8000 | 1–8000 chars |
| Image path | no | 512 | relative path text (legacy field) |
| Publish date | yes | n/a | `<input type="date">`, parsed AssumeUniversal/AdjustToUniversal; defaults to `UtcNow` |
| Display order | yes | n/a | integer ≥ 0 (`<input type="number" min="0" max="99999">`) |
| Active | (Edit only) | bool | — (Add path always creates `IsActive = true`) |
| Cover image | (Edit only) | n/a | D-357 `SimfImageUpload` (Category `NewsImage`, OwnerId = the article id); shown only once the row exists |

The client guard short-circuits before any POST/PUT if Title / Body / Category
(English **or** Arabic) is blank, surfacing `Admin.News.RequiredFields`.

## 5. Data flow + endpoints

- The CP page calls the Control-Panel BFF over JS interop
  (`simfAccount.postJson` / `getJson` / `putJson` / `deleteJson`) against
  `/account/api/admin/news/*`. The BFF (`AccountEndpoints.cs`) forwards each call
  to the API with the caller's bearer token via `SimfAdminClient`; the Excel
  export/import routes are wired through the shared `MapGridExcel(group,
  "news")` helper.
- The API endpoints (`AdminNewsEndpoints.cs`) are FastEndpoints returning
  `ApiResult<T>`: `list` → `GridPage<AdminNewsSummary>`, `{id}` →
  `AdminNewsDetail`, create/update → `AdminNewsDetail`, delete → `bool`.
- `AdminNewsService` (on `SimfAppDbContext`) owns paging
  (`Skip`/`Top` clamped 1–200, default 25), the per-column filters (`title`,
  `titlearabic`, `category`, `categoryarabic`, `isactive`), the free-text
  `Search` (Title/TitleArabic/Category/CategoryArabic LIKE), sorting (`title`,
  `displayorder`, `publishedat`; default `PublishedAt` desc then `DisplayOrder`),
  the create/update/soft-delete writes and one audit row per mutation.
- The Excel export/import endpoints (`NewsExcelEndpoints.cs`) subclass the
  generic `AdminGridExportEndpoint<AdminNewsSummary>` /
  `AdminGridImportEndpoint`; the import binds each row to `CreateNewsRequest`
  (insert-only) and demands `Title, TitleArabic, Body, BodyArabic, Category,
  CategoryArabic` as the required headers.

## 6. Validation + error handling

- **FluentValidation** (`CreateNewsValidator` / `UpdateNewsValidator`) lengths
  mirror `NewsConfiguration.HasMaxLength` exactly: Title/TitleArabic 200,
  Excerpt/ExcerptArabic 500, Body/BodyArabic 8000, Category/CategoryArabic 100,
  ImageRelativePath 512, DisplayOrder ≥ 0; Title, Body, Category (EN + AR) are
  `NotEmpty`.
- **Server-side `AdminNewsService.Validate`** re-checks the same bounds
  (`RequireText` / `OptionalText`) so the contract holds for non-HTTP callers
  (including the Excel import). A missing/over-length field or negative
  `DisplayOrder` is **400 `NEWS_INVALID`** with a bilingual message.
- **Duplicate English title:** **409 `NEWS_TITLE_DUPLICATE`** (bilingual; echoes
  the clashing title). On Update the check fires only when the English title is
  changed to clash with another row.
- **Not found:** **404 `NEWS_NOT_FOUND`** (Get / Update / Delete).
- **Excel import** per row: a blank required cell throws
  `DataValidationException` and a duplicate English title throws the 409 — both
  are caught per row into the result's error list, never aborting the batch.
- **Excel upload defence (D-045 H1):** an empty upload → 400
  (`AdminImportEmpty`); over 5 MB → 413; a file failing the ZIP-magic check, or
  a workbook missing the "News" sheet / a required header → 400 with a bilingual
  message surfaced by `CrudGridExcel.OnError`.

## 7. Edge cases + known limitations

- **Admin grid lists every row** — unlike most CP lists, it shows rows
  regardless of `IsActive` / publish window (drafts + soft-deleted), so "Delete"
  flips the Active column to "—" rather than removing the row.
- **Reactivate via Edit** — a soft-deleted article is recovered by re-ticking
  the Active checkbox in the Edit form (PUT with `IsActive = true`).
- **Delete is idempotent** — `DeactivateAsync` returns early if the row is
  already inactive (no second audit row).
- **Title uniqueness is on the English title only** (case-insensitive on the
  Update title-change check); the Arabic title is not constrained unique.
- **Two image surfaces coexist** — the legacy `ImageRelativePath` text field
  (round-tripped as plain text, also rendered as a preview in the detail view)
  and the D-357 `NewsImage` media-asset pipeline (`SimfImageUpload` in Edit;
  the detail/delete view shows the asset via
  `/account/api/admin/assets/NewsImage/{id}/image`).
- **Publish-in-the-future** is authored-but-hidden publicly; the admin grid
  still shows it.

## 8. i18n + RTL

`Admin.News.*` resx keys (banner title, column headers, field labels, Add/Edit/
Details/Delete titles, Save/Cancel/Close, toasts: Saved / Deleted / LoadFailed /
RequiredFields / None / Loading / Summary) plus the shared `Grid.*` and
`Grid.Import.*` keys, in EN ↔ AR parity. The delete confirmation copy
(`Admin.News.Delete.Message`) is formatted with the article title. RTL: the
Arabic toggle mirrors the page, grid and forms right-to-left (the exact Arabic
strings are descriptive here — see the resx for the authoritative text).

## 10. Use cases

- Create a News article (Add → POST), edit / reactivate (Edit → PUT), retire
  (Delete → soft-delete), bulk export/import via Excel (D-356), and attach a
  cover image via the media-asset pipeline (D-357).

## 11. E2E

See [`docs/tests/e2e/cp-admin-news.md`](../../tests/e2e/cp-admin-news.md):
E2E-NWS-001 full CRUD round-trip, 002 empty list, 003 auth gate, 004 Add field
set, 005 Edit pre-fill + Active, 006 delete confirm gate, 007 client validation,
008 over-length 400 `NEWS_INVALID`, 009 duplicate title 409
`NEWS_TITLE_DUPLICATE`, 010 date/order round-trip, 011 reactivate, 012 list 500
fallback, 013 RTL, 014 per-column filter, 015 column sort, 016/017 presentation
toggle + full-page round-trip (D-353), 018 delete confirmation gate (D-353),
019 Excel export, 020 Excel import, 021 Excel import rejection (D-356), 022 image
via the media-asset pipeline (D-357).

## 12. Related docs

- API integration tests: `tests/SIMF.Api.Tests/NewsTests.cs` (CRUD + 409/400/404
  + per-action `News.*` gates) and `tests/SIMF.Api.Tests/NewsExcelTests.cs`
  (export/import).
- Permissions: `News.View / Create / Edit / Delete / Export / Import` in
  `PermissionCatalog.News`; the `PublicRelations` role holds the `News.*`
  baseline, `Administrator` via the `"*"` wildcard.
- Decisions: D-199 (event-module wave), D-353 (CrudShell + presentation toggle),
  D-356 (grid Excel export/import), D-357 (media-asset image pipeline).
- Authority spec: SIMF-FDS-004 (News / Media Centre); Mockup screen 29 / 29b.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-02 | D-199 | Original — News entity + admin CRUD page (`SimfDataGrid` grid + inline `SimfModal` form + native `confirm()` delete) + public feed. |
| 2026-06-07 | D-353 | CRUD forms split into reusable `NewsAddEdit` + `NewsViewDelete` hosted by `CrudShell` (dialog / full page); `SimfConfirm`-gated soft-delete replaced the native `confirm()`; Page↔Popup toggle persisted in `localStorage` (`simf.cp.prefs.news`). |
| 2026-06-08 | D-357 | Cover image moved to the unified media-asset pipeline (`SimfImageUpload` / `NewsImage`); legacy `ImageRelativePath` retained. |
| 2026-06-10 | D-356 | Excel export + import added (toolbar Export/Import → `.xlsx`, sheet "News", header `Title | TitleArabic | Category | CategoryArabic | PublishedAt | DisplayOrder | IsActive`; import required headers add `Body`/`BodyArabic`; 5000-row cap; per-row errors). E2E catalogue extended with E2E-NWS-019…021. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 — Excel export + import + D-353 CrudShell delete + toggle).
