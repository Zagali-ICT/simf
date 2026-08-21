# Biometric step-up — enable Face-ID (تأكيد بصمة الوجه) — mobile `/auth/biometric-step-up`

| Field | Value |
|---|---|
| Route | `RouteNames.biometricStepUp` · pushed from the Face-ID toggle / post-sign-in nudge |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/account/biometric_step_up_screen.dart` (`BiometricStepUpScreen`) |
| Figma node | **none** — reuses the shared KSA OTP frame (D-369); §13.5 unbound (structural-only clean-code, render preserved) |
| Shell | `AuthScreenScaffold` (sweep + back/title header) over `AuthScrollBody`, with the pinned gold CTA in `AuthBottomBar` + the resend row |
| Providers | `authControllerProvider` (`sendBiometricStepUp` / `enrolDeviceKey`) · `biometricAuthProvider` (the OS confirm) · `deviceLabelProvider` (the enrolled device's name) · `biometricEnabledProvider` (invalidated on success) |
| Tests | `test/features/account/biometric_step_up_screen_test.dart` (widget, 7 cases) · golden `test/golden/biometric_step_up_golden_test.dart` (`goldens/biometric_step_up.png`) · E2E [`mobile-biometric-step-up.md`](../../../tests/e2e/mobile-biometric-step-up.md) |
| Status | ✅ Real — #7a (D-369 OTP frame) → **clean-code frozen (D-554, 2026-06-30)** |

## 1. Purpose
The emailed-OTP **step-up** that confirms the user wants to ENABLE biometric
(Face-ID) sign-in. On open it requests a code (`POST /app/auth/device-keys/step-up`);
entering it runs an **OS biometric confirmation** and only then enrols the device
key (`POST /app/auth/device-keys` with the code), which the server rejects without a
fresh code — so neither an emailed code alone nor a borrowed-but-unlocked phone
alone can bind a biometric credential (D-738).

The OS sheet is **biometric-only**: `BiometricAuth.confirmDeviceIdentity` passes
`biometricOnly: true`, so a face or a fingerprint is the only way through it and the
device PIN / pattern / passcode is **not** offered as a fallback. That reversed
D-738's original device-credential posture (commit `3be516b5`, "enforce
biometric-only authentication"), and it is why none of this screen's failure copy
may send the user to their PIN.

## 2. UI & behaviour (top → bottom)
The scroll body and the pinned CTA are each capped at
`SimfTokens.biometricStepUpScreenMaxWidth` (560) — `AuthScrollBody(maxWidth:)` and
`AuthBottomBar(maxWidth:)`.
1. **Header** — back chevron + centred "تأكيد بصمة الوجه".
2. **`OtpMark`** (fingerprint) + "أدخل رمز التأكيد" + the masked recipient (gold).
3. **`OtpCodeBoxes`** — six boxes; **resend countdown** below; pinned gold CTA
   "تحقق" (disabled until 6 digits); resend row.
4. **Inline error line** (below the countdown) — the one place every failure lands:
   a send failure, a rejected code, and each non-success outcome of the OS sheet.
   The entered code is never cleared, so a failed confirm is a straight retry.

### The OS confirm and its enrol-specific copy
Verify runs the OS sheet **before** the register call, so a cancelled or failed
confirm consumes no code. Each outcome maps through the shared
`localizedBiometricError`, but this screen overrides two of them:

| Outcome | What this screen shows |
|---|---|
| `cancelled` (dismissed / failed) | `biometricLocalConfirmCancelled` — "أُلغي التأكيد — لم يتم تفعيل الدخول ببصمة الوجه." |
| `lockedOut` | `biometricLockedOutEnrol` — "محاولات كثيرة خاطئة. المصادقة مقفلة مؤقتاً — حاول لاحقاً." |
| `unavailable` | `biometricUnavailableEnrol` — "تعذّر التحقق بالبصمة على هذا الجهاز. حاول مرة أخرى." |
| `noDeviceCredential` | the shared `biometricNoDeviceCredential` — "فعّل قفل الشاشة … أولاً ثم حاول مجدداً." |

The two overrides exist because the shared defaults are the **sign-in screen's**:
they tell the user to sign in with their password, which is advice a user who is
already signed in cannot act on, and there is no password form on this screen to
point at. The lockout copy also had to stop naming the device PIN — the OS sheet
does not offer one, so "use your device PIN" was a dead end. `unavailable` here
deliberately does **not** send the user to device settings either: the step-up is
only reachable once an enrolled biometric was found, so what lands on this branch
is an unexpected platform failure rather than a missing face/fingerprint.

`noDeviceCredential` takes **no** override on purpose — "set a screen lock" is
device-setup advice that reads the same from either caller. That is a decision, not
an omission, and `biometric_auth_test.dart` pins it so nobody has to re-derive it.

## 3. Data / API (wire contract D-219 frozen)
- `sendBiometricStepUp()` → masked recipient + start the cooldown (called on open).
- `confirmDeviceIdentity(biometricLocalConfirmReason)` → the OS sheet, no network
  call. Runs between the code and the register, so a non-success ends the attempt
  with the code still entered and unconsumed.
- `enrolDeviceKey(label:, stepUpCode:)` → invalidate `biometricEnabledProvider` +
  toast + pop. The label comes from `deviceLabelProvider` rather than
  `enrolDeviceKey`'s "SIMF mobile" default, so two enrolled devices stay
  distinguishable in the audit trail and on My Devices.

## 4. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct; all strings via `AppL10n`. The masked
email + code render LTR. Brand font applied once in the theme.

## 5. Testing
- **Widget** (`biometric_step_up_screen_test.dart`, 7 cases): on-open send, enrol on
  a correct code, the inline error on a wrong code, and one case per non-success OS
  outcome — cancel, no device screen lock, lockout and unavailable. The last two
  assert the **enrol** copy and explicitly assert the sign-in copy is absent, so a
  future change that drops the seam reddens rather than silently telling a
  signed-in user to use their password.
- **Unit** (`biometric_auth_test.dart`, group "localizedBiometricError caller
  seams"): the sign-in caller's defaults, the enrol caller's overrides (neither
  naming the password), and `noDeviceCredential` staying caller-neutral.
- **Golden** (`biometric_step_up_golden_test.dart`): `goldens/biometric_step_up.png`
  @375×812 RTL — render-regression lock (unbound screen; pumps frames, not settle,
  for the 1s resend timer).
- **E2E**: [`docs/tests/e2e/mobile-biometric-step-up.md`](../../../tests/e2e/mobile-biometric-step-up.md).

## 6. Clean-code DoD (D-554 freeze — 2026-06-30)
- [x] Lone sweep-tint const dropped → `SimfTokens.surfaceTint`; the long `build`
      split into `_buildHeader` / `_buildContent` / `_buildSubmitButton` /
      `_buildResendRow`
- [x] Shared, not copied: `OtpCodeBoxes` / `OtpMark`, `MaxWidthBody`
- [x] Flexible width via `MaxWidthBody(560)`; 0 raw `Color(0x…)` in the widget
- [x] **Unbound (no Figma node):** structural-only, render preserved + golden-locked
- [x] widget + golden tests + E2E catalogue + this doc, same changeset
- [x] `flutter analyze` clean (baseline info only); full suite green; wire contract
      (D-219) unchanged

## 7. Changelog
- **2026-08-20 (app deep-clean audit):** the failure copy on this screen became
  **caller-specific**. The OS sheet has passed `biometricOnly: true` since commit
  `3be516b5`, so it never offers the device PIN — but two shared messages still
  described the old fallback world: `biometricLockedOut` told a locked-out user to
  "use your device PIN" (a route the flag guarantees does not exist), and
  `biometricUnavailable` told them to "sign in with your password" (a form this
  screen does not have — the user is already signed in). `localizedBiometricError`
  grew two optional overrides and this screen passes `biometricLockedOutEnrol` /
  `biometricUnavailableEnrol` through them. `noDeviceCredential` was checked and
  left shared on purpose — a unit test now pins that call, so it reads as a decision
  rather than an oversight. (`biometricNotEnrolled` was checked too; it never went
  through `localizedBiometricError` in the first place — the sign-in path emits it
  directly before the OS sheet is ever reached.) +4 widget cases (3 → 7). No API, no render and no wire change; the golden held.
- **2026-06-30 (Phase 3, D-554):** clean-code freeze — dropped the sweep-tint const;
  split `build` into focused section builders; capped the body + CTA with
  `MaxWidthBody(560)`; added the render-lock golden + this doc. Behaviour + render
  unchanged. (Unbound auth screen — structural-only per the owner's Phase-3 cadence.)
- **D-369 / #7a:** built on the shared KSA OTP frame.
