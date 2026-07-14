# Contacts — `/admin/contacts`

| | |
|--|--|
| **Route** | `/admin/contacts` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator (admins holding the `Contacts.*` permissions) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Contacts.View)]` (page) + per-action API policies (`Contacts.View` for read, `Contacts.Edit` for create/update/deactivate, `Contacts.Export` / `Contacts.Import` for Excel) + `RequireApprovedAccount` |
| **Pattern** | SIMF-FDS-014 shared Contact directory (D-281 / Slice C2) on the uniform CRUD shell — **D-353** `CrudShell` Add/Edit + View/Delete with the Page ↔ Popup `CrudPresentationToggle`, **D-356** Excel export + import via `CrudGridExcel` |
| **Status** | ✅ Real (D-261/D-281; D-353 toggle/CrudShell + D-356 Excel; D-357 media-asset logo, 2026-06-11) |
| **Implements use case(s)** | Admin maintenance of the de-duplicated Contact/party directory reused by Sponsors / Exhibitors / MediaPartners / Speakers / Booth officers via a nullable `ContactId` (SIMF-FDS-014 / D-281) |
| **Backend endpoints** | `POST /account/api/admin/contacts/list`, `GET /account/api/admin/contacts/{id}`, `POST /account/api/admin/contacts`, `PUT /account/api/admin/contacts/{id}`, `DELETE /account/api/admin/contacts/{id}`, `GET /account/api/admin/contacts/picker`, `POST /account/api/admin/contacts/export`, `POST /account/api/admin/contacts/import` |
| **Source file** | [`ContactsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContactsList.razor), [`ContactsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContactsAddEdit.razor), [`ContactsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContactsViewDelete.razor) |
| **Tests** | [`docs/tests/e2e/cp-admin-contacts.md`](../../tests/e2e/cp-admin-contacts.md); API: `tests/SIMF.Api.Tests/ContactsTests.cs`, `tests/SIMF.Api.Tests/ContactsExcelTests.cs` |
| **Last reviewed** | 2026-06-11 |

---

## 1. Purpose

SIMF maintains one shared, de-duplicated **Contact** directory (SIMF-FDS-014,
D-281 / Slice C2): a single party record — logo, bilingual name, two phones,
email, website, four social links, map latitude/longitude and country — that the
Sponsor, Exhibitor, MediaPartner, Speaker and Booth-officer admin forms each link
to through a nullable `ContactId` instead of duplicating the same contact details.
This Control Panel page is where an administrator maintains that directory: add a
contact, edit any of its fields, search the grid, link a country, optionally attach
a logo image, and soft-delete (deactivate) a contact so it drops out of the link
picker. D-353 moved every form onto the uniform `CrudShell` (popup or full page,
per the admin's saved preference) and replaced the old inline `SimfModal` form +
native `confirm()` delete with a `SimfConfirm`-gated View/Delete form. D-356 added
Excel export and insert-only import. D-357 wired the contact **logo** to the unified
media-asset pipeline (asset category `CompanyLogo`).

## 2. Audience + permissions

- **Who can reach it:** any admin whose role grants `Contacts.View` (or the
  Administrator wildcard `"*"`). The page is gated by
  `@attribute [RequirePermission(PermissionCatalog.Contacts.View)]`.
- **Who can edit/write on it:** the action buttons are **not** individually wrapped
  in `<AuthorizedAction>`, so any admin who can open the page sees Add / Edit /
  Details / Delete / Export / Import. The finer-grained gate is enforced
  **API-side**. The catalogue deliberately defines **only** four codes —
  `Contacts.View`, `Contacts.Edit`, `Contacts.Export`, `Contacts.Import` (there is
  **no** `Contacts.Create` / `Contacts.Delete`). Create, update **and** soft-delete
  therefore all map onto the single write code `Contacts.Edit`:
  - List / Get by id / Picker → `Contacts.View`
  - Create (`POST`) → `Contacts.Edit`
  - Update (`PUT`) → `Contacts.Edit`
  - Deactivate (`DELETE`) → `Contacts.Edit`
  - Export → `Contacts.Export`
  - Import → `Contacts.Import`
- **Authorisation gates:** each API endpoint declares
  `Policies(PermissionCatalog.PolicyFor(<code>), nameof(AuthorizationPolicies.RequireApprovedAccount))`.
  The mutating endpoints (`POST` / `PUT` / `DELETE`) and the Excel endpoints also
  `RequireRateLimiting("auth")`; the read endpoints (`list` / `{id}` / `picker`) do not.
- **Navigation:** the side-nav item is `Module.Contacts` → `/admin/contacts`
  (icon `phone`) with `RequiredPermission: PermissionCatalog.Contacts.View`.
- **What an unauthenticated / under-privileged user sees:** an admin lacking
  `Contacts.View` is routed to `/not-permitted` (and the `Contacts` nav item is
  absent), so the `/list` call never fires; an admin with View but not `Contacts.Edit`
  gets HTTP 403 on the underlying create / update / delete POST/PUT/DELETE.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-contacts-default.png` | _pending_ |
| Empty state | `docs/screenshots/cp-admin-contacts-empty.png` | _pending_ |
| Add (popup) | `docs/screenshots/cp-admin-contacts-add-modal.png` | _pending_ |
| Add (full page) | `docs/screenshots/cp-admin-contacts-add-page.png` | _pending_ |
| View/Delete + SimfConfirm | `docs/screenshots/cp-admin-contacts-delete-confirm.png` | _pending_ |
| Conflict (CONTACT_IN_USE 409) | `docs/screenshots/cp-admin-contacts-in-use.png` | _pending_ |
| Import result modal | `docs/screenshots/cp-admin-contacts-import-result.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/cp-admin-contacts-rtl.png` | _pending_ |

## 4. UI affordances

### 4.1 Banner / page header
`SimfBanner` with the title `Admin.Contacts.Title` ("Contacts" / "جهات الاتصال").
The banner + grid are wrapped in `simf-page-wide` / `simf-surface`. When a form is
open in **full-page** mode the banner + grid are hidden (`GridHidden`); in popup
mode they stay behind the dialog. A `SimfAlert` toast (`success` / `error`) renders
above the toolbar.

### 4.2 Toolbar
A search box (above the grid) plus the standard `SimfDataGrid` action buttons.

| Control | Wired callback | Calls | Notes |
|---------|----------------|-------|-------|
| Search field + Search button | `ApplySearchAsync` | `POST /admin/contacts/list` with `GridQuery.Search` | resets `Skip` to 0; `null` when blank; label `Admin.Contacts.Search`, placeholder `Admin.Contacts.Search.Placeholder` ("Search by name, phone or email") |
| Select all | grid `Multiselect="true"` | — | mandatory per the list-page standard; `RowKey = r.Id`, `RowLabel = r.NameAr` |
| Add | `OnAddAsync` | opens `ContactsAddEdit` (Create) in `CrudShell` | `_target = null` |
| Edit | `OnEditAsync` | GET `/{id}` then opens `ContactsAddEdit` (Edit) | loads full detail first (summary omits logo/social/map) |
| Details | `OnDetailsAsync` | GET `/{id}` then opens `ContactsViewDelete` read-only | `IsDelete=false`, no Deactivate button |
| Delete | `OnDeleteAsync` | GET `/{id}` then opens `ContactsViewDelete` delete mode | `IsDelete=true`, Deactivate gated by `SimfConfirm` |
| Export | `OnExportAsync` | `POST /admin/contacts/export` via `_excel.ExportAsync` | selected ids, else whole filtered grid |
| Import | `OnImportAsync` | `_excel.TriggerImportAsync()` → file picker → `POST /admin/contacts/import` | insert-only |
| **Presentation toggle** | `CrudPresentationToggle` (`PageKey="contacts"`) | persists via `CpPreferences` to `localStorage` | Page ↔ Popup (D-353) |

`CrudGridExcel @ref="_excel" Resource="contacts"` is rendered below the grid; it
owns the hidden file `<input id="contacts-import-input" accept=".xlsx">`, fires
`OnImported` → `OnImportedAsync` (success toast `Grid.Import.Done` + reload) and
`OnError` → `OnExcelError` (error toast).

### 4.3 Grid columns
| Column | Source field | Sortable | Filterable | Notes |
|--------|--------------|----------|------------|-------|
| Name (Arabic) | `r.NameAr` | yes | yes | header `Admin.Contacts.Col.NameAr` |
| Name (English) | `r.NameEn` | no | yes | "—" when blank |
| Phone | `r.PhonePrimary` | no | no | "—" when blank; header `Admin.Contacts.Col.Phone` |
| Email | `r.Email` | no | no | "—" when blank |
| Country | `CountryCell(...)` from `r.CountryNameEn` / `r.CountryNameAr` | no | no | resolved bilingual name (server-side, no second fetch); "—" when none |
| Active | `r.IsActive` | yes | no | `SimfPill` on/off (`Grid.Active` / `Grid.Inactive`) |

The grid summary `AdminContactSummary` carries only Id, NameAr, NameEn,
PhonePrimary, Email, CountryId + resolved country names, and IsActive. Server-side
the list supports a free-text `Search` (LIKE over NameArabic / Name / Email /
PhonePrimary), filters `name` / `nameen` / `email` / `isactive`, and sort by
`name` / `isactive` (default order: NameArabic ascending). Empty list renders
`SimfEmptyState` with `Admin.Contacts.None` ("No contacts yet" / "...").

### 4.4 Pager
Standard `SimfDataGrid` pager — First / Prev / Next / Last + page-size selector,
summary caption via `FormatSummary` (`Grid.Summary`) and page caption via
`FormatPage` (`Grid.Page`). Default page size `Top = 20` (the service clamps a
requested `Top` to 1..200, defaulting to 25 if unset).

### 4.5 Form fields (`ContactsAddEdit`)
The only required field is the **Arabic name**; everything else is optional.
MaxLengths below are the form `MaxLength=` values, which mirror the server's
`ValidateAndNormalise` caps (`OptionalText(...)`) and `ContactConfiguration.HasMaxLength`.

| Field | Type | Required | MaxLength | Validation | Locale key |
|-------|------|----------|-----------|------------|------------|
| Name (Arabic) | text | **yes** | 256 | 1–256 chars (server `CONTACT_INVALID`) | `Admin.Contacts.Field.NameAr` |
| Name (English) | text | no | 256 | ≤256 chars | `Admin.Contacts.Field.NameEn` |
| Logo relative path | text | no | 512 | ≤512 chars (free-text path; helper `Admin.Contacts.Field.LogoHint`) | `Admin.Contacts.Field.LogoRelativePath` |
| Primary phone | text | no | 32 | ≤32 chars | `Admin.Contacts.Field.PhonePrimary` |
| Secondary phone | text | no | 32 | ≤32 chars | `Admin.Contacts.Field.PhoneSecondary` |
| Email | text | no | 320 | ≤320 chars | `Admin.Contacts.Field.Email` |
| Website | text | no | 512 | ≤512 chars | `Admin.Contacts.Field.Website` |
| Facebook URL | text | no | 256 | ≤256 chars | `Admin.Contacts.Field.FacebookUrl` |
| X URL | text | no | 256 | ≤256 chars | `Admin.Contacts.Field.XUrl` |
| LinkedIn URL | text | no | 256 | ≤256 chars | `Admin.Contacts.Field.LinkedInUrl` |
| Instagram URL | text | no | 256 | ≤256 chars | `Admin.Contacts.Field.InstagramUrl` |
| Country | `SimfSelect` | no | — | must be an existing **active** Country (else `CONTACT_INVALID`); picker loads active countries via `POST /admin/countries/list` | `Admin.Contacts.Field.Country` |
| Latitude | number | no | — | all-or-nothing pair with Longitude; -90..90 | `Admin.Contacts.Field.Latitude` |
| Longitude | number | no | — | all-or-nothing pair with Latitude; -180..180 | `Admin.Contacts.Field.Longitude` |
| Active | checkbox | Edit only | — | bool (Create is always active) | `Admin.Contacts.Field.IsActive` |
| **Logo (image)** | `SimfImageUpload Category="CompanyLogo"` | Edit only | — | unified media-asset pipeline (D-357) — see §4.7 | heading `Admin.Asset.Heading` |

The form runs Create (`POST`) when `IsEdit=false` and Edit (`PUT` against
`Initial.Id`) when `IsEdit=true`; only Edit shows the Active checkbox and the logo
image control (the row must exist before bytes can be attached). Optional fields
are sent as `null` when blank (`NullIfBlank`). Two client-side guards run before the
request: a blank Arabic name (`Admin.Contacts.Required`) and a half-filled lat/long
pair (`Admin.Contacts.Field.LatLongPair` / `Admin.Contacts.Field.LatLongInvalid`).

### 4.6 View / Delete form (`ContactsViewDelete`)
A read-only `<dl>` of Name (Ar/En), Logo relative path, both phones, Email, Website,
Facebook / X / LinkedIn / Instagram, Country, Latitude, Longitude and Active. In
delete mode a red **Deactivate** button (`Admin.Contacts.Action.Deactivate`) opens
a `SimfConfirm` (Danger) whose message is `Admin.Contacts.Delete.Message` formatted
with the contact's display name; only the confirm fires `DELETE`. If the server
returns `CONTACT_IN_USE` (409) the confirm is closed first so the error lands on the
visible form body. The old inline list `confirm()` was removed in D-353.

### 4.7 Logo via the unified media-asset pipeline (D-357)
On the Add/Edit form (edit only), a `<SimfImageUpload Category="CompanyLogo"
OwnerId="@Initial.Id" Alt="@_model.NameEn" />` control attaches the contact's logo
through the shared media-asset pipeline (asset category **`CompanyLogo`**). On the
View/Delete form a thumbnail is rendered:

```razor
<SimfImageThumb Src="@($"/account/api/admin/assets/CompanyLogo/{Initial.Id}/image")"
                Alt="@(Initial.NameEn ?? Initial.NameAr)" Class="simf-img-thumb--lg" />
```

The serve-URL pattern is `/account/api/admin/assets/CompanyLogo/{id}/image` (with
the public proxy at `/content/assets/CompanyLogo/{id}/image`). This is independent
of, and complements, the free-text `LogoRelativePath` field (which remains the
stored relative path shown in the details list).

> **Stale comment note:** the header comment in `ContactsViewDelete.razor`
> (lines 8–10) still claims "there is no image component in the Simf* library" —
> that predates D-357 and contradicts the actual `SimfImageThumb` markup a few lines
> below it. The code (the thumbnail) is authoritative.

## 5. Data flow

```
Admin action → ContactsList handler → JS interop (simfAccount.{post,get,put,delete}Json)
            → BFF /account/api/admin/contacts/* → API /api/v1/admin/contacts/*
            → IAdminContactService / Excel endpoints → SIMF_App DB (Contacts)
            → ApiResult<T> envelope → grid reload + toast
```

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| OnInit / query change / search | `POST /account/api/admin/contacts/list` | `GridQuery` | `ApiResult<GridPage<AdminContactSummary>>` |
| Edit / Details / Delete click | `GET /account/api/admin/contacts/{id}` | — | `ApiResult<AdminContactDetail>` |
| Add save | `POST /account/api/admin/contacts` | `CreateContactRequest` | `ApiResult<AdminContactDetail>` |
| Edit save | `PUT /account/api/admin/contacts/{id}` | `UpdateContactRequest` | `ApiResult<AdminContactDetail>` |
| Confirm deactivate | `DELETE /account/api/admin/contacts/{id}` | — | `ApiResult<bool>` |
| Link picker (other forms) | `GET /account/api/admin/contacts/picker?search=&top=` | — | `ApiResult<IReadOnlyList<ContactPickerItem>>` |
| Export | `POST /account/api/admin/contacts/export` | `AdminGridExportRequest { Ids, Query }` | XLSX binary |
| Import | `POST /account/api/admin/contacts/import` | multipart `file` (.xlsx) | `ApiResult<AdminGridImportResult>` |

Edit / Details / Delete always re-fetch the **detail** before opening a form
because the grid summary omits the logo / social / map fields (and `CountryId`) —
editing from a summary-only model would wipe them. The `PUT` endpoint reads the
route GUID via `Route<Guid>("id")` (the contract carries no id). The picker is used
by the Sponsor / Exhibitor / MediaPartner / Speaker / Booth-officer forms (it is not
shown on this page) and is gated by `Contacts.View`.

### 5.1 Excel export columns
`ExportContactsEndpoint` writes a sheet named **"Contacts"** with header row
`NameAr | NameEn | PhonePrimary | Email | CountryId | CountryNameEn | CountryNameAr | IsActive`.
File name: `simf-contacts-{yyyyMMddHHmmss}.xlsx`. With selected rows the export
honours `AdminGridExportRequest.Ids`; with none, it exports the whole filtered set
(`Query`, with `Skip=0`). Capped at **5000** rows (`MaxExportRows`).

### 5.2 Excel import
`ImportContactsEndpoint` is **insert-only** (every applied row returns
`GridRowApplyKind.Created`). Sheet name must be exactly **"Contacts"**; the only
**required header** is `NameAr`. Imported columns: `NameAr` (required), `NameEn`,
`PhonePrimary`, `Email`, `CountryId` (parsed as int when present). A blank Arabic
name raises a per-row `DataValidationException` ("The Arabic name is required." /
"الاسم بالعربية مطلوب."); any service-side `ApiException` (e.g. a too-long field or
an inactive country) is recorded as a **per-row error**, not a batch abort. Logo,
social links, website, phones-secondary, lat/long are **not** expressible via import
(set them afterwards via Edit). The result
`AdminGridImportResult { Created, Updated, Skipped, Errors[] }` drives the modal
("N created, M updated, K skipped" + per-row error list); the success toast is the
shared `Grid.Import.Done` key.

## 6. Validation + error handling

- **Client-side guards** (`ContactsAddEdit.HandleSubmitAsync`):
  - blank Arabic name → `Admin.Contacts.Required` ("The Arabic name is required.").
  - half-filled map pair → `Admin.Contacts.Field.LatLongPair`; out-of-range or
    unparseable lat/long → `Admin.Contacts.Field.LatLongInvalid`. Both mirror the
    server's `CONTACT_INVALID` pairing/bounds rule.
- **Server-side validation** (`AdminContactService.ValidateAndNormalise`): trims
  fields; NameAr 1–256; NameEn ≤256; LogoRelativePath ≤512; PhonePrimary/Secondary
  ≤32; Email ≤320; Website ≤512; each social URL ≤256; lat/long set together and
  within WGS84 bounds. Every length/pair/bounds failure throws
  `ApiException(ErrorCodes.ContactInvalid, 400, …)` (`CONTACT_INVALID`).
- **Country guard** (`EnsureCountryActiveAsync`): a supplied `CountryId` must point
  at an existing **active** Country, else `CONTACT_INVALID` (400) — "The selected
  country does not exist or is inactive." / "الدولة المحددة غير موجودة أو غير مفعّلة.".
- **Referenced-delete guard** (`IsReferencedByActiveEntityAsync`): a contact still
  linked by an **active** Exhibitor / Sponsor / MediaPartner / Speaker / Booth cannot
  be deactivated — `ApiException(ErrorCodes.ContactInUse, 409, …)` (`CONTACT_IN_USE`)
  — "This contact is still linked to an active company, sponsor, media partner,
  speaker or booth and cannot be deactivated." / "...". The same guard also fires on
  a `PUT` that flips `IsActive` true → false (so an edit cannot orphan a link the
  DELETE path forbids); a benign edit does not run the reference queries.
- **Not found:** `GET` / `PUT` / `DELETE` against a missing id → `CONTACT_NOT_FOUND`
  (404) — "The contact was not found." / "لم يتم العثور على جهة الاتصال.".
- **Deactivate is idempotent:** deactivating an already-inactive contact returns
  successfully without re-running the reference guard.
- **Import upload defence** (`AdminGridImportEndpoint`): missing/empty file → 400
  ("An Excel file is required."); file > 5 MB → 413 `ADMIN_IMPORT_EMPTY`; non-.xlsx
  (fails the ZIP-magic `50 4B 03 04` check) → 400 ("The file is not a valid Excel
  workbook."); wrong sheet name or a missing required header → 400 (from
  `importer.Parse`).
- **Error envelope:** standard `ApiResult<T>.Error` with `Code` + bilingual
  `Message` / `MessageArabic`; the forms surface `MessageForCurrentCulture()`.
- **Toast strategy:** success → `Admin.Contacts.Saved` ("Contact saved.") /
  `Admin.Contacts.Deleted` ("Contact deactivated.") and `Grid.Import.Done` after
  import; list/detail load failure → `Admin.Contacts.LoadFailed` ("Something went
  wrong. Please try again."); form-level errors render in the form's `SimfAlert`.

## 7. Edge cases + known limitations

- **Soft-delete only.** `DELETE` calls `contact.Deactivate()` (`IsActive=false`);
  the row stays in the grid (Active pill = "Inactive") until an `isActive` filter
  excludes inactive rows, and the contact stops appearing in the link picker
  (which lists active rows only).
- **Referenced contact cannot be deactivated** (409 `CONTACT_IN_USE`) — unlink it
  from the active Sponsor/Exhibitor/MediaPartner/Speaker/Booth first.
- **Detail re-fetch before every form** so the summary-only grid never wipes the
  logo / social / map / country fields when editing.
- **Action buttons are not `<AuthorizedAction>`-gated** — an admin with View but not
  `Contacts.Edit` / `Export` / `Import` sees the buttons, but the API rejects the
  call (403). Treat that as the per-action enforcement point.
- **No `Contacts.Create` / `Contacts.Delete` permission by design** — create, update
  and soft-delete all gate on `Contacts.Edit`. Granting a role `Contacts.Edit` grants
  all three; there is no way to grant create without delete (or vice versa).
- **Import is insert-only and narrow** — no upsert, and only NameAr / NameEn /
  PhonePrimary / Email / CountryId can be imported; everything else is set via Edit.
- **Logo image control is Edit-only** — a brand-new contact must be saved first
  before its `CompanyLogo` asset can be attached (the owner id must exist).
- **Stale code comment** in `ContactsViewDelete.razor` (no-image claim) contradicts
  the live `SimfImageThumb` — see §4.7. Cosmetic; out of scope for this doc.

## 8. i18n + RTL

All visible strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR) via
`IStringLocalizer<Strings> L`. Banner title "Contacts" / "جهات الاتصال". The Country
column and dropdown resolve to the Arabic name under the Arabic culture. Toggling to
Arabic sets `<html dir="rtl" lang="ar">`; the nav rail mirrors, the toolbar + search
+ pager reverse, and the `CrudShell` form mirrors. The grid country cell and the
`CountryName` detail both pick the Arabic name when
`CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"`.

## 9. Accessibility

- Keyboard: the `CrudShell` traps focus while a form is open and restores it on
  close; `SimfConfirm` requires an explicit Confirm/Cancel choice (no native
  `window.confirm`).
- Screen reader: `SimfDataGrid` exposes a `Caption` (`Admin.Contacts.Title`) and
  per-row labels (`RowLabel = NameAr`); select-all / per-row select have labels
  (`Grid.SelectAll` / `Grid.SelectRow`).
- Colour contrast: WCAG AA via `theme.tokens.css`; active/inactive use the
  `SimfPill` on/off variants, not colour alone (text "Active" / "Inactive").
- The logo thumbnail carries an `Alt` (English name, falling back to Arabic).
- Focus indicators: the `--focus-ring` token on every focusable element.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| (D-281 Contacts) | Maintain the shared Contact directory | SIMF-FDS-014 / Slice C2; reused by Sponsors / Exhibitors / MediaPartners / Speakers / Booths via `ContactId`; UCS detail entry to be authored |
| (D-281 ContactPicker) | Link an existing contact from an org-facing admin form | Slice C2b; catalogued centrally as E2E-CON-013/014 |

## 11. Related E2E test scenarios

Catalogue file: [`cp-admin-contacts.md`](../../tests/e2e/cp-admin-contacts.md)
(scenario ids **E2E-CON-001 … E2E-CON-021**).

| Scenario | Coverage |
|----------|----------|
| Golden path — create a contact | E2E-CON-001 |
| Edit (full detail round-trip) | E2E-CON-002 |
| Search reloads the grid server-side | E2E-CON-003 |
| Country column resolves the bilingual name | E2E-CON-004 |
| Empty state (`SimfEmptyState`) | E2E-CON-005 |
| Auth gate (no `Contacts.View` → /not-permitted) | E2E-CON-006 |
| Validation — blank Arabic name | E2E-CON-007 |
| Validation — latitude without longitude | E2E-CON-008 |
| Deactivate an unreferenced contact | E2E-CON-009 |
| Conflict — deactivate a referenced contact (409 `CONTACT_IN_USE`) | E2E-CON-010 |
| Server 500 surfaces an error toast | E2E-CON-011 |
| RTL render (Arabic UI) | E2E-CON-012 |
| ContactPicker — link an existing contact | E2E-CON-013 |
| ContactPicker — edit pre-loads / clear unlinks (no wipe) | E2E-CON-014 |
| Presentation toggle persists (D-353) | E2E-CON-015 |
| Full-page mode round-trip (D-353) | E2E-CON-016 |
| Delete confirmation gate — CrudShell + SimfConfirm (D-353) | E2E-CON-017 |
| Excel export (D-356) | E2E-CON-018 |
| Excel import + per-row outcome (D-356) | E2E-CON-019 |
| Excel import rejection — non-.xlsx / wrong-sheet (D-356) | E2E-CON-020 |
| Logo via the unified media-asset pipeline (D-357) | E2E-CON-021 |

## 12. Related docs

- E2E catalogue: [`cp-admin-contacts.md`](../../tests/e2e/cp-admin-contacts.md).
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `ApiResult<T>` envelope + admin grid endpoints.
- Permissions: [`SIMF-Auth-Permissions-Dev-Guide.md`](../../manuals/SIMF-Auth-Permissions-Dev-Guide.md), [`SIMF-Permission-Catalogue.md`](../../SIMF-Permission-Catalogue.md).
- Decisions log: D-261/D-281 (shared Contact directory + picker), D-283 (detail
  re-fetch before edit), D-353 (uniform CrudShell + Page/Popup toggle + SimfConfirm
  delete), D-356 (Excel export/import), D-357 (media-asset logo) in
  [`DECISIONS_LOG.md`](../../decisions/DECISIONS_LOG.md).
- Source: `ContactsList.razor`, `ContactsAddEdit.razor`, `ContactsViewDelete.razor`,
  `ContactEndpoints.cs`, `ContactsExcelEndpoints.cs`, `AdminContactService.cs`,
  `ContactContracts.cs`.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-11 | D-357 | Contact **logo** wired to the unified media-asset pipeline: `<SimfImageUpload Category="CompanyLogo" OwnerId="@Initial.Id" />` on the Add/Edit form (edit only) + a `SimfImageThumb` of `/account/api/admin/assets/CompanyLogo/{id}/image` on Details/Deactivate (complements the existing free-text `LogoRelativePath` field). E2E catalogue extended with E2E-CON-021. |
| 2026-06-11 (orig) | D-356 / D-353 / D-281 | Reference doc created. Documents the SIMF-FDS-014 shared Contact directory, the four-code permission model (`Contacts.View`/`Edit`/`Export`/`Import`, with create/update/deactivate all gating on `Contacts.Edit`), the D-353 `CrudShell` Add/Edit + View/Delete forms with the Page ↔ Popup `CrudPresentationToggle` (PageKey `contacts`) and the `SimfConfirm`-gated deactivate, the `CONTACT_INVALID`/`CONTACT_NOT_FOUND`/`CONTACT_IN_USE` error model, and the D-356 Excel export (`POST /export`) + insert-only import (`POST /import`) via `CrudGridExcel`. |

---

**2026-07-14 (D-357):** the Arabic-name column now renders the contact's logo
thumbnail via the shared `SimfIdentityCell` (`AdminContactSummary.HasLogo`, streamed
from the `CompanyLogo` /assets proxy) or a tinted initials tile. Column key
unchanged so server-side sort/filter is unaffected. E2E-CON-022.

_Last reviewed:_ 2026-07-14 by Claude (D-357 — contact logo thumbnail in the list). Prior: 2026-06-11 by Claude (D-357 contact-logo media-asset doc).
