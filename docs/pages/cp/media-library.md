# Media Library — `/admin/media-library`

| | |
|--|--|
| **Route** | `/admin/media-library` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator (admins holding `MediaLibrary.*`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.MediaLibrary.View)]` (page) + per-action API policies (`MediaLibrary.Manage` for deactivate/restore) + `RequireApprovedAccount` |
| **Pattern** | D-357 unified media-asset pipeline — central management page over the single `Asset` table; `SimfDataGrid` (details-only) + deactivate/restore |
| **Status** | ✅ Real (D-357, 2026-06-10) |
| **Implements** | The owner ask "a CP page for centralized management of all media" — one grid over every image-bearing entity's uploaded/linked asset |
| **Backend endpoints** | `POST /account/api/admin/assets/list`, `GET /account/api/admin/assets/item/{id}`, `DELETE /account/api/admin/assets/item/{id}`, `POST /account/api/admin/assets/item/{id}/restore` |
| **Source file** | [`MediaLibraryList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/MediaLibraryList.razor) |
| **Tests** | [`docs/tests/e2e/cp-admin-media-library.md`](../../tests/e2e/cp-admin-media-library.md); guard: `tests/SIMF.Api.Tests/AssetPermissionRegistryTests.cs` |
| **Last reviewed** | 2026-06-10 |

---

## 1. Purpose

The unified media-asset pipeline (D-357) gives every image-bearing entity —
Speaker, Company/Contact, Media Partner, Sponsor, Archive edition, News — **one**
way to attach an image: upload a file or set an external link, stored as a row in
a single `Asset` table. This page is the cross-cutting governance view over that
table: an administrator sees **every** asset in the system in one grid (which
entity owns it, its category, a preview, its kind and source), and can **soft-delete
(deactivate)** an asset or **restore** a previously deactivated one. It is the only
page that reads the whole `Asset` table; the per-entity Add/Edit forms each manage
just their own `(category, owner)` asset through the same service.

## 2. Audience + permissions

- **Reach it:** any admin whose role grants `MediaLibrary.View` (or the
  Administrator wildcard `"*"`). Gated by
  `@attribute [RequirePermission(PermissionCatalog.MediaLibrary.View)]` and the
  `CpNavigation` item's `RequiredPermission = MediaLibrary.View`.
- **Manage (deactivate / restore):** the destructive actions in the details modal
  are wrapped in `<AuthorizedAction Permission="PermissionCatalog.MediaLibrary.Manage">`,
  and the API endpoints declare
  `Policies(nameof(AuthorizationPolicies.RequireApprovedAccount))` plus an
  imperative `MediaLibrary.Manage` claim check (`AssetAuth.Has`). A View-only admin
  sees the grid + details but no Deactivate/Restore button, and a direct call is 403.
- **Note on the per-category image endpoints (not this page):** the upload / link /
  admin-serve endpoints under `/admin/assets/{category}/{owner}/image` are gated by
  the **owning entity's** existing View/Edit permission via `AssetPermissionRegistry`
  (e.g. a SpeakerPhoto upload needs `Speakers.Edit`), so the media pipeline adds no
  new per-entity permission surface — only `MediaLibrary.*` for this management page.

## 3. UI affordances

### 3.1 Banner + surface
`SimfBanner` titled `Admin.MediaLibrary.Title` ("Media library" / "مكتبة الوسائط"),
wrapped in `simf-page-wide` / `simf-surface`. A `SimfAlert` toast renders above the
grid for success/error.

### 3.2 Grid (`SimfDataGrid<AdminAssetSummary>`)
Details-only grid (no Add/Edit/Delete toolbar — assets are created from the owning
entity's form, not here). Columns:

| Column | Source | Notes |
|--------|--------|-------|
| Category | `Category` | `AssetCategory` name (SpeakerPhoto, CompanyLogo, …) |
| Owner | `OwnerName` | resolved per-category (speaker name, sponsor name, "SIMF {Year}", news title…) |
| Preview | `SimfImageThumb` | renders the serve URL as a thumbnail, else the placeholder icon |
| Kind | `Kind` | Image / Video / Document |
| Source | `SourceType` | Uploaded file / External link |
| Active | `IsActive` | yes/no |

Empty list renders `SimfEmptyState` with `Admin.MediaLibrary.None`
("No media assets yet." / "لا توجد وسائط بعد.").

### 3.3 Details modal
Opening a row (`OnDetailsOne`) shows the preview, the owner/category/kind/source/URL,
the active flag, and one destructive action wrapped in `MediaLibrary.Manage`:
- **active asset →** "Deactivate" → `DELETE …/item/{id}` → success toast
  `Admin.MediaLibrary.Deactivated`.
- **inactive asset →** "Restore" → `POST …/item/{id}/restore` → success toast
  `Admin.MediaLibrary.Restored`, **unless** a live asset already owns that
  `(category, owner)` pair, in which case the API returns **409** and an error toast
  surfaces the conflict (the filtered unique index forbids two live rows).

## 4. Data flow

```
Admin opens page → MediaLibraryList.LoadAsync → simfAccount.postJson
   → BFF /account/api/admin/assets/list → API /api/v1/admin/assets/list
   → IAssetService.ListAsync(GridQuery) → SIMF_App.Assets (+ per-category owner-name resolve)
   → ApiResult<GridPage<AdminAssetSummary>> → grid

Deactivate → DELETE …/item/{id} → IAssetService.DeactivateAsync (audit AssetRemoved)
Restore    → POST …/item/{id}/restore → IAssetService.RestoreAsync (409 on conflict; audit AssetRestored)
```

| When | Method + path | Body | Response |
|------|---------------|------|----------|
| Open / query change | `POST /account/api/admin/assets/list` | `GridQuery` (filters: category, kind, sourceType, isActive) | `ApiResult<GridPage<AdminAssetSummary>>` |
| Open details | `GET /account/api/admin/assets/item/{id}` | — | `ApiResult<AdminAssetSummary>` |
| Deactivate | `DELETE /account/api/admin/assets/item/{id}` | — | `ApiResult<bool>` |
| Restore | `POST /account/api/admin/assets/item/{id}/restore` | — | `ApiResult<bool>` (409 on live-pair conflict) |

## 5. Validation + error handling

- **List** filters are optional (`category`, `kind`, `sourceType`, `isActive`);
  rows are ordered newest-first (`CreatedAt` desc).
- **Restore conflict:** `IAssetService.RestoreAsync` returns 409 when an active
  asset already exists for the same `(Category, OwnerId)`; the toast surfaces the
  bilingual conflict message and the row stays inactive.
- **Load failure** → `Admin.MediaLibrary.LoadFailed`
  ("Could not load media assets." / "تعذّر تحميل الوسائط.").
- **Authorisation:** page → `/not-permitted` without `MediaLibrary.View`;
  deactivate/restore → 403 without `MediaLibrary.Manage`.

## 6. Edge cases + known limitations

- **No create on this page.** Assets are created only from the owning entity's
  Add/Edit form (`SimfImageUpload`); this page governs (views/retires/restores) them.
- **Soft-delete only.** Deactivate sets `IsActive=false` (+ `DeletedAt`); the bytes
  /link stay on disk so a restore is loss-less. Restore is blocked if a newer live
  asset already owns the pair.
- **Owner name is resolved on read** (cross-DB-safe, no stored copy per D-157); a
  deleted owner row shows a blank/placeholder owner name.
- **Video kind is link-only** (enforced in `IAssetService`); the grid still lists
  any historical rows by kind.

## 7. i18n + RTL

All strings from `Strings.resx` / `Strings.ar.resx` via `IStringLocalizer<Strings>`
(`Admin.MediaLibrary.*`). The grid, headers and details modal mirror under
`<html dir="rtl" lang="ar">`; `SimfImageThumb` is direction-agnostic.

## 8. Related E2E test scenarios

| Scenario | File | Coverage |
|----------|------|----------|
| Golden path (list + deactivate) | [`cp-admin-media-library.md`](../../tests/e2e/cp-admin-media-library.md) | E2E-MLIB-001 |
| Empty / auth / manage-gate | same | E2E-MLIB-002/003/006 |
| Restore + restore-conflict (409) | same | E2E-MLIB-004/005 |
| Server-500 / RTL / preview / external-link | same | E2E-MLIB-007/008/009/010 |

## 9. Related docs

- Dev guide: [`SIMF-Media-Asset-The-One-Way.md`](../../dev/SIMF-Media-Asset-The-One-Way.md)
  — how to add an image to a new entity through this pipeline.
- Permissions: [`SIMF-Auth-Permissions-Dev-Guide.md`](../../manuals/SIMF-Auth-Permissions-Dev-Guide.md).
- Decisions log: **D-357** (unified media-asset pipeline), D-90 (out-of-row storage),
  D-157 (Data/Identity separation, bare-Guid cross refs) in
  [`DECISIONS_LOG.md`](../../decisions/DECISIONS_LOG.md).
- Source: `MediaLibraryList.razor`, `SimfImageUpload.razor`, `SimfImageThumb.razor`,
  `AssetEndpoints.cs`, `AssetService.cs`, `AssetPermissionRegistry.cs`.

## 10. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-10 | D-357 | Page created — central management over the unified `Asset` table (list + soft-delete/restore), gated by `MediaLibrary.View`/`.Manage`. |

---

_Last reviewed:_ 2026-06-10 by Claude (D-357).
