# CP page — Design (أنواع ملفات الزوار · Visitor profile types)

Blazor (Control Panel) page design. As built in
`VisitorProfileTypesList.razor` + the reusable `ProfileTypeForm.razor`.
RTL-capable, Arabic/English bilingual via `IStringLocalizer<Strings>`.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

> **Shared form note.** The Add and Edit modals host the **same**
> `ProfileTypeForm.razor` used by the Other page. On this Visitor page the
> form is rendered with **`IsPartnerForm="false"`**, which (1) sends
> `IsVisitor=true` on Create and (2) **hides the Mobile-app role picker**
> (that field renders only on the Other / partner side). See the Logic doc
> L-5.

## Layout (top → bottom, as built)
1. **Page banner** — `SimfBanner Title="@L["Admin.ProfileTypes.Visitor.Title"]"`
   (AR **أنواع ملفات الزوار**). `<PageTitle>` = the same title + ` · SIMF`.
2. **Surface** — `.simf-page-wide > .simf-surface` wrapping the grid.
3. **Data grid** — `SimfDataGrid<AdminProfileTypeSummary>` with the full
   toolbar (`Multiselect="true"` row checkboxes, filter box, numbered pager,
   Add / per-row Edit / Details / Delete actions). Columns, left→right:

   | Key | Header (resx) | Cell |
   |-----|---------------|------|
   | `userType` | `Admin.ProfileTypes.Column.UserType` | `ProfileTypeLabels.LocaliseUserType(context.UserType)` — the localised scope label (EN **Visitor (audience)** / AR **زائر (جمهور)**) |
   | `name` | `Admin.ProfileTypes.Column.Name` | `@context.Name` — **Sortable + Filterable** |
   | `nameArabic` | `Admin.ProfileTypes.Column.NameArabic` | `@context.NameArabic` — **Sortable + Filterable** |
   | `pageColor` | `Admin.ProfileTypes.Column.PageColor` | `@context.PageColor` (raw string) |
   | `isActive` | `Admin.ProfileTypes.Column.Active` | `SimfPill Variant="on"` → `Admin.ProfileTypes.Active.Yes`, else `Variant="off"` → `Admin.ProfileTypes.Active.No` |

   - **Empty template:** `SimfEmptyState Title="@L["Admin.ProfileTypes.None"]"`.
   - **Pager labels:** First / Prev / Next / Last + page-size, summary +
     page formatter (`FormatSummary` / `FormatPage`), all from the
     `Admin.Users.Pager.*` resx keys (shared with the users grid).
4. **Toast** — when `_toast` is set, a `.simf-toast` wrapper renders a
   `SimfAlert` with the toast variant (`success` / `error`).

## The four modals (`SimfModal`)
All four are conditionally rendered from page state fields.

### Add (`_addOpen`)
- Title `Admin.ProfileTypes.Add.Title`.
- Hosts `<ProfileTypeForm IsPartnerForm="false" OnSuccess=… OnCancel=… />`
  (no `Initial` → Create mode).

### Edit (`_editTarget`)
- Title `string.Format(Admin.ProfileTypes.Edit.TitleFor, _editTarget.Name)`
  (e.g. "Edit profile type — {name}").
- Hosts `<ProfileTypeForm Initial="_editTarget" OnSuccess=… OnCancel=… />`
  (`Initial` set → Edit mode; `IsPartnerForm` not passed, so the form derives
  audience/partner from `Initial.IsVisitor`).

### Details (`_detailsTarget`)
- Title `string.Format(Admin.ProfileTypes.Details.Title, _detailsTarget.Name)`.
- Body is a **read-only** `<dl class="simf-dl">` description list:
  Account type (`LocaliseUserType`), Name, Name (Arabic), Page colour, and
  Active (Yes/No). **No editable fields, no Save.**
- Footer: a single secondary **Close** button (`Admin.ProfileTypes.Details.Close`).

### Delete / Deactivate (`_deleteTarget`)
- Title `Admin.ProfileTypes.Delete.Title`.
- Body: `string.Format(Admin.ProfileTypes.Delete.Confirm, _deleteTarget.Name)`.
- Footer: secondary **Cancel** (`Admin.ProfileTypes.Delete.Cancel`) +
  primary **Deactivate** (`Admin.ProfileTypes.Delete.Submit`, `Loading="_busy"`).

## The form (`ProfileTypeForm.razor`)
An `EditForm` (`class="simf-form"`, `OnSubmit="HandleSubmitAsync"`) with the
fields, top → bottom:

1. **Account type** — a read-only display row (`.simf-field`): label
   `Admin.ProfileTypes.Field.UserType`, value = `LocalisedUserType`
   (Audience vs Partner label). In **Edit** mode a helper line
   (`Admin.ProfileTypes.UserType.Hint`) explains it cannot change. **Not an
   input** — UserType is never editable.
2. **Name (English)** — `SimfTextField`, helper `…Field.NameHint`,
   `@bind-Value="_model.Name"`.
3. **Name (Arabic)** — `SimfTextField`, helper `…Field.NameArabicHint`,
   `@bind-Value="_model.NameArabic"`.
4. **Page colour (D-120 paired control)** — a `.simf-field` holding a
   `.simf-field__control--with-swatch` with **two** native inputs:
   - a **text** `<input type="text">` bound via `OnPageColorTextInput`
     (the **source of truth** — accepts `#rrggbb`, 3-digit hex, or a
     `var(--…)` CSS variable; `aria-describedby` the hint);
   - a **`<input type="color">` swatch** whose `value="@PageColorAsHex"`
     (falls back to navy **`#244A77`** when the text isn't a canonical
     6-hex value) and whose `OnPageColorSwatchInput` writes the picked
     `#rrggbb` back into the text input.
   - helper: `Admin.ProfileTypes.Field.PageColorHint`.
5. **Mobile-app role** — `SimfSelect` over `{ None, Staff, Moderator }`,
   **rendered only when `ShowMobileAppRolePicker` is true** → on this
   **Visitor page it does NOT render** (Create: `IsPartnerForm=false`;
   Edit: `Initial.IsVisitor==true`). Documented here for completeness; see
   the Other set for its visible behaviour.
6. **Active** — `SimfCheckbox` bound to `_model.IsActive` (defaults `true`),
   label `Admin.ProfileTypes.Field.IsActive`.
7. **Actions** — primary submit (`SubmitLabel` = `…Submit.Create` or
   `…Submit.Update`; `Loading="_busy"`, `LoadingLabel=…Submitting`) +
   secondary **Cancel** (`…Cancel`, rendered only when `OnCancel` is wired).

Above the form, a `SimfAlert Variant="success"` (`_success`) or
`Variant="error"` (`_error`) surfaces the create/update result or the
validation / server error.

## States
- **Loading** — `SimfDataGrid` shows its loading label (`Admin.Users.Loading`)
  while `_loading` is true during `LoadAsync`.
- **Empty** — the grid's `EmptyTemplate` renders `SimfEmptyState`
  (`Admin.ProfileTypes.None` — EN "No profile types yet." / AR "لا توجد أنواع ملفات بعد.").
  Also reached when the list call returns a non-success envelope (the page
  falls back to `GridPage.Of(empty)` — see Logic L-7).
- **Toast** — success (green) after create/update/deactivate; error (red)
  on a failed deactivate (in-use 409 etc.).
- **Modal validation** — blank/too-long Name, Name (Arabic) or Page colour
  surface an inline `SimfAlert` inside the modal **before** any POST
  (client guard — Logic L-6).

## RTL / localization
- All labels, headers, pills, toasts and modal copy come from
  `IStringLocalizer<Strings>` — Arabic primary, English secondary; the page
  mirrors to RTL when the UI language is Arabic (`<html dir="rtl" lang="ar">`).
- The Account-type column + the form's Account-type row render the localised
  **scope** label (Audience / Partner), not the raw enum — via
  `ProfileTypeLabels.LocaliseUserType` and `LocalisedUserType`.
- The Active pills read **نعم / لا** (Yes / No) per locale.
