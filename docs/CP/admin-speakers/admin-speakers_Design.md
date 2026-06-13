# Speakers — Design (`/admin/speakers` · المتحدّثون)

Control Panel screen design. Source of truth:
[`SpeakersList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakersList.razor)
(grid host) +
[`SpeakersAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakersAddEdit.razor)
(Add/Edit form) +
[`SpeakersViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakersViewDelete.razor)
(Details/Deactivate form). Bilingual (EN/AR), mirrors to RTL under `ar`.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Layout (top → bottom, as built)

1. **`<PageTitle>`** — `@L["Admin.Speakers.Title"] · SIMF`.
2. **`SimfBanner`** — title `Admin.Speakers.Title` (EN **Speakers** / AR
   **المتحدّثون**). Hidden when a form takes over the page in full-page mode
   (`GridHidden`).
3. **Toast slot** — a `SimfAlert` bound to `_toast` (variant `success` / `error`)
   shown above the grid for load / save / delete / import outcomes.
4. **`SimfDataGrid<AdminSpeakerSummary>`** — the canonical CRUD grid:
   - **Multiselect** (`Multiselect="true"`, `RowKey = r.Id.ToString()`,
     `RowLabel = r.Name`) — the row checkboxes feed the Export selected-ids set.
   - **Toolbar actions** (wired via the grid's `On*` callbacks):
     **Add speaker** (`OnAdd`), **Export** (`OnExport`), **Import** (`OnImport`),
     plus the **D-353 presentation toggle** in the `CustomToolbar`:
     `CrudPresentationToggle PageKey="speakers" @bind-Value="_presentation"`.
   - **Per-row actions:** **Edit** (`OnEditOne`), **Details** (`OnDetailsOne`),
     **Deactivate** (`OnDeleteOne`) — quiet per-row icons.
   - **Pager:** First / Prev / Next / Last + page-size selector
     (labels `Admin.Speakers.Pager.*`), with `FormatPage` + `FormatSummary`
     formatters.
   - **Filter:** a single column filter on **Name** (`FilterColumnLabel` =
     `Admin.Speakers.FilterColumn`, placeholder `Admin.Speakers.FilterPlaceholder`).
5. **`CrudGridExcel @ref="_excel" Resource="speakers"`** — the D-356 export/import
   engine (hidden chrome + the `speakers-import-input` file input), wired to
   `OnImported` → `OnImportedAsync` and `OnError` → `OnExcelError`.
6. **`CrudShell`** — when a form is open (`FormOpen`), it frames either the
   Add/Edit or the View/Delete form as a **dialog** or **full page** per
   `_presentation`. In full-page mode the banner + grid are hidden.

### Grid columns (in order)

| Key | Header resx | Sortable | Filterable | Cell |
|-----|-------------|----------|------------|------|
| `code` | `Admin.Speakers.Column.Code` | ✅ | — | `context.Code` |
| `name` | `Admin.Speakers.Column.Name` | ✅ | ✅ | `context.Name` |
| `nameArabic` | `Admin.Speakers.Column.NameArabic` | — | — | `context.NameArabic` |
| `rank` | `Admin.Speakers.Column.Rank` | — | — | `context.Rank ?? "—"` |
| `country` | `Admin.Speakers.Column.Country` | — | — | `CountryLabel(context) ?? "—"` (localized country name) |
| `displayOrder` | `Admin.Speakers.Column.DisplayOrder` | ✅ | — | `context.DisplayOrder` |
| `isActive` | `Admin.Speakers.Column.Active` | — | — | `SimfPill` — `on` → `Admin.Speakers.Active.Yes`, `off` → `Admin.Speakers.Active.No` |

- **Empty grid** → `EmptyTemplate` renders `SimfEmptyState Title="Admin.Speakers.None"`.
- **Country label** — `CountryLabel` picks the AR name under `ar` culture,
  the EN name otherwise (each with the other-language fallback).

## Add / Edit form (`SpeakersAddEdit.razor`)

An `EditForm` (`class="simf-form"`) hosted by `CrudShell`. Title is
`Admin.Speakers.Add.Title` (Add) or `Admin.Speakers.Edit.Title` (Edit). Fields,
top → bottom, with their `MaxLength`:

| Field (resx label) | Control | MaxLength | Notes |
|--------------------|---------|-----------|-------|
| Code (`…Field.Code`, helper `…Field.CodeHint`) | `SimfTextField` | 16 | upper-cased on submit |
| Name (`…Field.Name`) | `SimfTextField` | 128 | English name |
| Name Arabic (`…Field.NameArabic`) | `SimfTextField` | 128 | Arabic name |
| Rank / title (`…Field.Rank`) | `SimfTextField` | 64 | optional |
| Country (`…Field.Country`, placeholder `…Field.CountryPlaceholder`) | `SimfSelect<string>` | n/a | options loaded from `/account/api/admin/countries/list` (active rows) on first render |
| Bio EN / AR (`…Field.Bio` / `…Field.BioArabic`) | `SimfTextarea` (3 rows) | 2048 | optional |
| Qualifications EN / AR | `SimfTextarea` (3 rows) | 1024 | optional |
| Training & experience EN / AR | `SimfTextarea` (3 rows) | 1024 | optional |
| Awards EN / AR | `SimfTextarea` (3 rows) | 1024 | optional |
| Allows meeting requests (`…Field.AllowsMeetingRequests`) | `SimfCheckbox` | bool | default false |
| Allows data sharing (`…Field.AllowsDataSharing`) | `SimfCheckbox` | bool | default false |
| Facebook URL (`…Field.FacebookUrl`) | `SimfTextField` | 256 | optional |
| LinkedIn URL (`…Field.LinkedInUrl`) | `SimfTextField` | 256 | optional |
| X URL (`…Field.XUrl`) | `SimfTextField` | 256 | optional |
| Contact | `ContactPicker` | n/a | optional shared-Contact link (SIMF-FDS-014 / D-283) |
| Display order (`…Field.DisplayOrder`) | `SimfTextField Type="number"` | n/a | integer ≥ 0 |
| Active (`…Field.IsActive`) | `SimfCheckbox` | bool | **Edit mode only** |
| Image (`Admin.Asset.Heading`) | `SimfImageUpload Category="SpeakerPhoto"` | n/a | **Edit mode only**, and only when `Initial` exists — the speaker row must exist before bytes can attach (D-357) |

- **Actions:** primary submit — `Admin.Speakers.New.Submit` (Add) /
  `Admin.Speakers.Edit.Submit` (Edit), loading labels `…New.Submitting` /
  `…Edit.Submitting`; secondary **Cancel** (`Admin.Speakers.Cancel`) when
  `OnCancel` is bound.
- A `SimfAlert Variant="error"` shows the client/server `_error` at the top.

## Details / Deactivate form (`SpeakersViewDelete.razor`)

A read-only description list (`dl class="simf-dl"`) that always renders all
fields, plus (D-357) a `SimfImageThumb` of
`/account/api/admin/assets/SpeakerPhoto/{Initial.Id}/image` at the top. Title is
`Admin.Speakers.Details.Title` (Details) or `Admin.Speakers.Delete.Title`
(Deactivate). The `dl` rows (in order): Code, Name, Name (Arabic), Rank,
Country, Bio EN/AR, Qualifications EN/AR, Training & experience EN/AR, Awards
EN/AR, Allows meeting requests, Allows data sharing, Facebook/LinkedIn/X URL,
Display order, Active — each blank optional value renders the em dash **—**;
booleans render `Admin.Speakers.Active.Yes` / `…No`.

- **Details mode** (`IsDelete=false`): only a secondary **Close**
  (`Admin.Speakers.Details.Close`) button.
- **Deactivate mode** (`IsDelete=true`): a red **Deactivate**
  (`Admin.Speakers.Action.Deactivate`) button that opens a **`SimfConfirm`**
  Danger dialog (title `Admin.Speakers.Delete.Title`, message
  `Admin.Speakers.Delete.Message` with the speaker name) before the call fires.

## Page ↔ Popup presentation toggle (D-353)

`CrudPresentationToggle PageKey="speakers"` (`@bind-Value="_presentation"`). The
choice persists in `localStorage` under `simf.cp.prefs.speakers` via
`CpPreferences`; `OnInitializedAsync` re-reads it with
`Prefs.GetPresentationAsync("speakers")`. In **Dialog** mode forms open as a
popup over the grid; in **Page** mode `GridHidden` hides the banner + grid and
the `CrudShell` renders a full-page frame.

## States

- **Loading** — `_loading` drives the grid's `LoadingLabel`
  (`Admin.Speakers.Loading`) while `/list` runs.
- **Empty** — `SimfEmptyState` (`Admin.Speakers.None`) inside the grid body.
- **Load failure** — a non-success `/list` envelope (or a thrown call) sets the
  red `_toast` to the envelope's `MessageForCurrentCulture()` or
  `Admin.Speakers.LoadFailed`; no rows render.
- **Save / delete failure** — surfaces the envelope message or
  `Admin.Speakers.Fallback`.
- **Save success** — green `_toast` `Admin.Speakers.Created` / `…Updated`
  (formatted with the speaker name); grid reloads.
- **Deactivate success** — green `_toast` `Admin.Speakers.Deactivated`.
- **Import done** — green `Grid.Import.Done` toast + grid reload.

## RTL / localization

- `Admin.Speakers.*` keys span the banner, grid columns/actions/pager, the form
  fields/hints, the validation messages and the toast templates; shared
  `Grid.Export` / `Grid.Import` / `Grid.Import.Done` keys cover the Excel toolbar.
- The whole page mirrors to RTL under `<html dir="rtl" lang="ar">`; the Country
  column + picker render the Arabic country name under `ar`.
- **Known resx gap (out of scope — reported only):** the English resx is missing
  `Admin.Speakers.Delete.Title` / `Admin.Speakers.Delete.Message` (both exist in
  `Strings.ar.resx`), so the EN `SimfConfirm` title/body fall back to the resource
  keys until added. Flagged in the E2E catalogue.

## Cross-links

- Page index: [`docs/pages/cp/admin-speakers.md`](../../pages/cp/admin-speakers.md)
- E2E: [`docs/tests/e2e/cp-admin-speakers.md`](../../tests/e2e/cp-admin-speakers.md) (E2E-SPK-001…023)
- App consumer: [`docs/App/Page_016/Page_016_Design.md`](../../App/Page_016/Page_016_Design.md) (the speaker cards on the session list/detail)
- API contract: [admin-speakers_API.md](admin-speakers_API.md)
