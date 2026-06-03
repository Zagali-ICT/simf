# Page 006 — Function (التحقق بالبريد · Email verification)

What the user does on this screen, and the gate that controls access to it.

## Privilege / auth gate
| | |
|---|---|
| App privilege | **Anonymous** — the user is mid sign-up and holds **no access token yet**. |
| How reached | Forwarded automatically from screen #5 (sign-up form) after the account is created in `Registered` state and the verification email is queued. The email address is carried in the navigation arguments. |
| Backend auth | Both endpoints this screen calls are `AllowAnonymous` and rate-limited (`auth` limiter). Identity is asserted by **email + the emailed 6-digit code**, not by a token. |
| Exit on success | Account moves `Registered → EmailVerified`; the user proceeds to the next sign-up step (sign-in / complete-profile per the flow). |

## Elements on the screen
| Element | Purpose |
|---|---|
| Title **التحقق بالبريد / Email verification** | Screen heading. |
| Subtitle | "We sent a 6-digit code to **{email}**" / "أرسلنا رمزًا من 6 أرقام إلى **{email}**" — echoes the masked/target address. |
| 6-box OTP input | One digit per box, numeric keyboard, auto-advance, paste-fills-all. |
| **Verify** button (تحقّق) | Primary action — submits `email + code`. Disabled until 6 digits are entered. |
| **Resend code** link (إعادة إرسال الرمز) | Requests a fresh code; disabled during a cooldown countdown. |
| Cooldown text | "Resend in {n}s" / "إعادة الإرسال خلال {n} ث" while the resend cooldown is active. |
| Inline error region | Bilingual server/validation message (wrong code, expired, too many attempts). |
| Back affordance | Returns to the sign-up form (#5). |

## Step by step
1. The user arrives with their email already known and a code already emailed.
2. The user opens their inbox and reads the **6-digit** code.
3. The user types (or pastes) the 6 digits; the **Verify** button enables once all 6 boxes are filled.
4. The user taps **Verify** → `POST /app/auth/verify-email { email, code }`.
   - **Success** → toast "Email verified" / "تم التحقق من البريد"; navigate forward in the sign-up flow.
   - **Wrong code** → inline "The verification code is not correct" / "رمز التحقق غير صحيح"; the boxes clear for re-entry; an attempt is consumed.
   - **Expired** → inline "The verification code has expired. Request a new one." / "انتهت صلاحية رمز التحقق. اطلب رمزًا جديدًا." — the user must **Resend**.
   - **Too many attempts** → inline "Too many incorrect attempts. Request a new code." / "محاولات غير صحيحة كثيرة. اطلب رمزًا جديدًا." — the user must **Resend**.
5. If the code never arrived or has expired, the user taps **Resend code** → `POST /app/auth/resend-code { email }`.
   - A fresh code is emailed; the previous code is invalidated; the cooldown timer restarts using `codeExpiresInSeconds` from the response.
   - If the per-account resend cap is hit, the screen shows the bilingual cap message and keeps the button disabled until allowed.

## Acceptance criteria
- **Verify** is disabled until exactly 6 digits are present; non-digits are rejected at input.
- A correct, unexpired code advances the flow and the account becomes `EmailVerified`.
- A wrong code shows the bilingual inline error and clears the input without leaving the screen.
- An expired / attempt-capped code steers the user to **Resend**, not to retry the same code.
- **Resend** invalidates any prior code, restarts the cooldown, and re-enables **Verify** for the new code.
- All copy (labels, errors, toasts) renders in both AR and EN and mirrors correctly under RTL.
- No access token is required or stored on this screen.
