# Organisations — Design (`/admin/organisations`)

The as-built Control Panel screen. Source: `OrganisationsList.razor` +
`OrganisationAddEdit.razor` + `OrganisationViewDelete.razor` (Blazor Server,
`CpShellLayout`). Bilingual EN/AR, RTL-mirrored. Verified against code this
session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Function](admin-organisations_Function.md) ·
> [API](admin-organisations_API.md) · [Logic](admin-organisations_Logic.md) ·
> existing reference [`docs/pages/cp/admin-organisations.md`](../../pages/cp/admin-organisations.md) ·
> E2E [`docs/tests/e2e/cp-admin-organisations.md`](../../tests/e2e/cp-admin-organisations.md).

## Layout (top → bottom, as built)

When no CRUD form is open (`GridHidden = false`):

1. **Banner** — `SimfBanner Title="@L["Admin.Organisations.Title"]"` → EN
   **Organisations** (AR pair from `Strings.ar.resx`).
2. **Inline alert** — a `SimfAlert` renders above the toolbar only when a `_toast`
   is set (success or error). `Variant` is `"success"` or `"error"`.
3. **Toolbar** (`simf-toolbar`) — a server-side **Search** field
   (`SimfTextField`, label `Admin.Organisations.Search` = "Search", placeholder
   `Admin.Organisations.Search.Placeholder` = "Search by name, CR, sector or
   city") + a **Search** button (`Admin.Organisations.Search.Apply` = "Search").
   Both disable while `_loading`.
4. **SimfDataGrid** (`TItem="AdminOrganisationSummary"`, D-255 owner-mandated
   list-page standard) — `Multiselect="true"` (select-all + per-row checkbox),
   full pager, quiet per-row icon actions. `RowKey` = `Id`, `RowLabel` =
   `NameAr`. Its `CustomToolbar` hosts a **`CrudPresentationToggle`** bound to
   `PageKey = "organisations"` (the D-353 dialog⇄full-page toggle).
   - Grid action callbacks wired on the grid: `OnAdd`, `OnEditOne`,
     `OnDetailsOne`, `OnDeleteOne`, `OnExport`, `OnImport`.
   - `EmptyTemplate` → `SimfEmptyState Title="@L["Admin.Organisations.None"]"`
     ("No organisations found").

When a CRUD form is open, the page renders a **`CrudShell`** (dialog by default,
or full page when `_presentation == Page` — in which case the grid + banner are
hidden via `GridHidden`). The shell frames either `OrganisationAddEdit` or
`OrganisationViewDelete`. A separate `SimfModal` hosts the Excel import.

## Grid columns (verified)

| Key | Header resx → EN | Sortable | Filterable | Cell render |
|-----|------------------|----------|------------|-------------|
| `name` | `Admin.Organisations.Col.NameAr` → **Name (Arabic)** | ✅ | ✅ | `context.NameAr` |
| `nameEn` | `Admin.Organisations.Col.NameEn` → **Name (English)** | — | ✅ | value or `—` when blank |
| `commercialRegistration` | `Admin.Organisations.Col.CommercialRegistration` → **CR** | — | ✅ | value or `—` |
| `sector` | `Admin.Organisations.Col.Sector` → **Sector** | — | ✅ | value or `—` |
| `city` | `Admin.Organisations.Col.City` → **City** | ✅ | ✅ | value or `—` |
| `isActive` | `Admin.Organisations.Col.Active` → **Active** | ✅ | — | `SimfPill Variant="on"` (Active) / `Variant="off"` (Inactive) |

Empty / null text columns render an em-dash (`—`). The grid summary
(`AdminOrganisationSummary`) **omits** Phone / Email / Website — those are only
loaded via the per-id detail fetch before opening Edit / Details / Delete.

## Row actions (quiet per-row icons)

- **Add** (`OnAdd`) — opens `OrganisationAddEdit` in Create mode (`_isEdit =
  false`, `_target = null`).
- **Edit** (pencil, `OnEditAsync`) — first calls
  `GET /account/api/admin/organisations/{id}` (`LoadDetailAsync`) to get the full
  detail (the grid omits contact columns), then opens `OrganisationAddEdit` in
  Edit mode (`_isEdit = true`).
- **Details** (eye, `OnDetailsAsync`) — loads the detail, opens
  `OrganisationViewDelete` read-only (`_isDelete = false`, no Deactivate button).
- **Delete** (trash, `OnDeleteAsync`) — loads the detail, opens
  `OrganisationViewDelete` with the red Deactivate button (`_isDelete = true`).
- **Export** (toolbar, `OnExportAsync`) — direct XLSX browser download (§Excel).
- **Import** (toolbar, `OpenImport`) — opens the bespoke gov-Excel modal (§Excel).

The grid action labels come from shared `Grid.*` resx keys (`Grid.Add`,
`Grid.Edit`, `Grid.Details`, `Grid.Delete`, `Grid.Export`, plus the pager keys).

## Add / Edit form (`OrganisationAddEdit`)

`@inherits CrudAddEditFormBase<AdminOrganisationDetail>` — an `EditForm` over a
private `Model`. Eight text fields + an Active checkbox (Edit only). Each field
carries a UI `MaxLength` that matches the server cap.

| Field | resx label → EN | Required | UI `MaxLength` | Notes |
|-------|-----------------|----------|----------------|-------|
| Name (Arabic) | `Admin.Organisations.Field.NameAr` → Name (Arabic) | **yes** | 256 | only required field |
| Name (English) | `Admin.Organisations.Field.NameEn` → Name (English) | no | 256 | `null` when blank |
| Commercial registration | `Admin.Organisations.Field.CommercialRegistration` → Commercial registration | no | 32 | unique when present (409 on clash) |
| Sector | `Admin.Organisations.Field.Sector` → Sector | no | 128 | |
| City | `Admin.Organisations.Field.City` → City | no | 128 | |
| Phone | `Admin.Organisations.Field.Phone` → Phone | no | 32 | |
| Email | `Admin.Organisations.Field.Email` → Email | no | 320 | |
| Website | `Admin.Organisations.Field.Website` → Website | no | 512 | |
| Active | `Admin.Organisations.Field.IsActive` → Active | (Edit only) | bool | `SimfCheckbox`, shown only when `IsEdit` |

- **Submit guard (client):** a blank/whitespace Arabic name shows an inline
  `SimfAlert Variant="error"` reading `Admin.Organisations.Required` = "Arabic
  name is required." and **does not POST**. All other validation is server-side.
- **Optional-field normalisation:** `NullIfBlank` sends `null` (not `""`) for any
  blank optional field; the Arabic name is `.Trim()`-ed.
- **Buttons:** Save (`Admin.Organisations.Save` = "Save", with `Loading="_busy"`)
  + Cancel (`Admin.Organisations.Cancel` = "Cancel", secondary, when
  `OnCancel.HasDelegate`).
- **Server error:** a non-success envelope surfaces
  `env.Error.MessageForCurrentCulture()` (bilingual) in the top `SimfAlert`; the
  form stays open.

## View / Delete form (`OrganisationViewDelete`)

`@inherits CrudViewDeleteFormBase<AdminOrganisationDetail>` — a read-only `dl`
(`simf-dl`) of every column **including** the contact fields the grid omits:
Name (Arabic), Name (English), CR, Sector, City, **Phone, Email, Website**,
Active (rendered as `Grid.Active` / `Grid.Inactive`). Null values render `—`.

- **Read-only (Details):** when `IsDelete = false`, only a **Close** button
  (`Admin.Organisations.Details.Close` = "Close") — no Deactivate.
- **Delete:** when `IsDelete = true`, a red **Deactivate** button
  (`Admin.Organisations.Action.Deactivate` = "Deactivate") + Close.
- **Confirm gate:** clicking Deactivate opens a **`SimfConfirm`** (`Danger=true`)
  titled `Admin.Organisations.Delete.Title` = "Deactivate organisation", message
  `Admin.Organisations.Delete.Message` = `Deactivate "{0}"? It will be removed
  from the public lookup.` (`{0}` = the row's Arabic name), confirm label
  "Deactivate", cancel label "Cancel". Cancel → no DELETE. Confirm →
  `DELETE /account/api/admin/organisations/{id}`. A `SimfConfirm` replaces the
  pre-D-353 native `confirm()`.

## Excel import modal (bespoke, gov sheet)

Opened by the toolbar **Import** action (`Admin.Organisations.Import` = "Import
Excel", `Organisations.Import` gated). A `SimfModal`
(`Admin.Organisations.Import.Title` = "Import organisations from Excel"):

- Hint (`Admin.Organisations.Import.Hint`): "Upload a government .xlsx sheet.
  Existing rows are matched by commercial registration and updated; new rows are
  inserted."
- File input `id="organisations-import-input"`, `accept=".xlsx,application/…sheet"`.
- **Upload** button (`Admin.Organisations.Import.Upload` = "Upload") is disabled
  until a file is picked (`_importFileName` set); shows `Loading="_importing"`.
- On success the modal shows the row tallies
  (`Admin.Organisations.Import.Result` = "Rows read: {0} · Inserted: {1} ·
  Updated: {2} · Skipped: {3}") plus a per-row **error list** (`<ul>`), and a
  green toast `Admin.Organisations.Import.Done` = "Import complete — {0}
  inserted, {1} updated, {2} skipped." The grid reloads.
- On failure: error toast `Admin.Organisations.Import.Failed` = "Excel import
  failed." (or the server's bilingual message).

## Excel export (generic D-356 grid layer)

Toolbar **Export** (`Grid.Export`, `Organisations.Export` gated) →
`OnExportAsync` posts `AdminGridExportRequest { Ids, Query }` to
`/account/api/admin/organisations/export` via `simfAccount.downloadXlsx` (a
direct browser download). With rows selected it sends those `Ids` (and
`Query = null`); with none selected it sends the current `_query`. Workbook
prefix `simf-organisations`, sheet **"Organisations"**, header
`NameAr | NameEn | CommercialRegistration | Sector | City | IsActive`
(`OrganisationExcelEndpoints.cs`). Export is **export-only** — there is no
generic grid-import sibling; import is the bespoke modal above.

## Page ⇄ Popup presentation toggle (D-353)

The grid's `CustomToolbar` hosts a `CrudPresentationToggle` (`@bind-Value=
"_presentation"`, `PageKey = "organisations"`). `OnInitializedAsync` reads the
saved choice via `Prefs.GetPresentationAsync("organisations")`. In **dialog**
mode the CRUD forms open as a `CrudShell` popup; in **page** mode `GridHidden`
hides the grid + banner and `CrudShell` frames the form full-page. The choice
persists in `localStorage` via `CpPreferences`.

## States

- **Loading** — `_loading` disables the toolbar; the grid shows its loading text
  (`Admin.Organisations.Loading` = "Loading organisations…").
- **Empty / no-match** — `SimfEmptyState` titled "No organisations found"
  (`Admin.Organisations.None`); no error alert; toolbar actions stay visible.
- **List failure** — a non-success `/list` envelope sets an error `_toast`
  (`env.Error.MessageForCurrentCulture()` or fallback
  `Admin.Organisations.LoadFailed` = "Could not load organisations.").
- **Save / delete success** — green toast `Admin.Organisations.Saved` =
  "Organisation saved." / `Admin.Organisations.Deleted` = "Organisation
  deactivated."; the form closes and the grid reloads.

## Bilingual / RTL behaviour

- Every label, column header, button, modal title and toast comes from the
  `Admin.Organisations.*` resx keys (EN `Strings.resx` / AR `Strings.ar.resx`) +
  shared `Grid.*` keys — no hardcoded UI text in the razor (only the `—` glyph).
- Under `<html dir="rtl" lang="ar">` the whole page, grid, both CRUD forms and
  both Excel modals mirror; the bilingual server error/confirm strings render in
  the active culture via `MessageForCurrentCulture()`.
- The Arabic name is the lookup's **primary display name** (it is the only
  required field and the default grid sort key — see [Logic](admin-organisations_Logic.md)).

> The exact Arabic resx phrasing lives in `Strings.ar.resx` and is described, not
> quoted, here (this session read the EN `Strings.resx` values verbatim; the AR
> pair was confirmed present per key but not transcribed).
