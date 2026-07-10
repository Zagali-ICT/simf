# E2E test catalogue — `Badge activation` (`badgeActivation`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). This file catalogues the
> whole **badge-QR auth family** — the scan entry (`badge_sign_in_screen.dart`),
> the D-738 **password step** for a returning holder
> (`badge_password_screen.dart`, spec
> [`mobile/badge-password/README.md`](../../pages/mobile/badge-password/README.md)),
> and the Part B (D-430) **passwordless activation** screen
> (`badge_activation_screen.dart`, spec
> [`mobile/badge-activation/README.md`](../../pages/mobile/badge-activation/README.md)).
> Runner-agnostic Gherkin. The activation glue is widget-tested in
> `src/Mobile/simf_app/test/features/account/badge_auth_screens_test.dart` (+ the
> render-lock golden `test/golden/badge_activation_golden_test.dart`); the D-738
> password step in
> `src/Mobile/simf_app/test/features/account/badge_password_screen_test.dart`; the
> backend resolve/activation branches in `tests/SIMF.Api.Tests/BadgeAuthTests.cs`
> and the badge sign-in branch in `tests/SIMF.Api.Tests/BadgeSignInTests.cs`.
>
> **D-737 (unified scanner):** the badge-scan entry now uses the shared
> `SimfScannerBody` (`lib/app/widgets/simf_scanner_body.dart`, via `QrScanView`) —
> camera-first with the gold viewfinder, an always-visible manual-entry fallback,
> a single `ScanGate` dedupe policy (`lib/core/utils/scan_gate.dart` — no more
> repeated "not recognised" snackbars on a steady QR), and a visible
> camera-permission-denied error card instead of a black dead-end.
>
> **D-738 (password step):** a returning holder whose account already has a
> password no longer lands on a blank sign-in; the scan resolves to
> `/auth/badge-password` showing the resolved name + masked email, and completes
> sign-in with only the password via `POST /app/auth/badge-sign-in`.

| | |
|--|--|
| **Route** | aux auth `RouteNames.badgeActivation` — pushed from the badge-scan screen |
| **APIs** | `POST /app/auth/badge/activation/start` (`{ qrId, email? }` → masked email) · `POST /app/auth/badge/activation/complete` (`{ qrId, code, newPassword, confirmPassword }`) — both `AllowAnonymous`, `auth` limiter |
| **Surface** | Mobile (Flutter) — passwordless badge holder, not yet signed in |
| **Auth setup** | A scanned badge `qrId`. Codes via the email channel (read from `SIMF_Identity.AccountCodes`, latest unconsumed); **never** a literal code. |
| **Last reviewed** | 2026-07-11 (D-737/D-738 badge sign-in + unified scanner; authored at clean-code freeze D-555) |

## Coverage matrix
| Function | Scenario id |
|---|---|
| Auto-send on open (account has email) → code step | E2E-MOBBADGE-001 |
| Manual email entry (`needsEmail`) → send → code step | E2E-MOBBADGE-002 |
| Complete with a valid code + matching password → sign-in | E2E-MOBBADGE-003 |
| Invalid / expired code → inline error, stays | E2E-MOBBADGE-004 |
| Password policy + confirm-mismatch validation | E2E-MOBBADGE-005 |
| Empty-email / malformed-email validation (manual step) | E2E-MOBBADGE-006 |
| RTL (Arabic) renders correctly | E2E-MOBBADGE-007 |
| **Sign-in (D-738):** has-password badge → password step → tokens | E2E-MOBBADGE-008 |
| Wrong password → generic error + password field cleared | E2E-MOBBADGE-009 |
| 2FA account → email-OTP screen (verify-otp) | E2E-MOBBADGE-010 |
| Unknown / non-approved / passwordless qrId → same generic message | E2E-MOBBADGE-011 |
| **Scanner (D-737):** camera-permission-denied → error card + manual entry still works | E2E-MOBBADGE-012 |
| Steady unknown QR → a single "not recognised" (no snackbar spam) | E2E-MOBBADGE-013 |

## Scenarios

```gherkin
Scenario: E2E-MOBBADGE-001 — a badge with an email auto-sends the code on open
  Given a scanned badge whose account already has an email
  When the badge-activation screen opens
  Then a verification code is sent to the masked email shown on screen
  And the code + new-password step is displayed

Scenario: E2E-MOBBADGE-002 — a badge with no email collects one first
  Given a scanned badge whose account has no email ("needsEmail")
  When I enter "holder@example.sa" and tap "send code"
  Then the code is sent to that address
  And the code + new-password step is displayed

Scenario: E2E-MOBBADGE-003 — activating with a valid code and password
  Given the code + new-password step with a valid emailed code
  When I enter the code, a policy-valid new password and a matching confirmation
  And I tap "activate"
  Then the account is activated
  And I am routed to the sign-in screen with a confirmation toast

Scenario: E2E-MOBBADGE-004 — a wrong code is rejected in place
  Given the code + new-password step
  When I enter an incorrect code and tap "activate"
  Then an inline bilingual error is shown
  And I stay on the activation screen

Scenario: E2E-MOBBADGE-005 — password policy and confirm match are enforced
  Given the code + new-password step
  When the new password is shorter than 8 chars or lacks a letter or a digit
  Then a policy error blocks "activate"
  And when the confirmation does not match the password, a mismatch error blocks it

Scenario: E2E-MOBBADGE-006 — the manual email is validated
  Given the email-entry step ("needsEmail")
  When the email is empty or malformed
  Then a required / invalid-email error blocks "send code"

Scenario: E2E-MOBBADGE-007 — RTL render
  Given the device language is Arabic
  Then the screen renders right-to-left with the brand font and no clipped text
```

## Badge sign-in flow (D-738 password step + D-737 unified scanner)

> The scan entry (`badge_sign_in_screen.dart`) calls `POST /app/auth/resolve-badge`
> and branches on the response: `hasPassword` → the D-738 password step
> (`/auth/badge-password?qrId&name&masked`); passwordless → the activation screen
> above. The password step submits `POST /app/auth/badge-sign-in { qrId, password }`
> (anonymous; `auth` + `auth-email` rate limits) and returns the standard sign-in
> result — tokens or the email-OTP 2FA challenge — identical to email sign-in. The
> QR never bypasses the password: an unknown / non-approved / passwordless qrId and
> a wrong password all return the SAME generic `401 AUTH_INVALID_CREDENTIALS`.

### E2E-MOBBADGE-008 — has-password badge → password step → signed in

```gherkin
Scenario: A returning holder finishes with only their password
  Given an approved account "خالد الأحمد" (khalid@example.sa) that already has a password
  When the holder scans (or types) their badge QR on the badge sign-in screen
  Then POST /app/auth/resolve-badge returns found=true, hasPassword=true,
       displayName="خالد الأحمد", maskedEmail="k****@example.sa"
  And the app navigates to /auth/badge-password (title "أدخل كلمة المرور" / "Enter your password")
  And the screen greets "مرحبًا خالد الأحمد" / "Welcome, خالد الأحمد" and shows the LTR masked line
       "تسجيل الدخول إلى الحساب k****@example.sa" / "Signing in to k****@example.sa"
  And only a password field is shown (no email is typed — the QR selected the account)
  When they enter the correct password and tap "دخول" / "Sign in"
  Then POST /app/auth/badge-sign-in { qrId, password } returns the issued tokens
  And the app offers the Face-ID enrol nudge, then routes to the post-auth home
```

**Evidence:** `badge_password_screen_test` — "renders the resolved name + masked
email + password field" (asserts "Welcome, Khalid" + the masked line); the
signed-in success path is driven by the controller unit test (shared post-auth
routing). API: `BadgeSignInTests.Badge_sign_in_with_the_correct_password_and_2fa_off_issues_tokens`.

### E2E-MOBBADGE-009 — wrong password → generic error + field cleared

```gherkin
Scenario: A wrong password is rejected without leaking which factor failed
  Given the badge password step for a resolved has-password account
  When they enter an incorrect password and submit
  Then POST /app/auth/badge-sign-in returns 401 AUTH_INVALID_CREDENTIALS
  And the inline bilingual error shows
       "The email address or password is not correct." /
       "البريد الإلكتروني أو كلمة المرور غير صحيحة."
  And the password field is cleared
  And the holder stays on the badge password step (no navigation)
```

**Evidence:** `badge_password_screen_test` — "a wrong password shows the inline
error and clears the field";
`BadgeSignInTests.Badge_sign_in_with_a_wrong_password_returns_the_generic_401_and_counts_the_failure`.

### E2E-MOBBADGE-010 — 2FA account → email-OTP screen

```gherkin
Scenario: A badge sign-in for a 2FA account continues to the OTP challenge
  Given the resolved account requires an email second factor (visitor OTP)
  When they enter the correct password and submit on the badge password step
  Then POST /app/auth/badge-sign-in returns the awaiting-OTP challenge (a code is emailed)
  And the app navigates to the shared verify-otp screen (RouteNames.verifyOtp)
  And completing the emailed code finishes sign-in exactly as email sign-in does
```

**Evidence:** `badge_password_screen_test` — "a 2FA account continues to the OTP
screen" (routes to verifyOtp on `AuthStateAwaitingOtp`);
`BadgeSignInTests.Badge_sign_in_for_a_2fa_visitor_returns_the_email_otp_challenge`
(+ `…_completes_with_the_emailed_code`). The shared OTP screen itself is covered by
`mobile-sign-in.md` (2FA scenarios).

### E2E-MOBBADGE-011 — unknown / non-approved / passwordless → same generic message

```gherkin
Scenario: An unknown badge is indistinguishable from a wrong password
  Given a qrId that resolves to nothing, or to a non-approved account,
        or to an approved account that has NO password yet
  When any password is submitted to POST /app/auth/badge-sign-in with that qrId
  Then the response is 401 AUTH_INVALID_CREDENTIALS with the SAME bilingual text
       "The email address or password is not correct." /
       "البريد الإلكتروني أو كلمة المرور غير صحيحة."
  And a failed-sign-in audit row is written (detail "badge") — the public badge is
       never a valid-QR oracle and never substitutes for the password
```

**Evidence:** `BadgeSignInTests` — `Badge_sign_in_with_an_unknown_qr_returns_the_same_generic_401`,
`…_for_a_passwordless_account_returns_the_same_generic_401`, and
`…_for_a_non_approved_account_returns_the_same_generic_401` all assert the same
`401 AUTH_INVALID_CREDENTIALS`; the unknown-QR path also writes the
`SignInBadCredentials` audit.

### E2E-MOBBADGE-012 — camera-permission-denied → error card + manual entry

```gherkin
Scenario: A denied camera is never a silent black dead-end (D-737)
  Given the badge sign-in scanner opens with the camera enabled
  When the OS denies the camera permission (or the device has no camera)
  Then the shared SimfScannerBody shows the camera-error card with
       "تعذّر تشغيل الكاميرا. فعّل إذن الكاميرا من إعدادات النظام، أو أدخل الرمز يدويًا بالأسفل." /
       "Camera unavailable. Enable camera permission in system settings, or type the code below."
  And a "إعادة المحاولة / Try again" retry control is offered
  And the always-visible manual-entry field below still drives the resolve → branch flow
```

**Evidence:** source-verified — `simf_scanner_body.dart` renders `_CameraErrorCard`
when `_onControllerCreated` reports an error or the 8-second `_armWatchdog` fires
(the error-card render is device-only, needs a real camera + denial).
`simf_scanner_body_test` covers the always-mounted manual field + busy-disable with
the camera off — the fallback the error card points to.

### E2E-MOBBADGE-013 — steady unknown QR → one "not recognised" (no spam)

```gherkin
Scenario: Holding a steady unrecognised QR under the camera fires once
  Given the badge scanner camera is live and reading ~1 frame/second
  When an unknown (or foreign) QR stays in the viewfinder for several seconds
  Then resolve-badge runs ONCE and one bilingual snackbar shows
       "تعذّر التعرّف على الشارة." / "The badge was not recognised."
  And the same code does not re-fire while it stays in view (ScanGate single-flight + dedupe)
  And deliberately removing and re-presenting the badge lets it be tried again (onNoCode reset)
```

**Evidence:** `scan_gate_test` — `shouldHandle` is single-flight, dedupes the same
code within the 2-second cooldown, and `onNoCode` clears the last code so a
re-presented badge fires again.
