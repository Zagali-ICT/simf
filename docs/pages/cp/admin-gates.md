# Gates — `/admin/gates`

| | |
|--|--|
| **Route** | `/admin/gates` |
| **Audience** | Administrator (any role granted `Gates.Manage`; the wildcard Administrator) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Gates.Manage)]` (CP) + every API endpoint `Policies(PolicyFor(Gates.Manage), RequireApprovedAccount)`; create / update / delete are rate-limited (`"auth"` limiter) |
| **Pattern** | D-148 Gate Module CRUD list (mirrors `HallsList`). Centralised CrudShell framing + presentation toggle (D-353); grid Excel export + import (D-356). |
| **Status** | ✅ Real (D-148) |
| **Backend endpoints** | BFF `/account/api/admin/gates/*` → API: `POST /admin/gates/list`, `GET /admin/gates/{id}`, `POST /admin/gates`, `PUT /admin/gates/{id}`, `DELETE /admin/gates/{id}`, plus `POST /admin/gates/export` + `POST /admin/gates/import` (D-356, registered via `MapGridExcel(group, "gates")`). BUG-018: the form primes its pickers from the gate module's own `Gates.Manage`-gated lookups — `GET /admin/gates/form-options` (profile types + halls) and `POST /admin/gates/operator-candidates/list` (searchable, paged) — not the admins / profile-types / halls admin lists. |
| **Source** | [`GatesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GatesList.razor), [`GatesAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GatesAddEdit.razor), [`GatesViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GatesViewDelete.razor), [`GateEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/GateEndpoints.cs), [`GatesExcelEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/GatesExcelEndpoints.cs), [`AdminGateService`](../../../src/Backend/SIMF.Infrastructure/AccessControl/AdminGateService.cs) |
| **Backed by** | `dbo.Gates` (+ `GateProfileTypeAllow` allow-list and `GateAssignment` operator rows) on `SimfAppDbContext`. |
| **Tests** | [`docs/tests/e2e/cp-admin-gates.md`](../../tests/e2e/cp-admin-gates.md) |
| **Last reviewed** | 2026-07-26 |

## 1. Purpose

Entry-gate configuration for the venue access-control system (SIMF-FDS-003
§5.6 / SIMF-API-GATES-001 §6). Each gate has a **Code** (stable identifier,
e.g. "G-MAIN-1"), bilingual name + optional description, a **direction policy**
(In / Out / Both), an optional **allowed-profile-type** allow-list (empty = all
types admitted), and a set of **assigned operators** (the staff accounts who may scan at
that gate **from the mobile app** — BUG-018; the CP operator console is a
fallback desk). The CRUD list is the source of truth for
the gate definitions the operator console (`/admin/gates/operator`) and the
operations dashboard (`/admin/gates/dashboard`) read from.

This page is the admin **configuration** surface only — live scan recording,
the currently-inside report and the scan export live behind the operator and
dashboard pages (separate routes), served by the same `AdminGateService`.

## 4. UI

- `SimfBanner` (title `Admin.Gates.Title`) + the canonical `SimfDataGrid` with
  Select-all, per-row checkbox, and quiet per-row icon actions (Edit / Details /
  Deactivate). `Multiselect="true"`.
- Grid columns: **Code** (sortable, filterable), **Name** (sortable,
  filterable), **Name (Arabic)** (sortable, filterable), **Direction**
  (`DirectionMode`, sortable), **Allowed types** (`AllowedProfileTypeCount`;
  `0` renders `Admin.Gates.AllowAll` = "All"), **Operators**
  (`AssignedOperatorCount`, the active-assignment count), and **Status** —
  a `SimfPill` (`on` "Active" / `off` "Inactive").
- The Direction / Allowed types / Operators / Status columns are **not**
  filterable (only Code / Name / Name (Arabic) are).
- Add / Edit host `GatesAddEdit` (the **Active** checkbox renders in Edit only);
  Details / Deactivate host `GatesViewDelete` (read-only description list; in
  Delete mode a red Deactivate button gated by a `SimfConfirm` dialog).
- All four forms are framed by **`CrudShell`** — as a dialog (default) or a
  full page, per the toolbar toggle (D-353). In full-page mode the grid +
  banner hide (`GridHidden`) and the form takes over the content area.
- Empty grid renders `SimfEmptyState` (title `Admin.Gates.None`).
- **Excel export + import (D-356):** the grid wires `OnExport` + `OnImport` and
  renders `<CrudGridExcel Resource="gates" />`. **Export** posts
  `AdminGridExportRequest { Ids, Query }` to `/account/api/admin/gates/export`
  (selected row ids, else the whole filtered grid) and downloads
  `simf-gates-{timestamp}.xlsx` with sheet **"Gates"** and header row
  `Code | Name | NameArabic | DirectionMode | AllowedProfileTypeCount |
  AssignedOperatorCount | IsActive`. **Import** (insert-only) uploads an `.xlsx`
  to `/account/api/admin/gates/import` and shows a result modal
  ("N created, N updated, N skipped" + per-row errors); a duplicate code or an
  invalid field is a per-row error, not a batch abort.
- **Page ↔ Popup presentation toggle (D-353):** `<CrudPresentationToggle
  PageKey="gates" @bind-Value="_presentation" />` in the grid `CustomToolbar`;
  `OnInitializedAsync` seeds it from `Prefs.GetPresentationAsync("gates")` and
  the choice persists in `localStorage` under `simf.cp.prefs.gates`.

## 4.5 Form fields

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Code | yes | 16 | 2–16 chars; trimmed + upper-cased server-side; unique (case-insensitive) |
| Name (English) | yes | 128 | 1–128 chars |
| Name (Arabic) | yes | 128 | 1–128 chars |
| Description (English) | no | 1024 | optional |
| Description (Arabic) | no | 1024 | optional |
| Direction policy | yes | n/a | In / Out / Both (`DirectionMode`; defaults to Both) |
| Allowed profile types | no | n/a | multi-select; empty = all types; each id must exist |
| Assigned operators | no | n/a | searchable multi-select of **gate-operator candidates** (BUG-018), shown "{display name} — {email} ({profile type})". A candidate is an **approved app account** whose profile type is operational (`IsForVisitor=false`) and carries a `MobileAppRole` of Staff or Moderator. Admin accounts are no longer offered. Server-enforced: an ineligible id → 400 `GATE_ASSIGNMENT_INVALID` naming the id. |
| Hall (hall-door gate) | no | n/a | single-select of active halls; empty = perimeter gate. When set, an Allowed scan feeds `HallAttendance` for the session live in that hall (the scan→arrival→attendance chain). Server-validated: the hall must exist and be active (`GATE_HALL_INVALID`, 400). |
| Active | (Edit only) | bool | — |

## 5. Data flow + endpoints

Canonical D-132 CRUD shape over the BFF:

- **List** — `LoadAsync` posts `_query` (a `GridQuery`) to
  `/account/api/admin/gates/list`; the API returns
  `ApiResult<GridPage<AdminGateSummary>>`. `AdminGateService.ListAllAsync`
  clamps `Top` to 1–200 (default 25), applies the per-column filters
  (`code` / `name` / `namearabic` substring; `isActive` status), an optional
  free-text `Search` across Code/Name/NameArabic, and sorting on
  Code / Name / Name (Arabic) / Direction / CreatedAt (default Code asc).
- **Detail** — Edit / Details / Deactivate first `GET
  /account/api/admin/gates/{id}` → `ApiResult<AdminGateDetail>` (the grid
  carries the summary; the forms work against the full detail).
- **Create** — `POST /account/api/admin/gates` with `AdminCreateGateRequest`
  (Code uppercased client-side; blank descriptions sent as `null`).
- **Update** — `PUT /account/api/admin/gates/{id}` with `AdminUpdateGateRequest`
  (adds `IsActive`); the service syncs the allow-list and re-points the active
  assignment set (revoked rows get `RevokedAt` / `RevokedByUserId`).
- **Deactivate** — `DELETE /account/api/admin/gates/{id}` → soft-delete
  (`IsActive = false`); already-inactive is a no-op.
- **Export / Import** — `POST /account/api/admin/gates/export` (binary XLSX) and
  `POST /account/api/admin/gates/import` (multipart), gated by `Gates.Export` /
  `Gates.Import` respectively.

Every successful create / update / deactivate invalidates the gate-config cache
(`IGateConfigCache.Invalidate(gateId)`) so the operator console picks up the
change.

## 6. Validation + error handling

- **Client guard (`GatesAddEdit`)** before any POST/PUT: Code 2–16 chars
  (`Admin.Gates.Field.CodeInvalid`), Name 1–128 (`…NameInvalid`), Name (Arabic)
  1–128 (`…NameArabicInvalid`).
- **Server guard (`AdminGateService.Validate`)** — trims + upper-cases Code,
  length-gates Code (2–16), Name (1–128), NameArabic (1–128), Description /
  DescriptionArabic (≤1024); any failure → **400 `GATE_INVALID`** (bilingual,
  field-specific message).
- **Request validator (BUG-018)** — `AdminCreateGateRequestValidator` /
  `UpdateGateRequestValidator` (FluentValidation) enforce required Code / Name /
  Name (Arabic), the 16 / 128 / 1024 max lengths (matching EF `HasMaxLength` and
  the CP `MaxLength`) and non-null id lists before the endpoint runs.
- **Logical-FK guards** — an unknown allowed-profile-type id → **400
  `GATE_PROFILE_TYPE_INVALID`**; an **ineligible** operator user id → **400
  `GATE_ASSIGNMENT_INVALID`**, whose message names the offending id(s).
  Eligibility (BUG-018): an approved app account on an operational profile type
  carrying Staff/Moderator, or an approved CP admin account for the retained
  operator console. Resolved as two reads (Identity then App) — never a
  cross-database join (D-157).
- **Duplicate code** — **409 `GATE_CODE_DUPLICATE`** on create, and on update
  when changing a code to one another gate already holds (surfaces the
  upper-cased code in the message).
- **Not found** — **404 `GATE_NOT_FOUND`** on get / update / deactivate of a
  missing id.
- **List failure** — `LoadAsync` surfaces a bilingual error toast
  (`Admin.Gates.LoadFailed`) and renders no rows.
- **Import upload defence** (shared grid base): an empty / non-`.xlsx` upload
  (ZIP-magic check) → 400; a file over **5 MB** → 413; a workbook missing the
  "Gates" sheet or a required header → 400. Per-row failures (duplicate code,
  blank required field) are aggregated as `GridImportRowError`s — one bad row
  never aborts the batch.

## 7. Edge cases + known limitations

- **Empty allow-list = all types.** `AllowedProfileTypeCount == 0` is rendered
  as "All" and means the gate admits every profile type — it is not an error
  state.
- **Operator count is active-only.** The grid "Operators" column counts only
  `IsActive` assignments; revoking an operator on Edit soft-revokes the row
  (keeps the audit trail) rather than deleting it. The **Details** view lists the
  assigned operators by name + email (BUG-018), read from
  `GET /admin/gates/{id}/assignments`, so an assignment can be audited from the CP.
- **Code uppercasing.** "g-main-1" and "G-MAIN-1" are the same code; the server
  normalises to ASCII upper before the uniqueness check and before storing.
- **Deactivate is unconditional.** A soft-deleted gate's assignments and
  allow-list rows are left as-is; re-activation is via Edit (the Active
  checkbox).
- **Import is insert-only.** It always creates; it never updates an existing
  gate. A duplicate code therefore becomes a per-row error. The import binds
  only Code / Name / NameArabic (required) plus optional Description /
  DescriptionArabic / DirectionMode; allowed-types and operators are not
  importable.
- **Operator picker is searchable, not a blind list (BUG-018).** It loads the
  first 25 eligible candidates and offers a server-side search box; only
  **approved** accounts are offered (deactivated / pending / rejected are never
  candidates). Ticking is additive against the loaded set — an operator already
  assigned but off the current page keeps their assignment on save.
- **Gate-form lookups need only `Gates.Manage` (BUG-018).** The profile-type and
  hall options come from `GET /admin/gates/form-options`, so a Security-team gate
  manager no longer sees empty dropdowns; a lookup failure now renders an alert
  instead of being swallowed.

## 8. i18n + RTL

`Admin.Gates.*` resx keys (column headers, field labels + hints, direction
options, actions, pager, toasts, validation, empty state) plus the shared
`Grid.*` keys for the Excel toolbar and the import-result toast. EN ↔ AR parity
is maintained; the page mirrors under `<html dir="rtl" lang="ar">` and the
Direction options + form actions reverse. (resx phrasing is descriptive — the
exact strings live in the `Strings.*.resx` resources.)

## 10. Use cases

- Create / edit / deactivate a venue entry gate; restrict a gate to chosen
  profile types; assign / revoke gate operators; bulk-seed gates via Excel
  import and export the current gate set.

## 11. E2E

See [`docs/tests/e2e/cp-admin-gates.md`](../../tests/e2e/cp-admin-gates.md):
E2E-GAT-001 full CRUD round-trip, 002 restricted gate (allowed types +
operators), 003 direction column, 004 filter by code, 005 sort, 006 paging,
007 details modal, 008 empty state, 009 auth gate, 010 client validation,
011 `GATE_INVALID` 400, 012 `GATE_CODE_DUPLICATE` 409, 013 list 500, 014 RTL,
015 per-column filter, 016 presentation toggle persists (D-353), 017 full-page
round-trip (D-353), 018 delete-confirmation gate (D-353), 019 Excel export
(D-356), 020 Excel import (D-356), 021 Excel import rejection (D-356).

## 12. Related docs

- Permission: `PermissionCatalog.Gates.Manage` (+ `Operate`, `ViewOwnReports`,
  `Export`, `Import`); seeded in `PermissionCatalog.All` with `BaselineRoles`
  `AdminOnly` for Manage/Export/Import and `GateOperator` for Operate/ViewOwnReports.
- Nav: `CpNavigation` `Module.Gates` → `/admin/gates`, `RequiredPermission =
  PermissionCatalog.Gates.Manage`.
- Audit events: `Gate.Created`, `Gate.Updated`, `Gate.Deactivated`.
- Authority spec: SIMF-FDS-003 §5.6 / SIMF-API-GATES-001 §6.
- Decisions: D-148 (Gate Module), D-353 (CrudShell + presentation toggle +
  confirm-gated delete), D-356 (grid Excel export + import).
- Sibling pages: `/admin/gates/operator` (operator console),
  `/admin/gates/dashboard` (operations dashboard).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| (D-148) | D-148 | Original — Gate Module CRUD list mirroring `HallsList`; Direction + allowed-type + operator columns, allow-list + operator assignment, soft-delete, gate-config cache invalidation. |
| 2026-06-11 | D-356 / D-353 | Excel export + import added (toolbar Export/Import → `.xlsx`, sheet "Gates", `Gates.Export` / `Gates.Import` permissions); CRUD forms hosted by `CrudShell` as `GatesAddEdit` + `GatesViewDelete` with a `SimfConfirm`-gated Deactivate (no longer a one-click list delete) and a Page↔Popup presentation toggle persisted in `localStorage` (`simf.cp.prefs.gates`). E2E catalogue extended with E2E-GAT-016…021. Reference doc authored. |
| 2026-07-12 | D-751 (chain) | Optional **Hall** picker added (nullable `Gate.HallId` FK → active halls; migration `D744`). A gate with a Hall is a "hall-door gate": an Allowed scan feeds `HallAttendance` for the session live in that hall (scan→arrival→attendance chain), binding `HallAttendance.UserId` to the Identity user id. Reuses the existing `Gates` manage permission (no new permission). Hall is server-validated (`GATE_HALL_INVALID`). E2E extended with E2E-GAT-023/024. |
| 2026-07-26 | BUG-018 | **Gate-operator model corrected to the owner's app-first ruling.** The operator picker no longer lists Control-Panel admins: it binds to `POST /admin/gates/operator-candidates/list` (approved app accounts on an operational profile type carrying Staff/Moderator), is server-searched and shows "{name} — {email} ({profile type})". `AdminGateService.ValidateOperatorsAsync` now enforces the same eligibility and names the offending id in the 400. The profile-type / hall lookups moved to `GET /admin/gates/form-options` (`Gates.Manage`) so a Security-team gate manager stops seeing empty dropdowns, and a failed lookup renders an alert instead of being swallowed. The Details view lists the assigned operators (name + email) instead of a bare count, and the gate create/update requests gained FluentValidation validators. E2E extended with E2E-GAT-025…029. |

_Last reviewed:_ 2026-07-26 by Claude (BUG-018 — gate-operator model). Prior: 2026-06-11 by Claude (D-356 Phase 5 — Excel export + import + D-353 toggle).
