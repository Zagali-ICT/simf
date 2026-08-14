# News — `/admin/news`

| | |
|--|--|
| **Route** | `/admin/news` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator + the `PublicRelations` role (admins holding the `News.*` permissions) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.News.View)]` (page) + per-action API policies (`News.Create` / `Edit` / `Delete` / `Export` / `Import`) + `RequireApprovedAccount` |
| **Pattern** | D-199 event-module CRUD on the uniform CRUD shell — **D-353** `CrudShell` Add/Edit + View/Delete with the Page ↔ Popup `CrudPresentationToggle`, **D-356** Excel export + import via `CrudGridExcel`, **D-357** media-asset image |
| **Status** | ✅ Real (D-199; D-353 toggle/CrudShell + D-356 Excel, 2026-06-10; D-357 media-asset image, 2026-06-11) |
| **Implements use case(s)** | Admin maintenance of the public News feed (Mockup screen 29 / 29b) per SIMF-FDS-004 / D-199 |
| **Backend endpoints** | `POST /account/api/admin/news/list`, `GET /account/api/admin/news/{id}`, `POST /account/api/admin/news`, `PUT /account/api/admin/news/{id}`, `DELETE /account/api/admin/news/{id}`, `POST /account/api/admin/news/export`, `POST /account/api/admin/news/import` |
| **Source file** | [`NewsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/NewsList.razor), [`NewsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/NewsAddEdit.razor), [`NewsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/NewsViewDelete.razor) |
| **Tests** | [`docs/tests/e2e/cp-admin-news.md`](../../tests/e2e/cp-admin-news.md); API: `tests/SIMF.Api.Tests/NewsTests.cs`, `tests/SIMF.Api.Tests/NewsExcelTests.cs` |
| **Last reviewed** | 2026-06-11 |

---

## 1. Purpose

The public News feed (Mockup screen 29) lists event news articles — title,
category, teaser excerpt and image — sorted by publish date then display order.
Opening a card shows the full article (Mockup screen 29b). This Control Panel
page is where an administrator (or a `PublicRelations` role member) maintains
that feed: add an article, set its bilingual title / category / excerpt / body,
an image (relative-path string and/or a managed media asset), publish date and
display order, toggle the active flag, and soft-delete (deactivate) an article so
it drops off the public feed. Unlike most CP list pages, the **admin grid lists
every row regardless of `IsActive` / publish window** so editors can manage drafts
and reactivate soft-deleted items.

D-353 moved every form onto the uniform `CrudShell` (popup or full page, per the
admin's saved preference) and replaced the old inline `SimfModal` form + native
`confirm()` delete with a `SimfConfirm`-gated View/Delete form. D-356 added Excel
export and import. D-357 added a "News image" upload wired to the unified
media-asset pipeline (asset category `NewsImage`).

## 2. Audience + permissions

- **Who can reach it:** any admin whose role grants `News.View` (the
  `PublicRelations` role holds the whole `News.*` baseline; `Administrator` holds
  it via the wildcard `"*"`). The page is gated by
  `@attribute [RequirePermission(PermissionCatalog.News.View)]`.
- **Who can edit/write on it:** the action buttons are **not** individually wrapped
  in `<AuthorizedAction>`, so any admin who can open the page sees Add / Edit /
  Delete / Export / Import. The finer-grained gate is enforced **API-side**:
  - List / Get → `News.View`
  - Create → `News.Create`
  - Edit → `News.Edit`
  - Delete → `News.Delete`
  - Export → `News.Export`
  - Import → `News.Import`
- **Authorisation gates:** each API endpoint declares
  `Policies(PermissionCatalog.PolicyFor(<code>), nameof(AuthorizationPolicies.RequireApprovedAccount))`;
  the mutating endpoints (Create / Update / Delete) also `RequireRateLimiting("auth")`.
- **What an unauthenticated / under-privileged user sees:** an admin lacking
  `News.View` is routed to `/not-permitted` and the `/list` call never fires; an
  admin with View but not (say) Create gets HTTP 403 on the underlying POST.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-news-crud-before.png` | _pending_ |
| Empty state | `docs/screenshots/cp-admin-news-empty.png` | _pending_ |
| Add (modal) | `docs/screenshots/cp-admin-news-add-modal.png` | _pending_ |
| Edit (modal) | `docs/screenshots/cp-admin-news-edit-modal.png` | _pending_ |
| View/Delete + SimfConfirm | `docs/screenshots/cp-admin-news-crud-after.png` | _pending_ |
| Import result modal | `docs/screenshots/cp-admin-news-import-result.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/cp-admin-news-rtl.png` | _pending_ |

## 4. UI affordances

### 4.1 Banner / page header
`SimfBanner` with the title `Admin.News.Title` ("News" / "الأخبار"). The banner +
grid are wrapped in `simf-page-wide` / `simf-surface`. When a form is open in
**full-page** mode the banner + grid are hidden (`GridHidden`); in popup mode they
stay behind the dialog. A `SimfAlert` toast (success / error) renders above the grid.

### 4.2 Toolbar (`SimfDataGrid`)
| Button | Wired callback | Calls | Notes |
|--------|----------------|-------|-------|
| Select all | grid `Multiselect="true"` | — | mandatory per the list-page standard |
| Add | `OnAddAsync` | opens `NewsAddEdit` (Create) in `CrudShell` | no detail fetch (Add starts blank) |
| Edit | `OnEditAsync` | GET `/{id}` then opens `NewsAddEdit` (Edit) | loads full detail first (summary omits body/excerpt/image) |
| Details | `OnDetailsAsync` | GET `/{id}` then opens `NewsViewDelete` read-only | `IsDelete=false`, no Delete button |
| Delete | `OnDeleteAsync` | GET `/{id}` then opens `NewsViewDelete` delete mode | `IsDelete=true`, Delete gated by `SimfConfirm` |
| Export | `OnExportAsync` | `POST /admin/news/export` via `_excel.ExportAsync` | selected ids, else whole filtered grid |
| Import | `OnImportAsync` | `_excel.TriggerImportAsync()` → file picker → `POST /admin/news/import` | insert-only |
| **Presentation toggle** | `CrudPresentationToggle` (`PageKey="news"`) | persists to `localStorage` | Page ↔ Popup (D-353) |

`CrudGridExcel @ref="_excel" Resource="news"` is rendered below the grid; it owns
the hidden import file input, fires `OnImported` → `OnImportedAsync` (success toast
+ reload) and `OnError` → `OnExcelError` (error toast).

### 4.3 Grid columns
| Column | Source field | Key | Sortable | Filterable | Notes |
|--------|--------------|-----|----------|------------|-------|
| Title (English) | `r.Title` | `title` | yes | yes | |
| Title (Arabic) | `r.TitleArabic` | `titlearabic` | no | yes | |
| Category (English) | `r.Category` | `category` | no | yes | |
| Category (Arabic) | `r.CategoryArabic` | `categoryarabic` | no | yes | |
| Publish date | `r.PublishedAt` | `publishedat` | yes | no | rendered `yyyy-MM-dd` |
| Display order | `r.DisplayOrder` | `displayorder` | yes | no | |
| Active | `r.IsActive` | `isactive` | no | no | `SimfPill` on/off (Active / Inactive) |

Empty list renders `SimfEmptyState` with `Admin.News.None`
("No news articles yet." / "لا توجد أخبار بعد.").

> The default sort (no `Sort` key, or any unrecognised key) is `PublishedAt`
> descending then `DisplayOrder` ascending. The `publishedat` ascending branch is
> the only explicit ascending case; `title` and `displayorder` honour both directions.

### 4.4 Pager
Standard `SimfDataGrid` pager — First / Prev / Next / Last + page-size selector,
caption "Showing X–Y of Z" (`Admin.News.Summary` via `FormatSummary`). The page
default is `Top = 20` (the grid `GridQuery`); the service clamps an out-of-range
`Top` to 1–200 (defaulting to 25 when `Top <= 0`).

### 4.5 Form fields (`NewsAddEdit`)
| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| Title (English) | text | yes | 200 | 1–200 chars (server `NEWS_INVALID`) | `Admin.News.Field.TitleEn` |
| Title (Arabic) | text | yes | 200 | 1–200 chars | `Admin.News.Field.TitleAr` |
| Category (English) | text | yes | 100 | 1–100 chars | `Admin.News.Field.CategoryEn` |
| Category (Arabic) | text | yes | 100 | 1–100 chars | `Admin.News.Field.CategoryAr` |
| Excerpt (English) | textarea (2 rows) | no | 500 | ≤500 chars | `Admin.News.Field.ExcerptEn` |
| Excerpt (Arabic) | textarea (2 rows) | no | 500 | ≤500 chars | `Admin.News.Field.ExcerptAr` |
| Body (English) | textarea (6 rows) | yes | 8000 | 1–8000 chars | `Admin.News.Field.BodyEn` |
| Body (Arabic) | textarea (6 rows) | yes | 8000 | 1–8000 chars | `Admin.News.Field.BodyAr` |
| Publish date | `<input type="date">` | yes | — | parsed `AssumeUniversal`/`AdjustToUniversal`; defaults to today (`UtcNow`) | `Admin.News.Field.PublishedAt` |
| Display order | `<input type="number">` | yes | min 0, max 99999 | integer ≥ 0 | `Admin.News.Field.DisplayOrder` |
| Active | checkbox | Edit only | — | bool (Create always sets `IsActive = true`) | `Admin.News.Field.IsActive` |
| **News image** (media asset) | `SimfImageUpload` | Edit only | — | upload / external link via the media-asset pipeline (D-357) | `Admin.Asset.Heading` |

The form runs Create (`POST`) when `IsEdit=false` and Edit (`PUT` against
`Initial.Id`) when `IsEdit=true`; only Edit shows the Active checkbox **and** the
media-asset image control (the row must exist before bytes can be attached). Blank
required fields (title / body / category, EN+AR) are guarded client-side before any
request (`Admin.News.RequiredFields`). Blank optional fields are sent as `null`
(`NullIfBlank`).

> **There is now one image, not two.** The free-text **Image path**
> (`ImageRelativePath`) that used to sit beside the media asset is gone (D-889),
> and with it the question of which one a reader should believe. The article's
> typed `ImageFileId` points at the `StoredFile` the upload control creates, and
> the Details/Delete view renders that one thumbnail.

### 4.6 News image (media-asset pipeline, D-357)
- **Add/Edit:** in Edit mode (`IsEdit && Initial is not null`) the form renders
  `<SimfImageUpload Category="NewsImage" OwnerId="@Initial.Id" Alt="@_model.Title" />`
  under the `Admin.Asset.Heading` label ("Image" / "الصورة"). It supports an
  uploaded file or an external link, served through the unified
  `/account/api/admin/assets/NewsImage/{ownerId}/image` surface and listed in
  `/admin/media-library` as `NewsImage`.
- **Details/Delete:** `NewsViewDelete` renders the thumbnail
  `<SimfImageThumb Src="/account/api/admin/assets/NewsImage/{Initial.Id}/image" Alt="@Initial.Title" Class="simf-img-thumb--lg" />`
  above the read-only `<dl>`.

### 4.7 View / Delete form (`NewsViewDelete`)
Media-asset thumbnail (§4.6) then a read-only `<dl>` of Title (En/Ar), Category
(En/Ar), Excerpt (En/Ar — "—" when blank), Body (En/Ar), Publish
date, Display order and Active. In delete mode a red Delete button opens a
`SimfConfirm` (Danger) whose message is `Admin.News.Delete.Message` formatted with
the article's English title; only the confirm fires `DELETE`. The old inline list
`confirm()` was removed in D-353.

## 5. Data flow

```
Admin action → NewsList handler → JS interop (simfAccount.{post,get,put,delete}Json)
            → BFF /account/api/admin/news/* → API /api/v1/admin/news/*
            → IAdminNewsService / Excel endpoints → SIMF_App DB
            → ApiResult<T> envelope → grid reload + toast
```

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| OnInit / query change | `POST /account/api/admin/news/list` | `GridQuery` | `ApiResult<GridPage<AdminNewsSummary>>` |
| Edit / Details / Delete click | `GET /account/api/admin/news/{id}` | — | `ApiResult<AdminNewsDetail>` |
| Add save | `POST /account/api/admin/news` | `CreateNewsRequest` | `ApiResult<AdminNewsDetail>` |
| Edit save | `PUT /account/api/admin/news/{id}` | `UpdateNewsRequest` | `ApiResult<AdminNewsDetail>` |
| Confirm delete | `DELETE /account/api/admin/news/{id}` | — | `ApiResult<bool>` |
| Export | `POST /account/api/admin/news/export` | `AdminGridExportRequest { Ids, Query }` | XLSX binary |
| Import | `POST /account/api/admin/news/import` | multipart `file` (.xlsx) | `ApiResult<AdminGridImportResult>` |

Edit / Details / Delete always re-fetch the **detail** before opening a form
because the grid summary (`AdminNewsSummary`) omits the body / excerpt / image —
editing from a summary-only model would lose those fields.

### 5.1 Excel export columns
`ExportNewsEndpoint` writes a sheet named **"News"** with header row
`Title | TitleArabic | Category | CategoryArabic | PublishedAt | DisplayOrder | IsActive`
(it mirrors the lighter grid summary, so the long-form body / excerpt / image
columns are **not** exported). File name: `simf-news-{timestamp}.xlsx`. With
selected rows the export honours `AdminGridExportRequest.Ids`; with none, it
exports the whole filtered set (`Query`). The shared base caps the export at 5000
rows.

### 5.2 Excel import
`ImportNewsEndpoint` is **insert-only**. Required headers:
`Title`, `TitleArabic`, `Body`, `BodyArabic`, `Category`, `CategoryArabic` (the body
columns — omitted from the export — are required on import because the create
contract demands them). Each row binds to `CreateNewsRequest`; a blank required
cell raises a per-row `DataValidationException` (bilingual), not a batch abort. A
**duplicate English title** is rejected per-row by the service (409
`NEWS_TITLE_DUPLICATE`). Optional cells (`Excerpt`, `ExcerptArabic`) are sent as
`null` when blank; `PublishedAt` defaults to
`UtcNow` when unparseable; `DisplayOrder` defaults to `0`. The result
`AdminGridImportResult { Created, Updated, Skipped, Errors[] }` drives the modal,
and the success toast is the shared `Grid.Import.Done` key.

## 6. Validation + error handling

- **Client-side guards:** `NewsAddEdit.HandleSubmitAsync` blocks the request when
  any of Title / Body / Category (English **or** Arabic) is blank and shows
  `Admin.News.RequiredFields`
  ("Please fill in the required fields: title, body and category (English and Arabic)." /
  "يرجى تعبئة الحقول المطلوبة: العنوان والمحتوى والتصنيف (بالإنجليزية والعربية).").
- **Server-side validation** runs twice: `CreateNewsValidator` / `UpdateNewsValidator`
  (FluentValidation, lengths mirror `NewsConfiguration.HasMaxLength`) and again in
  `AdminNewsService.Validate` (`RequireText` / `OptionalText`) so the bounds hold even
  for non-HTTP callers. Trims fields; Title 1–200; Category 1–100; Body 1–8000;
  Excerpt ≤500; DisplayOrder ≥ 0. Length / required / order
  failures throw `ApiException(ErrorCodes.NewsInvalid, 400, …)` (`"NEWS_INVALID"`).
- **Duplicate guard:** a news article with the same **English title** (case-insensitive
  on update) → `ApiException(ErrorCodes.NewsTitleDuplicate, 409, …)`
  (`"NEWS_TITLE_DUPLICATE"`):
  "A news article with the English title '{title}' already exists." /
  "يوجد خبر بالعنوان الإنجليزي '{title}' بالفعل." On Update the check fires only when
  the English title actually changes.
- **Not found:** `GET` against a missing id throws the generic
  `ErrorCodes.NotFound` (404) — "The news article was not found." /
  "لم يتم العثور على الخبر."; `PUT` / `DELETE` against a missing id throw
  `ErrorCodes.NewsNotFound` (`"NEWS_NOT_FOUND"`, 404) with the same message text.
- **Error envelope:** standard `ApiResult<T>.Error` with `Code` + bilingual
  `Message` / `MessageArabic`; the forms surface `MessageForCurrentCulture()`.
- **Toast strategy:** success → `Admin.News.Saved` ("News article saved." /
  "تم حفظ الخبر.") / `Admin.News.Deleted` ("News article deleted." / "تم حذف الخبر.")
  (green) and `Grid.Import.Done` after import; load / save failure falls back to
  `Admin.News.LoadFailed` ("Could not complete the request. Please try again."); the
  list-load failure surfaces the same key when the envelope carries no error.
  Form-level errors render in the form's own `SimfAlert`.

## 7. Edge cases + known limitations

- **Admin grid lists every row.** The admin list returns rows regardless of
  `IsActive` / publish window (drafts + soft-deleted), so "Delete" flips the Active
  column to "—" rather than removing the row.
- **Soft-delete only + idempotent.** `DELETE` deactivates (`IsActive=false`);
  `DeactivateAsync` returns early (no audit row) when the article is already
  inactive. The row drops off the public feed immediately but stays in the admin grid.
- **Reactivate via Edit.** A soft-deleted article is recovered by re-ticking the
  Active checkbox in Edit and saving (`IsActive = true`).
- **Detail re-fetch before every form** so the body / excerpt / image are never lost
  when editing from the summary-only grid.
- **One image, managed in one place.** The `ImageRelativePath` free-text field is
  gone (D-889); the `NewsImage` media asset is the article's only image, and the
  workbook carries no image column at all.
- **Import never sets the media asset** (it binds only the `CreateNewsRequest`
  columns); attach the managed image afterwards via Edit.
- **Import is insert-only** — there is no upsert, so re-importing a workbook whose
  English title already exists yields a per-row 409 error.
- **Action buttons are not `<AuthorizedAction>`-gated** — an admin with View but not
  Create/Edit/Delete/Export/Import sees the buttons, but the API rejects the call
  (403). Treat that as the per-action enforcement point.
- **`GET` vs `PUT`/`DELETE` not-found codes differ** — a missing-id `GET` returns the
  generic `NOT_FOUND`, while `PUT`/`DELETE` return `NEWS_NOT_FOUND` (same HTTP 404 and
  message text).

## 8. i18n + RTL

All visible strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR) via
`IStringLocalizer<Strings> L`. Banner title `الأخبار`; grid headers
"العنوان (إنجليزي)", "العنوان (عربي)", "التصنيف (إنجليزي)", "التصنيف (عربي)",
"تاريخ النشر", "الترتيب", "نشط" (per the E2E catalogue). The language toggle in the
top header sets `<html dir="rtl" lang="ar">`; the nav rail mirrors, the toolbar +
pager reverse, and the `CrudShell` form mirrors. Server-side error messages and the
toasts are bilingual (`Message` / `MessageArabic`), surfaced via
`MessageForCurrentCulture()`.

## 9. Accessibility

- Keyboard: the `CrudShell` traps focus while a form is open and restores it on
  close; `SimfConfirm` requires an explicit Confirm/Cancel choice.
- Screen reader: `SimfDataGrid` exposes a `Caption` (`Admin.News.Title`) and per-row
  labels (`RowLabel = Title`); select-all / per-row select have labels; sortable
  headers expose `aria-sort`.
- Colour contrast: WCAG AA via `theme.tokens.css`; active/inactive use the `SimfPill`
  on/off variants, not colour alone (text "Active"/"Inactive").
- Focus indicators: the `--focus-ring` token on every focusable element.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| (D-199 News) | Maintain public News feed | Mockup screen 29 / 29b / SIMF-FDS-004; UCS detail entry to be authored |

## 11. Related E2E test scenarios

| Scenario | File | Coverage |
|----------|------|----------|
| Full CRUD round-trip | [`cp-admin-news.md`](../../tests/e2e/cp-admin-news.md) | E2E-NWS-001 |
| Empty list | same | E2E-NWS-002 |
| Auth gate (page) | same | E2E-NWS-003 |
| Add modal field set / Edit pre-fill + Active | same | E2E-NWS-004/005 |
| Delete confirm gate (CrudShell + SimfConfirm) | same | E2E-NWS-006, E2E-NWS-018 |
| Client / server validation / duplicate (409) | same | E2E-NWS-007/008/009 |
| Publish date + Display order round-trip | same | E2E-NWS-010 |
| Reactivate a soft-deleted article | same | E2E-NWS-011 |
| Server 500 on list → fallback toast | same | E2E-NWS-012 |
| RTL render | same | E2E-NWS-013 |
| Per-column filter / column sort | same | E2E-NWS-014/015 |
| Presentation toggle persists (D-353) | same | E2E-NWS-016 |
| Full-page round-trip (D-353) | same | E2E-NWS-017 |
| Excel export (D-356) | same | E2E-NWS-019 |
| Excel import + import rejection (D-356) | same | E2E-NWS-020/021 |
| Image via the media-asset pipeline (D-357) | same | E2E-NWS-022 |

## 12. Related docs

- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) (CRUD pages).
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `ApiResult<T>` envelope + admin grid endpoints.
- Permissions: [`SIMF-Auth-Permissions-Dev-Guide.md`](../../manuals/SIMF-Auth-Permissions-Dev-Guide.md), [`SIMF-Permission-Catalogue.md`](../../SIMF-Permission-Catalogue.md).
- Decisions log: D-199 (News module), D-353 (uniform CrudShell + Page/Popup toggle +
  SimfConfirm delete), D-356 (Excel export/import), D-357 (media-asset pipeline) in
  [`DECISIONS_LOG.md`](../../decisions/DECISIONS_LOG.md).
- Source: `NewsList.razor`, `NewsAddEdit.razor`, `NewsViewDelete.razor`,
  `AdminNewsEndpoints.cs`, `NewsExcelEndpoints.cs`, `AdminNewsService.cs`,
  `News.cs` (contracts + domain entity).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-08-14 | D-889 | **The free-text Image path is gone; the article's image is a typed key.** `News.ImageRelativePath` becomes `Guid? ImageFileId` with a real foreign key into `StoredFiles`. The Add/Edit form loses the text field and gains the save-first hint on create; the workbook loses its image column; `AssetService` now points `ImageFileId` at the article's active file on upload, link, deactivate and restore. Ends the "two image paths coexist" split this doc used to describe. |
| 2026-06-11 | D-357 | News **image** wired to the unified media-asset pipeline: `<SimfImageUpload Category="NewsImage" OwnerId="@Initial.Id" Alt="@_model.Title" />` on the Add/Edit form (Edit only) + a `<SimfImageThumb Src="/account/api/admin/assets/NewsImage/{Initial.Id}/image" …>` on Details/Delete. Coexists with the existing free-text `ImageRelativePath` field. E2E catalogue covers it as E2E-NWS-022. |
| 2026-06-10 | D-356 / D-353 | Reference doc created. Documents the D-353 `CrudShell` Add/Edit + View/Delete forms with the Page ↔ Popup `CrudPresentationToggle` (PageKey `news`) and the `SimfConfirm`-gated delete (replacing the old inline `SimfModal` + native `confirm()`), plus the D-356 Excel export (`POST /export`) and insert-only import (`POST /import`) via `CrudGridExcel`. |
| 2026-06-02 (orig) | D-199 | News admin CRUD shipped (Mockup screen 29 / 29b). |

---

**2026-07-14 (D-357):** the Title column now renders the article's image thumbnail
via the shared `SimfIdentityCell` (`AdminNewsSummary.HasImage`, streamed from the
`NewsImage` /assets proxy) or a tinted initials tile. Column key unchanged so
server-side sort/filter is unaffected. E2E-NWS-024.

_Last reviewed:_ 2026-07-14 by Claude (D-357 — news image thumbnail in the list). Prior: 2026-06-11 by Claude (D-357 media-asset pipeline).
