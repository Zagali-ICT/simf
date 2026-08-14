# Sponsors — `/admin/sponsors`

| | |
|--|--|
| **Route** | `/admin/sponsors` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator (admins holding the `Sponsors.*` permissions) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Sponsors.View)]` (page) + per-action API policies (`Sponsors.Create` / `Edit` / `Delete` / `Export` / `Import`) + `RequireApprovedAccount` |
| **Pattern** | D-199 event-module CRUD on the uniform CRUD shell — **D-353** `CrudShell` Add/Edit + View/Delete with the Page ↔ Popup `CrudPresentationToggle`, **D-356** Excel export + import via `CrudGridExcel` |
| **Status** | ✅ Real (D-199; D-353 toggle/CrudShell + D-356 Excel, 2026-06-10) |
| **Implements use case(s)** | Admin maintenance of the public Sponsors screen (Mockup page 23) per SIMF-FDS-004 / D-199 |
| **Backend endpoints** | `POST /account/api/admin/sponsors/list`, `GET /account/api/admin/sponsors/{id}`, `POST /account/api/admin/sponsors`, `PUT /account/api/admin/sponsors/{id}`, `DELETE /account/api/admin/sponsors/{id}`, `POST /account/api/admin/sponsors/export`, `POST /account/api/admin/sponsors/import` |
| **Source file** | [`SponsorsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SponsorsList.razor), [`SponsorsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SponsorsAddEdit.razor), [`SponsorsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SponsorsViewDelete.razor) |
| **Tests** | [`docs/tests/e2e/cp-admin-sponsors.md`](../../tests/e2e/cp-admin-sponsors.md); API: `tests/SIMF.Api.Tests/SponsorsTests.cs`, `tests/SIMF.Api.Tests/SponsorsExcelTests.cs` |
| **Last reviewed** | 2026-06-10 |

---

## 1. Purpose

The public website Sponsors screen (Mockup page 23) shows partner logos grouped
by tier — Platinum first, then Gold, Silver, Bronze, ordered by display order then
Arabic name within each tier. This Control Panel page is where an administrator
maintains that list: add a sponsor, set its tier, bilingual name, logo path, link
URL and display order, optionally link a shared Contact-directory record, toggle
the active flag, and soft-delete (deactivate) a sponsor so it drops off the public
screen. D-353 moved every form onto the uniform `CrudShell` (popup or full page,
per the admin's saved preference) and replaced the old inline modal + native
`confirm()` delete with a `SimfConfirm`-gated View/Delete form. D-356 added Excel
export and import so the list can be bulk-managed from a spreadsheet.

## 2. Audience + permissions

- **Who can reach it:** any admin whose role grants `Sponsors.View` (or the
  Administrator wildcard `"*"`). The page is gated by
  `@attribute [RequirePermission(PermissionCatalog.Sponsors.View)]`.
- **Who can edit/write on it:** the action buttons are **not** individually wrapped
  in `<AuthorizedAction>`, so any admin who can open the page sees Add / Edit /
  Delete / Export / Import. The finer-grained gate is enforced **API-side**:
  - Create → `Sponsors.Create`
  - Edit → `Sponsors.Edit`
  - Delete → `Sponsors.Delete`
  - Export → `Sponsors.Export`
  - Import → `Sponsors.Import`
- **Authorisation gates:** each API endpoint declares
  `Policies(PermissionCatalog.PolicyFor(<code>), nameof(AuthorizationPolicies.RequireApprovedAccount))`;
  the mutating + Excel endpoints also `RequireRateLimiting("auth")`.
- **What an unauthenticated / under-privileged user sees:** an admin lacking
  `Sponsors.View` is routed to `/not-permitted` and the `/list` call never fires;
  an admin with View but not (say) Create gets HTTP 403 on the underlying POST.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-sponsors-default.png` | _pending_ |
| Empty state | `docs/screenshots/cp-admin-sponsors-empty.png` | _pending_ |
| Add (popup) | `docs/screenshots/cp-admin-sponsors-add-modal.png` | _pending_ |
| Add (full page) | `docs/screenshots/cp-admin-sponsors-add-page.png` | _pending_ |
| View/Delete + SimfConfirm | `docs/screenshots/cp-admin-sponsors-delete-confirm.png` | _pending_ |
| Import result modal | `docs/screenshots/cp-admin-sponsors-import-result.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/cp-admin-sponsors-rtl.png` | _pending_ |

## 4. UI affordances

### 4.1 Banner / page header
`SimfBanner` with the title `Admin.Sponsors.Title` ("Sponsors" / "الرعاة"). The
banner + grid are wrapped in `simf-page-wide` / `simf-surface`. When a form is open
in **full-page** mode the banner + grid are hidden (`GridHidden`); in popup mode
they stay behind the dialog. A `SimfAlert` toast (success / error) renders above
the grid.

### 4.2 Toolbar (`SimfDataGrid`)
| Button | Wired callback | Calls | Notes |
|--------|----------------|-------|-------|
| Select all | grid `Multiselect="true"` | — | mandatory per the list-page standard |
| Add | `OnAddAsync` | opens `SponsorsAddEdit` (Create) in `CrudShell` | |
| Edit | `OnEditAsync` | GET `/{id}` then opens `SponsorsAddEdit` (Edit) | loads full detail first (summary omits `ContactId`) |
| Details | `OnDetailsAsync` | GET `/{id}` then opens `SponsorsViewDelete` read-only | `IsDelete=false`, no Delete button |
| Delete | `OnDeleteAsync` | GET `/{id}` then opens `SponsorsViewDelete` delete mode | `IsDelete=true`, Delete gated by `SimfConfirm` |
| Export | `OnExportAsync` | `POST /admin/sponsors/export` via `_excel.ExportAsync` | selected ids, else whole filtered grid |
| Import | `OnImportAsync` | `_excel.TriggerImportAsync()` → file picker → `POST /admin/sponsors/import` | insert-only |
| **Presentation toggle** | `CrudPresentationToggle` (`PageKey="sponsors"`) | persists to `localStorage` | Page ↔ Popup (D-353) |

`CrudGridExcel @ref="_excel" Resource="sponsors"` is rendered below the grid; it
owns the hidden file `<input id="sponsors-import-input" accept=".xlsx">`, fires
`OnImported` → `OnImportedAsync` (success toast + reload) and `OnError` →
`OnExcelError` (error toast).

### 4.3 Grid columns
| Column | Source field | Sortable | Filterable | Notes |
|--------|--------------|----------|------------|-------|
| Name (English) | `r.NameEn` | yes | yes | |
| Name (Arabic) | `r.NameAr` | yes | yes | |
| Tier | `r.TierName` | yes | no | Platinum / Gold / Silver / Bronze |
| Link | `r.Url` | no | no | "—" when blank |
| Display order | `r.DisplayOrder` | yes | no | |
| Active | `r.IsActive` | yes | no | `SimfPill` on/off (Active / Inactive) |

Empty list renders `SimfEmptyState` with `Admin.Sponsors.None`
("No sponsors yet." / "لا يوجد رعاة بعد.").

### 4.4 Pager
Standard `SimfDataGrid` pager — First / Prev / Next / Last + page-size selector,
caption "Showing X–Y of Z" (`Admin.Sponsors.Summary` via `FormatSummary`). Default
page size `Top = 20`.

### 4.5 Form fields (`SponsorsAddEdit`)
| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| Name (English) | text | yes | 256 | 1–256 chars (server `SponsorInvalid`) | `Admin.Sponsors.Field.NameEn` |
| Name (Arabic) | text | yes | 256 | 1–256 chars | `Admin.Sponsors.Field.NameAr` |
| Tier | select | yes | — | one of 10=Platinum / 20=Gold / 30=Silver / 40=Bronze (default Platinum) | `Admin.Sponsors.Field.Tier` |
| Link | text | no | 512 | ≤512 chars | `Admin.Sponsors.Field.Url` |
| Contact | `ContactPicker` | no | — | must be an existing active Contact (SIMF-FDS-014 / D-281) | — |
| Display order | number | yes | min 0, max 99999 | integer ≥ 0 | `Admin.Sponsors.Field.DisplayOrder` |
| Active | checkbox | Edit only | — | bool (Create always active) | `Admin.Sponsors.Field.IsActive` |

The form runs Create (`POST`) when `IsEdit=false` and Edit (`PUT` against
`Initial.Id`) when `IsEdit=true`; only Edit shows the Active checkbox. Blank
name(s) are guarded client-side before any request (`Admin.Sponsors.NameRequired`).

### 4.6 View / Delete form (`SponsorsViewDelete`)
Read-only `<dl>` of Name (En/Ar), Tier, Link, Display order, Active, plus a
`SimfImageThumb` of the sponsor's logo resolved by id.
In delete mode a red Delete button opens a `SimfConfirm` (Danger) whose message is
`Admin.Sponsors.Delete.Message` formatted with the sponsor's English name; only the
confirm fires `DELETE`. The old inline list `confirm()` was removed in D-353.

## 5. Data flow

```
Admin action → SponsorsList handler → JS interop (simfAccount.{post,get,put,delete}Json)
            → BFF /account/api/admin/sponsors/* → API /api/v1/admin/sponsors/*
            → IAdminSponsorService / Excel endpoints → SIMF_App DB
            → ApiResult<T> envelope → grid reload + toast
```

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| OnInit / query change | `POST /account/api/admin/sponsors/list` | `GridQuery` | `ApiResult<GridPage<AdminSponsorSummary>>` |
| Edit / Details / Delete click | `GET /account/api/admin/sponsors/{id}` | — | `ApiResult<AdminSponsorDetail>` |
| Add save | `POST /account/api/admin/sponsors` | `AdminCreateSponsorRequest` | `ApiResult<AdminSponsorDetail>` |
| Edit save | `PUT /account/api/admin/sponsors/{id}` | `AdminUpdateSponsorRequest` | `ApiResult<AdminSponsorDetail>` |
| Confirm delete | `DELETE /account/api/admin/sponsors/{id}` | — | `ApiResult<bool>` |
| Export | `POST /account/api/admin/sponsors/export` | `AdminGridExportRequest { Ids, Query }` | XLSX binary |
| Import | `POST /account/api/admin/sponsors/import` | multipart `file` (.xlsx) | `ApiResult<AdminGridImportResult>` |

Edit / Details / Delete always re-fetch the **detail** before opening a form
because the grid summary omits `ContactId` (SIMF-FDS-014 / D-283) — editing from a
summary-only model would wipe an existing contact link.

### 5.1 Excel export columns
`ExportSponsorsEndpoint` writes a sheet named **"Sponsors"** with header row
`NameEn | NameAr | Tier | Url | DisplayOrder | IsActive`. The logo column left the
workbook with D-889 — an image is a file, not a cell. Tier is
written by its **display name** (Platinum/Gold/Silver/Bronze) so the workbook
round-trips back through import. File name: `simf-sponsors-{yyyyMMddHHmmss}.xlsx`.
With selected rows the export honours `AdminGridExportRequest.Ids`; with none, it
exports the whole filtered set (`Query`). Capped at 5000 rows.

### 5.2 Excel import
`ImportSponsorsEndpoint` is **insert-only**. Required headers: `NameEn`, `NameAr`,
`Tier`. Tier is parsed from the display name (or the raw int 10/20/30/40); an
unknown tier raises a per-row error, not a batch abort. A row missing either name
raises a per-row error. `ContactId` cannot be expressed in plain text, so import
always leaves it unset — an admin links a contact afterwards via Edit. The result
`AdminGridImportResult { Created, Updated, Skipped, Errors[] }` drives the modal
("N created, M updated, K skipped" + per-row error list); the success toast is the
shared `Grid.Import.Done` key.

## 6. Validation + error handling

- **Client-side guards:** `SponsorsAddEdit.HandleSubmitAsync` blocks the request
  when Name (English) or Name (Arabic) is blank and shows `Admin.Sponsors.NameRequired`
  ("Both the English and Arabic names are required." / "الاسم بالإنجليزية والعربية مطلوبان.").
- **Server-side validation** (`AdminSponsorService.ValidateAndNormalise`): trims
  fields; NameEn/NameAr 1–256; Tier must be a defined `SponsorTier`;
  Url ≤512; DisplayOrder ≥ 0; an optional `ContactId` must point at an existing
  active Contact (else 400). All length/tier/order failures throw
  `ApiException(ErrorCodes.SponsorInvalid, 400, …)`.
- **Duplicate guard:** an **active** sponsor with the same Arabic name in the same
  tier → `ApiException(ErrorCodes.SponsorDuplicate, 409, …)`
  ("An active sponsor named '{name}' already exists in this tier." /
  "يوجد راعٍ نشط بالاسم '{name}' في هذه الفئة بالفعل."). The same Arabic name in a
  *different* tier, or against an inactive row, does not clash.
- **Not found:** `GET`/`PUT`/`DELETE` against a missing id → `SponsorNotFound` (404).
- **Import upload defence:** non-.xlsx (fails the ZIP-magic `50 4B 03 04` check) →
  400 ("The file is not a valid Excel workbook." / "الملف ليس مصنف Excel صالحًا.");
  file > 5 MB → 413 (`AdminImportEmpty`); wrong sheet name or missing required header
  → 400.
- **Error envelope:** standard `ApiResult<T>.Error` with `Code` + bilingual
  `Message`/`MessageArabic`; the forms surface `MessageForCurrentCulture()`.
- **Toast strategy:** success → `Admin.Sponsors.Saved` / `Admin.Sponsors.Deleted`
  (green) and `Grid.Import.Done` after import; load failure →
  `Admin.Sponsors.LoadFailed` ("Could not load sponsors. Please try again." /
  "تعذّر تحميل الرعاة. يُرجى المحاولة مرة أخرى."); form-level errors render in the
  form's `SimfAlert`.

## 7. Edge cases + known limitations

- **Soft-delete only.** `DELETE` deactivates (`IsActive=false`); the row stays in
  the grid (Active = "—") until an `isActive` filter excludes inactive rows, but
  drops off the public website list immediately.
- **Detail re-fetch before every form** so an existing `ContactId` is never lost
  when editing from the summary-only grid (SIMF-FDS-014 / D-283).
- **Tier is not a free enum on the public surface** — the four tiers are mirrored
  in the form dropdown and the import parser; both must stay aligned with
  `SIMF.Domain.Sponsors.SponsorTier`.
- **Import never sets `ContactId`** (a directory FK chosen with `ContactPicker`,
  not expressible as plain text); set it afterwards via Edit.
- **Import is insert-only** — there is no upsert, so re-importing a workbook with
  a duplicate active Arabic name in the same tier yields a per-row 409 error.
- **Action buttons are not `<AuthorizedAction>`-gated** — an admin with View but
  not Create/Edit/Delete/Export/Import sees the buttons, but the API rejects the
  call (403). Treat that as the per-action enforcement point.
- **EN/AR resx parity gap (known).** As of 2026-06-10 the English `Strings.resx`
  is missing nine D-353 keys that the Arabic `Strings.ar.resx` and the forms
  reference (`Admin.Sponsors.Delete.Message`, `Delete.Title`, `Details.Close`,
  `Details.Title`, `New.Submit`, `New.Submitting`, `Edit.Submit`, `Edit.Submitting`,
  `Fallback`) — those would render as the raw key name in the English UI. Fixing
  resx parity is outside this documentation changeset; flagged here for follow-up.

## 8. i18n + RTL

All visible strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR) via
`IStringLocalizer<Strings> L`. Banner title `الرعاة`; grid headers
"الاسم (إنجليزي)", "الاسم (عربي)", "الفئة", "مسار الشعار", "الرابط", "ترتيب العرض",
"نشط". Toggle: `العربية` / `English` in the top header sets `<html dir="rtl" lang="ar">`;
the nav rail mirrors, the toolbar + pager reverse, and the `CrudShell` form mirrors.
(See §7 for the current EN-key parity gap.)

## 9. Accessibility

- Keyboard: the `CrudShell` traps focus while a form is open and restores it on
  close; `SimfConfirm` requires an explicit Confirm/Cancel choice.
- Screen reader: `SimfDataGrid` exposes a `Caption` (`Admin.Sponsors.Title`) and
  per-row labels (`RowLabel = NameEn`); select-all / per-row select have labels.
- Colour contrast: WCAG AA via `theme.tokens.css`; active/inactive use the
  `SimfPill` on/off variants, not colour alone (text "Active"/"Inactive").
- Focus indicators: the `--focus-ring` token on every focusable element.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| (D-199 Sponsors) | Maintain public sponsors list | Mockup page 23 / SIMF-FDS-004; UCS detail entry to be authored |

## 11. Related E2E test scenarios

| Scenario | File | Coverage |
|----------|------|----------|
| Full CRUD round-trip | [`cp-admin-sponsors.md`](../../tests/e2e/cp-admin-sponsors.md) | E2E-SPN-001 |
| Create / Edit / Tier+ordering | same | E2E-SPN-002/003/006 |
| Delete confirm + cancel (SimfConfirm) | same | E2E-SPN-004/005, E2E-SPN-020 |
| Empty / auth (page + action) | same | E2E-SPN-007/008/009 |
| Validation / duplicate / server-500 | same | E2E-SPN-010/011/012/013 |
| RTL + filter + sort | same | E2E-SPN-015/016/017 |
| Presentation toggle persists (D-353) | same | E2E-SPN-018 |
| Full-page round-trip (D-353) | same | E2E-SPN-019 |
| Excel export (D-356) | same | E2E-SPN-021 |
| Excel import + import rejection (D-356) | same | E2E-SPN-022/023 |

## 12. Related docs

- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) (CRUD pages).
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `ApiResult<T>` envelope + admin grid endpoints.
- Decisions log: D-199 (Sponsors module), D-281/D-283 (shared Contact link),
  D-353 (uniform CrudShell + Page/Popup toggle + SimfConfirm delete), D-356 (Excel
  export/import) in [`DECISIONS_LOG.md`](../../decisions/DECISIONS_LOG.md).
- Source: `SponsorsList.razor`, `SponsorsAddEdit.razor`, `SponsorsViewDelete.razor`,
  `SponsorEndpoints.cs`, `SponsorsExcelEndpoints.cs`, `AdminSponsorService.cs`.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-08-14 | D-889 | **The free-text Logo path is gone; the sponsor's logo is a typed key.** `Sponsor.LogoRelativePath` becomes `Guid? LogoFileId` with a real foreign key into `StoredFiles`. The Add/Edit form loses the text field and gains the save-first hint on create; the workbook loses its logo column; `AssetService` points `LogoFileId` at the sponsor's active file on every asset transition. |
| 2026-06-11 | D-357 | Sponsor **logo** wired to the unified media-asset pipeline: `SimfImageUpload Category="SponsorLogo"` on the Add/Edit form (edit mode only — the sponsor row must exist before bytes can attach) + a `SimfImageThumb` of `/account/api/admin/assets/SponsorLogo/{id}/image` on Details/Deactivate (complements the existing free-text `LogoRelativePath` field). E2E catalogue extended with E2E-SPN-024. |
| 2026-06-10 | D-356 / D-353 | Reference doc created. Documents the D-353 `CrudShell` Add/Edit + View/Delete forms with the Page ↔ Popup `CrudPresentationToggle` (PageKey `sponsors`) and the `SimfConfirm`-gated delete (replacing the old inline modal + native `confirm()`), plus the D-356 Excel export (`POST /export`) and insert-only import (`POST /import`) via `CrudGridExcel`. |
| 2026-06-02 (orig) | D-199 | Sponsors admin CRUD shipped (Mockup page 23). |

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5).
