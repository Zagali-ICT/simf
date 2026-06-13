# CP · Profile types — Other (`/admin/profile-types/other`)

Per-page documentation folder for the Control Panel **Other (partner / staff)
profile-types** admin page. Everything about this page lives here.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [admin-profile-types-other_Function.md](admin-profile-types-other_Function.md) | What the admin does — grid, Add / Edit / Details / Deactivate, filter / sort / page, toasts |
| Logic | [admin-profile-types-other_Logic.md](admin-profile-types-other_Logic.md) | Business rules — the pinned `Visitor` + `isVisitor=false` filter, the D-186 partner-side model, validation, name-uniqueness, in-use delete gate, audit |
| API | [admin-profile-types-other_API.md](admin-profile-types-other_API.md) | The backend endpoints + DTOs (authoritative contract) |
| Design | [admin-profile-types-other_Design.md](admin-profile-types-other_Design.md) | CP screen design — banner, `SimfDataGrid`, the four modals, RTL / bilingual, states |

## Identity
| | |
|---|---|
| Route | `/admin/profile-types/other` |
| Layout | `CpShellLayout` |
| Permission (page gate) | `[RequirePermission(PermissionCatalog.ProfileTypes.View)]` → code `"ProfileTypes.View"` |
| Page component | `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProfileTypes/OtherProfileTypesList.razor` |
| Shared form | `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProfileTypes/ProfileTypeForm.razor` (hosted with `IsPartnerForm="true"`) |
| Nav item | `Module.AdminOtherProfileTypes` under `Nav.…` → label AR **أنواع الملفات الأخرى** · EN **Other profile types** (`CpNavigation.cs`, `RequiredPermission = PermissionCatalog.ProfileTypes.View`, Icon `list`) |
| Banner / title | `Admin.ProfileTypes.Other.Title` → AR **أنواع الملفات الأخرى** · EN **Other profile types** |
| Audience | Administrator (and any role granted `ProfileTypes.*`) |
| Nature | **Lookup-table CRUD** — the partner / staff profile-type pool the app profile form (Page 007) offers under the "Other / أخرى" tab |
| Status | **Real / shipped** (D-118; partner-side model D-186; mobile-app role D-161) |

## What this page manages
This is the **partner / staff** side of the `ProfileTypes` lookup — the pool the
mobile sign-up "Other / أخرى" tab on **[Page 007](../../App/Page_007/README.md)**
shows as a **required** picker. After the D-186 `UserType` collapse, every
non-admin profile type is `UserType = Visitor`; the audience-vs-partner split
lives on `ProfileType.IsForVisitor`. This page pins the grid to
`userType = "Visitor"` **and** `isVisitor = "false"`, and its Add modal posts
`UserType = "Visitor"`, `IsVisitor = false`. Because these rows are partner /
staff, this page also exposes the **Mobile-app role** column + picker
(None / Staff / Moderator) — the sibling Visitor page hides it.

## Linkage (C5 / D-371)
- **App profile form — [Page 007](../../App/Page_007/README.md)** reads this pool
  through `GET /api/v1/app/account/profile-types?isVisitor=false`
  (`ProfileTypesPickerEndpoint`). Under the **Other / أخرى** tab the picker is
  shown and a pick is **required** (C5, D-371); deactivating a row here removes
  it from that picker.
- **Walk-in wizard — `/admin/others`** consumes the same partner pool when an
  admin desk-registers an Other account (`AdminCreateOtherRequest.ProfileTypeId`
  must reference an active `IsVisitor = false` row).

## Related docs (cross-links)
- **CP reference (content source):** [`docs/pages/cp/admin-profile-types-other.md`](../../pages/cp/admin-profile-types-other.md)
- **CP E2E catalogue:** [`docs/tests/e2e/cp-admin-profile-types-other.md`](../../tests/e2e/cp-admin-profile-types-other.md) (`E2E-OPT-001` … `E2E-OPT-015`)
- **Visitor counterpart (sibling set):** [`docs/CP/admin-profile-types-visitor/`](../admin-profile-types-visitor/README.md) — same backend, audience scope (`isVisitor=true`), no Mobile-app role column
- **App profile-data form:** [`docs/App/Page_007/README.md`](../../App/Page_007/README.md)

## Shared backend (read this first)
The Visitor and Other CP pages share **one** set of admin endpoints
(`/api/v1/admin/profile-types*`) and **one** `ProfileTypeForm.razor`. The only
differences between the two pages are the pinned grid filter (`isVisitor`), the
`IsPartnerForm` form flag, and the Mobile-app-role column visibility. See
[admin-profile-types-other_API.md](admin-profile-types-other_API.md) and
[admin-profile-types-other_Logic.md](admin-profile-types-other_Logic.md).

## Sources of truth (read first)
`OtherProfileTypesList.razor` + `ProfileTypeForm.razor` + `ProfileTypeLabels.cs`
(the page) · `ProfileTypeEndpoints.cs` + `ListProfileTypesEndpoint.cs` +
`AdminProfileTypeCommandService.cs` + `AdminProfileTypeQueryService.cs` +
`AdminProfileTypeRequestValidators.cs` (the API) · `AdminAccount.cs` (the DTOs) ·
`PermissionCatalog.cs` + `CpNavigation.cs` (the gates / nav) ·
`ProfileTypesPickerEndpoint.cs` (the app read) · `Strings.resx` / `Strings.ar.resx`
(the labels) · DECISIONS_LOG **D-115 / D-118 / D-120 / D-125 / D-161 / D-186 /
D-190 / D-371**.
