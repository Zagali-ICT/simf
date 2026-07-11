# Sign up — create account (إنشاء حساب) — mobile `/sign-up`

| Field | Value |
|---|---|
| Route | `/sign-up` (`RouteNames.signUpForm`) · **Guest** (unauthenticated; creates the under-review account) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/account/sign_up_form_screen.dart` (`SignUpFormScreen`) |
| Figma node | `168:3454` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`; D-370) |
| Shell | Custom navy `Scaffold` — rotated sweep (168:3534) + shared `AuthTopControls` + `AuthBrandHeader` over the beige card |
| Providers | `authControllerProvider` (`signUp`) · `localeControllerProvider` (globe toggle) |
| Tests | `test/features/account/sign_up_form_screen_test.dart` (widget, 8 cases) · golden `test/golden/sign_up_form_golden_test.dart` (`goldens/sign_up_form_168-3454.png`) · E2E [`mobile-sign-up-form.md`](../../../tests/e2e/mobile-sign-up-form.md) |
| Status | ✅ Real — D-370 (KSA frame 168:3454) → D-198/D-270 (enumeration-resistant; server re-validates confirm) → **clean-code frozen (D-551, 2026-06-30)** |
| Legacy detail | `docs/App/Page_005/` — retained as the detailed historical spec |

## 1. Purpose
Sign-up **step 1**: email + password + confirm. On the generic **201** it forwards
to the email-OTP screen (Page 006) carrying the address. Enumeration-resistant —
identical for a new and an already-registered email (D-198); does **not** sign the
user in (creates the under-review Visitor account + triggers the email code).

## 2. Audience & access
Unauthenticated (the `auth` rate-limit bucket). `confirmPassword` is checked locally
for instant feedback **and** sent in the body — the server re-validates it (D-270).

## 3. UI & behaviour (top → bottom)
The body is capped by `MaxWidthBody(560)`; the back/globe controls are the last
Stack child (on top of the centred body).
1. **Top controls** — shared `AuthTopControls` (back chevron + gold globe, SVG
   glyphs matching the sign-in header; keys `authBack` / `authLanguage`).
2. **Header** — shared `AuthBrandHeader` (forum logo + name).
3. **Card** — title "إنشاء حساب".
4. **Email** (`TextFormField`, LTR, maxLength 50) — `isValidEmail` validator.
5. **Password** (obscured, maxLength 32) — `isValidPassword` (≥8 + letter + digit);
   SVG eye-toggle.
6. **Confirm password** (obscured, maxLength 32) — must equal the password; SVG
   eye-toggle; submit on field-submit.
7. **Mandatory T&C checkbox** (`AccountTermsCheckbox`, D-719) — "أوافق على الشروط
   والأحكام"; the "الشروط والأحكام" span is a link that opens Page 009 in consent
   mode and a موافق there auto-checks the box. Gates the CTA; an unchecked submit
   shows "يجب الموافقة على الشروط والأحكام" and reddens the box.
8. **Gold CTA "إنشاء حساب"** — shared `AuthSubmitButton` (busy spinner).
9. **Foot** — "هل لديك حساب؟ تسجيل الدخول" → sign-in.

## 4. Data / API (wire contract D-219 frozen)
- `POST` via `authControllerProvider.signUp(email, password, confirmPassword)`.
- On the generic 201 → `pushNamed(emailOtp, {email})` + the "check your email" toast.

## 5. Validation & edge cases
- Email format, password policy, confirm-match — all inline
  (`AutovalidateMode.onUserInteraction`). The form blocks submit until valid.
- **T&C acceptance is mandatory** (D-719): the submit is gated on the checkbox;
  an unchecked submit shows the terms error next to the box (alongside any field
  errors, not one gate at a time) and sends no request. Ticking the box — or
  accepting on Page 009 — clears it. Client-side only (D8); no wire change.
- `AuthFailure` / network failure → inline bilingual error; the CTA re-enables.

## 6. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct. All strings via `AppL10n`. Top
controls forced LTR so the chevron + globe sides match the frame under RTL. Brand
font applied once in the theme (incl. the gold CTA).

## 7. Testing
- **Widget** (`sign_up_form_screen_test.dart`, 8 cases): valid sign-up →
  email-OTP + toast, the three validators, the duplicate-email 201 (no
  enumeration branch), `AuthFailure` inline error, back-fallback to sign-in, globe
  toggle.
- **Golden** (`sign_up_form_golden_test.dart`): `goldens/sign_up_form_168-3454.png`
  @375×812 RTL — locks the frozen frame parity.
- **E2E**: [`docs/tests/e2e/mobile-sign-up-form.md`](../../../tests/e2e/mobile-sign-up-form.md).

## 8. Clean-code DoD (D-551 freeze — 2026-06-30)
- [x] Screen 432 → 344 lines; back/globe + header + CTA reuse shared
      `AuthTopControls` / `AuthBrandHeader` / `AuthSubmitButton`; foot link reuses
      `authLinkButtonStyle`; sign-in prompt to `_buildSignInPrompt`
- [x] Shared, not copied: the four `auth_chrome` widgets/styles + `MaxWidthBody`
      + `SimfFieldLabel` / `simfFieldDecoration` / `simfInputStyle`
- [x] Flexible width via `MaxWidthBody(560)`; 0 raw `Color(0x…)` in the widget
      (dropped the 8 screen-local aliases + the `Color(0x80C9A84C)` disabled tint)
- [x] **Figma parity fix:** the Material back/globe/eye icons swapped to the
      frame's exact SVG glyphs (matching the sign-in header) — golden re-audited
      against `168:3454`
- [x] widget + golden tests + E2E catalogue + this doc, same changeset
- [x] `flutter analyze` clean (baseline info only); full suite 708/708; wire
      contract (D-219) unchanged

## 9. Changelog
- **2026-07-11 (D-742):** OS-autofill fix — the form is now an `AutofillGroup` with
  `newUsername`/`newPassword` hints and commits the FINAL submitted email/password
  via `TextInput.finishAutofillContext()` on a successful submit, so a corrected
  address replaces any first-typed guess the OS grabbed (fixes "login kept offering
  the mistyped sign-up email"). Render/goldens unchanged.
- **2026-07-09 (D-719):** added the mandatory `AccountTermsCheckbox` — registration
  now gates the CTA on an explicit T&C accept (the profile / More menu keep the
  read-only link). The "الشروط والأحكام" span opens Page 009 in consent mode and a
  موافق there auto-checks the box. Client-side only (D8); no wire-contract change.
  New l10n `termsAcceptLead` / `termsMustAccept`; +4 widget cases (011–014); golden
  re-locked with the box present. Owner-mandated addition, no Figma frame of its own.
- **2026-06-30 (Phase 3, D-551):** clean-code freeze — dropped 8 colour aliases for
  `SimfTokens`; adopted the shared `AuthTopControls` / `AuthBrandHeader` /
  `AuthSubmitButton` / `authLinkButtonStyle`; **swapped the Material back/globe/eye
  icons to the frame's exact SVG glyphs** (parity with the sign-in header);
  `MaxWidthBody(560)`; added the `168:3454` golden + this consolidated doc.
- **D-370:** rebuilt to the KSA frame 168:3454.
- **D-198/D-270:** enumeration-resistant 201; server re-validates confirm-password.
