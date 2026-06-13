# CP Content blocks — Design (`/admin/content-blocks`)

Control Panel page design. RTL-aware, bilingual (EN/AR). Source of truth:
`ContentBlocksList.razor` + the two reusable forms `ContentBlockAddEdit.razor`
and `ContentBlockViewDelete.razor`. Every element below is read from those files.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Frame
- `@page "/admin/content-blocks"`, `@layout CpShellLayout`,
  `@attribute [RequirePermission(PermissionCatalog.ContentBlocks.View)]`.
- `<PageTitle>` = `@L["Admin.ContentBlocks.Title"]` + " · SIMF".
- The whole grid surface is wrapped in `@if (!GridHidden)` — in **full-page**
  presentation, while a form is open the grid + banner are hidden (`GridHidden`
  is `true` only when a form is open **and** `_presentation == CrudPresentation.Page`).

## Layout (top → bottom, as built)
1. **`SimfBanner`** — `Title="@L["Admin.ContentBlocks.Title"]"` (EN "Content
   blocks" / AR "كتل المحتوى"). Inside `div.simf-page-wide > div.simf-surface`.
2. **Toast** — an optional `<SimfAlert Variant="@_toast.Variant">` above the
   grid; carries the success/error message (`_toast`, a `(Variant, Message)`
   record). Cleared (`_toast = null`) when a form opens.
3. **`SimfDataGrid`** (`TItem="AdminContentBlockSummary"`) — the standard grid
   (D-255 migration from a raw table):
   - `Query="_query"` (`new GridQuery { Top = 20 }`) → up to **20 rows/page**
     with the standard `Prev` / `Next` / `First` / `Last` pager.
   - `Multiselect="true"` (select-all + per-row checkboxes). Selection feeds
     **only** the Excel export's selected-ids path — there is no bulk action.
   - `RowKey = r => r.Id.ToString()`, `RowLabel = r => r.Key`.
   - Toolbar callbacks: `OnAdd`, `OnEditOne`, `OnDetailsOne`, `OnDeleteOne`,
     `OnExport`, `OnImport`.
   - **CustomToolbar** hosts the `<CrudPresentationToggle PageKey="@PageKey"
     @bind-Value="_presentation" />` (`PageKey = "content-blocks"`).
4. **`CrudGridExcel`** — `@ref="_excel" Resource="content-blocks"`,
   `OnImported="OnImportedAsync"`, `OnError="OnExcelError"`. Renders the hidden
   import file input + drives export/import (D-356).
5. **`CrudShell`** (rendered under `@if (FormOpen)`) — frames the active form as
   a **dialog or full page** per `_presentation`. `Title="@FormTitle"`,
   `CloseLabel="@L["Admin.ContentBlocks.Details.Close"]"`, `OnClose="CloseForm"`.
   Hosts one of the two reusable forms.

## Grid columns
| Key (`SimfDataGridColumn.Key`) | Header (resx) | Cell render | Sortable | Filterable |
|--------------------------------|---------------|-------------|----------|------------|
| `key` | `Admin.ContentBlocks.Col.Key` | `<code>@context.Key</code>` | yes | yes |
| `content` | `Admin.ContentBlocks.Col.ContentEn` | `TruncatePreview(context.Content)` — > 80 chars → first 80 + "…" | yes | yes |
| `lastUpdatedAt` | `Admin.ContentBlocks.Col.LastUpdatedAt` | `context.LastUpdatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'")` | yes | no |
| `isActive` | `Admin.ContentBlocks.Col.Active` | `SimfPill Variant="on"` (`Grid.Active`) / `Variant="off"` (`Grid.Inactive`) | no | no |

> **Filter-column-key drift note:** the grid's English column declares
> `Key="content"`, and `AdminCmsService.ListContentBlocksAsync` maps the
> per-column filter case `"content"` to `b.Content.Contains(v)`. (The existing
> CP-ref + E2E docs call this filter key `contentEn`; the live grid + service
> agree on **`content`**.) See the API + Logic docs.

- **Empty template:** `<SimfEmptyState Title="@L["Admin.ContentBlocks.None"]" />`
  (EN/AR "No content blocks…").

## The two reusable forms (CrudShell-hosted)

### `ContentBlockAddEdit` (Add + Edit — one upsert)
`@inherits CrudAddEditFormBase<AdminContentBlockSummary>`. Bound to a private
`Model { Key, Content, ContentArabic, IsActive=true }`.
- **Fields** (each a `SimfTextField`, plus a `SimfCheckbox`):
  | Field | Label (resx) | MaxLength | Disabled when |
  |-------|--------------|-----------|---------------|
  | Key | `Admin.ContentBlocks.Field.Key` | **128** | `_busy \|\| IsEdit` (locked on Edit) |
  | Content (English) | `Admin.ContentBlocks.Field.ContentEn` | (none on field) | `_busy` |
  | Content (Arabic) | `Admin.ContentBlocks.Field.ContentAr` | (none on field) | `_busy` |
  | Active | `Admin.ContentBlocks.Field.IsActive` (checkbox) | bool, default ticked | `_busy` |
- **Actions:** primary `SimfButton` (`Admin.ContentBlocks.Submit`,
  `Loading="_busy"`); a secondary `SimfButton` (`Admin.ContentBlocks.Cancel`)
  only when `OnCancel.HasDelegate`.
- **Pre-fill:** on `OnInitialized`, if `Initial` is set, the model copies
  `Key/Content/ContentArabic/IsActive` from the grid row — **no detail fetch**.
- **Error:** `@if (_error is not null)` → `<SimfAlert Variant="error">@_error</SimfAlert>`
  at the top; the form stays open.

### `ContentBlockViewDelete` (Details + Delete)
`@inherits CrudViewDeleteFormBase<AdminContentBlockSummary>`. Read-only
**always**; the Delete button appears only when `IsDelete`.
- **Details** — a `<dl class="simf-dl">` description list of: Key
  (`<code>`), Content (English), Content (Arabic), Last updated
  (`yyyy-MM-dd HH:mm 'UTC'`), Active (`Grid.Active` / `Grid.Inactive`).
- **Actions:** when `IsDelete`, a `SimfButton Variant="danger"`
  (`Admin.ContentBlocks.Action.Delete`) that sets `_confirming = true`; always a
  secondary `SimfButton` (`Admin.ContentBlocks.Details.Close`).
- **Confirm gate:** a `<SimfConfirm Danger="true">` titled
  `Admin.ContentBlocks.Delete.Title`, message
  `string.Format(L["Admin.ContentBlocks.Delete.Message"], Initial.Key)` (the
  **Key** is interpolated), confirm `Admin.ContentBlocks.Action.Delete`, cancel
  `Admin.ContentBlocks.Cancel`. Only `OnConfirm` fires the DELETE.

## Form titles (`FormTitle`)
| Form kind | Title (resx) |
|-----------|--------------|
| AddEdit, create | `Admin.ContentBlocks.Add.Title` |
| AddEdit, edit | `Admin.ContentBlocks.Edit.Title` |
| ViewDelete, details | `Admin.ContentBlocks.Details.Title` |
| ViewDelete, delete | `Admin.ContentBlocks.Delete.Title` |

## States
- **Loading** — `_loading` drives the grid's `LoadingLabel`
  (`Admin.ContentBlocks.Loading`); set around the `/list` call.
- **Empty** — `SimfEmptyState` (`Admin.ContentBlocks.None`) in the grid body.
- **List load failure** — `_toast` error = `env.Error.MessageForCurrentCulture()`
  ?? `Admin.ContentBlocks.LoadFailed`; no rows render.
- **Save success** — `OnSavedAsync` closes the form, sets `_toast` success
  `Admin.ContentBlocks.Saved`, reloads the grid.
- **Delete success** — `OnDeletedAsync` closes the form, sets `_toast` success
  `Admin.ContentBlocks.Deleted`, reloads.
- **Form-level error** — surfaced inside the form's own `SimfAlert`; the form
  stays open.
- **Excel error** — `OnExcelError` sets `_toast` error from `CrudGridExcel`.
- **Excel import done** — `OnImportedAsync` sets `_toast` success
  `Grid.Import.Done` and reloads.

## Presentation toggle (D-353)
- `_presentation` defaults to `CrudPresentation.Dialog`; restored in
  `OnInitializedAsync` via `Prefs.GetPresentationAsync(PageKey)`
  (`PageKey = "content-blocks"`).
- The `CrudPresentationToggle` persists the choice to `localStorage`
  (key `simf.cp.prefs.content-blocks` per `CpPreferences`).
- **Page** presentation → `GridHidden` hides the grid + banner while a form is
  open; **Dialog** presentation → the form is a popup over the grid.

## RTL / localization
- Mirrors RTL under Arabic culture (nav rail, banner, grid, forms).
- All labels come from `Admin.ContentBlocks.*` (`Strings.resx` / `Strings.ar.resx`)
  plus the shared `Grid.*` keys for the toolbar/pager/Excel/select labels.
- The delete-confirm message interpolates the **Key** (untranslated slug) into
  the localized template.

## Verified strings (EN)
Title "Content blocks"; column headers Key / English / Last updated / Active;
field labels Key / Content (English) / Content (Arabic) / Active; the
`SimfPill` on/off via `Grid.Active` / `Grid.Inactive`; toolbar Add/Edit/
Details/Delete/Export/Import via the shared `Grid.*` keys; toasts via
`Admin.ContentBlocks.Saved` / `.Deleted`. (Arabic equivalents live in
`Strings.ar.resx` — not transcribed here; this set did not read the resx files,
so the AR glosses above for the banner/headers are carried from the existing
CP-ref doc, not re-verified from source.)
