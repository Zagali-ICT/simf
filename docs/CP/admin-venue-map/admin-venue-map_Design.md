# Venue map nodes — Design (`/admin/venue-map`)

The as-built Control Panel screen. Source: `VenueMapList.razor` +
`VenueMapAddEdit.razor` + `VenueMapViewDelete.razor` (Blazor Server,
`CpShellLayout`). Bilingual EN/AR, RTL-mirrored. Verified against code this
session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Function](admin-venue-map_Function.md) ·
> [API](admin-venue-map_API.md) · [Logic](admin-venue-map_Logic.md) ·
> existing reference [`docs/pages/cp/admin-venue-map.md`](../../pages/cp/admin-venue-map.md) ·
> E2E [`docs/tests/e2e/cp-admin-venue-map.md`](../../tests/e2e/cp-admin-venue-map.md).

## Layout (top → bottom, as built)

When no CRUD form is open (`GridHidden = false`):

1. **Banner** — `SimfBanner Title="@L["Admin.VenueMap.Title"]"` → EN **Venue map**
   (AR pair from `Strings.ar.resx`). The banner + grid are **hidden** while a form
   is open in full-page mode (`GridHidden => FormOpen && _presentation ==
   CrudPresentation.Page`).
2. **Inline alert** — a `SimfAlert` renders above the grid only when a `_toast` is
   set; `Variant` is `"success"` or `"error"`.
3. **SimfDataGrid** (`TItem="AdminVenueMapNodeSummary"`, D-255 owner-mandated
   list-page standard) — `Multiselect="true"` (select-all + per-row checkbox),
   full pager, quiet per-row icon actions. `RowKey` = `Id.ToString()`, `RowLabel`
   = `Label`, `Caption = Admin.VenueMap.Title`. Its `CustomToolbar` hosts a
   **`CrudPresentationToggle`** bound to `PageKey = "venue-map"` (the D-353
   dialog⇄full-page toggle).
   - Grid action callbacks wired on the grid: `OnAdd` → `OnAddAsync`, `OnEditOne`
     → `OnEditAsync`, `OnDetailsOne` → `OnDetailsAsync`, `OnDeleteOne` →
     `OnDeleteAsync`, `OnExport` → `OnExportAsync`, `OnImport` → `OnImportAsync`.
   - `EmptyTemplate` → `SimfEmptyState Title="@L["Admin.VenueMap.None"]"`
     ("No venue-map nodes yet." per the EN resx pair).
4. **`CrudGridExcel`** — `<CrudGridExcel @ref="_excel" Resource="venue-map"
   OnImported="OnImportedAsync" OnError="OnExcelError" />` renders below the grid;
   it owns the file `<input>` (id `venue-map-import-input`, `accept=".xlsx"`) and
   the import-result modal (D-356).

When a CRUD form is open (`FormOpen = _form != FormKind.None`), the page renders a
**`CrudShell`** (`Presentation="_presentation"`, `CloseLabel =
Admin.VenueMap.Details.Close`) framing either `VenueMapAddEdit` (when
`_form == FormKind.AddEdit`) or `VenueMapViewDelete` (when
`_form == FormKind.ViewDelete`). In dialog mode it is a popup; in page mode the
grid + banner are hidden via `GridHidden`.

The `CrudShell` title (`FormTitle`) is, by branch:
`Add` → `Admin.VenueMap.Add.Title`; `Edit` → `Admin.VenueMap.Edit.Title`;
`Details` → `Admin.VenueMap.Details.Title`; `Delete` → `Admin.VenueMap.Delete.Title`.

## Grid columns (verified)

| Key | Header resx | Sortable | Filterable | Cell render |
|-----|-------------|----------|------------|-------------|
| `label` | `Admin.VenueMap.Col.Label` (**Label**) | ✅ | ✅ | `context.Label` |
| `kind` | `Admin.VenueMap.Col.Kind` (**Kind**) | ✅ | — | `context.Kind` (`VenueMapNodeKind` enum name) |
| `position` | `Admin.VenueMap.Col.Position` (**Position**) | — | — | `context.X.ToString("0.#"), context.Y.ToString("0.#")` |
| `isActive` | `Admin.VenueMap.Col.Active` (**Active**) | — | — | `SimfPill Variant="on"` (`Grid.Active`) / `Variant="off"` (`Grid.Inactive`) |

Only **Label** is per-column filterable; the server maps that filter to
`Filters["label"]` and matches it against **both** `Label` **and** `LabelArabic`
(`VenueMapService.ListAsync`). Only **Label** and **Kind** are sortable (default:
Label ascending). The summary model carries neither the Hall/Booth link nor the
timestamps — those come from the per-id detail fetch.

## Row actions (quiet per-row icons + toolbar)

- **Add** ("New node", `OnAddAsync`) — opens `VenueMapAddEdit` in Create mode
  (`_isEdit = false`, `_target = null`); gated `VenueMap.Create`.
- **Edit** (pencil, `OnEditAsync`) — first calls
  `GET /account/api/admin/venue-map/{id}` (`LoadDetailAsync`) for the full detail,
  then opens `VenueMapAddEdit` in Edit mode (`_isEdit = true`); gated
  `VenueMap.Edit`.
- **Details** (eye, `OnDetailsAsync`) — loads the detail, opens
  `VenueMapViewDelete` read-only (`_isDelete = false`, no Delete button).
- **Delete** (trash, `OnDeleteAsync`) — loads the detail, opens
  `VenueMapViewDelete` with the red Delete button (`_isDelete = true`); gated
  `VenueMap.Delete`.
- **Export** (toolbar, `OnExportAsync`) — `_excel.ExportAsync(selectedIds,
  _query)` → `POST /export` (§Excel); gated `VenueMap.Export`.
- **Import** (toolbar, `OnImportAsync`) — `_excel.TriggerImportAsync()` (§Excel);
  gated `VenueMap.Import`.

The grid action labels come from shared `Grid.*` resx keys (`Grid.Add`,
`Grid.Edit`, `Grid.Details`, `Grid.Delete`, `Grid.Export`, `Grid.Import`, plus the
pager keys).

## Add / Edit form (`VenueMapAddEdit`)

`@inherits CrudAddEditFormBase<AdminVenueMapNodeDetail>` — an `EditForm` over a
private `Model`. Two text fields, an enum dropdown, two number inputs, two
optional pickers, and an Active checkbox (Edit only).

| Field | resx label | Required | UI cap / control | Notes |
|-------|------------|----------|------------------|-------|
| Label (English) | `Admin.VenueMap.Field.Label` | **yes** | `SimfTextField`, `MaxLength="128"` | trimmed; 1–128 server-side |
| Label (Arabic) | `Admin.VenueMap.Field.LabelArabic` | **yes** | `SimfTextField`, `MaxLength="128"` | trimmed; 1–128 server-side |
| Kind | `Admin.VenueMap.Field.Kind` | yes | `<select>` over `Enum.GetValues<VenueMapNodeKind>()` | options Hall / Zone / Booth / PointOfInterest (in enum order); default `Hall` (=0); on an out-of-range value `OnKindChanged` falls back to `Hall` |
| X position | `Admin.VenueMap.Field.X` | no | `<input type="number" step="0.1">` | `OnXChanged` → `double.TryParse` else `0` |
| Y position | `Admin.VenueMap.Field.Y` | no | `<input type="number" step="0.1">` | `OnYChanged` → `double.TryParse` else `0` |
| Linked hall (optional) | `Admin.VenueMap.Field.Hall` | no | `<select>` | first option `Admin.VenueMap.None.Option` ("— None —", value `""`), then one option per active hall showing `h.Name`; bound to `_model.HallId` (string) |
| Linked booth (optional) | `Admin.VenueMap.Field.Booth` | no | `<select>` | first option `Admin.VenueMap.None.Option`, then one option per active booth showing `b.Name`; bound to `_model.BoothId` (string) |
| Active | `Admin.VenueMap.Field.IsActive` | (Edit only) | `SimfCheckbox` | shown only when `IsEdit`; defaults `true` |

- **Picker load:** `OnInitializedAsync` → `LoadPickersAsync` posts
  `POST /account/api/admin/halls/list` and `POST /account/api/admin/booths/list`
  each with `new GridQuery { Top = 500 }`, into `_halls` / `_booths`. (The Edit
  path pre-fills `_model` from `Initial` before loading the pickers.)
- **Submit guard (client):** a blank/whitespace **English or Arabic** label shows
  an inline `SimfAlert Variant="error"` reading `Admin.VenueMap.Required` and
  **does not POST**. On submit the labels are `.Trim()`-ed and the optional
  Hall/Booth strings are `Guid.TryParse`d (a non-guid / `""` → `null`).
- **Buttons:** Save — `Admin.VenueMap.New.Submit` (Create) / `Admin.VenueMap.Edit.Submit`
  (Edit), with `Loading="_busy"` and a busy label (`…New.Submitting` /
  `…Edit.Submitting`) — + Cancel (`Admin.VenueMap.Cancel`, secondary, when
  `OnCancel.HasDelegate`).
- **Server error:** a non-success envelope (or a thrown exception) surfaces
  `env.Error.MessageForCurrentCulture()` (bilingual) — falling back to
  `Admin.VenueMap.Fallback` — in the top `SimfAlert`; the form stays open.

## View / Delete form (`VenueMapViewDelete`)

`@inherits CrudViewDeleteFormBase<AdminVenueMapNodeDetail>` — a read-only `dl`
(`simf-dl`) of: Label, Label (Arabic), Kind, Position (`X("0.#"), Y("0.#")`),
**Hall** (resolved name), **Booth** (resolved name), Active
(`Grid.Active` / `Grid.Inactive`).

- **Link-name resolution:** `OnInitializedAsync` fetches the hall list **only**
  when `Initial.HallId is not null`, and the booth list **only** when
  `Initial.BoothId is not null` (each via `…/list` with `Top = 500`). `HallName` /
  `BoothName` look the linked id up in that list and show the name, falling back to
  the raw guid if absent, or `"—"` when there is no link.
- **Read-only (Details):** when `IsDelete = false`, only a **Close** button
  (`Admin.VenueMap.Details.Close`) — no Delete.
- **Delete:** when `IsDelete = true`, a red **Delete** button
  (`Admin.VenueMap.Delete`) + Close.
- **Confirm gate:** clicking Delete opens a **`SimfConfirm`** (`Danger=true`)
  titled `Admin.VenueMap.Delete.Title`, message
  `string.Format(Admin.VenueMap.Delete.Message, Initial.Label)` (the row's English
  label), confirm label `Admin.VenueMap.Delete`, cancel label
  `Admin.VenueMap.Cancel`. Cancel → no DELETE. Confirm →
  `DELETE /account/api/admin/venue-map/{id}` (`simfAccount.deleteJson`). A
  `SimfConfirm` replaces the pre-D-353 native `confirm()`. On a failed delete the
  confirm closes first so the bilingual error (or `Admin.VenueMap.Fallback`) lands
  on the visible form body.

## Excel import / export (`CrudGridExcel`, generic D-356 grid layer)

Both Export and Import run through the shared `CrudGridExcel` component
(`Resource="venue-map"`), backed by `VenueMapExcelEndpoints.cs`:

- **Export** (`OnExportAsync`) → `_excel.ExportAsync(selectedIds, _query)` →
  `POST /account/api/admin/venue-map/export` carrying `AdminGridExportRequest
  { Ids, Query }`. With rows selected only those `Ids`; with none selected the
  current `_query` (whole filtered grid). Workbook prefix `simf-venue-map`, sheet
  **"VenueMap"**, header
  `Label | LabelArabic | Kind | X | Y | Hall | Booth | IsActive`. **Kind** is
  written as its enum name; **Hall** / **Booth** are written as the linked record's
  human-readable **code** (empty cell when the link was deactivated / unresolved).
- **Import** (`OnImportAsync`) → `_excel.TriggerImportAsync()` opens the file
  picker (`venue-map-import-input`, `accept=".xlsx"`) → `POST
  /account/api/admin/venue-map/import` (multipart, field `file`). The sheet must be
  **"VenueMap"** with required headers `Label | LabelArabic | Kind`. Import is
  **insert-only** — every applied row is a `Created`. On success `OnImportedAsync`
  shows a green toast `Grid.Import.Done` and reloads the grid; the result modal
  shows the `{Created}/{Updated}/{Skipped}` tallies + per-row errors. On failure
  `OnExcelError` sets a red `_toast`.

## Page ⇄ Popup presentation toggle (D-353)

The grid's `CustomToolbar` hosts a `CrudPresentationToggle` (`@bind-Value=
"_presentation"`, `PageKey = "venue-map"`). `OnInitializedAsync` reads the saved
choice via `Prefs.GetPresentationAsync("venue-map")` (defaults to `Dialog`). In
**dialog** mode the CRUD forms open as a `CrudShell` popup; in **page** mode
`GridHidden` hides the grid + banner and `CrudShell` frames the form full-page.
The choice persists in `localStorage` via `CpPreferences` (`simf.cp.prefs.venue-map`).

## States

- **Loading** — `_loading` is set while `LoadAsync` runs; the grid shows its
  loading text (`Admin.VenueMap.Loading`).
- **Empty / no-match** — `SimfEmptyState` titled `Admin.VenueMap.None` ("No
  venue-map nodes yet."); no error alert; the toolbar (incl. "New node" when
  permitted) stays visible. **This is the default first render** — the table ships
  empty.
- **List failure** — a non-success `/list` envelope sets an error `_toast`
  (`env.Error.MessageForCurrentCulture()` or fallback `Admin.VenueMap.LoadFailed`).
- **Save / delete success** — green toast `Admin.VenueMap.Saved` (save) /
  `Admin.VenueMap.Deleted` (delete); the form closes (`CloseForm`) and the grid
  reloads.

## Bilingual / RTL behaviour

- Every label, column header, button, modal title and toast comes from the
  `Admin.VenueMap.*` resx keys (EN `Strings.resx` / AR `Strings.ar.resx`) + shared
  `Grid.*` keys — no hardcoded UI text in the razor (only the `—` em-dash glyph in
  the View form and the `0.#` numeric format).
- Under `<html dir="rtl" lang="ar">` the whole page, grid, both CRUD forms and the
  Excel modal mirror; the bilingual server error/confirm strings render in the
  active culture via `MessageForCurrentCulture()`.
- The **English** label is the grid's `RowLabel` and the `SimfConfirm` subject, but
  **both** labels are required and the default grid sort is by `Label` (see
  [Logic](admin-venue-map_Logic.md)).

> **Known EN resx gap (defect, reported only).** The existing reference doc
> records that several D-353 keys (`Admin.VenueMap.Delete.Title`, `.Delete.Message`,
> `.Details.Title`, `.Details.Close`, `.New.Submit`, `.New.Submitting`,
> `.Edit.Submit`, `.Edit.Submitting`, `.Fallback`, `.Col.Hall`, `.Col.Booth`) exist
> in `Strings.ar.resx` but are missing from `Strings.resx` (EN), so the EN UI falls
> back to the resource-key name for those. Not verified key-by-key this session;
> carried forward from `docs/pages/cp/admin-venue-map.md` §7. Out of scope here.

> The exact Arabic resx phrasing lives in `Strings.ar.resx` and is described, not
> quoted, here (the server-thrown bilingual error/Arabic strings that ARE read
> verbatim from code appear in [API](admin-venue-map_API.md) / [Logic](admin-venue-map_Logic.md)).
