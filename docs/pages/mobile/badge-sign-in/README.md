# Badge sign-in (امسح شارتك) — mobile `/auth/badge-sign-in`

| Field | Value |
|---|---|
| Route | `RouteNames.badgeSignIn` · reached from the sign-in screen (Part B, D-430) |
| Screen | `lib/features/account/badge_sign_in_screen.dart` (`BadgeSignInScreen`) |
| Figma node | **none** — thin wrapper over the shared `QrScanView`; §13.5 unbound (structural-only, render preserved) |
| Providers | `authRepositoryProvider` (`resolveBadge`) |
| Tests | `test/features/account/badge_auth_screens_test.dart` (widget) · golden `test/golden/badge_sign_in_golden_test.dart` (`goldens/badge_sign_in.png`) · E2E [`mobile-badge-activation.md`](../../../tests/e2e/mobile-badge-activation.md) (badge flow) |
| Status | ✅ Real — Part B (D-430) → **clean-code reviewed + frozen (D-558, 2026-06-30)** |

## Purpose & behaviour
Badge-QR sign-in entry. The holder scans (or types) the QR printed on their badge;
`resolveBadge(qrId)` branches: an account that already has a password → normal
sign-in; a passwordless account → the set-password activation screen; an
unrecognised badge → a toast. Pre-login (anonymous). The UI is the shared
[`QrScanView`] (manual-first, bounded opt-in camera, never traps the user — D-426).

## Clean-code DoD (D-558 freeze — 2026-06-30)
- [x] Already a thin wrapper over the shared `QrScanView` — no colours, no inline
      styles, all strings via `AppL10n`; business logic in `_onCode`. **No
      screen-code change (render preserved).**
- [x] Unbound → golden-locked render (camera off in the golden/test)
- [x] widget + golden tests + this doc + E2E (mobile-badge-activation.md), same
      changeset
- [x] `flutter analyze` clean (baseline info only); full suite green; wire contract
      (D-219) unchanged

## Changelog
- **2026-06-30 (Phase 3, D-558):** reviewed + frozen; no code change (already a thin
  `QrScanView` wrapper); added the render-lock golden + this doc.
