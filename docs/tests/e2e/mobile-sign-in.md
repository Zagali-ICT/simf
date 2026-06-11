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
| **Last reviewed** | 2026-06-11 |

> **KSA-Project redesign (D-358/D-360, Figma 168:2800):** the screen is now the
> navy + beige-card design — no app bar (the D-272 theme/language placeholder
> buttons are gone), back chevron top-left, **remember-me checkbox** (default
> ON) gating the email prefill store, the **Face-ID button always visible**
> (silent fallback when unavailable), and the guest link kept below it. The
> previous mockup screen is parked in `lib/features/_legacy_mockup/`.

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
| E2E-MOB003-012 | Biometric (device-key) re-open | happy | P0 | authored ✓ (Dart client + controller tests; **.NET↔Dart interop proven by backend golden-vector test, D-266**; on-device prompt → simf-run) |
| E2E-MOB003-013 | Signed-in → profile-incomplete routes to Page_007 (`/sign-up/visitor`); complete → Home; probe failure → Home | happy | P0 | authored ✓ (widget test) |
| E2E-MOB003-014 | "Browse without signing in" → guest landing (Page 012) → public Home, no token (D-325) | happy | P1 | authored ✓ (widget test) |
| E2E-MOB003-015 | Remember-me unchecked → the email is NOT stored for the next prefill (D-360) | edge | P1 | authored ✓ (widget test) |

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
  And the email is persisted for next time (remember-me checked — the default)
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

> **Built (Dart):** the ES256 device-key client (P-256 / SPKI / IEEE-P1363) + the
> register/challenge/sign-in-with-device-key wiring + the `local_auth`-gated
> biometric button are implemented and unit-tested in Dart
> (`device_key_client_test.dart`, `auth_controller_device_key_test.dart`).
> **.NET ↔ Dart interop — proven (D-266):** a real Dart-produced public key +
> signature (golden vector) is run through the backend's actual verify path in
> `DeviceKeySignInTests.Dart_client_signature_verifies_against_the_backend`
> (`tests/SIMF.Api.Tests/DeviceKeySignInTests.cs`); the app's SPKI imports and its
> IEEE-P1363 signature over SHA-256(challenge) verifies in .NET's `ECDsa`.
> **simf-run follow-up (only):** the `local_auth` native config (manifest /
> Info.plist / MainActivity) + a secure-enclave key — the on-device biometric
> prompt — land in simf-run, where android/ios exist.

### E2E-MOB003-013 — Profile-completion auto-route (D-288)

```gherkin
Scenario: A signed-in visitor with an incomplete profile is sent to complete it
  Given a visitor signs in successfully (password or device-key)
  When the app probes GET /app/account/user-profile once
  Then if the profile is incomplete (no Arabic/English name or no interests) it
       routes to the visitor profile-completion screen (Page_007, /sign-up/visitor)
  And if the profile is complete it routes Home (#13)
  And if the probe fails (network / 5xx) it falls back Home — sign-in is never blocked
```

**Evidence:** `sign_in_screen_test` — "successful sign-in with a complete profile
routes home and stores the email" + "successful sign-in with an incomplete profile
routes to the visitor profile screen (Page_007 auto-route)".

### E2E-MOB003-014 — Browse without signing in (guest entry, D-325)

```gherkin
Feature: Open the app as a guest
Scenario: A signed-out user enters the app without an account
  Given the sign-in screen
  When the user taps "Browse without signing in"
  Then the guest-mode landing (Page 012) opens
  And "Continue as guest" enters the public Home (#13) with no token
  And Guest+ content (sessions, speakers, venue map, media) is browsable
  And account-only actions (badge, notifications, booking, contacts) still gate to sign-in
```

**Evidence:** `sign_in_screen_test` — "browse-without-signing-in opens the guest
screen (no auth)". The guest landing itself is `GuestModeScreen` (Page 012).

### E2E-MOB003-015 — Remember-me gates the email store (D-360)

```gherkin
Scenario: Unchecking remember-me skips storing the email
  Given the sign-in screen with the remember-me checkbox checked by default
  When the user unchecks remember-me and signs in successfully
  Then the app routes to Home
  And the email is NOT stored for the next prefill
```

**Evidence:** `sign_in_screen_test` — "unchecking remember-me skips storing the
email".

---

_Last reviewed:_ `2026-06-11` by `SIMF Team`.
