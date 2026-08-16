# Halls — Design (`/admin/halls`)

The as-built Control Panel screen. Source: `HallsList.razor` +
`HallsAddEdit.razor` + `HallsViewDelete.razor` (Blazor Server, `CpShellLayout`).
Bilingual EN/AR, RTL-mirrored. Verified against code this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Function](admin-halls_Function.md) ·
> [API](admin-halls_API.md) · [Logic](admin-halls_Logic.md) ·
> existing reference [`docs/pages/cp/admin-halls.md`](../../pages/cp/admin-halls.md) ·
> E2E [`docs/tests/e2e/cp-admin-halls.md`](../../tests/e2e/cp-admin-halls.md).

## Layout (top → bottom, as built)

When no CRUD form is open (`GridHidden = false`):

1. **Banner** — `SimfBanner Title="@L["Admin.Halls.Title"]"` → EN **Halls &
   seating** / AR **القاعات والمقاعد** (`Strings.ar.resx`).
2. **Inline alert** — a `SimfAlert` renders above the grid only when a `_toast`
   is set. `Variant` is `"success"` or `"error"`.
3. **SimfDataGrid** (`TItem="AdminHallSummary"`, D-255 owner-mandated list-page
   standard) — `Multiselect="true"` (select-all + per-row checkbox), full pager,
   quiet per-row icon actions. `RowKey` = `Id`, `RowLabel` = `Name`. Its
   `CustomToolbar` hosts a **`CrudPresentationToggle`** bound to `PageKey =
   "halls"` (the D-353 dialog⇄full-page toggle).
   - Grid action callbacks wired on the grid: `OnAdd`, `OnEditOne`,
     `OnDetailsOne`, `OnDeleteOne`, `OnExport`, `OnImport`.
   - `EmptyTemplate` → `SimfEmptyState Title="@L["Admin.Halls.None"]"` ("No halls
     yet.").
   - A `CrudGridExcel @ref="_excel" Resource="halls"` component sits below the
     grid and backs Export / Import (§Excel).

When a CRUD form is open (`FormOpen`), the page renders a **`CrudShell`** (dialog
by default, or full page when `_presentation == Page` — in which case the grid +
banner are hidden via `GridHidden`). The shell frames either `HallsAddEdit`
(`FormKind.AddEdit`) or `HallsViewDelete` (`FormKind.ViewDelete`).

## Grid columns (verified)

| Key | Header resx → EN | Sortable | Filterable | Cell render |
|-----|------------------|----------|------------|-------------|
| `code` | `Admin.Halls.Column.Code` → **Code** | ✅ | ✅ | `context.Code` |
| `name` | `Admin.Halls.Column.Name` → **Name** | ✅ | ✅ | `context.Name` |
| `nameArabic` | `Admin.Halls.Column.NameArabic` → **Name (Arabic)** | ✅ | ✅ | `context.NameArabic` |
| `capacity` | `Admin.Halls.Column.Capacity` → **Capacity** | ✅ | — | `context.Capacity` |
| `floor` | `Admin.Halls.Column.Floor` → **Floor** | — | ✅ | value or `—` when null |
| `isActive` | `Admin.Halls.Column.Active` → **Status** | — | — | `SimfPill Variant="on"` = **Active** (`Admin.Halls.Active.Yes`) / `Variant="off"` = **Inactive** (`Admin.Halls.Active.No`) |

The four filterable columns (`code`, `name`, `nameArabic`, `floor`) expose a
per-column filter input; `capacity` and `isActive` do not (verified against
`AdminHallService.ListAllAsync`). The grid summary (`AdminHallSummary`) carries
`Id, Code, Name, NameArabic, Capacity, Floor, IsActive, CreatedAt, Purpose` —
it **omits** `FacilityNotes` and the geofence triple, which load only via the
per-id detail fetch (`AdminHallDetail`) before Edit / Details / Deactivate.

## Row actions (quiet per-row icons)

- **Add** (`OnAddAsync`) — opens `HallsAddEdit` in Create mode (`_isEdit =
  false`, `_target = null`). Action label `Admin.Halls.Action.Add` = "Add hall".
- **Edit** (`OnEditAsync`) — first calls `GET /account/api/admin/halls/{id}`
  (`LoadDetailAsync`) for the full detail (the grid omits notes + geofence), then
  opens `HallsAddEdit` in Edit mode (`_isEdit = true`). Label
  `Admin.Halls.Action.Edit` = "Edit".
- **Details** (`OnDetailsAsync`) — loads the detail, opens `HallsViewDelete`
  read-only (`_isDelete = false`, no Deactivate button). Label
  `Admin.Halls.Action.Details` = "Details".
- **Deactivate** (`OnDeleteAsync`) — loads the detail, opens `HallsViewDelete`
  with the red Deactivate button (`_isDelete = true`). Label
  `Admin.Halls.Action.Deactivate` = "Deactivate".
- **Export** (toolbar, `OnExportAsync`) — XLSX download via `CrudGridExcel` (§Excel).
- **Import** (toolbar, `OnImportAsync`) — file picker via `CrudGridExcel` (§Excel).

Select-all / select-row labels: `Admin.Halls.Action.SelectAll` = "Select all" /
`Admin.Halls.Action.SelectRow` = "Select row". Actions header
`Admin.Halls.Column.Actions` = "Actions".

## Add / Edit form (`HallsAddEdit`)

`@inherits CrudAddEditFormBase<AdminHallDetail>` — an `EditForm` over a private
`Model`. Six editable fields + three geofence inputs + an Active checkbox (Edit
only). Each text field carries a UI `MaxLength` that matches the server cap.

| Field | resx label → EN | Required | UI `MaxLength` | Notes |
|-------|-----------------|----------|----------------|-------|
| Code | `Admin.Halls.Field.Code` → Code | **yes** | 16 | 2–16 chars; uppercased on send; unique |
| Name (English) | `Admin.Halls.Field.Name` → Name (English) | **yes** | 128 | 1–128 |
| Name (Arabic) | `Admin.Halls.Field.NameArabic` → Name (Arabic) | **yes** | 128 | 1–128 |
| Capacity | `Admin.Halls.Field.Capacity` → Capacity | **yes** | n/a (`Type="number"`) | integer ≥ 0; bound via `_capacityInput` string |
| Floor | `Admin.Halls.Field.Floor` → Floor | no | 32 | `null` when blank |
| Equipment + accessibility notes | `Admin.Halls.Field.FacilityNotes` → Equipment + accessibility notes | no | 1024 | `SimfTextarea Rows="3"`; `null` when blank |
| Geofence centre latitude | `Admin.Halls.Field.GeofenceLat` → Geofence centre latitude | no | n/a | `_geoLatInput` string; invariant-culture parse |
| Geofence centre longitude | `Admin.Halls.Field.GeofenceLon` → Geofence centre longitude | no | n/a | `_geoLonInput` string |
| Geofence radius (metres) | `Admin.Halls.Field.GeofenceRadius` → Geofence radius (metres) | no | n/a (`Type="number"`) | `_geoRadiusInput` string |
| Active | `Admin.Halls.Field.IsActive` → Active — available for Session assignment | (Edit only) | bool | `SimfCheckbox`, shown only when `IsEdit` |

Field hints (verbatim, shown under the inputs):
- Code: `Admin.Halls.Field.CodeHint` = "2–16 characters; unique. Venue team's
  stable identifier (e.g. H1, A201)."
- Name (English): `Admin.Halls.Field.NameHint` = "Up to 128 characters."
- Name (Arabic): `Admin.Halls.Field.NameArabicHint` = "Up to 128 characters."
- Capacity: `Admin.Halls.Field.CapacityHint` = "Seating + standing capacity
  (zero or more). Drives the Sessions booking cap."
- Floor: `Admin.Halls.Field.FloorHint` = "Optional. Up to 32 characters (e.g.
  \"Ground\", \"Level 2\")."
- Equipment notes: `Admin.Halls.Field.FacilityNotesHint` = "Optional. Up to
  1024 characters."
- Geofence (lat field): `Admin.Halls.Field.GeofenceHint` = "Optional. Set
  latitude, longitude and radius together to enable GPS arrival, or leave all
  three empty for QR-scan-only."
- Geofence radius: `Admin.Halls.Field.GeofenceRadiusHint` = "Greater than 0, up
  to 100000 metres."

**Client submit guards** (each shows an inline `SimfAlert Variant="error"` and
**does not send** — verified in `HandleSubmitAsync`):
- Code blank or length ∉ [2,16] → `Admin.Halls.Field.CodeInvalid` = "Code must
  be between 2 and 16 characters."
- Name blank or > 128 → `Admin.Halls.Field.NameInvalid` = "English name is
  required (1–128 characters)."
- Arabic name blank or > 128 → `Admin.Halls.Field.NameArabicInvalid` = "Arabic
  name is required (1–128 characters)."
- Capacity not an int or < 0 → `Admin.Halls.Field.CapacityInvalid` = "Capacity
  must be zero or a positive integer."
- Partial / out-of-range geofence (`TryParseGeofence` false) →
  `Admin.Halls.Field.GeofenceInvalid` = "The geofence needs a valid latitude
  (−90..90), longitude (−180..180) and radius (greater than 0, up to 100000 m) —
  set all three or leave all empty."

**On send:** Code is `.Trim().ToUpperInvariant()`-ed; Name/NameArabic trimmed;
blank Floor/FacilityNotes sent as `null`; geofence values parsed
invariant-culture. Create → `POST /account/api/admin/halls`
(`AdminCreateHallRequest`); Edit → `PUT /account/api/admin/halls/{Initial.Id}`
(`AdminUpdateHallRequest`, includes `IsActive`).

**Buttons:** Create → `Admin.Halls.New.Submit` = "Create hall" (busy label
`Admin.Halls.New.Submitting` = "Creating"); Edit → `Admin.Halls.Edit.Submit` =
"Save changes" (busy label `Admin.Halls.Edit.Submitting` = "Saving"); Cancel →
`Admin.Halls.Cancel` = "Cancel" (secondary, when `OnCancel.HasDelegate`).

**Server error:** a non-success envelope surfaces
`envelope.Error.MessageForCurrentCulture()` (bilingual) in the top `SimfAlert`,
falling back to `Admin.Halls.Fallback` = "The operation could not be completed.";
the form stays open. A thrown exception also lands on the fallback.

## View / Delete form (`HallsViewDelete`)

`@inherits CrudViewDeleteFormBase<AdminHallDetail>` — a read-only `dl`
(`simf-dl`) of: Code, Name, Name (Arabic), Capacity, Floor (or `—`), Equipment +
accessibility notes (or `—`), and Status (`Admin.Halls.Active.Yes`/`.No`). The
geofence triple is **not** rendered in this form's `dl`.

- **Read-only (Details):** when `IsDelete = false`, only a **Close** button
  (`Admin.Halls.Details.Close` = "Close") — no Deactivate.
- **Deactivate:** when `IsDelete = true`, a red **Deactivate** button
  (`Admin.Halls.Action.Deactivate` = "Deactivate") + Close.
- **Confirm gate:** clicking Deactivate opens a **`SimfConfirm`** (`Danger=true`)
  titled `Admin.Halls.Delete.Title` = "Deactivate hall", message
  `Admin.Halls.Delete.Message` = `Deactivate the hall "{0}"? It will no longer be
  available for session assignment.` (`{0}` = the row's `Name`), confirm label
  `Admin.Halls.Action.Deactivate` = "Deactivate", cancel label
  `Admin.Halls.Cancel` = "Cancel". Cancel → no DELETE. Confirm →
  `DELETE /account/api/admin/halls/{id}`. (D-353 replaced the pre-existing
  one-click delete with this gate.)

## Excel export + import (generic D-356 grid layer)

`CrudGridExcel Resource="halls"`:
- **Export** (toolbar `Grid.Export`, `Halls.Export` gated) — `OnExportAsync`
  calls `_excel.ExportAsync(selectedIds, _query)`; posts to
  `/account/api/admin/halls/export`. Sheet header
  `Code | Name | NameArabic | Capacity | Floor | IsActive`; capped at 5000 rows.
  With rows selected, only those `Ids` export; otherwise the current filtered
  `_query`.
- **Import** (toolbar `Grid.Import`, `Halls.Import` gated) — `OnImportAsync`
  calls `_excel.TriggerImportAsync()` (file input `id="halls-import-input"`,
  `accept=".xlsx"`); posts to `/account/api/admin/halls/import` as multipart.
  On success `OnImportedAsync` raises a green toast `Grid.Import.Done` and
  reloads the grid; a non-`.xlsx` / oversized / wrong-sheet upload is rejected
  (HTTP 400) and surfaces a bilingual error toast via `OnExcelError`.

## Page ⇄ Popup presentation toggle (D-353)

The grid's `CustomToolbar` hosts a `CrudPresentationToggle` (`@bind-Value=
"_presentation"`, `PageKey = "halls"`). `OnInitializedAsync` reads the saved
choice via `Prefs.GetPresentationAsync("halls")`. In **dialog** mode the CRUD
forms open as a `CrudShell` popup; in **page** mode `GridHidden` hides the grid +
banner and `CrudShell` frames the form full-page. The choice persists in
`localStorage` (`simf.cp.prefs.halls`) via `CpPreferences`.

## States

- **Loading** — `_loading` true; the grid shows `Admin.Halls.Loading` =
  "Loading halls…".
- **Empty / no-match** — `SimfEmptyState` titled "No halls yet."
  (`Admin.Halls.None`); no error alert; toolbar actions stay visible.
- **List failure** — a non-success `/list` envelope sets an error `_toast`
  (`envelope.Error.MessageForCurrentCulture()` or fallback `Admin.Halls.LoadFailed`
  = "The halls could not be loaded.").
- **Save success** — green toast `Admin.Halls.Created` = `Hall "{0}" was
  created.` / `Admin.Halls.Updated` = `Hall "{0}" was updated.` (`{0}` =
  `saved.Name`); the form closes and the grid reloads.
- **Deactivate success** — green toast `Admin.Halls.Deactivated` = `Hall "{0}"
  was deactivated.`; the form closes and the grid reloads.

## Pager

`Admin.Halls.Summary` = "Showing {0}–{1} of {2}"; page indicator
`Admin.Halls.Pager.Page` = "Page {0} of {1}"; controls
`Admin.Halls.Prev` / `.Next` / `Admin.Halls.Pager.First` = "First page" /
`.Last` = "Last page" / `.PageSize` = "Show". Default page size `Top = 20`
(server clamps `Top` to [1,200], default 25 when unset).

## Bilingual / RTL behaviour

- Every label, column header, button, modal title, hint and toast comes from the
  `Admin.Halls.*` resx keys (EN `Strings.resx` / AR `Strings.ar.resx`) + shared
  `Grid.*` keys — no hardcoded UI text in the razor (only the `—` glyph).
- Under `<html dir="rtl" lang="ar">` the whole page, grid, both CRUD forms and
  the confirm dialog mirror; the bilingual server error/confirm strings render in
  the active culture via `MessageForCurrentCulture()`.

> The exact Arabic resx phrasing lives in `Strings.ar.resx`; the AR pairs for
> Title / None / LoadFailed / Created / Updated / Deactivated / Delete.Message /
> Geofence hints were read verbatim this session, the remaining AR keys were
> confirmed present per key but not all transcribed here.
