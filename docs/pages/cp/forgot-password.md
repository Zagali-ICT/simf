# Forgot password — `/forgot-password`

| | |
|--|--|
| **Route** | `/forgot-password` |
| **Audience** | Anyone (unauthenticated) |
| **Auth** | Anonymous |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/auth/forgot-password` → API `POST /api/v1/auth/forgot-password` (always returns 200 to avoid email enumeration) |
| **Source** | [`ForgotPassword.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/ForgotPassword.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Starts the password-reset flow. User types email → submit → server emails
a single-use 6-digit code (15-minute TTL, 3-use rate limit per email
window). UI always shows the same success message regardless of whether
the email exists — prevents enumeration.

## 7. Edge cases

- **Email doesn't exist** → still shows success (anti-enumeration).
- **Rate limited per IP** (`auth-email-limit` middleware) → 429 with
  retry-after.

## 11. E2E

| Scenario | ID |
|----------|----|
| Submit valid email → success message + email arrives | E2E-FPW-001 |
| Submit unknown email → success message (no leak) | E2E-FPW-002 |
| Rate limit after 3 submits / minute | E2E-FPW-003 |

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 4).
