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
> **Face-ID button** (rendered only when `biometricAvailableProvider` reports a
> usable biometric — `SignInAltActions` guards it with `if (biometricAvailable)`,
> so it is NOT always visible as an earlier revision of this note claimed); the
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
| E2E-MOB003-006 | Client caps email≤50 / **password≤128** (UI only; server email stays 256). The password cap **matches `PasswordPolicy.MaxLength = 128`** so a valid long passphrase can still be typed — the old 32 cap locked those users out. Corrected 2026-08-03; the field is `AccountPasswordField(maxLength: 128)`. | edge | P1 | authored (maxLength) |
| E2E-MOB003-007 | Forgot password → emails an OTP → reset screen (enumeration-resistant) | happy | P0 | authored (screen) |
| E2E-MOB003-008 | Reset password (OTP + new password, passwords-match) → back to sign-in | happy | P0 | authored (screen) |
| E2E-MOB003-009 | Pending/rejected account → routed by registration status (Page 11) | edge | P1 | authored (status drives routing) |
| E2E-MOB003-010 | Network / 500 → non-blocking error; fields preserved; no token mutation | resilience | P1 | authored (error surface) |
| E2E-MOB003-011 | RTL render (Arabic) — fields, errors, links mirror; email stays LTR | i18n | P1 | authored (screen) |
| E2E-MOB003-012 | Biometric (device-key) re-open. The OS sheet is **biometric-only** (`biometricOnly: true`, commit `3be516b5`) — no device-PIN fallback — and each failure message points at the password form on this screen; a plain cancel is silent. Full outcome table in [`mobile-biometric-step-up.md`](mobile-biometric-step-up.md) E2E-MBSU-015 | happy | P0 | authored ✓ (Dart client + controller tests; **.NET↔Dart interop proven by backend golden-vector test, D-266**; on-device prompt → simf-run) |
| E2E-MOB003-013 | Signed-in → server `profileComplete=false` routes to Page_007 (`/sign-up/visitor`); true → Home (D-374, both auth paths) | happy | P0 | authored ✓ (widget + API tests) |
| E2E-MOB003-014 | "Browse without signing in" → guest landing (Page 012) → public Home, no token (D-325) | happy | P1 | authored ✓ (widget test) |
| E2E-MOB003-015 | Remember-me unchecked → the email is NOT stored for the next prefill (D-360) **and the session is kept in memory only — it does NOT survive an app restart (#9)** | edge | P0 | authored ✓ (widget + `auth_controller_signin_test` #9) |
| E2E-MOB003-016 | Globe button toggles AR ↔ EN and persists the preference (D-363) | happy | P1 | authored ✓ (widget test) |
| E2E-MOB003-017 | **Post-sign-in Face-ID enrol nudge (D-442/D-445; #7a):** when the device has a usable biometric and Face-ID is not yet enabled, a notification-style SnackBar with an "Enable" action appears after **every** sign-in (both the password and OTP paths); tapping Enable now **routes to the emailed-OTP step-up** (`biometricStepUp`, see [`mobile-biometric-step-up.md`](mobile-biometric-step-up.md)) instead of a one-tap enrol; never shows when already-enabled or unavailable; the captured GoRouter is lifetime-safe after the screen routes away | happy | P1 | authored ✓ (`biometric_auth_test` — show / no-show + routes-to-step-up) |
| E2E-MOB003-018 | A malformed email → inline "Invalid email" / "بريد إلكتروني غير صالح" and the sign-in round-trip is blocked (no `signIn` call), #7 | edge | P0 | authored ✓ (widget `a malformed email shows the inline error and does not sign in`) |
| E2E-MOB003-019 | Server error messages render in the app's language — the envelope carries `message`+`messageArabic` and the data layer picks by locale (#8/#11; fixes the whole error surface, not just sign-in) | i18n | P0 | authored ✓ (data-pkg `picks the localized message by isArabic`, `decodes the Arabic message alongside the English one`) |
| E2E-MOB003-021 | **The form controls have accessible names (BUG-012)** — the email box, the password box and the "remember me" checkbox each expose their visible caption as their own semantics label. The captions are separate `SimfFieldLabel` / `Text` siblings, so the controls previously announced as an unnamed "edit box" / "checkbox". Fixed on the shared `AccountEmailField` / `AccountPasswordField` / `AccountRememberForgot`, so every auth form benefits | a11y | P1 | authored ✓ (pattern covered by `simf_search_field_semantics_test`; the same `Semantics(label:, textField: true)` wrap + `Checkbox.semanticLabel`) |
| E2E-MOB003-020 | **Verify-OTP "Resend" re-issues the code IN PLACE (#12)** — once the 60s countdown ends, "إعادة الإرسال" calls `POST /app/auth/resend-otp` (keyed by the ticket, no password), emails a fresh code, restarts the countdown, and toasts; it no longer bounces to sign-in. The ticket is unchanged; a wrong/expired ticket → `AUTH_OTP_TOKEN_INVALID`; the 6th request/hour → `RATE_LIMIT_EXCEEDED` (429) shown inline | happy | P0 | authored ✓ (backend `ResendOtpTests` 4/4 + `auth_controller_signin_test` resendOtp) |
| E2E-MOB003-022 | **A never-verified account is no longer a dead end.** Signing in as an account at `AccountState.Registered` returns 403 `AUTH_EMAIL_NOT_VERIFIED`; the screen now calls `POST /app/auth/resend-code` and pushes the verification screen (`RouteNames.emailOtp`) carrying the address, instead of printing "verify your email address" with nowhere to do it. Sign-up is closed to that account (the address is already registered), so before this the holder could not get in by any route. If the resend is REFUSED (the per-address cap), the user stays on sign-in with that reason — routing anyway would strand them on a 2-minute resend cooldown with an empty inbox | edge | P0 | authored ✓ (widget `an unverified account is sent a fresh code and taken to the verification screen`, `a refused resend keeps the user on sign-in with the reason`) |
| E2E-MOB003-023 | **A completed password reset verifies the email.** `ForgotPasswordAsync` refuses only Disabled and Rejected, so a `Registered` account is sent a reset code; consuming it proves control of the mailbox — the same fact the verification code proves — and `ResetPasswordAsync` now advances `Registered → EmailVerified` (+ `EmailConfirmed`) in the same transaction, audited as `EmailVerificationSucceeded`. An account in any other state is untouched: an Approved holder who resets stays Approved and does not lose the app | happy | P0 | authored ✓ (backend `Reset_password_promotes_a_never_verified_account_to_EmailVerified`, `Reset_password_leaves_an_already_approved_account_approved`) |
| E2E-MOB003-024 | **The Face-ID button NAMES the account it opens.** The device key is a discoverable credential: the request carries `{deviceKeyId, challenge, signature}` and no address, and the server resolves the account from the key row. An anonymous "Sign in with Face ID" button therefore signed the holder into whoever last enrolled on the handset, whatever the form said. The button now reads "Continue as `a***@example.sa`" | happy | P0 | authored ✓ (widget `an enrolled device NAMES the account the button opens`) |
| E2E-MOB003-025 | **A different address disables it, with the reason, BEFORE the prompt.** Typing an address the enrolled credential does not open leaves the button visible but disabled and shows "Face ID on this device is set up for `a***@…`. Enter that address, or sign in with your password." Visible-but-disabled, not hidden: a control that vanishes as you type explains nothing | edge | P0 | authored ✓ (widget `typing a DIFFERENT address disables it with the reason`) |
| E2E-MOB003-026 | **THE REGRESSION.** Account A enrols Face ID and signs out; the key deliberately survives sign-out so a re-open can use it. Entering B's address and tapping Face ID used to sign the holder into **A**, silently. `signInWithDeviceKey(expectedEmail:)` now returns `accountMismatch` and **issues no challenge** — refused on the handset, so no round trip and no OS prompt spent on a sign-in that could not have succeeded. B's account was never reachable this way and is not implicated | edge | P0 | authored ✓ (package `THE REGRESSION: another account is refused before any network call`, with `verifyNever(issueDeviceKeyChallenge)`) |
| E2E-MOB003-027 | **The button is hidden until something is enrolled.** Visibility used to rest on OS biometric HARDWARE alone, so a fresh phone offered a button whose only possible outcome was "not enrolled". It now needs a credential bound to an account as well. This SUPERSEDES the older rule elsewhere in this file that the button shows whenever a biometric is usable | edge | P1 | authored ✓ (widget `the Face-ID button is hidden when nothing is enrolled`) |
| E2E-MOB003-032 | **Deleting the account clears this device's credential.** `signOut` deliberately keeps the device key so a re-open can use it; an erasure must not, or the sign-in screen goes on naming the account that was just erased, on a pre-auth screen, over a credential the server already revoked | edge | P0 | authored ✓ (`delete_account_tile.dart` calls `disableDeviceKey()` before `signOut()`, best-effort) |
| E2E-MOB003-028 | **An upgraded install re-enrols once.** A key stored before the owner binding existed reads as NOT enrolled and is cleared, because nothing can say which account it would open — keeping it would preserve the defect on precisely the devices that already have it. Cost: one re-enrolment through the existing step-up flow. The orphaned server row stays listed under My Devices until revoked | edge | P1 | authored ✓ (package `an upgraded install with no binding reads as not enrolled, and the stale key is cleared`) |
| E2E-MOB003-029 | **A different account signing in on this handset drops the previous key.** Otherwise B signs in with a password, signs out, and the sign-in screen offers "Continue as `a***@…`" — showing B a masked form of A's address and a way into A's account. A loses Face ID and re-enrols, which is the right way round for a shared device. A cold-start token refresh does NOT trigger this: it re-establishes the same account | edge | P0 | authored ✓ (package `a DIFFERENT account signing in here drops the previous key`) |
| E2E-MOB003-030 | **The enrolled address itself keeps the button live**, trimmed and case-folded on both sides — a reader's capitalisation must never lock them out of their own credential. An EMPTY field also signs in, meaning "whoever this device is set up for", which is the normal case | happy | P1 | authored ✓ (widget `the enrolled address itself keeps the button live`; package `an empty email field still signs in as the enrolled account`) |
| E2E-MOB003-031 | **The stored owner is a DIGEST, never the address.** The binding holds `sha256(deviceKeyId + ':' + normalisedEmail)` plus a masked form for display. It is read on the sign-in screen, pre-auth, by whoever holds the phone, so the plain address must not sit there; salting with the server-issued key id also stops one person's address correlating across two installs. Not a secret-keeping claim — anyone who can read it can read the private key beside it | security | P1 | authored ✓ (`device_key_binding_test` — `the stored address is a digest, never the address itself`, plus the salt and case/whitespace cases) |
| E2E-MOB003-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB003-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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

_Last reviewed:_ `2026-09-04` by `SIMF Team` — the biometric device key is now
bound to the account that enrolled it, and the button names that account
(E2E-MOB003-024..031). Reported by the owner: enrolled on A, typed B, and was
signed in. The cause was not a credential bypass - B was never reachable - but
an anonymous button over a credential that resolves its own account.
_Prior:_ `2026-07-26` by `SIMF Team` — BUG-012: the email / password /
remember-me controls now carry their own accessible names (E2E-MOB003-021).
_Prior:_ `2026-07-11` by `SIMF Team`.
