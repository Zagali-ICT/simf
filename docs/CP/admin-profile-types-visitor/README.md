# CP page — أنواع ملفات الزوار · Visitor profile types (`/admin/profile-types/visitor`)

Per-page documentation folder for the Control Panel config page. Everything
about this admin page lives here. Companion of the Other-side set at
[`../admin-profile-types-other/`](../admin-profile-types-other/) — **the two
pages share one backend and one `ProfileTypeForm.razor`**; this set documents
the **Visitor** route.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [admin-profile-types-visitor_Function.md](admin-profile-types-visitor_Function.md) | What the admin does — grid CRUD, the Add/Edit/Details/Delete modals, filter/sort/page, the PageColor swatch |
| Logic | [admin-profile-types-visitor_Logic.md](admin-profile-types-visitor_Logic.md) | The `UserType=Visitor` + `IsVisitor=true` pins, per-UserType name uniqueness, in-use delete guard, the MobileAppRole-picker hide rule, validation alignment |
| API | [admin-profile-types-visitor_API.md](admin-profile-types-visitor_API.md) | The five admin endpoints + DTOs + error codes (authoritative contract), and the BFF proxy the page actually calls |
| Design | [admin-profile-types-visitor_Design.md](admin-profile-types-visitor_Design.md) | Blazor page design — `SimfBanner` + `SimfDataGrid` columns/toolbar, the four modals, the paired colour control, RTL |

## Identity
| | |
|---|---|
| Route | `/admin/profile-types/visitor` |
| Surface | **Control Panel** (Blazor Server) |
| Source | [`VisitorProfileTypesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProfileTypes/VisitorProfileTypesList.razor) + reusable child [`ProfileTypeForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProfileTypes/ProfileTypeForm.razor) |
| Layout | `CpShellLayout` |
| Page title | `Admin.ProfileTypes.Visitor.Title` (AR **أنواع ملفات الزوار**) |
| Page permission | **`PermissionCatalog.ProfileTypes.View`** (`@attribute [RequirePermission(...)]`) |
| Nav item | `CpNavigation` `Module.AdminVisitorProfileTypes` → `/admin/profile-types/visitor`, `RequiredPermission = ProfileTypes.View`, icon `list` |
| Audience | Administrator (and any role granted `ProfileTypes.*`) |
| Status | **✅ Real / shipped** — D-115 backend, D-118 CP pages, D-186 audience-vs-partner split via `IsVisitor`, D-120 colour picker, D-161 MobileAppRole |

## Sources of truth (read first)
- `VisitorProfileTypesList.razor` + `ProfileTypeForm.razor` (the page — the
  visible behaviour) ·
- `src/Backend/SIMF.Api/Endpoints/Admin/ProfileTypeEndpoints.cs` (the five admin
  endpoints) + `AdminProfileTypeCommandService.cs` (the rules + the bilingual
  error text) + `AdminProfileTypeRequestValidators.cs` (field-shape) ·
- `src/Shared/SIMF.Common/PermissionCatalog.cs` `ProfileTypes` (the gates) +
  `CpNavigation.cs` (the rail entry) ·
- `src/Shared/SIMF.Contracts/Authentication/AdminAccount.cs`
  (`AdminProfileTypeSummary`, `AdminCreateProfileTypeRequest`,
  `AdminUpdateProfileTypeRequest`).

## Cross-links
- **CP page reference:** [`docs/pages/cp/admin-profile-types-visitor.md`](../../pages/cp/admin-profile-types-visitor.md)
- **E2E catalogue:** [`docs/tests/e2e/cp-admin-profile-types-visitor.md`](../../tests/e2e/cp-admin-profile-types-visitor.md) (E2E-VPT-001 … 014)
- **Sibling (Other / partner side):** [`../admin-profile-types-other/`](../admin-profile-types-other/) — same backend, same `ProfileTypeForm.razor`, `IsVisitor=false`, and it **shows** the MobileAppRole picker this page hides.
- **Consumer app page:** [`docs/App/Page_007/`](../../App/Page_007/) — the visitor sign-up profile form. Its **نوع التسجيل = Visitor** tab reads `GET /app/account/profile-types?isVisitor=true` (D-190) and, per **C5 (D-371)**, **auto-locks** to the single seeded **"Normal" / "عادي"** row (no picker shown); the **Other** tab shows the partner picker. The rows this page manages are exactly what that picker offers.

## What this page is
The **admin-managed lookup table** of visitor-side profile types (the
audience "tiers"). Each row carries a bilingual name, a `PageColor`
swatch, a MobileAppRole (kept at `None` for visitor rows — see the Logic
doc) and an Active flag. The page pins every query to the **Visitor**
scope (`UserType=Visitor` + `IsVisitor=true`) so a row created here can
never leak into the Other / partner pool. Adding a row makes it available
to the app's sign-up profile picker immediately; the **Visitor** tab of
that picker still locks to the seeded "Normal" type (C5/D-371), so the
multi-row list matters most for the **Other** counterpart — both pages
write the **same** table.
