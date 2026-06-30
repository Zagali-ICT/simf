# Reset password (تعيين كلمة مرور جديدة) — mobile `/auth/reset-password`

| Field | Value |
|---|---|
| Route | `RouteNames.resetPassword` · reached from forgot-password (email carried) |
| Screen | `lib/features/account/reset_password_screen.dart` (`ResetPasswordScreen`) |
| Figma node | **none** — shared KSA auth chrome (`SimfFormScaffold`); §13.5 unbound (structural-only, render preserved) |
| Providers | `authRepositoryProvider` (`resetPassword`) · `simfPrefsStorageProvider` (pre-fill the email on sign-in, non-web) |
| Tests | `test/features/account/reset_password_screen_test.dart` (widget) · golden `test/golden/reset_password_golden_test.dart` (`goldens/reset_password.png`) · E2E in [`mobile-sign-in.md`](../../../tests/e2e/mobile-sign-in.md) (the forgot/reset auth flow) |
| Status | ✅ Real — D-374 (KSA chrome) → **clean-code reviewed + frozen (D-557, 2026-06-30)** |

## Purpose & behaviour
Collects the emailed OTP + a new password (+ confirm) and calls
`POST /app/auth/reset-password`, then returns to sign-in with the email pre-filled
(non-web; D-384). Built on `SimfFormScaffold`: title + body + code + new-password
(eye toggle) + confirm + the gold `AuthSubmitButton` "reset". Validators: required +
6-digit code; `isValidPassword` policy; confirm-match.

## Clean-code DoD (D-557 freeze — 2026-06-30)
- [x] Already factored onto the shared `SimfFormScaffold` + `AuthSubmitButton` +
      `SimfFieldLabel`/`simfFieldDecoration`/`simfInputStyle` + `core/validation` +
      `SimfTokens` direct — **no screen-code change (render preserved)**
- [x] 0 raw `Color(0x…)`; unbound → golden-locked render
- [x] widget + golden tests + this doc + E2E (mobile-sign-in.md), same changeset
- [x] `flutter analyze` clean (baseline info only); full suite green; wire contract
      (D-219) unchanged
- [ ] Deferred (shared): non-pinned `SimfFormScaffold` cap `400`→`MaxWidthBody(560)`
      (§13.7); dedicated "enter the 6-digit code" l10n message (reuses `requiredField`)
      — both noted follow-ups

## Changelog
- **2026-06-30 (Phase 3, D-557):** reviewed + frozen; no code change (already clean);
  added the render-lock golden + this doc.
