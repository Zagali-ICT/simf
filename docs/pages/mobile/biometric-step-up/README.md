# Biometric step-up — enable Face-ID (تأكيد بصمة الوجه) — mobile `/auth/biometric-step-up`

| Field | Value |
|---|---|
| Route | `RouteNames.biometricStepUp` · pushed from the Face-ID toggle / post-sign-in nudge |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/account/biometric_step_up_screen.dart` (`BiometricStepUpScreen`) |
| Figma node | **none** — reuses the shared KSA OTP frame (D-369); §13.5 unbound (structural-only clean-code, render preserved) |
| Shell | Custom navy `Scaffold` — sweep + back/title header + pinned gold CTA + resend row |
| Providers | `authControllerProvider` (`sendBiometricStepUp` / `enrolDeviceKey`) · `biometricEnabledProvider` (invalidated on success) |
| Tests | `test/features/account/biometric_step_up_screen_test.dart` (widget, 3 cases) · golden `test/golden/biometric_step_up_golden_test.dart` (`goldens/biometric_step_up.png`) · E2E [`mobile-biometric-step-up.md`](../../../tests/e2e/mobile-biometric-step-up.md) |
| Status | ✅ Real — #7a (D-369 OTP frame) → **clean-code frozen (D-554, 2026-06-30)** |

## 1. Purpose
The emailed-OTP **step-up** that confirms the user wants to ENABLE biometric
(Face-ID) sign-in. On open it requests a code (`POST /app/auth/device-keys/step-up`);
entering it enrols the device key (`POST /app/auth/device-keys` with the code), which
the server rejects without a fresh code — so a borrowed-but-unlocked phone can't
silently bind a biometric credential.

## 2. UI & behaviour (top → bottom)
The scroll body + the pinned CTA are each capped by `MaxWidthBody(560)`.
1. **Header** — back chevron + centred "تأكيد بصمة الوجه".
2. **`OtpMark`** (fingerprint) + "أدخل رمز التأكيد" + the masked recipient (gold).
3. **`OtpCodeBoxes`** — six boxes; **resend countdown** below; pinned gold CTA
   "تحقق" (disabled until 6 digits); resend row.

## 3. Data / API (wire contract D-219 frozen)
- `sendBiometricStepUp()` → masked recipient + start the cooldown (called on open).
- `enrolDeviceKey(stepUpCode)` → invalidate `biometricEnabledProvider` + toast + pop.

## 4. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct; all strings via `AppL10n`. The masked
email + code render LTR. Brand font applied once in the theme.

## 5. Testing
- **Widget** (`biometric_step_up_screen_test.dart`, 3 cases): on-open send, enrol on
  a correct code, the inline error on a wrong code.
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
- **2026-06-30 (Phase 3, D-554):** clean-code freeze — dropped the sweep-tint const;
  split `build` into focused section builders; capped the body + CTA with
  `MaxWidthBody(560)`; added the render-lock golden + this doc. Behaviour + render
  unchanged. (Unbound auth screen — structural-only per the owner's Phase-3 cadence.)
- **D-369 / #7a:** built on the shared KSA OTP frame.
