# CP page — العارضون · Exhibitors (`/admin/exhibitors`)

Per-page documentation folder for a **Control Panel** config page. Everything
about this admin page lives here. This set mirrors the Flutter per-page format
(`docs/App/Page_NNN/`) for the Control Panel surface.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

The page is the **exhibitor directory** — the CP-only admin CRUD over the
**exhibitor companies** that back exhibition booths. Per D-199 #3 / D-202
Track-2, an exhibitor is a **CP-created Company + login accounts**; in-app
exhibitor self-signup was permanently descoped. Each exhibitor is a bilingual
record (English + Arabic name) with optional contact email / phone / website and
an optional link to the shared **Contact** directory. Beyond plain CRUD the page
hosts a **per-exhibitor account-provisioning** sub-flow that creates a
least-privilege **Visitor** login tagged to the exhibitor.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [admin-exhibitors_Function.md](admin-exhibitors_Function.md) | What the admin does — grid, CRUD, account provisioning, Excel, toggle, navigation, acceptance |
| Logic | [admin-exhibitors_Logic.md](admin-exhibitors_Logic.md) | Business rules — validation, audit, AccountCount derivation, cross-DB rule, edge cases, the booth→exhibitor link |
| API | [admin-exhibitors_API.md](admin-exhibitors_API.md) | The backend endpoints + DTOs that serve this page (authoritative contract) |
| Design | [admin-exhibitors_Design.md](admin-exhibitors_Design.md) | Blazor CP layout — `SimfDataGrid` + `CrudShell` framing, account modal, fields, states, RTL |

## Identity
| | |
|---|---|
| Surface | **Control Panel** (Blazor Server) |
| Route | `/admin/exhibitors` |
| Layout | `CpShellLayout` |
| Titles | Banner / nav: AR **العارضون** · EN **Exhibitors** (`Admin.Exhibitors.Title`, `Module.Exhibitors`) |
| Audience | Administrator |
| Page permission | `@attribute [RequirePermission(PermissionCatalog.Exhibitors.View)]` |
| Nav item | `new("Module.Exhibitors", "/admin/exhibitors", RequiredPermission: PermissionCatalog.Exhibitors.View, Icon: "briefcase")` under `Nav.Exhibition` |
| Pattern | D-202 Track-2 CP CRUD + per-exhibitor account provisioning · D-281 Contact link · D-353 `CrudShell` framing + Page↔Popup toggle · D-356 Excel export/import |
| Status | ✅ Real (D-202; D-281 Contact link; D-353 CrudShell + toggle; D-356 Excel) |
| Backed by | `dbo.Exhibitors` + `dbo.ExhibitorMemberships` (`SimfAppDbContext`; D-199/D-202 migration `D202_CompaniesAndProvisioning`; renamed in `D274_AuditFoldAndExhibitorRename`) |

## Source files (verified this session)
| File | Role |
|------|------|
| [`ExhibitorsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ExhibitorsList.razor) | The page — `SimfDataGrid` + `CrudShell` host + account-provisioning `SimfModal` + `CrudGridExcel` |
| [`ExhibitorsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ExhibitorsAddEdit.razor) | Reusable Add/Edit form (`CrudAddEditFormBase<AdminExhibitorDetail>`) |
| [`ExhibitorsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ExhibitorsViewDelete.razor) | Reusable View/Delete form (`CrudViewDeleteFormBase<AdminExhibitorDetail>`) |
| [`ExhibitorEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Exhibitors/ExhibitorEndpoints.cs) | **The confirmed backing endpoint file** — CRUD + accounts (FastEndpoints) |
| [`ExhibitorsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/ExhibitorsExcelEndpoints.cs) | D-356 Excel export/import endpoints |
| [`AdminExhibitorService.cs`](../../../src/Backend/SIMF.Infrastructure/Exhibitors/AdminExhibitorService.cs) | The service — list/get/create/update/deactivate/accounts/provision |
| [`ExhibitorContracts.cs`](../../../src/Shared/SIMF.Contracts/Exhibitors/ExhibitorContracts.cs) | The DTOs (`AdminExhibitorSummary` / `AdminExhibitorDetail` / requests / `ExhibitorAccountSummary`) |
| [`Exhibitor.cs`](../../../src/Backend/SIMF.Domain/Exhibitors/Exhibitor.cs) | The domain entity (`BaseAuditEntity`) |
| [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs) | `PermissionCatalog.Exhibitors.*` (View/Create/Edit/Delete/Export/Import) |
| [`CpNavigation.cs`](../../../src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs) | Nav registration |
| [`AccountEndpoints.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs) | BFF passthroughs `/account/api/admin/exhibitors/*` → API |

## Sources of truth (read first)
`docs/pages/cp/admin-exhibitors.md` (the existing per-page reference) ·
`docs/tests/e2e/cp-admin-exhibitors.md` (the E2E catalogue, E2E-EXH-001…023) ·
the source files above (the **code is authoritative**) ·
`DECISIONS_LOG` D-199 (#3 exhibitor module) + **D-202** (CP CRUD + account
provisioning) + **D-222** (booth→exhibitor FK) + **D-281** (Contact link) +
**D-353** (CrudShell + toggle) + **D-356** (Excel) · cross-DB rule **D-157**.

## App linkage — booth → exhibitor (D-222)
The mobile **Venue map (Page 015)** and the **Booth detail sheet** attribute a
booth to its exhibitor. `Booth.ExhibitorId` is a **real FK** to `Exhibitor.Id`
(same App DB, D-222) and is the source of truth for the booth's exhibitor; the
public booth projection fills `ExhibitorName` / `ExhibitorNameArabic` from the
linked exhibitor when set (the free-text `Booth.ExhibitorName*` columns are a
legacy fallback retained for the wire contract, no longer settable from the admin
write surface). On the app, the venue-map booth **info card** surfaces the
exhibitor **name** + **sector** (`docs/App/Page_015/`). So this CP page is where
the company that the app shows behind a booth is created and maintained. See
[admin-exhibitors_Logic.md](admin-exhibitors_Logic.md) §L-8 for the link detail.

## Related docs
- App: [`docs/App/Page_015/`](../../App/Page_015/README.md) (Venue map — the
  booth info card shows exhibitor name + sector).
- Existing CP reference: [`docs/pages/cp/admin-exhibitors.md`](../../pages/cp/admin-exhibitors.md).
- E2E catalogue: [`docs/tests/e2e/cp-admin-exhibitors.md`](../../tests/e2e/cp-admin-exhibitors.md).
- Sibling Exhibition modules: `docs/pages/cp/admin-booths.md`,
  `docs/pages/cp/admin-sponsors.md`; shared `docs/pages/cp/admin-contacts.md`.
</content>
</invoke>
