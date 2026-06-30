# Email verification — sign-up step 2 (التحقق بالبريد) — mobile `/auth/email-otp`

| Field | Value |
|---|---|
| Route | `/auth/email-otp` (`RouteNames.emailOtp`) · **Guest** (anonymous; carries the `email` from Page 005) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/account/sign_up_email_verify_screen.dart` (`SignUpEmailVerifyScreen`) |
| Figma node | `505:837` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`; D-364) |
| Shell | Custom navy `Scaffold` — sweep + back/title header + pinned gold CTA + resend row |
| Providers | `authControllerProvider` (`verifyEmail` / `resendCode`) |
| Tests | `test/features/account/sign_up_email_verify_screen_test.dart` (widget, 3 cases) · golden `test/golden/sign_up_email_verify_golden_test.dart` (`goldens/sign_up_email_verify_505-837.png`) · E2E [`mobile-email-otp.md`](../../../tests/e2e/mobile-email-otp.md) |
| Status | ✅ Real — D-364 (KSA OTP frame 505:837) → **clean-code frozen (D-553, 2026-06-30)** |
| Legacy detail | `docs/App/Page_006/` — retained as the detailed historical spec |

## 1. Purpose
Sign-up **step 2** — the visitor enters the 6-digit code emailed after Page 005 →
`POST /app/auth/verify-email { email, code }` (anonymous). Success toasts and routes
to **sign-in** (verify-email issues no session; the authenticated profile step needs
a token). **Resend** re-issues via `POST /app/auth/resend-code` and starts the
cooldown from the returned `codeExpiresInSeconds`.

## 2. Audience & access
Unauthenticated. The `email` is a navigation argument from the sign-up form; no token.

## 3. UI & behaviour (top → bottom)
The scroll body + the bottom actions are each capped by `MaxWidthBody(560)`.
1. **Header** — back chevron + centred "التحقق بالبريد".
2. **`OtpMark`** — the gold-ringed mail circle.
3. **"أدخل رمز التحقق"** title + "أرسلنا رمز التحقق إلى" + the recipient email (gold).
4. **`OtpCodeBoxes`** — six boxes over one invisible capture field.
5. **Cooldown row** — shown only while a resend cooldown is running.
6. **Pinned gold CTA "تحقق"** — disabled until 6 digits; busy spinner.
7. **Resend row** — "لم يصلك الرمز؟ إعادة الإرسال" (enabled when no cooldown).

> **Note (preserved behaviour):** the resend cooldown starts only *after* a resend,
> so on first open no countdown shows and resend is immediately available — unlike
> the 2FA `verifyOtp` screen, which starts a 60s countdown on entry. (Documented for
> a possible future alignment; not changed here.)

## 4. Data / API (wire contract D-219 frozen)
- `verifyEmail(email, code)` → toast + `goNamed(signIn)`.
- `resendCode(email)` → returns the cooldown seconds; `_startCooldown` restarts it.

## 5. Validation & edge cases
- CTA enabled only at exactly 6 digits (`OtpCodeBoxes` is digits-only, max 6).
- `AuthFailure` / network failure → inline bilingual error + the code field clears.

## 6. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct. All strings via `AppL10n`. The code +
email render LTR. Brand font applied once in the theme.

## 7. Testing
- **Widget** (`sign_up_email_verify_screen_test.dart`, 3 cases): verify success →
  sign-in, the 6-digit gate, resend → cooldown.
- **Golden** (`sign_up_email_verify_golden_test.dart`):
  `goldens/sign_up_email_verify_505-837.png` @375×812 RTL (initial state) — locks
  the frozen frame parity.
- **E2E**: [`docs/tests/e2e/mobile-email-otp.md`](../../../tests/e2e/mobile-email-otp.md).

## 8. Clean-code DoD (D-553 freeze — 2026-06-30)
- [x] Lone sweep-tint const dropped → `SimfTokens.surfaceTint`; the long `build`
      split into `_buildHeader` / `_buildContent` / `_buildCooldownRow` /
      `_buildBottomActions`
- [x] Shared, not copied: `OtpCodeBoxes` / `OtpMark`, `MaxWidthBody`,
      `authLinkButtonStyle` (resend link)
- [x] Flexible width via `MaxWidthBody(560)` (scroll body + bottom actions); 0 raw
      `Color(0x…)` in the widget
- [x] Figma node `505:837` bound; golden locks parity
- [x] widget + golden tests + E2E catalogue + this doc, same changeset
- [x] `flutter analyze` clean (baseline info only); full suite green; wire contract
      (D-219) unchanged

## 9. Changelog
- **2026-06-30 (Phase 3, D-553):** clean-code freeze — dropped the sweep-tint const;
  split `build` into focused section builders; capped the body + actions with
  `MaxWidthBody(560)`; the resend link reuses `authLinkButtonStyle`; added the
  `505:837` golden + this consolidated doc. Behaviour + render unchanged.
- **D-364:** rebuilt to the KSA OTP frame 505:837.
