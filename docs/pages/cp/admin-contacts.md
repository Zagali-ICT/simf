# Contacts — `/admin/contacts`

| | |
|--|--|
| **Route** | `/admin/contacts` |
| **Audience** | Administrator (any role holding `Contacts.View`) |
| **Auth** | `[RequirePermission(PermissionCatalog.Contacts.View)]` (page) + `RequireApprovedAccount`; writes gated `Contacts.Edit`, export `Contacts.Export`, import `Contacts.Import`; mutations `RequireRateLimiting("auth")` |
| **Pattern** | SimfDataGrid list + D-353 `CrudShell` (dialog/full-page) hosting `ContactsAddEdit` + `ContactsViewDelete`; D-356 grid Excel export + import. |
| **Status** | ✅ Real (SIMF-FDS-014 / D-281 / Slice C2) |
| **Backend endpoints** | BFF `/account/api/admin/contacts/*` → API `/api/v1/admin/contacts/*`: `POST .../list`, `GET .../{id}`, `GET .../picker`, `POST ...` (create), `PUT .../{id}`, `DELETE .../{id}`, `POST .../export`, `POST .../import` |
| **Source** | [`ContactsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContactsList.razor), [`ContactsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContactsAddEdit.razor), [`ContactsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContactsViewDelete.razor), [`ContactEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/ContactEndpoints.cs), [`ContactsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/ContactsExcelEndpoints.cs), [`AdminContactService.cs`](../../../src/Backend/SIMF.Infrastructure/Contacts/AdminContactService.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-contacts.md`](../../tests/e2e/cp-admin-contacts.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

The shared, de-duplicated **Contact directory** (SIMF-FDS-014 / D-281,
Slice C2). One Contact record holds a party's bilingual name (Arabic
required, English optional), a logo relative path, two phones, email,
website, four social links (Facebook / X / LinkedIn / Instagram), a map
location (latitude + longitude) and a country. The directory is the single
source of truth reused by **Sponsors, Exhibitors, Media partners, Speakers
and Booth officers** through a nullable `ContactId` FK — the `ContactPicker`
on each of those admin forms links an existing contact rather than copying
its fields (catalogued centrally as E2E-CON-013/014). `CountryId` is a
same-DB FK to `Country`, resolved to bilingual names for the grid/detail in
one batched lookup (no N+1). Records soft-delete via `IsActive`.

## 4. UI

- `SimfBanner` (`Admin.Contacts.Title`) + a search toolbar with a
  `SimfTextField` and a Search button; the grid is hidden when a full-page
  form is open (`GridHidden`).
- `SimfDataGrid` (multiselect, server-driven via `GridQuery`). Columns:
  **Name (Arabic)** (sortable, filterable), **Name (English)** (filterable,
  "—" when blank), **Primary phone** ("—" when blank), **Email** ("—" when
  blank), **Country** (resolved bilingual name, "—" when none), **Active**
  (`SimfPill` on/off). Per-row Edit / Details / Delete actions; toolbar Add.
- Per-row Edit / Details / Delete **fetch the full detail first**
  (`GET /account/api/admin/contacts/{id}`) because the grid summary omits the
  logo / social / map fields; on failure a toast shows and the form does not
  open.
- `EmptyTemplate` renders `SimfEmptyState` (`Admin.Contacts.None`).
- **Page ↔ Popup presentation toggle (D-353):** the grid `CustomToolbar`
  carries a `CrudPresentationToggle` bound to `_presentation`; the choice
  persists via `CpPreferences` under the page key `"contacts"`
  (`localStorage` key `simf.cp.prefs.contacts`) and is restored in
  `OnInitializedAsync`. Add / Edit / Details / Delete are hosted by
  `CrudShell` as a dialog or a full page per that choice — the inline
  `SimfModal` form and the native `confirm()` delete the page used to carry
  are gone.
- **Excel export + import (D-356):** the toolbar carries **Export** and
  **Import** actions wired to the reusable `CrudGridExcel` component
  (`Resource="contacts"`). Export posts the selected row ids (or, with no
  selection, the current `GridQuery` including `Search`); import opens the
  hidden `.xlsx` file picker. On import success the shared `Grid.Import.Done`
  toast shows and the grid reloads; an import/export error surfaces via the
  `OnError` toast.

## 4.5 Form fields

`ContactsAddEdit` (`CrudAddEditFormBase<AdminContactDetail>`). MaxLength
values below are the form's `MaxLength` attributes; the server enforces the
same lengths (`AdminContactService.ValidateAndNormalise`, mirroring
`ContactConfiguration.HasMaxLength`).

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Name (Arabic) | yes | 256 | 1–256 chars (the only required field) |
| Name (English) | no | 256 | optional |
| Logo relative path | no | 512 | optional (free text path) |
| Primary phone | no | 32 | optional |
| Secondary phone | no | 32 | optional |
| Email | no | 320 | optional |
| Website | no | 512 | optional |
| Facebook URL | no | 256 | optional |
| X URL | no | 256 | optional |
| LinkedIn URL | no | 256 | optional |
| Instagram URL | no | 256 | optional |
| Country | no | n/a | `SimfSelect` over active countries; must exist + be active server-side |
| Latitude | no | n/a | number; with longitude or both blank; −90…90 |
| Longitude | no | n/a | number; with latitude or both blank; −180…180 |
| Active | (Edit only) | bool | — |

The country picker loads once on first render (active rows only, top 500); if
the load fails the picker stays empty and the admin can still save with no
country. **D-357:** in Edit mode a `SimfImageUpload` (Category `CompanyLogo`,
`OwnerId` = the contact id) attaches the logo via the unified media-asset
pipeline — edit-only because the row must exist before bytes can be attached.

## 5. Data flow + endpoints

The page calls the BFF (`/account/api/...`) via the `simfAccount` JS interop;
the BFF forwards to the API (`/api/v1/admin/contacts/*`) with the access
token. `MapGridExcel(group, "contacts")` registers the `/export` (binary) and
`/import` (multipart) passthroughs alongside the bespoke CRUD passthroughs.

| BFF route (CP) | API route | Permission | Returns |
|----------------|-----------|------------|---------|
| `POST /account/api/admin/contacts/list` | `POST /admin/contacts/list` | `Contacts.View` | `ApiResult<GridPage<AdminContactSummary>>` |
| `GET /account/api/admin/contacts/{id}` | `GET /admin/contacts/{id}` | `Contacts.View` | `ApiResult<AdminContactDetail>` |
| `GET /account/api/admin/contacts/picker` | `GET /admin/contacts/picker` | `Contacts.View` | `ApiResult<IReadOnlyList<ContactPickerItem>>` |
| `POST /account/api/admin/contacts` | `POST /admin/contacts` | `Contacts.Edit` | `ApiResult<AdminContactDetail>` |
| `PUT /account/api/admin/contacts/{id}` | `PUT /admin/contacts/{id}` | `Contacts.Edit` | `ApiResult<AdminContactDetail>` |
| `DELETE /account/api/admin/contacts/{id}` | `DELETE /admin/contacts/{id}` | `Contacts.Edit` | `ApiResult<bool>` |
| `POST /account/api/admin/contacts/export` | `POST /admin/contacts/export` | `Contacts.Export` | XLSX bytes |
| `POST /account/api/admin/contacts/import` | `POST /admin/contacts/import` | `Contacts.Import` | `ApiResult<AdminGridImportResult>` |

**Export** (`ExportContactsEndpoint : AdminGridExportEndpoint<AdminContactSummary>`):
sheet **"Contacts"**, file prefix `simf-contacts` (so
`simf-contacts-{yyyyMMddHHmmss}.xlsx`), capped at **5,000** rows. With
`AdminGridExportRequest.Ids` set it exports exactly those rows; otherwise it
exports the whole filtered grid (the base resets `Skip` and sets `Top` to
the cap). Columns: `NameAr | NameEn | PhonePrimary | Email | CountryId |
CountryNameEn | CountryNameAr | IsActive`.

**Import** (`ImportContactsEndpoint : AdminGridImportEndpoint`): sheet
**"Contacts"**, **insert-only** (each row → `CreateContactRequest` →
`service.CreateAsync`, recorded as `Created`). Required header: **`NameAr`**
(the row key for the error list). Optional headers read: `NameEn`,
`PhonePrimary`, `Email`, `CountryId`. The base owns the upload defence (5 MB
cap, ZIP-magic `.xlsx` check), the parse and the per-row try/catch so one bad
row records a per-row error instead of aborting the batch; capped at **5,000**
data rows. The result modal shows created / updated / skipped counts plus the
per-row error list.

## 6. Validation + error handling

Server-side `AdminContactService` is the source of truth:

- **`Contact Arabic name must be between 1 and 256 characters.`** → 400
  `CONTACT_INVALID`. Optional text fields over their max length → 400
  `CONTACT_INVALID` (per-field bilingual message).
- **Lat/long pairing:** both set or both blank, else 400 `CONTACT_INVALID`
  ("Latitude and longitude must be set together."); out-of-range (lat ∉
  −90…90 or lng ∉ −180…180) → 400 `CONTACT_INVALID`. The form mirrors this
  client-side before submitting.
- **Country:** a non-existent or inactive `CountryId` → 400 `CONTACT_INVALID`
  ("The selected country does not exist or is inactive.").
- **Not found:** `GET` / `PUT` / `DELETE` on a missing id → 404
  `CONTACT_NOT_FOUND`.
- **Referenced-delete guard:** a contact still linked from an active
  Exhibitor / Sponsor / Media partner / Speaker / Booth → 409
  `CONTACT_IN_USE`. This guard also fires on the **Edit** deactivation
  transition (`IsActive` true → false via PUT), not only on DELETE, so an
  edit cannot orphan a link the DELETE path forbids.
- The CP forms surface the API's bilingual message
  (`Error.MessageForCurrentCulture()`); a transport failure falls back to a
  generic load-failed toast.

## 7. Edge cases + known limitations

- **`DELETE` is idempotent** — deactivating an already-inactive contact is a
  no-op success.
- **`ContactsViewDelete` shows the logo two ways** — a `SimfImageThumb`
  preview from the media-asset endpoint
  (`/account/api/admin/assets/CompanyLogo/{id}/image`, D-357) **and** the
  stored relative-path text in the detail list.
- **409 placement** — when a delete is blocked, the `SimfConfirm` dialog is
  closed first so the error lands on the visible form body, not behind the
  overlay.
- **Picker is link-existing only** — managing the directory (create / edit /
  deactivate) happens here; the `ContactPicker` on org forms only links an
  existing active contact.
- **Search** matches Arabic name, English name, email and primary phone
  (server-side `LIKE`), and resets `Skip` to 0.

## 8. i18n + RTL

`Admin.Contacts.*` resx keys (title, search, columns, field labels, action
labels, toasts, confirm message) plus shared `Grid.*` keys; EN ↔ AR parity.
The Arabic title is rendered RTL with mirrored grid / toolbar / forms; the
country dropdown and grid column show Arabic country names under the Arabic
culture. (Exact resx values are descriptive here — verify against the
resource files.)

## 10. Use cases

Create / edit / view / deactivate a shared contact; link an existing contact
onto a Sponsor / Exhibitor / Media partner / Speaker / Booth-officer form
(`ContactPicker`); bulk export the directory to XLSX; bulk insert contacts
from an XLSX workbook.

## 11. E2E

See [`docs/tests/e2e/cp-admin-contacts.md`](../../tests/e2e/cp-admin-contacts.md):
E2E-CON-001 create golden, 002 edit round-trip, 003 server-side search, 004
resolved country column, 005 empty state, 006 auth gate, 007 blank-Arabic-name
validation, 008 lat-without-long validation, 009 deactivate unreferenced, 010
referenced-delete 409, 011 server 500, 012 RTL, 013/014 `ContactPicker`
link/clear, 015/016 D-353 presentation toggle + full-page round-trip, 017
`CrudShell` + `SimfConfirm` delete gate, 018 Excel export, 019 Excel import,
020 import rejection, 021 D-357 logo media-asset pipeline. API-layer coverage:
`tests/SIMF.Api.Tests/ContactsTests.cs` and
`tests/SIMF.Api.Tests/ContactsExcelTests.cs`.

## 12. Related docs

- Authority spec: SIMF-FDS-014 (the shared Contact directory, Slice C2/C2b).
- Decisions: D-281 (directory), D-261 (admin CRUD + picker), D-353
  (CrudShell dialog/full-page + SimfConfirm delete), D-356 (grid Excel export
  + import), D-357 (logo via the unified media-asset pipeline).
- Permission catalogue: `PermissionCatalog.Contacts` (`View` / `Edit` /
  `Export` / `Import`); navigation item `Module.Contacts` gated `Contacts.View`.
- Sibling org-facing modules that link a contact: Sponsors, Exhibitors,
  Media partners, Speakers, Booths.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-?? | D-281 / D-261 | Original — shared Contact directory: SimfDataGrid list, inline SimfModal CRUD form, server-side search, country resolution, referenced-delete 409, and the link `ContactPicker` for org forms. |
| 2026-06-?? | D-353 | CRUD forms split into `ContactsAddEdit` + `ContactsViewDelete` hosted by `CrudShell` (dialog or full page); `SimfConfirm`-gated Deactivate replaces the native `confirm()`; Page↔Popup presentation toggle persisted in `localStorage` (`simf.cp.prefs.contacts`). |
| 2026-06-?? | D-356 | Excel export + import added (toolbar Export/Import via `CrudGridExcel`; sheet "Contacts"; export columns + import required header `NameAr`; 5,000-row cap; 5 MB + ZIP-magic upload defence). `Contacts.Export` + `Contacts.Import` permissions. |
| 2026-06-?? | D-357 | Edit-mode logo via the unified media-asset pipeline (`SimfImageUpload` / `SimfImageThumb`, Category `CompanyLogo`). |

_Last reviewed:_ 2026-06-11 by Claude (D-356 — Excel export + import + D-353 toggle + CrudShell delete).
