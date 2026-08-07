# E2E test catalogue — CP auth flow (`/login` → `/login/totp` → `/login/recovery`)

| | |
|--|--|
| **Pages covered** | [`cp/login.md`](../../pages/cp/login.md), [`cp/login-totp.md`](../../pages/cp/login-totp.md), [`cp/login-recovery.md`](../../pages/cp/login-recovery.md), [`cp/forgot-password.md`](../../pages/cp/forgot-password.md), [`cp/auth-pending.md`](../../pages/cp/auth-pending.md), [`cp/auth-rejected.md`](../../pages/cp/auth-rejected.md) |
| **Surface** | Control Panel |
| **Related** | #2 / Q1 (2026-07-30) added a **fourth** outcome to the password step: an admin with no authenticator paired is routed to `/login/enrol-2fa` instead of receiving a token. That page has its own file — [`cp-2fa-enrolment.md`](cp-2fa-enrolment.md), `E2E-TFE-001..013`. |
| **Last reviewed** | 2026-07-30 |

## Coverage matrix

| ID | Scenario | Page(s) | Priority |
|----|----------|---------|----------|
| E2E-AUTH-001 | Happy: super-admin signs in (password + TOTP) | login + login/totp + / | P0 |
| E2E-AUTH-002 | Wrong password → bilingual error, no token | login | P0 |
| E2E-AUTH-003 | Lockout after 5 failed sign-ins | login | P1 |
| E2E-AUTH-004 | TOTP wrong code → toast, can retry | login/totp | P0 |
| E2E-AUTH-005 | TOTP page → "Use a recovery code" → /login/recovery, burn code | login/totp + login/recovery | P1 |
| E2E-AUTH-006 | Pending admin sign-in → /auth/pending | login + auth-pending | P0 |
| E2E-AUTH-007 | Rejected admin sign-in → /auth/rejected with reason | login + auth-rejected | P0 |
| E2E-AUTH-008 | Forgot password → email code → /reset-password → sign-in | forgot-password + reset-password + login | P1 |
| E2E-AUTH-009 | D-121 cookie refresh: session stays alive past the 5-min access token | (CP shell) | P1 |
| E2E-AUTH-010 | RTL toggle on login page works | login | P2 |
| E2E-AUTH-011 | D-443 idle warning: modal → "Stay signed in" silently refreshes; ignore → auto sign-out | (CP shell) | P1 |
| E2E-AUTH-012 | D-443 absolute 24h cap: a continuously active session is still forced to re-sign-in after 24h | (CP shell) | P1 |
| E2E-AUTH-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-AUTH-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-AUTH-001 — Happy sign-in

> **Pre-requisite as of #2 / Q1 (2026-07-30).** This scenario reaches
> `/login/totp` only for an account that **already has an authenticator secret
> paired**. An admin with none — which is what the production super-admin was
> recorded as — is now routed to `/login/enrol-2fa` and issued no token; see
> `E2E-TFE-001`. If this scenario lands on `/login/enrol-2fa`, the fixture
> account is unenrolled, not the flow broken.

```gherkin
Feature: Administrator sign-in
  Background:
    Given the API is reachable on http://localhost:5175
    And the Control Panel is reachable on http://localhost:5158
    And the super-admin account has an authenticator secret paired

  Scenario: Super-admin signs in (password + TOTP)
    Given an administrator opens /login
    When they fill Email="superadmin@zagali-ict.com"
    And they fill Password="[REDACTED - supply via SIMF_SuperAdmin__TempPassword]"
    And they click "Sign in"
    Then they land on /login/totp
    When they generate a TOTP via Get-Totp '[REDACTED - supply via SIMF_SuperAdmin__TotpSecret]'
    And they fill that 6-digit code
    And they click "Verify"
    Then they land on /
    And the SimfBanner "Dashboard" renders
    And the header shows "Super Administrator" and a notification bell
    And the cookie holds access_token, refresh_token, expires_at
```

### E2E-AUTH-002 — Wrong password

```gherkin
Scenario: Wrong password shows bilingual error, no cookie issued
  Given an administrator opens /login
  When they fill the right email + the wrong password
  And they click "Sign in"
  Then a SimfAlert error appears
  And it reads "Invalid email or password." / "البريد الإلكتروني أو كلمة المرور غير صحيحة."
  And no auth cookie is set
  And the URL is still /login
```

### E2E-AUTH-003 — Lockout

```gherkin
Scenario: 5 wrong passwords in 5 min trip the lockout
  Given the audit log shows no prior failed attempts for the account
  When the administrator submits 5 wrong passwords for the same email within 5 minutes
  Then the 6th submit returns HTTP 423 with ApiResult.Error.Code = "AccountLocked"
  And the bilingual lockout message includes the unlock-after timestamp
  And the user must wait or contact an admin to unlock
```

### E2E-AUTH-004 — TOTP wrong code

```gherkin
Scenario: Wrong TOTP code allows retry without restarting sign-in
  Given the user has just passed the password step and is on /login/totp
  When they fill an invalid 6-digit code
  And they click "Verify"
  Then a SimfAlert reads "Invalid verification code." / "رمز التحقق غير صحيح."
  And the URL is still /login/totp
  And the SecondFactorToken ticket is still valid for the 5-minute TTL
  When they fill the correct code within that TTL
  Then they sign in successfully
```

### E2E-AUTH-005 — Recovery code

```gherkin
Scenario: Use a recovery code, then re-pair after
  Given the user has just passed the password step and is on /login/totp
  When they click "Use a recovery code instead"
  Then they land on /login/recovery
  When they fill one of their 10 saved recovery codes
  And they click "Verify"
  Then they sign in successfully
  And the audit log records "Auth.RecoveryCodeUsed" for the account
  And the used code is marked consumed (can never be used again)

  When they next try to sign in
  Then they go through /login/totp normally (recovery doesn't disable TOTP)
  And they should re-pair via /account/profile → Reset my 2FA if they want fresh codes
```

### E2E-AUTH-006 — Pending admin

```gherkin
Scenario: A self-registered admin signs in before approval
  Given an admin account exists in AccountState=PendingApproval
  And the credentials are correct
  When they sign in (password + TOTP)
  Then the server returns AuthRequiresApproval envelope
  And the browser redirects to /auth/pending
  And the page reads "Your account is awaiting approval."
  And the only available action is Sign out
```

### E2E-AUTH-007 — Rejected admin

```gherkin
Scenario: A rejected admin signs in
  Given an admin account exists in AccountState=Rejected
  And its RejectionReason is "Wrong email domain — must be @rsnf.gov.sa"
  When they sign in (password + TOTP)
  Then the browser redirects to /auth/rejected
  And the rejection reason appears verbatim (bilingual)
  And the rejection timestamp appears
  And the only available action is Sign out
```

### E2E-AUTH-008 — Forgot password

```gherkin
Scenario: Forgot password → reset → sign in
  Given an admin has forgotten their password
  When they open /forgot-password
  And submit their email
  Then the page always shows the success message (anti-enumeration)
  And (if the email exists) a 6-digit reset code is emailed (15-min TTL)

  When they paste the code into /reset-password
  And type a new password "Bb@123456789012" (meets complexity)
  And click "Reset password"
  Then they see a success toast
  And land on /login
  When they sign in with the new password
  Then the sign-in completes (TOTP still required)
  And the audit log shows "Auth.PasswordReset" for the account
  And all prior sessions for the account are revoked
```

### E2E-AUTH-009 — D-121 cookie refresh

```gherkin
Scenario: Session stays alive past the 5-min access token via the refresh hook
  Given the administrator has been signed in for >3 minutes (access-token nears expiry)
  When they click any nav item that fires an /account/api/* request
  Then the SimfCookieRefreshHandler reads expires_at from the cookie
  And calls SimfAuthClient.RefreshAsync with the refresh token
  And the API rotates the pair (D-013)
  And the cookie is rewritten with the new pair + fresh expires_at
  And the request succeeds (HTTP 200)
  And no 401 is observed
```

### E2E-AUTH-010 — RTL on login

```gherkin
Scenario: Arabic toggle on the login page mirrors layout
  Given an administrator opens /login
  When they click "العربية"
  Then the page reloads with <html dir="rtl" lang="ar">
  And the heading reads "تسجيل الدخول"
  And the form labels are Arabic
  And the brand panel + form swap sides
```

### E2E-AUTH-011 — D-443 idle session-timeout warning (token-driven)

```gherkin
Scenario: Idle admin is warned, then "Stay signed in" silently refreshes
  Given the administrator is signed in on the CP shell
  And they stop interacting (no mouse, keyboard, scroll or touch activity)
  When the 5-minute access token is ~1 minute from expiry
  Then a session-timeout modal appears
  And the title reads "Session about to expire" / "جلستك على وشك الانتهاء"
  And it shows a live seconds countdown plus "Stay signed in" and "Sign out now"
  When they click "Stay signed in" before the countdown reaches zero
  Then GET /session/status refreshes the token silently (no full reload)
  And the modal closes and they remain on the same page

Scenario: Idle admin who ignores the warning is auto-signed-out
  Given the session-timeout modal is showing with the countdown running
  When the administrator does nothing until the countdown reaches zero
  Then the guard POSTs /auth/sign-out
  And the browser lands on /login

Scenario: Active admin is never shown the modal
  Given the administrator keeps moving the mouse / typing
  When the access token nears expiry
  Then the guard silently calls GET /session/status and rotates the token
  And no modal is ever shown
```

### E2E-AUTH-012 — D-443 absolute 24h session cap

```gherkin
Scenario: A continuously active session is still capped at 24 hours
  Given the administrator signed in at T0
  And they keep working so the token is silently refreshed throughout
  When 24 hours have elapsed since T0
  Then the next silent refresh (GET /session/status) fails with AUTH_REFRESH_TOKEN_EXPIRED
  And the guard signs them out to /login
  And they must sign in again (activity does not slide the absolute cap)
```

---

_Last reviewed:_ 2026-06-19 by Claude (D-443 — token caps + session-timeout guard).
