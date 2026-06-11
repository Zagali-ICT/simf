# Speakers — `/admin/speakers`

| | |
|--|--|
| **Route** | `/admin/speakers` |
| **Audience** | Administrator (any role holding `Speakers.View`) |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.Speakers.View)]`. API: per-endpoint `Speakers.View` / `Speakers.Create` / `Speakers.Edit` / `Speakers.Delete` / `Speakers.Export` / `Speakers.Import` policies + `RequireApprovedAccount`; mutations + export/import are `RequireRateLimiting("auth")`. |
| **Pattern** | Canonical `SimfDataGrid` CRUD. **D-353** Page↔Popup presentation toggle + `CrudShell`-framed reusable forms. **D-356** generic Excel export/import via `CrudGridExcel`. |
| **Status** | ✅ Real (D-199 original; D-353 framing + D-356 Excel, 2026-06-10) |
| **Backend endpoints** | `POST /account/api/admin/speakers/list`, `GET /account/api/admin/speakers/{id}`, `POST /account/api/admin/speakers`, `PUT /account/api/admin/speakers/{id}`, `DELETE /account/api/admin/speakers/{id}`, `POST /account/api/admin/speakers/export`, `POST /account/api/admin/speakers/import` (BFF → API `/api/v1/admin/speakers/*`) |
| **Source** | [`SpeakersList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakersList.razor), [`SpeakersAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakersAddEdit.razor), [`SpeakersViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakersViewDelete.razor), [`SpeakerEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SpeakerEndpoints.cs), [`SpeakersExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SpeakersExcelEndpoints.cs), [`AdminSpeakerService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminSpeakerService.cs) |
| **Backed by** | `dbo.Speakers` table on `SimfAppDbContext` (D-199 build wave). |
| **Tests** | [`docs/tests/e2e/cp-admin-speakers.md`](../../tests/e2e/cp-admin-speakers.md); API integration: [`tests/SIMF.Api.Tests/AdminSpeakersTests.cs`](../../../tests/SIMF.Api.Tests/AdminSpeakersTests.cs) + [`tests/SIMF.Api.Tests/SpeakersExcelTests.cs`](../../../tests/SIMF.Api.Tests/SpeakersExcelTests.cs) |
| **Last reviewed** | 2026-06-10 |

## 1. Purpose

Programme speakers per SIMF-DAT-001 §5.4 — the people who appear on the public
speaker list and are referenced by sessions. Each speaker carries a **Code**
(the programme team's stable identifier, e.g. "SPK-001"), a bilingual name,
an optional rank / title, an optional country, bilingual rich-text (bio,
qualifications, training & experience, awards), consent flags (allows meeting
requests / allows data sharing), social URLs (Facebook / LinkedIn / X), a
display-order key, and an active flag. An optional link to the shared Contact
directory (SIMF-FDS-014 / D-281..D-283) and an optional `UserProfileId` are
also persisted.

This page follows the canonical `SimfDataGrid` CRUD shape used across the
Control Panel. Two cross-cutting upgrades apply: **D-353** moved Add / Edit /
Details / Deactivate into reusable forms framed by `CrudShell` with a
Page↔Popup presentation toggle, and **D-356** added generic Excel export +
import through the shared `CrudGridExcel` component.

## 4. UI

- `SimfBanner` (title `Admin.Speakers.Title`) + the canonical `SimfDataGrid`
  toolbar (Select all, Add speaker, Export, Import).
- **Grid columns:** Code (sortable), Name (sortable + filterable), Name
  (Arabic), Rank, Country (resolved to the localized country name; "—" when
  absent), Display order (sortable), Active (Active / Inactive pill via
  `SimfPill`). Name (Arabic), Rank, Country and Active are not sortable;
  only Name is filterable.
- **Multiselect** row checkboxes (`Multiselect="true"`, `RowKey` = the speaker
  id) feed the Export action's selected-ids set.
- Per-row actions: **Edit**, **Details**, **Deactivate** icons.
- Empty grid renders `SimfEmptyState` (title `Admin.Speakers.None`).
- **Page ↔ Popup presentation toggle (D-353):** the toolbar
  `CrudPresentationToggle PageKey="speakers"` (`@bind-Value="_presentation"`)
  lets the admin host Add / Edit / Details / Deactivate as a dialog or a full
  page; the choice persists in `localStorage` under `simf.cp.prefs.speakers`
  (read back on load via `Prefs.GetPresentationAsync("speakers")`). In full-page
  mode the grid + banner are hidden (`GridHidden`) while the form takes over.
- **CrudShell framing (D-353):** when a form is open, `CrudShell` renders either
  `SpeakersAddEdit` (Add / Edit) or `SpeakersViewDelete` (Details / Deactivate)
  as a popup or full page per `_presentation`.
- **Excel export + import (D-356):** the toolbar **Export** and **Import**
  actions are wired to a `CrudGridExcel @ref="_excel" Resource="speakers"`.
  Export posts `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/speakers/export` (selected rows if any, else the whole
  filtered grid) and downloads `simf-speakers-{timestamp}.xlsx`; the "Speakers"
  sheet header row is `Code | Name | NameArabic | Rank | Country | DisplayOrder | IsActive`.
  Import clicks the hidden file input `speakers-import-input` (`accept=".xlsx"`),
  posts the workbook to `/account/api/admin/speakers/import`, and shows a result
  modal ("N created, N updated, N skipped" + per-row errors) followed by the
  shared green `Grid.Import.Done` toast and a grid reload.

## 4.5 Form fields

`SpeakersAddEdit.razor` (Add / Edit). Lengths below are the `MaxLength` on the
field plus the server-side guard in `AdminSpeakerService.ValidateAndNormalise`
and the create/update path.

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Code | yes | 16 | 2–16 chars; trimmed + upper-cased server-side; unique |
| Name (English) | yes | 128 | 1–128 chars |
| Name (Arabic) | yes | 128 | 1–128 chars |
| Rank / title | no | 64 | optional |
| Country | no | n/a | picker loaded from `/account/api/admin/countries/list` (active rows); must reference an existing **active** Country |
| Bio (English / Arabic) | no | 2048 | optional rich-text |
| Qualifications (English / Arabic) | no | 1024 | optional |
| Training & experience (English / Arabic) | no | 1024 | optional |
| Awards (English / Arabic) | no | 1024 | optional |
| Allows meeting requests | no | bool | checkbox |
| Allows data sharing | no | bool | checkbox |
| Facebook / LinkedIn / X URL | no | 256 each | each ≤ 256 chars server-side |
| Contact | no | n/a | optional `ContactPicker` link to the shared Contact directory; must reference an existing **active** Contact |
| Display order | yes | n/a | integer ≥ 0 |
| Active | (Edit only) | bool | shown only in Edit mode |

`SpeakersViewDelete.razor` renders all of the above as a read-only description
list (`dl`); in Delete mode it adds a red **Deactivate** button gated by a
`SimfConfirm` dialog.

## 5. Data flow + endpoints

- **List** — `POST /account/api/admin/speakers/list` (BFF → API
  `POST /admin/speakers/list`, policy `Speakers.View`). Body is a `GridQuery`
  (default `Top = 20`); response is `ApiResult<GridPage<AdminSpeakerSummary>>`.
- **Get one** — `GET /account/api/admin/speakers/{id}` (API
  `GET /admin/speakers/{id:guid}`, policy `Speakers.View`). Returns the full
  `AdminSpeakerDetail` (the grid summary omits the bilingual rich-text + social
  URLs, so Edit / Details / Deactivate first fetch the detail).
- **Create** — `POST /account/api/admin/speakers` (API `POST /admin/speakers`,
  policy `Speakers.Create`, rate-limited "auth"). Body `AdminCreateSpeakerRequest`;
  Code is upper-cased client- and server-side.
- **Update** — `PUT /account/api/admin/speakers/{id}` (API
  `PUT /admin/speakers/{id:guid}`, policy `Speakers.Edit`, rate-limited "auth").
  Body `AdminUpdateSpeakerRequest`.
- **Deactivate (soft-delete)** — `DELETE /account/api/admin/speakers/{id}` (API
  `DELETE /admin/speakers/{id:guid}`, policy `Speakers.Delete`, rate-limited
  "auth"). Sets `IsActive = false`; idempotent (early-returns when already
  inactive, writing no second audit row).
- **Export (D-356)** — `POST /account/api/admin/speakers/export` (policy
  `Speakers.Export`). `ExportSpeakersEndpoint : AdminGridExportEndpoint<AdminSpeakerSummary>`;
  sheet "Speakers"; file prefix `simf-speakers`; columns Code, Name, NameArabic,
  Rank, Country (English name), DisplayOrder, IsActive; whole-grid set capped at
  5000 rows; lists via the same `IAdminSpeakerService.ListAllAsync` the list
  endpoint uses, so the export honours the current filter.
- **Import (D-356)** — `POST /account/api/admin/speakers/import` (policy
  `Speakers.Import`). `ImportSpeakersEndpoint : AdminGridImportEndpoint`;
  insert-only; sheet "Speakers"; required headers `Code | Name | NameArabic`
  (Rank + DisplayOrder optional); Country, bilingual rich-text, social URLs and
  consent flags are **deliberately not imported** (set later via Edit). Each row
  binds to `AdminCreateSpeakerRequest` with Code upper-cased; a duplicate Code is
  a per-row error, not a batch abort.

**Permission gating (per CLAUDE.md hard rule):**
- Page: `@attribute [RequirePermission(PermissionCatalog.Speakers.View)]`.
- Nav: `CpNavigation` item `Module.Speakers` → `/admin/speakers`,
  `RequiredPermission = Speakers.View` (icon "mic").
- Permission codes (`PermissionCatalog.Speakers`): `Speakers.View`,
  `Speakers.Create`, `Speakers.Edit`, `Speakers.Delete`, `Speakers.Export`,
  `Speakers.Import`.

**Audit events** (`AuditEvents`, written by `AdminSpeakerService`):
`Speaker.Created`, `Speaker.Updated`, `Speaker.Deactivated`, each with the
actor's user id in `ActorUserId`.

## 6. Validation + error handling

- **Server-side `AdminSpeakerService.ValidateAndNormalise`:** trims +
  upper-cases Code (case-insensitive uniqueness); length-gates Code (2–16),
  Name (1–128), NameArabic (1–128). `DisplayOrder` must be ≥ 0; each social
  URL ≤ 256 chars (`ValidateSocialUrls`). A supplied `CountryId` must reference
  an existing **active** Country (`EnsureCountryIsValidAsync`); a supplied
  `ContactId` must reference an existing **active** Contact
  (`EnsureContactIsValidAsync`) — all of these throw
  `SPEAKER_INVALID` (400) with a bilingual message.
- **Client-side `SpeakersAddEdit.HandleSubmitAsync`:** mirrors the bounds (Code
  2–16, Name ≤ 128, NameArabic ≤ 128, Display order parses to ≥ 0, Country id
  parses to > 0) and shows a `SimfAlert` error in the form without firing a
  request when a guard fails (resx keys `Admin.Speakers.Field.CodeInvalid`,
  `NameInvalid`, `NameArabicInvalid`, `DisplayOrderInvalid`, `CountryInvalid`).
- **Duplicate code:** `SPEAKER_CODE_DUPLICATE` (409), bilingual, surfaces the
  code: "A speaker with code '{code}' already exists." On update the clash check
  runs only when the code actually changes.
- **Not found:** `SPEAKER_NOT_FOUND` (404).
- **Reserved:** `SPEAKER_IN_USE` is registered in `ErrorCodes` for a future
  in-use guard; the current Deactivate is unconditional.
- **Load / server failure:** a non-success `/list` envelope (or a thrown call)
  surfaces a red toast (`Admin.Speakers.LoadFailed`); save / delete failures
  surface the envelope's `MessageForCurrentCulture()` or the
  `Admin.Speakers.Fallback` key.
- **Import errors:** a non-`.xlsx` upload (ZIP-magic check) and an over-size
  upload are rejected by the shared import base before any row is applied; the
  per-row binder in `ImportSpeakersEndpoint.ApplyRowAsync` throws a bilingual
  `DataValidationException` for a blank/out-of-range Code or a blank
  English/Arabic name, and a duplicate Code raises `SPEAKER_CODE_DUPLICATE` as a
  per-row error — one bad row never aborts the batch. `CrudGridExcel` raises
  `OnError`, which the page surfaces as a red toast.

## 7. Edge cases + known limitations

- **Code uppercasing** — server normalises to upper-invariant before storing,
  so "spk-001" and "SPK-001" are the same code; display preserves the upper form.
- **Idempotent deactivate** — re-deactivating an already-inactive speaker
  returns 200 and writes no second audit row.
- **Country picker resilience** — if `/admin/countries/list` fails on first
  render the picker stays empty and the admin can still submit with no country.
- **Import scope** — insert-only and intentionally narrow (Code / Name /
  NameArabic / Rank / DisplayOrder). Country (a numeric FK), bilingual rich-text,
  social URLs and consent flags cannot be expressed safely in a flat sheet, so
  they are set via the Edit form after the bulk insert; the exported Country
  column is the read-only display name.
- **Known resx gap (out of scope here, flagged in the E2E catalogue).** The
  English resx is missing `Admin.Speakers.Delete.Title` /
  `Admin.Speakers.Delete.Message`, so the EN `SimfConfirm` title/body fall back
  to the resource keys until added (both exist in `Strings.ar.resx`).

## 8. i18n + RTL

`Admin.Speakers.*` keys span the banner, grid columns/actions/pager, the form
fields/hints, the validation messages, and the create/update/deactivate toast
templates; shared `Grid.Export` / `Grid.Import` / `Grid.Import.Done` keys cover
the Excel toolbar. EN ↔ AR parity is preserved (apart from the noted
`Admin.Speakers.Delete.*` EN gap). The Country column and picker label render the
Arabic country name under `ar` culture; the whole page mirrors to RTL under
`<html dir="rtl" lang="ar">`.

## 10. Use cases

- UC-SPK-CREATE-001 (add a speaker), UC-SPK-EDIT-001 (edit incl. Active toggle),
  UC-SPK-DETAILS-001 (read-only details), UC-SPK-DEACTIVATE-001 (SimfConfirm-gated
  soft-delete), UC-SPK-EXPORT-001 / UC-SPK-IMPORT-001 (Excel export / import).

## 11. E2E

See [`docs/tests/e2e/cp-admin-speakers.md`](../../tests/e2e/cp-admin-speakers.md):
E2E-SPK-001 full CRUD round-trip, 002 full-payload round-trip, 003 empty state,
004 page auth gate, 005 filter, 006 sort, 007 pager, 008 details modal, 009
Active toggle, 010 required-field validation, 011 bounds validation, 012
duplicate code 409, 013 invalid/inactive country 400, 014 server 500 on list,
015 idempotent deactivate, 016 RTL, **017 presentation-toggle persist (D-353),
018 full-page round-trip (D-353), 019 SimfConfirm delete gate (D-353), 020 Excel
export (D-356), 021 Excel import (D-356), 022 Excel import rejection (D-356)**.

## 12. Related docs

- Authority spec: SIMF-DAT-001 §5.4.
- Decisions: D-199 (build-wave freeze-lift — original Speakers module), D-353
  (Page↔Popup toggle + `CrudShell` + `SimfConfirm` delete gate), D-356 (generic
  Excel export/import via `CrudGridExcel`), D-281..D-283 (shared Contact link).
- Auth/permissions: `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`,
  `docs/SIMF-Permission-Catalogue.md`.
- Sibling Programme modules: [`admin-themes.md`](admin-themes.md),
  [`admin-sponsors.md`](admin-sponsors.md).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-10 | D-199 | Original — Speakers entity + `dbo.Speakers` table + canonical `SimfDataGrid` CRUD page (this reference doc backfilled 2026-06-10). |
| 2026-06-10 | D-356 / D-353 | Add / Edit / Details / Deactivate moved into reusable `SpeakersAddEdit` + `SpeakersViewDelete` forms hosted by `CrudShell` with a `SimfConfirm`-gated Deactivate and a Page↔Popup presentation toggle persisted in `localStorage` (`simf.cp.prefs.speakers`); Excel export + import added (toolbar Export/Import → `.xlsx`, sheet "Speakers", endpoints `/admin/speakers/export|import`, policies `Speakers.Export` / `Speakers.Import`). E2E catalogue extended with E2E-SPK-017…022. |

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5).
