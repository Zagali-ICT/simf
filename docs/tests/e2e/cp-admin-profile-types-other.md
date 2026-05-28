# E2E test catalogue — Other profile types (`/admin/profile-types/other`)

| | |
|--|--|
| **Page** | [`cp/admin-profile-types-other.md`](../../pages/cp/admin-profile-types-other.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

Per-kind sibling of `cp-admin-profile-types-visitor.md`. Scenarios mirror
the Visitor file with namespace `OPT` instead of `VPT`:

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-OPT-001 | Add → tile appears in /admin/others wizard | P0 |
| E2E-OPT-002 | Edit name + PageColor → wizard picks up new color | P1 |
| E2E-OPT-003 | Deactivate in-use → 409 ProfileTypeInUse | P0 |
| E2E-OPT-004 | Cross-UserType id rejected (Visitor id on Other route) | P0 |

Scenario bodies are byte-identical to E2E-VPT-001..004 with these
substitutions: route `/admin/profile-types/other`, UserType=Other,
consuming wizard `/admin/others`, namespace `OPT`.

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
