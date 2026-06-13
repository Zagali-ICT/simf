# Page 006 — Function (التحقق بالبريد · Email verification)

_Last updated: 2026-06-13 — as-built conformance pass (D-364/D-369)._

What the user does on this screen, and the gate that controls access to it.

## Privilege / auth gate
| | |
|---|---|
| App privilege | **Anonymous** — the user is mid sign-up and holds **no access token yet**. |
| How reached | Forwarded automatically from screen #5 (sign-up form) after the sign-up call succeeds (same generic 201 for a new account and a D-198 restart/deflect). The email address is carried as the **`?email=` query parameter** on `/sign-up/otp`. |
| Backend auth | Both endpoints this screen calls are `AllowAnonymous` and rate-limited (`auth` limiter). Identity is asserted by **email + the emailed 6-digit code**, not by a token. |
| Exit on success | Account moves `Registered → EmailVerified`; the app routes to **`/sign-in`** (verify-email issues no session — the profile step needs a token, so the verified user signs in next). |

## Elements on the screen
| Element | Purpose |
|---|---|
| Header title **التحقق بالبريد / Email verification** | Screen heading (custom header band, no Material app bar — D-364). |
| Heading **أدخل رمز التحقق / Enter the verification code** | Instruction above the boxes, under the gold-ringed mail mark (`OtpMark`). |
| Sent-to caption | "أرسلنا رمز التحقق إلى / We sent a verification code to" with the target address beneath it in gold (no digit count — D-373). |
| 6-box OTP input (`OtpCodeBoxes`) | Six segmented boxes over **one invisible capture field** — numeric keyboard, digits-only, max 6; tap anywhere to focus; paste fills it. |
| **Verify** button (تحقّق) | Primary gold action pinned at the bottom — submits `email + code`. Disabled until 6 digits are entered. |
| **Resend** footer (لم يصلك الرمز؟ **إعادة الإرسال** / Didn't get the code? **Resend**) | Requests a fresh code; disabled during a cooldown countdown or while a call is in flight. |
| Cooldown text | "إعادة الإرسال خلال / Resend in" + gold **`mm:ss`** — shown only while the resend cooldown is active. |
| Inline error region | Server/validation message in the request's language (wrong code, expired, too many attempts, network). |
| Back affordance | Chevron in the header — pops back to the sign-up form (#5); falls back to `/sign-up/form` when there is nothing to pop. Disabled while a call is in flight. |

## Step by step
1. The user arrives with their email already known (the `?email=` query parameter) and a code already emailed.
2. The user opens their inbox and reads the **6-digit** code.
3. The user types (or pastes) the 6 digits into the capture field behind the boxes; the **Verify** button enables once 6 digits are present (submitting from the keyboard also verifies).
4. The user taps **Verify** → `POST /app/auth/verify-email { email, code }`.
   - **Success** → toast "Email verified" / "تم التحقق من البريد"; navigate to **`/sign-in`** (verify-email issues no session).
   - **Wrong code** → inline "The verification code is not correct" / "رمز التحقق غير صحيح"; the field clears for re-entry; an attempt is consumed.
   - **Expired** → inline "The verification code has expired. Request a new one." / "انتهت صلاحية رمز التحقق. اطلب رمزًا جديدًا." — the user must **Resend**.
   - **Too many attempts** → inline "Too many incorrect attempts. Request a new code." / "محاولات غير صحيحة كثيرة. اطلب رمزًا جديدًا." — the user must **Resend**.
   - On **every** verify failure (including network) the entered code is cleared.
5. If the code never arrived or has expired, the user taps **إعادة الإرسال / Resend** → `POST /app/auth/resend-code { email }`.
   - A fresh code is emailed; the previous code is invalidated; the cooldown timer starts from `codeExpiresInSeconds` in the response (fallback 60 s) and the gold `mm:ss` countdown appears. No cooldown runs before the first resend.
   - If the per-account resend cap is hit (429), the screen shows the cap message; no cooldown starts, so the link re-enables — the server keeps rejecting until the window passes.

## Acceptance criteria
- **Verify** is disabled until exactly 6 digits are present; non-digits are rejected at input (digits-only formatter).
- A correct, unexpired code advances the flow (toast → `/sign-in`) and the account becomes `EmailVerified`.
- A wrong code shows the inline server error and clears the input without leaving the screen.
- An expired / attempt-capped code steers the user to **Resend**, not to retry the same code.
- **Resend** invalidates any prior code and starts the `mm:ss` cooldown; **Verify** works for the new code.
- All client copy comes from `AppL10n` (AR + EN) and mirrors correctly under RTL; server errors arrive in the request's language (`Accept-Language`).
- No access token is required or stored on this screen.
