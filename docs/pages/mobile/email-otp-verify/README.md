# Email-OTP — sign-in 2FA (التحقق بالبريد) — mobile `/auth/verify-otp`

| Field | Value |
|---|---|
| Route | `/auth/verify-otp` (`RouteNames.verifyOtp`) · **mid-sign-in** (the controller holds the `otpToken`) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/account/email_otp_verify_screen.dart` (`EmailOtpVerifyScreen`) |
| Figma node | `758:2616` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`; D-369 — the shared OTP frame) |
| Shell | Custom navy `Scaffold` — sweep + back/title header + pinned gold CTA + resend row |
| Providers | `authControllerProvider` (`verifyOtp` / `resendOtp`; reads `AuthStateAwaitingOtp.email`) |
| Tests | `test/features/account/email_otp_verify_screen_test.dart` (widget, 4 cases — **added D-552**) · golden `test/golden/email_otp_verify_golden_test.dart` (`goldens/email_otp_758-2616.png`) · E2E [`mobile-sign-in.md`](../../../tests/e2e/mobile-sign-in.md) (the sign-in 2FA scenarios) |
| Status | ✅ Real — D-369 (KSA OTP frame 758:2616) → #12 (resend in place) / D-441 (Face-ID enrol on the OTP path) → **clean-code frozen (D-552, 2026-06-30)** |
| Legacy detail | `docs/App/Page_003/` — the sign-in flow's detailed historical spec |

## 1. Purpose
The **email second factor** reached after a sign-in when the account has 2FA on
(Visitor-only; no TOTP path). The user enters the emailed 6-digit code →
`POST /app/auth/verify-otp`. On success the shared post-auth rule routes (incomplete
profile first, D-374) after the Face-ID enrolment offer (D-441). A resend countdown
shows below the boxes; when it elapses, "إعادة الإرسال" re-issues the code **in
place** via `POST /app/auth/resend-otp` (#12, keyed by the ticket) and restarts it.

## 2. Audience & access
Mid-sign-in only — the `authControllerProvider` is in `AuthStateAwaitingOtp` (carries
the `otpToken` + the recipient `email`). No standalone deep link.

## 3. UI & behaviour (top → bottom)
The scroll body + the pinned CTA are each capped by `MaxWidthBody(560)`.
1. **Header** — back chevron (`ic_back.svg`) + centred "التحقق بالبريد".
2. **`OtpMark`** — the gold-ringed mail circle.
3. **"أدخل رمز التحقق"** title.
4. **"أرسلنا رمزاً الى"** + the recipient email on a gold line (or the generic
   sentence when no address is carried).
5. **`OtpCodeBoxes`** — six segmented boxes over one invisible capture field.
6. **Resend countdown** — "إعادة الإرسال خلال mm:ss".
7. **Pinned gold CTA "تحقق"** — disabled until 6 digits; busy spinner.
8. **Resend row** — "لم يصلك الرمز؟ إعادة الإرسال" (the action arms only at 0).

## 4. Data / API (wire contract D-219 frozen)
- `verifyOtp(code)` → on `AuthStateSignedIn`, `maybeOfferBiometricEnrolment` then
  `routeAfterAuth`.
- `resendOtp()` (#12) → restart the cooldown + the "code resent" toast.

## 5. Validation & edge cases
- CTA enabled only at exactly 6 digits (`OtpCodeBoxes` is digits-only, max 6).
- `AuthFailure` / network failure → inline bilingual error; the resend action is
  inert until the countdown reaches 0 (recognizer attached only then).

## 6. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct. All strings via `AppL10n`. The code
digits + the email render LTR. Brand font applied once in the theme.

## 7. Testing
- **Widget** (`email_otp_verify_screen_test.dart`, 4 cases — added D-552): renders
  the email + boxes + controls; verify disabled until 6 digits; tapping verify
  calls `verifyOtp` with the code; back-fallback to sign-in. (Pumps frames, not
  `pumpAndSettle`, because of the 1s resend timer.)
- **Golden** (`email_otp_verify_golden_test.dart`): `goldens/email_otp_758-2616.png`
  @375×812 RTL — locks the frozen frame parity.
- **E2E**: the sign-in 2FA scenarios in
  [`docs/tests/e2e/mobile-sign-in.md`](../../../tests/e2e/mobile-sign-in.md).

## 8. Clean-code DoD (D-552 freeze — 2026-06-30)
- [x] Lone sweep-tint const dropped → `SimfTokens.surfaceTint`; the long `build`
      split into `_buildHeader` / `_buildContent` / `_buildSubmitButton` /
      `_buildResendRow`
- [x] Shared, not copied: `OtpCodeBoxes` / `OtpMark` (already shared), `MaxWidthBody`
- [x] Flexible width via `MaxWidthBody(560)` (scroll body + pinned CTA); 0 raw
      `Color(0x…)` in the widget
- [x] Figma node `758:2616` bound; golden locks parity
- [x] **widget test added (was missing)** + golden + this doc, same changeset
- [x] `flutter analyze` clean (baseline info only); full suite green; wire contract
      (D-219) unchanged

## 9. Changelog
- **2026-06-30 (Phase 3, D-552):** clean-code freeze — dropped the sweep-tint const;
  split the ~220-line `build` into focused section builders; capped the body + CTA
  with `MaxWidthBody(560)`; **added the previously-missing widget test (4 cases)** +
  the `758:2616` golden + this consolidated doc. Behaviour + render unchanged.
- **#12 / D-441:** resend-in-place + Face-ID enrolment offered on the OTP path.
- **D-369:** rebuilt to the shared KSA OTP frame 758:2616.
