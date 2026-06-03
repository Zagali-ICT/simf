# Page 006 — Logic (التحقق بالبريد · Email verification)

Client + server logic for the sign-up email-OTP step. The authoritative request /
response shapes and error codes are in [Page_006_API.md](Page_006_API.md); the user-facing
steps are in [Page_006_Function.md](Page_006_Function.md).

## L-1 — Inputs the screen owns
| Input | Source | Rule |
|---|---|---|
| `email` | Navigation argument from screen #5 (not user-editable here). | Required, valid email, ≤ 256 chars (server `VerifyEmailRequestValidator` / `ResendCodeRequestValidator`). |
| `code` | The 6 OTP boxes. | Exactly 6 digits, regex `^\d{6}$` (server-enforced; mirror client-side). Digits only. |

## L-2 — Client logic
- Concatenate the 6 boxes into one string; **Verify** stays disabled until `code.length == 6`.
- Auto-advance focus on each digit; backspace moves focus back; a 6-digit paste fills all boxes.
- On **Verify**: client-validate `^\d{6}$`, then `POST /app/auth/verify-email`. Show a spinner on the button; disable inputs while in flight.
- On **Resend**: `POST /app/auth/resend-code`; start a countdown from the returned `codeExpiresInSeconds`; keep **Resend** disabled until it elapses (also re-armed by any server cap message).
- No token is read or written; nothing is persisted locally beyond the in-memory `email` + transient UI state.

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
| 1 | No account for `email` | `404 auth.account_not_found` |
| 2 | Account not in `Registered` (already verified) | `400 auth.code_invalid` — "This account's email address is already verified." |
| 3 | No outstanding unconsumed `EmailVerification` code | `400 auth.code_invalid` — "No verification code is outstanding. Request a new one." |
| 4 | `now >= code.ExpiresAt` | `400 auth.code_expired` |
| 5 | `code.AttemptCount >= MaxCodeAttempts` | `400 auth.code_invalid` — "Too many incorrect attempts. Request a new code." |
| 6 | Submitted code ≠ stored code | `AttemptCount++`, `400 auth.code_invalid` — "The verification code is not correct." |
| ✓ | All pass | Consume code, flip state, return success. |

## L-5 — Server guards (resend-code)
- Same account-existence (1) and `Registered`-only (2) guards as above.
- Then an **account-scoped resend cap** (`EnsureVerificationCodeCapNotReachedAsync`) — independent of the per-IP `auth` rate limiter; resend abuse is keyed on the email. If reached, a bilingual cap error is thrown.
- On success: issue a fresh `EmailVerification` code (invalidating the prior one), enqueue the verification email, return `{ email, codeExpiresInSeconds }`.

## L-6 — Validation (shared client/server)
- `email`: NotEmpty + EmailAddress + MaxLength(256).
- `code`: NotEmpty + `Matches(^\d{6}$)`.
- Client mirrors these so obviously-bad input never round-trips; the server remains the source of truth.

## L-7 — Empty / error / RTL handling
- **Empty**: with `code.length < 6`, **Verify** is disabled (no call is made). There is no list/empty-collection state on this screen.
- **Wrong code**: clear the boxes, refocus box 1, show the bilingual inline error; the attempt was already counted server-side.
- **Expired / attempt-cap**: show the bilingual error and visually steer to **Resend** (retrying the same code cannot succeed).
- **Resend cap**: show the cap message and keep **Resend** disabled.
- **Network / 500**: generic bilingual "Something went wrong, please try again" / "حدث خطأ ما، يرجى المحاولة مرة أخرى"; inputs stay filled so the user can retry.
- **Rate limited (429)**: surface the bilingual throttle message; keep the user on-screen.
- **RTL**: under Arabic the layout mirrors; the **OTP boxes stay left-to-right** (digits are LTR) while labels, subtitle and buttons mirror.

## L-8 — Dependencies
- `POST /app/auth/verify-email` and `POST /app/auth/resend-code` (both **built**).
- Outbound email queue (delivers the 6-digit code) — out of scope for this screen; the screen only triggers the resend.
- No new permission code (anonymous sign-up endpoints; permission system does not apply).
