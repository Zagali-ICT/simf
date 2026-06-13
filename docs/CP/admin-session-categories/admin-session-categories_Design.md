# CP Session categories — Design (`/admin/session-categories`)

Control Panel screen design. As built on the canonical `SimfDataGrid`
(owner-mandated list-page standard, D-256) with `CrudShell` dialog/full-page
framing (D-353) and Excel toolbar actions (D-356). Source:
`SessionCategoriesList.razor` (+ `SessionCategoriesAddEdit.razor` /
`SessionCategoriesViewDelete.razor`). Layout `CpShellLayout`.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Layout (top → bottom, as built)

1. **Banner** — `SimfBanner` with title `Admin.SessionCategories.Title`. Hidden
   when a form is open in full-page presentation (`GridHidden`).
2. **Surface** (`.simf-page-wide` → `.simf-surface`) holding:
   - **Inline alert** (`SimfAlert`, when `_toast` is set) above the grid.
   - **Data grid** (`SimfDataGrid TItem="AdminSessionCategorySummary"`).
3. **CrudShell** (overlay or full-page frame) when a form is open.

## Grid

- **Toolbar:** **Add** · **Export** · **Import** (built-in grid actions) +
  `CustomToolbar` hosting `<CrudPresentationToggle PageKey="session-categories"
  @bind-Value="_presentation" />`. Labels from `Grid.Add` / `Grid.Export` /
  `Grid.Import`.
- **Multiselect:** select-all header checkbox + per-row checkboxes
  (`RowKey = r.Id`, `RowLabel = r.Name`). No bulk-action button (selection feeds
  Export only).
- **Columns:**

  | Header (resx) | Key | Sortable | Filterable | Cell |
  |---------------|-----|----------|------------|------|
  | `Admin.SessionCategories.Col.NameEn` | `name` | yes | yes | `@context.Name` |
  | `Admin.SessionCategories.Col.NameAr` | `namearabic` | yes | yes | `@context.NameArabic` |
  | `Admin.SessionCategories.Col.Order` | `order` | yes | no | `@context.DisplayOrder` |
  | `Admin.SessionCategories.Col.Active` | `isActive` | yes | no | `SimfPill` `on`=`Grid.Active` / `off`=`Grid.Inactive` |

- **Per-row actions (quiet affordances):** Edit (pencil) · Details (eye) ·
  Delete (trash) — `OnEditOne` / `OnDetailsOne` / `OnDeleteOne`. Labels
  `Grid.Edit` / `Grid.Details` / `Grid.Delete`; actions column header
  `Grid.Actions`.
- **Pager:** numbered, with First / Prev / Next / Last + page-size + a summary
  (`Admin.SessionCategories.Summary` formatted `skip+1 … skip+taken … total`;
  page formatter `Grid.Page`). Loading label
  `Admin.SessionCategories.Loading`.
- **Empty template:** `SimfEmptyState` titled `Admin.SessionCategories.None`
  ("No session categories yet." / "لا توجد تصنيفات جلسات بعد.") — the **default
  first render** while the table is empty (OI-2).
- **Excel host:** `<CrudGridExcel @ref="_excel" Resource="session-categories"
  OnImported=… OnError=… />` sits under the grid (hidden file input
  `session-categories-import-input`, accept `.xlsx`).

## CrudShell + forms

When a form is open, `CrudShell` frames it as a **dialog** or **full page**
(`Presentation="_presentation"`), titled per `FormTitle`:

| State | Title resx | Form |
|-------|-----------|------|
| Add | `Admin.SessionCategories.Add.Title` | `SessionCategoriesAddEdit` (`IsEdit=false`) |
| Edit | `Admin.SessionCategories.Edit.Title` | `SessionCategoriesAddEdit` (`IsEdit=true`) |
| Details | `Admin.SessionCategories.Details.Title` | `SessionCategoriesViewDelete` (`IsDelete=false`) |
| Delete | `Admin.SessionCategories.Delete.Title` | `SessionCategoriesViewDelete` (`IsDelete=true`) |

Close label `Admin.SessionCategories.Details.Close`.

### Add / Edit form (`SessionCategoriesAddEdit`)

`EditForm` (`.simf-form`) over `.simf-form__fields`:
- **Name (English)** — `SimfTextField`, `MaxLength="128"`, label
  `Admin.SessionCategories.Field.NameEn`.
- **Name (Arabic)** — `SimfTextField`, `MaxLength="128"`, label
  `Admin.SessionCategories.Field.NameAr`.
- **Display order** — `SimfTextField Type="number"`, bound to a string
  (`_displayOrderInput`, default `"0"`), label
  `Admin.SessionCategories.Field.DisplayOrder`.
- **Active** — `SimfCheckbox` (**rendered only when `IsEdit`**), label
  `Admin.SessionCategories.Field.IsActive`. On Add the model defaults
  `IsActive = true`.

Actions (`.simf-form__actions`): a primary **Save**
(`Admin.SessionCategories.Save`, with loading state) + a secondary **Cancel**
(`Admin.SessionCategories.Cancel`, shown when `OnCancel` is wired). An
in-form `SimfAlert` (variant `error`) shows the validation / load-failure
message (`Admin.SessionCategories.Required` for the client guard). All inputs
disabled while `_busy`.

### View / Delete form (`SessionCategoriesViewDelete`)

A read-only `<dl class="simf-dl">` of the four fields (Name En / Name Ar /
Order / Active — Active shows `Grid.Active` or `Grid.Inactive`). Actions:
- when `IsDelete` → a **danger** `SimfButton` **Deactivate**
  (`Admin.SessionCategories.Action.Deactivate`) that opens a **`SimfConfirm`**
  (title `Admin.SessionCategories.Delete.Title`, message
  `Admin.SessionCategories.Delete.Message` formatted with the row name,
  confirm = Deactivate, cancel = `Admin.SessionCategories.Cancel`,
  `Danger="true"`);
- always a secondary **Close** (`Admin.SessionCategories.Details.Close`).
An in-form `SimfAlert` shows a delete failure (the confirm closes first so the
error lands on the visible body).

## States

- **Loading** — `_loading` drives the grid's loading state
  (`Admin.SessionCategories.Loading`).
- **Empty** — `SimfEmptyState` (`Admin.SessionCategories.None`); the default
  first render (OI-2).
- **Populated** — paged rows; deactivated rows remain with the `off` pill.
- **Error (list)** — red `SimfAlert` toast above the grid
  (`Error.MessageForCurrentCulture()` or `Admin.SessionCategories.LoadFailed`);
  no rows.
- **Form busy** — Save / Deactivate buttons show loading; inputs disabled.
- **Confirm** — `SimfConfirm` overlay names the row before the destructive call.

## RTL / localization

Whole page mirrors under `<html dir="rtl">` in Arabic (banner, grid headers,
toolbar, pager, forms, confirm). Every label is a `Admin.SessionCategories.*` or
shared `Grid.*` resx key with EN ↔ AR parity. Both names are stored + displayed
bilingually; server messages are bilingual via `ApiException`.

## Notes

- No raw `<table>` — the page uses the canonical `SimfDataGrid` per the
  owner-mandated CP list-page standard (D-256).
- No inline styles / no native `confirm()` — delete runs through the in-page
  `SimfConfirm` Blazor component (D-353), so a Chrome DevTools MCP run needs no
  `handle_dialog` pre-arming.
- Presentation (dialog vs full page) persists in `localStorage`
  `simf.cp.prefs.session-categories`.

## Cross-links

- What the admin does: [admin-session-categories_Function.md](admin-session-categories_Function.md)
- Contract: [admin-session-categories_API.md](admin-session-categories_API.md)
- Behaviour / validation / audit: [admin-session-categories_Logic.md](admin-session-categories_Logic.md)
- CP reference: [`docs/pages/cp/admin-session-categories.md`](../../pages/cp/admin-session-categories.md)
- E2E: [`docs/tests/e2e/cp-admin-session-categories.md`](../../tests/e2e/cp-admin-session-categories.md)
- Consumer (app agenda): [`docs/App/Page_016/Page_016_Design.md`](../../App/Page_016/Page_016_Design.md)
