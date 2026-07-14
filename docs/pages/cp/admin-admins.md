# Admins CRUD — `/admin/admins`

| | |
|--|--|
| **Route** | `/admin/admins` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator |
| **Auth** | `[Authorize(Roles = "Administrator")]` + Approved account |
| **Pattern** | D-117 canonical CRUD (gold-standard reference) |
| **Status** | ✅ Real |
| **Implements use case(s)** | UC-USR-LIST, UC-USR-CREATE, UC-USR-EDIT (stub), UC-USR-DETAILS, UC-USR-DELETE (single + bulk), UC-USR-DUPLICATE, UC-USR-IMPORT, UC-USR-EXPORT _(to be authored)_ |
| **Backend endpoints** | `POST /account/api/admin/admins/list`, `POST /account/api/admin/admins`, `POST .../bulk-delete`, `POST .../duplicate`, `POST .../export`, `POST .../import` |
| **Source file** | [`src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/UsersList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/UsersList.razor) |
| **Deep-link fallback** | `/admin/admins/new` → [`CreateUser.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateUser.razor) (hosts the same `CreateAdminForm.razor` the modal uses) |
| **Tests** | [`docs/tests/e2e/cp-admin-admins.md`](../../tests/e2e/cp-admin-admins.md) _(pending)_; integration tests in `tests/SIMF.Api.Tests/AdminUsersTests.cs` |
| **Last reviewed** | 2026-05-28 |

---

## 1. Purpose

`/admin/admins` is the **gold-standard reference** for every CRUD list page in
the Control Panel. It lists every account with the `Administrator` role and
exposes the canonical D-117 toolbar: Add (opens modal hosting
`CreateAdminForm`), Edit (stub modal — real Edit awaits the User Management
module), Details (read-only modal), Delete (per-row + bulk with required
reason 10–500 chars), Duplicate (clone with a new email), Copy / Paste /
Import / Export. It's the page operators land on most often when onboarding a
new administrator or removing access for a departing one.

## 2. Audience + permissions

- **Who can reach it:** `Administrator` only.
- **Authorisation:** the BFF and API both enforce `Administrator` role +
  `RequireApprovedAccount`. The page also blocks the operator from deleting
  themselves (server-side guard).
- **Self-delete protection:** bulk-delete and per-row delete both silently
  skip the actor's own row (audited as `AdminUserSelfDeleteSkipped`). The
  toolbar Delete button is never hidden, but submitting your own id has no
  effect.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/audit-admin-admins.png` | 2026-05-28 |
| Add modal | `docs/screenshots/audit-admin-admins-modal.png` | 2026-05-28 |
| RTL | `docs/screenshots/audit-rtl-admins.png` | 2026-05-28 |
| Bulk-delete reason modal | _to capture_ | — |
| Duplicate modal | _to capture_ | — |
| Import-result modal | _to capture_ | — |
| Edit-stub modal | _to capture_ | — |

## 4. UI affordances

### 4.1 Banner

`<SimfBanner Title="@L[\"Admin.Users.Title\"]" />` → EN "Admins" / AR
"المسؤولون" (fixed in D-132 step 11; was "Users" before).

### 4.2 Toolbar

> **D-353 / D-356 (2026-06-10).** The `CustomToolbar` now hosts a
> `CrudPresentationToggle` (PageKey `"admins"`) that switches Add / Edit-roles /
> Details between a **popup dialog** and a **full page**; the choice is persisted
> in `localStorage` (`simf.cp.prefs.admins`) via `CpPreferences` and rehydrated in
> `OnInitializedAsync`. Add / Edit-roles / Details are framed by `CrudShell`
> (hosting `UsersAddEdit` / `UsersViewDelete`); the inline SimfModal versions are
> gone. **Excel is N/A as a generic `CrudGridExcel` here** — this account page
> keeps its own bespoke Excel wiring (`OnExportAsync` → `simfAccount.downloadXlsx`,
> `OnImportAsync` → the `#users-import-input` picker, both against
> `/account/api/admin/admins/{export,import}`), preserved unchanged through the
> D-356 program. Delete stays the reason-gated bulk-delete modal (10–500 chars),
> **not** a `SimfConfirm` gate.

| Button | Wired callback | Endpoint | Notes |
|--------|----------------|----------|-------|
| Select all | built-in | — | tick every visible row |
| Add | `OnAddAsync` | opens modal → `CreateAdminForm` → `POST /admins` | Q-D: `/admin/admins/new` deep-link still works |
| Edit | `OnEditAsync` | stub modal | Real Edit awaits User Management module; modal renders `Admin.Users.Edit.NotYet` copy |
| Details | `OnDetailsAsync` | client-side modal | Reads `AdminUserSummary` directly — no extra fetch (Q-G) |
| Delete (per-row + bulk) | `OnRowDeleteAsync` / `OnBulkDeleteAsync` | `POST /bulk-delete` | Requires reason (10–500 chars); self-delete silently skipped |
| Duplicate | `OnDuplicateAsync` | `POST /duplicate` | Modal asks for the new email; backend mints a fresh user with same role |
| Copy / Copy selected | `OnCopyOneAsync` / `OnCopySelectedAsync` | client-side toast only | Stub for future paste-to-spreadsheet |
| Paste | `OnPasteAsync` | toast | Not implemented; placeholder |
| Import | `OnImportAsync` | `POST /import` | XLSX upload, ≤ 5 MB, ZIP-magic validation |
| Export | `OnExportAsync` | `POST /export` | Exports selected rows OR the full query if nothing selected |

### 4.3 Grid columns

| Column | Source | Sortable | Filterable |
|--------|--------|----------|------------|
| Email | `AdminUserSummary.Email` | yes | yes |
| Display name | `.DisplayName` | yes | yes |
| State | `.AccountState` | yes | no |
| Role | `.IsAdministrator` | no | no — pill (Admin / User) |

### 4.4 Pager + i18n + accessibility

Same as the canonical D-117 + D-132 shape — see [`admin-interests.md`](admin-interests.md)
§4.4, §8, §9. No deviations.

## 5. Data flow

The Add path is the most common:

```
Admin clicks +Add
  → _addOpen = true
  → <SimfModal> renders <CreateAdminForm>
  → form: email, displayName, password (mandatory), TOTP-on-first-login flag
  → submit
  → simfAccount.postJson("/account/api/admin/admins", AdminCreateUserRequest)
  → BFF forwards with bearer
  → POST /api/v1/admin/admins (Administrator + Approved)
  → AdminUserProvisioningService creates SimfUser (Approved if invited),
     assigns Administrator role, audits row, mints first-login TOTP-pairing token
  → ApiResult<AdminCreateUserResponse>
  → modal calls OnSuccess → grid reloads → toast Admin.CreateAdmin.Success
```

## 6. Validation + error handling

Mirrors §6 of [`admin-interests.md`](admin-interests.md) — `AdminCreateUserRequestValidator`
on the API enforces: email format, display name 2–128, password complexity
(min 12 chars, ≥1 digit ≥1 upper ≥1 lower ≥1 special), uniqueness of email.

## 7. Edge cases + known limitations

- **Bulk-delete requires a reason** (10–500 chars) — the modal disables
  Submit until the textarea passes the length gate.
- **Self-delete** silently skipped (see §2).
- **Administrator-role on a Visitor / Other** — D-113 type-smuggling guard
  means an Administrator-roled Visitor in the batch is skipped silently from
  the `/admin/admins/*` paths (those scopes are role-pinned).
- **Import XLSX** caps at 5 MB; the parser validates the ZIP magic before
  reading. Bad rows land in the error report; good rows still commit.
- **Edit is a stub** — the modal exists for UI consistency but no fields are
  editable yet. Awaits the User Management module.
- **No 2FA reset here** — that lives on `/admin/reset-2fa` (per-target reset).

## 8. i18n + RTL

Identical to [`admin-interests.md`](admin-interests.md) §8. `Admin.Users.*`
keys cover the toolbar; all 576 EN/AR pairs were verified at D-132 audit.

## 9. Accessibility

Identical to [`admin-interests.md`](admin-interests.md) §9.

## 10. Related use cases

| UC ID | Title | Notes |
|-------|-------|-------|
| UC-USR-LIST | List + filter + sort administrators | _(pending UCS author)_ |
| UC-USR-CREATE | Invite a new administrator | _(pending)_ |
| UC-USR-DETAILS | View administrator details | _(pending)_ |
| UC-USR-DUPLICATE | Duplicate administrator with new email | _(pending)_ |
| UC-USR-DELETE | Delete (single + bulk with reason) | _(pending)_ |
| UC-USR-IMPORT | Bulk-import administrators from XLSX | _(pending)_ |
| UC-USR-EXPORT | Export administrators to XLSX | _(pending)_ |

## 11. Related E2E test scenarios

_(catalogue to be authored under `docs/tests/e2e/cp-admin-admins.md`)_

| Scenario | ID | Coverage |
|----------|----|----------|
| Golden: Add → invited admin appears Approved | E2E-USR-001 | full create |
| Bulk-delete: select 3, type reason, submit, see toast + reload | E2E-USR-002 | bulk-delete |
| Self-delete attempt → silently skipped (toast says "Deleted 0, skipped 1") | E2E-USR-003 | self-guard |
| Duplicate: clone Bob to Bob2 → new row appears | E2E-USR-004 | duplicate |
| Import: 50-row XLSX → 50 created, 0 errors | E2E-USR-005 | import |
| Export: select all, click Export → XLSX downloads | E2E-USR-006 | export |
| Auth: non-admin user → /not-permitted | E2E-USR-007 | role gate |
| RTL: toggle Arabic → page mirrors, toolbar Arabic, pager Arabic | E2E-USR-008 | i18n |
| Name column shows the admin's profile-photo thumbnail (initials fallback when none) | E2E-USR-025 | D-357 photo-in-list |

## 12. Related docs

- Manual chapter: `Admin-Manual.md § 10.1 Admins` _(scaffold; chapter pending)_
- Pattern: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) — `UsersList.razor` is the gold-standard reference.
- API: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `/admin/admins/*` endpoints.
- Decisions: D-042 (original), D-044 / D-045 H1 (hardening), D-117 (canonical promotion), D-132 (title fix EN/AR).
- Source: `UsersList.razor`, `CreateAdminForm.razor`, `CreateUser.razor` (deep-link fallback).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-?? | D-042 | Original `/admin/admins` ships. |
| 2026-?? | D-044 / D-045 H1 | Selection state hardening (selection clears on page/size change). |
| 2026-05-26 | D-117 | Promoted to canonical CRUD pattern (banner + modals + full toolbar). |
| 2026-05-28 | D-132 | Title resx flipped EN "Users → Admins" + AR "المستخدمون → المسؤولون". |
| 2026-06-10 | D-353 / D-356 | Add/Edit-roles/Details moved to `CrudShell` + `CrudPresentationToggle` (Page↔Popup, persisted in `simf.cp.prefs.admins`); inline modals removed. Excel export/import kept as the page's bespoke wiring (no generic `CrudGridExcel`). New E2E scenarios E2E-USR-022..024. |
| 2026-07-14 | D-357 | **Photo thumbnail in the list.** The name column now renders the shared `SimfIdentityCell` — the admin's profile-photo thumbnail (streamed from `/account/api/admin/admins/{id}/avatar`, gated `Admins.View`, when `AdminUserSummary.HasAvatar`, the central `AvatarRelativePath` sentinel) or an initials tile when there is no photo (never a broken image) — matching the visitors/others/speakers lists. Avatars for every user type live in the one central file store (D-568). Column stays `Key="displayName"` so server-side sort/filter is unchanged. E2E-USR-025. |

---

_Last reviewed:_ 2026-07-14 by Claude (D-357 — profile-photo thumbnail in the list). Earlier: 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle).
