# Sign in (تسجيل الدخول) — mobile `/sign-in`

| Field | Value |
|---|---|
| Route | `/sign-in` (`RouteNames.signIn`) · **Guest entry** (unauthenticated; promotes to Visitor/Moderator/Staff on success) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/account/sign_in_screen.dart` (`SignInScreen`) |
| Figma node | `168:2800` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`; D-360/D-363) |
| Shell | Custom navy `Scaffold` — rotated sweep (168:2850) + back/globe top controls (627:2361) over the beige card |
| Providers | `authControllerProvider` (sign-in / device-key), `simfPrefsStorageProvider` (last-email + remember-me), `localeControllerProvider` (globe toggle), `biometricAvailableProvider` (Face-ID gate) |
| Tests | `test/features/account/sign_in_screen_test.dart` (widget, 18 cases) · golden `test/golden/sign_in_screen_golden_test.dart` (`goldens/sign_in_168-2800.png`) · auth controller in `packages/simf_auth_pkg/test/auth_controller_signin_test.dart` · E2E [`mobile-sign-in.md`](../../../tests/e2e/mobile-sign-in.md) (E2E-MOB003-001..017) |
| Status | ✅ Real — D-358/D-360 (KSA frame) → D-363 (globe + guest link) → D-422/D-430/D-441 (biometric/badge) → **clean-code frozen (D-549, 2026-06-30)** |
| Legacy detail | `docs/App/Page_003/` — retained as the detailed historical spec |

## 1. Purpose
The app's sign-in screen and only guest-mode entry. Authenticates email + password
against `POST /app/auth/sign-in`, then routes via the shared post-auth rule
(`routeAfterAuth`): 2FA accounts → email-OTP; incomplete profiles → profile screen
(D-374); otherwise Home. Also offers Face-ID device-key sign-in (D-441) and the
printed-badge QR sign-in (Part B, D-430).

## 2. Audience & access
Unauthenticated (the `auth` rate-limit bucket; not `AllowAnonymous`-gated at the
screen). On success the session's role drives the landing destination.

## 3. UI & behaviour (top → bottom)
The body is capped by `MaxWidthBody(560)` (fills a phone, doesn't stretch
edge-to-edge on a tablet — §13.7). The back/language controls are the **last Stack
child** so they paint and hit-test on top of the centred body.
1. **Top controls** (627:2361) — back chevron (start) + gold globe language toggle
   (end), forced LTR so the sides match the frame under RTL. Keys `signInBack` /
   `signInLanguage`.
2. **Header** — forum logo (`SimfLogo`) + name "الملتقى الدولى البحرى".
3. **Card** (beige `SimfTokens.cardBeige`, radius 4) — title "تسجيل الدخول".
4. **Email** (`TextFormField`, LTR, maxLength 50) — required + email-format validator
   (`isValidEmail`, D-548 2C); pre-filled from the last successful sign-in.
5. **Password** (`TextFormField`, obscured, maxLength 32) — required-only validator
   (sign-in must accept any existing password); eye-toggle suffix.
6. **Remember-me + forgot row** — the checkbox gates whether the email is stored
   (default ON natively, OFF on the web PoC, D-384); "نسيت كلمة المرور؟" → reset flow.
7. **Gold CTA "دخول"** — the shared `AuthSubmitButton` (busy spinner; disabled until
   both fields are non-empty).
8. **Create-account prompt** → sign-up form.
9. **Alt entry methods** (`SignInAltActions`): "or" divider, **Face-ID** button
   (shown only when a biometric is usable), **badge-QR** button (D-430 — a deliberate
   addition beyond the frame), and the underlined **guest** link (627:2390).

## 4. Data / API (wire contract D-219 frozen)
- `POST /app/auth/sign-in` → `AuthStateSignedIn` / `AuthStateAwaitingOtp`.
- Device-key path: the biometric sign-in calls `signInWithDeviceKey()` after the OS
  biometric + a prior enrolment (D-441); the button reports the two no-enrolment
  cases instead of failing silently (D-422).
- Reads/writes `StorageKeys.lastEmail` via `simfPrefsStorageProvider`.

## 5. Validation & edge cases
- Email empty → `requiredField`; malformed → `invalidEmail` (rejected before the
  network round-trip). Password empty → `requiredField`.
- Invalid credentials / network failure surface as an inline error and the password
  is cleared; the screen stays put.
- Face-ID hidden entirely on sensorless devices; when shown but not enrolled it
  reports `biometricUnavailable` / `biometricNotEnrolled`.

## 6. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct. All strings via `AppL10n`. The brand
font (incl. the gold CTA **and** the outlined Face-ID/badge buttons) is applied once
in the theme — the dark `outlinedButtonTheme` gained the brand family in D-549 (see
Changelog).

## 7. Testing
- **Widget** (`sign_in_screen_test.dart`, 18 cases): success→home + email store,
  malformed-email inline error, empty/blank-password gating, Face-ID visibility,
  remember-me both directions, incomplete-profile + 2FA routing, invalid creds,
  pre-fill, create-account / forgot / guest links, back-fallback, globe toggle.
- **Golden** (`sign_in_screen_golden_test.dart`): `goldens/sign_in_168-2800.png`
  @375×950 RTL (empty/default, Face-ID shown) — locks the frozen frame parity.
- **E2E**: [`docs/tests/e2e/mobile-sign-in.md`](../../../tests/e2e/mobile-sign-in.md).

## 8. Clean-code DoD (D-549 freeze — 2026-06-30)
- [x] Screen 676 → 589 lines; alt-entry block → `SignInAltActions`; shared
      `AuthSubmitButton` + new shared `AuthAltButton`; `_buildCard` split into
      focused section builders
- [x] Shared, not copied: `AuthSubmitButton`, `AuthAltButton`, `MaxWidthBody`,
      `SimfFieldLabel`, `simfFieldDecoration`/`simfInputStyle`, `SimfLogo`/`SimfSvgIcon`
- [x] Flexible width via `MaxWidthBody(560)`; 0 raw `Color(0x…)` in the widget
      (the `Color(0x80C9A84C)` disabled tint now comes from `authSubmitButtonStyle`)
- [x] Figma node `168:2800` bound; golden locks parity (documented D-430 deviation:
      the badge-QR button is kept beyond the frame)
- [x] widget + golden tests + E2E catalogue + this doc, same changeset
- [x] `flutter analyze` clean (baseline info only); full suite 706/706; wire
      contract (D-219) unchanged

## 9. Changelog
- **2026-07-11 (D-742):** OS-autofill fix — the login form is now an `AutofillGroup`
  with `username`/`password` hints and commits the FINAL submitted credentials via
  `TextInput.finishAutofillContext(shouldSave: _rememberMe)` on a successful sign-in,
  so the platform remembers the email the user actually used — not the heuristic
  first-typed guess it previously saved. Unchecking "remember me" discards both
  `lastEmail` and the OS autofill context. Render/goldens unchanged (non-visual).
- **2026-06-30 (Phase 3, D-549):** clean-code freeze — dropped screen-local colour
  aliases; reused `AuthSubmitButton`; extracted `SignInAltActions` + `AuthAltButton`;
  `MaxWidthBody(560)`; top controls moved on top with stable keys; added the
  `168:2800` golden + this consolidated doc. **App-wide:** the dark
  `outlinedButtonTheme` gained the brand font (Arabic outlined-button labels were
  tofu in goldens / OS-fallback on device) — corrected `session_detail` golden too.
  Behaviour unchanged.
- **D-441:** Face-ID device-key sign-in + post-sign-in enrol nudge.
- **D-430:** printed-badge QR sign-in (Part B).
- **D-422:** Face-ID button reports the no-enrolment cases instead of failing silently.
- **D-363:** globe language toggle + underlined guest link (627:2390).
- **D-358/D-360:** rebuilt to the KSA frame 168:2800.
