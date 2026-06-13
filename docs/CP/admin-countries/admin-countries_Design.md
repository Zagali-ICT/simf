# CP — Design (البلدان · Countries `/admin/countries`)

Control Panel screen design. The page is a Blazor Server page on `CpShellLayout`,
rendered through the canonical SIMF CP component set (`SimfBanner`, `SimfDataGrid`,
`CrudShell`, `SimfAlert`, `SimfPill`, `SimfEmptyState`, `SimfConfirm`). RTL under
the Arabic locale. Source: `CountriesList.razor`, `CountryAddEdit.razor`,
`CountryViewDelete.razor`.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

## Layout (top → bottom, as built)
1. **Banner** — `SimfBanner Title="@L["Admin.Countries.Title"]"` (AR **البلدان** /
   EN **Countries**), wrapped in `simf-page-wide` → `simf-surface`.
2. **Toast slot** — a `SimfAlert` (`Variant="success|error"`) above the grid when
   `_toast` is set.
3. **Data grid** — a full-width `SimfDataGrid` with the canonical toolbar
   (Select-all + Add + Export + Import) and the `CrudPresentationToggle` in the
   `CustomToolbar` slot. Seven columns (see below) + a quiet per-row icon action
   cluster (Edit / Details / Deactivate).
4. **Form host** — when `FormOpen`, a `CrudShell` overlays (dialog) or replaces
   (full page) the surface, framing `CountryAddEdit` or `CountryViewDelete`.

## Grid columns (render order)
| # | Header (resx) | Content | Sort key |
|---|---------------|---------|----------|
| 1 | `…Column.Id` | ISO numeric id | `id` |
| 2 | `…Column.Code` | ISO alpha-2 code | `code` (filterable) |
| 3 | `…Column.NameEn` | English name | `name` |
| 4 | `…Column.NameAr` | Arabic name | — |
| 5 | `…Column.PhonePrefix` | dial code or `—` | — |
| 6 | `…Column.DisplayOrder` | display order | `displayOrder` |
| 7 | `…Column.Active` | `SimfPill` `on` (Yes) / `off` (No) | — |

- **Active pill**: `SimfPill Variant="on"` → `Admin.Countries.Active.Yes`;
  `Variant="off"` → `…Active.No`.
- **Empty**: `SimfEmptyState Title="@L["Admin.Countries.None"]"` in the grid body.
- **Loading**: the grid's `Loading="_loading"` indicator with label
  `Admin.Countries.Loading`.

## Add / Edit form (`CountryAddEdit`)
- An `EditForm class="simf-form"` with a `simf-form__fields` block of `SimfTextField`s
  (and, in Edit, a `SimfCheckbox`), then a `simf-form__actions` block (submit +
  optional cancel).
- **Field order**: Id (number) · Code (max 2) · Name EN (max 128) · Name AR (max 128)
  · Phone prefix (max 8) · Display order (number) · [Edit only] Active checkbox.
- **Id field** is disabled in Edit (`Disabled="@(_busy || IsEdit)"`); its helper text
  switches `Admin.Countries.Field.IdHint` (Add) → `…Field.IdReadOnly` (Edit).
- **Submit** button: `Admin.Countries.New.Submit` / `…Edit.Submit` with
  `Loading="_busy"` and the matching `…Submitting` label.
- **Inline error**: a top-of-form `SimfAlert Variant="error"` shows `_error`
  (`Error.MessageForCurrentCulture()` or `Admin.Countries.Fallback`).

## View / Delete form (`CountryViewDelete`)
- A `dl class="simf-dl"` description list of all seven fields (Id, Code, Name EN,
  Name AR, Dial code `—`-fallback, Display order, Active Yes/No).
- **Details** mode: read-only list + secondary **Close** button
  (`Admin.Countries.Details.Close`).
- **Deactivate** mode: a danger button (`Admin.Countries.Action.Deactivate`) opens a
  `SimfConfirm` (title `…Delete.Title`, message `…Delete.Message` with the name,
  `Danger="true"`, `ConfirmLabel="…Action.Deactivate"`, `CancelLabel="…Cancel"`).

## CrudShell presentation (D-353)
- `CrudShell Open Presentation="_presentation" Title="@FormTitle"
  CloseLabel="@L["Admin.Countries.Details.Close"]"`.
- `FormTitle` resolves to `…Add.Title` / `…Edit.Title` / `…Details.Title` /
  `…Delete.Title`.
- **Dialog** mode: the form floats over the grid. **Page** mode: `GridHidden` hides
  the grid and the form takes the surface; closing returns to the grid.
- The toggle (`CrudPresentationToggle PageKey="countries"`) persists the choice via
  `CpPreferences` (`Prefs.GetPresentationAsync("countries")` in `OnInitializedAsync`).

## States
- **Loading** — grid loading indicator while `POST /list` runs.
- **Empty** — `SimfEmptyState` (`Admin.Countries.None`); the Add toolbar button stays.
- **Error (load)** — error toast `Error.MessageForCurrentCulture()` ?? `…LoadFailed`;
  no rows render.
- **Error (form)** — inline `SimfAlert error` in the form (`…Fallback` on exception).
- **Success** — green toast `…Created` / `…Updated` / `…Deactivated` (formatted with
  the country name), then the grid reloads.

## i18n / RTL
- All visible strings are resx keys under `Admin.Countries.*` (title, columns, field
  labels/helpers/validation, pager, actions, toasts) plus the shared `Grid.Export` /
  `Grid.Import` / `Grid.Import.Done` keys. EN ↔ AR parity expected.
- Server validation / conflict / not-found messages are themselves **bilingual**; the
  CP renders the culture-appropriate side via `MessageForCurrentCulture()`.
- Under the Arabic locale the page mirrors to **RTL** — grid headers, toolbar, pager
  arrows, and the hosted Add / Edit / View forms.

## Relationship to the app screen it feeds
This CP page is the **editor**; the app's **Page 007** nationality picker is the
**consumer**. The CP grid's **Display order** column sets the picker order, the
**Active** pill controls picker visibility, and the **Code / Name / Name (Arabic)**
columns are exactly the `code` / `name` / `nameArabic` the picker renders. See the
app screen design in `docs/App/Page_007/Page_007_Design.md` (the searchable
nationality sheet defaulting to Saudi Arabia).

## Cross-links
- CP reference doc: [`../../pages/cp/admin-countries.md`](../../pages/cp/admin-countries.md)
- CP E2E catalogue: [`../../tests/e2e/cp-admin-countries.md`](../../tests/e2e/cp-admin-countries.md)
- Consuming app page: [`../../App/Page_007/Page_007_Design.md`](../../App/Page_007/Page_007_Design.md)
