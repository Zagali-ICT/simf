# Previous Editions (Archive) — `/admin/archive`

| | |
|--|--|
| **Route** | `/admin/archive` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator (admins holding the `Archive.*` permissions) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Archive.View)]` (page) + per-action API policies (`Archive.Create` / `Edit` / `Delete` / `Snapshot` / `Export` / `Import`) + `RequireApprovedAccount` |
| **Pattern** | D-199 event-module CRUD on the uniform CRUD shell — **D-353** `CrudShell` Add/Edit + View/Delete with the Page ↔ Popup `CrudPresentationToggle`, **D-356** Excel export + import via `CrudGridExcel`, plus the **D-275** "make this year history" snapshot action (extra, beyond standard CRUD) |
| **Status** | ✅ Real (D-199; D-275 snapshot; D-353 toggle/CrudShell + D-356 Excel, 2026-06-10; D-357 cover image, 2026-06-11) |
| **Implements use case(s)** | Admin maintenance of the public Archive / Past Editions screen (Mockup page 24) + the per-edition detail (Mockup screen 24-01) per SIMF-FDS-004 / D-199 |
| **Backend endpoints** | `POST /account/api/admin/archive/list`, `GET /account/api/admin/archive/{id}`, `POST /account/api/admin/archive`, `PUT /account/api/admin/archive/{id}`, `DELETE /account/api/admin/archive/{id}`, `POST /account/api/admin/archive/snapshot-current`, `POST /account/api/admin/archive/export`, `POST /account/api/admin/archive/import` |
| **Source file** | [`ArchiveList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ArchiveList.razor), [`ArchiveAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ArchiveAddEdit.razor), [`ArchiveViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ArchiveViewDelete.razor) |
| **Tests** | [`docs/tests/e2e/cp-admin-archive.md`](../../tests/e2e/cp-admin-archive.md); API: `tests/SIMF.Api.Tests/AdminArchiveTests.cs`, `tests/SIMF.Api.Tests/ArchiveTests.cs`, `tests/SIMF.Api.Tests/ArchiveExcelTests.cs` |
| **Last reviewed** | 2026-06-11 |

---

## 1. Purpose

The public Archive / Past Editions screen (Mockup page 24) lists prior forum
editions — each with a year, bilingual title + summary, the headline
attendees / sessions / speakers counters, an optional cover image, place + date
label, and the active flag that gates public visibility. The per-edition detail
(Mockup screen 24-01 "تفاصيل النسخة") shows the same fields for one edition. This
Control Panel page is where an administrator maintains that list: add an edition,
set its year and counters, fill the bilingual title / summary / location / date
label, attach a cover image, toggle the active flag, and soft-delete (deactivate)
an edition so it drops off the public archive.

Beyond standard CRUD this page carries one extra action — **"Make this year
history"** (D-275), a one-click snapshot that creates a past-edition record for
the current year with counters computed from live event data. D-353 moved every
form onto the uniform `CrudShell` (popup or full page, per the admin's saved
preference) and replaced the old inline `SimfModal` edit form + native
`confirm()` delete with a `SimfConfirm`-gated View/Delete form. D-356 added Excel
export and import. D-357 wired the cover image to the unified media-asset
pipeline.

## 2. Audience + permissions

- **Who can reach it:** any admin whose role grants `Archive.View` (or the
  Administrator wildcard `"*"`). The page is gated by
  `@attribute [RequirePermission(PermissionCatalog.Archive.View)]`.
- **Who can edit/write on it:** the standard CRUD action buttons (Add / Edit /
  Details / Delete / Export / Import) are rendered by `SimfDataGrid` and are
  **not** individually wrapped in `<AuthorizedAction>`, so any admin who can open
  the page sees them; the finer-grained gate is enforced **API-side**:
  - Create → `Archive.Create`
  - Edit → `Archive.Edit`
  - Delete → `Archive.Delete`
  - Export → `Archive.Export`
  - Import → `Archive.Import`
  - **Snapshot ("Make this year history") → `Archive.Snapshot`** — unlike the CRUD
    buttons, the snapshot button **is** wrapped in
    `<AuthorizedAction Permission="@PermissionCatalog.Archive.Snapshot">`, so an
    admin lacking that code does not even see the button.
- **Authorisation gates:** each API endpoint declares
  `Policies(PermissionCatalog.PolicyFor(<code>), nameof(AuthorizationPolicies.RequireApprovedAccount))`;
  the mutating + snapshot + Excel endpoints also `RequireRateLimiting("auth")`.
- **What an unauthenticated / under-privileged user sees:** an admin lacking
  `Archive.View` is routed to `/not-permitted` and the `/list` call never fires
  (and the `Module.PreviousEditions` nav item is not rendered); an admin with View
  but not (say) Create gets HTTP 403 on the underlying POST.

> The archive-**visibility** operations toggle (the public on/off switch, D-166)
> is a *separate* surface, gated by `Operations.View` / `Operations.Edit`, not by
> `Archive.*`. This page only touches it indirectly: the snapshot's optional
> "Show in the archive now" checkbox flips it on (see §4.6).

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-archive-001-before.png` | _pending_ |
| Empty state | `docs/screenshots/cp-admin-archive-empty.png` | _pending_ |
| Add (modal) | `docs/screenshots/cp-admin-archive-001-add-modal.png` | _pending_ |
| After create | `docs/screenshots/cp-admin-archive-001-after-create.png` | _pending_ |
| View/Delete + SimfConfirm | `docs/screenshots/cp-admin-archive-001-after-delete.png` | _pending_ |
| Snapshot confirm dialog | `docs/screenshots/cp-admin-archive-snapshot.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/cp-admin-archive-rtl.png` | _pending_ |

## 4. UI affordances

### 4.1 Banner / page header
`SimfBanner` with the title `Admin.Archive.Title` ("Previous Editions" /
"النسخ السابقة"). The banner + toolbar + grid are wrapped in `simf-page-wide` /
`simf-surface`. When a form is open in **full-page** mode the banner + grid are
hidden (`GridHidden`); in popup mode they stay behind the dialog. A `SimfAlert`
toast (success / error) renders above the toolbar.

### 4.2 Toolbar
| Button | Wired callback | Calls | Notes |
|--------|----------------|-------|-------|
| **Make this year history** | `OnSnapshotOpen` | opens the D-275 snapshot confirm dialog | wrapped in `<AuthorizedAction Permission="Archive.Snapshot">`; lives in `simf-toolbar` **above** the grid |
| Select all | grid `Multiselect="true"` | — | mandatory per the list-page standard (cosmetic — no bulk action) |
| Add | `OnAddAsync` | opens `ArchiveAddEdit` (Create) in `CrudShell` | |
| Edit | `OnEditAsync` | opens `ArchiveAddEdit` (Edit) bound to the grid row | no per-row detail re-fetch (the summary row carries every field the form needs) |
| Details | `OnDetailsAsync` | opens `ArchiveViewDelete` read-only | `IsDelete=false`, no Deactivate button |
| Delete | `OnDeleteAsync` | opens `ArchiveViewDelete` delete mode | `IsDelete=true`, Deactivate gated by `SimfConfirm` |
| Export | `OnExportAsync` | `POST /admin/archive/export` via `_excel.ExportAsync` | selected ids, else whole filtered grid |
| Import | `OnImportAsync` | `_excel.TriggerImportAsync()` → file picker → `POST /admin/archive/import` | insert-only |
| **Presentation toggle** | `CrudPresentationToggle` (`PageKey="archive"`) | persists to `localStorage` | Page ↔ Popup (D-353) |

`CrudGridExcel @ref="_excel" Resource="archive"` is rendered below the grid; it
fires `OnImported` → `OnImportedAsync` (success toast + reload) and `OnError` →
`OnExcelError` (error toast).

### 4.3 Grid columns
| Column | Source field | Sortable | Filterable | Notes |
|--------|--------------|----------|------------|-------|
| Year | `context.Year` | yes (`year`) | no | default order is Year descending |
| Title (English) | `context.TitleEn` | yes (`titleEn`) | yes (`titleEn`) | |
| Title (Arabic) | `context.TitleAr` | no | yes (`titleAr`) | |
| Attendees | `context.Attendees` | no | no | |
| Sessions | `context.Sessions` | no | no | |
| Speakers | `context.Speakers` | no | no | |
| Active | `context.IsActive` | no | no | `SimfPill` on/off (`Grid.Active` / `Grid.Inactive`) |

Empty list renders `SimfEmptyState` with `Admin.Archive.None`
("No archive editions yet." / "لا توجد نسخ مؤرشفة بعد."). Backend filter keys are
case-insensitive `Contains` on `titleEn` / `titleAr`; sort keys are `year` |
`titleEn` (anything else falls back to Year-descending).

### 4.4 Pager
Standard `SimfDataGrid` pager — First / Prev / Next / Last + page-size selector,
caption via `FormatSummary` (`Admin.Archive.Summary`). Default page size
`Top = 20` (the service clamps `Top` to 1–500, defaulting to 50 when unset).

### 4.5 Form fields (`ArchiveAddEdit`)
| Field | Type | Required | MaxLength (UI) | Validation (server) | Locale key |
|-------|------|----------|----------------|---------------------|------------|
| Year | number (min 2000 / max 2100) | yes | — | 2000–2100 (`archive_edition_invalid`) | `Admin.Archive.Field.Year` |
| Title (English) | `SimfTextField` | yes | — | 1–200 chars | `Admin.Archive.Field.TitleEn` |
| Title (Arabic) | `SimfTextField` | yes | — | 1–200 chars | `Admin.Archive.Field.TitleAr` |
| Summary (English) | textarea (4 rows) | no | — | ≤1024 chars | `Admin.Archive.Field.SummaryEn` |
| Summary (Arabic) | textarea (4 rows) | no | — | ≤1024 chars | `Admin.Archive.Field.SummaryAr` |
| Attendees | number (min 0 / max 1000000) | no | — | ≥0 | `Admin.Archive.Field.Attendees` |
| Sessions | number (min 0 / max 1000000) | no | — | ≥0 | `Admin.Archive.Field.Sessions` |
| Speakers | number (min 0 / max 1000000) | no | — | ≥0 | `Admin.Archive.Field.Speakers` |
| Cover image path | `SimfTextField` | no | — | ≤512 chars | `Admin.Archive.Field.CoverImageRelativePath` |
| Location (English) | `SimfTextField` | no | 256 | ≤256 chars | `Admin.Archive.Field.LocationEn` |
| Location (Arabic) | `SimfTextField` | no | 256 | ≤256 chars | `Admin.Archive.Field.LocationAr` |
| Date label (English) | `SimfTextField` | no | 128 | ≤128 chars | `Admin.Archive.Field.DateLabelEn` |
| Date label (Arabic) | `SimfTextField` | no | 128 | ≤128 chars | `Admin.Archive.Field.DateLabelAr` |
| Active | checkbox | Edit only | — | bool (Create is always active) | `Admin.Archive.Field.IsActive` |
| **Cover image** | `SimfImageUpload` (`Category="ArchiveCover"`) | Edit only | — | media-asset pipeline (D-357, see §4.7) | `Admin.Asset.Heading` |

On Create the Year defaults to `DateTime.UtcNow.Year` and `IsActive` is `true`
(the checkbox is hidden — Create is always active). The form runs Create (`POST`)
when `IsEdit=false` and Edit (`PUT` against `Initial.Id`) when `IsEdit=true`. The
`SimfImageUpload` control only renders in Edit (`IsEdit && Initial is not null`)
because bytes can only be attached once the row exists. Both titles are
trimmed before the request.

### 4.6 Snapshot — "Make this year history" (D-275, the extra action)

A `<AuthorizedAction Permission="Archive.Snapshot">`-gated `SimfButton` above the
grid (`Admin.Archive.Snapshot.Button` — "Make this year history" /
"اجعل هذه السنة أرشيفاً") opens a `SimfModal` confirm dialog
(`OnSnapshotOpen` → `_snapshotOpen = true`):

- **Title** `Admin.Archive.Snapshot.Title` ("Archive the current event" /
  "أرشفة الفعالية الحالية").
- **Intro** `Admin.Archive.Snapshot.Intro` explaining the counters are computed
  automatically from live data.
- **One checkbox** `Admin.Archive.Snapshot.MakeVisible` ("Show in the archive
  now" / "إظهارها في الأرشيف الآن"), **ticked by default** (`_snapshotMakeVisible = true`).
- **Footer** Cancel (`Admin.Archive.Cancel`) + Confirm
  (`Admin.Archive.Snapshot.Confirm` — "Create snapshot" / "إنشاء النسخة").

Confirm (`SnapshotConfirmAsync`) POSTs
`/account/api/admin/archive/snapshot-current` with
`SnapshotCurrentEditionRequest { MakeVisible = _snapshotMakeVisible }`. The
server (`AdminArchiveService.SnapshotCurrentAsync`) does **all** the work — no
field is client-supplied except the visibility flag:

- **Year** = current UTC year.
- **Title** = `"SIMF {year}"` (En) / `"سيمف {year}"` (Ar), generated.
- **Attendees** = distinct count of allowed `CheckIn` gate scans with a resolved
  `UserProfileId` (`ScanOutcome.Allowed` + `ScanDirection.CheckIn`).
- **Sessions** = active-session count; **Speakers** = active-speaker count.
- It then **reuses `CreateAsync`**, so a second snapshot of the same year hits the
  one-edition-per-year guard and returns `archive_edition_year_duplicate` (409);
  the create also writes the `archive_edition.created` audit.
- If `MakeVisible` is true it calls
  `IOperationsToggleService.UpdateArchiveVisibilityAsync(... IsVisible = true ...)`
  to reveal the public archive.

On success the dialog closes and a green toast shows
`Admin.Archive.Snapshot.Done` ("Archived the current event as {year}." /
"تمت أرشفة الفعالية الحالية كـ {year}.") with `env.Data.Year`, then the grid
reloads. On error the bilingual `MessageForCurrentCulture()` (or the
`Admin.Archive.LoadFailed` fallback) renders and the dialog stays open.

### 4.7 Cover image (D-357, unified media-asset pipeline)

Independent of the free-text `Cover image path` field, the cover image is also
wired to the unified media-asset pipeline (asset category **`ArchiveCover`**):

- **Add/Edit** (`ArchiveAddEdit`, Edit only): a `<SimfImageUpload Category="ArchiveCover" OwnerId="@Initial.Id" Alt="@_model.TitleEn" />`
  control under the `Admin.Asset.Heading` label lets the admin upload a file or
  attach an external link.
- **Details / Delete** (`ArchiveViewDelete`): a thumbnail
  `<SimfImageThumb Src="@($"/account/api/admin/assets/ArchiveCover/{Initial.Id}/image")" Alt="@Initial.TitleEn" Class="simf-img-thumb--lg" />`
  renders the current asset above the read-only `<dl>`.

See `docs/pages/cp/admin-media-library.md` and the media-asset dev guide for the
full pipeline (upload / link / proxy / Media Library listing).

### 4.8 View / Delete form (`ArchiveViewDelete`)
A read-only `<dl>` of Year, Title (En/Ar), Summary (En/Ar), Attendees, Sessions,
Speakers, Location (En/Ar), Date label (En/Ar), Cover image path (with an inline
`<img>` preview when set) and Active. The D-357 cover thumbnail renders above the
list. In delete mode a red **Deactivate** button (`Admin.Archive.Action.Deactivate`)
opens a `SimfConfirm` (Danger) whose message is `Admin.Archive.Delete.Message`
formatted with the edition's English title ("Deactivate "{title}"? It will be
removed from the public archive immediately." / "تعطيل «{title}»؟ ستتم إزالتها من
الأرشيف العام فورًا."); only the confirm fires `DELETE`. The old native list
`confirm()` was removed in D-353.

## 5. Data flow

```
Admin action → ArchiveList handler → JS interop (simfAccount.{post,get,put,delete}Json)
            → BFF /account/api/admin/archive/* → API /api/v1/admin/archive/*
            → IAdminArchiveService / Excel endpoints → SIMF_App DB
            → ApiResult<T> envelope → grid reload + toast
```

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| OnInit / query change | `POST /account/api/admin/archive/list` | `GridQuery` | `ApiResult<GridPage<AdminArchiveEditionSummary>>` |
| Edit / Details / Delete click | (none — the grid row drives the form) | — | — |
| Add save | `POST /account/api/admin/archive` | `CreateArchiveEditionRequest` | `ApiResult<AdminArchiveEditionDetail>` |
| Edit save | `PUT /account/api/admin/archive/{id}` | `UpdateArchiveEditionRequest` | `ApiResult<AdminArchiveEditionDetail>` |
| Confirm deactivate | `DELETE /account/api/admin/archive/{id}` | — | `ApiResult<bool>` |
| **Snapshot confirm** | `POST /account/api/admin/archive/snapshot-current` | `SnapshotCurrentEditionRequest { MakeVisible }` | `ApiResult<AdminArchiveEditionDetail>` |
| Export | `POST /account/api/admin/archive/export` | `AdminGridExportRequest { Ids, Query }` | XLSX binary |
| Import | `POST /account/api/admin/archive/import` | multipart `file` (.xlsx) | `ApiResult<AdminGridImportResult>` |

> `GET /account/api/admin/archive/{id}` exists (gated by `Archive.View`) and is
> used by lower-layer / API callers, but this CP page does **not** call it for
> Edit/Details/Delete — the `AdminArchiveEditionSummary` grid row already carries
> every field the forms bind (title / summary / counters / cover / place / date
> label / active), so the forms bind the summary directly with no per-row detail
> round-trip (mirrors Interests / ContentBlocks).

The endpoint policy → permission mapping (verified in `ArchiveEndpoints.cs` /
`ArchiveExcelEndpoints.cs`):

| Endpoint | Method + API path | Policy (permission) |
|----------|-------------------|---------------------|
| `ListAdminArchiveEndpoint` | `POST /admin/archive/list` | `Archive.View` |
| `GetArchiveEditionEndpoint` | `GET /admin/archive/{id:guid}` | `Archive.View` |
| `CreateArchiveEditionEndpoint` | `POST /admin/archive` | `Archive.Create` |
| `UpdateArchiveEditionEndpoint` | `PUT /admin/archive/{id:guid}` | `Archive.Edit` |
| `DeleteArchiveEditionEndpoint` | `DELETE /admin/archive/{id:guid}` | `Archive.Delete` |
| `SnapshotCurrentArchiveEndpoint` | `POST /admin/archive/snapshot-current` | **`Archive.Snapshot`** |
| `ExportArchiveEndpoint` | `POST /admin/archive/export` | `Archive.Export` |
| `ImportArchiveEndpoint` | `POST /admin/archive/import` | `Archive.Import` |

### 5.1 Excel export columns
`ExportArchiveEndpoint` writes a sheet named **"Archive"** with header row
`Year | TitleEn | TitleAr | Attendees | Sessions | Speakers | IsActive`. File name
prefix `simf-archive` (`simf-archive-{timestamp}.xlsx`). With selected rows the
export honours `AdminGridExportRequest.Ids`; with none, it exports the whole
filtered set (`Query`). (Export is capped by the shared `AdminGridExportEndpoint`
base.)

### 5.2 Excel import
`ImportArchiveEndpoint` is **insert-only**. Sheet name **"Archive"**; required
headers `Year`, `TitleEn`, `TitleAr` (the row key is the `Year` cell). Per-row
validation throws `DataValidationException` when Year is not a whole number, or
when either title is blank ("The year is required and must be a whole number." /
"العام مطلوب ويجب أن يكون رقماً صحيحاً."; "The English title is required." /
"العنوان بالإنجليزية مطلوب."; "The Arabic title is required." / "العنوان بالعربية
مطلوب."). Optional `SummaryEn`, `SummaryAr`, `Attendees`, `Sessions`, `Speakers`
cells are read when present (counters default to 0 when blank or unparsable). Each
row calls `service.CreateAsync`, so a **duplicate year** raises the service's
`archive_edition_year_duplicate` (409), which the base records as a **per-row
error** rather than aborting the batch. The result
`AdminGridImportResult { Created, Updated, Skipped, Errors[] }` drives the outcome
modal; the success toast is the shared `Grid.Import.Done` key.

## 6. Validation + error handling

- **Client-side guard:** `ArchiveAddEdit.HandleSubmitAsync` blocks the request
  when Title (English) or Title (Arabic) is blank and shows
  `Admin.Archive.Validation.TitleRequired`.
- **Server-side validation** (`AdminArchiveService.Validate`): Year must be
  2000–2100; TitleEn/TitleAr trimmed, 1–200 chars; SummaryEn/SummaryAr ≤1024;
  Attendees/Sessions/Speakers ≥0; CoverImageRelativePath ≤512; LocationEn/Ar ≤256;
  DateLabelEn/Ar ≤128. Every failure throws
  `ApiException(ErrorCodes.ArchiveEditionInvalid, 400, …)`
  (`"archive_edition_invalid"`), e.g. "Year must be between 2000 and 2100." /
  "يجب أن يكون العام بين 2000 و 2100.".
- **Duplicate guard:** one edition per year. Create with an existing year, or Edit
  that changes the year onto another edition's year, throws
  `ApiException(ErrorCodes.ArchiveEditionYearDuplicate, 409, …)`
  (`"archive_edition_year_duplicate"`) — "An archive edition for year {year}
  already exists." / "توجد نسخة أرشيف للعام {year} بالفعل.".
- **Not found:** `GET`/`PUT`/`DELETE` against a missing id →
  `ArchiveEditionNotFound` (404, `"archive_edition_not_found"`) — "The archive
  edition was not found." / "لم يتم العثور على نسخة الأرشيف.".
- **Import upload defence:** non-.xlsx / oversize / wrong-sheet / missing required
  header are rejected by the shared `AdminGridImportEndpoint` base with a bilingual
  400 (see `docs/pages/cp/admin-sponsors.md` §6 for the shared upload-defence
  detail).
- **Error envelope:** standard `ApiResult<T>.Error` with `Code` + bilingual
  `Message`/`MessageArabic`; the forms surface `MessageForCurrentCulture()`.
- **Toast strategy:** success → `Admin.Archive.Saved` ("Edition saved.") /
  `Admin.Archive.Deleted` ("Edition deleted.") / `Admin.Archive.Snapshot.Done`
  (green) and `Grid.Import.Done` after import; load failure →
  `Admin.Archive.LoadFailed`; form-level errors render in the form's `SimfAlert`.

## 7. Edge cases + known limitations

- **Soft-delete only.** `DELETE` deactivates (`IsActive=false`) via
  `DeactivateAsync`; the row stays in the admin grid (Active pill → "Inactive")
  but drops off the public `/archive` list immediately. Deactivating an already
  inactive edition is a no-op (returns without a second audit write).
- **No detail re-fetch before forms.** Unlike Sponsors, the Archive grid summary
  carries every field the forms need, so Edit/Details/Delete bind the row directly
  with no `GET /{id}` round-trip.
- **Snapshot counters are computed, not editable.** "Make this year history"
  takes no field input except the visibility checkbox; Year, title and the three
  counters are all server-generated. To adjust them afterwards, Edit the created
  edition.
- **One edition per year.** Both the create path and the snapshot path enforce it;
  a re-run of the snapshot in the same calendar year returns a 409.
- **Two cover representations coexist.** The free-text `CoverImageRelativePath`
  field (rendered as an inline `<img>` on Details) and the D-357 `ArchiveCover`
  media-asset are independent; the public surface chooses which to render.
- **CRUD action buttons are not `<AuthorizedAction>`-gated** (only the snapshot
  button is) — an admin with View but not Create/Edit/Delete/Export/Import sees
  those buttons, but the API rejects the call (403). Treat the API policy as the
  per-action enforcement point.
- **Archive visibility is a separate toggle.** The public archive can be created
  here (or via snapshot) yet remain hidden until the D-166 `ArchiveVisibility`
  toggle (Operations module, `Operations.Edit`) is on. The snapshot's checkbox is
  the only place this page touches it.

## 8. i18n + RTL

All visible strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR) via
`IStringLocalizer<Strings> L`. Banner title `النسخ السابقة`; snapshot button
`اجعل هذه السنة أرشيفاً`; deactivate `تعطيل`. The Arabic toggle
(`العربية` / `English`) sets `<html dir="rtl" lang="ar">`; the nav rail mirrors,
the toolbar + pager reverse, and the `CrudShell` form + the `SimfModal` snapshot
dialog mirror. The snapshot "Done" toast and the delete-confirm message are
positional-format strings (`{0}` = year / title) and render correctly in both
locales.

## 9. Accessibility

- Keyboard: the `CrudShell` traps focus while a form is open and restores it on
  close; the snapshot `SimfModal` and the `SimfConfirm` require an explicit
  Confirm/Cancel choice.
- Screen reader: `SimfDataGrid` exposes a `Caption` (`Admin.Archive.Title`) and
  per-row labels (`RowLabel = TitleEn`); select-all / per-row select have labels.
- Colour contrast: WCAG AA via `theme.tokens.css`; active/inactive use the
  `SimfPill` on/off variants, not colour alone (text "Active"/"Inactive").
- Focus indicators: the `--focus-ring` token on every focusable element.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| (D-199 Archive) | Maintain public Archive / Past Editions list | Mockup page 24 / SIMF-FDS-004; UCS detail entry to be authored |
| (D-275 Snapshot) | Snapshot the current event into a past edition | Mockup §9; counters from live gate-scan / session / speaker data |

## 11. Related E2E test scenarios

| Scenario | File | Coverage |
|----------|------|----------|
| Full CRUD round-trip (Add → Edit → Deactivate) | [`cp-admin-archive.md`](../../tests/e2e/cp-admin-archive.md) | E2E-ARC-001 |
| Empty / auth gate | same | E2E-ARC-002/003 |
| Add modal defaults / client title validation | same | E2E-ARC-004/005 |
| Server validation (year range) / duplicate year 409 | same | E2E-ARC-006/007 |
| Edit pre-fill + IsActive toggle / delete-confirm cancel | same | E2E-ARC-008/009 |
| Server 500 / RTL / filter / sort | same | E2E-ARC-010/011/012/013 |
| **Snapshot golden path / duplicate-year 409 / permission gate / visibility flip** | same | E2E-ARC-014/015/016/017 |
| Presentation toggle persists (D-353) / full-page round-trip | same | E2E-ARC-018/019 |
| Delete confirmation gate (CrudShell + SimfConfirm) (D-353) | same | E2E-ARC-020 |
| Excel export / import / import rejection (D-356) | same | E2E-ARC-021/022/023 |
| Cover Image via the unified media-asset pipeline (D-357) | same | E2E-ARC-024 |

Scenario id range: **E2E-ARC-001 … E2E-ARC-024**.

## 12. Related docs

- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) (CRUD pages).
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `ApiResult<T>` envelope + admin grid endpoints.
- Sibling page: [`admin-sponsors.md`](admin-sponsors.md) (same D-353/D-356 CRUD shell + Excel pattern).
- Decisions log: D-199 (Archive module), D-273 (place + date label), D-275 (snapshot
  "make this year history"), D-353 (uniform CrudShell + Page/Popup toggle +
  SimfConfirm delete), D-356 (Excel export/import), D-357 (unified media-asset cover
  image) in [`DECISIONS_LOG.md`](../../decisions/DECISIONS_LOG.md).
- Source: `ArchiveList.razor`, `ArchiveAddEdit.razor`, `ArchiveViewDelete.razor`,
  `ArchiveEndpoints.cs`, `ArchiveVisibilityEndpoints.cs`, `ArchiveExcelEndpoints.cs`,
  `AdminArchiveService.cs`, `ArchiveContracts.cs`.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-11 | D-357 | Cover image wired to the unified media-asset pipeline: `SimfImageUpload Category="ArchiveCover"` on the Add/Edit form (Edit only) + a `SimfImageThumb` of `/account/api/admin/assets/ArchiveCover/{id}/image` on Details/Deactivate (complements the existing free-text `CoverImageRelativePath` field). E2E catalogue extended with E2E-ARC-024. |
| 2026-06-11 | D-199 / D-275 | Reference doc created. Documents the D-199 Archive CRUD on the D-353 `CrudShell` Add/Edit + View/Delete forms (Page ↔ Popup `CrudPresentationToggle`, PageKey `archive`; `SimfConfirm`-gated deactivate), the D-356 Excel export (`POST /export`) + insert-only import (`POST /import`), and the **D-275 "make this year history" snapshot** action (`POST /snapshot-current`, gated by `Archive.Snapshot`, server-computed counters + optional visibility flip). |

---

**2026-07-14 (D-357):** the English-title column now renders the edition's cover
thumbnail via the shared `SimfIdentityCell` (`AdminArchiveEditionSummary.HasCover`,
streamed from the `ArchiveCover` /assets proxy) or a tinted initials tile. Column
key unchanged so server-side sort/filter is unaffected. E2E-ARC-026.

_Last reviewed:_ 2026-07-14 by Claude (D-357 — archive cover thumbnail in the list). Prior: 2026-06-11 by Claude (technical-writer).
