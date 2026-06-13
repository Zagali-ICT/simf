# Profile types — Other · Logic

Business rules behind `/admin/profile-types/other`. Grounded in
`OtherProfileTypesList.razor`, `ProfileTypeForm.razor`,
`AdminProfileTypeCommandService.cs`, `AdminProfileTypeQueryService.cs`,
`AdminProfileTypeRequestValidators.cs`, and `ProfileTypesPickerEndpoint.cs`.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## The D-186 partner-side model (core rule)
After the D-186 `UserType` collapse, **every** non-admin profile type is stored
with `UserType = Visitor`. The audience-vs-partner distinction now lives on the
boolean **`ProfileType.IsForVisitor`** (`true` = audience, `false` = partner / staff).

- This page is the **partner / staff** queue. It pins the grid query to
  `Filters["userType"] = "Visitor"` **and** `Filters["isVisitor"] = "false"`
  (`_query` initialiser), and re-pins both on every `OnQueryChangedAsync` so a
  filter / sort / page change can never widen the scope.
- The Add modal hosts `ProfileTypeForm` with **`IsPartnerForm="true"`**, which
  makes Create send `UserType = "Visitor"`, `IsVisitor = false`.
- The sibling **Visitor** page is identical but pins `isVisitor = "true"` and
  hosts the form with `IsPartnerForm="false"`.

## Auth gate
- Page: `[RequirePermission(PermissionCatalog.ProfileTypes.View)]` → a user
  lacking `"ProfileTypes.View"` (and not the `Administrator = "*"` wildcard) is
  redirected to `/not-permitted`.
- Every backing endpoint is policy-gated per action (View / Create / Edit /
  Delete) **and** `RequireApprovedAccount` — see [_API](admin-profile-types-other_API.md).

## List query (server-side)
`AdminProfileTypeCommandService.ListAllAsync` over `SimfAppDbContext.ProfileTypes`
(`AsNoTracking`):
- `skip = max(0, query.Skip)`; `top = clamp(query.Top>0 ? query.Top : 25, 1, 200)`
  (the page sets `Top = 20`).
- `query.Search` (if set) → `LIKE %term%` over `Name` OR `NameArabic`.
- `Filters["name"]` → `LIKE %name%` over `Name`.
- `Filters["isActive"]` (bool) → `IsActive == value`.
- `Filters["isVisitor"]` (bool) → `IsForVisitor == value` — **this is how the
  Other page is constrained to partner rows** (`isVisitor = false`).
- Sort: `name` / `namearabic` asc|desc, `createdat` asc; default `OrderBy(Name)`.
- Projects each row to `AdminProfileTypeSummary` with `UserType = nameof(UserType.Visitor)`
  (always `"Visitor"`) and `MobileAppRole = role.ToString()`.

## App picker read (the linkage)
`ProfileTypesPickerEndpoint` — `GET /api/v1/app/account/profile-types`:
- Returns active rows (`IsActive`) only.
- Optional `?isVisitor=false` → `IsForVisitor == false` (partner rows) — this is
  the read [Page 007](../../App/Page_007/README.md) issues under the Other tab.
- Auth-required but **not** approval-gated (the caller is mid-registration);
  Visitor-scope only (Admin-scope rows are never surfaced).
- A row deactivated on this CP page disappears from the app picker.

## Validation
Two layers, both must pass:

**Client-side (`ProfileTypeForm.HandleSubmitAsync`)** — blocks the network call:
- `Name` non-blank and ≤ 128 → else `Field.NameInvalid`
  (*Name must be 1–128 characters.*).
- `NameArabic` non-blank and ≤ 128 → else `Field.NameArabicInvalid`.
- `PageColor` non-blank and ≤ 32 → else `Field.PageColorInvalid`.
The failing message renders as an in-modal `SimfAlert`; the modal stays open.

**Server-side (`AdminCreate/UpdateProfileTypeRequestValidator`)** — FluentValidation,
limits mirror the EF column maxes: `UserType` required + ≤ 16 (create only),
`Name` / `NameArabic` required + ≤ 128, `PageColor` required + ≤ 32. Bilingual messages.

## Create rules (`CreateAsync`)
- **Scope guard:** `UserType` must parse to `UserType.Visitor`; anything else →
  **400** `ProfileTypeInvalidUserType` (*A profile type may only be created for
  the Visitor scope.*). This page always sends `"Visitor"`, so it never trips —
  but it means there is **no** `userType=Other` wire value (the route docs say
  "Other" only as the page intent).
- **Name uniqueness:** case-insensitive `Name` clash across the **whole**
  `ProfileTypes` table → **409** `ProfileTypeNameTaken` (*A profile type named
  '{name}' already exists for {userType}.*). Note: uniqueness is global by Name,
  not scoped per `IsForVisitor`.
- **Mobile-app role:** `ParseMobileAppRole` — null/blank → `None`; unknown → 400
  `ProfileTypeInvalidUserType`; `Visitor` is **rejected** (the Visitor mapping is
  resolved from `UserType` at JWT issue time, never from a ProfileType row).
- Persists with `IsForVisitor = request.IsVisitor` (`false` from this page),
  `CreatedAt = now`. Audits `ProfileTypeCreated` with
  `Detail = "id=…; userType=Visitor; name=…"`.

## Update rules (`UpdateAsync`)
- 404 `ProfileTypeNotFound` if the id is missing.
- Re-checks Name uniqueness only when the name changed → 409 `ProfileTypeNameTaken`.
- `UserType` is **not** updatable (absent from the route body).
- **`IsVisitor` is mutable** — the form sends `Initial.IsVisitor` unchanged, so a
  normal edit on this page keeps the row partner-side. Flipping it re-routes the
  row between the CP Visitors / Others approval queues; the underlying user
  accounts stay `UserType.Visitor` either way.
- **Audit (threat-detection H-1):** the prior `IsForVisitor` is captured before
  the mutation. On a flip, the audit Detail records `isVisitorChanged=true`,
  old/new values, and `linkedAccountCount` (count of `UserProfiles` referencing
  the row) so SOC can gauge blast radius. No flip → `isVisitorChanged=false`.

## Mobile-app role payload rule (D-161)
- The picker is shown only when `ShowMobileAppRolePicker` is true — Edit: when
  `Initial.IsVisitor == false`; Create: when `IsPartnerForm` (this page). So the
  field is present here and absent on the Visitor page.
- `MobileAppRole` is sent only when the picker was shown; otherwise it is `null`
  (the backend defaults to `None`). Options: **None / Staff / Moderator**
  (`Visitor` is intentionally omitted).

## Deactivate rules (`DeactivateAsync`)
- 404 `ProfileTypeNotFound` if missing.
- **In-use gate:** any `UserProfile.ProfileTypeId == id` → **409** `ProfileTypeInUse`
  (*The profile type cannot be removed while it is still assigned to one or more
  accounts.*). The CP surfaces the bilingual server message; if absent it falls
  back to `Admin.ProfileTypes.Delete.InUse`.
- Idempotent — an already-inactive row returns without re-writing.
- Soft-delete: sets `IsActive = false`, `UpdatedAt = now`, audits
  `ProfileTypeDeactivated`. (List endpoints / the app picker filter on `IsActive`.)

## Failure handling (CP)
- `LoadAsync` falls back to `GridPage.Of(empty)` on a non-success envelope — a
  500 on `/list` resolves to an empty grid, no crash (E2E-OPT-014).
- Save / delete errors surface `envelope.Error.MessageForCurrentCulture()` (the
  bilingual server message) as a toast / in-modal alert, else a fixed fallback
  (`Admin.ProfileTypes.Fallback` for save, `Delete.InUse` for delete).

## Page colour handling (D-120)
- The text input is the source of truth (CSS-variable / short-hex friendly).
- The native swatch is seeded from `PageColorAsHex` — the model value when it
  matches `^#[0-9A-Fa-f]{6}$`, else a brand-neutral `#244A77` for display only;
  picking from the swatch writes `#rrggbb` back into the text input.

## Data ↔ Identity separation
`ProfileTypes`, `UserProfiles` and the audit log all live in **`SIMF_App`**
(`SimfAppDbContext`). No cross-database relation is involved on this page.

## Drift / code-vs-doc note
The **list grid** and the **Details modal** render the Account-type column /
field via `ProfileTypeLabels.LocaliseUserType(row.UserType)`, and `row.UserType`
is hard-coded to `"Visitor"` — so they display the localised **UserType.Visitor**
display name, **not** the `Scope.Partner` string. Only the **Add / Edit form**
shows `Admin.ProfileTypes.Scope.Partner` (via `LocalisedUserType`). The existing
CP-reference and E2E docs that say the **grid / Details** show "Partner / staff
(…)" describe the form, not the grid — a documentation drift, reported only (no
code change).
