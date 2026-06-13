# CP — Interests — Design (`/admin/interests`)

Control Panel screen design. As built in `InterestsList.razor` (banner +
`SimfDataGrid` + `CrudShell` + `CrudGridExcel`) on the `CpShellLayout`. This page is
the **canonical `SimfDataGrid` list-page exemplar** — the reference layout the other
CP list pages follow.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

## Layout (top → bottom, as built)
1. **`CpShellLayout`** — the CP nav rail + header (culture toggle, profile/bell). The
   Interests link is highlighted under the **Reference data** group.
2. **`SimfBanner`** — `Title="@L["Admin.Interests.Title"]"` (AR **الاهتمامات** /
   EN **Interests**). Title only — no subtitle, no Actions slot.
3. **`.simf-page-wide` › `.simf-surface`** — the page card. When a form opens in
   **full-page** mode the grid + banner are hidden and the form takes the surface;
   in **dialog** mode the grid stays and the form floats over it.
4. **Inline `SimfAlert`** — rendered above the grid when `_toast` is set
   (`Variant="success"` green / `"error"` red), carrying the load/CRUD/import message.
5. **`SimfDataGrid TItem="AdminInterestSummary"`** — Multiselect on,
   `RowKey = r.Id`, `RowLabel = r.Name`, `Caption = Admin.Interests.Title`.

## Toolbar
| Control | Wiring | Permission to act |
|---------|--------|-------------------|
| **Select all** | built into `SimfDataGrid` (Multiselect=true) | — |
| **Dialog ↔ full page** | `CrudPresentationToggle PageKey="interests" @bind-Value="_presentation"` — persists `simf.cp.prefs.interests` via `CpPreferences` | — |
| **Add** | `OnAddAsync` → `CrudShell` hosts `InterestAddEdit IsEdit=false` | `Interests.Create` |
| **Export** | `OnExportAsync` → `CrudGridExcel.ExportAsync(selectedIds, query)` | `Interests.Export` |
| **Import** | `OnImportAsync` → `CrudGridExcel.TriggerImportAsync()` | `Interests.Import` |
| **Edit** (per row) | `OnEditAsync(row)` → `InterestAddEdit IsEdit=true Initial=row` | `Interests.Edit` |
| **Details** (per row) | `OnDetailsAsync(row)` → `InterestViewDelete IsDelete=false` | `Interests.View` |
| **Deactivate** (per row) | `OnDeleteAsync(row)` → `InterestViewDelete IsDelete=true` → `SimfConfirm` → DELETE | `Interests.Delete` |

Bulk-delete, Copy/Paste/Duplicate are **not** wired (a per-row destructive action is
safer for a small lookup). Edit/Details open straight from the in-memory row — no
extra GET.

## Grid columns
| Column | Header resx | Source | Sortable | Filterable | Render |
|--------|-------------|--------|----------|------------|--------|
| Name | `Admin.Interests.Column.Name` ("Name") | `context.Name` | yes | yes | text |
| Name (Arabic) | `Admin.Interests.Column.NameArabic` ("Name (Arabic)") | `context.NameArabic` | yes | yes | text |
| Order | `Admin.Interests.Column.DisplayOrder` ("Order") | `context.DisplayOrder` | yes | no | integer |
| Status | `Admin.Interests.Column.Active` ("Status") | `context.IsActive` | no | no | `SimfPill` — `Variant="on"` **Active** / `Variant="off"` **Inactive** |

Pager labels: First page / Last page / Previous / Next / Show (page-size); summary
`Showing {0}–{1} of {2}`; page `Page {0} of {1}`. Empty body → `SimfEmptyState`
("No interests yet." / "لا توجد اهتمامات بعد.").

## Forms (hosted by `CrudShell`)
`CrudShell Open Presentation=_presentation Title=FormTitle` frames one of two forms;
`FormTitle` resolves to the right resx by mode (`Add.Title` / `Edit.Title` /
`Details.Title` / `Delete.Title`).

### Add / Edit — `InterestAddEdit` (`CrudAddEditFormBase<AdminInterestSummary>`)
| Field | Component | Required | MaxLength | Helper resx | Notes |
|-------|-----------|----------|-----------|-------------|-------|
| Name (English) | `SimfTextField` | yes | 128 | `…Field.NameHint` ("Up to 128 characters; must be unique.") | `Admin.Interests.Field.Name` |
| Name (Arabic) | `SimfTextField` | yes | 128 | `…Field.NameArabicHint` ("Up to 128 characters.") | `Admin.Interests.Field.NameArabic` |
| Display order | `SimfTextField Type="number"` | yes | n/a | `…Field.DisplayOrderHint` | `Value`/`ValueChanged`/`ValueExpression` (parses at submit) |
| Active | `SimfCheckbox` | **Edit only** | n/a | — | `Admin.Interests.Field.IsActive` ("Active — show in the visitor picker") |

Submit button: **Create interest** / **Save changes** (loading: **Creating** /
**Saving**); secondary **Cancel**. Client-side invalid → a `SimfAlert` at the top
(`…Field.NameInvalid` / `…Field.NameArabicInvalid` / `…Field.DisplayOrderInvalid`);
server error → the envelope's `MessageForCurrentCulture()`, fallback
`Admin.Interests.Fallback` ("The operation could not be completed.").

### Details / Deactivate — `InterestViewDelete` (`CrudViewDeleteFormBase<AdminInterestSummary>`)
A read-only `<dl class="simf-dl">` of **Name · Name (Arabic) · Order · Status**.
- **Details (IsDelete=false)** — details + **Close**.
- **Deactivate (IsDelete=true)** — details + a red **Deactivate** button →
  `SimfConfirm` (`Danger`, message `Admin.Interests.Delete.Message` naming the
  interest) → confirm fires the DELETE. On error the confirm closes first so the
  alert lands on the visible form body.

## Excel — `CrudGridExcel Resource="interests"`
A hidden `.xlsx` file input + an import-result `SimfModal`. Export →
`simfAccount.downloadXlsx("/account/api/admin/interests/export", …)`; import →
`simfAccount.uploadFile("/account/api/admin/interests/import", …)`. The result modal
shows `Created / Updated / Skipped` and a per-row error list; `OnImported` reloads the
grid, `OnError` raises the page's red alert.

## States
- **Loading** — `SimfDataGrid` shows its loading indicator (label "Loading interests…")
  while `LoadAsync` runs.
- **Populated** — rows + pager + status pills.
- **Empty** — `SimfEmptyState` ("No interests yet.").
- **Error (list)** — red `SimfAlert` with the server message or
  `Admin.Interests.LoadFailed` ("The interests could not be loaded.").
- **Form open** — dialog (over the grid) or full-page (replaces the grid) per the
  toggle.
- **Confirm** — `SimfConfirm` over the Deactivate form.

## i18n / RTL
- All visible strings via `IStringLocalizer<Strings>` (`Strings.resx` /
  `Strings.ar.resx`). Banner AR **الاهتمامات** / EN **Interests**.
- The header `العربية` / `English` toggle round-trips with `culture=ar|en`; Arabic
  sets `dir="rtl"` and the nav rail, grid headers, toolbar and form actions mirror.
- Bilingual data columns (Name / Name (Arabic)) show both; the app picker shows the
  locale's side of the pair.

## Accessibility
- Multiselect renders a Select-all checkbox (`Select all`) and per-row
  (`Select row`) labels; the grid `Caption` is announced.
- Forms: focus moves into the first field on open; ESC / backdrop closes a dialog
  (the `SimfConfirm` requires an explicit choice — no backdrop dismiss for the
  destructive step).
- Status uses `SimfPill` (Active/Inactive) — colour plus text, not colour alone.
- WCAG AA via `theme.tokens.css`; the `--focus-ring` token is visible on every
  focusable control.

## Related
- CP reference doc: [`../../pages/cp/admin-interests.md`](../../pages/cp/admin-interests.md)
- CP E2E catalogue: [`../../tests/e2e/cp-admin-interests.md`](../../tests/e2e/cp-admin-interests.md)
  (`E2E-INT-001…013`)
- Consuming app screen: [`../../App/Page_007-01/Page_007-01_Design.md`](../../App/Page_007-01/Page_007-01_Design.md)
- Format template: `docs/App/Page_016/Page_016_Design.md`
