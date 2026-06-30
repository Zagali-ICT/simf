# Badge activation (تفعيل حسابك) — mobile `/auth/badge-activation`

| Field | Value |
|---|---|
| Route | `RouteNames.badgeActivation` · pushed from the badge-scan screen (Part B, D-430) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/account/badge_activation_screen.dart` (`BadgeActivationScreen`) |
| Figma node | **none** — built on the shared KSA auth chrome (`SimfFormScaffold`); §13.5 unbound (structural-only, render preserved) |
| Shell | `SimfFormScaffold` (shared navy sweep + back/globe + logo header + beige card) |
| Providers | `authRepositoryProvider` (`badgeActivationStart` / `badgeActivationComplete`) |
| Tests | `test/features/account/badge_auth_screens_test.dart` (widget — activation email + code steps) · golden `test/golden/badge_activation_golden_test.dart` (`goldens/badge_activation.png`) · E2E [`mobile-badge-activation.md`](../../../tests/e2e/mobile-badge-activation.md) |
| Status | ✅ Real — Part B (D-430) → **clean-code reviewed + frozen (D-555, 2026-06-30)** |

## 1. Purpose
Activate a passwordless **badge** account: verify an emailed code, then set the
first password. Reached from the badge-scan screen. When the account already has a
real email the code is sent there automatically on open; when it has none
(`needsEmail`) the holder enters one first, which is verified and attached.

## 2. UI & behaviour
Two steps in one `SimfFormScaffold` beige card:
- **Email step** (`needsEmail` && code not yet sent) — email field + "send code"
  (`AuthSubmitButton`). Validates email format.
- **Code step** — the 6-digit code + new password + confirm, then "activate"
  (`AuthSubmitButton`). On success: toast + route to sign-in.

## 3. Data / API (wire contract D-219 frozen)
- `badgeActivationStart(qrId, email?)` → masked recipient + the code is sent.
- `badgeActivationComplete(qrId, code, newPassword, confirmPassword)` → toast +
  `goNamed(signIn)`.

## 4. Validation & edge cases
- Email: required + format (manual-entry step only — the auto-send path has no
  field to validate, D-430 guard).
- Code: required; new password: ≥8 + letter + digit (`isValidPassword`); confirm
  must match. `AuthFailure` / network → inline bilingual error.

## 5. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct; all strings via `AppL10n`. Brand font
applied once in the theme (incl. the gold CTA).

## 6. Clean-code DoD (D-555 freeze — 2026-06-30)
- [x] Already well-factored — built on the shared `SimfFormScaffold` +
      `AuthSubmitButton` + `SimfFieldLabel` / `simfFieldDecoration` /
      `simfInputStyle`; validators via `core/validation`; steps decomposed
      (`_emailStep` / `_codeStep`). **No screen-code change — render preserved.**
- [x] 0 raw `Color(0x…)` in the widget (uses `SimfTokens` directly)
- [x] **Unbound (no Figma node):** structural-only + golden-locked render
- [x] widget + golden tests + E2E catalogue + this doc, same changeset
- [x] `flutter analyze` clean (baseline info only); full suite green; wire contract
      (D-219) unchanged
- [ ] **Deferred (shared scaffold):** the non-pinned `SimfFormScaffold` still caps
      content at `ConstrainedBox(400)` rather than `MaxWidthBody(560)` (§13.7) — a
      shared-widget item affecting badge/forgot/reset together, to be done in a
      focused `SimfFormScaffold` pass (golden unchanged at 375px).

## 7. Changelog
- **2026-06-30 (Phase 3, D-555):** clean-code reviewed + frozen. The screen was
  already factored onto the shared auth chrome, so no code change was needed;
  added the render-lock golden + this doc + the E2E catalogue. Behaviour + render
  unchanged. (Unbound auth screen — structural-only per the owner's Phase-3 cadence.)
- **D-430 (Part B):** badge activation built.
