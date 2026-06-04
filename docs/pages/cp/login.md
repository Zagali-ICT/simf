# Sign in (CP) — `/login`

| | |
|--|--|
| **Route** | `/login` |
| **Layout** | Auth layout (no nav rail) |
| **Audience** | Anyone (unauthenticated) |
| **Auth** | Anonymous |
| **Pattern** | Auth form. Two-column landing with branding aside + email/password form. |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/auth/sign-in` → forwards to API `POST /api/v1/auth/sign-in` |
| **Source** | [`SignIn.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/SignIn.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Entry point for every CP user. Validates email + password; on success
redirects to `/login/totp` (or `/account/totp-pairing` for first-time
users). Failure surfaces a bilingual error in a `SimfAlert`.

## 4. UI

- Branding aside: logo, "2026 Control Panel", "Royal Saudi Naval Forces".
- Main: `العربية` toggle, theme toggle, `Sign in` heading + form
  (Email, Password with show-password toggle, Forgot-password link,
  Sign-in button).

## 6. Validation + errors

- **Invalid credentials** → bilingual `Account.SignIn.Error.Invalid`.
- **PendingApproval account** → explicit `Account.SignIn.Error.Pending` toast
  + link to `/auth/pending`.
- **Rejected account** → redirect to `/auth/rejected`.
- **Email change required** / **Password change required** flags from
  the server route to the appropriate setup page.

## 7. Edge cases

- **Rate limit hit** (5 wrong attempts / 5 min via `RequireRateLimiting("auth")`)
  → 429 + retry-after toast.
- **Account locked** (10 failed attempts / 30 min) → 423 + bilingual
  lockout message.
- **2FA required but not yet paired** → redirect to `/account/totp-pairing`.

## 10. Use cases

UC-AUTH-SIGNIN, UC-AUTH-SIGNIN-PENDING, UC-AUTH-SIGNIN-REJECTED,
UC-AUTH-SIGNIN-LOCKED.

## 11. E2E

| Scenario | ID |
|----------|----|
| Valid credentials → redirect to /login/totp | E2E-LGN-001 |
| Wrong password → SimfAlert | E2E-LGN-002 |
| Pending account → SimfAlert + /auth/pending link | E2E-LGN-003 |
| Rate limit after 5 failed → 429 | E2E-LGN-004 |
| Show-password toggle reveals password | E2E-LGN-005 |
| RTL render | E2E-LGN-006 |

## 12. Related

- Companion pages: [`login-totp.md`](login-totp.md), [`login-recovery.md`](login-recovery.md), [`forgot-password.md`](forgot-password.md), [`auth-pending.md`](auth-pending.md), [`auth-rejected.md`](auth-rejected.md).
- D-121 (cookie-refresh handler keeps the session fresh after sign-in).

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 4).
