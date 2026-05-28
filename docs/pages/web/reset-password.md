# Reset password (Web) — `/reset-password`

| | |
|--|--|
| **Route** | `/reset-password` |
| **Audience** | Visitor with a fresh reset code |
| **Auth** | Anonymous (code is the bearer) |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/auth/reset-password` |
| **Source** | [`ResetPassword.razor`](../../../src/Website/SIMF.Web/Components/Pages/Auth/ResetPassword.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Set a new password using the 6-digit code from the forgot-password email.
Password complexity: min 12 chars, ≥ 1 digit, ≥ 1 upper, ≥ 1 lower, ≥ 1
special. Success → toast + redirect to `/login`.

## 11. E2E

| Scenario | ID |
|----------|----|
| Valid code + strong password → success + redirect | E2E-WEB-RST-001 |
| Wrong code → bilingual error | E2E-WEB-RST-002 |
| Weak password → bilingual complexity message | E2E-WEB-RST-003 |
| Expired code (>15 min) → invalid | E2E-WEB-RST-004 |

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 5).
