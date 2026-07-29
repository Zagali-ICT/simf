# E2E test catalogue - `Change email` self-service flow (`changeEmail`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Build #24 - the signed-in,
> self-service change of a user's login email. A one-time code is emailed to the
> NEW address, and the change only completes once the user submits it, proving they
> control that inbox. Runner-agnostic Gherkin. The screen glue is widget-tested in
> `src/Mobile/simf_app/test/features/account/change_email_screen_test.dart` (+ the
> render-lock golden `test/golden/change_email_golden_test.dart`); the two endpoints
> + behaviour in `tests/SIMF.Api.Tests/ChangeEmailTests.cs`.
>
> **#24 (identity change → forced re-login):** on a successful confirm the server
> moves the login email + username together, sets `EmailConfirmed=true`, ROLLS the
> security stamp and REVOKES every refresh token. The caller's old tokens are then
> dead, so the app signs the user out and routes to sign-in for a genuine fresh
> login (the same protection reset-password applies, D-659). The one-time code hash
> is bound to the target address (`Hash("{code}:{newEmail}")`), so a code issued for
> one address can never confirm a change to a different one.

| | |
|--|--|
| **Route** | aux auth `/auth/change-email` (`RouteNames.changeEmail`) - pushed from the More tab (`/more`) → "الإعدادات / Settings" section → "تغيير البريد الإلكتروني / Change email" row, shown only when signed in |
| **APIs** | `POST /app/auth/change-email/send-otp` (issue, body `{"newEmail"}`) · `POST /app/auth/change-email/confirm` (complete, body `{"newEmail","code","currentPassword"}`) |
| **Surface** | Mobile (Flutter) - signed-in, Approved account |
| **Permissions** | `RequireApprovedAccount` (both endpoints) + the `auth` per-IP rate limiter + the `auth-email` per-email limiter keyed on the target `newEmail` (both endpoints); not a CP/admin action |
| **Auth setup** | A signed-in approved visitor. The verification code is emailed to the NEW address (the email channel), read from the test outbox / `SIMF_Identity.AccountCodes` at run time; **never a literal secret**. |
| **Figma node** | **none** - reuses the shared navy auth chrome (sweep + back/title header + pinned gold CTA + OTP boxes), like biometric-step-up and reset-password, which have no dedicated frame either |
| **Last reviewed** | 2026-07-22 (Build #24 - initial authoring + post-review security hardening: current-password re-auth on confirm, enumeration-resistant send-otp + per-email limiter, old-address alert) |

> **#24 design:** changing the login email is a sensitive, identity-level action.
> The flow is two phases in one screen: **(1)** enter the new address → the server
> emails a 6-digit code TO that address; **(2)** enter the code → the server confirms
> and applies the change. The code goes to the NEW inbox because controlling it is
> the proof of ownership. Guards mirror the emailed-OTP step-up: 10-minute code
> lifetime, single newest-code-only, a 5-attempts cap per code, and a 5-codes/hour
> per-account cap (a 120 s client resend cooldown on top). Because the confirm rolls
> the stamp + revokes sessions, the app cannot stay signed in on the old token: it
> signs out and sends the user to sign-in with the bilingual
> `changeEmailSuccessToast`. The **CP twin** is the admin account-edit path
> (`AdminAccountService.UpdateAccountAsync`): when an admin corrects a login email it
> now also sets `EmailConfirmed=false`, so the corrected address is re-verified at
> the owner's next sign-in via the email-OTP 2FA (sign-in gates on `AccountState`,
> not `EmailConfirmed`, so it is not a lockout) - see
> [`cp-admin-visitors.md`](cp-admin-visitors.md) (E2E-VIS-001) and
> [`cp-admin-others.md`](cp-admin-others.md) (E2E-OTH-005).

> **#24 security hardening (post-review, 2026-07-22):** three controls were folded
> into the flow after security review, all built + tested.
> **(A) Current-password re-auth on confirm.** The confirm step now REQUIRES the
> account's current password (body gains `currentPassword`); a stolen access token
> proves a held session, not the owner's intent, so - as change-password does -
> the password is re-checked BEFORE the code. A wrong password returns 401
> `AUTH_INVALID_CREDENTIALS` ("The password is not correct." / "كلمة المرور غير
> صحيحة.") and does NOT consume the code. In the app, phase 2 gains a
> "Current password / كلمة المرور الحالية" field (`changeEmailPasswordLabel`, with a
> show/hide toggle) below the OTP boxes; Verify stays disabled until BOTH a 6-digit
> code AND a non-empty password are entered.
> **(B) Enumeration-resistant send-otp (deflect).** send-otp no longer returns 409
> for a target already registered to ANOTHER account: it returns the SAME 200 success
> shape as a free address (masked new email + cooldown) but issues NO code, and the
> real owner of that address is emailed an "account already exists" notice (the
> `AccountExists` template) warning them someone tried to move an account onto it.
> So an authenticated caller cannot use the response to map which emails exist
> (mirrors the D-198 sign-up deflect). The per-account cap (5/hour) now runs BEFORE
> the uniqueness check. The **confirm** path still returns 409
> `AUTH_EMAIL_ALREADY_REGISTERED` for a taken address (there the caller must already
> hold a valid code bound to the address, so it is not an enumeration oracle). UX
> cost: a user who typos their new address as someone else's registered email sees
> "code sent" and then, with no code arriving, fails at confirm - the accepted price
> of enumeration resistance.
> **(C) Old-address security alert.** On a successful confirm the server emails the
> PREVIOUS address a security alert (new template `EmailChangedNotice`, subject "SIMF
> login email changed") telling them the login email was changed to {masked new
> email} and to contact support if it was not them. Because a takeover revokes the
> victim's sessions and points the new login email at the attacker, this out-of-band
> alert to the address they still control is how they learn of it.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MCE-001 | Golden path - new email → code emailed to it → enter code + current password → confirm → forced sign-out + sign-in + success toast | happy | P0 | authored ✓ (screen + backend tests) |
| E2E-MCE-002 | New email equals the current one - client validator blocks it; the server also returns 400 `AUTH_EMAIL_UNCHANGED` | edge | P0 | authored ✓ (screen + backend tests) |
| E2E-MCE-003 | Malformed new email - client `invalidEmail` validator blocks; no round-trip fires | error | P1 | authored ✓ (screen test) |
| E2E-MCE-004 | New email registered to another account → send-otp DEFLECTS (200 generic success, no code issued, real owner warned); confirm still 409 `AUTH_EMAIL_ALREADY_REGISTERED` | security | P1 | authored ✓ (backend test) |
| E2E-MCE-005 | Wrong code → 400 `AUTH_CODE_INVALID`, stays on the verify phase, email not changed | edge | P0 | authored ✓ (screen + backend tests) |
| E2E-MCE-006 | Expired code (older than 10 min) → 400 `AUTH_CODE_EXPIRED`, request a new one | edge | P1 | authored ✓ (backend test) |
| E2E-MCE-007 | Resend re-requests a code for the same new address once the countdown ends | happy | P2 | authored (screen - resend wired to `_sendCode`) |
| E2E-MCE-008 | Request cap - 5 codes per hour, the 6th send → 429 `RATE_LIMIT_EXCEEDED` | resilience | P1 | authored ✓ (backend test) |
| E2E-MCE-009 | Auth gate - a guest never sees the row and cannot reach `/auth/change-email` | auth | P0 | authored ✓ (more-screen `signedIn` guard + endpoint policy) |
| E2E-MCE-010 | RTL render (Arabic) - both phases mirror; the emails + code stay LTR | i18n | P1 | authored (shared navy auth chrome, mirrored) |
| E2E-MCE-011 | Attempt cap + no-outstanding-code - a 6th wrong try / a confirm with no live code → 400 `AUTH_CODE_INVALID` (request a new code) | edge | P1 | authored ✓ (backend test) |
| E2E-MCE-012 | Wrong current password on confirm → 401 `AUTH_INVALID_CREDENTIALS`, code NOT consumed (checked before the code) | security | P0 | authored ✓ (screen + backend tests) |
| E2E-MCE-013 | Successful confirm emails the OLD address a security alert (`EmailChangedNotice`, "SIMF login email changed") | security | P1 | authored ✓ (backend test) |
| E2E-MCE-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MCE-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MCE-001 - Golden path (change email end-to-end)

```gherkin
Feature: Change my login email with an emailed code
Scenario: A signed-in visitor moves their login email to a new address
  Given a signed-in approved visitor whose current email is "old.visitor@simf.test"
  And they open the More tab and tap "تغيير البريد الإلكتروني / Change email"
       under the "الإعدادات / Settings" section
  Then the change-email screen opens on phase 1 showing the current email read-only
       (LTR) with the heading "Enter your new email address. We'll send a
       verification code to it to confirm." (changeEmailHeading)
  When they type New email = "new.visitor@simf.test" and tap "إرسال الرمز / Send code"
  Then the app calls POST /app/auth/change-email/send-otp with body
       {"newEmail":"new.visitor@simf.test"}
  And the server emails a 6-digit code TO new.visitor@simf.test and returns 200 with
       {"maskedNewEmail":"n***@simf.test","expiresInSeconds":600,"resendCooldownSeconds":120}
  And the screen advances to phase 2 "تأكيد البريد الجديد / Confirm new email",
       shows "أرسلنا رمزاً الى / We sent a code to" + the masked address (gold, LTR),
       a "كلمة المرور الحالية / Current password" field (changeEmailPasswordLabel,
       with a show/hide toggle) below the OTP boxes, and starts the 02:00 resend
       countdown
  And "تحقّق / Verify" stays disabled until BOTH a 6-digit code AND a non-empty
       password are entered
  When they enter the emailed 6-digit code and their current password, then tap
       "تحقّق / Verify"
  Then the app calls POST /app/auth/change-email/confirm with body
       {"newEmail":"new.visitor@simf.test","code":"<the 6 digits>","currentPassword":"<their password>"}
  And the server re-checks the password (before the code), validates + consumes the
       code, moves Email + UserName to the new address, sets EmailConfirmed=true,
       rolls the security stamp and revokes all refresh tokens, returning 200
       {"emailChanged":true}
  And the server emails the PREVIOUS address a "SIMF login email changed" security
       alert (EmailChangedNotice)
  And the app signs out locally and routes to sign-in (context.goNamed signIn)
  And a snackbar shows "تم تغيير بريدك الإلكتروني. يرجى تسجيل الدخول مرة أخرى." /
       "Your email was changed. Please sign in again." (changeEmailSuccessToast)
```

**Evidence:** `change_email_screen_test` (send-otp advances to the code phase and
shows the masked recipient; the golden path enters the current password with the
code, then a correct pair confirms, signs out and routes to sign-in with the toast);
`ChangeEmailTests` happy path (password re-checked, email + username move,
EmailConfirmed set, stamp rolled, refresh tokens revoked, old address emailed the
alert). Golden: `change_email_golden_test` (`goldens/change_email_enter.png` +
`goldens/change_email_code.png`).

### E2E-MCE-002 - New email equals the current one

```gherkin
Scenario: The new address is the account's current address (client guard)
  Given the change-email screen on phase 1 with current email "old.visitor@simf.test"
  When the user types New email = "OLD.VISITOR@simf.test" (same, case-insensitive)
       and taps "Send code"
  Then the client validator blocks submit with the inline field error
       "هذا هو بريدك الإلكتروني الحالي بالفعل." /
       "This is already your email address." (changeEmailSameAsCurrent)
  And no POST /app/auth/change-email/send-otp request fires

Scenario: The same-address guard is also enforced server-side
  Given a client posts POST /app/auth/change-email/send-otp with the caller's own
       current address as newEmail
  Then the server returns 400 with ApiResult.Error.Code = "AUTH_EMAIL_UNCHANGED"
  And the message is "This is already your email address." /
       "هذا هو بريدك الإلكتروني الحالي بالفعل."
  And no code is issued
```

**Evidence:** `change_email_screen_test` (the same-as-current inline validator error);
`ChangeEmailTests` (send-otp with the current email is `AUTH_EMAIL_UNCHANGED` 400 and
issues no code; the confirm path re-checks it too).

### E2E-MCE-003 - Malformed new email (client validation)

```gherkin
Scenario: A badly formed email is rejected before any round-trip
  Given the change-email screen on phase 1
  When the user types New email = "not-an-email" and taps "Send code"
  Then the client validator blocks submit with the inline field error
       "بريد إلكتروني غير صالح" / "Invalid email" (invalidEmail)
  And no POST /app/auth/change-email/send-otp request fires
  When they clear the field and tap "Send code" with it blank
  Then the inline error switches to "هذا الحقل مطلوب" /
       "This field is required" (requiredField)
```

**Evidence:** `change_email_screen_test` - the blank + malformed cases both keep the
screen on phase 1 with no repository call (the field validator runs first).

### E2E-MCE-004 - New email registered to another account (enumeration-resistant deflect)

```gherkin
Scenario: send-otp deflects a target held by a different account (no enumeration oracle)
  Given a signed-in visitor "old.visitor@simf.test"
  And another account already owns "taken@simf.test"
  When they submit New email = "taken@simf.test" on phase 1
  Then POST /app/auth/change-email/send-otp returns HTTP 200 with the SAME success
       shape as a free address {"maskedNewEmail":"t***@simf.test","expiresInSeconds":600,
       "resendCooldownSeconds":120}
  And NO change-email code is issued to the caller
  And the real owner of "taken@simf.test" is emailed the "account already exists"
       notice (the AccountExists template), warning them someone tried to move an
       account onto their address
  And the app advances to phase 2 as usual (the response is indistinguishable from
       a free address), so an authenticated caller cannot tell which emails exist
  # UX cost of enumeration resistance: a user who TYPOS their new address as someone
  # else's registered email sees "code sent", then - with no code arriving - fails
  # at confirm. This is accepted.

Scenario: confirm still 409s for a taken address (not an enumeration oracle there)
  Given the caller reaches confirm with a target now held by another account
  When they post POST /app/auth/change-email/confirm
  Then the server returns HTTP 409 with
       ApiResult.Error.Code = "AUTH_EMAIL_ALREADY_REGISTERED"
  And the message is "An account with this email address already exists." /
       "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل."
  # Confirm keeps the 409 because the caller must already hold a valid code bound to
  # the address, so it leaks nothing an attacker did not already supply. The
  # uniqueness check also re-runs here, catching an address taken BETWEEN the two steps.
```

**Evidence:** `ChangeEmailTests` - send-otp to an address held by another user
returns the generic 200 success, issues no change-email code, and enqueues the
AccountExists notice to the real owner (the D-198-style deflect); confirm to a taken
address still returns `AUTH_EMAIL_ALREADY_REGISTERED` 409 and mutates nothing.

### E2E-MCE-005 - Wrong code

```gherkin
Scenario: An incorrect code is rejected and the email is unchanged
  Given the change-email screen on phase 2 with a code emailed to "new.visitor@simf.test"
  When the user enters a wrong 6-digit code and taps "Verify"
  Then POST /app/auth/change-email/confirm returns 400 with
       ApiResult.Error.Code = "AUTH_CODE_INVALID"
  And an inline bilingual error shows "رمز التحقق غير صحيح." /
       "The verification code is not correct."
  And the login email is NOT changed and the screen stays on phase 2
  And the wrong attempt is counted against the code's 5-attempt cap
```

**Evidence:** `change_email_screen_test` (a wrong code shows the inline error and
stays on the verify phase); `ChangeEmailTests` (a mismatched code is
`AUTH_CODE_INVALID` 400, increments the attempt count, and leaves the email as-is;
the compare is constant-time and the hash is bound to the target address).

### E2E-MCE-006 - Expired code

```gherkin
Scenario: A code older than its 10-minute lifetime is rejected
  Given a change-email code was emailed more than 10 minutes ago
  When the user submits it on confirm
  Then POST /app/auth/change-email/confirm returns 400 with
       ApiResult.Error.Code = "AUTH_CODE_EXPIRED"
  And the message is "The verification code has expired. Request a new one." /
       "انتهت صلاحية رمز التحقق. اطلب رمزًا جديدًا."
  And the expired code is burned (consumed) and the email is unchanged
```

**Evidence:** `ChangeEmailTests` - a code past `CodeLifetime` (10 min) confirms to
`AUTH_CODE_EXPIRED` 400 and is consumed.

### E2E-MCE-007 - Resend after the cooldown

```gherkin
Scenario: Resend re-requests a code once the countdown ends
  Given the change-email screen on phase 2 with the 02:00 resend countdown running
  Then the "إعادة الإرسال / Resend" action under "لم يصلك الرمز؟ / Didn't get the
       code?" is inert while the countdown is above 00:00
  When the countdown reaches 00:00 and the user taps "Resend"
  Then the app calls POST /app/auth/change-email/send-otp again for the same new address
  And the server consumes the previous unconsumed code (only the newest stays valid)
       and emails a fresh one, restarting the countdown
```

**Evidence:** `change_email_screen_test` - `_resend` is gated on the countdown and
re-invokes `_sendCode`; `ChangeEmailTests` - a re-issue consumes the prior code so an
old code no longer confirms.

### E2E-MCE-008 - Per-account request cap (429)

```gherkin
Scenario: A signed-in session cannot spam change-email codes
  Given a signed-in visitor
  When they request a change-email code 5 times within the hour
  Then each POST /app/auth/change-email/send-otp returns 200
  When they request a 6th within the same hour
  Then the response is 429 with ApiResult.Error.Code = "RATE_LIMIT_EXCEEDED"
  And the message is "Too many verification codes have been requested. Try again
       later." / "تم طلب رموز تحقق كثيرة. حاول مرة أخرى لاحقًا."
  And the screen surfaces it as the inline error on phase 1
```

**Evidence:** `ChangeEmailTests` - the 6th send inside the 1-hour window is
`RATE_LIMIT_EXCEEDED` 429 (the per-account cap `MaxCodesPerWindow=5`; the endpoint
also carries the `auth` per-IP limiter).

### E2E-MCE-009 - Auth gate (guest cannot reach it)

```gherkin
Scenario: A guest never sees the row and cannot reach the screen
  Given the app is in guest (not-signed-in) mode
  When the guest opens the More tab
  Then the "تغيير البريد الإلكتروني / Change email" row is NOT rendered
       (the row is wrapped in the `if (signedIn)` guard, like Reset password)
  And there is no way to push /auth/change-email from the UI

Scenario: The endpoints refuse an unauthenticated / unapproved caller
  Given a request to POST /app/auth/change-email/send-otp or /confirm
  When the caller is not signed in, or the account is not Approved
  Then the RequireApprovedAccount policy rejects it (401 / 403) before the handler
  And a disabled or missing subject on an otherwise valid token is reported as
       401 "Account unavailable." / "الحساب غير متاح." (AUTH_ACCOUNT_NOT_FOUND),
       never leaking whether the account exists
```

**Evidence:** `more_screen.dart` wraps the row in `if (signedIn)`; both endpoints
carry `Policies(RequireApprovedAccount)`; `ChangeEmailTests` covers the
`AUTH_ACCOUNT_NOT_FOUND` 401 for a disabled/unknown subject and the unauthorized path.

### E2E-MCE-010 - RTL render (Arabic)

```gherkin
Scenario: The screen mirrors correctly in Arabic
  Given the app UI language is Arabic (RTL)
  When the change-email screen is shown on phase 1 then phase 2
  Then the back chevron, the "تغيير البريد الإلكتروني" title, the field labels
       (البريد الإلكتروني الحالي / البريد الإلكتروني الجديد), the OTP boxes, the
       resend row and the pinned "إرسال الرمز" / "تحقّق" CTA all mirror right-to-left
  And the current email, the masked new email and the 6-digit code stay LTR
  And every visible string resolves through AppL10n (no hardcoded literal)
```

**Evidence:** shared navy auth chrome (`AccountSubHeader` + `OtpMark` + `OtpCodeBoxes`
+ `MaxWidthBody`) is RTL-correct; the golden `change_email_golden_test` locks the
Arabic render; the email + code carry an explicit `TextDirection.ltr`.

### E2E-MCE-011 - Attempt cap + no outstanding code

```gherkin
Scenario: A code is dead after too many wrong attempts
  Given a change-email code emailed to "new.visitor@simf.test"
  When the user submits a wrong code 5 times (reaching MaxCodeAttempts)
  And submits again
  Then POST /app/auth/change-email/confirm returns 400 AUTH_CODE_INVALID with
       "Too many incorrect attempts. Request a new code." /
       "محاولات غير صحيحة كثيرة. اطلب رمزًا جديدًا."

Scenario: Confirming with no live code
  Given no unconsumed change-email code exists for the account
  When the user posts POST /app/auth/change-email/confirm
  Then the response is 400 AUTH_CODE_INVALID with
       "No verification code is outstanding. Request a new one." /
       "لا يوجد رمز تحقق فعّال. اطلب رمزًا جديدًا."
  And the email is unchanged in both cases
```

**Evidence:** `ChangeEmailTests` - the attempt-cap branch (`code.AttemptCount >=
MaxCodeAttempts`) and the no-outstanding-code branch both confirm to
`AUTH_CODE_INVALID` 400 and mutate nothing.

### E2E-MCE-012 - Wrong current password on confirm

```gherkin
Scenario: The re-authentication password is wrong (code is preserved)
  Given the change-email screen on phase 2 with a valid code emailed to
       "new.visitor@simf.test"
  When the user enters the correct 6-digit code but the WRONG current password
       and taps "تحقّق / Verify"
  Then POST /app/auth/change-email/confirm returns HTTP 401 with
       ApiResult.Error.Code = "AUTH_INVALID_CREDENTIALS"
  And the message is "The password is not correct." / "كلمة المرور غير صحيحة."
  And the login email is NOT changed and the screen stays on phase 2
  And the code is NOT consumed and its attempt count is NOT incremented (the
       password is checked BEFORE the code), so the user can retry with the right
       password and the same code
```

**Evidence:** `change_email_screen_test` (a wrong password surfaces the inline
error and stays on the verify phase); `ChangeEmailTests` (a bad password confirms to
`AUTH_INVALID_CREDENTIALS` 401, checked before the code, leaving the code unconsumed
and its attempt count untouched, and the email unchanged).

### E2E-MCE-013 - Old-address security alert on success

```gherkin
Scenario: The previous address is warned out-of-band on a successful change
  Given a signed-in visitor whose current email is "old.visitor@simf.test"
  When they complete a change to "new.visitor@simf.test" (correct code + password)
  Then after the change commits the server emails "old.visitor@simf.test" the
       EmailChangedNotice template, subject "SIMF login email changed"
  And the alert states the login email was changed to the masked new address
       (n***@simf.test) and to contact SIMF support if it was not them
  # This is the out-of-band early-warning control: a takeover revokes the victim's
  # sessions and the new login email is the attacker's, so this alert to the address
  # the victim still controls is how they learn of it. A mail hiccup here never
  # re-throws, since the change already committed.
```

**Evidence:** `ChangeEmailTests` - a successful confirm enqueues the
`EmailChangedNotice` email to the PREVIOUS address with the masked new email token.

---

## Implementation notes

- **Manual smoke is canonical today.** The canonical run is a signed-in device
  session: open More → Settings → Change email, drive both phases, and read the
  emailed code from the test outbox / `SIMF_Identity.AccountCodes`. Never paste a
  literal secret into a scenario - the code is a per-run value.
- **API integration tests cover the same surface at a lower layer**
  (`tests/SIMF.Api.Tests/ChangeEmailTests.cs`): send-otp (same-email, taken-deflect,
  rate-limit) and confirm (happy, wrong-password-401, wrong/expired code,
  attempt-cap, no-code, unauthorized) plus the old-address alert. Where an E2E
  scenario fully covers one of these, the lower-layer case may be retired later -
  keep both during the transition.
- **Wire contract (D-219).** The public JSON field names the app sends/decodes -
  `newEmail`, `code`, `currentPassword` (sent on confirm), `maskedNewEmail`,
  `expiresInSeconds`, `resendCooldownSeconds`, `emailChanged` - are append-only and
  must not be renamed.

---

_Last reviewed:_ `2026-07-22` by `SIMF Team` - Build #24 initial authoring
(MCE-001..011): the self-service change-email flow (emailed code to the new
address, forced re-login on success) + the CP admin-edit `EmailConfirmed=false`
tightening cross-linked from the visitors / others catalogues. Same date, post
security review, hardened (MCE-012/013 added; MCE-001/004 revised): current-password
re-auth on confirm (wrong password → 401 `AUTH_INVALID_CREDENTIALS`, code not
consumed), enumeration-resistant send-otp deflect + the `auth-email` per-email
limiter, and the old-address `EmailChangedNotice` security alert on success.
