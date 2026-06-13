# Profile types — Other · Design

Control Panel screen design for `/admin/profile-types/other`. Blazor Server,
`CpShellLayout`, bilingual (EN / AR with RTL). Grounded in
`OtherProfileTypesList.razor` + `ProfileTypeForm.razor` + `Strings(.ar).resx`.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Layout (top → bottom)
1. **Page banner** — `SimfBanner Title="@L["Admin.ProfileTypes.Other.Title"]"`
   (EN **Other profile types** · AR **أنواع الملفات الأخرى**).
2. **Wide surface** — `<div class="simf-page-wide"><div class="simf-surface">`
   wrapping the grid (the owner-mandated full-width list-page frame).
3. **Data grid** — `SimfDataGrid` (`TItem = AdminProfileTypeSummary`,
   `Multiselect=true`), with filter box, pager, per-row icon actions, and the
   columns listed in [_Function](admin-profile-types-other_Function.md). Caption +
   labels all come from resx keys (`Admin.Users.*` for the shared grid chrome,
   `Admin.ProfileTypes.*` for this page).
4. **Modals** (rendered conditionally, `SimfModal`): Add, Edit, Details, Deactivate.
5. **Toast** — `<div class="simf-toast"><SimfAlert>` for success / error feedback.

## Grid columns (visual)
| Column | Render |
|--------|--------|
| Account type | localised `LocaliseUserType(row.UserType)` (UserType.Visitor display name — see drift note in _Logic) |
| Name | plain text |
| Name (Arabic) | plain text |
| Page colour | the raw colour string (e.g. `#FFD700`) |
| Mobile-app role | localised `Admin.ProfileTypes.MobileAppRole.{None\|Staff\|Moderator}` |
| Active | `SimfPill` — `on` → **Yes** (green), `off` → **No** |

Empty grid → `SimfEmptyState` titled `Admin.ProfileTypes.None`.

## The four modals

### Add (`Admin.ProfileTypes.Add.Title`)
Hosts `ProfileTypeForm IsPartnerForm="true"`. The form (`simf-form`) shows, in order:
- **Account type** — a read-only `simf-field` showing `LocalisedUserType` =
  `Admin.ProfileTypes.Scope.Partner` (EN **Partner / staff (Sponsor, Exhibitor,
  Media, …)** · AR **شريك / فريق (راعي، عارض، إعلام، …)**).
- **Name (English)** — `SimfTextField` + hint.
- **Name (Arabic)** — `SimfTextField` + hint.
- **Page colour** — a paired `<input type="text">` + `<input type="color">`
  swatch (`simf-field__control--with-swatch`) + hint.
- **Mobile-app role** — `SimfSelect` (None / Staff / Moderator) + hint
  (**partner-only** — shown here, hidden on the Visitor page).
- **Visible in pickers (active)** — `SimfCheckbox`, ticked by default.
- **Actions** — primary **Create profile type** (`Submit.Create`, loading label
  `Submitting`) + secondary **Cancel** (`Cancel`).
Validation errors render as a `SimfAlert variant="error"` at the top of the form.

### Edit (`Admin.ProfileTypes.Edit.TitleFor` → "Edit profile type — {name}")
Same `ProfileTypeForm` with `Initial` pre-filled. The Account-type line is
read-only with a helper **Cannot be changed after creation.** (`UserType.Hint`).
Primary action **Save changes** (`Submit.Update`).

### Details (`Admin.ProfileTypes.Details.Title` → "Profile type details — {name}")
A read-only `<dl class="simf-dl">` listing Account type, Name, Name (Arabic),
Page colour, Mobile-app role, Active (Yes / No). Footer: secondary **Close**
(`Details.Close`). No inputs, no save.

### Deactivate (`Admin.ProfileTypes.Delete.Title` → "Deactivate profile type")
Body `Delete.Confirm` (*Deactivate the profile type "{name}"? Existing users keep
their assignment; the type stops appearing in pickers. …*). Footer: secondary
**Cancel** (`Delete.Cancel`) + primary **Deactivate** (`Delete.Submit`, shows
`Loading="_busy"`).

## States
- **Loading** — `SimfDataGrid` shows its loading indicator (`_loading`, label
  `Admin.Users.Loading`).
- **Empty** — `SimfEmptyState` (`Admin.ProfileTypes.None`).
- **Populated** — the row grid with pager summary (`Admin.Users.Pager.Summary`)
  and page indicator (`Admin.Users.Pager.Page`).
- **Saved** — green toast `Saved "{name}"` (`Admin.ProfileTypes.Saved`).
- **Deactivated** — green toast `Deactivated "{name}"` (`Delete.Success`).
- **Error** — red toast / in-modal `SimfAlert` carrying the bilingual server
  message (or `Fallback` / `Delete.InUse`).
- **List failure** — non-success envelope resolves to an empty grid (no crash).

## RTL / localization
- Whole page mirrors RTL when the UI language is العربية (`<html dir="rtl">`); the
  nav rail, banner, grid, and modal action order all flip.
- Every label is an `IStringLocalizer<Strings>` key — both `Strings.resx` (EN) and
  `Strings.ar.resx` (AR) carry the `Admin.ProfileTypes.*` and `Module.AdminOtherProfileTypes`
  entries. Sample bilingual pairs:
  | Key | EN | AR |
  |-----|----|----|
  | `Admin.ProfileTypes.Other.Title` | Other profile types | أنواع الملفات الأخرى |
  | `Admin.ProfileTypes.Scope.Partner` | Partner / staff (Sponsor, Exhibitor, Media, …) | شريك / فريق (راعي، عارض، إعلام، …) |
  | `Admin.ProfileTypes.Column.MobileAppRole` | Mobile-app role | دور تطبيق الجوّال |
  | `Admin.ProfileTypes.MobileAppRole.Staff` | Staff — gate operations | موظف — تشغيل البوابات |

## CSS / theme
Uses the shared CP classes (`simf-page-wide`, `simf-surface`, `simf-form`,
`simf-field`, `simf-dl`, `simf-toast`) and the `Simf*` component library
(`SimfBanner`, `SimfDataGrid`, `SimfModal`, `SimfTextField`, `SimfSelect`,
`SimfCheckbox`, `SimfButton`, `SimfPill`, `SimfAlert`, `SimfEmptyState`). No inline
styles; colours flow from theme tokens. The page-colour swatch is the one native
`<input type="color">` (a data value the admin edits, not a UI theme colour).

## Difference from the Visitor sibling
Same component tree and modals; this page (a) pins the grid to `isVisitor=false`,
(b) hosts the form with `IsPartnerForm="true"`, and (c) therefore **shows the
Mobile-app role column + picker**, which the Visitor page omits. See the sibling
set at [`docs/CP/admin-profile-types-visitor/`](../admin-profile-types-visitor/README.md).
