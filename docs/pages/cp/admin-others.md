# Others CRUD — `/admin/others`

| | |
|--|--|
| **Route** | `/admin/others` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator |
| **Auth** | `[Authorize(Roles = "Administrator")]` + Approved |
| **Pattern** | D-117 canonical CRUD (per-kind sibling of `/admin/visitors`) |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/admin/others/list`, `POST /admin/others/register-onsite` (D-127), `GET /admin/others/{id}/profile` (D-126), `POST /bulk-delete`, `POST /duplicate`, `POST /export`, `POST /import`, `POST /admin/others/{id}/id-document` (D-129) |
| **Source** | [`OthersList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OthersList.razor) + [`CreateOtherForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateOtherForm.razor) (hosts the D-127 walk-in wizard with `Kind="Other"`) + [`CreateOther.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateOther.razor) deep-link fallback |
| **Tests** | `tests/SIMF.Api.Tests/AdminGridOthersTests.cs` |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Mirror of [`/admin/visitors`](admin-visitors.md) for **Other-typed accounts**
— exhibitor representatives, sponsor staff, press, contractors, and similar
non-visitor non-admin attendees. Same walk-in wizard, same canonical toolbar;
the only differences are the **Add** form excludes the Interests section
(Others don't pick interests — per D-127), and the profile-type pool comes
from `/admin/profile-types/other` (not the Visitor pool).

## 2–6. Inherits canonical shape

Identical to [`admin-visitors.md`](admin-visitors.md) except:

- **Profile-type tiles** are pulled from `/account/api/admin/profile-types?userType=Other`.
- **Interests section omitted** in the walk-in wizard (`Kind="Other"`).
- **Cross-kind security:** Visitor ids on `/admin/others/*` return 404 +
  `ErrorCodes.NotFound` (per D-113 / D-124 type-smuggling guard).

## 6b. Presentation toggle + Excel (D-356 / D-353)

- **D-353 Page↔Popup toggle.** The toolbar carries a `CrudPresentationToggle`
  (`PageKey="others"`); Add / Edit / Details are framed by `CrudShell` and render
  as either a popup (default) or a full-page frame per the admin's choice, persisted
  in `localStorage` under `simf.cp.prefs.others` and rehydrated on load via
  `CpPreferences.GetPresentationAsync("others")`. The forms are `OthersAddEdit`
  (hosts the walk-in `CreateOtherForm` / shared `EditAccountForm`) and
  `OthersViewDelete` (Details-only — no Delete button). Covered by E2E-OTH-023/024.
- **D-356 uniform Excel (CrudGridExcel): N/A — account page.** Others is not on the
  uniform-Excel/`CrudGridExcel` track; its single-row delete is the reason-gated
  `/bulk-delete` dialog (not a `SimfConfirm` gate). The page keeps its existing
  pre-D-356 direct export/import (`/account/api/admin/others/export` +
  `/import` via the `#others-import-input` picker and the inline import-result
  modal), already covered by E2E-OTH-009/010.

## 7. Edge cases

- **Other walk-in with no profile-type seeded** → server 400
  `AdminProfileTypeInvalid`. Admin must seed at least one Other profile-type
  via `/admin/profile-types/other` before walk-ins can succeed.
- **Cross-kind ProfileTypeId** (Visitor profile-type on Other route) → 400
  `AdminProfileTypeInvalid`.

## 10. Use cases

UC-OTH-LIST, UC-OTH-WALKIN-CREATE, UC-OTH-DETAILS, UC-OTH-DELETE,
UC-OTH-DUPLICATE, UC-OTH-IMPORT, UC-OTH-EXPORT _(pending UCS)_.

## 11. E2E

| Scenario | ID |
|----------|----|
| Walk-in Other → Approved + QR minted | E2E-OTH-001 |
| Cross-kind ProfileTypeId rejected | E2E-OTH-002 |
| Cross-kind id on `.../profile` → 404 | E2E-OTH-003 |
| Bulk-delete with reason | E2E-OTH-004 |
| Name column shows the account's profile-photo thumbnail (initials fallback when none) | E2E-OTH-026 |

## 12. Related

- Sibling: [`admin-visitors.md`](admin-visitors.md)
- Walk-in wizard: `WalkInRegistrationForm.razor`
- Profile types pool: [`admin-profile-types-other.md`](admin-profile-types-other.md)
- Decisions: D-113, D-114, D-127, D-129.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-26 | D-113 + D-114 | Type-scoped endpoints + canonical CRUD adoption. |
| 2026-05-28 | D-127 + D-129 | Walk-in wizard + ID-image upload. |
| 2026-06-10 | D-353 + D-356 | Add/Edit/Details moved to `CrudShell` with the Page↔Popup `CrudPresentationToggle` (`simf.cp.prefs.others`). Uniform-Excel (`CrudGridExcel`) N/A — account page keeps its existing direct export/import. |
| 2026-07-09 | D-728 | **Change type (owner item 9).** The Details view (`OthersViewDelete`) now hosts a shared `ChangeAccountTypeBlock` (gated `Accounts.ChangeType`) that flips the account into a Visitor-scope type via `POST /admin/accounts/{id}/change-type`. The dropdown lists only active Visitor-scope types (opposite scope); the flip rolls the security stamp + revokes sessions and keeps the approval state. E2E-OTH-025. |
| 2026-07-14 | D-568 | **Photo thumbnail in the list.** The name column renders the shared `SimfIdentityCell` — the account's profile-photo thumbnail (streamed from `/account/api/admin/others/{id}/avatar` when `AdminUserSummary.HasAvatar`) or an initials tile when there is no photo. Column key unchanged so server-side sort/filter is unaffected. E2E-OTH-026. |

_Last reviewed:_ 2026-07-14 by Claude (D-568 — profile-photo thumbnail in the list). Earlier: 2026-07-09 by SIMF Team (D-728 — change-account-type action); 2026-06-10 (D-356 Phase 5 — Excel + toggle).

## D-809 — changing the account type confirms first

The shared `ChangeAccountTypeBlock` scope flip moves the account to the other
desk and changes what the holder can do. It used to commit on the first click;
it now opens a `SimfConfirm` naming the target type.
