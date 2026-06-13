# CP page — Design (العارضون · Exhibitors)

Blazor Control Panel layout. Source: `ExhibitorsList.razor` (the page),
`ExhibitorsAddEdit.razor` + `ExhibitorsViewDelete.razor` (the reusable forms,
hosted by `CrudShell`). RTL on the Arabic toggle (`<html dir="rtl" lang="ar">`).

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Layout (top → bottom, as built)
1. **`SimfBanner`** — title `Admin.Exhibitors.Title` ("Exhibitors" / "العارضون").
   Rendered only when `!GridHidden` (hidden when a form takes over the page in
   full-page mode).
2. **`.simf-page-wide > .simf-surface`** wrapper.
3. **Toast** — `SimfAlert` (`Variant` = `_toast.Variant`), shown when `_toast` is
   set.
4. **`SimfDataGrid<AdminExhibitorSummary>`** — the canonical CP list grid:
   - `Multiselect="true"`, `RowKey` = `r.Id`, `RowLabel` = `r.NameEn`.
   - **CustomToolbar:** `CrudPresentationToggle PageKey="exhibitors"
     @bind-Value="_presentation"`.
   - **Columns:**
     | Key | Header (resx) | Sortable | Filterable | Cell |
     |-----|---------------|:---:|:---:|------|
     | `nameEn` | `Admin.Exhibitors.Col.NameEn` | ✅ | ✅ | `NameEn` |
     | `nameAr` | `Admin.Exhibitors.Col.NameAr` | ✅ | ✅ | `NameAr` |
     | `accountCount` | `Admin.Exhibitors.Col.AccountCount` | — | — | `AccountCount` |
     | `isActive` | `Admin.Exhibitors.Col.Active` | ✅ | — | `SimfPill` on/off (`Grid.Active`/`Grid.Inactive`) |
   - **RowActions:** the toolbar Edit/Details/Delete quiet icons (from
     `SimfDataGrid`) plus a page-specific **`SimfToolbarButton` Icon="user"**
     (title `Admin.Exhibitors.Accounts`) wrapped in
     `<AuthorizedAction Permission="@PermissionCatalog.Exhibitors.Edit">` — the
     account-provisioning entry. It is the **only** individually gated affordance.
   - **EmptyTemplate:** `SimfEmptyState Title="@L[\"Admin.Exhibitors.None\"]"`.
   - Labels (Prev/Next/First/Last/PageSize/Add/Edit/Details/Delete/Export/Import/
     SelectAll/SelectRow/Actions) come from the shared `Grid.*` resx keys;
     loading label = `Admin.Exhibitors.Loading`.
5. **`CrudGridExcel @ref="_excel" Resource="exhibitors"`** — wired to
   `OnImported="OnImportedAsync"` + `OnError="OnExcelError"`.
6. **`CrudShell`** (when `FormOpen`) — `Presentation="_presentation"`,
   `Title="@FormTitle"`, `CloseLabel="@L["Admin.Exhibitors.Details.Close"]`. Hosts
   either `ExhibitorsAddEdit` (`_form == AddEdit`) or `ExhibitorsViewDelete`
   (`_form == ViewDelete`).
7. **Account-provisioning `SimfModal`** (when `_accountsOpen`) — independent of
   `CrudShell`; see below.

`FormTitle` resolves to `Admin.Exhibitors.Add.Title` / `Edit.Title` /
`Delete.Title` / `Details.Title` per `_form` + `_isEdit` / `_isDelete`.

## Add/Edit form (`ExhibitorsAddEdit`)
`@inherits CrudAddEditFormBase<AdminExhibitorDetail>`. A `.simf-form__fields`
block of `SimfTextField`s + a `ContactPicker`, then `.simf-form__actions`
(Save + optional Cancel).

| Field | Label (resx) | Control | MaxLength | Notes |
|-------|--------------|---------|:---:|-------|
| Name (English) | `Admin.Exhibitors.Field.NameEn` | `SimfTextField` | 256 | required (client guard) |
| Name (Arabic) | `Admin.Exhibitors.Field.NameAr` | `SimfTextField` | 256 | required (client guard) |
| Contact email | `Admin.Exhibitors.Field.ContactEmail` | `SimfTextField` | 320 | optional |
| Contact phone | `Admin.Exhibitors.Field.ContactPhone` | `SimfTextField` | 32 | optional |
| Website | `Admin.Exhibitors.Field.Website` | `SimfTextField` | 512 | optional |
| Contact | — | `ContactPicker` (`_form.ContactId`) | n/a | optional link to an active Contact (D-281) |
| Active | `Admin.Exhibitors.Field.IsActive` | `SimfCheckbox` | bool | **rendered only when `IsEdit`** |

- All controls disable while `_busy`.
- On `OnInitialized` with a non-null `Initial`, the form pre-fills from the
  detail (optional strings default to `string.Empty`; `IsActive` from detail).
- `_error` (a `SimfAlert Variant="error"`) shows the client guard message
  (`Admin.Exhibitors.NameRequired`) or the server envelope message.
- Buttons: **Save** (`Admin.Exhibitors.Save`, `Loading="_busy"`), **Cancel**
  (`Admin.Exhibitors.Cancel`, only when `OnCancel.HasDelegate`).

## View/Delete form (`ExhibitorsViewDelete`)
`@inherits CrudViewDeleteFormBase<AdminExhibitorDetail>`. A `<dl class="simf-dl">`
of read-only fields (NameEn, NameAr, Contact email, Contact phone, Website,
Active) — empty optional fields render **"—"**, Active renders
`Grid.Active`/`Grid.Inactive`.

- When `IsDelete=true`: a red `SimfButton Variant="danger"`
  (`Admin.Exhibitors.Action.Deactivate`) plus a **`SimfConfirm`** (Danger,
  title `Admin.Exhibitors.Delete.Title`, message
  `Admin.Exhibitors.Delete.Message` formatted with `Initial.NameEn`, confirm
  `Action.Deactivate`, cancel `Cancel`).
- Always a secondary **Close** (`Admin.Exhibitors.Details.Close`).
- Confirm → `DELETE …/{id}` via `simfAccount.deleteJson`.

## Account-provisioning modal
`SimfModal` titled `Admin.Exhibitors.Accounts.Title` formatted with the
exhibitor's English name. Body:
- An info `SimfAlert` (`Admin.Exhibitors.Accounts.Hint`).
- Existing accounts: a loading line (`Accounts.Loading`), else an empty
  `SimfEmptyState` (`Accounts.None`), else a `simf-table` (Contact name / Email /
  Role / Active; Role "—" when blank, Active "✓"/"—").
- A provision sub-form (`Provision.Heading`): `SimfTextField`s **Contact name**
  (256), **Email** (320), **Role label** (128), disabled while `_provisionBusy`.
- Footer: **Close** (`Accounts.Close`) + **Provision** (`Provision.Submit`,
  `Loading="_provisionBusy"`).

## States
- **Loading** — `SimfDataGrid Loading="_loading"`; the grid shows its loading
  label (`Admin.Exhibitors.Loading`) while `LoadAsync` runs.
- **Empty** — `SimfEmptyState` (`Admin.Exhibitors.None`) in the grid body; the
  toolbar Add stays visible.
- **Error** — a red toast: `Admin.Exhibitors.LoadFailed` (list/get),
  `Accounts.LoadFailed` (accounts), or the server envelope's
  `MessageForCurrentCulture()`; save/provision errors surface in-form / as toast.
- **Form open** — popup `CrudShell` (dialog) or full page (banner + grid hidden
  via `GridHidden`).

## i18n / RTL
- All strings via `IStringLocalizer<Strings>` (`Admin.Exhibitors.*` EN↔AR parity)
  plus shared `Grid.*` / `Grid.Import.*` keys.
- Banner "Exhibitors" / "العارضون"; columns "Name (English)" / "الاسم
  (بالإنجليزية)", "Name (Arabic)" / "الاسم (بالعربية)", "Accounts" / "الحسابات",
  "Active" / "نشط".
- Full RTL mirror on the Arabic toggle.

## Notes / deviations
- The account-provisioning modal uses a raw `simf-table` (not `SimfDataGrid`) —
  it is a small read-only inner list inside the modal, not a CP list page, so the
  D-258 "every list page uses `SimfDataGrid`" standard does not bind it.
- The CRUD action buttons (Add/Edit/Details/Delete) are **not** individually
  `<AuthorizedAction>`-wrapped; per-action enforcement is API-side. Only the
  Accounts icon is wrapped (`Exhibitors.Edit`).
</content>
