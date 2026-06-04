# Forgot password (Web) — `/forgot-password`

| | |
|--|--|
| **Route** | `/forgot-password` (Website) |
| **Audience** | Anyone (visitor) |
| **Auth** | Anonymous |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/auth/forgot-password` (same shape as CP — always-200 to prevent enumeration) |
| **Source** | [`ForgotPassword.razor`](../../../src/Website/SIMF.Web/Components/Pages/Auth/ForgotPassword.razor) |
| **Last reviewed** | 2026-05-28 |

Same flow as the CP — visitor types email, server emails a single-use
6-digit code with 15-min TTL, UI always shows success. Code goes to
`/reset-password`.

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 5).
