# E2E test catalogue — `Email verification` (`emailOtp`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #6 — sign-up
> step 2 (email-OTP). Spec: [`Page_006`](../../App/Page_006/README.md). Runner-agnostic
> Gherkin. The screen glue is widget-tested in
> `src/Mobile/simf_app/test/features/account/sign_up_email_verify_screen_test.dart`
> (+ the golden `test/golden/sign_up_email_verify_golden_test.dart`, 505:837);
> the controller delegation in
> `src/Mobile/simf_app/packages/simf_auth_pkg/test/auth_controller_email_verify_test.dart`;
> the repository contract in
> `src/Mobile/simf_app/packages/simf_auth_pkg/test/auth_repository_impl_test.dart`.

| | |
|--|--|
| **Page** | [`Page_006`](../../App/Page_006/README.md) (App page docs) |
| **Route** | app screen #6 `emailOtp` → `/sign-up/otp` (email carried as a query arg) |
| **APIs** | `POST /api/v1/app/auth/verify-email` (`{ email, code }` → `{ email, emailVerified }`) · `POST /api/v1/app/auth/resend-code` (`{ email }` → `{ email, codeExpiresInSeconds }`) — both `AllowAnonymous`, `auth` limiter |
| **Surface** | Mobile (Flutter) — Anonymous (mid sign-up, no token yet) |
| **Auth setup** | None. No token / `Authorization` header — identity is asserted by email + the emailed 6-digit code. The code is read at run time from `SIMF_Identity.AccountCodes` (`Purpose = EmailVerification`, latest unconsumed) — **never** a literal code. |
| **Last reviewed** | 2026-06-30 (clean-code freeze D-553; behaviour unchanged) |

> **KSA-Project redesign (D-364, Figma 505:837):** the sign-up verify screen
> now renders six segmented code boxes (one invisible capture field), the
> gold-ringed mail mark, the gold `mm:ss` countdown + muted-blue label, and
> the **لم يصلك الرمز؟ إعادة الإرسال** footer. The verify / resend contract is
> unchanged; the cooldown now runs a fixed **2-minute** countdown that starts on
> entry (D-695). The old screen is parked in
> `lib/features/_legacy_mockup/`. (The sign-in 2FA OTP screen keeps its
> previous look until its own redesign changeset.)

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB006-001 | Verify disabled until exactly 6 digits; a correct code verifies → "Email verified" → sign-in | happy | P0 | authored ✓ (widget test) |
| E2E-MOB006-002 | Wrong code → bilingual inline error, field cleared, stays on screen (attempt consumed) | error | P0 | authored ✓ (widget test) |
| E2E-MOB006-003 | Expired / attempt-capped code → bilingual error steering to Resend | error | P1 | authored (API errors AUTH_CODE_EXPIRED / AUTH_CODE_INVALID) |
| E2E-MOB006-004 | A 2-minute countdown shows on entry (D-695); Resend is disabled until it elapses, then re-issues the code and restarts the 2-minute cooldown | happy | P0 | authored ✓ (widget test) |
| E2E-MOB006-005 | Resend cap reached → 429 `RATE_LIMIT_EXCEEDED` bilingual message; Resend stays disabled | resilience | P1 | authored (API E2 / repo `_guard`) |
| E2E-MOB006-006 | Non-digits rejected at input; no request fired for < 6 digits | edge | P1 | authored ✓ (digitsOnly + length gate) |
| E2E-MOB006-007 | Network / 5xx → generic bilingual message, inputs kept | resilience | P1 | authored ✓ (widget test — NetworkUnavailable branch) |
| E2E-MOB006-008 | RTL render (Arabic) — labels/subtitle/buttons mirror; the OTP field + email stay LTR | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB006-001 — Golden path: verify → sign-in

```gherkin
Feature: Sign-up email verification
Scenario: A correct code verifies the email
  Given a guest arrives from sign-up with email "visitor@example.sa"
  And a 6-digit verification code was emailed
  Then the Verify button is disabled until exactly 6 digits are entered
  When they enter the correct code
  And they tap "Verify"
  Then the app POSTs { email, code } to /app/auth/verify-email
  And the account moves Registered -> EmailVerified
  And it shows the "Email verified" toast
  And it routes to sign-in (the authenticated profile step needs a token)
```

**Evidence:** `sign_up_email_verify_screen_test` — "Verify is disabled until 6 digits, then verifies and routes to sign-in"; `auth_controller_email_verify_test` — "verifyEmail delegates to the repository and never changes state".

### E2E-MOB006-002 — Wrong code

```gherkin
Scenario: An incorrect code is rejected
  When the guest enters a wrong 6-digit code and taps "Verify"
  Then the server returns 400 AUTH_CODE_INVALID and consumes one attempt
  And the screen shows the bilingual "The verification code is not correct." inline
  And the field is cleared for re-entry and the screen is kept
```

**Evidence:** `sign_up_email_verify_screen_test` — "a wrong code shows the inline error and clears the field".

### E2E-MOB006-003 — Expired / attempt-capped

```gherkin
Scenario: The code can no longer be used
  When the code is past its expiry (or the attempt cap is reached)
  And the guest taps "Verify"
  Then the server returns 400 AUTH_CODE_EXPIRED (expired) or 400 AUTH_CODE_INVALID (cap)
  And the screen shows the bilingual error steering the user to Resend
```

> Retrying the same code cannot succeed — the user must Resend (Page_006 L-7).

### E2E-MOB006-004 — On-entry countdown + resend cooldown (D-695)

```gherkin
Scenario: The countdown shows on entry and gates the first resend
  Given the guest has just landed on the email-verify screen (the code was sent)
  Then a 2-minute countdown "إعادة الإرسال خلال 02:00" is shown
  And the Resend action is disabled until the countdown reaches 00:00

Scenario: Resend re-issues the code after the countdown elapses
  Given the 2-minute countdown has reached 00:00
  When the guest taps "Resend code"
  Then the app POSTs { email } to /app/auth/resend-code
  And the previous code is invalidated and a fresh one is emailed
  And a fresh 2-minute cooldown restarts (a fixed client cooldown — NOT the
      600s codeExpiresInSeconds the endpoint returns)
```

**Evidence:** `sign_up_email_verify_screen_test` — "the resend cooldown shows on entry and blocks resend until it elapses (D-695)".

### E2E-MOB006-005 — Resend cap

```gherkin
Scenario: Too many resends
  Given the account-scoped resend cap has been reached
  When the guest taps "Resend code"
  Then the server returns 429 RATE_LIMIT_EXCEEDED with the bilingual cap message
  And the Resend button stays disabled
```

> The cap surfaces the same 429 / RATE_LIMIT_EXCEEDED wire signature as the per-IP
> limiter, so the client cannot distinguish the two (Page_006 L-5 / API E2).

### E2E-MOB006-006 — Input gating

```gherkin
Scenario: Only 6 digits enable Verify
  When the guest types non-digit characters
  Then they are rejected at input (digits only)
  And with fewer than 6 digits the Verify button stays disabled and no request fires
```

### E2E-MOB006-007 — Network / server failure

```gherkin
Scenario: The call fails on the wire
  When verify or resend fails (network unavailable / 5xx)
  Then the screen shows the generic bilingual error
  And the entered digits are kept so the user can retry
```

**Evidence:** `sign_up_email_verify_screen_test` (NetworkUnavailable → `networkErrorBody`).

### E2E-MOB006-008 — RTL render (Arabic)

```gherkin
Scenario: The screen mirrors under Arabic
  Given the app language is Arabic
  Then the title, subtitle, error and buttons mirror right-to-left
  And the OTP field and the echoed email stay LTR (digits / address read correctly)
```

---

_Last reviewed:_ `2026-06-04` by `SIMF Team`.
