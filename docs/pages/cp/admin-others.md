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

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle).
