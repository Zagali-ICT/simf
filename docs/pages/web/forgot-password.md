# Forgot password (Web) — `/forgot-password`

| | |
|--|--|
| **Route** | `/forgot-password` (Website) |
| **Audience** | Anyone (visitor) |
| **Auth** | Anonymous |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/auth/forgot-password` (same shape as CP — always-200 to prevent enumeration) |
| **Source** | [`ForgotPassword.razor`](../../../src/Website/SIMF.Web/Components/Pages/Auth/ForgotPassword.razor) + [`ForgotPassword.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Auth/ForgotPassword.razor.cs) |
| **Last reviewed** | 2026-07-06 |

Same flow as the CP — visitor types email, server emails a single-use
6-digit code with 15-min TTL, UI always shows success. Code goes to
`/reset-password`.

## Changelog

- 2026-07-06 (D-632) — C# moved to a `ForgotPassword.razor.cs` code-behind
  partial (Website clean-code, Phase 5); behaviour unchanged.

_Last reviewed:_ 2026-07-06 by Claude (D-632).
