# Change email (تغيير البريد الإلكتروني) - `/auth/change-email`

| | |
|--|--|
| **Route** | `/auth/change-email` (route name `changeEmail`, `RouteNames.changeEmail` → `ChangeEmailScreen`, aux auth route #0) - pushed from More → Settings |
| **Layout** | Custom navy `Scaffold` - decorative sweep + back/title header (`AccountSubHeader`) + pinned gold CTA + OTP boxes (the shared auth chrome, no `SimfPageShell`) |
| **Surface** | Mobile App (Flutter) |
| **Audience** | Any **signed-in, Approved** account (the row is hidden for a guest) |
| **Auth** | **Signed-in + Approved.** The More row is wrapped in `if (signedIn)`; both endpoints carry the `RequireApprovedAccount` policy + the `auth` per-IP rate limiter + the `auth-email` per-email limiter (keyed on the target `newEmail`). The one-time code is emailed to the NEW address, so controlling that inbox is the proof of ownership. On confirm the account's current password is re-checked as well. |
| **Pattern** | Build #24 self-service email change. Two phases in one screen: (1) enter the new address → a code is emailed to it (`send-otp`); (2) enter the code → confirm (`confirm`). On success the server rolls the security stamp + revokes sessions, so the app forces a fresh sign-in. Mirrors the emailed-OTP design of biometric step-up and the forced-re-login of reset-password (D-659). |
| **Figma node** | **none** - reuses the shared navy auth chrome, like [biometric-step-up](biometric-step-up/README.md) and [reset-password](reset-password/README.md), which have no bound frame either (§13.5 unbound; render preserved) |
| **Status** | 🟢 Screen built (Build #24) |
| **Backend endpoints** | `POST /api/v1/app/auth/change-email/send-otp` (issue; body `{"newEmail"}` → `{"maskedNewEmail","expiresInSeconds","resendCooldownSeconds"}`) · `POST /api/v1/app/auth/change-email/confirm` (complete; body `{"newEmail","code","currentPassword"}` → `{"emailChanged":true}`) |
| **Source file** | Flutter `features/account/change_email_screen.dart` (`ChangeEmailScreen`) + `features/account/data/email_change_repository.dart` (`EmailChangeRepository`, `EmailChangeCodeSent`). Backend `SIMF.Application/IdentityAccess/EmailChangeService.cs` + `SIMF.Api/Endpoints/Auth/ChangeEmailEndpoints.cs`. |
| **Tests** | [`docs/tests/e2e/mobile-change-email.md`](../../tests/e2e/mobile-change-email.md) (`E2E-MCE-001..013`); widget `test/features/account/change_email_screen_test.dart`; golden `test/golden/change_email_golden_test.dart` (`change_email_enter.png` + `change_email_code.png`); backend `tests/SIMF.Api.Tests/ChangeEmailTests.cs`. |
| **Last reviewed** | 2026-07-22 |

---

## 1. Purpose

The change-email screen lets a signed-in user move their **login email** to a new
address, themselves, without an admin. It is a sensitive, identity-level action, so
it is not a single "save": a **one-time code is emailed to the NEW address**, and
the change completes only once the user submits that code, proving they control the
new inbox. On a successful confirm the server rolls the security stamp and revokes
every session, so the app cannot stay signed in on the old token: it signs the user
out and routes to sign-in for a genuine fresh login.

## 2. Audience + permissions

- **Who can reach it:** any **signed-in, Approved** account, from the More tab →
  "الإعدادات / Settings" section → "تغيير البريد الإلكتروني / Change email" row.
  The row is wrapped in `if (signedIn)`, so a guest never sees it (the same guard
  as the Reset password and Notifications rows).
- **Endpoint gate:** both `send-otp` and `confirm` carry
  `Policies(RequireApprovedAccount)` and both rate limiters (`auth` per-IP +
  `auth-email` per-email, keyed on the target `newEmail`). A guest / pending
  caller is rejected before the handler runs.
- **Account-safety:** a disabled or unknown subject on an otherwise valid token is
  reported as `AUTH_ACCOUNT_NOT_FOUND` (401, "Account unavailable." / "الحساب غير
  متاح."), without leaking whether the account exists.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Phase 1 (enter new email) | `test/golden/goldens/change_email_enter.png` | ✅ golden (Arabic) |
| Phase 2 (verify code) | `test/golden/goldens/change_email_code.png` | ✅ golden (Arabic) |
| Error (taken / invalid) | `docs/screenshots/change-email-error.png` | _pending_ |

> No Figma reference frame - the screen reuses the shared navy auth chrome.

## 4. UI affordances

### 4.1 Header
Back chevron + centred title **تغيير البريد الإلكتروني / Change email**
(`AccountSubHeader`). The back action on phase 2 steps back to phase 1 (clearing the
code + error); on phase 1 it pops the screen.

### 4.2 Phase 1 - enter the new email
| Element | Source | Notes |
|---------|--------|-------|
| Mark | `OtpMark(icon: alternate_email)` | brand OTP mark |
| Heading | `changeEmailHeading` | "Enter your new email address. We'll send a verification code to it to confirm." |
| Current email | `changeEmailCurrentLabel` + the live session email | read-only, LTR, gold |
| New email field | `changeEmailNewLabel` (`NaviFormField`) | LTR, `emailAddress` keyboard, `maxLength: 256` |
| CTA | `changeEmailSendButton` | "إرسال الرمز / Send code"; disabled until the field is non-blank |

### 4.3 Phase 2 - verify the code
| Element | Source | Notes |
|---------|--------|-------|
| Mark | `OtpMark(icon: mail_outline)` | |
| Heading | `changeEmailVerifyHeading` | "تأكيد البريد الجديد / Confirm new email" |
| Sent-to line | `otpSentToPrefix` + `maskedNewEmail` | "We sent a code to" + masked address (gold, LTR) |
| Code boxes | `OtpCodeBoxes` (6 digits) | shared OTP widget |
| Current password | `changeEmailPasswordLabel` (`NaviFormField`) | below the OTP boxes; obscured, with a `NavyPasswordToggle` show/hide; re-authenticates the owner (not just a held session) before the identity change |
| Countdown | `otpResendCountdown` + `mm:ss` | starts from `resendCooldownSeconds` (default 120) |
| Resend row | `otpDidntReceive` + `otpResendAction` | inert until the countdown hits 00:00 |
| CTA | `verifyButton` | "تحقّق / Verify"; disabled until BOTH a 6-digit code AND a non-empty password are entered |

## 5. Data flow

```
More → Settings → "تغيير البريد الإلكتروني" (signedIn only) → /auth/change-email
  Phase 1: type new email → _sendCode()
    → EmailChangeRepository.sendOtp(newEmail)
    → POST /app/auth/change-email/send-otp {"newEmail"}
    → {"maskedNewEmail","expiresInSeconds","resendCooldownSeconds"}
    → advance to phase 2, start the countdown
  Phase 2: type the emailed code + current password → _confirm()
    → EmailChangeRepository.confirm(newEmail, code, currentPassword)
    → POST /app/auth/change-email/confirm {"newEmail","code","currentPassword"}
    → server re-checks the password (before the code), then the code
    → {"emailChanged": true}   (server rolls the stamp + revokes sessions,
       and emails the OLD address the EmailChangedNotice security alert)
    → authController.signOut() → context.goNamed(signIn) + success toast
```

The server stores only a keyed hash bound to the target address
(`Hash("{code}:{newEmail}")`), so a code emailed for one address can never confirm a
change to a different one, and the frozen `AccountCode` table needs no new column.

## 6. Validation

**Client (before any round-trip), on the new-email field:**
- blank → `requiredField` ("هذا الحقل مطلوب / This field is required").
- not an email → `invalidEmail` ("بريد إلكتروني غير صالح / Invalid email").
- equal to the current email (case-insensitive) → `changeEmailSameAsCurrent`
  ("هذا هو بريدك الإلكتروني الحالي بالفعل. / This is already your email address.").

**Server:**

| Condition | HTTP | Error code | Message (EN / AR) |
|-----------|------|-----------|--------------------|
| New email equals current | 400 | `AUTH_EMAIL_UNCHANGED` | "This is already your email address." / "هذا هو بريدك الإلكتروني الحالي بالفعل." |
| New email held by another account - **send-otp** | 200 | none (DEFLECT) | generic success shape (masked email + cooldown), NO code issued; the real owner is emailed the `AccountExists` notice. Enumeration-resistant, mirrors the D-198 sign-up deflect. |
| New email held by another account - **confirm** | 409 | `AUTH_EMAIL_ALREADY_REGISTERED` | "An account with this email address already exists." / "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل." (kept: the caller must already hold a valid code, so it is not an enumeration oracle) |
| Wrong current password (confirm; checked BEFORE the code, code NOT consumed) | 401 | `AUTH_INVALID_CREDENTIALS` | "The password is not correct." / "كلمة المرور غير صحيحة." |
| More than 5 codes in an hour | 429 | `RATE_LIMIT_EXCEEDED` | "Too many verification codes have been requested. Try again later." / "تم طلب رموز تحقق كثيرة. حاول مرة أخرى لاحقًا." |
| Wrong / no / attempt-capped code | 400 | `AUTH_CODE_INVALID` | "The verification code is not correct." / "رمز التحقق غير صحيح." (and the no-code / attempt-cap variants) |
| Code older than 10 minutes | 400 | `AUTH_CODE_EXPIRED` | "The verification code has expired. Request a new one." / "انتهت صلاحية رمز التحقق. اطلب رمزًا جديدًا." |
| Disabled / unknown subject | 401 | `AUTH_ACCOUNT_NOT_FOUND` | "Account unavailable." / "الحساب غير متاح." |
| Bad email format / non-6-digit code | 400 | `VALIDATION_FAILED` | request-shape validation |

## 7. Edge cases + known limitations

- **Forced re-login is intentional.** A confirmed change rolls the security stamp +
  revokes refresh tokens, so the app signs out and routes to sign-in. This is not an
  error - it is the identity-change protection (same as reset-password, D-659).
- **Current-password re-auth on confirm (Build #24 hardening).** Confirm requires the
  account's current password (sent as `currentPassword`), re-checked BEFORE the code.
  An access token proves a held session, not the owner's intent, so - as
  change-password does - a leaked token alone cannot seize the account by pointing
  the login email at an attacker's inbox. A wrong password returns 401
  `AUTH_INVALID_CREDENTIALS` and does NOT consume the code (nor bump its attempt
  count), so the user can retry with the same code.
- **Enumeration-resistant send-otp (Build #24 hardening).** send-otp no longer 409s a
  target already held by another account: it returns the SAME 200 success shape as a
  free address but issues NO code, and the real owner is emailed the `AccountExists`
  notice warning them someone tried to move an account onto their address (mirrors the
  D-198 sign-up deflect; the per-account 5/hour cap runs before this uniqueness
  check). **UX cost:** a user who typos their new address as someone else's registered
  email sees "code sent" and then, with no code arriving, fails at confirm - the
  accepted price of not leaking which emails exist. (confirm still 409s a taken
  address, since there the caller must already hold a valid bound code.)
- **Old-address security alert (Build #24 hardening).** On a successful confirm the
  server emails the PREVIOUS address the `EmailChangedNotice` template (subject "SIMF
  login email changed"), stating the login email moved to the masked new address and
  to contact support if it was not them. Because a takeover revokes the victim's
  sessions and the new login email is the attacker's, this out-of-band alert to the
  address they still control is how they learn of it. A mail hiccup here never
  re-throws (the change already committed).
- **Newest code only.** Requesting again (resend, or a change of target address)
  consumes the previous unconsumed code, so an old code no longer confirms.
- **Address taken between the two steps.** Uniqueness is re-checked on confirm, so an
  address that becomes taken after phase 1 is still rejected with the 409 at confirm.
- **Single-use under concurrency.** Only the caller that flips the code's `ConsumedAt`
  from null proceeds, so a double-submit applies the change exactly once.
- **CP twin (Build #24).** When an admin corrects a login email on the account-edit
  path (`AdminAccountService.UpdateAccountAsync`), the address is now marked
  `EmailConfirmed=false`, so it is re-verified at the owner's next sign-in via the
  email-OTP 2FA. Sign-in gates on `AccountState`, not `EmailConfirmed`, so this is
  not a lockout - see the visitors / others E2E catalogues.

## 8. i18n + RTL

All strings localized (AR/EN, Arabic-first) via `AppL10n`: the title, both headings,
the current / new labels, the send / verify CTAs, the same-as-current guard, the
success toast, plus the reused OTP strings (sent-to prefix, countdown, resend). Under
Arabic the header, labels, OTP boxes, resend row and CTA mirror right-to-left; the
current email, the masked new email and the 6-digit code carry an explicit
`TextDirection.ltr`. The golden locks the Arabic render.

## 9. Accessibility

- The pinned CTA is disabled (not just visually) until its phase's precondition is
  met (non-blank email / 6 digits), so an assistive-tech user is never offered a
  no-op button.
- The body + CTA are capped by `MaxWidthBody(560)`, keeping line length and tap
  targets comfortable on a tablet; layout respects `MediaQuery.textScaler`.
- Errors render as inline text near the field / boxes (not only colour), so the
  failure is conveyed without relying on colour alone.
- The masked recipient + code are LTR and read left-to-right even under an RTL UI.

## 10. Related E2E test scenarios

See [`docs/tests/e2e/mobile-change-email.md`](../../tests/e2e/mobile-change-email.md)
(`E2E-MCE-001..013`): the golden path (code + current password → forced re-login +
toast), same-as-current (client + server), malformed email (client), the
already-registered deflect (send-otp 200 no-code + owner warned; confirm still 409),
wrong code (400), expired code (400), resend after cooldown, the 5-per-hour cap
(429), the guest auth gate, RTL, the attempt-cap / no-outstanding-code branches,
the wrong-current-password 401 (code not consumed), and the old-address security
alert on success.

## 11. Related docs

- Biometric step-up (closest sibling, emailed-OTP re-verification): [`biometric-step-up/README.md`](biometric-step-up/README.md).
- Reset password (the forced-re-login precedent, D-659): [`reset-password/README.md`](reset-password/README.md).
- More hub (the entry point): [`more/README.md`](more/README.md).
- CP admin edit twin (`EmailConfirmed=false`): [`../../tests/e2e/cp-admin-visitors.md`](../../tests/e2e/cp-admin-visitors.md) (E2E-VIS-001) and [`../../tests/e2e/cp-admin-others.md`](../../tests/e2e/cp-admin-others.md) (E2E-OTH-005).

## 12. Changelog

| Date | Build | Change |
|------|-------|--------|
| 2026-07-22 | #24 | **Security hardening (after security review).** Three controls folded into the flow: **(1) current-password re-auth on confirm** - confirm now requires `currentPassword`, re-checked before the code; a wrong password returns 401 `AUTH_INVALID_CREDENTIALS` and does not consume the code (phase 2 gains a `changeEmailPasswordLabel` field with a show/hide toggle; Verify needs code + password). **(2) enumeration-resistant send-otp** - a target held by another account no longer 409s on send-otp; it returns the generic 200 success with no code and emails the real owner the `AccountExists` notice (confirm still 409s). Both endpoints also carry the `auth-email` per-email limiter; the per-account cap runs before the uniqueness check. **(3) old-address security alert** - a successful confirm emails the previous address the new `EmailChangedNotice` template ("SIMF login email changed"). |
| 2026-07-22 | #24 | Self-service change-email flow added: a signed-in user enters a new address (`POST /app/auth/change-email/send-otp`), receives a 6-digit code emailed to that address, and confirms it (`POST /app/auth/change-email/confirm`). On success the login email + username move, `EmailConfirmed=true`, the security stamp rolls and sessions revoke, so the app forces a fresh sign-in with `changeEmailSuccessToast`. Reached from More → Settings (signed-in only); reuses the navy auth chrome (unbound). The one-time code hash is bound to the target address; the frozen `AccountCode` table is unchanged. **CP tightening (same build):** the admin account-edit path now sets `EmailConfirmed=false` when it changes an email, so a corrected new-account address is re-verified at the owner's next sign-in via the email-OTP 2FA (not a lockout). |

---

_Last reviewed:_ 2026-07-22 by SIMF Team (Build #24 - change-email flow + CP edit `EmailConfirmed=false`; post-review security hardening: current-password re-auth on confirm, enumeration-resistant send-otp + `auth-email` per-email limiter, old-address `EmailChangedNotice` alert).
