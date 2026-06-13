# CP — Function (البلدان · Countries `/admin/countries`)

What the admin does on this page — every element, action, and outcome. Grounded
in `CountriesList.razor`, `CountryAddEdit.razor`, `CountryViewDelete.razor`.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

## Purpose
Maintain the **country / nationality** reference list. Each row is one country
carrying its ISO 3166-1 numeric id (the primary key), ISO alpha-2 `Code`, a
bilingual name (`Name` / `NameArabic`), an optional dial-code `PhonePrefix`, a
`DisplayOrder`, and the soft-delete `IsActive` flag. This list feeds the app's
nationality picker (Page 007) and the speaker country field.

## Page elements (top → bottom)
1. **Banner** — `SimfBanner` titled `Admin.Countries.Title` (AR **البلدان** /
   EN **Countries**), inside a `simf-page-wide` → `simf-surface` shell.
2. **Toast** — a `SimfAlert` (`_toast`) surfaces success / error messages above
   the grid (variant `success` or `error`).
3. **Grid** — a `SimfDataGrid` (`TItem="AdminCountrySummary"`, `Multiselect="true"`,
   `RowKey = r.Id.ToString()`, `RowLabel = r.Name`) with the canonical toolbar and
   per-row icon actions.
4. **Custom toolbar slot** — a `CrudPresentationToggle` bound to `PageKey="countries"`
   (the Page ↔ Popup toggle, D-353).
5. **Excel component** — a `CrudGridExcel` (`Resource="countries"`) wired to the
   toolbar Export / Import actions (rendered inside the surface, D-356).
6. **CrudShell** — when a form is open, `CrudShell` frames `CountryAddEdit` or
   `CountryViewDelete` as a **dialog or a full page** per `_presentation`. In
   full-page mode the grid is hidden (`GridHidden`).

## Grid columns (verified in `CountriesList.razor`)
| Column | resx header key | Cell | Sortable | Filterable |
|--------|-----------------|------|----------|------------|
| Id (ISO numeric) | `Admin.Countries.Column.Id` | `@context.Id` | yes (`id`) | — |
| Code (ISO alpha-2) | `Admin.Countries.Column.Code` | `@context.Code` | yes (`code`) | yes |
| Name (English) | `Admin.Countries.Column.NameEn` | `@context.Name` | yes (`name`) | — |
| Name (Arabic) | `Admin.Countries.Column.NameAr` | `@context.NameArabic` | — | — |
| Dial code | `Admin.Countries.Column.PhonePrefix` | `@(context.PhonePrefix ?? "—")` | — | — |
| Display order | `Admin.Countries.Column.DisplayOrder` | `@context.DisplayOrder` | yes (`displayOrder`) | — |
| Active | `Admin.Countries.Column.Active` | `SimfPill on/off` (`Admin.Countries.Active.Yes/No`) | — | — |

- **Empty result** → `SimfEmptyState` titled `Admin.Countries.None`.
- **Missing dial code** renders as the em dash `—`.
- Default query `Top = 50`. Summary/pager strings come from
  `Admin.Countries.Summary` / `Admin.Countries.Pager.*`.

## Toolbar + per-row actions
| Action | Wiring (`CountriesList.razor`) | Opens / does |
|--------|--------------------------------|--------------|
| Select all / Select row | `SimfDataGrid` multiselect | row selection (does not narrow Export — see Logic) |
| Add | `OnAdd="OnAddAsync"` | opens `CountryAddEdit` in **Add** mode (`_isEdit=false`, `_target=null`) |
| Edit (per row) | `OnEditOne="OnEditAsync"` | `GET /{id}` → opens `CountryAddEdit` in **Edit** mode pre-filled |
| Details (per row) | `OnDetailsOne="OnDetailsAsync"` | `GET /{id}` → opens `CountryViewDelete` read-only (`_isDelete=false`) |
| Deactivate (per row) | `OnDeleteOne="OnDeleteAsync"` | `GET /{id}` → opens `CountryViewDelete` with the Deactivate button (`_isDelete=true`) |
| Export | `OnExport="OnExportAsync"` | `_excel.ExportAsync(Array.Empty<Guid>(), _query)` — always the **current filtered grid** |
| Import | `OnImport="OnImportAsync"` | `_excel.TriggerImportAsync()` — opens the `.xlsx` file picker |
| Presentation toggle | `CrudPresentationToggle` | Dialog ↔ full-page; persisted via `CpPreferences` |

## The Add / Edit form (`CountryAddEdit.razor`)
Fields, in render order. Client guards live in `HandleSubmitAsync`; the matching
server validation is in `AdminCountryService.Validate` (see Logic / API).

| Field | resx label | Control | Required | MaxLength | Notes |
|-------|-----------|---------|----------|-----------|-------|
| Id (ISO 3166-1 numeric) | `Admin.Countries.Field.Id` | `SimfTextField` `Type="number"` | yes (Add only) | — | client guard `1–999`; **disabled on Edit** (`Disabled="@(_busy || IsEdit)"`); helper switches `IdHint` → `IdReadOnly` |
| Code (ISO alpha-2) | `Admin.Countries.Field.Code` | `SimfTextField` `MaxLength="2"` | yes | 2 | trimmed + `ToUpperInvariant()` on submit; exactly 2 chars |
| Name (English) | `Admin.Countries.Field.NameEn` | `SimfTextField` `MaxLength="128"` | yes | 128 | trimmed |
| Name (Arabic) | `Admin.Countries.Field.NameAr` | `SimfTextField` `MaxLength="128"` | yes | 128 | trimmed |
| Phone prefix (dial code) | `Admin.Countries.Field.PhonePrefix` | `SimfTextField` `MaxLength="8"` | no | 8 | blank stored as `null` (`NullIfBlank`) |
| Display order | `Admin.Countries.Field.DisplayOrder` | `SimfTextField` `Type="number"` | yes | — | integer `≥ 0` |
| Active | `Admin.Countries.Field.IsActive` | `SimfCheckbox` | Edit only | bool | shown only when `IsEdit` (reactivation path) |

- **Submit button** label: `Admin.Countries.New.Submit` (Add) / `Admin.Countries.Edit.Submit`
  (Edit); loading labels `…New.Submitting` / `…Edit.Submitting`.
- **Cancel** (`Admin.Countries.Cancel`) when `OnCancel` is wired.
- **Add** posts `AdminCreateCountryRequest`; **Edit** puts `AdminUpdateCountryRequest`
  (no `Id` in the body — the route id is authoritative).
- On success → `OnSuccess.InvokeAsync(detail)` → the list shows the green toast
  `Admin.Countries.Created` / `…Updated` (formatted with the country name) and reloads.
- On failure → inline `SimfAlert error` with `Error.MessageForCurrentCulture()`,
  falling back to `Admin.Countries.Fallback`.

## The View / Delete form (`CountryViewDelete.razor`)
- A read-only description list (`dl.simf-dl`) of all fields: Id, Code,
  Name (English), Name (Arabic), Dial code (`—` when null), Display order, Active
  (Yes/No).
- **Details** mode (`IsDelete=false`): only the read-only list + a Close button
  (`Admin.Countries.Details.Close`).
- **Deactivate** mode (`IsDelete=true`): a danger button
  (`Admin.Countries.Action.Deactivate`) opens a `SimfConfirm` dialog
  (title `…Delete.Title`, message `…Delete.Message` formatted with the name,
  `Danger=true`). Confirm → `DELETE /{id}` → green toast `Admin.Countries.Deactivated`
  and reload. Cancel closes the confirm without a call.

## Step-by-step: the golden CRUD path
1. Open `/admin/countries` → the grid loads via `POST /account/api/admin/countries/list`.
2. **Add** → fill Id / Code / Name (EN) / Name (AR) / Dial code / Display order →
   **Create country** → toast `'Country "<name>" was created.'`, grid reloads (+1 row).
3. **Edit** the new row → change Display order → **Save changes** → toast
   `'… was updated.'`, the Order cell updates.
4. **Details** → read-only list → **Close**.
5. **Deactivate** → confirm → toast `'… was deactivated.'`, the row's Active pill
   flips to `No`. **Reactivate** = Edit → tick **Active** → Save.
6. **Export** the filtered grid to `.xlsx`; **Import** an `.xlsx` to bulk-insert.

## Outcomes / acceptance
- A created / edited / reordered / deactivated country is reflected in the grid on
  the next load, and in the app's Page 007 nationality picker on its next fetch.
- An admin without `Countries.View` lands on `/not-permitted` (page gate) and the
  list call never fires.
- Validation failures keep the form open and surface a bilingual error; no write
  request fires until the client guards pass.

## E2E
See [`../../tests/e2e/cp-admin-countries.md`](../../tests/e2e/cp-admin-countries.md)
— `E2E-CTY-001` (full CRUD round-trip) … `E2E-CTY-020` (Excel import rejection),
covering empty state, the `Countries.View` auth gate, search/sort/pager, the
details modal, reactivation, validation, the duplicate-id / duplicate-code 409s,
not-found, server-500, RTL, the D-353 presentation toggle + full-page mode, and the
D-356 Excel export / import.
