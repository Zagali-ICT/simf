# CP — كتل المحتوى · Content blocks (CMS) — `/admin/content-blocks`

Per-page documentation folder for the Control Panel **Content blocks** admin
page. Everything about this CP config page lives here. The CODE is the source
of truth; every statement traces to source read for this set.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [admin-content-blocks_Function.md](admin-content-blocks_Function.md) | What the admin does — grid, New block, edit (key locked), details, delete-with-confirm, Excel export/import, presentation toggle, filter/sort |
| Logic | [admin-content-blocks_Logic.md](admin-content-blocks_Logic.md) | The keyed-upsert model, key normalisation, soft-delete/idempotency, the public read contract the app consumes, `terms`/`about` block keys, `If-Modified-Since`/304, D-377 core-content seed, the `cyber.*` wire contract |
| API | [admin-content-blocks_API.md](admin-content-blocks_API.md) | The admin CMS endpoints + DTOs + the public read endpoint (authoritative contract) |
| Design | [admin-content-blocks_Design.md](admin-content-blocks_Design.md) | CP page design — `SimfDataGrid` + `CrudShell` framing, columns, the two reusable forms, RTL, states |

## Identity
| | |
|---|---|
| Route | `/admin/content-blocks` (`ContentBlocksList.razor`, `@page "/admin/content-blocks"`) |
| Layout | `CpShellLayout` |
| Permission (page) | `@attribute [RequirePermission(PermissionCatalog.ContentBlocks.View)]` |
| Nav item | `CpNavigation` `"Module.ContentBlocks"` → `/admin/content-blocks`, `RequiredPermission: PermissionCatalog.ContentBlocks.View`, `Icon: "layout"` |
| Audience | Administrator (per-action gated — see API) |
| Title (resx) | `Admin.ContentBlocks.Title` — EN **"Content blocks"** · AR **"كتل المحتوى"** |
| Section | Dynamic CMS (D-173, gap doc G8, PDF §1, §2.1) |
| Nature | **Keyed bilingual content blocks** — runtime-editable EN/AR text the public Website + Flutter app read by stable `Key` slug |
| Status | API **BUILT** (D-173); CP page **BUILT** — `SimfDataGrid` migration (D-255), `CrudShell` framing + presentation toggle (D-353), Excel export/import (D-356) |

## Sources of truth (read first)
The pages + endpoints below were read this session and are the binding source:
- CP page: `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContentBlocksList.razor`
- CP forms: `ContentBlockAddEdit.razor`, `ContentBlockViewDelete.razor` (same folder)
- Admin API: `src/Backend/SIMF.Api/Endpoints/Admin/CmsEndpoints.cs`
- Public read API: `src/Backend/SIMF.Api/Endpoints/Public/PublicCmsEndpoints.cs`
- Service: `src/Backend/SIMF.Infrastructure/Cms/AdminCmsService.cs`
- Contracts: `src/Shared/SIMF.Contracts/Admin/Cms.cs` (admin) · `src/Shared/SIMF.Contracts/Cms/ContentBlocks.cs` (public)
- Entity: `src/Backend/SIMF.Domain/Cms/ContentBlock.cs`
- Permissions: `src/Shared/SIMF.Common/PermissionCatalog.cs` (`ContentBlocks.*`)
- Seed: `src/Backend/SIMF.Infrastructure/Identity/IdentitySeeder.cs` (`EnsureCoreAppContentAsync` — `terms` + `about`, D-377)
- Decisions: `docs/decisions/DECISIONS_LOG.md` D-173, D-255, D-353, D-356, D-377

## How the app reads what this page writes
This admin page **writes** the keyed blocks; the Flutter app **reads** them
anonymously through `GET /api/v1/app/content/{key}`:

- **`terms`** → app **Page 009** (الشروط والأحكام · Terms) — `GET /api/v1/app/content/terms`.
  See [`docs/App/Page_009/`](../../App/Page_009/README.md).
- **`about`** → app static About content — `GET /api/v1/app/content/about`.
  Home is [`docs/App/Page_013/`](../../App/Page_013/README.md); the `about`
  block is seeded alongside `terms` by D-377 (`EnsureCoreAppContentAsync`).
- **`cyber.*`** → app cybersecurity-policy screen (seeded by `IdentitySeeder`;
  a Flutter **wire contract** — renaming/deactivating breaks the app).

Inactive blocks are hidden from the public read (404). Renaming a `Key` is a
**wire-breaking change** because the client codes against the slug.

## Cross-links
- Existing CP reference: [`docs/pages/cp/admin-content-blocks.md`](../../pages/cp/admin-content-blocks.md)
- CP E2E catalogue: [`docs/tests/e2e/cp-admin-content-blocks.md`](../../tests/e2e/cp-admin-content-blocks.md) (E2E-CNT-001…020)
- Related app pages: [Page_009 (terms)](../../App/Page_009/README.md) · [Page_013 (home/about)](../../App/Page_013/README.md)
- Permission catalogue: `docs/SIMF-Permission-Catalogue.md` (`ContentBlocks.*`)
- Page index: `docs/pages/PAGE-INDEX.md`
