# Reset user 2FA — `/admin/reset-2fa`

| | |
|--|--|
| **Route** | `/admin/reset-2fa` |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Admins.ResetTwoFactor)]` |
| **Pattern** | SimfBanner (D-132) + search-and-action form (no grid). |
| **Status** | ✅ Real |
| **Backend** | `GET /account/api/admin/users/search?q={email}`, `POST /account/api/admin/users/{id}/reset-2fa` (decision D-041) |
| **Source** | [`ResetTwoFactor.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ResetTwoFactor.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Admin-driven 2FA reset for users who lost their authenticator + recovery
codes. Wipes the target's authenticator, recovery codes, and active
sessions, and emails the target out-of-band (D-041). The target must
re-pair on next sign-in.

## 4. UI

- `<SimfBanner Title="@L[\"Admin.ResetTwoFactor.Title\"]" />`
- Search field (email substring) → result list → per-row **Reset 2FA**
  button → confirmation modal → `POST .../reset-2fa`.
- Success state: SimfAlert green, audit row written by the API
  (`Admin.UserTwoFactorReset`).

## 7. Edge cases

- **Email not found** → "No user matches" toast.
- **Target is yourself** → server-side guard rejects (self-reset must be
  done via `/account/profile`).
- **Target already has no 2FA** → still succeeds (idempotent — wipe is
  a no-op in that case).

## 10. Use cases

UC-2FA-SEARCH-USER, UC-2FA-RESET-USER.

## 11. E2E

| Scenario | ID |
|----------|----|
| Reset a normal user → success + email sent | E2E-2FA-001 |
| Self-reset rejected | E2E-2FA-002 |
| Email not found → no rows | E2E-2FA-003 |

## 12. Related

- Decisions: D-041 (original), D-132 (banner swap).

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 4).
