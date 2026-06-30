# Forgot password (استعادة كلمة المرور) — mobile `/auth/forgot-password`

| Field | Value |
|---|---|
| Route | `RouteNames.forgotPassword` · reached from sign-in |
| Screen | `lib/features/account/forgot_password_screen.dart` (`ForgotPasswordScreen`) |
| Figma node | **none** — shared KSA auth chrome (`SimfFormScaffold`); §13.5 unbound (structural-only, render preserved) |
| Providers | `authRepositoryProvider` (`forgotPassword`) |
| Tests | `test/features/account/forgot_password_screen_test.dart` (widget) · golden `test/golden/forgot_password_golden_test.dart` (`goldens/forgot_password.png`) · E2E in [`mobile-sign-in.md`](../../../tests/e2e/mobile-sign-in.md) (the forgot/reset auth flow) |
| Status | ✅ Real — D-374 (KSA chrme) → **clean-code reviewed + frozen (D-556, 2026-06-30)** |

## Purpose & behaviour
Emails a reset OTP for the entered address, then routes to the reset screen with the
email carried forward. Enumeration-resistant on the server (always success-shaped),
so the app always proceeds to the reset step. Built on `SimfFormScaffold`: title +
body + email field (`isValidEmail`) + the gold `AuthSubmitButton` "send code".

## Clean-code DoD (D-556 freeze — 2026-06-30)
- [x] Already factored onto the shared `SimfFormScaffold` + `AuthSubmitButton` +
      `SimfFieldLabel`/`simfFieldDecoration`/`simfInputStyle` + `core/validation` +
      `SimfTokens` direct — **no screen-code change (render preserved)**
- [x] 0 raw `Color(0x…)`; unbound → golden-locked render
- [x] widget + golden tests + this doc + E2E (mobile-sign-in.md), same changeset
- [x] `flutter analyze` clean (baseline info only); full suite green; wire contract
      (D-219) unchanged
- [ ] Deferred (shared): non-pinned `SimfFormScaffold` cap `400`→`MaxWidthBody(560)`
      (§13.7) — one focused shared-scaffold pass (invisible at 375px golden)

## Changelog
- **2026-06-30 (Phase 3, D-556):** reviewed + frozen; no code change (already clean);
  added the render-lock golden + this doc.
