# Sign in (Web) — `/login`

| | |
|--|--|
| **Route** | `/login` (Website) |
| **Audience** | Anyone (unauthenticated visitor) |
| **Auth** | Anonymous |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/auth/sign-in` (Website BFF, same shape as CP) |
| **Source** | [`SignIn.razor`](../../../src/Website/SIMF.Web/Components/Pages/Auth/SignIn.razor) + [`SignIn.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Auth/SignIn.razor.cs) |
| **Last reviewed** | 2026-07-06 |

## 1. Purpose

Visitor sign-in. Same email+password+TOTP flow as the CP, scoped to
visitor accounts (the API rejects Administrator-role accounts on this
audience — they must use the CP `/login`). Failure surfaces a bilingual
error; pending/rejected accounts get redirected to the matching state page.

## 6. Validation + errors

Identical envelope to [`cp/login.md`](../cp/login.md) — the difference is
the audience scope (Visitor only). An Administrator typing their CP
credentials here gets `Account.SignIn.Error.NotVisitor` toast directing
them to the CP `/login`.

## 11. E2E

| Scenario | ID |
|----------|----|
| Visitor signs in → /login/verify | E2E-WEB-LGN-001 |
| Admin tries to use Website login → "Use the Control Panel" toast | E2E-WEB-LGN-002 |
| Pending visitor → /account/pending | E2E-WEB-LGN-003 |
| Rate limit after 5 fails | E2E-WEB-LGN-004 |

## 12. Related

- TOTP verify: [`web/otp-verify.md`](otp-verify.md)
- Forgot password: [`web/forgot-password.md`](forgot-password.md)

## Changelog

- 2026-07-06 (D-632) — C# moved to a `SignIn.razor.cs` code-behind partial
  (Website clean-code, Phase 5); behaviour unchanged (SignInPageTests still green).

_Last reviewed:_ 2026-07-06 by Claude (D-632).
