# CP page — Function (أنواع ملفات الزوار · Visitor profile types)

What the administrator does on this Control Panel page. Grounded in
`VisitorProfileTypesList.razor` + `ProfileTypeForm.razor`.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

## Privilege / auth gate
**Administrator (or any role granted `ProfileTypes.*`).** The page carries
`@attribute [RequirePermission(PermissionCatalog.ProfileTypes.View)]` and the
nav item is hidden without it; a signed-in admin lacking `ProfileTypes.View`
lands on `/not-permitted`. Each write action is additionally gated at the API
(`ProfileTypes.Create` / `.Edit` / `.Delete` — see the API doc). `Administrator`
holds the `*` wildcard so it passes all four.

## Elements
- A **data grid** of visitor profile-type rows (columns: Account type, Name,
  Name (Arabic), Page colour, Active pill).
- A **toolbar**: Add button, filter box (filters on Name), numbered pager
  (First / Prev / Next / Last + page size), and row checkboxes (multiselect).
- **Per-row actions:** Edit, Details, Deactivate.
- Four **modals**: Add, Edit, Details (read-only), and a Deactivate confirm.

## What the admin does
1. **Browse** the visitor profile-type list. Every row shows the localised
   scope label (Visitor (audience) / زائر (جمهور)), both names, the raw
   PageColor string and an on/off Active pill. The list is always pinned to
   the **Visitor** scope (`UserType=Visitor` + `IsVisitor=true`).
2. **Add a profile type** — click Add → the Add modal opens hosting
   `ProfileTypeForm` (`IsPartnerForm="false"`). Fill **Name (English)**,
   **Name (Arabic)**, **Page colour** (type a hex / CSS variable, or pick from
   the native swatch), leave **Active** ticked → submit. On success the modal
   closes, a green toast reads the localised "Created …" / "Saved …" text and
   the grid reloads. The created row carries `UserType=Visitor`, `IsVisitor=true`.
   *(The Mobile-app role picker does **not** appear on this page.)*
3. **Edit a profile type** — click Edit on a row → the Edit modal opens with
   the row pre-filled (Account-type row read-only). Change the names, the
   colour, or the Active flag → Save changes → green toast + grid reload.
   **UserType cannot be changed** (it is never sent in the PUT body); the
   row keeps its existing `IsVisitor` flag.
4. **View details** — click Details → a read-only description-list modal shows
   Account type, Name, Name (Arabic), Page colour and Active; **Close**
   dismisses it. No network call fires.
5. **Deactivate (soft-delete) a profile type** — click Deactivate → confirm in
   the modal → Deactivate. On success a green toast reads the localised
   "Deactivated …" text and the row drops from the active-filtered list.
   If any user account still references the row, the server returns **409**
   and a **red toast** surfaces the bilingual "still assigned …" message; the
   row stays Active (Logic L-4).
6. **Filter / sort / page** — type in the filter box to narrow by Name; click
   the Name / Name (Arabic) headers to sort; use the pager to move through
   pages and change page size. On every grid change the page **re-applies**
   the `userType=Visitor` + `isVisitor=true` pins so the scope can never widen.

## Validation the admin sees
- **Name (English)** required, **1–128** characters (client guard +
  server validator) — else the modal shows the bilingual `…Field.NameInvalid`.
- **Name (Arabic)** required, **1–128** characters — `…Field.NameArabicInvalid`.
- **Page colour** required, **1–32** characters — `…Field.PageColorInvalid`.
- **Duplicate name** within the Visitor scope → server **409**
  `PROFILE_TYPE_NAME_TAKEN`, surfaced in the modal verbatim (bilingual).
- **Deactivate while in use** → server **409** `PROFILE_TYPE_IN_USE`, red toast.

## Acceptance criteria
- Only a caller with `ProfileTypes.View` can open the page; only
  `ProfileTypes.Create/.Edit/.Delete` (or the `*` wildcard) can perform the
  matching write.
- Every list / sort / page / filter request carries `userType=Visitor` and
  `isVisitor=true` — a Visitor row can never be created or listed under the
  Other pool.
- Add → grid shows the new row; Edit → values update without touching
  UserType; Details → read-only; Deactivate → row leaves the active list
  unless it is in use (then 409 + the row stays).
- All copy, pills, toasts and errors are bilingual and the page mirrors to RTL
  in Arabic.

## Where it fits
This is a **reference-data / config** page. Its rows are consumed by the
mobile sign-up profile form (**[Page_007](../../App/Page_007/)**) via
`GET /app/account/profile-types?isVisitor=true` (D-190). On that screen the
**Visitor** tab auto-locks to the single seeded **"Normal" / "عادي"** row
(C5 / D-371 — no picker); the partner picker (Other tab) shows the full list.
The partner / Other side of the same table is managed by the sibling page at
[`../admin-profile-types-other/`](../admin-profile-types-other/).
