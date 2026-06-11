# System configuration — `/admin/configuration`

| | |
|--|--|
| **Route** | `/admin/configuration` |
| **Audience** | Administrator (any role holding the `Configuration.*` permissions) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Configuration.View)]`; API endpoints gated per-action by `Configuration.{View,Create,Edit,Delete,Export,Import}` + `RequireApprovedAccount`; mutations also `RequireRateLimiting("auth")` |
| **Pattern** | P2.4 (D-229) canonical CRUD over a flat key/value store, on `SimfDataGrid` (D-256). D-353 dialog/full-page framing + D-356 Excel. |
| **Status** | ✅ Real (P2.4 / D-229). Store **ships empty** — the team seeds the keys once the client confirms the list (FDS-012 OI-2). |
| **Backend endpoints** | CRUD (BFF `/account/api/admin/system-settings/*` → API `/admin/system-settings/*`): `POST .../list`, `GET .../{id}`, `POST ...`, `PUT .../{id}`, `DELETE .../{id}`. Excel (D-356): `POST .../export`, `POST .../import`. |
| **Source** | [`ConfigurationList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ConfigurationList.razor), `ConfigurationAddEdit` / `ConfigurationViewDelete` (hosted by `CrudShell`), [`SystemSettingEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SystemSettingEndpoints.cs), [`ConfigurationExcelEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/ConfigurationExcelEndpoints.cs), [`AdminSystemSettingService`](../../../src/Backend/SIMF.Infrastructure/Configuration/AdminSystemSettingService.cs), [contracts](../../../src/Shared/SIMF.Contracts/Admin/SystemSettings.cs) |
| **Backed by** | `dbo.SystemSettings` key/value table (`App/D229` migration — see CLAUDE.md D-229). |
| **Tests** | [`docs/tests/e2e/cp-admin-configuration.md`](../../tests/e2e/cp-admin-configuration.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

Admin CRUD over the platform **system-settings store** per SIMF-FDS-012 §5.5 —
the configuration values that are *not* content and *not* lookup categories. Each
setting is a flat row: an immutable **Key**, a free-text **Value**, an optional
**Description**, and an **Active** flag (soft-delete).

The store is intentionally **empty on first run**. The keys are seeded by the
team once the client confirms the exact list (FDS-012 open item OI-2), so nothing
is invented in the service — `AdminSystemSettingService` documents this explicitly.
Registration open/close is **not** managed here; it lives on its own Operations
page. The empty `SimfEmptyState` is the default first-run experience.

## 4. UI

- `SimfBanner` titled `Admin.Configuration.Title`, then a `SimfDataGrid`
  (`TItem="AdminSystemSettingSummary"`, page size `Top=20`, `Multiselect="true"`).
- Grid columns: **Key** (rendered in a `<code>` element; sortable + filterable),
  **Value** (filterable), **Description** (filterable), **Active**
  (`SimfPill` on/off — not filterable, not sortable).
- Only **Key** is sortable; Value / Description / Active headers do not sort.
- Empty store renders `SimfEmptyState` titled `Admin.Configuration.None`.
- Row actions are the grid's quiet icon buttons — **Edit** (`OnEditOne`),
  **Details** (`OnDetailsOne`) and **Delete** (`OnDeleteOne`); **Add** is the grid
  toolbar `OnAdd`. Edit / Details / Delete first fetch the full detail by id
  (`GET .../{id}`) because the grid row only carries the summary.
- **Page ↔ Popup presentation toggle (D-353):** the grid's `CustomToolbar` hosts a
  `CrudPresentationToggle` bound to `_presentation` with `PageKey = "configuration"`.
  Add / Edit / View / Delete are framed by `CrudShell` either as a dialog or a
  full page; in full-page mode the grid + banner are hidden (`GridHidden`). The
  choice is read/written through `CpPreferences` (localStorage key
  `simf.cp.prefs.configuration`) and restored in `OnInitializedAsync`.
- **Excel export + import (D-356):** the grid's `OnExport` / `OnImport` are wired
  to a reusable `CrudGridExcel` component (`Resource="system-settings"`).
  Export posts an `AdminGridExportRequest { Ids, Query }` (selected row ids, else
  the current filtered `Query`) to `/account/api/admin/system-settings/export`.
  Import opens the component's hidden `.xlsx` file input
  (`id="system-settings-import-input"`, `accept=".xlsx"`) and uploads to
  `/account/api/admin/system-settings/import`, then shows an import-result modal
  ("N created, N updated, N skipped" + a per-row error list) and reloads the grid.

## 4.5 Form fields

| Field | Required | MaxLength | Editable | Validation |
|-------|----------|-----------|----------|------------|
| Key | yes | 128 | Add only (locked on Edit) | server: 1–128 chars; unique among **active** rows |
| Value | yes (free text) | 2048 | Add + Edit | server: ≤ 2048 chars (trimmed) |
| Description | no | 512 | Add + Edit | optional; trimmed, blank → `null`, truncated to 512 |
| Active | (Edit only) | bool | Edit only | — (Add always creates `IsActive = true`) |

Notes grounded in `AdminSystemSettingService` + the contracts
(`AdminCreateSystemSettingRequest` carries Key/Value/Description only;
`AdminUpdateSystemSettingRequest` carries Value/Description/IsActive — there is no
`Key` on update, which is why the Key is immutable once created). Blank-Key is
caught client-side before any network call; length + duplicate are server-side.

## 5. Data flow + endpoints

`ConfigurationList` talks to the BFF passthroughs, which forward to the API with
the admin's access token:

| Action | BFF route | API route | Permission |
|--------|-----------|-----------|------------|
| List (grid) | `POST /account/api/admin/system-settings/list` | `POST /admin/system-settings/list` | `Configuration.View` |
| Detail by id | `GET /account/api/admin/system-settings/{id}` | `GET /admin/system-settings/{id}` | `Configuration.View` |
| Create | `POST /account/api/admin/system-settings` | `POST /admin/system-settings` | `Configuration.Create` |
| Update | `PUT /account/api/admin/system-settings/{id}` | `PUT /admin/system-settings/{id}` | `Configuration.Edit` |
| Deactivate | `DELETE /account/api/admin/system-settings/{id}` | `DELETE /admin/system-settings/{id}` | `Configuration.Delete` |
| Export (D-356) | `POST /account/api/admin/system-settings/export` | `POST /admin/system-settings/export` | `Configuration.Export` |
| Import (D-356) | `POST /account/api/admin/system-settings/import` | `POST /admin/system-settings/import` | `Configuration.Import` |

The Excel pair is registered at the BFF by `MapGridExcel(group, "system-settings")`
(= `MapGridExport` + a multipart `/import` proxy that reads form file `"file"`).
All responses use the `ApiResult<T>` envelope.

### 5.1 Excel export (D-356)

`ExportConfigurationEndpoint : AdminGridExportEndpoint<AdminSystemSettingSummary>`:

- Sheet name **`Configuration`**; columns, in order: **Key | Value | Description |
  IsActive** (mirroring the grid).
- The base resets `Skip = 0` and caps `Top` at **5000** rows (whole-grid cap). If
  the request carries `Ids`, only those rows are kept; otherwise the current
  filtered `Query` decides the set.
- API file-name prefix is `simf-configuration` (`FilePrefix`); the **BFF**
  `MapGridExport` re-wraps the download as `simf-system-settings-{timestamp}.xlsx`
  (the slug-based name the browser ultimately saves).

### 5.2 Excel import (D-356, insert-only)

`ImportConfigurationEndpoint : AdminGridImportEndpoint`:

- Required sheet **`Configuration`**; required headers **`Key`, `Value`**
  (`RowKey` echoes the **Key** in any per-row error so the admin can find the row).
- **Insert-only** — each row is bound to `AdminCreateSystemSettingRequest`
  (Key / Value / Description) and created; a blank Key throws a
  `DataValidationException` ("The setting key is required." / "مفتاح الإعداد مطلوب.").
  A duplicate **active** Key surfaces as a **per-row error** (the service's 409
  `SYSTEM_SETTING_KEY_DUPLICATE`), **not** a batch abort — one bad row never stops
  the rest.
- Upload defence in the shared base: file form-field `"file"` required; **5 MB**
  max (`413` `ADMIN_IMPORT_EMPTY` on over-size); ZIP-magic check (`50 4B 03 04`);
  **5000**-row workbook cap. A non-`.xlsx` / wrong-sheet / missing-header upload is
  rejected with HTTP 400.

## 6. Validation + error handling

Server-side, all in `AdminSystemSettingService`:

- **Key** — trimmed; 1–128 chars else **400** `SYSTEM_SETTING_INVALID`
  ("The setting key must be between 1 and 128 characters." /
  "يجب أن يتراوح طول مفتاح الإعداد بين 1 و 128 حرفاً.").
- **Value** — trimmed; ≤ 2048 chars else **400** `SYSTEM_SETTING_INVALID`
  ("The setting value must be 2048 characters or fewer." /
  "يجب ألا يتجاوز طول قيمة الإعداد 2048 حرفاً.").
- **Description** — trimmed; blank → `null`; silently truncated to 512 chars.
- **Duplicate key** — a create whose Key already exists on an **active** row →
  **409** `SYSTEM_SETTING_KEY_DUPLICATE` ("A setting with the key '{key}' already
  exists." / "يوجد إعداد بالمفتاح '{key}' بالفعل.").
- **Not found** — get / update / deactivate on an unknown id → **404**
  `SYSTEM_SETTING_NOT_FOUND` ("The system setting was not found." /
  "لم يتم العثور على الإعداد.").

Client-side, `ConfigurationList` surfaces failures in a `SimfAlert` toast via the
error's `MessageForCurrentCulture()`, with a localized fallback
(`Admin.Configuration.LoadFailed`) on a transport / 500. A blank Key is rejected
in the form before any request fires.

Every successful mutation writes an `OperationLog` audit row through `IAuditLog`:
`SystemSetting.Created`, `SystemSetting.Updated`, `SystemSetting.Deactivated`, each
carrying the actor id and a `Detail` of the form `id=...; key=...`.

## 7. Edge cases + known limitations

- **Empty by default** — the table ships with **no** rows; the team seeds keys once
  the client confirms the list (FDS-012 OI-2). The empty state, not data, is the
  first-run experience.
- **Key is immutable** — there is no `Key` on the update request, so the Edit form
  locks the Key field; only Value / Description / Active change after create.
- **Uniqueness is among active rows only** — the duplicate check is
  `s.IsActive && s.Key == key`. A deactivated row does not block re-creating the
  same key (and import is insert-only, so it cannot revive a row).
- **Deactivate is idempotent** — `DeactivateAsync` returns early if the row is
  already inactive, so a repeat delete is a no-op (still returns 200/true).
- **List paging defaults** — `Top` defaults to 50 and is clamped to 1–200 in the
  service; the page itself requests `Top=20`. Default sort is **Key ascending**.
- **Multiselect is cosmetic for CRUD** — select-all + per-row checkboxes exist, but
  there is no bulk-delete toolbar action; selection only seeds the Export `Ids`.

## 8. i18n + RTL

`Admin.Configuration.*` resx keys (Title, Loading, None, Summary, LoadFailed,
Saved, Deleted, the `Col.*` headers, and the Add/Edit/Details/Delete titles +
close label), plus the shared `Grid.*` keys (filter, paging, action labels,
Export / Import, and the import-result `Grid.Import.*` strings). EN ↔ AR parity is
maintained; the page mirrors to RTL under Arabic. (Exact Arabic phrasings live in
the resx files and the E2E catalogue, which quotes the expected bilingual text.)

## 10. Use cases

- Create a setting, edit its value, deactivate/activate it, and remove it
  (soft-delete) — the golden CRUD round-trip.
- Bulk-seed settings from an Excel workbook (D-356 import) and export the current
  set for review / back-up (D-356 export).
  _(Formal UCS detail entries follow the module's UCS expansion follow-up.)_

## 11. E2E

See [`docs/tests/e2e/cp-admin-configuration.md`](../../tests/e2e/cp-admin-configuration.md):
CRUD round-trip, empty store, Add/Edit modal shape (locked Key, Edit-only Active),
soft-delete via the D-353 `CrudShell` + `SimfConfirm` gate, page/action auth gates,
blank-Key client validation, over-length / duplicate / not-found server errors,
500 fallback, RTL, per-column filter + Key sort, presentation-toggle persistence,
full-page round-trip, and the D-356 Excel **export / import / import-rejection**
scenarios (E2E-CFG-021…023).

## 12. Related docs

- Authority spec: SIMF-FDS-012 §5.5 (System Configuration); OI-2 (the key list).
- Decisions: `docs/decisions/DECISIONS_LOG.md` D-229 (page + `SystemSettings`
  table), D-256 (raw table → `SimfDataGrid`), D-353 (dialog/full-page framing +
  `SimfConfirm` delete gate), D-356 (generic grid Excel export + import).
- Permissions: `PermissionCatalog.Configuration.{View,Create,Edit,Delete,Export,Import}`
  in [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs).
- Shared grid Excel plumbing: `AdminGridExportEndpoint<TRow>` /
  `AdminGridImportEndpoint` (API), `CrudGridExcel` (CP), `MapGridExcel` (BFF).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-02 | D-229 (P2.4) | Original — System Configuration page + `dbo.SystemSettings` key/value table + `Configuration.*` CRUD, gated API + CP. Store ships empty (FDS-012 OI-2). |
| (P2 wave) | D-256 | List page converted from a raw `<table>` to `SimfDataGrid` (per-column filters, select-all, quiet row icon actions). |
| (P2 wave) | D-353 | Inline `SimfModal` + native `confirm()` delete replaced by reusable `ConfigurationAddEdit` / `ConfigurationViewDelete` hosted in `CrudShell`, with a `SimfConfirm`-gated Deactivate and a Page↔Popup presentation toggle persisted in `localStorage`. |
| 2026-06-11 | D-356 | Excel **export + import** added — grid toolbar Export/Import via the reusable `CrudGridExcel` (`Resource="system-settings"`); sheet "Configuration", export columns `Key | Value | Description | IsActive`, insert-only import (required headers `Key | Value`, duplicate-key = per-row error), 5000-row + 5 MB caps. New permissions `Configuration.Export` / `Configuration.Import`. E2E catalogue extended with E2E-CFG-021…023. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 — Excel export + import + D-353 toggle).
