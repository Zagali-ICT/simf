# Organisations — `/admin/organisations`

| | |
|--|--|
| **Route** | `/admin/organisations` |
| **Audience** | Administrator (any role granted `Organisations.*`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Organisations.View)]` (page); API endpoints gated per-action by `Organisations.View / .Create / .Edit / .Delete / .Import / .Export` + `RequireApprovedAccount`; mutations + import + export carry `RequireRateLimiting("auth")` |
| **Pattern** | D-220 reference lookup + D-255 SimfDataGrid + D-353 CrudShell forms + D-356 Excel **export only** |
| **Status** | ✅ Real (D-220 lookup; D-353 form split; D-356 Excel export) |
| **Backend endpoints** | BFF passthroughs `/account/api/admin/organisations/*` (`AccountEndpoints.cs`) → API: `POST /admin/organisations/list`, `GET /admin/organisations/{id}`, `POST /admin/organisations`, `PUT /admin/organisations/{id}`, `DELETE /admin/organisations/{id}`, `POST /admin/organisations/import` (bespoke gov-Excel), `POST /admin/organisations/export` (D-356 generic grid export) |
| **Source** | [`OrganisationsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OrganisationsList.razor), [`OrganisationAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OrganisationAddEdit.razor), [`OrganisationViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OrganisationViewDelete.razor), [`OrganisationEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/OrganisationEndpoints.cs), [`OrganisationExcelEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/OrganisationExcelEndpoints.cs), [`AdminOrganisationService`](../../../src/Backend/SIMF.Infrastructure/Organisations/AdminOrganisationService.cs), [`OrganisationContracts`](../../../src/Shared/SIMF.Contracts/Organisations/OrganisationContracts.cs), [`Organisation`](../../../src/Backend/SIMF.Domain/Organisations/Organisation.cs) |
| **Backed by** | `dbo.Organisations` table on `SimfAppDbContext` (D-220 additive migration). |
| **Tests** | [`docs/tests/e2e/cp-admin-organisations.md`](../../tests/e2e/cp-admin-organisations.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

The shared **bilingual Saudi-companies lookup** introduced under D-220 — the
reference table the visitor profile "الجهة" (organisation) picker resolves
against, and the directory admins can curate by hand or bulk-load from a
government Excel sheet. Each row carries an Arabic name (the only required
field), an optional English name, an optional unique commercial-registration
number, plus optional sector, city, phone, email and website. Rows soft-delete
via `IsActive`, so deactivating one removes it from the public picker without
losing history.

The admin grid is the canonical SimfDataGrid list-page shape (server-paged,
sortable, per-column filterable, multiselect). The public typeahead the visitor
app calls is a separate, sign-in-only endpoint (`GET /app/organisations`) backed
by `IPublicOrganisationService`; it is not part of this CP page and is not
admin-gated.

## 4. UI

- `SimfBanner` titled "Organisations" + a toolbar carrying the server-side
  Search field (text + "Search" button) and the `SimfDataGrid` action set.
- `SimfDataGrid` (D-255 owner-mandated list-page standard) with select-all +
  per-row checkbox, full pager, and quiet per-row icon actions:
  - **Add** (`OnAdd`) — opens `OrganisationAddEdit` in Create mode.
  - **Edit** (pencil, `OnEditOne`) — fetches the full detail via
    `GET /account/api/admin/organisations/{id}` first (the grid summary omits
    Phone / Email / Website), then opens `OrganisationAddEdit` in Edit mode.
  - **Details** (eye, `OnDetailsOne`) — opens `OrganisationViewDelete`
    read-only (no Deactivate button).
  - **Delete** (trash, `OnDeleteOne`) — opens `OrganisationViewDelete` with the
    red Deactivate button.
- Grid columns: Name (Arabic) [sortable, filterable], Name (English)
  [filterable], Commercial registration [filterable], Sector [filterable],
  City [sortable, filterable], Active [sortable] (rendered as an on/off
  `SimfPill`). Empty / null text columns render "—".
- `SimfEmptyState` titled "No organisations found" when the grid is empty.
- **Excel export only (D-356):** the toolbar **Export** action posts
  `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/organisations/export` via `simfAccount.downloadXlsx`
  (a direct browser download). With rows selected it sends only those `Ids`
  (and `Query = null`); with none selected it sends the current `Query` (the
  filtered / searched grid). The workbook is `simf-organisations-{timestamp}.xlsx`,
  sheet name **"Organisations"**, header row
  `NameAr | NameEn | CommercialRegistration | Sector | City | IsActive`. The
  generic grid layer is the **reference example** for the `AdminGridExportEndpoint<TRow>`
  base used across resources — a concrete subclass supplies only the route,
  the `Organisations.Export` permission, the sheet name, the file prefix and
  the column descriptors. The whole-grid export is **capped at 5,000 rows**.
- **Bespoke government-Excel import (separate from the D-356 grid layer):** the
  toolbar **Import** action opens a `SimfModal` ("Import organisations from
  Excel") with a `.xlsx`-only file input. Upload posts the workbook
  (multipart, field `file`) to `/account/api/admin/organisations/import` via
  `simfAccount.uploadFile`; the modal then shows the `OrganisationImportResult`
  tallies (rows read / inserted / updated / skipped) plus a per-row error list.
  This is an **upsert keyed on commercial registration** (or, absent a CR, on
  the exact active Arabic name) — it is **not** a generic grid import; the
  generic export base has no matching import sibling here. See §6 for the
  upload guards.
- **Page ↔ Popup presentation toggle (D-353):** the grid's `CustomToolbar`
  hosts a `CrudPresentationToggle` bound to `PageKey = "organisations"`. The
  admin can host Add / Edit / View / Delete as a dialog or a full page; the
  choice is read on init and persisted via `CpPreferences` under the
  `organisations` page key (`localStorage`). In full-page mode the grid +
  banner hide (`GridHidden`) while the `CrudShell` frames the active form.

## 4.5 Form fields

`OrganisationAddEdit` (`CrudAddEditFormBase<AdminOrganisationDetail>`). MaxLength
values are the UI caps. The stored column width in `OrganisationConfiguration`
is the source of truth; `AdminOrganisationService` carries it as named constants
used by both `ValidateAndNormalise` and the import `Clamp`, so a value that
clears validation always fits the column. A UI cap may be stricter than the
server cap but never looser.

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Name (Arabic) | yes | 150 | 1–150 chars; trimmed; only required field |
| Name (English) | no | 150 | ≤ 150 chars; null when blank |
| Commercial registration | no | 32 | server allows ≤ 700 (the form is stricter); unique when present (409 on clash) |
| Sector | no | 128 | ≤ 128 chars |
| City | no | 128 | ≤ 128 chars |
| Phone | no | 32 | ≤ 32 chars |
| Email | no | 320 | ≤ 320 chars |
| Website | no | 512 | ≤ 512 chars |
| Active | (Edit only) | bool | shown only in Edit mode |

The form blocks an obviously-empty submit (blank Arabic name → inline bilingual
`Admin.Organisations.Required` alert, no POST); all other validation is enforced
server-side. Optional fields are sent as `null` rather than empty strings.

## 5. Data flow + endpoints

Canonical CP→BFF→API shape. The page never calls the API directly; it calls the
BFF passthroughs under `/account/api/admin/organisations/*` via the
`simfAccount.*` JS interop (`postJson` / `getJson` / `putJson` / `deleteJson` /
`uploadFile` / `downloadXlsx`), which forward to the FastEndpoints in
`OrganisationEndpoints.cs` + `OrganisationExcelEndpoints.cs`.

| Action | BFF call | API endpoint | Permission |
|--------|----------|--------------|------------|
| List (paged grid) | `POST …/organisations/list` | `ListOrganisationsEndpoint` | `Organisations.View` |
| Get one (detail) | `GET …/organisations/{id}` | `GetOrganisationEndpoint` | `Organisations.View` |
| Create | `POST …/organisations` | `CreateOrganisationEndpoint` | `Organisations.Create` |
| Update | `PUT …/organisations/{id}` | `UpdateOrganisationEndpoint` | `Organisations.Edit` |
| Deactivate (soft-delete) | `DELETE …/organisations/{id}` | `DeactivateOrganisationEndpoint` | `Organisations.Delete` |
| Import (gov Excel upsert) | `POST …/organisations/import` | `ImportOrganisationsEndpoint` | `Organisations.Import` |
| Export (grid → XLSX) | `POST …/organisations/export` | `ExportOrganisationsEndpoint` | `Organisations.Export` |

`ListAsync` clamps `Top` to 1–200 (export resets `Skip=0`, `Top=5000`); Search
runs a `LIKE` across Arabic name, English name, commercial registration and city;
per-column filters (`name`, `nameEn`, `commercialRegistration`, `sector`, `city`,
`isActive`) accumulate; sortable on `name`, `city`, `isActive`. All writes stamp
an audit entry (`organisation.created` / `.updated` / `.deactivated` /
`.imported`) via `IAuditLog`.

## 6. Validation + error handling

- **Server-side `AdminOrganisationService.ValidateAndNormalise`:** trims the
  Arabic name and length-gates it (1–150); each optional field is length-gated
  (NameEn ≤ 150, CommercialRegistration ≤ 700, Sector ≤ 128, City ≤ 128,
  Phone ≤ 32, Email ≤ 320, Website ≤ 512) and stored `null` when blank.
- **Invalid field:** 400 `ORGANISATION_INVALID` (`ErrorCodes.OrganisationInvalid`),
  bilingual message naming the offending field/limit.
- **Duplicate commercial registration:** 409 `ORGANISATION_INVALID` (bilingual,
  surfaces the CR in the message). Checked on create and on update when the CR
  changes.
- **Not found:** 404 `ORGANISATION_NOT_FOUND` (`ErrorCodes.OrganisationNotFound`).
- **Deactivate** is idempotent — an already-inactive row returns without a second
  audit write.
- **Import upload guards (`ImportOrganisationsEndpoint`):** a file is required;
  the upload is capped at **5 MB** (over → 413 `ORGANISATION_IMPORT_FAILED`);
  the first four bytes must be the ZIP magic `50 4B 03 04` (else a
  `DataValidationException` "not a valid Excel workbook"); an unparseable
  workbook → 400 `ORGANISATION_IMPORT_FAILED`. Per-row failures (e.g. a blank
  Arabic name) are counted under "Skipped" and surfaced in the result's error
  list (capped at 50 messages) — a bad row is **not** a batch abort.

## 7. Edge cases + known limitations

- **Export only for the generic grid layer.** Organisations exposes the D-356
  generic grid **export** (`ExportOrganisationsEndpoint` over the shared
  `AdminGridExportEndpoint<TRow>` base) but keeps its **bespoke** government-Excel
  bulk **import** (`ImportOrganisationsEndpoint` + `OrganisationImportResult`);
  there is **no** generic grid-import endpoint. Do not assume a symmetric
  generic import.
- **Import upsert key.** A row matches an existing organisation by commercial
  registration when present, otherwise by exact **active** Arabic name; imported
  text is clamped to the column lengths rather than rejected. The Arabic name is
  **not** unique, so the name path takes the oldest match (`CreatedAt`, then
  `Id`) rather than throwing once two organisations share a name.
- **Import fills, it does not clear.** On the update side every optional column
  coalesces (`existing.X = value ?? existing.X`): a blank cell in a bulk sheet
  means "not supplied", never "clear it", so a partial sheet carrying only the
  Arabic name cannot erase curated columns. Clearing a field deliberately is
  what the explicit admin edit form is for.
- **Import lookup is pre-loaded, not per row.** The whole sheet is normalised
  first, then two chunked `IN (...)` queries (≤ 500 keys each) load the
  candidate rows into two case-insensitive maps, replacing one
  `SingleOrDefaultAsync` per spreadsheet row.
- **Grid summary omits contact columns.** Phone / Email / Website are not in the
  grid; Edit / Details / Delete therefore fetch the full detail
  (`GET …/{id}`) before opening the form.
- **Deactivate is unconditional.** A row referenced by a visitor profile can
  still be deactivated; it simply drops out of the public picker.
- **Public picker is out of scope here.** `GET /app/organisations` is sign-in-only
  (not admin-gated, not `AllowAnonymous`) and lives on `IPublicOrganisationService`.

## 8. i18n + RTL

`Admin.Organisations.*` resx keys (title, search, columns, field labels, import
modal copy, toasts, confirm copy) plus shared `Grid.*` keys for the data-grid
chrome. EN ↔ AR parity is maintained; the page mirrors fully under
`<html dir="rtl">`. (The exact resx phrasing is owned by the resource files and
is described, not quoted, here.)

## 10. Use cases

- Create / edit / deactivate a lookup row; bulk-import a government workbook;
  export the filtered grid; resolve the visitor "الجهة" picker against the active
  set _(formal UCS entries tracked under the D-220 lookup workstream)_.

## 11. E2E

See [`docs/tests/e2e/cp-admin-organisations.md`](../../tests/e2e/cp-admin-organisations.md):
E2E-ORG-001 golden round-trip, 002 empty/no-match, 003 server-side search,
004 import golden, 005 page auth gate, 006 action gates, 007 client validation,
008 server validation (Arabic name > 150), 009 duplicate-CR conflict,
010 delete-confirm cancelled, 011 import bad-file rejection, 012 list 500,
013 RTL, 014 per-column filter, 015 column sort, 016 presentation toggle persists,
017 full-page round-trip, 018 SimfConfirm delete gate, 019 Excel export.

## 12. Related docs

- Per-page CP documentation set (4-aspect + README, D-380):
  [`../../CP/admin-organisations/README.md`](../../CP/admin-organisations/README.md)
  (Function / Logic / API / Design).
- E2E catalogue: `docs/tests/e2e/cp-admin-organisations.md`.
- Lower-layer API tests: `tests/SIMF.Api.Tests/OrganisationTests.cs` (CRUD +
  import upsert + public picker) and `tests/SIMF.Api.Tests/OrganisationExcelTests.cs`
  (D-356 grid export).
- Permission catalogue: `PermissionCatalog.Organisations` (View / Create / Edit /
  Delete / Import / Export); guide `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`.
- Decisions: D-220 (Organisation lookup), D-353 (CrudShell form split + presentation
  toggle), D-356 (generic grid Excel export).
- Generic grid layer: `AdminGridExportEndpoint<TRow>` — Organisations is its
  reference example.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-31 | D-220 / D-255 | Original — Organisation lookup entity + additive migration + bilingual admin CRUD on the SimfDataGrid list-page standard, plus the bespoke government-Excel import and the public picker search. |
| 2026-06-02 | D-353 | CRUD forms split into reusable `OrganisationAddEdit` + `OrganisationViewDelete` hosted by `CrudShell`; the inline `SimfModal` edit + native `confirm()` delete replaced by a `SimfConfirm`-gated Deactivate and a Page↔Popup presentation toggle persisted in `localStorage`. |
| 2026-06-11 | D-356 | Generic grid **Excel export only** added (toolbar Export → `ExportOrganisationsEndpoint`, sheet "Organisations", header `NameAr \| NameEn \| CommercialRegistration \| Sector \| City \| IsActive`, 5,000-row cap); the bespoke gov-Excel import is unchanged and there is **no** generic import endpoint. Reference doc authored; E2E-ORG-019 added. |

| 2026-08-18 | — | Import + validation hardening. The two name caps corrected from 256 to the column's real 150 (a 151-to-256-character name passed the 400 validator and then failed `SaveChangesAsync` as an unhandled `SqlException`), the commercial-registration server cap corrected to the widened 700, and all eight lengths pulled into named constants on `AdminOrganisationService`. The import update path now coalesces every optional column, so a partial sheet no longer nulls curated data; the per-row `SingleOrDefaultAsync` lookups were replaced by two chunked pre-load queries, and the non-unique Arabic-name match now takes the oldest row instead of throwing. |

_Last reviewed:_ 2026-08-18 by Claude (import/validation hardening — column-width alignment + non-destructive import upsert).
