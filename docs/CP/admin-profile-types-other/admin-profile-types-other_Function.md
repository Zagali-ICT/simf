# Profile types — Other · Function

What the administrator does on `/admin/profile-types/other`. Grounded in
`OtherProfileTypesList.razor` + `ProfileTypeForm.razor`.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Purpose
Manage the **partner / staff** profile-type pool (Sponsor, Exhibitor, Media,
Staff, …) — the list the mobile sign-up "Other / أخرى" tab on
[Page 007](../../App/Page_007/README.md) shows as a required picker, and the pool
the `/admin/others` walk-in wizard consumes. One row per partner subtype, each
with a name (English + Arabic), a page/badge colour, a **Mobile-app role**, and an
active flag.

## Elements (top → bottom)
1. **Page banner** — `SimfBanner` titled `Admin.ProfileTypes.Other.Title`
   (EN **Other profile types** · AR **أنواع الملفات الأخرى**).
2. **Data grid** — `SimfDataGrid` (multiselect, filter box, pager, per-row icon
   actions) bound to `AdminProfileTypeSummary` rows. Columns, in order:
   | Key | Header (EN / AR) | Sortable | Filterable |
   |-----|------------------|----------|------------|
   | `userType` | Account type / نوع الحساب | no | no |
   | `name` | Name / الاسم | **yes** | **yes** |
   | `nameArabic` | Name (Arabic) / الاسم بالعربية | **yes** | **yes** |
   | `pageColor` | Page colour / لون الصفحة | no | no |
   | `mobileAppRole` | Mobile-app role / دور تطبيق الجوّال | no | no |
   | `isActive` | Active / نشط | no | no |

   - The **Account type** cell renders `ProfileTypeLabels.LocaliseUserType(row.UserType)`.
     Because the server hard-codes `row.UserType = "Visitor"` for every row, this
     cell shows the localised **UserType.Visitor** display name (see the drift note
     in [_Logic](admin-profile-types-other_Logic.md)).
   - The **Mobile-app role** cell renders the localised
     `Admin.ProfileTypes.MobileAppRole.{None|Staff|Moderator}` string.
   - The **Active** cell renders a `SimfPill` — `on` → **Yes** (`Active.Yes`),
     `off` → **No** (`Active.No`).
   - **Empty grid** → `SimfEmptyState` titled `Admin.ProfileTypes.None`
     (EN **No profile types yet.** · AR **لا توجد أنواع ملفات بعد.**).
3. **Toolbar / per-row actions** (wired through `SimfDataGrid` callbacks):
   **Add** (`OnAdd`), **Edit** (`OnEditOne`), **Details** (`OnDetailsOne`),
   **Deactivate** (`OnDeleteOne`).
4. **Toast** — a transient `SimfAlert` (success / error) after a save or delete.

## User steps

### Add a partner profile type
1. Click **Add profile type** (`Admin.ProfileTypes.Add.Title`).
2. The **Add** modal opens hosting `ProfileTypeForm` with `IsPartnerForm="true"`.
   Fields, in order:
   - **Account type** — read-only line showing `Admin.ProfileTypes.Scope.Partner`
     (EN **Partner / staff (Sponsor, Exhibitor, Media, …)** · AR **شريك / فريق
     (راعي، عارض، إعلام، …)**).
   - **Name (English)** (`Field.Name`) — hint `Field.NameHint`.
   - **Name (Arabic)** (`Field.NameArabic`) — hint `Field.NameArabicHint`.
   - **Page colour** (`Field.PageColor`) — a text input paired with a native
     colour swatch; hint `Field.PageColorHint`.
   - **Mobile-app role** (`Field.MobileAppRole`) — a `SimfSelect` with exactly
     **None / Staff / Moderator** (this picker is **partner-only**; the Visitor
     page hides it). Hint `Field.MobileAppRoleHint`.
   - **Visible in pickers (active)** (`Field.IsActive`) — checkbox, ticked by default.
3. Click **Create profile type** (`Submit.Create`). On success the modal closes,
   a green toast reads `Saved "{name}"` (`Admin.ProfileTypes.Saved`), and the grid
   reloads. **Cancel** (`Cancel`) discards without a POST.

### Edit a row
1. Click the **Edit** action → the **Edit** modal opens titled
   `Edit profile type — {name}` (`Edit.TitleFor`) with the values pre-filled.
2. The **Account type** line is read-only and shows a helper
   `Cannot be changed after creation.` (`UserType.Hint`). All other fields are editable.
3. Click **Save changes** (`Submit.Update`). Success → modal closes, green
   `Saved "{name}"` toast, grid reloads.

### View details (read-only)
1. Click **Details** → a read-only modal titled `Profile type details — {name}`
   (`Details.Title`) showing a description list: Account type, Name, Name (Arabic),
   Page colour, Mobile-app role, Active (Yes / No).
2. **Close** (`Details.Close`) dismisses it — no network call.

### Deactivate (soft-delete)
1. Click **Deactivate** → a confirm modal titled `Deactivate profile type`
   (`Delete.Title`) with the body `Delete.Confirm`
   (*Deactivate the profile type "{name}"? Existing users keep their assignment;
   the type stops appearing in pickers. …*).
2. **Deactivate** (`Delete.Submit`) fires the DELETE. Success → green
   `Deactivated "{name}"` toast (`Delete.Success`), grid reloads, row drops out.
   **Cancel** (`Delete.Cancel`) closes with no call. A row still referenced by a
   user returns **409** and surfaces the server message as a red toast (see
   [_Logic](admin-profile-types-other_Logic.md)).

## Navigation
- Reached from the left CP nav item `Module.AdminOtherProfileTypes`
  (`/admin/profile-types/other`).
- No outward navigation from the page — all actions open in-page modals.

## Acceptance criteria
- The grid loads on first render with the pinned filters `userType="Visitor"`
  **and** `isVisitor="false"`; both survive every filter / sort / page change.
- Name + Name (Arabic) are sortable and filterable; the Name filter narrows the
  grid server-side without dropping the pinned partner filter.
- The **Mobile-app role** column + picker are present (this is the partner page).
- Add / Edit validate Name, Arabic name and Page colour client-side before any
  network call; a blank field shows an in-modal `SimfAlert` and blocks submit.
- A duplicate name → **409** `ProfileTypeNameTaken`; an in-use deactivate →
  **409** `ProfileTypeInUse`; both surface the bilingual server message.
- The page is gated by `ProfileTypes.View`; a user lacking it lands on
  `/not-permitted`.

## E2E
Catalogue: [`docs/tests/e2e/cp-admin-profile-types-other.md`](../../tests/e2e/cp-admin-profile-types-other.md)
(`E2E-OPT-001` … `E2E-OPT-015`).
