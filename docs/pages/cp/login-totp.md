# Verify TOTP (CP) — `/login/totp`

| | |
|--|--|
| **Route** | `/login/totp` |
| **Audience** | Mid-sign-in user (cookie established, TOTP pending) |
| **Auth** | Cookie present from `/login` step 1 |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/auth/verify-totp` → API `POST /api/v1/auth/totp/verify` |
| **Source** | [`TotpVerify.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/TotpVerify.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Second factor for sign-in. Six-digit numeric input; server validates
against the user's paired TOTP secret with the standard 30-second window
+ 1-step tolerance for clock drift.

## 7. Edge cases

- **Code expired** → "Invalid verification code" toast; user reads next code.
- **Lost phone** → "Use a recovery code instead" link routes to
  [`/login/recovery`](login-recovery.md).
- **Clock drift > 60s** → reliably fails; user must fix their device clock.

## 10. Use cases

UC-AUTH-TOTP-VERIFY, UC-AUTH-TOTP-USE-RECOVERY.

## 11. E2E

| Scenario | ID |
|----------|----|
| Valid code → redirect to / | E2E-TPV-001 |
| Wrong code → toast | E2E-TPV-002 |
| Recovery-code link → /login/recovery | E2E-TPV-003 |

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 4).
