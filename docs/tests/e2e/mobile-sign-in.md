# E2E test catalogue — `Sign in` (`signIn`) + auth flow

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #3 and the
> linked auth flow (email-OTP, forgot-password, reset-password). Spec:
> [`mobile/sign-in/README.md`](../../pages/mobile/sign-in/README.md) (legacy
> [`Page_003`](../../App/Page_003/README.md)). Runner-agnostic Gherkin. The screen
> glue is widget-tested in
> `src/Mobile/simf_app/test/features/account/sign_in_screen_test.dart` + the golden
> `test/golden/sign_in_screen_golden_test.dart` (168:2800); the auth controller
> (sign-in hydration, email-OTP, refresh) in
> `src/Mobile/simf_app/packages/simf_auth_pkg/test/auth_controller_signin_test.dart`.

| | |
|--|--|
| **Page** | [`Page_003`](../../App/Page_003/README.md) (App page docs) |
| **Route** | app screen #3 `/sign-in` (+ `/auth/verify-otp`, `/auth/forgot-password`, `/auth/reset-password`) |
| **APIs** | `POST /app/auth/sign-in` · `verify-otp` · `forgot-password` · `reset-password` · `refresh` (+ device-key for biometric) |
| **Surface** | Mobile (Flutter) — Guest entry; promotes to Visitor/Moderator/Staff on success |
| **Auth setup** | A registered visitor (approved / pending / 2FA-on as the scenario needs). OTP codes via the email channel; **no literal secrets**. |
| **Last reviewed** | 2026-06-30 (clean-code freeze D-549; behaviour unchanged) |

> **KSA-Project redesign (D-358/D-360/D-363, Figma 168:2800):** the screen is
> the navy + beige-card design — no app bar; back chevron top-left and a
> **globe language toggle** top-right (wired: AR ↔ EN persisted via
> `LocaleController` — supersedes the old D-272 unwired placeholders);
> **remember-me checkbox** (default ON) gating the email prefill store; the
> **Face-ID button always visible** (silent fallback when unavailable); the
> underlined **"الدخول كزائر"** guest link below it (design-native since the
> frame's 2026-06-11 update). The previous mockup screen is parked in
> `lib/features/_legacy_mockup/`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB003-001 | Password sign-in (no 2FA) → tokens → Home, with real privilege | happy | P0 | authored ✓ (widget + controller tests) |
| E2E-MOB003-002 | Invalid credentials → inline **localized** error (the envelope's AR/EN message, #8), password cleared, stays on sign-in | edge | P0 | authored ✓ (widget test) |
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
| E2E-MOB003-013 | Signed-in → server `profileComplete=false` routes to Page_007 (`/sign-up/visitor`); true → Home (D-374, both auth paths) | happy | P0 | authored ✓ (widget + API tests) |
| E2E-MOB003-014 | "Browse without signing in" → guest landing (Page 012) → public Home, no token (D-325) | happy | P1 | authored ✓ (widget test) |
| E2E-MOB003-015 | Remember-me unchecked → the email is NOT stored for the next prefill (D-360) **and the session is kept in memory only — it does NOT survive an app restart (#9)** | edge | P0 | authored ✓ (widget + `auth_controller_signin_test` #9) |
| E2E-MOB003-016 | Globe button toggles AR ↔ EN and persists the preference (D-363) | happy | P1 | authored ✓ (widget test) |
| E2E-MOB003-017 | **Post-sign-in Face-ID enrol nudge (D-442/D-445; #7a):** when the device has a usable biometric and Face-ID is not yet enabled, a notification-style SnackBar with an "Enable" action appears after **every** sign-in (both the password and OTP paths); tapping Enable now **routes to the emailed-OTP step-up** (`biometricStepUp`, see [`mobile-biometric-step-up.md`](mobile-biometric-step-up.md)) instead of a one-tap enrol; never shows when already-enabled or unavailable; the captured GoRouter is lifetime-safe after the screen routes away | happy | P1 | authored ✓ (`biometric_auth_test` — show / no-show + routes-to-step-up) |
| E2E-MOB003-018 | A malformed email → inline "Invalid email" / "بريد إلكتروني غير صالح" and the sign-in round-trip is blocked (no `signIn` call), #7 | edge | P0 | authored ✓ (widget `a malformed email shows the inline error and does not sign in`) |
| E2E-MOB003-019 | Server error messages render in the app's language — the envelope carries `message`+`messageArabic` and the data layer picks by locale (#8/#11; fixes the whole error surface, not just sign-in) | i18n | P0 | authored ✓ (data-pkg `picks the localized message by isArabic`, `decodes the Arabic message alongside the English one`) |
| E2E-MOB003-021 | **The form controls have accessible names (BUG-012)** — the email box, the password box and the "remember me" checkbox each expose their visible caption as their own semantics label. The captions are separate `SimfFieldLabel` / `Text` siblings, so the controls previously announced as an unnamed "edit box" / "checkbox". Fixed on the shared `AccountEmailField` / `AccountPasswordField` / `AccountRememberForgot`, so every auth form benefits | a11y | P1 | authored ✓ (pattern covered by `simf_search_field_semantics_test`; the same `Semantics(label:, textField: true)` wrap + `Checkbox.semanticLabel`) |
| E2E-MOB003-020 | **Verify-OTP "Resend" re-issues the code IN PLACE (#12)** — once the 60s countdown ends, "إعادة الإرسال" calls `POST /app/auth/resend-otp` (keyed by the ticket, no password), emails a fresh code, restarts the countdown, and toasts; it no longer bounces to sign-in. The ticket is unchanged; a wrong/expired ticket → `AUTH_OTP_TOKEN_INVALID`; the 6th request/hour → `RATE_LIMIT_EXCEEDED` (429) shown inline | happy | P0 | authored ✓ (backend `ResendOtpTests` 4/4 + `auth_controller_signin_test` resendOtp) |

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
  And routes by the profileComplete flag — Home when complete, the Page_007
      profile form when not (same rule as the password path, D-374)
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
  And the screen renders on the KSA entry chrome (navy + beige card, same as sign-in — D-374)
```

### E2E-MOB003-008 — Reset password

```gherkin
Scenario: Reset with the emailed code
  Given the user is on the reset screen with their email
  When they enter the OTP + a new password + matching confirmation and tap Reset
  Then the app calls POST /app/auth/reset-password
  And on success returns to /sign-in with the email pre-filled
  And a mismatched confirmation is blocked client-side before any call
  And the screen renders on the KSA entry chrome (navy + beige card, same as sign-in — D-374)
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

### E2E-MOB003-013 — Profile-completion auto-route (D-288, reworked D-374)

```gherkin
Scenario: A signed-in visitor with an incomplete profile is sent to complete it
  Given a visitor completes sign-in (password, device-key, or the 2FA OTP step)
  Then the server-computed profileComplete flag on the session user decides the route
       (GET /app/users/me hydration — names + ≥1 interest + male→ID-photo)
  And if profileComplete is false it routes to the visitor profile-completion
       screen (Page_007, /sign-up/visitor)
  And if profileComplete is true it routes Home (#13)
  And no extra client-side profile probe is made (the old GET user-profile probe is gone)
```

**Evidence:** `sign_in_screen_test` — "successful sign-in with a complete profile
routes home and stores the email" + "successful sign-in with an incomplete profile
routes to the visitor profile screen (Page_007 auto-route)"; server flag:
`SIMF.Api.Tests/UserProfileTests.Me_profileComplete_*`.

### E2E-MOB003-014 — Browse without signing in (guest entry, D-325)

```gherkin
Feature: Open the app as a guest
Scenario: A signed-out user enters the app without an account
  Given the sign-in screen
  When the user taps the underlined "الدخول كزائر / Enter as guest" link
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

Scenario: Unchecking remember-me keeps the session in memory only (#9)
  Given the user signs in with "Keep me logged in" unchecked
  Then the session works for this run (the access token refreshes from memory)
  But nothing is written to durable secure storage (and any prior stored
      session is cleared)
  So restarting the app restores no session — the user lands signed out
  # The backend gives no session-scoped token; the client enforces it by not
  # persisting. A biometric (device-key) re-open always persists (it is an
  # explicit "remember me on this device").
```

**Evidence:** `sign_in_screen_test` — "unchecking remember-me skips storing the
email"; `auth_controller_signin_test` — "an un-remembered sign-in keeps the
session in memory only" + "a remembered sign-in persists the session".

### E2E-MOB003-016 — Language toggle (D-363)

```gherkin
Scenario: The globe button switches the app language
  Given the sign-in screen in Arabic
  When the user taps the globe button (top-right)
  Then the app switches to English and the preference is persisted
  And tapping again returns to Arabic
```

**Evidence:** `sign_in_screen_test` — "the globe button toggles and persists
the language".

### E2E-MOB003-017 — Post-sign-in Face-ID enrol nudge (D-442/D-445; #7a)

```gherkin
Scenario: Every sign-in nudges an un-enrolled biometric device to enable Face-ID
  Given a device with a usable biometric and Face-ID NOT yet enabled
  When the user completes sign-in (password OR the 2FA email-OTP path)
  Then a notification-style SnackBar appears with an "Enable / تفعيل" action
  When the user taps Enable
  Then the app routes to the emailed-OTP step-up screen (#7a, biometricStepUp)
       — enrolment proceeds there through the confirmed-code flow, not a one-tap enrol
  And the captured GoRouter still routes even after the app moved to Home
       (the action holds the lifetime-safe router/container, not the disposed screen)

Scenario: The nudge stays silent when it should
  Given Face-ID is already enabled OR the device has no usable biometric
  When the user completes sign-in
  Then no enrol nudge is shown
```

**Evidence:** `biometric_auth_test.dart` — "available + not enabled → shows the
nudge; tapping Enable routes to the step-up screen", "already enabled → no
nudge", "biometrics unavailable → no nudge". The full step-up + enrol path is
catalogued in [`mobile-biometric-step-up.md`](mobile-biometric-step-up.md); the
on-device OS biometric prompt is the owner's device test.

### E2E-MOB003-018 — OS autofill remembers the last-used email, not a first-typed guess (D-742)

```gherkin
Scenario: A corrected sign-up email is the one offered at login
  Given a new user starts sign-up, types a mistyped email, then corrects it and
        completes email verification
  When the user returns to the sign-in screen
  Then the email field is pre-filled with the CORRECTED, just-verified address
        (the app writes it to lastEmail on a successful verify)
  And the OS password-manager offers the corrected credentials — not the
        first-typed guess — because the auth form is an AutofillGroup that commits
        the FINAL submitted values via finishAutofillContext

Scenario: Unchecking "remember me" discards the saved email in both stores
  Given the sign-in screen with "remember me" unchecked
  When the user signs in successfully
  Then the app removes lastEmail AND tells the OS to discard the autofill context
        (finishAutofillContext(shouldSave: false)), so nothing is pre-filled or
        offered next time
```

**Evidence:** `sign_in_screen_test` — "the login form is an AutofillGroup and the
fields carry OS autofill hints…" and "unchecking remember-me forgets a previously
remembered email"; `sign_up_email_verify_screen_test` — "…verifies and routes to
sign-in" asserts `lastEmail` is set to the verified address. The on-device OS
password-manager save/offer is the owner's device test.

---

_Last reviewed:_ `2026-07-26` by `SIMF Team` — BUG-012: the email / password /
remember-me controls now carry their own accessible names (E2E-MOB003-021).
_Prior:_ `2026-07-11` by `SIMF Team`.
