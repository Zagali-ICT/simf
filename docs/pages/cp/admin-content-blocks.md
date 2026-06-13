# Content blocks (editable site copy) — `/admin/content-blocks`

| | |
|--|--|
| **Route** | `/admin/content-blocks` |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.ContentBlocks.View)]` on the page; API gated per-action (`ContentBlocks.View` / `.Edit` / `.Delete` / `.Export` / `.Import`) + `RequireApprovedAccount`; mutations + Excel routes `RequireRateLimiting("auth")` |
| **Pattern** | D-173 dynamic CMS · D-255 SimfDataGrid migration · D-353 CrudShell framing + presentation toggle · D-356 Excel export + import |
| **Status** | ✅ Real |
| **Backend endpoints** | BFF `/account/api/admin/content-blocks/*` → API. `POST /admin/content-blocks/list`, `GET /admin/content-blocks/{key}`, `PUT /admin/content-blocks` (upsert), `DELETE /admin/content-blocks/{key}`, `POST /admin/content-blocks/export`, `POST /admin/content-blocks/import` |
| **Source** | [`ContentBlocksList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContentBlocksList.razor), [`ContentBlockAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContentBlockAddEdit.razor), [`ContentBlockViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContentBlockViewDelete.razor), [`CmsEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/CmsEndpoints.cs), [`ContentBlocksExcelEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/ContentBlocksExcelEndpoints.cs), [`AdminCmsService`](../../../src/Backend/SIMF.Infrastructure/Cms/AdminCmsService.cs), [`ContentBlock`](../../../src/Backend/SIMF.Domain/Cms/ContentBlock.cs) |
| **Backed by** | `dbo.ContentBlocks` table (migration `AddCms`, 2026-05-29; D-199 CMS wave). |
| **Tests** | [`docs/tests/e2e/cp-admin-content-blocks.md`](../../tests/e2e/cp-admin-content-blocks.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

Dynamic-CMS admin surface (D-173, gap doc G8, PDF §1, §2.1): editable
key/value text blocks — welcome message, page copy, labels, and the
well-known `cyber.*` policy text the Flutter app reads — that are surfaced
on the public Website and the mobile app. Each block is a stable **Key**
slug (lower-kebab-case dotted hierarchy, e.g. `home.welcome.title`) plus
bilingual content (`Content` English, `ContentArabic` Arabic) and an active
flag. The text is edited at runtime from the Control Panel with **no code
change and no redeploy**.

The public read side consumes what this page writes via the anonymous
`GET /app/content/{key}` endpoint (with an `If-Modified-Since` handshake)
and `POST /app/content/batch`; inactive blocks are hidden from the public
read. Renaming a Key is a wire-breaking change because the client codes
against the slug.

## 4. UI

- `SimfBanner` title + the standard **`SimfDataGrid`** (D-255 — migrated
  from a raw table). The grid loads `new GridQuery { Top = 20 }`, so it
  shows up to **20 rows per page** with the standard prev/next/first/last
  pager.
- Grid columns: **Key** (rendered in `<code>`), **English** (`Content`,
  truncated to 80 chars + "…"), **Last updated** (`yyyy-MM-dd HH:mm UTC`),
  **Active** (`SimfPill` on/off).
- Sortable on **Key** (`key`), **English** (`content`) and **Last updated**
  (`lastUpdatedAt`); the Active column is not sortable. Per-column filter
  inputs on the **Key** and **English** columns.
- `Multiselect="true"` (select-all + per-row checkboxes); there is **no
  `CustomToolbar` bulk action** wired, so selection only feeds the Excel
  export's selected-ids path.
- Quiet per-row icon actions: **Edit** (pencil), **Details** (eye) and
  **Delete** (trash), plus a toolbar **Add** ("New block").
- **CrudShell framing (D-353):** Add / Edit / View / Delete are hosted by
  `CrudShell` as a popup or a full page per the toolbar toggle — not the
  old inline `SimfModal`. Add/Edit render `ContentBlockAddEdit`;
  Details/Delete render `ContentBlockViewDelete`. The grid summary already
  carries every field the forms need, so the row binds straight through —
  no detail-fetch.
- **Page ↔ Popup presentation toggle (D-353):** the toolbar
  `CrudPresentationToggle` (bound to `PageKey="content-blocks"`) lets the
  admin host the forms as a dialog or a full page; the choice persists in
  `localStorage` via `CpPreferences` (key `simf.cp.prefs.content-blocks`)
  and is restored in `OnInitializedAsync` via `Prefs.GetPresentationAsync`.
  Default is `CrudPresentation.Dialog`. In page mode the grid + banner are
  hidden (`GridHidden`) while a form is open.
- **Delete is no longer one-click (D-353):** the trash action opens
  `ContentBlockViewDelete` (`IsDelete=true`); its red Delete button raises
  a `SimfConfirm` (Danger=true) whose message interpolates the Key
  (`string.Format` of `Admin.ContentBlocks.Delete.Message`). Only on
  confirm does the soft-delete fire.
- **Excel export + import (D-356):** the toolbar carries **Export** and
  **Import** actions wired to a shared `<CrudGridExcel Resource="content-blocks">`.
  Export posts `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/content-blocks/export` — selected row ids when rows
  are ticked, else the current filtered grid — and downloads an `.xlsx`
  (`simf-content-blocks-{timestamp}.xlsx`, sheet **"ContentBlocks"**).
  Import posts an `.xlsx` (multipart) to
  `/account/api/admin/content-blocks/import`; on success a result modal
  reports "N created, N updated, N skipped" with a per-row error list, then
  the shared `Grid.Import.Done` toast fires and the grid reloads.

## 4.5 Form fields (`ContentBlockAddEdit`)

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Key | yes | 128 | 2–128 chars; trimmed + lower-cased server-side; disabled on Edit |
| Content (English) | (see note) | 8000 | ≤ 8000 chars server-side |
| Content (Arabic) | (see note) | 8000 | ≤ 8000 chars server-side |
| Active | (default ticked) | bool | — |

The razor only guards a present, ≤ 128-char Key before the PUT (`Admin.ContentBlocks.Required`);
every other bound is enforced server-side in `AdminCmsService.UpsertContentBlockAsync`.
On the **upsert PUT** the request defaults `Content`/`ContentArabic` to empty,
so the API does not reject an empty body — but the **Excel import** path
(`ImportContentBlocksEndpoint.ApplyRowAsync`) requires Key, Content and
ContentArabic to be non-blank per row.

## 5. Data flow + endpoints

The CP page calls the BFF passthroughs at `/account/api/admin/content-blocks/*`
(`AccountEndpoints.cs`), which forward with the admin's access token to the API
(`SimfAdminClient`). The export + import routes are registered by the shared
`MapGridExcel(group, "content-blocks")` helper. At the API:

| Endpoint | Permission | Notes |
|----------|------------|-------|
| `POST /admin/content-blocks/list` | `ContentBlocks.View` | `GridQuery` → `GridPage<AdminContentBlockSummary>` |
| `GET /admin/content-blocks/{key}` | `ContentBlocks.View` | single block; 404 `CONTENT_BLOCK_NOT_FOUND` if absent |
| `PUT /admin/content-blocks` | `ContentBlocks.Edit` | upsert by Key (create or update-in-place); rate-limited |
| `DELETE /admin/content-blocks/{key}` | `ContentBlocks.Delete` | soft-delete (deactivate); rate-limited |
| `POST /admin/content-blocks/export` | `ContentBlocks.Export` | `AdminGridExportRequest { Ids, Query }` → `.xlsx`; rate-limited |
| `POST /admin/content-blocks/import` | `ContentBlocks.Import` | multipart `.xlsx`; rate-limited |

**Upsert is keyed, not id-based.** The same `PUT /admin/content-blocks`
serves create and edit. The server normalises the Key (`Trim()` +
`ToLowerInvariant()`) and creates the row if absent or updates it in place
(same id) if present. Because the Key field is disabled while editing, a
key collision can only be reached from the New-block path — and it does
**not** error, it silently upserts onto the existing row.

The actor is read from the `sub` claim; on upsert/delete the row stamps
`LastUpdatedByUserId` (a bare logical FK to `SimfUser.Id` on the Identity
DB — resolved on read, never a cross-DB constraint per D-157). Upsert and
deactivate each write an audit entry (`ContentBlock.Upserted` /
`ContentBlock.Deactivated`, `Detail = "key=…"`).

### Excel export columns (sheet "ContentBlocks")

`Key | Content | ContentArabic | IsActive | LastUpdatedAt` — capped at 5000
rows (`AdminGridExportEndpoint.MaxExportRows`); selected ids filter the
listed rows when present, otherwise the whole filtered grid is exported.

### Excel import

- Required headers: **`Key | Content | ContentArabic`** (case-insensitive;
  `IsActive` is optional — a blank/unparseable cell defaults to active).
- Worksheet name must be **"ContentBlocks"** (exact match).
- Row key for the per-row error list = the `Key` cell.
- Per row: Key, Content and ContentArabic must be non-blank
  (`DataValidationException` otherwise). The endpoint probes the existing
  key, then upserts: a new key → **Created**, an existing key →
  **Updated** (the service normalises the key, so the raw cell matches).
- Capped at 5000 data rows (`MaxImportRows`); upload max 5 MB
  (`MaxUploadBytes`, 413 `ADMIN_IMPORT_EMPTY`); a non-`.xlsx` upload fails
  the ZIP-magic check (`50 4B 03 04`) → HTTP 400.
- One bad row is recorded as a per-row error and never aborts the batch.

## 6. Validation + error handling

- **`UpsertContentBlockAsync`:** Key length 2–128 → 400
  `CONTENT_BLOCK_INVALID` ("Content block key must be between 2 and 128
  characters."); Content or ContentArabic > 8000 chars → 400
  `CONTENT_BLOCK_INVALID` ("Content cannot exceed 8000 characters.").
- **Not found:** `GET` / `DELETE` on an absent Key → 404
  `CONTENT_BLOCK_NOT_FOUND` ("Content block not found.").
- **Delete is idempotent:** deactivating an already-inactive block is a
  no-op that returns HTTP 200 `Data = true` (no error).
- `CONTENT_BLOCK_KEY_DUPLICATE` exists in `ErrorCodes` but is **not raised**
  by the content-block upsert path (the keyed upsert never produces a
  duplicate); listed here for completeness.
- The CP forms surface `env.Error.MessageForCurrentCulture()` on a failed
  PUT/DELETE in a red `SimfAlert` and keep the form open; a failed `/list`
  shows `Admin.ContentBlocks.LoadFailed`. A failed Excel import/export
  raises `CrudGridExcel.OnError` → `OnExcelError` → red toast.

## 7. Edge cases + known limitations

- **No client-side validation.** The razor performs no length checks before
  the PUT beyond the Key-present guard — every server validation path is
  reached by actually submitting the out-of-bound value.
- **Key normalisation** — "HOME.WELCOME.TITLE" and "home.welcome.title" are
  the same block; the server lower-cases + trims before lookup and storage.
- **Selection is cosmetic for the grid** — there is no bulk delete; ticking
  rows only narrows the Excel export to those ids.
- **`cyber.*` keys are a wire contract.** The well-known cybersecurity-policy
  blocks are seeded by `IdentitySeeder`; the Flutter app codes against those
  keys, so an admin editing/deactivating them from this page can break the
  app's policy screens. Renaming any Key is wire-breaking.
- **No separate Details fetch.** Details/Edit/Delete all bind the grid row
  directly; there is no read-back round-trip to the API to open a form.

## 8. i18n + RTL

`Admin.ContentBlocks.*` resx keys (EN: `Strings.resx`, AR: `Strings.ar.resx`)
plus the shared `Grid.*` keys for the toolbar/pager/Excel labels. The
verified EN strings include Title "Content blocks", New "New block", the
column headers Key / English / Last updated / Active, the field labels
Key / Content (English) / Content (Arabic) / Active, and the toasts
"Content block saved." / "Content block deleted." The Arabic equivalents
live in `Strings.ar.resx` (descriptive — not transcribed here); the page
mirrors RTL with the nav rail and the Add/Edit form. The delete-confirm
message `Admin.ContentBlocks.Delete.Message` interpolates the Key.

## 10. Use cases

- Create a content block (golden CRUD path), edit content in place (Key
  locked), soft-delete (deactivate) a block, re-use an existing key
  (upsert-in-place, no duplicate), bulk seed/update via Excel import,
  export the grid to Excel.

## 11. E2E

See [`docs/tests/e2e/cp-admin-content-blocks.md`](../../tests/e2e/cp-admin-content-blocks.md):
E2E-CNT-001 golden CRUD round-trip, 002 single create, 003 edit (key locked),
004 delete (idempotent deactivate), 005 re-use key upserts in place, 006
empty state, 007 auth gate, 008 short-key 400, 009 over-8000-char 400, 010
missing/already-removed delete, 011 server 500 on list, 012 RTL, 013
per-column filter, 014 column sort, 015 presentation toggle persists, 016
full-page round-trip, 017 delete confirmation gate, 018 Excel export, 019
Excel import, 020 Excel import rejection.

## 12. Related docs

- Per-page CP documentation set (4-aspect + README, D-380):
  [`../../CP/admin-content-blocks/README.md`](../../CP/admin-content-blocks/README.md)
  (Function / Logic / API / Design).
- Page index: `docs/pages/PAGE-INDEX.md`.
- Sibling CMS page: Banners (`/admin/banners`) — shares `AdminCmsService`.
- Permission catalogue: `docs/SIMF-Permission-Catalogue.md` (the
  `ContentBlocks.*` codes) + `src/Shared/SIMF.Common/PermissionCatalog.cs`.
- Decisions: D-173 (CMS), D-199 (CMS wave migration), D-255 (SimfDataGrid),
  D-353 (CrudShell + presentation toggle), D-356 (Excel export + import).
- Authority spec: gap doc G8, PDF §1, §2.1.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-29 | D-173 / D-199 | Original — dynamic CMS content blocks + `ContentBlocks` table (migration `AddCms`); keyed upsert CRUD + public read endpoints. |
| 2026-06-xx | D-255 | Page migrated from a raw `<table>` to the standard `SimfDataGrid` (per-column filters, select-all, sort). |
| 2026-06-10 | D-353 | Add/Edit/View/Delete split into reusable `ContentBlockAddEdit` + `ContentBlockViewDelete` forms hosted by `CrudShell`; one-click delete replaced by a `SimfConfirm` gate; Page↔Popup presentation toggle persisted in `localStorage` (`simf.cp.prefs.content-blocks`). |
| 2026-06-10 | D-356 | Excel **export + import** added (toolbar Export/Import → `.xlsx`, sheet "ContentBlocks"; export columns `Key/Content/ContentArabic/IsActive/LastUpdatedAt`; import required headers `Key/Content/ContentArabic`; both capped at 5000 rows; non-`.xlsx` upload rejected 400). New `ContentBlocks.Export` + `ContentBlocks.Import` permissions. E2E catalogue extended with E2E-CNT-015…020. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 — Excel export + import + D-353 toggle, grounded in live source).
