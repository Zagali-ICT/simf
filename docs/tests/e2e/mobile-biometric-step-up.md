# E2E test catalogue — `Confirm Face ID` biometric-enable step-up (`biometricStepUp`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). #7a — the emailed-OTP
> step-up that confirms a signed-in user wants to **enable** biometric (Face-ID)
> sign-in before a device key is enrolled. Runner-agnostic Gherkin. The screen
> glue is widget-tested in
> `src/Mobile/simf_app/test/features/auth/biometric_step_up_screen_test.dart`;
> the toggle + nudge launch in
> `src/Mobile/simf_app/test/features/auth/biometric_auth_test.dart`; the backend
> gate in `tests/SIMF.Api.Tests/DeviceKeyStepUpTests.cs`.

| | |
|--|--|
| **Route** | aux auth `/auth/biometric-step-up` (`RouteNames.biometricStepUp`) — pushed from the Face-ID toggle (profile / side-menu) and the post-sign-in enrol nudge |
| **APIs** | `POST /app/auth/device-keys/step-up` (issue) · `POST /app/auth/device-keys` with `stepUpCode` (the gated register) |
| **Surface** | Mobile (Flutter) — signed-in, Approved account; the device must have a usable OS biometric |
| **Permissions** | `RequireApprovedAccount` (both endpoints); not a CP/admin action |
| **Auth setup** | A signed-in approved visitor on a biometric-capable device. Codes via the email channel; **no literal secrets**. Server gate `DeviceKey:RequireStepUpForEnrol` is ON in production. |
| **Last reviewed** | 2026-06-21 |

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

---

_Last reviewed:_ `2026-06-21` by `SIMF Team`.
