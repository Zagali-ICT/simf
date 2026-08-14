# E2E test catalogue — `Confirm Face ID` biometric-enable step-up (`biometricStepUp`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). #7a — the emailed-OTP
> step-up that confirms a signed-in user wants to **enable** biometric (Face-ID)
> sign-in before a device key is enrolled. Runner-agnostic Gherkin. The screen
> glue is widget-tested in
> `src/Mobile/simf_app/test/features/account/biometric_step_up_screen_test.dart`
> (+ the render-lock golden `test/golden/biometric_step_up_golden_test.dart`);
> the toggle + nudge launch in
> `src/Mobile/simf_app/test/features/account/biometric_auth_test.dart`; the backend
> gate in `tests/SIMF.Api.Tests/DeviceKeyStepUpTests.cs`.
>
> **D-738 (device-credential confirm):** enrolment now runs the banking-standard
> two-factor chain — emailed OTP **AND** an OS device-credential confirmation
> (device PIN / biometric via `local_auth`, the shared
> `BiometricAuth.confirmDeviceIdentity` with `biometricOnly: false`) inserted
> AFTER the code is entered and BEFORE `POST /app/auth/device-keys` enrols the
> key. So neither an emailed code alone nor a borrowed-but-unlocked phone alone
> can bind a biometric credential. The same `confirmDeviceIdentity` mapping now
> also backs the **sign-in** Face-ID prompt (device-PIN fallback + explicit
> outcome errors instead of the old silent password fallback) —
> `lib/features/account/sign_in_screen.dart` `_biometricSignIn` /
> `_biometricPromptError`.

| | |
|--|--|
| **Route** | aux auth `/auth/biometric-step-up` (`RouteNames.biometricStepUp`) — pushed from the Face-ID toggle (profile / side-menu) and the post-sign-in enrol nudge |
| **APIs** | `POST /app/auth/device-keys/step-up` (issue) · `POST /app/auth/device-keys` with `stepUpCode` (the gated register) |
| **Surface** | Mobile (Flutter) — signed-in, Approved account; the device must have a usable OS biometric |
| **Permissions** | `RequireApprovedAccount` (both endpoints); not a CP/admin action |
| **Auth setup** | A signed-in approved visitor on a biometric-capable device. Codes via the email channel; **no literal secrets**. Server gate `DeviceKey:RequireStepUpForEnrol` is ON in production. |
| **Last reviewed** | 2026-06-30 (clean-code freeze D-554; behaviour unchanged) |

> **#7a design:** enrolling a biometric credential is a sensitive action — a
> borrowed-but-unlocked phone could otherwise silently bind a new device key. So
> enabling now: **confirm intent → email a 6-digit code → enter it → enrol**. The
> server **rejects** `POST /app/auth/device-keys` without a fresh, unconsumed
> `BiometricEnrolStepUp` code (constant-time compare, 10-min expiry, single-use,
> 5-attempts cap, 5-issues/hour cap). Disabling keeps its own destructive-delete
> confirm (#7b). The screen reuses the shared KSA OTP frame
> (`OtpCodeBoxes`/`OtpMark`, D-369).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MBSU-001 | Toggle ON → confirm dialog → step-up screen emails a code → enter it → device key enrols → toast + switch ON | happy | P0 | authored ✓ (screen + toggle + backend tests) |
| E2E-MBSU-002 | The screen requests a code on open and shows the masked recipient ("d***@…") | happy | P0 | authored ✓ (screen test) |
| E2E-MBSU-003 | A wrong code → inline `BIOMETRIC_STEP_UP_INVALID` error, stays on the screen, not enrolled | edge | P0 | authored ✓ (screen + backend tests) |
| E2E-MBSU-004 | Register without a code (gate ON) → `BIOMETRIC_STEP_UP_REQUIRED` 401, no key | edge | P0 | authored ✓ (backend test) |
| E2E-MBSU-005 | A consumed code can't be reused; an expired code is rejected | edge | P0 | authored ✓ (backend test — single-use; service expiry) |
| E2E-MBSU-006 | Issue is capped at 5 per hour → the 6th send → 429 `RATE_LIMIT_EXCEEDED` | resilience | P1 | authored ✓ (backend test) |
| E2E-MBSU-007 | Cancelling the confirm dialog never sends a code / never enrols | edge | P1 | authored ✓ (toggle test — Continue gates the launch) |
| E2E-MBSU-008 | The post-sign-in nudge "Enable" routes to this step-up screen (not a one-tap enrol) | happy | P1 | authored ✓ (`biometric_auth_test`) |
| E2E-MBSU-009 | Resend re-requests a code once the countdown ends | happy | P2 | authored (screen — resend wired to `_send`) |
| E2E-MBSU-010 | RTL render (Arabic) — header, hint, boxes and resend row mirror; the email stays LTR | i18n | P1 | authored (shared OTP frame, mirrored) |
| E2E-MBSU-011 | Network / 500 on send → inline "couldn't send" error, retto via resend; no key mutation | resilience | P1 | authored (screen error surface) |
| E2E-MBSU-012 | **D-738:** code entered → OS device-credential sheet → success → key enrols → toast + switch ON | happy | P0 | authored ✓ (screen test — device-credential success) |
| E2E-MBSU-013 | Cancel the device-credential sheet → NOT enrolled, inline "confirmation cancelled" message, retry works | edge | P0 | authored ✓ (screen test — cancelled outcome) |
| E2E-MBSU-014 | No device screen lock → blocked with the "set a device screen lock" message; no code consumed | edge | P0 | authored ✓ (screen test — noDeviceCredential outcome) |
| E2E-MBSU-015 | Sign-in Face-ID offers a device-PIN fallback and surfaces explicit lockout/unavailable errors (no silent password fallback) | edge | P1 | source-verified (sign_in_screen `_biometricSignIn`; `confirmDeviceIdentity` `biometricOnly:false`) |
| E2E-MBSU-016 | A device key cannot sign in while the account owes a password change (expired past PasswordMaxAgeDays, or admin-forced) | edge | P0 | authored ✓ (backend test) |
| E2E-MBSU-017 | A device key cannot sign in while the account is locked out | edge | P0 | authored ✓ (backend test) |
| E2E-MBSU-018 | Changing the password revokes every device key, so the biometric credential dies with the sessions | edge | P0 | authored ✓ (backend test) |
| E2E-MBSU-019 | An administrator account is refused at enrolment (403 FORBIDDEN); a visitor is not | edge | P0 | authored ✓ (backend test) |
| E2E-MBSU-020 | A label carrying `;`, `=` or a newline is rejected, so it cannot forge a field in the audit detail | edge | P0 | authored ✓ (backend theory, 3 cases) |
| E2E-MBSU-021 | Enrolling past the 5-key cap retires an older key; the key just enrolled always survives | edge | P1 | authored ✓ (backend test) |
| E2E-MBSU-022 | A revoked key and an id that never existed give byte-identical challenge responses | edge | P1 | authored ✓ (backend test) |
| E2E-MBSU-023 | Enrolling names the device: the row's `Label` is `{manufacturer} {model} · {8 hex}` on Android, or the marketing model on iOS, never the old `SIMF mobile` constant | happy | P0 | authored ✓ (unit + screen tests) |
| E2E-MBSU-024 | Two devices under one account produce two distinguishable labels | happy | P1 | authored ✓ (unit test: the suffix is per-install) |
| E2E-MBSU-025 | Re-enrolling on the same install reuses the same fingerprint suffix | edge | P1 | authored ✓ (unit test) |
| E2E-MBSU-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MBSU-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MBSU-001 — Enable Face-ID end-to-end

```gherkin
Feature: Enable biometric sign-in with an emailed step-up
Scenario: A signed-in visitor turns Face-ID on
  Given a signed-in approved visitor on a biometric-capable device with Face-ID off
  When they flip the "Face ID sign-in" toggle ON
  Then a confirm dialog "Enable Face ID sign-in?" is shown
  When they tap Continue
  Then the app opens the step-up screen and calls POST /app/auth/device-keys/step-up
  And a 6-digit code is emailed and the masked recipient is shown
  When they enter the emailed code and tap Verify
  Then the app calls POST /app/auth/device-keys with the code (+ the generated public key)
  And the server validates + consumes the code and registers the device key
  And a "Face ID sign-in enabled" toast shows and the screen pops with the toggle ON
```

**Evidence:** `biometric_auth_test` (toggle → confirm → routes to step-up);
`biometric_step_up_screen_test` (enrols with the entered code, pops + toast);
`DeviceKeyStepUpTests.Register_with_valid_code_succeeds_then_code_is_single_use`.

### E2E-MBSU-002 — Code requested on open

```gherkin
Scenario: The step-up screen emails a code as it opens
  Given the step-up screen is pushed
  Then it calls POST /app/auth/device-keys/step-up
  And shows the masked address the code was sent to (e.g. "d***@simf.test")
  And starts the resend countdown
```

**Evidence:** `biometric_step_up_screen_test` — "on open it requests a code and
shows the masked recipient"; `DeviceKeyStepUpTests.Send_step_up_returns_masked_email_and_issues_a_code`.

### E2E-MBSU-003 — Wrong code

```gherkin
Scenario: An incorrect code is rejected
  Given the step-up screen with a code emailed
  When the user enters a wrong 6-digit code and taps Verify
  Then POST /app/auth/device-keys returns 401 BIOMETRIC_STEP_UP_INVALID
  And an inline bilingual error shows
  And the device key is NOT registered and the screen stays open
```

**Evidence:** `biometric_step_up_screen_test` — "a wrong code shows the inline
error and stays on the screen"; `DeviceKeyStepUpTests.Register_with_wrong_code_is_BIOMETRIC_STEP_UP_INVALID`.

### E2E-MBSU-004 — Register requires the code (server-enforced)

```gherkin
Scenario: The register endpoint refuses without a fresh code
  Given the server gate DeviceKey:RequireStepUpForEnrol is ON
  When a client posts POST /app/auth/device-keys with no stepUpCode
  Then the response is 401 BIOMETRIC_STEP_UP_REQUIRED
  And no device key is created
```

**Evidence:** `DeviceKeyStepUpTests.Register_without_step_up_code_is_rejected_when_gate_on`.

### E2E-MBSU-005 — Single-use + expiry

```gherkin
Scenario: A code can be used once
  Given a valid code that just enrolled a device key
  When the same code is submitted again
  Then it is rejected (consumed)

Scenario: An expired code is rejected
  Given a step-up code older than its 10-minute lifetime
  When it is submitted on register
  Then the response is 401 BIOMETRIC_STEP_UP_INVALID and the code is burned
```

**Evidence:** `DeviceKeyStepUpTests.Register_with_valid_code_succeeds_then_code_is_single_use`
(reuse rejected); service expiry + attempt-cap logic in `DeviceKeyService.VerifyEnrolStepUpAsync`.

### E2E-MBSU-006 — Issue rate cap

```gherkin
Scenario: A signed-in session can't spam step-up emails
  Given a signed-in visitor
  When they request a step-up code 5 times within the hour
  Then each returns 200
  When they request a 6th
  Then the response is 429 RATE_LIMIT_EXCEEDED
```

**Evidence:** `DeviceKeyStepUpTests.Step_up_send_is_capped_per_window`.

### E2E-MBSU-007 — Confirm gate

```gherkin
Scenario: Cancelling the confirm never starts the flow
  Given the user flips the toggle ON
  And the "Enable Face ID sign-in?" dialog is shown
  When they dismiss it (Cancel)
  Then no code is requested, the step-up screen never opens, and nothing is enrolled
```

**Evidence:** `biometric_auth_test` — the step-up screen only appears after
Continue (the confirm gates the launch).

### E2E-MBSU-008 — Nudge routes to the step-up

```gherkin
Scenario: The post-sign-in nudge's Enable opens the step-up
  Given the post-sign-in Face-ID nudge is shown (device capable, not enrolled)
  When the user taps "Enable / تفعيل"
  Then the app routes to the step-up screen (the captured GoRouter is lifetime-safe)
  And enrolment proceeds through the same emailed-code flow (no one-tap enrol)
```

**Evidence:** `biometric_auth_test` — "tapping Enable routes to the step-up screen".

### E2E-MBSU-012 — Device-credential confirm then enrol (D-738)

```gherkin
Scenario: The emailed code is followed by an OS device-credential confirmation
  Given the step-up screen with a code emailed and the 6 digits entered
  When the user taps Verify
  Then the app first runs the OS device-credential sheet
       ("أكّد قفل الشاشة أو بصمتك لتفعيل الدخول ببصمة الوجه" /
        "Confirm your device PIN or biometric to enable Face ID sign-in")
  And the user proves it with a fingerprint / Face / device PIN
  Then the app calls POST /app/auth/device-keys with the code (+ the generated public key)
  And a "Face ID sign-in enabled" toast shows and the screen pops with the toggle ON
```

**Evidence:** `biometric_step_up_screen_test` — "entering a code enrols with it
and pops with a success toast" (the fake `confirmDeviceIdentity` returns
`success`);
`DeviceKeyStepUpTests.Register_with_valid_code_succeeds_then_code_is_single_use`.

### E2E-MBSU-013 — Cancel the device-credential sheet

```gherkin
Scenario: Dismissing the device sheet does not enrol
  Given the step-up screen with the code entered
  When the user taps Verify and then dismisses / fails the OS device-credential sheet
  Then no device key is registered (POST /app/auth/device-keys is NOT called)
  And an inline message shows
       "أُلغي التأكيد — لم يتم تفعيل الدخول ببصمة الوجه." /
       "Confirmation cancelled — Face ID sign-in was not enabled."
  And the entered code stays so the user can simply tap Verify again
```

**Evidence:** `biometric_step_up_screen_test` — "cancelling the device-credential
confirm does NOT enrol and shows the cancelled message" (`LocalAuthOutcome.cancelled`).

### E2E-MBSU-014 — No device screen lock

```gherkin
Scenario: A device with no screen lock can't secure Face-ID sign-in
  Given the step-up screen with the code entered
  And the device has NO screen lock set (no PIN / pattern / passcode)
  When the user taps Verify
  Then the OS device-credential step reports no device credential
  And the screen blocks enrolment with
       "فعّل قفل الشاشة (رمز PIN أو نمط أو كلمة مرور) على جهازك أولاً ثم حاول مجدداً." /
       "Set a device screen lock (PIN, pattern or password) first, then try again."
  And the step-up code is not consumed (no register call)
```

**Evidence:** `biometric_step_up_screen_test` — "no device screen lock shows the
set-a-lock message (D-738)" (`LocalAuthOutcome.noDeviceCredential`).

### E2E-MBSU-016 — A password change owed blocks the biometric path

```gherkin
Scenario: Face ID cannot outlive the maximum password age
  Given a visitor with an enrolled device key
  And the account holds PasswordChangeRequired (admin-forced, or the password
    aged past IdentityLifecycle:PasswordMaxAgeDays)
  When the client takes a challenge, signs it, and posts sign-in-with-device-key
  Then the response is 403 AUTH_PASSWORD_CHANGE_REQUIRED
  And no tokens are issued
  And the refusal is audited
```

**Why it matters:** the password path refuses this at every other token-mint
surface. Before this gate the device key was the one way around it, which made
the NCA maximum-password-age control unenforceable for any biometric user.

**Evidence:** `DeviceKeySignInTests.Sign_in_is_refused_when_a_password_change_is_required`,
plus `Sign_in_still_succeeds_when_no_password_change_is_pending` as the negative
control.

### E2E-MBSU-017 — Lockout blocks the biometric path

```gherkin
Scenario: A locked account stays locked on every door
  Given a visitor with an enrolled device key
  And the account is locked out
  When the client takes a challenge, signs it, and posts sign-in-with-device-key
  Then the response is 423 AUTH_ACCOUNT_LOCKED
  And no tokens are issued
```

**Evidence:** `DeviceKeySignInTests.Sign_in_is_refused_while_the_account_is_locked_out`.

### E2E-MBSU-018 — A password change revokes the device keys

```gherkin
Scenario: The remedy removes every credential, not just the sessions
  Given a visitor with an enrolled device key
  When the user changes their password at POST /app/auth/change-password
  Then every device key on the account is revoked
  And a later challenge request for that key returns 401 DEVICE_KEY_REVOKED
```

**Why it matters:** sign-in-with-device-key is anonymous, so before this an
attacker who had enrolled a key simply minted a fresh session after the victim
did the one thing every security notice tells them to do.

**Evidence:** `DeviceKeySignInTests.Changing_the_password_revokes_the_device_keys`.

### E2E-MBSU-019 — Administrators cannot enrol

```gherkin
Scenario: A biometric credential is not offered to an admin account
  Given an approved account whose UserType is Admin
  When it posts POST /app/auth/device-keys
  Then the response is 403 FORBIDDEN
  And no device key is created
  And the refusal is audited
```

**Why it matters:** the mint issues the caller's full role and permission set with
`secondFactorCompleted` null, so an enrolled admin would hold an
admin-permissioned bearer obtainable with one factor, against the enrolment-first
rule that the Control Panel must never mint a session on the password alone.

**Evidence:** `DeviceKeySignInTests.An_administrator_cannot_enrol_a_device_key`.

### E2E-MBSU-020 — A label cannot forge an audit field

```gherkin
Scenario Outline: Separator and control characters are refused
  Given a signed-in approved visitor
  When it registers a device key labelled "<label>"
  Then the response is 400 DEVICE_KEY_INVALID
  And no device key is created

  Examples:
    | label               |
    | Phone; actor=admin  |
    | Phone=admin         |
    | Phone\nactor=admin  |
```

**Why it matters:** the label is interpolated into an audit detail shaped
`key=value; key=value`, which is the only record of who enrolled which
credential.

**Evidence:** `DeviceKeySignInTests.A_label_that_could_forge_an_audit_field_is_rejected`.

### E2E-MBSU-021 — The active-key cap holds

```gherkin
Scenario: A sixth enrolment retires an older key
  Given a visitor holding 5 active device keys and DeviceKey:MaxActiveKeysPerUser = 5
  When the visitor enrols a sixth
  Then exactly 5 of the 6 keys remain usable
  And the key just enrolled is one of them
```

**Why it matters:** every key is a permanent alternative credential, and until the
"my devices" screen exists nobody can see a set accumulating.

**Evidence:** `DeviceKeySignInTests.Enrolling_past_the_cap_revokes_the_oldest_key`.

### E2E-MBSU-022 — The challenge endpoint is not an oracle

```gherkin
Scenario: Revoked and never-existed are indistinguishable
  Given a device key that has been revoked
  When a challenge is requested for it, and for a random unknown id
  Then both return 401 with the same error code and the same message
```

**Evidence:** `DeviceKeySignInTests.A_revoked_key_and_an_unknown_key_are_indistinguishable`.

### E2E-MBSU-023 — The enrolled device is named (D-884)

```gherkin
Scenario Outline: The label carries the real device, not a constant
  Given a signed-in approved visitor on <platform>
  When the user completes the biometric step-up
  Then the created device key's label is "<name> · <8 hex>"
  And it is NOT the string "SIMF mobile"
  And it is 64 characters or fewer

  Examples:
    | platform | name             |
    | Android  | samsung SM-S911B |
    | iOS      | iPhone 15 Pro    |
```

**Why the platform column is testable off-device:** `DeviceLabel` takes an
injectable `DevicePlatform` and `DeviceInfoSource` precisely so these two rows
are exercised by the suite. Before that they were reachable only from physical
hardware, which meant the only branch the tests ever ran was the fallback.

**Residual, and stated rather than implied:** the real
`PluginDeviceInfoSource` reads `manufacturer`, `model`, `modelName` and
`identifierForVendor` from `device_info_plus`, and those four reads are the one
part still unproven until this runs on a device.

**Evidence:** `device_label_test` group "the real device branches";
`biometric_step_up_screen_test` asserts the resolved label reaches
`enrolDeviceKey`.

### E2E-MBSU-024 — Two devices are distinguishable

```gherkin
Scenario: One account, two enrolled devices
  Given the account has enrolled a device key on a phone
  When it enrols another on a tablet
  Then the two labels differ
  And an operator reading the DeviceKeys rows can tell them apart
```

The defect this closes: every row in production read `SIMF mobile`, so a phone
and a tablet were indistinguishable, including to an administrator revoking one.

### E2E-MBSU-025 — The suffix is stable per install

```gherkin
Scenario: Re-enrolling on the same device keeps its identity
  Given a device that has enrolled once and then disabled Face ID
  When the user enables it again on the same install
  Then the fingerprint suffix is unchanged
```

The fingerprint is minted once into secure storage and reused, so a
disable-then-enable cycle does not look like a different device.

**Evidence:** `device_label_test` — "mints a fingerprint once and reuses it on
the next enrolment".

### E2E-MBSU-015 — Sign-in Face-ID device-PIN fallback + explicit errors

```gherkin
Scenario: The sign-in Face-ID prompt falls back to the device PIN
  Given a signed-in-capable device with an enrolled device key
  When the user taps "Sign in with Face ID" on the sign-in screen
  Then the OS sheet is biometric-first but offers the device PIN/pattern/passcode
       as a fallback (confirmDeviceIdentity uses biometricOnly:false)
  And proving either one completes the device-key sign-in (POST /app/auth/device-keys sign-in)

Scenario: A failed biometric surfaces an explicit error, not a silent password fallback
  Given the same sign-in Face-ID prompt
  When the OS reports a lockout or an unavailable sensor
  Then an explicit inline message shows (biometricLockedOut / biometricUnavailable)
       instead of silently dropping the user to the password field
  And a plain user cancel stays silent (their own choice)
```

**Evidence:** source-verified — `sign_in_screen.dart` `_biometricSignIn`
(availability + enrolment guards → `confirmDeviceIdentity` → explicit
`_biometricPromptError`, cancel is silent) and `biometric_auth.dart`
`confirmDeviceIdentity` (`biometricOnly: false` → device-PIN fallback; maps
`passcodeNotSet`/`notEnrolled` → `noDeviceCredential`, `lockedOut`/
`permanentlyLockedOut` → `lockedOut`). Widget coverage today asserts the Face-ID
button visibility (`sign_in_screen_test`); the fallback/lockout branches have no
dedicated widget test yet — **flagged**.

---

_Last reviewed:_ `2026-07-11` by `SIMF Team` — D-738 device-credential confirm
(MBSU-012..014) + sign-in Face-ID device-PIN fallback (MBSU-015). Earlier:
`2026-06-21`.
