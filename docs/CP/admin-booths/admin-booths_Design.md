# Exhibition booths — Design (`/admin/booths`)

The as-built Control Panel screen. Blazor Server, RTL-aware, bilingual (EN/AR).
Source: `Components/Pages/Admin/BoothsList.razor` (+ `BoothsAddEdit.razor`,
`BoothsViewDelete.razor`). Page rules are in
[admin-booths_Logic.md](admin-booths_Logic.md); the contract is in
[admin-booths_API.md](admin-booths_API.md).

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Layout (top → bottom, as built)

1. **Banner** — `SimfBanner Title="@L["Admin.Booths.Title"]"` (EN **Exhibition
   booths** / AR **أجنحة المعرض**). Hidden when a form is open in **full-page**
   mode (`GridHidden = FormOpen && _presentation == Page`); in popup mode it
   stays behind the dialog.
2. **Surface** — `div.simf-page-wide > div.simf-surface` wrapping the toast +
   grid.
3. **Toast** — `SimfAlert` rendered above the grid when `_toast is not null`
   (`Variant` = `success` / `error`).
4. **Data grid** — `SimfDataGrid TItem="AdminBoothSummary"` (`Multiselect="true"`),
   bound to `_page` with `Query="_query"` (default `Top = 20`). It carries the
   row-action callbacks (`OnAdd`/`OnEditOne`/`OnDetailsOne`/`OnDeleteOne`),
   the toolbar `OnExport`/`OnImport`, a `<CustomToolbar>` holding the
   `CrudPresentationToggle PageKey="booths"`, the column set, an
   `EmptyTemplate`, and the pager labels/formatters.
5. **Excel host** — `<CrudGridExcel @ref="_excel" Resource="booths" …>`
   directly below the grid (the hidden file input + import-result modal live
   inside it).
6. **CrudShell** — rendered when `FormOpen`; frames either `BoothsAddEdit`
   (Add/Edit) or `BoothsViewDelete` (View/Delete) as a popup or full page per
   `_presentation`. Title from `FormTitle`; close via `CloseForm`.

## Grid columns (`<Columns>`)

| Key | Header key | Sortable | Filterable | Cell |
|-----|------------|----------|------------|------|
| `code` | `Admin.Booths.Col.Code` | yes | yes | `@context.Code` |
| `name` | `Admin.Booths.Col.NameEn` | yes | yes | `@context.Name` |
| `nameArabic` | `Admin.Booths.Col.NameAr` | no | yes | `@context.NameArabic` |
| `exhibitor` | `Admin.Booths.Col.Exhibitor` | **no** | **no** | `@ExhibitorName(context.ExhibitorId)` |
| `sector` | `Admin.Booths.Col.SectorEn` | yes | yes | `@(context.Sector ?? "—")` |
| `hall` | `Admin.Booths.Col.Hall` | **no** | **no** | `@HallName(context.HallId)` |
| `isActive` | `Admin.Booths.Col.Active` | yes | no | `SimfPill` `on` (`Grid.Active`) / `off` (`Grid.Inactive`) |

- **Exhibitor + Hall are client-resolved.** The `AdminBoothSummary` row carries
  only `ExhibitorId` / `HallId`; the page resolves a display name from two
  cached lookups (`_exhibitors`, `_halls`) loaded at mount via
  `POST /account/api/admin/exhibitors/list` and `.../halls/list` (`Top=500`,
  active rows only). Because the value is computed client-side, neither column
  is server-sortable or server-filterable (intentionally — see
  `AdminBoothService.ListAllAsync`). Unknown ids render `—`.
- **Empty list** → `EmptyTemplate` renders `SimfEmptyState Title="@L["Admin.Booths.None"]"`.
- **Row identity** — `RowKey = r.Id.ToString()`, `RowLabel = "{Code} — {Name}"`.

## Grid toolbar + pager

- **Row actions** (quiet icons): Add (`OnAddAsync`), Edit pencil
  (`OnEditAsync`), Details (`OnDetailsAsync`), Delete trash (`OnDeleteAsync`).
- **Toolbar Export / Import** (`OnExportAsync` / `OnImportAsync`) — wired to the
  `CrudGridExcel` host; labels `Grid.Export` / `Grid.Import`.
- **Select-all + per-row checkboxes** (`Multiselect="true"`) — used by Export to
  scope to selected ids; there is **no** bulk-action toolbar button.
- **Pager** — First/Prev/Next/Last + page size, with the
  `SummaryFormatter` ("Showing {0}–{1} of {2}", `Grid.Summary`) and
  `PageFormatter` ("Page {0} of {1}", `Grid.Page`). Loading label
  `Admin.Booths.Loading`.
- **Presentation toggle** — `CrudPresentationToggle PageKey="booths"
  @bind-Value="_presentation"` in `<CustomToolbar>`; the choice persists in
  `localStorage` under `simf.cp.prefs.booths` via `CpPreferences` and is
  restored in `OnInitializedAsync` (`Prefs.GetPresentationAsync("booths")`).

## Add / Edit form (`BoothsAddEdit`)

`@inherits CrudAddEditFormBase<AdminBoothDetail>`. An `EditForm` with
`class="simf-form"`; `_busy` disables every control while a request is in
flight; `_error` renders a top `SimfAlert`. Fields top → bottom:

| # | Field | Control | `MaxLength` | Label key |
|---|-------|---------|-------------|-----------|
| 1 | Code | `SimfTextField` | 16 | `Admin.Booths.Field.Code` |
| 2 | Name (English) | `SimfTextField` | 128 | `Admin.Booths.Field.NameEn` |
| 3 | Name (Arabic) | `SimfTextField` | 128 | `Admin.Booths.Field.NameAr` |
| 4 | Exhibitor company | `<select>` | — | `Admin.Booths.Field.Exhibitor` (first option `Admin.Booths.Field.ExhibitorNone`; options `{NameEn} — {NameAr}`) |
| 5 | Booth officer name | `SimfTextField` | 256 | `Admin.Booths.Field.OfficerName` |
| 6 | Booth officer phone | `SimfTextField` | 32 | `Admin.Booths.Field.OfficerPhone` |
| 7 | Booth officer email | `SimfTextField` | 320 | `Admin.Booths.Field.OfficerEmail` |
| 8 | Contact | `ContactPicker` | — | (SIMF-FDS-014 / D-283 shared Contact link) |
| 9 | Sector (English) | `SimfTextField` | 128 | `Admin.Booths.Field.SectorEn` |
| 10 | Sector (Arabic) | `SimfTextField` | 128 | `Admin.Booths.Field.SectorAr` |
| 11 | Description (English) | `<textarea rows="3" maxlength="2048">` | 2048 | `Admin.Booths.Field.DescriptionEn` |
| 12 | Description (Arabic) | `<textarea rows="3" maxlength="2048">` | 2048 | `Admin.Booths.Field.DescriptionAr` |
| 13 | Hall | `<select>` | — | `Admin.Booths.Field.HallId` (first option `Admin.Booths.Field.HallNone`; options `{Name} — {NameArabic}`) |
| 14 | Map X position | `<input type="number" step="any">` | — | `Admin.Booths.Field.MapX` |
| 15 | Map Y position | `<input type="number" step="any">` | — | `Admin.Booths.Field.MapY` |
| 16 | Active | `SimfCheckbox` | — | `Admin.Booths.Field.IsActive` — **Edit only** (`@if (IsEdit)`) |

- **Pickers.** The exhibitor + hall `<select>`s load their own active lists via
  `POST /account/api/admin/exhibitors/list` and `.../halls/list` (`Top=500`,
  `IsActive` filter) in the form's own `OnInitializedAsync`. Map X/Y parse with
  `CultureInfo.InvariantCulture`.
- **Submit.** The button label is `Admin.Booths.New.Submit` (Create) /
  `Admin.Booths.Edit.Submit` (Edit), loading label `…New.Submitting` /
  `…Edit.Submitting`. Cancel (`Admin.Booths.Cancel`) renders only when
  `OnCancel.HasDelegate`.
- **Create vs Edit.** `IsEdit=false` → `POST /account/api/admin/booths`
  (`AdminCreateBoothRequest`); `IsEdit=true` → `PUT
  /account/api/admin/booths/{Initial.Id}` (`AdminUpdateBoothRequest`, includes
  `IsActive`). Blank string fields are sent as `null` (`NullIfBlank`).

## View / Delete form (`BoothsViewDelete`)

`@inherits CrudViewDeleteFormBase<AdminBoothDetail>`. A read-only `dl.simf-dl`
listing **Code, Name (EN), Name (AR), Exhibitor (resolved name), Officer name,
Officer phone, Officer email, Sector (EN), Sector (AR), Description (EN),
Description (AR), Hall (resolved name), Map X, Map Y, Active**. Null/blank text
fields render `—`; Map X/Y render with `CultureInfo.InvariantCulture` or `—`.

- **Details mode** (`IsDelete=false`) — only the read-only `dl` + a secondary
  Close button (`Admin.Booths.Details.Close`); no Delete button.
- **Delete mode** (`IsDelete=true`) — adds a red danger Delete button
  (`Admin.Booths.Delete`) that opens a `SimfConfirm` (Danger). The confirm
  message is `string.Format(L["Admin.Booths.Delete.Message"], Initial.Name)`;
  confirm fires `DELETE /account/api/admin/booths/{Initial.Id}` via
  `simfAccount.deleteJson`. **Not** a native `window.confirm` (removed D-353).

## States

- **Loading** — `_loading = true` from first paint (set in `OnInitializedAsync`
  before the first `await`); the grid shows `Admin.Booths.Loading`. `LoadAsync`'s
  `finally` clears it.
- **Empty** — `SimfEmptyState` (`Admin.Booths.None`).
- **List-load error** — a red `_toast` with the server message or the
  `Admin.Booths.LoadFailed` fallback; no rows render.
- **Lookup-load error** — `Admin.Booths.HallsLoadFailed` /
  `Admin.Booths.ExhibitorsLoadFailed` toast (the grid still renders; FK names
  fall back to `—`).
- **Form error** — surfaced inside the form's own `SimfAlert` (`_error`), the
  shell stays open.
- **Success** — green `_toast`: `Admin.Booths.Saved` (create/update),
  `Admin.Booths.Deleted` (delete), `Grid.Import.Done` (import); the grid reloads.

## i18n + RTL

- Every visible string comes from `Strings.resx` (EN) + `Strings.ar.resx` (AR)
  via `IStringLocalizer<Strings> L` — `Admin.Booths.*` keys + the shared
  `Grid.*` keys (toolbar / pager / Excel). The exact resx literals are owned by
  the resource files, not duplicated here.
- Banner title AR **أجنحة المعرض**; grid headers mirror (الرمز / الاسم (إنجليزي) /
  الاسم (عربي) / الشركة / القطاع / القاعة / نشط).
- The `العربية` / `English` toggle sets `<html dir="rtl" lang="ar">`; the nav
  rail mirrors, the grid toolbar + pager reverse, and the `CrudShell` form
  mirrors with the form-action buttons in reversed order.
