# Regions — `/admin/regions`

| | |
|--|--|
| **Route** | `/admin/regions` |
| **Audience** | Administrator (any role granted `Regions.*`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Regions.View)]` (page); API endpoints gated per-action by `Regions.View / .Create / .Edit / .Delete` + `RequireApprovedAccount`; mutations carry `RequireRateLimiting("auth")` |
| **Pattern** | D-547 reference lookup + SimfDataGrid list-page standard + CrudShell forms |
| **Status** | ✅ Real (D-547) |
| **Backend endpoints** | BFF passthroughs `/account/api/admin/regions/*` (`AccountEndpoints.cs`) → API: `POST /admin/regions/list`, `GET /admin/regions/{id}`, `POST /admin/regions`, `PUT /admin/regions/{id}`, `DELETE /admin/regions/{id}`. Public app picker: `GET /app/regions` (sign-in only) |
| **Source** | [`RegionsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RegionsList.razor), [`RegionAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RegionAddEdit.razor), [`RegionViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RegionViewDelete.razor), [`RegionEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/RegionEndpoints.cs), [`AdminRegionService`](../../../src/Backend/SIMF.Infrastructure/Regions/AdminRegionService.cs), [`PublicRegionService`](../../../src/Backend/SIMF.Infrastructure/Regions/PublicRegionService.cs), [`RegionContracts`](../../../src/Shared/SIMF.Contracts/Regions/RegionContracts.cs), [`Region`](../../../src/Backend/SIMF.Domain/Regions/Region.cs), [`RegionSeeder`](../../../src/Backend/SIMF.Infrastructure/Regions/RegionSeeder.cs) |
| **Backed by** | `dbo.Regions` table on `SimfAppDbContext` (D-547 additive migration `App/D547_AddRegionLookup`, permitted under the D-219 freeze-lift). |
| **Tests** | [`docs/tests/e2e/cp-admin-regions.md`](../../tests/e2e/cp-admin-regions.md) |
| **Last reviewed** | 2026-06-30 |

## 1. Purpose

The shared **bilingual Saudi-regions lookup** introduced under D-547 — the
reference table the visitor place-of-birth / region picker resolves against,
replacing the previously-hardcoded `SaudiRegions.cs` server static and the
`saudi_regions.dart` mobile mirror with a CP-managed, seeded table. Each row
carries a stable `Code` (the lookup key, e.g. `riyadh`), a required Arabic name,
an optional English name and a `SortOrder` controlling display order. Rows
soft-delete via `IsActive`, so deactivating one removes it from the public picker
without losing history.

The admin grid is the canonical SimfDataGrid list-page shape (server-paged,
sortable, per-column filterable, multiselect). The public picker the visitor app
calls is a separate, sign-in-only endpoint (`GET /app/regions`) backed by
`IPublicRegionService`; it is not part of this CP page and is not admin-gated.

On a fresh database the table is **seeded with the 13 official Saudi regions** by
`RegionSeeder`, which runs idempotently in **all** environments (keyed on `Code`,
so it never overwrites an admin's edits and never inserts duplicates).

## 4. UI

- `SimfBanner` titled "Regions" + a toolbar carrying the server-side Search field
  (text + "Search" button) and the `SimfDataGrid` action set.
- `SimfDataGrid` (owner-mandated list-page standard) with select-all + per-row
  checkbox, full pager, and quiet per-row icon actions:
  - **Add** (`OnAdd`) — opens `RegionAddEdit` in Create mode.
  - **Edit** (pencil, `OnEditOne`) — fetches the full detail via
    `GET /account/api/admin/regions/{id}` first, then opens `RegionAddEdit` in
    Edit mode.
  - **Details** (eye, `OnDetailsOne`) — opens `RegionViewDelete` read-only (no
    Deactivate button).
  - **Delete** (trash, `OnDeleteOne`) — opens `RegionViewDelete` with the red
    Deactivate button.
- Grid columns: Code [sortable, filterable], Name (Arabic) [sortable, filterable],
  Name (English) [filterable], Sort order, Active [sortable] (rendered as an
  on/off `SimfPill`). Empty / null text columns render "—".
- `SimfEmptyState` titled "No regions found" when the grid is empty.
- **No Excel import/export.** Unlike Organisations, Regions has no bulk-import or
  grid-export toolbar action — the seeded 13-row baseline plus manual CRUD is the
  whole surface.

## 4.5 Form fields

`RegionAddEdit` (`CrudAddEditFormBase<AdminRegionDetail>`). MaxLength values are
the UI caps; the server-side `ValidateAndNormalise` is the source of truth and
its limits match these.

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Code | yes | 16 | 1–16 chars; trimmed; unique (409 on clash); stable lookup key |
| Name (Arabic) | yes | 256 | 1–256 chars; trimmed |
| Name (English) | no | 256 | ≤ 256 chars; null when blank |
| Sort order | yes | int | display order, ascending |
| Active | (Edit only) | bool | shown only in Edit mode |

The form blocks an obviously-empty submit (blank Arabic name → inline bilingual
`Admin.Regions.Required` alert, no POST); all other validation is enforced
server-side. The optional English name is sent as `null` rather than an empty
string.

> **Dual-DTO gotcha (mirrors Organisations):** `CreateRegionRequest` binds from
> the request body; `UpdateRegionRequest` carries **no Id** — the PUT reads the
> id from the route (`Route<Guid>("id")`). `UpdateRegionRequest` additionally
> carries `IsActive` (default `true`).

## 5. Data flow + endpoints

Canonical CP→BFF→API shape. The page never calls the API directly; it calls the
BFF passthroughs under `/account/api/admin/regions/*` via the `simfAccount.*` JS
interop (`postJson` / `getJson` / `putJson` / `deleteJson`), which forward to the
FastEndpoints in `RegionEndpoints.cs`.

| Action | BFF call | API endpoint | Method | Permission |
|--------|----------|--------------|--------|------------|
| List (paged grid) | `POST …/regions/list` | `ListRegionsEndpoint` | POST | `Regions.View` |
| Get one (detail) | `GET …/regions/{id}` | `GetRegionEndpoint` | GET | `Regions.View` |
| Create | `POST …/regions` | `CreateRegionEndpoint` | POST | `Regions.Create` |
| Update | `PUT …/regions/{id}` | `UpdateRegionEndpoint` | PUT | `Regions.Edit` |
| Deactivate (soft-delete) | `DELETE …/regions/{id}` | `DeactivateRegionEndpoint` | DELETE | `Regions.Delete` |
| Public picker (app) | — (app calls API directly) | `RegionListEndpoint` | GET `/app/regions` | sign-in only |

`ListAsync` takes a `GridQuery`; Search runs a `LIKE` across Code, Arabic name and
English name; per-column filters (`code`, `name` ⇒ NameArabic, `nameen`,
`isactive`) accumulate; sortable on `code`, `name` (⇒ NameArabic), `isactive`;
the default sort (no explicit `Sort`) is `SortOrder` ascending then `NameArabic`.
All writes stamp an audit entry (`region.created` / `region.updated` /
`region.deactivated`) via the audit log.

The public app picker `GET /app/regions` returns
`ApiResult<IReadOnlyList<RegionPickerItem>>` — `(Code, Name, NameArabic)` for the
**active** regions only, ordered by `SortOrder` then `NameArabic`. It is
sign-in-only (rate-limit `"auth"`), not admin-gated, not approval-gated and not
`AllowAnonymous`.

## 6. Validation + error handling

- **Server-side `AdminRegionService.ValidateAndNormalise`:** trims and
  length-gates Code (1–16, required) and the Arabic name (1–256, required); the
  optional English name is length-gated (≤ 256) and stored `null` when blank.
- **Invalid field:** 400 `REGION_INVALID` (`ErrorCodes.RegionInvalid`), bilingual
  message naming the offending field/limit.
- **Duplicate Code:** 409 `REGION_INVALID` (bilingual, surfaces the clashing code).
  Checked on create and on update when the Code changes.
- **Not found:** 404 `REGION_NOT_FOUND` (`ErrorCodes.RegionNotFound`).
- **Deactivate** is idempotent — an already-inactive row returns without a second
  audit write; a missing id returns 404.

## 7. Edge cases + known limitations

- **No Excel layer.** Regions exposes neither a bulk Excel import nor a generic
  grid export; the seeded baseline plus manual CRUD is the whole surface.
- **Code is the stable key.** The lookup resolves on `Code` (not the display
  name), so editing the Arabic/English name does not break references; changing a
  `Code` that is already in use is a curation decision left to the admin.
- **Deactivate is unconditional.** A row referenced by a visitor profile can still
  be deactivated; it simply drops out of the public picker.
- **Public picker is out of scope here.** `GET /app/regions` is sign-in-only (not
  admin-gated, not `AllowAnonymous`) and lives on `IPublicRegionService`.
- **Mobile picker swap deferred.** The Flutter `saudi_regions.dart` static and the
  CP `WalkInRegistrationForm.razor` birth-region `<select>` still read the legacy
  `SaudiRegions.cs` static; wiring them to `GET /app/regions` is deferred to
  Phase 2 (account consolidation) per D-547. `SaudiRegions.cs` therefore stays in
  the tree as the seed source until that swap lands.

## 8. i18n + RTL

`Admin.Regions.*` resx keys (title, search, columns, field labels, toasts,
confirm copy) plus shared `Grid.*` keys for the data-grid chrome. EN ↔ AR parity
is maintained; the page mirrors fully under `<html dir="rtl">`. (The exact resx
phrasing is owned by the resource files and is described, not quoted, here.)

## 10. Use cases

- Create / edit / deactivate a region lookup row; resolve the visitor
  place-of-birth / region picker against the active set _(formal UCS entries
  tracked under the D-547 lookup workstream)_.

## 11. E2E

See [`docs/tests/e2e/cp-admin-regions.md`](../../tests/e2e/cp-admin-regions.md):
E2E-REGION-001 golden round-trip, 002 empty/no-match, 003 server-side
search/filter, 004 seeded 13-region baseline, 005 page auth gate, 006 action
gates, 007 client validation, 008 server validation (over length), 009
duplicate-Code conflict, 010 delete-confirm cancelled, 011 not-found, 012 list
500, 013 RTL, 014 column sort, 015 app-picker parity, 016 SimfConfirm delete gate.

## 12. Related docs

- E2E catalogue: `docs/tests/e2e/cp-admin-regions.md`.
- Reference template: `docs/pages/cp/admin-organisations.md` (the lookup-CRUD
  pattern Regions mirrors, minus the Excel import/export layer).
- Permission catalogue: `PermissionCatalog.Regions` (View / Create / Edit /
  Delete); guide `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`.
- Decisions: D-547 (Region lookup), D-219 (freeze-lift permitting the additive
  table), D-157 / D-246 (Data ↔ Identity DB separation — `Regions` lives on
  `SimfAppDbContext`).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-30 | D-547 | Original — Region lookup entity + additive migration (`App/D547_AddRegionLookup`) + bilingual admin CRUD on the SimfDataGrid list-page standard, the sign-in-only public picker `GET /app/regions`, and the idempotent all-environment seed of the 13 official Saudi regions. Replaces the hardcoded `SaudiRegions.cs` / `saudi_regions.dart` as the source of truth (mobile picker swap deferred to Phase 2). |

_Last reviewed:_ 2026-06-30 by Claude (D-547 — reference doc authored).
