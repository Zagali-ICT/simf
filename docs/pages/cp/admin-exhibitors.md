# Exhibitors — `/admin/exhibitors`

| | |
|--|--|
| **Route** | `/admin/exhibitors` |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Exhibitors.View)]` (page) + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (mutations) |
| **Pattern** | D-202 Track-2 CP CRUD + per-exhibitor account provisioning; D-353 CrudShell framing; D-356 Excel export/import. |
| **Status** | ✅ Real (D-202; D-353 toggle + CrudShell; D-356 Excel) |
| **Backend endpoints** | `POST /account/api/admin/exhibitors/list`, `GET /account/api/admin/exhibitors/{id}`, `POST /account/api/admin/exhibitors`, `PUT /account/api/admin/exhibitors/{id}`, `DELETE /account/api/admin/exhibitors/{id}`, `GET /account/api/admin/exhibitors/{id}/accounts`, `POST /account/api/admin/exhibitors/{id}/accounts`, `POST /account/api/admin/exhibitors/{id}/accounts/link` (D-781), `DELETE /api/v1/admin/exhibitors/{id}/accounts/{membershipId}` (revoke a booth membership; the API route is live, its BFF `/account/api/...` forward is still to be added), `POST /account/api/admin/exhibitors/export`, `POST /account/api/admin/exhibitors/import` (BFF → API `/api/v1/admin/exhibitors/*`) |
| **Source** | [`ExhibitorsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ExhibitorsList.razor), [`ExhibitorsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ExhibitorsAddEdit.razor), [`ExhibitorsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ExhibitorsViewDelete.razor), [`ExhibitorEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Exhibitors/ExhibitorEndpoints.cs), [`ExhibitorsExcelEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Admin/ExhibitorsExcelEndpoints.cs), [`AdminExhibitorService`](../../../src/Backend/SIMF.Infrastructure/Exhibitors/AdminExhibitorService.cs), [`Exhibitor`](../../../src/Backend/SIMF.Domain/Exhibitors/Exhibitor.cs) |
| **Backed by** | `dbo.Exhibitors` + `dbo.ExhibitorMemberships` (D-199/D-202 migration `D202_CompaniesAndProvisioning`; renamed in `D274_AuditFoldAndExhibitorRename`). |
| **Tests** | [`docs/tests/e2e/cp-admin-exhibitors.md`](../../tests/e2e/cp-admin-exhibitors.md) · `tests/SIMF.Api.Tests/ExhibitorsTests.cs` · `tests/SIMF.Api.Tests/ExhibitorsExcelTests.cs` |
| **Last reviewed** | 2026-06-10 |

## 1. Purpose

The exhibitor directory per D-199 #3 / D-202 Track-2. Exhibitors are
created **CP-side only** — in-app exhibitor self-signup was permanently
descoped (D-199/D-202). Each exhibitor is a bilingual record (English +
Arabic name) with optional contact email / phone / website and an
optional link to the shared **Contact** directory (SIMF-FDS-014 / D-281).

Beyond plain CRUD, the page hosts a **per-exhibitor account-provisioning**
sub-flow: an admin provisions a least-privilege **Visitor** login tagged
to the exhibitor, created in the pending-approval state through the
existing admin provisioning pipeline (`CreateVisitorAsync`) and linked by
an `ExhibitorMembership` row. The cross-database boundary is respected
(D-157): the membership's `UserId` is a logical FK to the Identity
database's `SimfUser`, resolved on read with a second query — no cross-DB
JOIN.

## 4. UI

- `SimfBanner` titled "Exhibitors" + the canonical `SimfDataGrid`
  (filter + select-all + per-row quiet icon actions).
- Grid columns: **Name (English)**, **Name (Arabic)**, **Accounts** (the
  count of active memberships), **Active** (on/off `SimfPill`).
- Grid toolbar actions (via `SimfDataGrid`): **Add**, plus per-row
  **Edit** / **Details** / **Delete** quiet icons, plus **Export** /
  **Import**.
- Per-row **Accounts** quiet icon (user) — opens the account-provisioning
  `SimfModal`, wrapped in `<AuthorizedAction Permission="Exhibitors.Edit">`;
  the CRUD action buttons are not individually gated in the CP (enforcement
  is API-side).
- Inside that modal, the **Link an existing account** block (D-781) is the
  second `<AuthorizedAction>`-gated affordance, on its own
  `Exhibitors.LinkAccount` permission — it attaches an account somebody else
  created to this exhibitor, which is what hands out access to the booth's
  visitor contact cards.
- Add/Edit (`ExhibitorsAddEdit`), View/Delete and the read-only Details
  view (`ExhibitorsViewDelete`) are hosted by **`CrudShell`** as a popup
  or a full page (D-353). Delete opens `ExhibitorsViewDelete` with a red
  **Deactivate** button gated by a `SimfConfirm` dialog — no native
  `window.confirm` (D-353).
- Sortable on Name (English), Name (Arabic), Active. Filterable on Name
  (English), Name (Arabic). `AccountCount` is a computed sub-query, so it
  is neither sortable nor server-filterable.
- **Excel export + import (D-356):** the toolbar carries **Export** and
  **Import** actions via `CrudGridExcel` (`Resource="exhibitors"`).
  Export posts `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/exhibitors/export` (selected rows, else the whole
  filtered grid — `Query` is sent only when no rows are selected) and
  downloads `simf-exhibitors-{timestamp}.xlsx` with the sheet
  "Exhibitors" and header row
  `NameEn | NameAr | ContactEmail | ContactPhone | Website | AccountCount | IsActive`.
  Import (insert-only) posts an `.xlsx` to
  `/account/api/admin/exhibitors/import` (required headers `NameEn | NameAr`)
  and shows a result modal ("N created, N updated, N skipped" + per-row
  errors); a blank-name row is a per-row error, not a batch abort. Both
  are capped at 5000 rows; a non-`.xlsx` upload is rejected with HTTP 400.
- **Page ↔ Popup presentation toggle (D-353):** the toolbar
  `CrudPresentationToggle` (`PageKey="exhibitors"`) lets the admin host
  Add/Edit/View/Delete as a dialog or a full page; the choice persists in
  `localStorage` under `simf.cp.prefs.exhibitors` and is restored on load
  via `Prefs.GetPresentationAsync("exhibitors")`.

## 4.5 Form fields

### Exhibitor (ExhibitorsAddEdit)

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Name (English) | yes | 256 | 1–256 chars |
| Name (Arabic) | yes | 256 | 1–256 chars |
| Contact email | no | 320 | ≤320 chars |
| Contact phone | no | 32 | ≤32 chars |
| Website | no | 512 | ≤512 chars |
| Contact (picker) | no | n/a | optional link to an existing **active** Contact (else 400) |
| Active | (Edit only) | bool | the checkbox renders only when `IsEdit`; Create always sets `IsActive = true` |

### Provision account (ExhibitorsList account-provisioning modal)

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Contact name | yes | 256 | 1–256 chars (used as the account display name) |
| Email | yes | 320 | 1–320 chars; must not already be registered |
| Role label | no | 128 | ≤128 chars; free text |

### Link an existing account (D-781 — same modal, `Exhibitors.LinkAccount`)

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Account email | yes | 320 | 1–320 chars; must already be registered, and the account must carry an **active exhibitor-mapped profile type** |
| Contact name (optional) | no | 256 | ≤256 chars; defaults to the account's display name, then its email |
| Role label (optional) | no | 128 | ≤128 chars; free text |

## 5. Data flow + endpoints

- **List** — `OnInitializedAsync`/`OnQueryChangedAsync` → BFF
  `simfAccount.postJson` → `POST /account/api/admin/exhibitors/list`
  (policy `Exhibitors.View`). `AdminExhibitorService.ListAllAsync` pages
  (`Top` clamped 1–200, default 25), applies the search term + per-column
  filters (`nameen` / `namear` / `isactive`), orders (default
  `NameAr` asc), and projects `AdminExhibitorSummary` (carrying the
  active-membership `AccountCount`).
- **Get** — Edit/Details/Delete first GET
  `/account/api/admin/exhibitors/{id}` (policy `Exhibitors.View`) for the
  full `AdminExhibitorDetail` (the grid summary omits `ContactId` +
  timestamps).
- **Create** — `POST /account/api/admin/exhibitors` (policy
  `Exhibitors.Create`, rate-limited "auth"). Sets `IsActive = true`,
  writes `Exhibitor.Created` audit.
- **Update** — `PUT /account/api/admin/exhibitors/{id}` (policy
  `Exhibitors.Edit`, rate-limited "auth"). Writes `Exhibitor.Updated`.
- **Deactivate** — `DELETE /account/api/admin/exhibitors/{id}` (policy
  `Exhibitors.Delete`, rate-limited "auth"). Soft-delete (`IsActive =
  false`); idempotent (returns early if already inactive). Writes
  `Exhibitor.Deactivated`. **DEF-EXH-006:** this also revokes the app
  lead-capture tools for every officer under the exhibitor — the scan and My
  Visitors endpoints require an active `ExhibitorMembership` of an **active**
  `Exhibitor`, so closing the booth answers 403 on their existing tokens.
- **List accounts** — `GET /admin/exhibitors/{id}/accounts` (policy
  `Exhibitors.View`). 404 if the exhibitor id is unknown; resolves the
  account emails cross-context from the Identity DB.
- **Provision account** — `POST /admin/exhibitors/{id}/accounts` (policy
  `Exhibitors.Create`, rate-limited "auth"). Reuses `CreateOtherAsync`
  (least-privilege partner-side account, pending approval) + an
  `ExhibitorMembership` row. Writes `Exhibitor.AccountProvisioned`.
  **DEF-EXH-005:** the account is provisioned with the **exhibitor profile
  type** — resolved by `ProfileType.MobileAppRole == Exhibitor` (D-519), never
  by a name literal — because the app lead-capture tools (scan a visitor badge
  / My Visitors) authorise on exactly that column; the earlier "no profile
  type" account could never scan. Consequence for the desk: a booth officer now
  lands in the **Others** pending-approval queue, not the Visitors queue. With
  no active exhibitor-mapped profile type at all, the call answers 409
  `ADMIN_PROFILE_TYPE_INVALID` instead of minting an unusable account.
  **DEF-EXH-006:** the `ExhibitorMembership` row is not just a tag — it is half
  the authorisation. Deactivating it (or the exhibitor) is what takes the
  lead-capture tools away again; the profile type alone no longer grants them.
- **Link an existing account** — `POST /admin/exhibitors/{id}/accounts/link`
  (policy **`Exhibitors.LinkAccount`**, rate-limited "auth"). Writes
  `Exhibitor.AccountLinked`. **D-781 (owner decision 2026-07-27):** provisioning
  used to be the ONLY writer of `ExhibitorMembership`, so an exhibitor-typed
  account created through the generic Others pipeline (`POST /admin/others`) or
  the Others walk-in desk had the right profile type and no membership — 403 on
  badge scan and on My Visitors (DEF-EXH-006) with no CP path to attach it to a
  booth. This action is that path: it resolves the account by email on the
  Identity DB and writes only the App-DB membership row (D-157 — two contexts,
  two queries, no cross-database join). It does **not** mutate the account's
  profile type: the account must already carry an active exhibitor-mapped type
  (set on the Others page), else it answers 409
  `EXHIBITOR_ACCOUNT_NOT_ELIGIBLE`. Its own permission — separate from
  `Exhibitors.Create` — because it creates nothing and instead grants an existing
  account access to visitor PII.
- **Revoke a booth membership**: `DELETE /admin/exhibitors/{id}/accounts/{membershipId}`
  (policy **`Exhibitors.RevokeAccount`**, rate-limited "auth"). Writes
  `Exhibitor.AccountRevoked`. The counterpart to the two actions above, and the
  other half of the same gap: provisioning and linking both WRITE
  `ExhibitorMembership` and nothing anywhere cleared one, so an account attached
  to a booth kept the booth tools until the whole exhibitor was retired. Three
  readers lose the account the moment this runs: the lead-capture badge scan and
  the booth's captured visitor contact cards, the business-meeting notifications
  that fan out to every active membership, and the account count on this grid.
  A **soft** revoke (`IsActive` cleared, `DeletedAt` stamped), never a hard
  delete, because the row is the attribution trail for the visitor cards that
  account already captured and each capture notified the visitor that their
  details had been shared. The membership is matched on its own id **and** the
  exhibitor from the route, so an id under another booth answers 404 rather than
  letting one exhibitor's administrator revoke another's officer; an already
  revoked membership answers 409. Deliberately does **not** reuse the "is the
  booth still active?" guard that provisioning and linking share: refusing an
  inactive exhibitor is right when adding an officer and backwards when removing
  one, so a closed booth's officers can still be stripped. Its own permission,
  separate from `Exhibitors.Delete`, because Delete retires the whole exhibitor
  while this removes one person and leaves the booth trading.
  **Status:** the API, the permission and the `SimfAdminClient` method are
  shipped; the Control Panel row action, its `SimfConfirm`, its resource strings
  and the BFF `MapDelete` forward are still to be wired.
- **Export** — `POST /admin/exhibitors/export` (policy
  `Exhibitors.Export`, rate-limited "auth") via
  `ExportExhibitorsEndpoint : AdminGridExportEndpoint<AdminExhibitorSummary>`.
  Sheet "Exhibitors"; file prefix `simf-exhibitors`; columns NameEn,
  NameAr, ContactEmail, ContactPhone, Website, AccountCount, IsActive;
  5000-row cap; honours `Ids` (selected rows) else `Query`.
- **Import** — `POST /admin/exhibitors/import` (policy `Exhibitors.Import`,
  rate-limited "auth") via
  `ImportExhibitorsEndpoint : AdminGridImportEndpoint`. Multipart "file";
  sheet "Exhibitors"; required headers NameEn/NameAr; insert-only (every
  applied row → Created); per-row `RowKey` = NameEn. AccountCount,
  IsActive and the ContactId directory FK are intentionally **not**
  importable.

## 6. Validation + error handling

- **Client guard (AddEdit):** blank Name (English) and/or Name (Arabic) →
  in-form `SimfAlert` "Both the English and Arabic names are required."
  (`Admin.Exhibitors.NameRequired`) and **no** POST.
- **Server-side `AdminExhibitorService.Validate`:** Name (EN+AR) 1–256,
  Contact email ≤320, Contact phone ≤32, Website ≤512 → 400
  `EXHIBITOR_INVALID` (bilingual). A Contact link that does not reference
  an existing **active** Contact → 400 `EXHIBITOR_INVALID`.
- **Not found:** 404 `EXHIBITOR_NOT_FOUND`.
- **Provisioning under an inactive exhibitor:** 409 `EXHIBITOR_INACTIVE`.
- **Invalid provisioning input** (contact name/email/role length): 400
  `EXHIBITOR_ACCOUNT_INVALID`. A duplicate / already-registered account
  email surfaces from the reused `CreateVisitorAsync` pipeline.
- **Link an existing account (D-781):** blank/over-length email, contact name or
  role label → 400 `EXHIBITOR_ACCOUNT_INVALID`; no account registered under the
  email → 404 `EXHIBITOR_ACCOUNT_NOT_FOUND`; the account carries no active
  exhibitor-mapped profile type → 409 `EXHIBITOR_ACCOUNT_NOT_ELIGIBLE`; the
  account already holds an active membership → 409
  `EXHIBITOR_ACCOUNT_ALREADY_LINKED` (an account belongs to at most one booth);
  linking under a deactivated exhibitor → 409 `EXHIBITOR_INACTIVE`.
- **Import per-row errors:** a blank name row is reported individually
  ("Both the English and Arabic names are required." /
  "الاسمان بالإنجليزية والعربية كلاهما مطلوبان.") without aborting the batch.
- **Upload defence (import):** >5 MB → 413 `AdminImportEmpty`; non-`.xlsx`
  (fails ZIP-magic `50 4B 03 04`) → 400; wrong sheet / missing required
  header → 400 (bilingual).
- **Transport / 500:** list failures surface
  `Admin.Exhibitors.LoadFailed` ("Could not load exhibitors. Please try
  again.") as a red toast; save/delete failures surface the envelope's
  `MessageForCurrentCulture()` (or the fallback) in-form / as a toast.

## 7. Edge cases + known limitations

- **Create has no Active checkbox** — `ExhibitorsAddEdit` only renders the
  "Active" checkbox when `IsEdit`; a created exhibitor is always active.
- **AccountCount is derived** — counted from active `ExhibitorMembership`
  rows; it is read-only, not sortable and not server-filterable, and is
  never set by import.
- **Deactivate is idempotent** — deactivating an already-inactive
  exhibitor returns early and writes no second audit row.
- **Account provisioning modal is independent of CrudShell** — it is a
  separate `SimfModal` overlay, so the presentation toggle does not affect
  it.
- **Cross-DB separation (D-157)** — `ExhibitorMembership.UserId` is a
  logical FK to the Identity DB's `SimfUser`; account emails are resolved
  with a second AsNoTracking query, never a cross-DB JOIN.
- **Soft-deactivated rows still show** in the grid (with the "off" pill)
  unless an `isActive` filter excludes inactive rows.

## 8. i18n + RTL

`Admin.Exhibitors.*` keys (EN ↔ AR parity), plus the shared `Grid.*` and
`Grid.Import.*` keys for the toolbar / Excel result modal. Banner title
"Exhibitors" / "العارضون"; columns "Name (English)" / "الاسم
(بالإنجليزية)", "Name (Arabic)" / "الاسم (بالعربية)", "Accounts" /
"الحسابات", "Active" / "نشط". Full RTL mirror on the Arabic toggle
(`<html dir="rtl" lang="ar">`).

## 10. Use cases

- UC-EXH-CREATE-001 (add an exhibitor), UC-EXH-EDIT-001 (edit / toggle
  Active), UC-EXH-DEACTIVATE-001 (soft-deactivate), UC-EXH-PROVISION-001
  (provision a per-exhibitor account), UC-EXH-EXPORT-001 / UC-EXH-IMPORT-001
  (Excel export / import).

## 11. E2E

See [`docs/tests/e2e/cp-admin-exhibitors.md`](../../tests/e2e/cp-admin-exhibitors.md):
E2E-EXH-001 CRUD round-trip, 002 add-only, 003 edit, 004 delete-confirm,
005 cancel-delete, 006 read-only Details, 007 empty, 008 page auth gate,
009 action auth gate (403), 010 client validation, 011 server validation
(`EXHIBITOR_INVALID`), 012 conflict (`EXHIBITOR_INACTIVE`), 013 server 500,
014 cancel discards, 015 RTL, 016 column filter, 017 column sort, 018
toggle persist, 019 full-page round-trip, 020 account provisioning, 021
Excel export, 022 Excel import, 023 Excel import rejection, 027/028 link an
existing account and its rejections, 029 provision an already-registered email,
030 revoke a booth membership, 031 revoke rejections and its permission gate
(030/031 are authored against the API; the CP control is still to be wired).

## 12. Related docs

- Per-page CP documentation set (4-aspect + README, D-380):
  [`../../CP/admin-exhibitors/README.md`](../../CP/admin-exhibitors/README.md)
  (Function / Logic / API / Design).
- Sibling Exhibition modules: [`admin-booths.md`](admin-booths.md),
  [`admin-sponsors.md`](admin-sponsors.md).
- Shared Contact directory: [`admin-contacts.md`](admin-contacts.md)
  (SIMF-FDS-014 / D-281).
- Decisions: D-199 (#3 exhibitor module), D-202 (CP CRUD + account
  provisioning), D-281 (Contact link), D-353 (CrudShell + toggle), D-356
  (Excel export/import). Cross-DB rule: D-157.
- Authority spec: SIMF-FDS-014 (Contact directory); D-199 owner decisions.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-30 | D-199 / D-202 | Original — Exhibitor CRUD (CP-only) + per-exhibitor account provisioning; `Exhibitors` + `ExhibitorMemberships` tables. |
| 2026-06-04 | D-281 / D-275 | Added the optional shared-Contact link (`ContactId`) and bilingual NameEn/NameAr standardisation. |
| 2026-06-10 | D-353 | Add/Edit/View/Delete split into `ExhibitorsAddEdit` + `ExhibitorsViewDelete` hosted by `CrudShell` (Page↔Popup toggle persisted in `localStorage`); Deactivate gated by `SimfConfirm`. |
| 2026-06-10 | D-356 | Excel export + import added (toolbar Export/Import via `CrudGridExcel`, sheet "Exhibitors"); E2E catalogue authored (E2E-EXH-001…023). |

**2026-07-14 (D-357):** the English-name column now renders the exhibitor's
company-logo thumbnail via the shared `SimfIdentityCell` — the **linked Contact's**
`CompanyLogo` asset (an exhibitor owns no logo of its own; `AdminExhibitorSummary`
gained `ContactId` + `HasLogo`) — or a tinted initials tile (unlinked exhibitors,
or contacts with no logo). Column key unchanged so server-side sort/filter is
unaffected. E2E-EXH-025.

**2026-07-25 (D-764):** an exhibitor now owns its **own** logo
(`AssetCategory.ExhibitorLogo`, owner = the exhibitor, independent of the linked
Contact). `ExhibitorsAddEdit` (Edit mode) has a `<SimfImageUpload
Category="ExhibitorLogo">` "Logo" field; the grid renders the exhibitor's own logo
(`AdminExhibitorSummary.HasExhibitorLogo`) and the app exhibitor-detail screen shows
it (`GET /app/assets/ExhibitorLogo/{id}/image`, CompanyLogo fallback). Works with no
linked Contact. Gated by `Exhibitors.Edit`. E2E-EXH-025 / E2E-EXH-026.

**2026-07-27 (D-781 — owner decision):** the Accounts modal gained a **Link an
existing account** block on its own `Exhibitors.LinkAccount` permission, posting
`POST /admin/exhibitors/{id}/accounts/link`. It fixes a lockout: DEF-EXH-006 made
a current `ExhibitorMembership` half the app lead-capture authorisation, and
provisioning was the only writer of that row, so an exhibitor-typed account
created through the generic Others pipeline (`POST /admin/others`) or the Others
walk-in desk was 403 on badge scan and on My Visitors with no CP path to attach
it to a booth. Linking does not change the account's profile type — it must
already carry an active exhibitor-mapped one (409
`EXHIBITOR_ACCOUNT_NOT_ELIGIBLE` otherwise). Audit `Exhibitor.AccountLinked`.
E2E-EXH-027.

**2026-08-19 (revoke a booth membership):** the counterpart to linking, and the
other half of the same gap. `DELETE /admin/exhibitors/{id}/accounts/{membershipId}`
on its own `Exhibitors.RevokeAccount` permission soft-revokes the
`ExhibitorMembership`, which nothing anywhere had ever cleared: an account
attached to a booth kept badge scan, the booth's captured visitor contact cards
and the business-meeting notifications until somebody retired the whole
exhibitor. Soft, not hard, because the row is the attribution trail for the
visitor cards that account already captured. 404 on an unknown or wrong-booth
membership id (the lookup is scoped to the route's exhibitor as well as the id),
409 when it was already revoked, and unlike the add paths it does not refuse an
inactive exhibitor, so a closed booth's officers can still be stripped. Audit
`Exhibitor.AccountRevoked`. E2E-EXH-030 / E2E-EXH-031. The API, the permission
and the `SimfAdminClient` method shipped together; the CP row action, its
`SimfConfirm`, its resource strings and the BFF forward are still to be wired.

_Last reviewed:_ 2026-08-19 by Claude (revoke an exhibitor account: the API,
permission and client half). Prior: 2026-07-27 by Claude (D-781 — link an
existing account to an exhibitor); 2026-07-25 by Claude (D-764 — exhibitor's own
logo upload + thumbnail); 2026-07-14 by Claude (D-357).
