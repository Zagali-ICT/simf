# Media Partners — `/admin/media-partners`

| | |
|--|--|
| **Route** | `/admin/media-partners` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator (admins holding the `MediaPartners.*` permissions) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.MediaPartners.View)]` (page) + per-action API policies (`MediaPartners.Create` / `Edit` / `Delete` / `Export` / `Import`) + `RequireApprovedAccount` |
| **Pattern** | D-199 event-module CRUD on the uniform CRUD shell — **D-353** `CrudShell` Add/Edit + View/Delete with the Page ↔ Popup `CrudPresentationToggle`, **D-356** Excel export + import via `CrudGridExcel`, **D-357** logo via the unified media-asset pipeline |
| **Status** | ✅ Real (D-199; D-353 toggle/CrudShell + D-356 Excel; D-357 media-asset logo, 2026-06-11) |
| **Implements use case(s)** | Admin maintenance of the public Media Partners screen (Mockup page 31 — "شركاء النجاح") per SIMF-FDS-004 / D-199 |
| **Backend endpoints** | `POST /account/api/admin/media-partners/list`, `GET /account/api/admin/media-partners/{id}`, `POST /account/api/admin/media-partners`, `PUT /account/api/admin/media-partners/{id}`, `DELETE /account/api/admin/media-partners/{id}`, `POST /account/api/admin/media-partners/export`, `POST /account/api/admin/media-partners/import` |
| **Source file** | [`MediaPartnersList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/MediaPartnersList.razor), [`MediaPartnerAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/MediaPartnerAddEdit.razor), [`MediaPartnerViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/MediaPartnerViewDelete.razor) |
| **Tests** | [`docs/tests/e2e/cp-admin-media-partners.md`](../../tests/e2e/cp-admin-media-partners.md); API: `tests/SIMF.Api.Tests/MediaPartnersTests.cs`, `tests/SIMF.Api.Tests/AdminMediaPartnersTests.cs`, `tests/SIMF.Api.Tests/MediaPartnersExcelTests.cs` |
| **Last reviewed** | 2026-06-11 |

---

## 1. Purpose

The public Media Partners screen (Mockup page 31 — "شركاء النجاح" / "Success
Partners") lists partner logos ordered by display order then Arabic name, active
rows only. This Control Panel page is where an administrator maintains that list:
add a media partner, set its bilingual name (English + Arabic), logo path, link
URL and display order, optionally link a shared Contact-directory record, toggle
the active flag, and soft-delete (deactivate) a media partner so it drops off the
public screen. D-353 moved every form onto the uniform `CrudShell` (popup or full
page, per the admin's saved preference) and replaced the old inline `SimfModal`
form + native `confirm()` delete with a `SimfConfirm`-gated View/Delete form.
D-356 added Excel export and import so the list can be bulk-managed from a
spreadsheet. D-357 wired a logo image (asset category `MediaPartnerLogo`) onto the
Add/Edit and Details/Delete forms through the unified media-asset pipeline.

## 2. Audience + permissions

- **Who can reach it:** any admin whose role grants `MediaPartners.View` (or the
  Administrator wildcard `"*"`). The page is gated by
  `@attribute [RequirePermission(PermissionCatalog.MediaPartners.View)]`.
- **Who can edit/write on it:** the toolbar action buttons are **not** individually
  wrapped in `<AuthorizedAction>`, so any admin who can open the page sees Add /
  Edit / Delete / Export / Import. The finer-grained gate is enforced **API-side**:
  - List / Get → `MediaPartners.View`
  - Create → `MediaPartners.Create`
  - Edit → `MediaPartners.Edit`
  - Delete (deactivate) → `MediaPartners.Delete`
  - Export → `MediaPartners.Export`
  - Import → `MediaPartners.Import`
- **Authorisation gates:** each API endpoint declares
  `Policies(PermissionCatalog.PolicyFor(<code>), nameof(AuthorizationPolicies.RequireApprovedAccount))`;
  the mutating endpoints (Create / Update / Deactivate) also
  `Options(rb => rb.RequireRateLimiting("auth"))`.
- **What an unauthenticated / under-privileged user sees:** an admin lacking
  `MediaPartners.View` is routed to `/not-permitted` and the `/list` call never
  fires (the `Module.MediaPartners` nav item is also hidden — its
  `RequiredPermission` is `MediaPartners.View`); an admin with View but not (say)
  Create gets HTTP 403 on the underlying POST.

| Action | Permission code | Endpoint policy declared |
|--------|-----------------|--------------------------|
| Open page / List / Get | `MediaPartners.View` | `PolicyFor(MediaPartners.View)` (list + get-by-id) |
| Create | `MediaPartners.Create` | `PolicyFor(MediaPartners.Create)` |
| Edit | `MediaPartners.Edit` | `PolicyFor(MediaPartners.Edit)` |
| Delete (deactivate) | `MediaPartners.Delete` | `PolicyFor(MediaPartners.Delete)` |
| Export | `MediaPartners.Export` | `PolicyFor(MediaPartners.Export)` |
| Import | `MediaPartners.Import` | `PolicyFor(MediaPartners.Import)` |

All six codes are seeded `AdminOnly` in `PermissionCatalog.All`.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-media-partners-default.png` | _pending_ |
| Empty state | `docs/screenshots/cp-admin-media-partners-empty.png` | _pending_ |
| Add (popup) | `docs/screenshots/cp-admin-media-partners-add-modal.png` | _pending_ |
| Add (full page) | `docs/screenshots/cp-admin-media-partners-add-page.png` | _pending_ |
| View/Delete + SimfConfirm | `docs/screenshots/cp-admin-media-partners-delete-confirm.png` | _pending_ |
| Import result modal | `docs/screenshots/cp-admin-media-partners-import-result.png` | _pending_ |
| Logo upload (media-asset) | `docs/screenshots/cp-admin-media-partners-logo-upload.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/cp-admin-media-partners-rtl.png` | _pending_ |

## 4. UI affordances

### 4.1 Banner / page header
`SimfBanner` with the title `Admin.MediaPartners.Title` ("Media Partners" /
"شركاء النجاح"). The banner + grid are wrapped in `simf-page-wide` /
`simf-surface`. When a form is open in **full-page** mode the banner + grid are
hidden (`GridHidden`); in popup mode they stay behind the dialog. A `SimfAlert`
toast (success / error) renders above the grid.

### 4.2 Toolbar (`SimfDataGrid`)
| Button | Wired callback | Calls | Notes |
|--------|----------------|-------|-------|
| Select all | grid `Multiselect="true"` | — | select-all + per-row checkboxes present |
| Add | `OnAddAsync` | opens `MediaPartnerAddEdit` (Create) in `CrudShell` | |
| Edit | `OnEditAsync` | GET `/{id}` then opens `MediaPartnerAddEdit` (Edit) | loads full detail first (summary omits `ContactId`) |
| Details | `OnDetailsAsync` | GET `/{id}` then opens `MediaPartnerViewDelete` read-only | `IsDelete=false`, no Delete button |
| Delete | `OnDeleteAsync` | GET `/{id}` then opens `MediaPartnerViewDelete` delete mode | `IsDelete=true`, Delete gated by `SimfConfirm` |
| Export | `OnExportAsync` | `POST /admin/media-partners/export` via `_excel.ExportAsync` | selected ids, else whole filtered grid |
| Import | `OnImportAsync` | `_excel.TriggerImportAsync()` → file picker → `POST /admin/media-partners/import` | insert-only |
| **Presentation toggle** | `CrudPresentationToggle` (`PageKey="media-partners"`) | persists to `localStorage` | Page ↔ Popup (D-353) |

`CrudGridExcel @ref="_excel" Resource="media-partners"` is rendered below the
grid; it fires `OnImported` → `OnImportedAsync` (success toast + reload) and
`OnError` → `OnExcelError` (error toast).

### 4.3 Grid columns
| Column | Source field | Key | Sortable | Filterable | Notes |
|--------|--------------|-----|----------|------------|-------|
| Name (English) | `r.Name` | `name` | yes | yes | |
| Name (Arabic) | `r.NameArabic` | `namearabic` | yes | yes | |
| Logo path | `r.LogoRelativePath` | `logo` | no | no | "—" when blank |
| Link | `r.Url` | `url` | no | no | "—" when blank |
| Display order | `r.DisplayOrder` | `displayorder` | yes | no | |
| Active | `r.IsActive` | `isActive` | no | no | `SimfPill` on/off (Active / Inactive) |

> The backend `AdminMediaPartnerService.ListAllAsync` honours per-column filters
> on `name` and `namearabic` (case-sensitive `Contains`) plus an `isactive`
> bool filter, and sort on `name` / `namearabic` / `displayorder` (default sort:
> `DisplayOrder` asc then `NameArabic` asc). The grid only exposes filter inputs
> on the two name columns and sort on `name` / `namearabic` / `displayorder`.

Empty list renders `SimfEmptyState` with `Admin.MediaPartners.None`
("No media partners yet." / "لا يوجد شركاء إعلاميون بعد.").

### 4.4 Pager
Standard `SimfDataGrid` pager — First / Prev / Next / Last + page-size selector,
caption "Showing X–Y of Z" (`Admin.MediaPartners.Summary` via `FormatSummary`).
Default page size `Top = 20`.

### 4.5 Form fields (`MediaPartnerAddEdit`)
| Field | Type | Required | MaxLength (UI) | Validation (server) | Locale |
|-------|------|----------|----------------|---------------------|--------|
| Name (English) | text | yes | 256 | 1–256 chars (`VALIDATION_FAILED`) | `Admin.MediaPartners.Field.NameEn` |
| Name (Arabic) | text | yes | 256 | 1–256 chars (`VALIDATION_FAILED`) | `Admin.MediaPartners.Field.NameAr` |
| Logo path | text | no | 512 | ≤512 chars | `Admin.MediaPartners.Field.Logo` |
| Link | text | no | 512 | ≤512 chars | `Admin.MediaPartners.Field.Url` |
| Contact | `ContactPicker` | no | — | must be an existing active Contact (SIMF-FDS-014 / D-281) | — |
| Display order | number | — | — | parsed; non-integer / negative coerced to 0 client-side; server requires ≥ 0 | `Admin.MediaPartners.Field.DisplayOrder` |
| Active | checkbox | Edit only | — | bool (Create always persists `IsActive=true`) | `Admin.MediaPartners.Field.IsActive` |
| Image (logo) | `SimfImageUpload` | Edit only | — | media-asset pipeline (see §4.7) | `Admin.Asset.Heading` ("Image") |

The form runs Create (`POST`) when `IsEdit=false` and Edit (`PUT` against
`Initial.Id`) when `IsEdit=true`; only Edit shows the Active checkbox and the
Image upload. Blank name(s) are guarded client-side before any request
(`Admin.MediaPartners.NameRequired`). The display-order textbox is a free-text
number input — a non-integer or negative value is silently coerced to `0` in
`HandleSubmitAsync` (`int.TryParse(... ) || order < 0 ⇒ 0`).

### 4.6 View / Delete form (`MediaPartnerViewDelete`)
A logo thumbnail (see §4.7) followed by a read-only `<dl>` of Name (En/Ar), Logo
path, Link, Display order, Active. In delete mode a red Delete button opens a
`SimfConfirm` (Danger) whose message is `Admin.MediaPartners.Delete.Message`
formatted with the partner's English name; only the confirm fires `DELETE`. The
old inline list `confirm()` was removed in D-353.

### 4.7 Logo image — unified media-asset pipeline (D-357)
- **Add/Edit form (Edit only):** when `IsEdit && Initial is not null`, the form
  renders a labelled `SimfImageUpload`:
  `<SimfImageUpload Category="MediaPartnerLogo" OwnerId="@Initial.Id" Alt="@_model.Name" />`
  under the heading `Admin.Asset.Heading` ("Image"). It is Edit-only because the
  media-partner row must exist (have an `Id`) before image bytes / a link can be
  attached to it.
- **Details / Delete form:** renders a thumbnail of the same asset:
  `<SimfImageThumb Src="/account/api/admin/assets/MediaPartnerLogo/{Initial.Id}/image" Alt="@Initial.Name" Class="simf-img-thumb--lg" />`
  inside a `simf-image-upload__preview` wrapper.
- The asset category is **`MediaPartnerLogo`**, and the upload / link / proxy /
  Media-Library behaviour is the shared media-asset pipeline (see the media-asset
  dev guide). This image control is **additive** to — and independent of — the
  free-text `LogoRelativePath` field, which remains the value shown in the grid's
  "Logo path" column.

## 5. Data flow

```
Admin action → MediaPartnersList handler → JS interop (simfAccount.{post,get,put,delete}Json)
            → BFF /account/api/admin/media-partners/* → API /api/v1/admin/media-partners/*
            → IAdminMediaPartnerService / Excel endpoints → SIMF_App DB
            → ApiResult<T> envelope → grid reload + toast
```

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| OnInit / query change | `POST /account/api/admin/media-partners/list` | `GridQuery` | `ApiResult<GridPage<AdminMediaPartnerSummary>>` |
| Edit / Details / Delete click | `GET /account/api/admin/media-partners/{id}` | — | `ApiResult<AdminMediaPartnerDetail>` |
| Add save | `POST /account/api/admin/media-partners` | `AdminCreateMediaPartnerRequest` | `ApiResult<AdminMediaPartnerDetail>` |
| Edit save | `PUT /account/api/admin/media-partners/{id}` | `AdminUpdateMediaPartnerRequest` | `ApiResult<AdminMediaPartnerDetail>` |
| Confirm delete | `DELETE /account/api/admin/media-partners/{id}` | — | `ApiResult<bool>` |
| Export | `POST /account/api/admin/media-partners/export` | `AdminGridExportRequest { Ids, Query }` | XLSX binary |
| Import | `POST /account/api/admin/media-partners/import` | multipart `file` (.xlsx) | `ApiResult<AdminGridImportResult>` |

Edit / Details / Delete always re-fetch the **detail** before opening a form
because the grid summary (`AdminMediaPartnerSummary`) omits `ContactId`
(SIMF-FDS-014 / D-283) — editing from a summary-only model would wipe an existing
contact link. The `Create` request omits `IsActive` entirely (the server always
sets `IsActive=true` on create); only `Update` carries `IsActive`.

### 5.1 Excel export columns
`ExportMediaPartnersEndpoint` writes a sheet named **"MediaPartners"** with header
row `Name | NameArabic | LogoRelativePath | Url | DisplayOrder | IsActive`. File
name prefix: `simf-media-partners`. With selected rows the export honours
`AdminGridExportRequest.Ids`; with none, it exports the whole filtered set
(`Query`). Export is capped at 5000 rows.

### 5.2 Excel import
`ImportMediaPartnersEndpoint` is **insert-only** and reads the sheet named
**"MediaPartners"**. Required headers: `Name`, `NameArabic`. A row missing the
English name raises a per-row `DataValidationException` ("The English name is
required." / "الاسم بالإنجليزية مطلوب."); a row missing the Arabic name raises a
per-row error ("The Arabic name is required." / "الاسم بالعربية مطلوب."). Optional
`LogoRelativePath` and `Url` are taken verbatim (blank ⇒ null); `DisplayOrder` is
parsed (unparseable ⇒ 0). A duplicate English name surfaces as a per-row error
(the service throws `MEDIA_PARTNER_NAME_DUPLICATE`), not a batch abort. `ContactId`
cannot be expressed in plain text, so import always leaves it unset — an admin
links a contact afterwards via Edit. The result `AdminGridImportResult`
drives the modal ("N created, M updated, K skipped" + per-row error list); the
success toast is the shared `Grid.Import.Done` key.

## 6. Validation + error handling

- **Client-side guards:** `MediaPartnerAddEdit.HandleSubmitAsync` blocks the
  request when Name (English) or Name (Arabic) is blank and shows
  `Admin.MediaPartners.NameRequired` ("Both the English and Arabic names are
  required." / "الاسم بالإنجليزية والعربية مطلوبان."). The display-order field
  coerces a bad value to `0` rather than erroring.
- **Server-side validation** (`AdminMediaPartnerService.Validate`): trims fields;
  NameEn 1–256; NameAr 1–256; LogoRelativePath ≤512; Url ≤512; DisplayOrder ≥ 0.
  Each failure throws `ApiException(ErrorCodes.ValidationFailed, 400, …)`
  (`VALIDATION_FAILED`) with a field-specific bilingual message — e.g. "Media
  partner English name must be between 1 and 256 characters." / "يجب أن يتراوح
  الاسم الإنجليزي للشريك الإعلامي بين 1 و 256 حرفاً."
- **Contact link guard:** an optional `ContactId` must point at an existing
  **active** `Contact`, else `VALIDATION_FAILED` (400) — "Contact id '…' does not
  exist or is inactive." / "جهة الاتصال '…' غير موجودة أو غير مفعّلة."
  (SIMF-FDS-014 / D-281).
- **Duplicate guard:** any media partner with the same English name
  (case-insensitive on update; exact-match `Any` on create) →
  `ApiException(ErrorCodes.MediaPartnerNameDuplicate, 409, …)`
  (`MEDIA_PARTNER_NAME_DUPLICATE`) — "A media partner named '{name}' already
  exists." / "يوجد شريك إعلامي بالاسم '{name}' بالفعل." On update the clash check
  only runs when the name actually changes (case-insensitive comparison) and
  excludes the row itself.
- **Not found:** `GET`/`PUT`/`DELETE` against a missing id → the generic
  `ErrorCodes.NotFound` (`NOT_FOUND`, 404) — "The media partner was not found." /
  "لم يتم العثور على الشريك الإعلامي." (there is no dedicated `MEDIA_PARTNER_NOT_FOUND`
  code; the generic `NotFound` is used both at the endpoint and in the service).
- **Import upload defence:** handled by the shared `AdminGridImportEndpoint` base
  — a non-.xlsx upload (fails the ZIP-magic check), an over-size file, or a wrong
  sheet name / missing required header returns HTTP 400; the page surfaces it via
  `CrudGridExcel OnError → OnExcelError`.
- **Error envelope:** standard `ApiResult<T>.Error` with `Code` + bilingual
  `Message`/`MessageArabic`; the forms surface `MessageForCurrentCulture()`.
- **Toast strategy:** success → `Admin.MediaPartners.Saved`
  ("Media partner saved.") / `Admin.MediaPartners.Deleted` ("Media partner
  deleted.") (green) and `Grid.Import.Done` after import; load failure →
  `Admin.MediaPartners.LoadFailed` ("Could not load media partners. Please try
  again." / "تعذّر تحميل الشركاء الإعلاميين. يُرجى المحاولة مرة أخرى."); form-level
  errors render in the form's `SimfAlert`.

## 7. Edge cases + known limitations

- **Soft-delete only.** `DELETE` deactivates (`IsActive=false`); the row stays in
  the admin grid (Active pill = "Inactive") because the admin list is unfiltered,
  but drops off the public website list immediately. Deactivating an already
  inactive row is a no-op (the service returns early, no audit row written).
- **Detail re-fetch before every form** so an existing `ContactId` is never lost
  when editing from the summary-only grid (SIMF-FDS-014 / D-283).
- **Create never carries `IsActive`** — the Add form shows no Active checkbox and
  the create request always persists `IsActive=true`; the Active checkbox is
  Edit-only.
- **Duplicate detection is on the English name only** (a media partner has no
  separate business code). The same English name in a different case still clashes
  on create; the Arabic name is not part of the uniqueness rule.
- **Import never sets `ContactId`** (a directory FK chosen with `ContactPicker`,
  not expressible as plain text); set it afterwards via Edit.
- **Import is insert-only** — there is no upsert, so re-importing a workbook with a
  duplicate English name yields a per-row 409 error.
- **The logo Image control is Edit-only** (D-357) — a brand-new media partner has
  no `Id` until the first save, so the `SimfImageUpload` only appears after the
  row exists; on the Add (create) form there is no image control, only the
  free-text `Logo path` field.
- **Action buttons are not `<AuthorizedAction>`-gated** — an admin with View but
  not Create/Edit/Delete/Export/Import sees the buttons, but the API rejects the
  call (403). Treat that as the per-action enforcement point.

## 8. i18n + RTL

All visible strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR) via
`IStringLocalizer<Strings> L`. Banner title `شركاء النجاح`; grid headers
"الاسم (إنجليزي)", "الاسم (عربي)", "مسار الشعار", "الرابط", "ترتيب العرض", "نشط".
Toolbar Add button `إضافة شريك إعلامي`. Toggle: `العربية` / `English` in the top
header sets `<html dir="rtl" lang="ar">`; the nav rail mirrors, the toolbar +
pager reverse, and the `CrudShell` form mirrors. The D-353 form keys
(`Admin.MediaPartners.Details.Title`/`.Close`, `Delete.Title`/`.Message`) and the
asset heading (`Admin.Asset.Heading`) are present in both EN and AR resx.

## 9. Accessibility

- Keyboard: the `CrudShell` traps focus while a form is open and restores it on
  close; `SimfConfirm` requires an explicit Confirm/Cancel choice.
- Screen reader: `SimfDataGrid` exposes a `Caption` (`Admin.MediaPartners.Title`)
  and per-row labels (`RowLabel = Name`); select-all / per-row select have labels.
  The `SimfImageUpload` / `SimfImageThumb` carry `Alt` text (the partner name).
- Colour contrast: WCAG AA via `theme.tokens.css`; active/inactive use the
  `SimfPill` on/off variants, not colour alone (text "Active"/"Inactive").
- Focus indicators: the `--focus-ring` token on every focusable element.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| (D-199 Media Partners) | Maintain public media-partner list | Mockup page 31 / SIMF-FDS-004; UCS detail entry to be authored |

## 11. Related E2E test scenarios

| Scenario | File | Coverage |
|----------|------|----------|
| Full CRUD round-trip (Add → Edit → Deactivate) | [`cp-admin-media-partners.md`](../../tests/e2e/cp-admin-media-partners.md) | E2E-MPR-001 |
| Add optional fields / toggle Active | same | E2E-MPR-002/003 |
| Delete confirm cancelled / empty / auth gate | same | E2E-MPR-004/005/006 |
| Client + server validation / duplicate / 500 | same | E2E-MPR-007/008/009/010 |
| RTL + per-column filter + sort | same | E2E-MPR-011/012/013 |
| Presentation toggle persists (D-353) | same | E2E-MPR-014 |
| Full-page round-trip (D-353) | same | E2E-MPR-015 |
| Delete confirmation gate — SimfConfirm (D-353) | same | E2E-MPR-016 |
| Excel export (D-356) | same | E2E-MPR-017 |
| Excel import + import rejection (D-356) | same | E2E-MPR-018/019 |
| Logo via the unified media-asset pipeline (D-357) | same | E2E-MPR-020 |

## 12. Related docs

- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) (CRUD pages).
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `ApiResult<T>` envelope + admin grid endpoints.
- Decisions log: D-199 (Media Partners module), D-281/D-283 (shared Contact link),
  D-353 (uniform CrudShell + Page/Popup toggle + SimfConfirm delete), D-356 (Excel
  export/import), D-357 (unified media-asset pipeline) in
  [`DECISIONS_LOG.md`](../../decisions/DECISIONS_LOG.md).
- Source: `MediaPartnersList.razor`, `MediaPartnerAddEdit.razor`,
  `MediaPartnerViewDelete.razor`, `MediaPartnerEndpoints.cs`,
  `MediaPartnersExcelEndpoints.cs`, `AdminMediaPartnerService.cs`.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-11 | D-357 | Media-partner **logo** wired to the unified media-asset pipeline: `SimfImageUpload Category="MediaPartnerLogo"` (Edit-only) on `MediaPartnerAddEdit` + a `SimfImageThumb` of `/account/api/admin/assets/MediaPartnerLogo/{id}/image` on `MediaPartnerViewDelete` (complements the existing free-text `LogoRelativePath` field). E2E catalogue extended with E2E-MPR-020. |
| 2026-06-10 | D-356 / D-353 | Reference doc created. Documents the D-353 `CrudShell` Add/Edit + View/Delete forms with the Page ↔ Popup `CrudPresentationToggle` (PageKey `media-partners`) and the `SimfConfirm`-gated delete (replacing the old inline `SimfModal` + native `confirm()`), plus the D-356 Excel export (`POST /export`) and insert-only import (`POST /import`) via `CrudGridExcel`. |
| 2026-06-02 (orig) | D-199 | Media Partners admin CRUD shipped (Mockup page 31 — "شركاء النجاح"). |

---

_Last reviewed:_ 2026-06-11 by Claude (D-357 media-asset logo).
