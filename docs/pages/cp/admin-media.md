# Media Center — `/admin/media`

| | |
|--|--|
| **Route** | `/admin/media` |
| **Audience** | Administrator (any admin holding `Media.View`) |
| **Auth** | `[RequirePermission(PermissionCatalog.Media.View)]` (page) + `RequireApprovedAccount` on every API endpoint + `RequireRateLimiting("auth")` on mutations |
| **Pattern** | D-199 media gallery (Mockup page 30) → D-256 SimfDataGrid → D-353 CrudShell framing + presentation toggle → D-356 Excel export + import |
| **Status** | ✅ Real |
| **Backend endpoints** | BFF `/account/api/admin/media/*` → API `/admin/media/*`: `POST /admin/media/list`, `GET /admin/media/{id}`, `POST /admin/media`, `PUT /admin/media/{id}`, `DELETE /admin/media/{id}`, `POST /admin/media/{id}/image` (multipart), and the D-356 `POST /admin/media/export` + `POST /admin/media/import` |
| **Source** | [`MediaList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/MediaList.razor), [`MediaAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/MediaAddEdit.razor), [`MediaViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/MediaViewDelete.razor), [`MediaEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/MediaEndpoints.cs), [`MediaExcelEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/MediaExcelEndpoints.cs), [`AdminMediaService`](../../../src/Backend/SIMF.Infrastructure/Media/AdminMediaService.cs), [`AdminMedia` contracts](../../../src/Shared/SIMF.Contracts/Media/AdminMedia.cs) |
| **Backed by** | `dbo.MediaItems` on `SimfAppDbContext` (D-199). The image bitmap is held **out-of-row** via `IMediaImageStorage` (D-90), never in the row. |
| **Tests** | [`docs/tests/e2e/cp-admin-media.md`](../../tests/e2e/cp-admin-media.md) |
| **Last reviewed** | 2026-06-11 |

> **Not the Media Library.** This page is the **public media gallery** admin
> (`MediaItem`, Mockup page 30). The separate **Media Library** at
> `/admin/media-library` (D-357, gated by `MediaLibrary.View`) is a different
> entity and is documented elsewhere.

## 1. Purpose

Admin CRUD over the public media gallery (SIMF Mockup page 30). Each row is a
**media item** that is one of two kinds:

- **Image** — has no playback `Url`; its bitmap is uploaded out-of-row via
  `POST /admin/media/{id}/image` once the row exists.
- **Video** — carries an external playback `Url` (required); no bitmap is
  attached.

Each item carries a bilingual **Title** and **Album**, a numeric **Display
order**, and an **Active** flag (soft-delete). The grid feeds the public
gallery surface; deactivated items drop out of the public list.

## 4. UI

- `SimfBanner` titled "Media Center" / "مركز الوسائط" (`Admin.Media.Title`) over
  the owner-mandated `SimfDataGrid` (D-256 — server-paged, sortable, per-column
  filterable, multiselect with select-all + per-row icon actions).
- **Grid columns:** Kind, Title (English), Title (Arabic), Album (English),
  Album (Arabic), Has image (on/off pill), Display order, Active (on/off pill).
  Empty text values render "—".
- **Sortable columns:** Kind, Title (English), Display order, Active. Title
  (Arabic), Album (English), Album (Arabic) and Has image are not sortable.
- **Filterable columns:** Title (English), Title (Arabic), Album (English),
  Album (Arabic). Each maps to a server-side `Contains` filter key
  (`title`, `titlearabic`, `album`, `albumarabic`); unknown column keys are
  ignored server-side.
- **Default order:** Display order ascending, then CreatedAt descending.
- Empty list renders `SimfEmptyState` ("No media items yet" / `Admin.Media.None`).
- **CRUD forms (D-353):** Add / Edit / View / Delete are hosted by `CrudShell`,
  which frames the reusable `MediaAddEdit` and `MediaViewDelete` forms as a
  popup **or** a full page per the admin's toolbar choice. The earlier inline
  `SimfModal` form and native `confirm()` delete are gone.
- **Image create is a two-step flow.** An Image create does **not** close the
  shell — `MediaAddEdit` flips into Edit mode in place so the bitmap can be
  attached (the upload control only renders in Edit mode for an Image item). A
  Video create is complete on save and closes the shell.
- **Page ↔ Popup presentation toggle (D-353):** the toolbar
  `CrudPresentationToggle` (`PageKey="media"`) hosts the forms as a dialog or a
  full page; the choice persists in `localStorage` under `simf.cp.prefs.media`
  via `CpPreferences` and is restored on load (`OnInitializedAsync`). In
  full-page mode the grid + banner are hidden while a form is open
  (`GridHidden`).
- **Excel export + import (D-356):** the grid toolbar carries **Export** and
  **Import** actions wired to the shared `CrudGridExcel` component
  (`Resource="media"`). See §5.

## 4.5 Form fields

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Kind | yes | — | `Image` or `Video`; locked once the row exists (disabled in Edit) |
| Title (English) | no | 200 | optional |
| Title (Arabic) | no | 200 | optional |
| Album (English) | no | 200 | optional |
| Album (Arabic) | no | 200 | optional |
| Video URL | yes for Video | 2048 | required only when Kind = Video; shown only for Video |
| Display order | yes | n/a | integer ≥ 0 (UI input `min=0 max=99999`) |
| Active | (Edit only) | bool | — |
| Image file | (Image, Edit only) | ≤ 10 MB | `image/png,image/jpeg,image/webp`; uploaded out-of-row after the row exists |

Server-side lengths mirror `MediaItemConfiguration.HasMaxLength` (Title /
TitleArabic / Album / AlbumArabic = 200, Url = 2048). Blank text fields are
normalised to `null` and trimmed on write.

## 5. Data flow + endpoints

Every page call goes through the CP BFF (`/account/api/admin/media...` in
`AccountEndpoints.cs`), which forwards to the API `/admin/media...` endpoints
with the bearer token.

| Action | Method + route (API) | Permission | Request → Response |
|--------|----------------------|------------|--------------------|
| List (grid) | `POST /admin/media/list` | `Media.View` | `GridQuery` → `ApiResult<GridPage<AdminMediaSummary>>` |
| Get detail | `GET /admin/media/{id}` | `Media.View` | → `ApiResult<AdminMediaDetail>` (Edit/View pre-fill) |
| Create | `POST /admin/media` | `Media.Create` | `AdminCreateMediaRequest` → `ApiResult<AdminMediaDetail>` |
| Update | `PUT /admin/media/{id}` | `Media.Edit` | `AdminUpdateMediaRequest` → `ApiResult<AdminMediaDetail>` |
| Soft-delete | `DELETE /admin/media/{id}` | `Media.Delete` | → `ApiResult<bool>` (`true`) |
| Upload image | `POST /admin/media/{id}/image` | `Media.Edit` | multipart `File` → `ApiResult<AdminMediaDetail>` |
| Export | `POST /admin/media/export` | `Media.Export` | `AdminGridExportRequest { Ids, Query }` → `.xlsx` binary |
| Import | `POST /admin/media/import` | `Media.Import` | multipart `file` (`.xlsx`) → `ApiResult<AdminGridImportResult>` |

The BFF export/import proxies are registered by the generic
`MapGridExcel(group, "media")` line (D-356), so the CP route is
`/account/api/admin/media/export` + `/account/api/admin/media/import`.

**Excel export.** Export posts `AdminGridExportRequest { Ids, Query }` — the
selected row ids if any are ticked, otherwise the current filtered `GridQuery`
(the page sends `_query` only when no rows are selected, per `OnExportAsync`).
The downloaded workbook is `simf-media-{yyyyMMddHHmmss}.xlsx`, sheet **"Media"**,
with the header row:

```
Kind | Title | TitleArabic | Album | AlbumArabic | HasImage | Url | DisplayOrder | IsActive
```

The image bitmap is **never** exported (it is held out-of-row); the workbook
carries metadata + the external `Url` only. The whole-grid export is capped at
**5000 rows**.

**Excel import (insert-only).** Import uploads an `.xlsx` whose sheet is named
**"Media"** with the single required header `Kind`. Each row creates one media
item via `AdminMediaService.CreateAsync` (metadata only — no bitmap rides the
workbook). The row key echoed in the per-row error list is the row's `Title`,
falling back to `Kind`. Caps + defence (in the shared `AdminGridImportEndpoint`):

- Max upload **5 MB** → HTTP 413 `admin_import_empty` if exceeded.
- Empty file → `DataValidationException` ("An Excel file is required.").
- ZIP-magic check (the four `.xlsx` bytes) → `DataValidationException`
  ("The file is not a valid Excel workbook.") on a non-workbook upload.
- Wrong/missing worksheet or missing required header → rejected by the parser.
- Max **5000** data rows.
- A bad row never aborts the batch: each row is applied in its own try/catch and
  failures are aggregated into the per-row error list.

The result modal shows `Grid.Import.ResultBody` ("{0} created, {1} updated,
{2} skipped.") plus the per-row error list; on success the page raises
`OnImported` → the `Grid.Import.Done` toast ("Import complete.") and reloads the
grid.

## 6. Validation + error handling

Server-side validation lives in `AdminMediaService.Validate` (applied on Create
and Update) and the image-upload endpoint:

- **Invalid kind** (`Enum.IsDefined` fails) → 400 `media_invalid`
  ("Media kind is invalid." / "نوع الوسائط غير صالح.").
- **Negative display order** → 400 `media_invalid`
  ("Display order must be zero or a positive integer.").
- **Over-length field** → 400 `media_invalid` naming the field + the max.
- **Video without URL** → 400 `media_invalid`
  ("A video media item requires a URL." / "يتطلّب عنصر الفيديو رابطاً.").
  The form also guards this client-side in `SaveAsync` (no POST fires) with a
  descriptive bilingual message before the request leaves the browser.
- **Not found** (Get / Update / Delete / image upload on an unknown id) → 404
  `media_not_found` ("The media item was not found." /
  "لم يتم العثور على عنصر الوسائط.").
- **Image upload — empty file** → 400 `validation_failed`
  ("No file was uploaded." / "لم يتم رفع أي ملف.").
- **Image upload — over 10 MB** → 400 `validation_failed`
  ("Image must be 10 MB or smaller." / "يجب أن تكون الصورة 10 ميجابايت أو أقل.").
- Import per-row failures (e.g. missing/invalid kind, Video without URL) raise a
  `DataValidationException` that is recorded in the per-row error list rather
  than aborting the batch.

The page surfaces a `SimfAlert` toast; on an envelope error it shows the
server's bilingual message, falling back to `Admin.Media.LoadFailed` (and
`Admin.Media.Image.UploadFailed` for the upload path).

## 7. Edge cases + known limitations

- **No unique business key → no 409/duplicate path.** Unlike `Speaker.Code`, a
  media item has no unique key, so two items can share a title/album. The
  negative paths are video-URL-required, image-upload size/empty, and the 404
  missing-item.
- **Deactivate is idempotent.** Deleting an already-inactive item returns 200
  with no second audit row (`DeactivateAsync` early-returns when `IsActive` is
  already false). Delete is a **soft-delete** (`IsActive = false`), never a hard
  delete.
- **Image bytes are server-internal.** The detail/summary contracts expose only
  `HasImage` / `HasThumbnail` booleans; the raw relative paths are not surfaced,
  and there is no embeddable public image-preview endpoint, so View shows a
  presence indicator rather than a thumbnail.
- **Kind is fixed after create.** The Kind select is disabled in Edit mode (the
  bitmap-vs-URL distinction is structural).
- **Import is insert-only.** A workbook only creates rows; it never updates or
  deletes existing items, and it cannot attach a bitmap.

## 8. i18n + RTL

`Admin.Media.*` keys cover the page, grid headers, form labels, kind labels,
toasts and the delete confirmation; shared `Grid.*` keys cover the toolbar,
pager, export/import labels and the import-result modal. EN ↔ AR parity is
maintained (banner "Media Center" / "مركز الوسائط"). The page mirrors RTL under
the Arabic locale (grid, forms, and CrudShell frame).

## 10. Use cases

- UC-MED-CREATE-IMAGE-001 (create Image → attach bitmap), UC-MED-CREATE-VIDEO-001
  (create Video with URL), UC-MED-EDIT-001, UC-MED-DEACTIVATE-001,
  UC-MED-EXPORT-001, UC-MED-IMPORT-001.

## 11. E2E

See [`docs/tests/e2e/cp-admin-media.md`](../../tests/e2e/cp-admin-media.md):
E2E-MED-001 golden Image round-trip, 002 add Video, 003 edit, 004 soft-delete
with confirm, 005 image upload, 006 kind toggle, 007 empty state, 008 auth gate,
009 Video-URL-required, 010 image-upload rejected, 011 missing-item 404, 012
server-500, 013 delete cancelled, 014 RTL, 015 per-column filter, 016 column
sort, 017 presentation toggle persists (D-353), 018 full-page round-trip (D-353),
019 delete confirmation gate (D-353), 020 Excel export (D-356), 021 Excel import
(D-356), 022 Excel import rejection (D-356).

## 12. Related docs

- E2E catalogue: `docs/tests/e2e/cp-admin-media.md`.
- Authority spec: SIMF Mockup page 30 (public media gallery).
- Decisions: D-199 (module), D-256 (SimfDataGrid migration), D-353 (CrudShell +
  presentation toggle), D-356 (Excel export + import), D-90 (out-of-row image
  storage).
- Sibling modules: Media Partners (`/admin/media-partners`), News
  (`/admin/news`), Media Library (`/admin/media-library`, D-357 — distinct).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-11 | D-356 / D-353 | First reference doc for the Media Center page. Documents the SimfDataGrid CRUD surface, the D-353 CrudShell framing + Page↔Popup presentation toggle, the two-step out-of-row image upload, and the D-356 Excel export (sheet "Media", header `Kind \| Title \| TitleArabic \| Album \| AlbumArabic \| HasImage \| Url \| DisplayOrder \| IsActive`) + insert-only import (required header `Kind`, 5 MB / 5000-row caps, per-row error aggregation). |

_Last reviewed:_ 2026-06-11 by Claude (D-356 — Media Center reference doc: Excel export + import + D-353 toggle).
