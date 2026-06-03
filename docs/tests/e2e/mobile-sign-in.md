# E2E test catalogue — `Sign in` (`signIn`) + auth flow

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #3 and the
> linked auth flow (email-OTP, forgot-password, reset-password). Spec:
> [`Page_003`](../../App/Page_003/README.md). Runner-agnostic Gherkin. The screen
> glue is widget-tested in
> `src/Mobile/simf_app/test/features/auth/sign_in_screen_test.dart`; the auth
> controller (sign-in hydration, email-OTP, refresh) in
> `src/Mobile/packages/simf_auth_pkg/test/auth_controller_signin_test.dart`.

| | |
|--|--|
| **Page** | [`Page_003`](../../App/Page_003/README.md) (App page docs) |
| **Route** | app screen #3 `/sign-in` (+ `/auth/verify-otp`, `/auth/forgot-password`, `/auth/reset-password`) |
| **APIs** | `POST /app/auth/sign-in` · `verify-otp` · `forgot-password` · `reset-password` · `refresh` (+ device-key for biometric) |
| **Surface** | Mobile (Flutter) — Guest entry; promotes to Visitor/Moderator/Staff on success |
| **Auth setup** | A registered visitor (approved / pending / 2FA-on as the scenario needs). OTP codes via the email channel; **no literal secrets**. |
| **Last reviewed** | 2026-06-03 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB003-001 | Password sign-in (no 2FA) → tokens → Home, with real privilege | happy | P0 | authored ✓ (widget + controller tests) |
| E2E-MOB003-002 | Invalid credentials → inline error, password cleared, stays on sign-in | edge | P0 | authored ✓ (widget test) |
| E2E-MOB003-003 | 2FA account → email-OTP screen → `verify-otp` → Home | happy | P0 | authored ✓ (widget + controller tests) |
| E2E-MOB003-004 | Privilege comes from `/app/users/me` hydration, not the token payload | resilience | P0 | authored ✓ (controller test) |
| E2E-MOB003-005 | Email pre-filled from the last successful sign-in | happy | P1 | authored ✓ (widget test) |
| E2E-MOB003-006 | Client caps email≤50 / password≤32 (UI only; server stays 256/128) | edge | P1 | authored (maxLength) |
| E2E-MOB003-007 | Forgot password → emails an OTP → reset screen (enumeration-resistant) | happy | P0 | authored (screen) |
| E2E-MOB003-008 | Reset password (OTP + new password, passwords-match) → back to sign-in | happy | P0 | authored (screen) |
| E2E-MOB003-009 | Pending/rejected account → routed by registration status (Page 11) | edge | P1 | authored (status drives routing) |
| E2E-MOB003-010 | Network / 500 → non-blocking error; fields preserved; no token mutation | resilience | P1 | authored (error surface) |
| E2E-MOB003-011 | RTL render (Arabic) — fields, errors, links mirror; email stays LTR | i18n | P1 | authored (screen) |
| E2E-MOB003-012 | Biometric (device-key) re-open | happy | P0 | **pending — next commit (device-key client + local_auth)** |

## Scenarios

### E2E-MOB003-001 — Password sign-in

```gherkin
Feature: Password sign-in
Scenario: An approved visitor signs in
  Given an approved visitor with email "visitor@example.sa"
  When they enter the email + password and tap Sign in
  Then the app calls POST /api/v1/app/auth/sign-in
  And on success the tokens are stored
  And the app hydrates the real app-role from GET /app/users/me
  And routes to Home (#13) as a Visitor (not Guest)
  And the email is persisted for next time
```

**Evidence:** `sign_in_screen_test` (routes home + stores email); `auth_controller_signin_test` (hydration → Visitor).

### E2E-MOB003-002 — Invalid credentials

```gherkin
Scenario: Wrong password
  When the user signs in with a wrong password
  Then the response is AUTH_INVALID_CREDENTIALS
  And an inline bilingual error shows
  And the password field is cleared, the email kept
  And the app stays on /sign-in
```

**Evidence:** `sign_in_screen_test` — "invalid credentials show the error and stay on sign-in".

### E2E-MOB003-003 — Email-OTP second factor

```gherkin
Scenario: A 2FA visitor completes the email OTP
  Given a visitor whose account has 2FA enabled
  When they sign in with the correct password
  Then the response carries mfaRequired = true with an otpToken (no TOTP)
  And the app routes to the email-OTP screen
  When they enter the code emailed to them
  Then the app calls POST /app/auth/verify-otp and receives tokens
  And routes to Home
```

**Evidence:** `sign_in_screen_test` (routes to OTP); `auth_controller_signin_test` ("email-OTP (2FA) hydrates the real app-role").

### E2E-MOB003-004 — Privilege from /users/me

```gherkin
Scenario: The token payload's role is never trusted
  Given the sign-in token payload carries only id/email/displayName
  When the app finishes sign-in
  Then it reads appRole from GET /app/users/me
  And an approved Visitor is treated as Visitor, never defaulted to Guest
```

### E2E-MOB003-005 — Email pre-fill

```gherkin
Scenario: The last email is pre-filled
  Given a previous successful sign-in stored "prefilled@example.sa"
  When the sign-in screen opens
  Then the email field shows "prefilled@example.sa" and focus is on the password
```

**Evidence:** `sign_in_screen_test` — "the email field is pre-filled from the last sign-in".

### E2E-MOB003-007 — Forgot password

```gherkin
Scenario: Request a reset code
  When the user enters their email on Forgot password and taps Send code
  Then the app calls POST /app/auth/forgot-password
  And (enumeration-resistant) the app always proceeds to the reset screen with the email carried forward
```

### E2E-MOB003-008 — Reset password

```gherkin
Scenario: Reset with the emailed code
  Given the user is on the reset screen with their email
  When they enter the OTP + a new password + matching confirmation and tap Reset
  Then the app calls POST /app/auth/reset-password
  And on success returns to /sign-in with the email pre-filled
  And a mismatched confirmation is blocked client-side before any call
```

### E2E-MOB003-012 — Biometric re-open (pending)

```gherkin
Scenario: Face/biometric re-open within the window
  Given a device-key is enrolled and the window is unexpired
  When the user taps the biometric button and authenticates
  Then the app signs the server challenge and calls sign-in-with-device-key
  And mints fresh tokens with no typed password
```

> **Status: pending** — the Dart ES256 device-key client + `local_auth` gate land
> in the next commit; the `local_auth` native config + secure-enclave hardening
> are the simf-run/native follow-up (the android/ios folders are not in this tree).

---

_Last reviewed:_ `2026-06-03` by `SIMF Team`.
