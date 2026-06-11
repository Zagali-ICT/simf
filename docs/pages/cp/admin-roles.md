# Roles & permissions — `/admin/roles`

| | |
|--|--|
| **Route** | `/admin/roles` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator |
| **Auth** | `[Authorize(Roles = "Administrator")]` + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (mutations) |
| **Pattern** | D-117 + D-132 canonical CRUD (mirror of `InterestsList.razor`) |
| **Status** | ✅ Real (D-134 Sprint A) |
| **Implements UC(s)** | UC-ROL-LIST, UC-ROL-CREATE, UC-ROL-RENAME, UC-ROL-DELETE _(pending UCS entry)_ |
| **Backend endpoints** | `POST /account/api/admin/roles/list`, `GET /admin/roles/{id}`, `POST /admin/roles`, `PUT /admin/roles/{id}`, `DELETE /admin/roles/{id}` |
| **Source files** | [`RolesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RolesList.razor) + [`RoleForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RoleForm.razor) |
| **Backend service** | [`AdminRoleService`](../../../src/Backend/SIMF.Infrastructure/Identity/AdminRoleService.cs) (over the existing `SimfRole` + `Permission` + `RolePermission` entities — **no schema change**) |
| **Tests** | [`docs/tests/e2e/cp-admin-roles.md`](../../tests/e2e/cp-admin-roles.md) |
| **Last reviewed** | 2026-05-29 |

---

## 1. Purpose

Roles are the unit of authorisation in SIMF (SIMF-RPM-001). An
administrator uses this page to **create custom roles**, **rename
custom roles**, and **delete custom roles** that are no longer needed.
Baseline roles (`SimfRole.IsBaseline = true` — seeded by
`IdentitySeeder`) are read-only here: they ship with the system,
guarantee the canonical permission set is always present, and cannot
be renamed or deleted from the UI.

The page is **Sprint A's MVP** of the eventual full Roles module — the
per-permission grant editor and the assign-to-user flows ship in a
follow-up commit so this slice stays minimum-viable
(CLAUDE.md §17). The Details modal already surfaces the per-role
user count + permission count so an operator can size the impact of a
delete without leaving the page.

## 2. Audience + permissions

- **Who can reach it:** Administrator (only role with the
  `Administrator` CP role).
- **Authorisation gates:**
  - Razor: `@attribute [Authorize(Roles = "Administrator")]` on
    `RolesList.razor`.
  - BFF: `/account/api/admin/roles/*` routes require auth via the
    `account` group's `RequireAuthorization()`.
  - API: every endpoint uses `Policies(AdministratorOnly,
    RequireApprovedAccount)`. Writes also use
    `RequireRateLimiting("auth")`.
- **What an unauthenticated user sees:** redirect to `/login` via the
  cookie challenge; an authenticated non-admin sees the standard
  `/not-permitted` fallback (403).

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (Administrator baseline row visible) | `docs/screenshots/d134-roles-canonical.png` | 2026-05-29 |
| Add modal | `docs/screenshots/d134-roles-add-modal.png` | 2026-05-29 |
| Edit modal — custom role | _to capture_ | — |
| Edit modal — baseline notice | _to capture_ | — |
| Details modal | _to capture_ | — |
| Delete refused (RoleInUse 409) | _to capture_ | — |
| RTL | _to capture_ | — |

## 4. UI affordances

### 4.1 Banner

`<SimfBanner Title="@L[\"Admin.Roles.Title\"]" />` → EN
"Roles & permissions" / AR "الأدوار والصلاحيات".

### 4.2 Toolbar

| Button | Wired callback | Endpoint | Notes |
|--------|----------------|----------|-------|
| Select all | built-in | — | Multiselect=true mandatory per D-132 |
| Add role | `OnAddAsync` | opens modal → `RoleForm` (Initial=null) → `POST /admin/roles` | always creates custom (IsBaseline=false) |
| Edit | `OnEditAsync` | opens modal → if `IsBaseline` shows read-only notice; else `RoleForm` (Initial=row) → `PUT /admin/roles/{id}` | |
| Details | `OnDetailsAsync` | client-side `simf-dl` modal | reads the row directly — no extra fetch |
| Delete | `OnDeleteAsync` | `DELETE /admin/roles/{id}` | refused for IsBaseline + RoleInUse |

Bulk-delete (`OnDeleteSelected`) is intentionally not wired — bulk-delete
roles is a destructive operation whose UX would be more dangerous than
useful for ≤ 10-row tables.

**D-356 / D-353 (Uniform CRUD).** The toolbar also exposes **Export** and
**Import** (.xlsx), wired through the reusable `CrudGridExcel`
(`Resource="roles"`) to `POST /account/api/admin/roles/export` and
`/import`; export posts `AdminGridExportRequest { Ids, Query }` (Query only
when no rows are selected, otherwise the selected ids), and import uploads
the chosen workbook (input id `roles-import-input`) and shows an
"N created, N updated, N skipped" result modal with a per-row error list.
The page also renders the **D-353 Page⇄Popup presentation toggle**
(`CrudPresentationToggle`, `PageKey="roles"` → `localStorage`
`simf.cp.prefs.roles`); Add/Edit/View/Delete are framed by `CrudShell` as a
popup or a full page per that preference, and the delete is gated by a
`SimfConfirm` dialog inside `RolesViewDelete` (no longer a one-click row
delete).

### 4.3 Grid columns

| Column | Source | Sortable | Filterable |
|--------|--------|----------|------------|
| Name | `AdminRoleSummary.Name` | yes | yes |
| Type | `.IsBaseline` (pill: Built-in / Custom) | yes | no |
| Users | `.UserCount` | no | no |
| Permissions | `.PermissionCount` | no | no |

### 4.4 Form fields (Add + Edit modal)

| Field | Type | Required | MaxLength | Validation |
|-------|------|----------|-----------|------------|
| Role name | text | yes | 64 | 1–64 chars; unique across all roles |

### 4.5 Pager / i18n / accessibility

Identical to the canonical pattern — see [`admin-interests.md`](admin-interests.md)
§4.4, §8, §9.

## 5. Data flow

```
Admin clicks +Add role
  → _addOpen = true
  → <SimfModal> renders <RoleForm Initial=null>
  → admin types name + clicks Create role
  → JS interop: simfAccount.postJson("/account/api/admin/roles", { Name })
  → CP BFF reads access_token from cookie, forwards via SimfAdminClient
  → API: POST /api/v1/admin/roles (Administrator + Approved + RateLimit)
  → AdminRoleService.CreateAsync:
      - trims + length-gates name (1..64)
      - RoleManager.RoleExistsAsync to surface 409 RoleNameDuplicate
      - RoleManager.CreateAsync (handles normalised-name + concurrency stamp)
      - audit Role.Created
  → ApiResult<AdminRoleSummary>
  → modal closes; grid reloads; toast Admin.Roles.Created
```

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| Page init | `POST /account/api/admin/roles/list` | `GridQuery` | `ApiResult<GridPage<AdminRoleSummary>>` |
| Add submit | `POST /account/api/admin/roles` | `AdminCreateRoleRequest { Name }` | `ApiResult<AdminRoleSummary>` |
| Edit submit | `PUT /account/api/admin/roles/{id}` | `AdminUpdateRoleRequest { Name }` | `ApiResult<AdminRoleSummary>` |
| Delete | `DELETE /account/api/admin/roles/{id}` | — | `ApiResult<bool>` |

## 6. Validation + error handling

- **Client (`RoleForm.HandleSubmitAsync`):** trims; length ∈ [1..64];
  surfaces `Admin.Roles.Field.NameInvalid`.
- **Server (`AdminRoleService.CreateAsync` / `.UpdateAsync`):**
  - Empty / >64 → `400 RoleInvalid` (bilingual).
  - Duplicate name → `409 RoleNameDuplicate` (bilingual).
  - Update of baseline → `409 RoleIsBaseline`.
  - Delete of baseline → `409 RoleIsBaseline`.
  - Delete while any user holds → `409 RoleInUse` (count surfaced in
    message).
- **Toast strategy:** success → `Admin.Roles.{Created,Updated,Deleted}`;
  error → server envelope via `MessageForCurrentCulture()`, fallback
  `Admin.Roles.Fallback`.

## 7. Edge cases + known limitations

- **Baseline-role rename attempt** — `Edit` modal renders a read-only
  notice + Close button (no form). Server still guards in case anyone
  hand-crafts a PUT.
- **Baseline-role delete attempt** — server returns 409 `RoleIsBaseline`
  with a bilingual explanation; toast surfaces verbatim.
- **Delete while users hold the role** — server returns 409 `RoleInUse`
  with the holder count interpolated into the bilingual message. The
  CP unassign flow (per-user role editor) is a follow-up.
- **Identity normalised-name** — RoleManager handles the
  `NormalizedName` invariant + concurrency stamp; the page never sets
  these directly.
- **No bulk operations** — out of scope for the MVP.
- **Permission grant editor + assign-to-user** — deferred to a follow-up.
  The Details modal explicitly mentions the deferral in the body copy
  so operators don't go hunting for missing controls.

## 8. i18n + RTL

- EN + AR keys live under `Admin.Roles.*` (45 keys per locale, added in
  this commit). EN ↔ AR parity preserved per D-132 audit gate.
- Banner title flips EN "Roles & permissions" → AR "الأدوار والصلاحيات".
- "Built-in" pill uses the `admin` variant (brand accent); "Custom"
  uses the default neutral pill.
- RTL: nav rail mirrors, grid headers flip, modal renders RTL when
  Arabic is active.

## 9. Accessibility

Identical to the canonical pattern — see [`admin-interests.md`](admin-interests.md) §9.
The "Built-in" / "Custom" pill text is announced by screen readers
(the column header is "Type").

## 10. Use cases (UCS-001 § UC-ROL-*)

| UC ID | Title | Notes |
|-------|-------|-------|
| UC-ROL-LIST | List + filter + sort roles | _(pending UCS detail entry)_ |
| UC-ROL-CREATE | Create a custom role | _(pending)_ |
| UC-ROL-RENAME | Rename a custom role | _(pending)_ |
| UC-ROL-DELETE | Delete a custom role (with the IsBaseline + InUse guards) | _(pending)_ |

## 11. E2E test scenarios

See [`docs/tests/e2e/cp-admin-roles.md`](../../tests/e2e/cp-admin-roles.md):

- E2E-ROL-001 — Create custom role (golden)
- E2E-ROL-002 — Duplicate name → 409 RoleNameDuplicate
- E2E-ROL-003 — Rename custom role
- E2E-ROL-004 — Rename baseline blocked (modal shows notice)
- E2E-ROL-005 — Delete custom unused role
- E2E-ROL-006 — Delete baseline blocked (409 RoleIsBaseline)
- E2E-ROL-007 — Delete in-use role blocked (409 RoleInUse, count in toast)
- E2E-ROL-008 — Auth gate (non-admin → /not-permitted)
- E2E-ROL-009 — RTL

## 12. Related docs

- Admin Manual: `Admin-Manual.md § 4.4 Roles & permissions`
- D-134 plan: [`SIMF-D134-Module-Build-Plan.md`](../../SIMF-D134-Module-Build-Plan.md) §3.1.1
- Authority spec: SIMF-RPM-001 §8 (the page-and-action model the
  permission editor will surface in the follow-up commit)
- Pattern: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) (D-117 + D-132)
- Decisions: D-134 (plan), and this commit (Sprint A).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-29 | D-134 Sprint A | Original implementation: list + create + rename + delete with IsBaseline + RoleInUse guards. Permission editor + user assignment deferred. |
| 2026-06-10 | D-356 / D-353 | Uniform CRUD: added Excel Export + Import (.xlsx via `CrudGridExcel`, `Resource="roles"`) and the Page⇄Popup presentation toggle (`PageKey="roles"`); Add/Edit/View/Delete now framed by `CrudShell`, hard delete gated by `SimfConfirm`. E2E catalogue extended with E2E-ROL-019..024. |

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 / D-353 — Excel export+import + presentation toggle).
