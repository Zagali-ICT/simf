# Verify OTP (Web) — `/login/verify`

| | |
|--|--|
| **Route** | `/login/verify` |
| **Audience** | Mid-sign-in visitor |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/auth/verify-otp` |
| **Source** | [`OtpVerify.razor`](../../../src/Website/SIMF.Web/Components/Pages/Auth/OtpVerify.razor) + [`OtpVerify.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Auth/OtpVerify.razor.cs) |
| **Last reviewed** | 2026-07-06 |

## 1. Purpose

Visitor-side TOTP / email-OTP verification step. Mirrors the CP `/login/totp`
flow but accepts both authenticator-app TOTP and (if the visitor opted into
email-OTP at registration) a 6-digit email code.

## 11. E2E

| Scenario | ID |
|----------|----|
| Valid TOTP → /account | E2E-WEB-OTP-001 |
| Wrong code → bilingual toast | E2E-WEB-OTP-002 |
| Email-OTP path → code emailed and verified | E2E-WEB-OTP-003 |

## Changelog

- 2026-07-06 (D-632) — C# moved to an `OtpVerify.razor.cs` code-behind partial
  (Website clean-code, Phase 5); behaviour unchanged.

_Last reviewed:_ 2026-07-06 by Claude (D-632).
