# Page 006 — Logic (التحقق بالبريد · Email verification)

_Last updated: 2026-06-13 — as-built conformance pass (D-364/D-369)._

Client + server logic for the sign-up email-OTP step. The authoritative request /
response shapes and error codes are in [Page_006_API.md](Page_006_API.md); the user-facing
steps are in [Page_006_Function.md](Page_006_Function.md).

## L-1 — Inputs the screen owns
| Input | Source | Rule |
|---|---|---|
| `email` | The `?email=` query parameter on `/sign-up/otp`, set by screen #5 (not user-editable here). | Required, valid email, ≤ 256 chars (server `VerifyEmailRequestValidator` / `ResendCodeRequestValidator`). |
| `code` | The single invisible capture field behind the 6 rendered boxes (`OtpCodeBoxes`). | Exactly 6 digits — server regex `^\d{6}$`; the client guarantees the shape via the digits-only input formatter + `maxLength: 6` + a length-6 gate before submit. |

## L-2 — Client logic
- One `TextEditingController` feeds the six rendered boxes; **Verify** stays disabled until `code.length == 6` (and while a call is in flight).
- There is no per-box focus model: tapping anywhere on the row focuses the capture field; the gold highlight follows the caret; a 6-digit paste fills the field. Submitting from the keyboard verifies when 6 digits are present.
- On **Verify**: `_verify()` early-returns unless the trimmed code is 6 digits, then calls `AuthController.verifyEmail` → `POST /app/auth/verify-email`. The button shows a spinner and the field, back chevron and Resend disable while in flight. Success → `emailVerifiedToast` SnackBar + `goNamed(signIn)`. On **any** `AuthFailure` the field clears and the inline error shows — `networkErrorBody` for `NetworkUnavailable`, otherwise the server's `error.message` as-is.
- On **Resend**: `AuthController.resendCode` → `POST /app/auth/resend-code`; on success start a 1-second-tick countdown from the returned `codeExpiresInSeconds` (fallback **60 s** when the value is ≤ 0) and keep **Resend** disabled until it reaches 0. On failure (incl. the 429 cap) the inline error shows, no cooldown starts, and the entered code is left intact. No cooldown runs before the first resend.
- No token is read or written (verify-email issues no session); nothing is persisted locally beyond the in-memory `email` + transient UI state.

## L-3 — Server state transition (verify-email)
The account is created on screen #5 in **`Registered`**. A successful verify performs, in one transaction:

```
Registered  ──(correct, unexpired, under attempt-cap code)──▶  EmailVerified
            EmailConfirmed = true ; code.ConsumedAt = now ; user.UpdatedAt = now
```

After the transition the server dispatches an in-app + email **welcome** notification
(`NotificationKind.AccountWelcome`) — best-effort, never re-thrown. The endpoint returns
`{ email, emailVerified: true }`.

## L-4 — Server guards (verify-email), in order
| # | Condition | Result |
|---|---|---|
| 1 | No account for `email` | `404 AUTH_ACCOUNT_NOT_FOUND` |
| 2 | Account not in `Registered` (already verified) | `400 AUTH_CODE_INVALID` — "This account's email address is already verified." |
| 3 | No outstanding unconsumed `EmailVerification` code | `400 AUTH_CODE_INVALID` — "No verification code is outstanding. Request a new one." |
| 4 | `now >= code.ExpiresAt` | `400 AUTH_CODE_EXPIRED` |
| 5 | `code.AttemptCount >= MaxCodeAttempts` (= 5) | `400 AUTH_CODE_INVALID` — "Too many incorrect attempts. Request a new code." |
| 6 | Submitted code ≠ stored code | `AttemptCount++`, `400 AUTH_CODE_INVALID` — "The verification code is not correct." |
| ✓ | All pass | Consume code, flip state, return success. |

## L-5 — Server guards (resend-code)
- Same account-existence (1) and `Registered`-only (2) guards as above.
- Then an **account-scoped resend cap** (`EnsureVerificationCodeCapNotReachedAsync` — at most `MaxCodesPerWindow` = 5 `EmailVerification` codes per rolling 1-hour `ResendWindow`, shared with the D-198 unverified-restart sign-up path) — keyed on the account rather than the per-IP `auth` rate limiter, but it surfaces the **same `429 / RATE_LIMIT_EXCEEDED`** wire signature as the per-IP limiter, so the client cannot distinguish the two. If reached, a bilingual cap error is thrown.
- On success: issue a fresh `EmailVerification` code (invalidating the prior one), enqueue the verification email, dispatch the in-app `CredentialEmailVerificationResent` trail (best-effort), return `{ email, codeExpiresInSeconds: 600 }` (`CodeLifetime` = 10 min).

## L-6 — Validation (shared client/server)
- `email`: NotEmpty + EmailAddress + MaxLength(256).
- `code`: NotEmpty + `Matches(^\d{6}$)`.
- The client guarantees the code shape structurally (digits-only formatter + `maxLength: 6` + the length-6 submit gate) rather than running a regex mirror, so obviously-bad input never round-trips; the server remains the source of truth.

## L-7 — Empty / error / RTL handling
- **Empty**: with `code.length < 6`, **Verify** is disabled (no call is made). There is no list/empty-collection state on this screen.
- **Wrong code**: the capture field clears (no automatic refocus) and the inline server error shows; the attempt was already counted server-side. The clear-on-failure applies to **every** verify failure, not only wrong-code.
- **Expired / attempt-cap**: show the server error — its copy steers to **Resend** (retrying the same code cannot succeed).
- **Resend cap**: show the cap message; no cooldown starts, so **Resend** re-enables once the call finishes (the server keeps rejecting until the window passes).
- **Network**: `NetworkUnavailable` → `networkErrorBody` ("Could not reach the server. Check your internet connection and try again." / "تعذر الاتصال بالخادم. تحقق من الاتصال بالإنترنت وحاول مرة أخرى."). A failed verify still clears the entered code; a failed resend leaves it intact.
- **Rate limited (429) / other server errors**: the server's `error.message` (in the request's language) is surfaced inline; the user stays on-screen.
- **RTL**: under Arabic the layout mirrors; the **OTP boxes stay left-to-right** (digits are LTR) — as do the email line, the `mm:ss` countdown digits and the chevron glyph — while labels, captions and the footer mirror.

## L-8 — Dependencies
- `POST /app/auth/verify-email` and `POST /app/auth/resend-code` (both **built**), called through `AuthController.verifyEmail` / `AuthController.resendCode` (simf_auth_pkg).
- Shared `OtpCodeBoxes` / `OtpMark` widgets (`lib/features/auth/widgets/otp_code_boxes.dart` — extracted in D-369; the 2FA OTP screen is the second consumer).
- Outbound email queue (delivers the 6-digit code) — out of scope for this screen; the screen only triggers the resend.
- No new permission code (anonymous sign-up endpoints; permission system does not apply).
