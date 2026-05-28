# Use a recovery code — `/login/recovery`

| | |
|--|--|
| **Route** | `/login/recovery` |
| **Audience** | Mid-sign-in user without authenticator access |
| **Auth** | Cookie present from `/login` step 1 |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/auth/recovery-code` → API |
| **Source** | [`RecoveryCodeVerify.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/RecoveryCodeVerify.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Backup path when the authenticator app is unavailable (lost phone, etc.).
Accepts one of the 10 single-use recovery codes generated at TOTP pairing.
The code burns on use. After sign-in, the user should re-pair TOTP via
**My profile → Reset 2FA** (or have an administrator do it via
`/admin/reset-2fa`).

## 7. Edge cases

- **Invalid / already-used code** → toast.
- **No codes remaining** → user must contact an admin (admin resets 2FA
  via `/admin/reset-2fa` → user re-pairs on next sign-in).

## 11. E2E

| Scenario | ID |
|----------|----|
| Valid code → sign-in succeeds | E2E-RCV-001 |
| Used code → toast | E2E-RCV-002 |

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 4).
