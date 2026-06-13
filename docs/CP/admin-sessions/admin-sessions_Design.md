# Programme sessions — Design (`/admin/sessions`)

The as-built Control Panel screen. Source: `SessionsList.razor` +
`SessionsAddEdit.razor` + `SessionsViewDelete.razor` (Blazor Server,
`CpShellLayout`). Bilingual EN/AR, RTL-mirrored. Verified against code this
session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Function](admin-sessions_Function.md) ·
> [API](admin-sessions_API.md) · [Logic](admin-sessions_Logic.md) ·
> existing reference [`docs/pages/cp/admin-sessions.md`](../../pages/cp/admin-sessions.md) ·
> E2E [`docs/tests/e2e/cp-admin-sessions.md`](../../tests/e2e/cp-admin-sessions.md).

## Layout (top → bottom, as built)

When no CRUD form is open (`GridHidden = false`):

1. **Banner** — `SimfBanner Title="@L["Admin.Sessions.Title"]"` → EN **Sessions**
   (AR pair from `Strings.ar.resx`). The banner + grid are wrapped in
   `simf-page-wide` / `simf-surface`.
2. **Inline alert** — a `SimfAlert` renders above the grid only when a `_toast`
   is set. `Variant` is `"success"` or `"error"` (the `Toast` record).
3. **SimfDataGrid** (`TItem="AdminSessionSummary"`, D-255 owner-mandated list-page
   standard) — `Multiselect="true"` (select-all + per-row checkbox), full pager,
   quiet per-row icon actions. `RowKey` = `Id`, `RowLabel` = `Title`,
   `Caption` = `Admin.Sessions.Title`. Its `CustomToolbar` hosts a
   **`CrudPresentationToggle`** bound to `PageKey = "sessions"` (the D-353
   dialog⇄full-page toggle).
   - Grid action callbacks wired on the grid: `OnAdd` → `OnAddAsync`,
     `OnEditOne` → `OnEditAsync`, `OnDetailsOne` → `OnDetailsAsync`,
     `OnDeleteOne` → `OnDeleteAsync`, `OnExport` → `OnExportAsync`,
     `OnImport` → `OnImportAsync`.
   - `EmptyTemplate` → `SimfEmptyState Title="@L["Admin.Sessions.None"]"`.
4. **`CrudGridExcel`** — `<CrudGridExcel @ref="_excel" Resource="sessions"
   OnImported="OnImportedAsync" OnError="OnExcelError" />` renders below the grid;
   it owns the hidden file picker + the import-result modal (D-356).

When a CRUD form is open (`FormOpen`), the page renders a **`CrudShell`**
(`Presentation="_presentation"`, dialog by default, full page when
`_presentation == CrudPresentation.Page` — in which case the grid + banner are
hidden via `GridHidden`). The shell frames either `SessionsAddEdit` or
`SessionsViewDelete` per `_form` (`FormKind.AddEdit` / `FormKind.ViewDelete`).
The shell title comes from `FormTitle`:

| State | Resx key |
|-------|----------|
| Add | `Admin.Sessions.Add.Title` |
| Edit | `Admin.Sessions.Edit.Title` |
| Details (view) | `Admin.Sessions.Details.Title` |
| Deactivate (delete) | `Admin.Sessions.Delete.Title` |

The shell close label is `Admin.Sessions.Details.Close`.

## Grid columns (`SessionsList.razor`)

| # | Column header (resx) | Source field | Sortable | Filterable | Render |
|---|----------------------|--------------|----------|------------|--------|
| 1 | `Admin.Sessions.Column.Code` | `Code` | yes | no | plain text |
| 2 | `Admin.Sessions.Column.Title` | `Title` | yes | **yes** | plain text |
| 3 | `Admin.Sessions.Column.TitleArabic` | `TitleArabic` | no | no | plain text |
| 4 | `Admin.Sessions.Column.Hall` | `HallLabel(row)` | no | no | culture-aware: `HallNameArabic` when UI culture is `ar`, else `HallName` |
| 5 | `Admin.Sessions.Column.StartUtc` | `StartUtc` | yes | no | `StartUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm")` |
| 6 | `Admin.Sessions.Column.EndUtc` | `EndUtc` | yes | no | `EndUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm")` |
| 7 | `Admin.Sessions.Column.Capacity` | `Capacity` | no | no | effective capacity (int) |
| 8 | `Admin.Sessions.Column.Active` | `IsActive` | no | no | `SimfPill` — `on` (`Admin.Sessions.Active.Yes`) / `off` (`Admin.Sessions.Active.No`) |
| 9 | `Admin.Sessions.Column.Status` | `Status` | no | no | `SimfPill` — variant `StatusPillVariant`: `Published`→`on`, `Scheduled`→`neutral`, else `admin`; label `Admin.Sessions.Status.{enumName}` |

The only filterable column is **Title** (`FilterColumnLabel` =
`Admin.Sessions.FilterColumn`). Pager labels: `Admin.Sessions.Prev` / `.Next` /
`.Pager.First` / `.Pager.Last` / `.Pager.PageSize`; default page size `Top = 20`;
summary `FormatSummary` → `string.Format(Admin.Sessions.Summary, skip+1,
skip+taken, total)`; page format `Admin.Sessions.Pager.Page`.

## Add / Edit form (`SessionsAddEdit.razor`)

A reusable form inheriting `CrudAddEditFormBase<AdminSessionDetail>` (exposes
`IsEdit` / `Initial` / `OnSuccess` / `OnCancel`). `IsEdit=false` runs Create
(POST); `IsEdit=true` runs Edit (PUT against `Initial`) and shows the `IsActive`
checkbox. An `EditForm` (`OnSubmit="HandleSubmitAsync"`) over a local `Model`.

Fields, in render order:

| Field (resx label) | Control | MaxLength | Notes |
|--------------------|---------|-----------|-------|
| `Admin.Sessions.Field.Code` | `SimfTextField` | 16 | helper `…CodeHint`; trimmed + `ToUpperInvariant` on submit |
| `Admin.Sessions.Field.Title` | `SimfTextField` | 256 | English title |
| `Admin.Sessions.Field.TitleArabic` | `SimfTextField` | 256 | Arabic title |
| `Admin.Sessions.Field.Description` | `SimfTextarea` (3 rows) | 2048 | optional; `null` if blank |
| `Admin.Sessions.Field.DescriptionArabic` | `SimfTextarea` (3 rows) | 2048 | optional; `null` if blank |
| `Admin.Sessions.Field.LiveStreamUrl` | `SimfTextField` | 1024 | helper `…LiveStreamUrlHint`; `LiveStreamUrlPolicy.IsAllowed` (§8 / D-349) |
| `Admin.Sessions.Field.LiveSignLanguageUrl` | `SimfTextField` | 1024 | helper `…LiveSignLanguageUrlHint`; same policy |
| `Admin.Sessions.Field.Hall` | `SimfSelect` | — | required; placeholder `…HallPlaceholder`; label `"{Name} ({Code})"` (AR pair when `ar`) |
| `Admin.Sessions.Field.Category` | `SimfSelect` | — | optional; placeholder `…CategoryNone`; ordered by `DisplayOrder` then `Name` (D-226) |
| `Admin.Sessions.Field.StartUtc` | `SimfTextField` type `datetime-local` | — | parsed, treated as UTC |
| `Admin.Sessions.Field.EndUtc` | `SimfTextField` type `datetime-local` | — | must be `> Start` |
| `Admin.Sessions.Field.CapacityOverride` | `SimfTextField` type `number` | — | helper `…CapacityHint`; blank = inherit hall; else int ≥ 0 |
| `Admin.Sessions.Field.AddSpeaker` | `SimfSelect` | — | only shown when speaker options exist; builds the roster |
| (speaker chips) | `<ul class="simf-form__chips">` | — | each chip: `"{n}. {label}"`, a role `<select>` (`…Role.Speaker` / `…Role.Host`), and **Up** (`…Action.Up`) / **Down** (`…Action.Down`) / **Remove** (`…Action.Remove`) ghost buttons |
| `Admin.Sessions.Field.AddTheme` | `SimfSelect` | — | only shown when theme options exist |
| (theme chips) | `<ul class="simf-form__chips">` | — | each chip: theme label + **Remove** |
| `Admin.Sessions.Field.IsActive` | `SimfCheckbox` | — | **Edit only** ("show in the public agenda") |

Submit button: `Admin.Sessions.New.Submit` (create) / `Admin.Sessions.Edit.Submit`
(edit), with loading labels `…New.Submitting` / `…Edit.Submitting`. A secondary
**Cancel** (`Admin.Sessions.Cancel`) shows when `OnCancel` is bound. A failed
client guard sets `_error` (a top-of-form `SimfAlert Variant="error"`) and **no**
request fires.

The four pickers lazy-load on first render (`OnAfterRenderAsync`):
`POST .../halls/list`, `.../speakers/list`, `.../themes/list`,
`.../session-categories/list` (each `Top=500`, filter `isActive=true`). A picked
speaker leaves the Add-speaker option list (no duplicate pick); the roster is
1-based renumbered on add/move/remove.

## View / Delete form (`SessionsViewDelete.razor`)

A reusable form inheriting `CrudViewDeleteFormBase<AdminSessionDetail>` (exposes
`IsDelete` / `Initial` / `OnDeleted` / `OnCancel`). Read-only details always show;
extra blocks gate on `Sessions.Publish`.

1. **Read-only `<dl class="simf-dl">`** — Code, Title, Title (Arabic),
   Description (`—` when null), Description (Arabic), Hall (`HallLabel`),
   Start / End (`yyyy-MM-dd HH:mm 'UTC'`), Capacity override
   (`…CapacityInherits` = "Inherits from hall" when null), Effective capacity,
   Live stream URL (`—` when blank), Live sign-language URL, Speakers (`—` or a
   `<ul>` of `"{Name} ({NameArabic})"`), Active (`…Active.Yes/.No`),
   Status (`SimfPill`), Published at (only when `PublishedAt` is set), and the
   Recording row (`…Recording.Label` → file + `FormatBytes` when `HasRecording`,
   else `…Recording.None`).
2. **Recording block** — wrapped in
   `<AuthorizedAction Permission="@PermissionCatalog.Sessions.Publish">`: a file
   `<input id="session-recording-input" type="file" accept="video/*">`
   (`…Recording.File`), an **Upload** button (`…Recording.Upload`), and — when a
   recording exists — a **Remove** button (`…Recording.Delete`).
3. **Broadcast-lifecycle footer** — also `Sessions.Publish`-gated: one button per
   legal next move from `NextTransitions(Status)`:
   - `Scheduled` → **Mark held** (`…Lifecycle.MarkHeld`)
   - `Held` → **Back to scheduled** (`…BackToScheduled`) + **Mark recorded** (`…MarkRecorded`)
   - `Recorded` → **Back to held** (`…BackToHeld`) + **Publish** (`…Publish`)
   - `Published` → **Un-publish** (`…Unpublish`)
4. **Footer actions** — a red **Deactivate** button (`…Action.Deactivate`, only
   when `IsDelete`) that opens a `SimfConfirm`, and a **Close** button
   (`…Details.Close`).
5. **`SimfConfirm`** — title `…Delete.Title`, message
   `string.Format(…Delete.Message, Title)`, confirm `…Action.Deactivate`,
   cancel `…Cancel`, `Danger="true"`; gates the soft-delete `DELETE`.

## States

- **Loading** — `_loading` drives the grid's loading indicator
  (`LoadingLabel` = `Admin.Sessions.Loading`).
- **Empty** — `SimfEmptyState` with `Admin.Sessions.None` ("No sessions yet."),
  the **Add** toolbar action stays available, no error toast.
- **Error** — a list/load failure sets a `"error"` `_toast` showing
  `Error.MessageForCurrentCulture()` or the `Admin.Sessions.LoadFailed` fallback;
  form-level failures show the in-form `SimfAlert` (`Admin.Sessions.Fallback`).
- **Success** — green `_toast`: `Admin.Sessions.Created` / `…Updated` /
  `…Deactivated` (each `string.Format` with the title); import → `Grid.Import.Done`.

## RTL / localization

- All strings via `IStringLocalizer<Strings> L` under `Admin.Sessions.*`
  (EN `Strings.resx` + AR `Strings.ar.resx`); status labels derive
  `Admin.Sessions.Status.{enumName}` 1:1 with `SessionStatus` (no switch).
- Hall / speaker / theme / category labels are culture-aware
  (`CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"` → Arabic name).
- RTL (`<html dir="rtl" lang="ar">`) mirrors the nav rail, grid headers, pills,
  pager arrows, the speaker chip Up/Down/Remove buttons and the lifecycle footer.
- Times in the grid + view render in **UTC** (admin surface); the **app** (Page 016)
  renders the same `StartUtc`/`EndUtc` in the device timezone.
