# Other profile types — `/admin/profile-types/other`

| | |
|--|--|
| **Route** | `/admin/profile-types/other` |
| **Audience** | Administrator |
| **Pattern** | Per-kind sibling of [`admin-profile-types-visitor.md`](admin-profile-types-visitor.md); identical canonical shape. |
| **Status** | ✅ Real |
| **Backend** | Same as Visitor sibling — `userType=Other` filter / body. |
| **Source** | [`OtherProfileTypesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProfileTypes/OtherProfileTypesList.razor) + shared `ProfileTypeForm.razor` |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Other profile types drive the tile picker for Other-typed walk-ins (sponsor
staff / exhibitor reps / press / contractors etc.). UserType pinned to
`Other`; otherwise identical to the Visitor sibling — same form, same
PageColor swatch (D-120), same in-use deletion gate, same cross-kind 404
guard.

## Differences from Visitor sibling

- `UserType` pinned to `Other` at list-load and at create.
- The walk-in wizard on `/admin/others` consumes this pool.
- **D-725 — "Show in the app sign-up picker" toggle.** The shared
  `ProfileTypeForm` carries a `IsAppRegisterable` checkbox (default ticked). When
  un-ticked the type is **admin-assigned only** and is filtered out of the mobile
  self-registration picker (`GET /api/v1/app/account/profile-types`). The seeded
  **Staff** and **Moderator** partner types ship un-ticked (the migration data
  step + `IdentitySeeder` derive it from `MobileAppRole`), so a customer never
  self-registers as one; this CP grid still lists every type. The flag is on both
  the Visitor and Other forms, but only matters in practice for the partner pool.

## 11. E2E

Same shape as Visitor sibling — see [`admin-profile-types-visitor.md`](admin-profile-types-visitor.md) §11; substitute "Other" for "Visitor".

## 12. Related

- Per-page CP documentation set (4-aspect + README, D-380):
  [`../../CP/admin-profile-types-other/README.md`](../../CP/admin-profile-types-other/README.md)
  (Function / Logic / API / Design).

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 3).
