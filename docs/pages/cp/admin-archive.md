# Archive (past editions) — `/admin/archive`

| | |
|--|--|
| **Route** | `/admin/archive` |
| **Audience** | Administrator (any role granting `Archive.View`; `Administrator = "*"`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Archive.View)]` (CP page) + per-action API policies + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (mutations) |
| **Pattern** | D-199 (Mockup screen 24) CRUD over `ArchiveEdition`; D-353 centralized CrudShell forms; D-356 Excel export + import. |
| **Status** | ✅ Real (D-199; D-273 location/date; D-275 snapshot; D-353 toggle; D-356 Excel; D-357 cover image) |
| **Backend endpoints** | BFF `/account/api/admin/archive/*` → API `/api/v1/admin/archive/*`: `POST .../list`, `GET .../{id}`, `POST ...` (create), `PUT .../{id}`, `DELETE .../{id}`, `POST .../snapshot-current`, `POST .../export`, `POST .../import` |
| **Source** | [`ArchiveList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ArchiveList.razor), [`ArchiveAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ArchiveAddEdit.razor), [`ArchiveViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ArchiveViewDelete.razor), [`AdminArchiveService`](../../../src/Backend/SIMF.Infrastructure/Archive/AdminArchiveService.cs), [`ArchiveEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Archive/ArchiveEndpoints.cs), [`ArchiveExcelEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/ArchiveExcelEndpoints.cs) |
| **Tests** | [`../../tests/e2e/cp-admin-archive.md`](../../tests/e2e/cp-admin-archive.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

Admin CRUD over the public Archive / Past Editions list (Mockup screen 24,
D-199). Each `ArchiveEdition` carries a **year**, a bilingual title and summary,
the headline **attendees / sessions / speakers** counts, an optional
**cover-image relative path**, an optional bilingual **location** and **date
label** (D-273, Mockup screen 24-01), and an `IsActive` flag that gates public
visibility. The list feeds the Website's "Previous Editions" page and per-year
detail page.

**One edition per year.** The service enforces year uniqueness and surfaces a
clash as a 409 (`archive_edition_year_duplicate`). The `IdentitySeeder` seeds
demo editions for **2022, 2023, 2024 and 2025** (idempotent by year — it inserts
a missing year and backfills detail fields on counters-only rows), so the public
archive timeline shows several years with real content out of the box.

The page also hosts the D-275 **"make this year history"** snapshot action and,
since D-356, the **Excel export + import** toolbar actions.

## 4. UI

- `SimfBanner` (title `Admin.Archive.Title`) + the `simf-page-wide` / `simf-surface`
  shell; an inline `SimfAlert` toast renders for success / error feedback.
- A toolbar above the grid carries the **"Make this year history"** button
  (`Admin.Archive.Snapshot.Button`), wrapped in
  `<AuthorizedAction Permission="Archive.Snapshot">` so it only renders for an
  account that holds that permission.
- The list is a `SimfDataGrid` (`TItem="AdminArchiveEditionSummary"`,
  `Top = 20` page size, `Multiselect="true"` select-all checkboxes). Columns:
  **Year**, **Title (English)**, **Title (Arabic)**, **Attendees**, **Sessions**,
  **Speakers**, **Active** (an `on`/`off` `SimfPill` reading Active / Inactive).
  Sortable on **Year** (`year`) and **Title (English)** (`titleEn`); per-column
  **Filterable** on **Title (English)** (`titleEn`) and **Title (Arabic)**
  (`titleAr`).
- Row actions are the grid's quiet icon buttons: **Add**, **Edit** (`OnEditOne`),
  **Details** (`OnDetailsOne`), **Delete** (`OnDeleteOne`), plus **Export**
  (`OnExport`) and **Import** (`OnImport`).
- An `EmptyTemplate` renders `SimfEmptyState` (title `Admin.Archive.None`) when
  the grid has no rows.
- **CrudShell-hosted forms (D-353).** Add / Edit open `ArchiveAddEdit`; Details /
  Delete open `ArchiveViewDelete`. Both are framed by `CrudShell` as either a
  **dialog** or a **full page** per the toolbar's `CrudPresentationToggle`
  (`PageKey = "archive"`). The choice is loaded in `OnInitializedAsync` via
  `Prefs.GetPresentationAsync("archive")` and persisted by `CpPreferences` in
  `localStorage` (`simf.cp.prefs.archive`). In full-page mode the grid + banner
  are hidden (`GridHidden`) while the form takes over the content area. The inline
  `SimfModal` edit form + native `confirm()` delete that the page used to carry
  are gone.
- **"Make this year history" dialog (D-275).** The snapshot button opens a
  `SimfModal` with an intro paragraph and a single **"Show in the archive now"**
  checkbox (`_snapshotMakeVisible`, ticked by default), plus Cancel / Confirm
  buttons. Confirm POSTs `/account/api/admin/archive/snapshot-current`; on success
  the dialog closes and a toast reports the archived year.
- **Excel export + import (D-356).** The grid's `OnExport` / `OnImport` wire to a
  shared `CrudGridExcel` component (`Resource="archive"`). Export sends the
  selected row Ids (else the whole filtered grid via the current `_query`) and
  downloads an `.xlsx`; Import triggers the file picker, uploads the workbook, and
  on success raises `Grid.Import.Done` and reloads the grid.
- **Cover image (D-357).** In Edit mode `ArchiveAddEdit` renders a
  `SimfImageUpload` (`Category="ArchiveCover"`, `OwnerId=Initial.Id`) for the
  unified media-asset pipeline; `ArchiveViewDelete` shows a `SimfImageThumb`
  preview from `/account/api/admin/assets/ArchiveCover/{id}/image`.

## 4.5 Form fields

`ArchiveAddEdit` model (lengths mirror the FluentValidation rules in
`AdminArchiveEditionValidators` and the service-side `AdminArchiveService.Validate`):

| Field | Required | Range / MaxLength | Notes |
|-------|----------|-------------------|-------|
| Year | yes | 2000–2100 | number input (`min=2000 max=2100`); defaults to `DateTime.UtcNow.Year` on Add |
| Title (English) | yes | 1–200 | `SimfTextField` |
| Title (Arabic) | yes | 1–200 | `SimfTextField` |
| Summary (English) | no | ≤ 1024 | textarea; blank → null |
| Summary (Arabic) | no | ≤ 1024 | textarea; blank → null |
| Attendees | yes | ≥ 0 (`min=0 max=1000000`) | number input |
| Sessions | yes | ≥ 0 (`min=0 max=1000000`) | number input |
| Speakers | yes | ≥ 0 (`min=0 max=1000000`) | number input |
| Cover image relative path | no | ≤ 512 | `SimfTextField` |
| Location (English) | no | ≤ 256 | `SimfTextField MaxLength="256"` (D-273) |
| Location (Arabic) | no | ≤ 256 | `SimfTextField MaxLength="256"` (D-273) |
| Date label (English) | no | ≤ 128 | `SimfTextField MaxLength="128"` (D-273) |
| Date label (Arabic) | no | ≤ 128 | `SimfTextField MaxLength="128"` (D-273) |
| Active | (Edit only) | bool | `SimfCheckbox`; create always lands `IsActive = true` |

The Cover-image `SimfImageUpload` control (D-357) appears only when editing an
existing row (the entity must exist before bytes can be attached).

## 5. Data flow + endpoints

The page talks only to the BFF `/account/api/admin/archive/*` routes
(`AccountEndpoints`), which forward to the API `/api/v1/admin/archive/*`
endpoints (`SimfAdminClient`). All responses use the `ApiResult<T>` envelope.

| Action | CP call | BFF → API | Permission policy (API) |
|--------|---------|-----------|--------------------------|
| List grid | `simfAccount.postJson` `/list` | `POST /admin/archive/list` | `Archive.View` |
| Get one | (forms bind the summary row directly) `GET /{id}` | `GET /admin/archive/{id}` | `Archive.View` |
| Create | `postJson` (no id) | `POST /admin/archive` | `Archive.Create` |
| Update | `putJson` `/{id}` | `PUT /admin/archive/{id}` | `Archive.Edit` |
| Delete (soft) | `deleteJson` `/{id}` | `DELETE /admin/archive/{id}` | `Archive.Delete` |
| Snapshot | `postJson` `/snapshot-current` | `POST /admin/archive/snapshot-current` | `Archive.Snapshot` |
| Export | `CrudGridExcel` POST `/export` | `POST /admin/archive/export` | `Archive.Export` |
| Import | `CrudGridExcel` POST `/import` | `POST /admin/archive/import` | `Archive.Import` |

Every API endpoint also requires `RequireApprovedAccount`; the mutating
endpoints add `RequireRateLimiting("auth")`. The CP page is gated by
`RequirePermission(Archive.View)` and the nav item `Module.PreviousEditions`
(`/admin/archive`) carries `RequiredPermission = Archive.View`.

The grid summary (`AdminArchiveEditionSummary`) already carries every field the
Add/Edit and View/Delete forms need, so the forms bind the row directly with no
per-row detail round-trip; the save endpoints return `AdminArchiveEditionDetail`,
which `ArchiveAddEdit.ToSummary` projects back to a summary for the host grid.

**Snapshot (D-275).** `SnapshotCurrentAsync` generates the year (current UTC
year) and the bilingual title (`"SIMF {year}"` / `"سيمف {year}"`), and computes
the three counters from live App data — **attendees** = distinct
`UserProfileId` across `GateScans` where `Outcome == Allowed` and
`Direction == CheckIn` and a profile is resolved; **sessions** = active session
count; **speakers** = active speaker count — then reuses `CreateAsync` (so the
one-edition-per-year 409 and the `ArchiveEditionCreated` audit apply). When
`MakeVisible` is set it flips the D-166 archive-visibility toggle on via
`IOperationsToggleService`.

**Excel export (D-356).** `ExportArchiveEndpoint` (sheet **"Archive"**, file
prefix `simf-archive`) writes columns
`Year | TitleEn | TitleAr | Attendees | Sessions | Speakers | IsActive`. It lists
via `service.ListAllAsync(query, …)` honouring the current filter/sort, keyed on
`row.Id`.

**Excel import (D-356).** `ImportArchiveEndpoint` is **insert-only**, sheet
**"Archive"**, required headers `Year | TitleEn | TitleAr`, with `RowKey = Year`.
Each row binds to `CreateArchiveEditionRequest` (it also reads optional
`SummaryEn / SummaryAr / Attendees / Sessions / Speakers`; counters default to 0
when blank/unparseable) and creates the edition. A missing/non-numeric Year, a
blank English title, or a blank Arabic title raises a `DataValidationException`
recorded as a per-row error. A duplicate year hits the service's
`archive_edition_year_duplicate` 409, which the base records as a per-row error
rather than aborting the batch.

## 6. Validation + error handling

- **Client guard (`ArchiveAddEdit.HandleSubmitAsync`):** a blank English **or**
  Arabic title short-circuits before any request, surfacing
  `Admin.Archive.Validation.TitleRequired` in a `SimfAlert`.
- **FastEndpoints validators** (`CreateArchiveEditionRequestValidator` /
  `UpdateArchiveEditionRequestValidator` on `UpdateArchiveEditionRoute`):
  Year `InclusiveBetween(2000, 2100)`; TitleEn/TitleAr `NotEmpty().MaximumLength(200)`;
  SummaryEn/SummaryAr `≤ 1024`; Attendees/Sessions/Speakers `≥ 0`;
  CoverImageRelativePath `≤ 512`; LocationEn/Ar `≤ 256`; DateLabelEn/Ar `≤ 128`.
- **Service `Validate`** mirrors those limits and throws
  `ApiException(ErrorCodes.ArchiveEditionInvalid, 400, …)` (bilingual) for an
  out-of-range year, an out-of-length title (1–200), summary (≤ 1024), negative
  counters, an over-512 cover path, an over-256 location, or an over-128 date
  label.
- **Duplicate year** → `ApiException(ErrorCodes.ArchiveEditionYearDuplicate, 409)`
  on create, and on update when the year changes to one another row already owns.
  Message names the year (e.g. "An archive edition for year 2019 already exists.").
- **Not found** → `ApiException(ErrorCodes.ArchiveEditionNotFound, 404)` on
  update / delete of a missing id.
- **List failure / unexpected error** surfaces a bilingual fallback toast
  (`Admin.Archive.LoadFailed`); forms catch exceptions to the same fallback.
- **Excel upload defence (D-356)** lives in the shared import base (ZIP-magic /
  size gate, sheet-name + required-header checks); a non-`.xlsx` or wrong-sheet /
  missing-header upload returns HTTP 400 with a bilingual message and creates
  nothing.

## 7. Edge cases + known limitations

- **Delete is a soft-delete.** `DeactivateAsync` flips `IsActive = false`
  (pulling the edition from the public archive) and returns early if already
  inactive; the row stays in the admin grid with its Active pill flipped to
  Inactive. It never hard-deletes.
- **Delete requires explicit confirmation (D-353).** `ArchiveViewDelete` shows
  the read-only details and a red Deactivate button; clicking it opens a
  `SimfConfirm` (titled `Admin.Archive.Delete.Title`, message formatting in the
  edition title). Cancelling fires no DELETE; confirming fires exactly one.
- **Snapshot is fully automatic.** No client input drives the year, title, or
  counters; a second snapshot of the same year returns the 409.
- **Import is insert-only.** No existing edition is updated; a duplicate year is
  a per-row error, not a batch abort.
- **One edition per year** is the core invariant — both manual create and the
  snapshot path enforce it.
- **Snapshot counters depend on live data** — attendees reflects allowed CheckIn
  gate scans with a resolved profile, so a fresh / empty event snapshots to 0
  attendees.

## 8. i18n + RTL

All copy is resolved through `IStringLocalizer<Strings>` against
`Admin.Archive.*`, `Grid.*` and `Admin.Asset.*` resource keys (EN + AR resx),
so labels, column headers, buttons, toasts and dialog text render bilingually and
the page mirrors under `dir="rtl"`. The nav label is `Module.PreviousEditions`
("Previous editions" / "الدورات السابقة"). Exact resx phrasings are descriptive
here; the catalogue is the source of the literal strings.

## 10. Use cases

- Create / edit / soft-delete a past edition (manual CRUD).
- One-click "make this year history" snapshot of the live event into a new
  edition with computed counters, optionally revealing the archive.
- Bulk-load historical editions via Excel import; export the grid to Excel for
  offline edit / reporting.
- Attach or replace an edition's cover image via the media-asset pipeline.

## 11. E2E

See [`../../tests/e2e/cp-admin-archive.md`](../../tests/e2e/cp-admin-archive.md):
E2E-ARC-001 full CRUD round-trip, 002 empty list, 003 auth gate, 004 add
defaults, 005 client title validation, 006 server year-range 400, 007 duplicate
year 409, 008 edit + IsActive toggle, 009 delete-confirm cancel, 010 list 500
fallback, 011 RTL, 012 per-column filter, 013 column sort, 014–017 snapshot
(golden / duplicate / permission gate / visibility checkbox), 018 presentation
toggle persists, 019 full-page round-trip, 020 delete confirmation gate, 021–023
Excel export / import / import-rejection, 024 cover image via the media-asset
pipeline. API integration coverage:
`tests/SIMF.Api.Tests/AdminArchiveTests.cs`,
`tests/SIMF.Api.Tests/ArchiveTests.cs`,
`tests/SIMF.Api.Tests/ArchiveExcelTests.cs`.

## 12. Related docs

- Page index + per-page reference: `docs/pages/PAGE-INDEX.md`.
- Permissions: `src/Shared/SIMF.Common/PermissionCatalog.cs` (`Archive` class —
  View / Create / Edit / Delete / Snapshot / Export / Import) and
  `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`.
- Decisions: D-199 (module), D-273 (location/date), D-275 (snapshot), D-353
  (CrudShell + presentation toggle), D-356 (Excel export/import), D-357 (cover
  image), D-166 (archive-visibility toggle).
- Authority spec: SIMF-FDS-004 §9 / Mockup screen 24 + 24-01.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-11 | D-356 | Reference doc authored — backfills the Archive (past editions) CP page covering the D-199 CRUD baseline, D-273 location/date, D-275 snapshot, D-353 CrudShell + Page↔Popup toggle, D-356 Excel export + import, and D-357 cover image. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 ref-doc backfill).
