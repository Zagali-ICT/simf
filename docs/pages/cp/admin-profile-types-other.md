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

## 11. E2E

Same shape as Visitor sibling — see [`admin-profile-types-visitor.md`](admin-profile-types-visitor.md) §11; substitute "Other" for "Visitor".

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 3).
