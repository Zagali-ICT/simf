# Badge password (أدخل كلمة المرور) — mobile `/auth/badge-password`

| Field | Value |
|---|---|
| Route | `RouteNames.badgePassword` → `/auth/badge-password?qrId&name&masked` · pushed from the badge-scan screen when the resolved account already has a password (D-738) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/account/badge_password_screen.dart` (`BadgePasswordScreen`) |
| Figma node | **none** — built on the navy auth family (D-659): `Scaffold(navySurface)` + `AccountSubHeader` + gold `AuthSubmitButton`, sibling to `BadgeActivationScreen` |
| Shell | `Scaffold(SimfTokens.navySurface)` + `AccountSubHeader` (back + title) + `MaxWidthBody(560)` body + gold CTA |
| Providers | `authControllerProvider` (`signInWithBadge`) → `maybeOfferBiometricEnrolment` + `routeAfterAuth` |
| Endpoint | `POST /app/auth/badge-sign-in` `{ qrId, password }` — `AllowAnonymous`, `auth` + `auth-email` rate limits; returns the standard `SignInResponse` (tokens or the email-OTP 2FA challenge) |
| Permissions | None — anonymous / pre-login (like email sign-in). The QR selects the account; the password (+ any 2FA / lockout) runs the real sign-in pipeline |
| Tests | `test/features/account/badge_password_screen_test.dart` (widget — render, wrong-password clears + generic error, 2FA→OTP, Arabic) · API `tests/SIMF.Api.Tests/BadgeSignInTests.cs` · E2E [`mobile-badge-activation.md`](../../../tests/e2e/mobile-badge-activation.md) (E2E-MOBBADGE-008..013) |
| Status | ✅ Real — D-738 (badge-QR sign-in password step) |

## 1. Purpose
Complete a **badge-QR sign-in** for a returning holder whose account already has a
password. The holder scanned their printed-badge QR on the badge-scan screen; the
server resolved it (`POST /app/auth/resolve-badge`) and returned
`hasPassword = true` with the display name + masked on-file email. Before D-738 that
branch dumped the holder onto a blank sign-in screen; now it lands here with the
name + masked email pre-shown so **only the password** is typed. The badge never
substitutes for the password (D-430).

## 2. UI & behaviour
One `Scaffold(navySurface)` column:
- **`AccountSubHeader`** — back button (pops, or falls back to the sign-in screen)
  + the title `badgePasswordTitle` ("أدخل كلمة المرور" / "Enter your password").
- **Greeting** — `badgeWelcomeName(name)` ("مرحبًا {name}" / "Welcome, {name}"),
  shown only when a name was passed.
- **Account line** — `badgeSignInAccountLine(masked)` ("تسجيل الدخول إلى الحساب
  {masked}" / "Signing in to {masked}"), **forced LTR** so the masked email reads
  correctly in Arabic; shown only when a masked email was passed.
- **Password field** — `passwordLabel`, obscured with a `NavyPasswordToggle` show/
  hide, `maxLength: 128`, submits on the keyboard action.
- **Forgot-password link** — `forgotPasswordLink` → `RouteNames.forgotPassword`.
- **Submit** — gold `AuthSubmitButton` labelled `signInButton` ("دخول" / "Sign
  in"); enabled only when the password is non-empty and no request is in flight.

On submit → `authControllerProvider.signInWithBadge(qrId, password, displayEmail:
masked)` → `POST /app/auth/badge-sign-in`:
- **`AuthStateSignedIn`** → offer the Face-ID enrol nudge
  (`maybeOfferBiometricEnrolment`) → `routeAfterAuth` (same as password sign-in).
- **`AuthStateAwaitingOtp`** (2FA account) → `goNamed(RouteNames.verifyOtp)` — the
  shared email-OTP second-factor screen.
- **`AuthFailure`** → inline bilingual error + the password field is **cleared**.

## 3. Data / API
- **Resolve (previous screen):** `POST /app/auth/resolve-badge { qrId }` →
  `{ found, hasPassword, displayName, needsEmail, maskedEmail }`. `hasPassword=true`
  routes here; a placeholder `@simf.local` account returns a null masked email.
- **This screen:** `POST /app/auth/badge-sign-in { qrId, password }` → the standard
  `SignInResponse`. The endpoint delegates to the real `ISignInService` pipeline, so
  the response is identical to email sign-in (tokens or the OTP challenge). Wire
  contract additive (D-219) — no schema change.

## 4. Validation & edge cases
- **Password required** — the field validator blocks submit on a blank value
  (`requiredField`).
- **Generic invalid credentials** — an unknown / non-approved / passwordless
  `qrId` and a wrong password ALL return the SAME `401 AUTH_INVALID_CREDENTIALS`
  ("The email address or password is not correct." / "البريد الإلكتروني أو كلمة
  المرور غير صحيحة."). The public badge is never a valid-QR oracle; the field is
  cleared on failure. Server writes a `SignInBadCredentials` audit (detail "badge").
- **Lockout** — repeated wrong passwords lock the account exactly as email sign-in
  does (covered by `BadgeSignInTests.Badge_sign_in_locks_the_account_after_five_wrong_passwords`).
- **2FA** — a visitor account with the email second factor continues to the shared
  verify-otp screen; completing the emailed code finishes sign-in.
- **Network / 5xx** — surfaced inline via `failure.source.localizedMessage`.

## 5. i18n / RTL
Bilingual (ar/en), Arabic-first; every string via `AppL10n`. RTL-correct — the
masked-email account line is pinned `TextDirection.ltr` so the address is not
mirrored. Brand font applied once in the theme (incl. the gold CTA).

## 6. Accessibility
- `AccountSubHeader` exposes a labelled back control; the busy state disables it.
- The password field carries a visible label + a show/hide toggle; the CTA shows a
  spinner while the request is in flight (no double-submit).
- Content capped by `MaxWidthBody(560)` and stretches within portrait (tablet-safe,
  §13.7).

## 7. E2E scenarios
Catalogued in [`mobile-badge-activation.md`](../../../tests/e2e/mobile-badge-activation.md)
(the badge-QR auth family shares one catalogue file):
- **E2E-MOBBADGE-008** — has-password badge → password step → signed in.
- **E2E-MOBBADGE-009** — wrong password → generic error + field cleared.
- **E2E-MOBBADGE-010** — 2FA account → email-OTP screen.
- **E2E-MOBBADGE-011** — unknown / non-approved / passwordless → same generic message.
- (Scanner: **E2E-MOBBADGE-012/013** cover the shared `SimfScannerBody` on the
  badge-scan entry — camera-denied error card + steady-QR dedupe.)

## 8. Changelog
- **D-738 (2026-07-11):** badge-QR sign-in password step built. A resolved
  has-password badge routes to `/auth/badge-password` (was: blank sign-in); the new
  anonymous `POST /app/auth/badge-sign-in` endpoint runs the full password + 2FA +
  lockout pipeline; unknown / non-approved / passwordless qrId + wrong password all
  return the same generic `401 AUTH_INVALID_CREDENTIALS`. Widget + API tests + the
  E2E catalogue + this doc landed in the same changeset.
