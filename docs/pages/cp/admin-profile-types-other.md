# Other profile types — `/admin/profile-types/other`

| | |
|--|--|
| **Route** | `/admin/profile-types/other` |
| **Audience** | Administrator |
| **Pattern** | Per-kind sibling of [`admin-profile-types-visitor.md`](admin-profile-types-visitor.md); identical canonical shape. |
| **Status** | ✅ Real |
| **Backend** | Same as Visitor sibling — `userType=Other` filter / body. |
| **Source** | [`OtherProfileTypesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProfileTypes/OtherProfileTypesList.razor) + shared `ProfileTypeForm.razor` |
| **Last reviewed** | 2026-08-04 (D-843 — the Meet-People switch can be turned off) |

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

- **Show in Meet People (D-760).** A per-type master switch over the networking
  surfaces: un-ticked, no account on this profile type appears in the
  "Meet People (same interests)" directory or the recommender. Default **true**,
  so a new partner type is visible until an admin hides it.

  **D-843 — until 2026-08-04 this switch could only be turned ON.** The update
  endpoint bound a hand-written route DTO that omitted the field, so
  FastEndpoints left it at the contract's `true` default and the service assigned
  it unconditionally. Un-ticking the box returned a success toast and silently
  left the type exposed — the drop failed *open*. The route DTO now inherits the
  contract (D-505 shape), so the field cannot be dropped. E2E-OPT-018 covers the
  `true → false` direction, which is the one that never worked; E2E-OPT-017's API
  twin only drove `false → true` and therefore passed throughout.

## 11. E2E

Same shape as Visitor sibling — see [`admin-profile-types-visitor.md`](admin-profile-types-visitor.md) §11; substitute "Other" for "Visitor".

## 12. Related

- Per-page CP documentation set (4-aspect + README, D-380):
  [`../../CP/admin-profile-types-other/README.md`](../../CP/admin-profile-types-other/README.md)
  (Function / Logic / API / Design).

_Last reviewed:_ 2026-08-04 by Claude (D-843 — "Show in Meet People" can now be
turned off; the D-760 switch itself was never documented here). Prior: 2026-05-28
by Claude (D-133 slice 3).
