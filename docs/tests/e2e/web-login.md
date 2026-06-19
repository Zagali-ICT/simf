# E2E test catalogue — Sign in (Website) (`/login`)

| | |
|--|--|
| **Page** | [`web/login.md`](../../pages/web/login.md) |
| **Route** | `/login` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | This page is the visitor sign-in itself — **no prior auth**. The golden path uses a **Visitor** account (`visitor@example.com` / `Aa@123456789`). The visitor email-OTP is read from `SIMF_Identity.AccountCodes` (`Purpose = SignInOtp`, latest unconsumed, plaintext). `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper is used only by the wrong-surface scenario (E2E-WLG-004). |
| **Last reviewed** | 2026-06-02 |

> **Surface note.** `/login` is **anonymous** (`SignInEndpoint` is `AllowAnonymous()`),
> so there is no `PermissionCatalog` gate here — unlike a CP admin page. The
> "auth gate" analogue on this page is the **P2 audience gate**: an Administrator
> account is rejected on the Web surface with `AUTH_WRONG_SURFACE_WEB`
> (`SignInService.EnforceAudienceAsync`). Post-credential **account-state**
> routing (pending / rejected / unverified) is the other access-control surface
> and is covered below.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WLG-001 | Golden path — visitor signs in → emailed OTP → `/login/verify` → `/account/profile` | happy | P0 | _to author_ |
| E2E-WLG-002 | 2FA-off visitor — tokens returned directly → skip verify → `/account/profile` | happy | P1 | _to author_ |
| E2E-WLG-003 | "Forgot password?" link navigates to `/forgot-password` | nav | P2 | _to author_ |
| E2E-WLG-004 | Audience gate — Administrator credentials → `AUTH_WRONG_SURFACE_WEB` banner ("Use the Control Panel") | auth | P0 | _to author_ |
| E2E-WLG-005 | Account-state route — pending visitor → `/account/pending` | auth | P1 | _to author_ |
| E2E-WLG-006 | Account-state route — rejected visitor → `/account/rejected` with bilingual reason | auth | P1 | _to author_ |
| E2E-WLG-007 | Client validation — empty email + empty password → inline field errors, no POST | error | P1 | _to author_ |
| E2E-WLG-008 | D-443 session guard (authenticated area) — idle visitor sees the "Stay signed in / Sign out" countdown modal; "Stay" silently refreshes via `GET /session/status`, ignore → auto sign-out to `/login`; an active visitor is never interrupted; the session is still capped at an absolute 24h. Mirrors CP `E2E-AUTH-011/012`. | auth | P1 | _to author_ |
| E2E-WLG-008 | Client validation — email without `@` → "Enter a valid email address." | error | P2 | _to author_ |
| E2E-WLG-009 | Bad credentials — wrong password → 401 `AUTH_INVALID_CREDENTIALS` bilingual banner | error | P0 | _to author_ |
| E2E-WLG-010 | Unverified account — `Registered` state → `AUTH_EMAIL_NOT_VERIFIED` banner | error | P1 | _to author_ |
| E2E-WLG-011 | Disabled account → `AUTH_ACCOUNT_DISABLED` banner | error | P1 | _to author_ |
| E2E-WLG-012 | Account lockout after 5 wrong passwords → 423 `AUTH_ACCOUNT_LOCKED` banner | error | P1 | _to author_ |
| E2E-WLG-013 | OTP request throttle — 6th sign-in within the hour → 429 `RATE_LIMIT_EXCEEDED` | resilience | P2 | _to author_ |
| E2E-WLG-014 | Server / transport down — API unreachable → bilingual transport-failure banner | resilience | P2 | _to author_ |
| E2E-WLG-015 | Double-submit guard — second click while loading is a no-op | resilience | P2 | _to author_ |
| E2E-WLG-016 | Theme toggle persists across reload | i18n | P2 | _to author_ |
| E2E-WLG-017 | RTL / Arabic render — language switch mirrors the card + Arabic labels | i18n | P1 | _to author_ |

## Scenarios

### E2E-WLG-001 — Golden path (visitor → emailed OTP → profile)

```gherkin
Feature: Website sign-in golden path
  As a visitor with an approved SIMF account and 2FA enabled
  I want to sign in with my email + password + emailed code
  So that I land on my account profile with a real session cookie

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And the browser is on http://localhost:5115/login
  And an approved Visitor account exists: visitor@example.com / Aa@123456789 with TwoFactorEnabled = true

Scenario: Visitor completes the two-step sign-in and reaches the profile
  Given the sign-in card titled "Sign in" with supporting text "Sign in to your SIMF account." is shown
  When the visitor fills "Email address" = "visitor@example.com"
  And fills "Password" = "Aa@123456789"
  And clicks the "Sign in" button
  Then POST http://localhost:5175/api/v1/auth/sign-in fires with body { email, password, audience: "Web" }
  And the API returns HTTP 200 ApiResult.Success = true with Data.OtpToken set and Data.Tokens null
  And the browser navigates to /login/verify
  And an info SimfAlert reads "A code was sent to vi****@example.com" (email masked)

  When the visitor reads the 6-digit code from SIMF_Identity.AccountCodes (Purpose=SignInOtp, latest unconsumed)
  And fills the "Verification code" field with that code
  And clicks "Verify"
  Then POST http://localhost:5175/api/v1/auth/verify-otp fires with { otpToken, code }
  And the API returns HTTP 200 with Data.Tokens (AccessToken, RefreshToken, User)
  And the browser force-loads /auth/complete?reference={ticket}
  And /auth/complete writes the authentication cookie and 302-redirects to /account/profile
  And the /account/profile page renders for "visitor@example.com"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-login-golden-before.png` (sign-in card)
- Screenshot mid: `docs/screenshots/web-login-golden-verify.png` (OTP step, masked-email alert)
- Screenshot after: `docs/screenshots/web-login-golden-after.png` (`/account/profile`)
- Console errors: 0 expected
- Network: `/api/v1/auth/sign-in` → 200, `/api/v1/auth/verify-otp` → 200, `/auth/complete` → 302
- Audit rows: `SignIn.SecondFactorIssued` (detail=`EmailOtp`), then `RefreshTokenIssued` + `SignIn.Succeeded`, all with the visitor's user id

### E2E-WLG-002 — 2FA-off visitor (tokens returned directly)

```gherkin
Scenario: A visitor with TwoFactorEnabled = false skips the verify step
  Given an approved Visitor account with TwoFactorEnabled = false exists
  And the browser is on /login
  When the visitor fills "Email address" + "Password" correctly
  And clicks "Sign in"
  Then the API returns HTTP 200 with Data.OtpToken = null and Data.Tokens set (myComment #34 / D-033)
  And the page does NOT navigate to /login/verify
  And it force-loads /auth/complete?reference={ticket}
  And the visitor lands on /account/profile signed in
  And no /api/v1/auth/verify-otp call fires
```

### E2E-WLG-003 — Forgot-password link

```gherkin
Scenario: The "Forgot password?" link routes to the reset flow
  Given the browser is on /login
  When the visitor clicks the "Forgot password?" link
  Then the browser navigates to /forgot-password
  And the forgot-password card renders
```

### E2E-WLG-004 — Audience gate (Administrator on the Web surface)

```gherkin
Scenario: An Administrator account is rejected on the visitor surface
  Given the Administrator account superadmin@zagali-ict.com / Aa@123456789 exists (UserType = Admin)
  And the browser is on /login
  When the operator fills "Email address" = "superadmin@zagali-ict.com"
  And fills "Password" = "Aa@123456789"
  And clicks "Sign in"
  Then POST /api/v1/auth/sign-in fires with audience: "Web"
  And the API returns HTTP 403 with ApiResult.Error.Code = "AUTH_WRONG_SURFACE_WEB"
  And an error SimfAlert appears at the top of the form
  And it reads "Sign in to the Control Panel instead — this account is not allowed on the visitor surfaces."
  And (Arabic) "سجّل الدخول إلى لوحة التحكم — هذا الحساب غير مسموح به في واجهات الزوار."
  And the page stays on /login (no navigation to /login/verify)
  And a "SignIn.WrongSurface" audit row is written with detail = "Web"
```

### E2E-WLG-005 — Account-state route: pending visitor

```gherkin
Scenario: A pending-approval visitor lands on the pending state-banner
  Given a Visitor account in AccountState = PendingApproval exists, TwoFactorEnabled = false
  And the browser is on /login
  When the visitor signs in with correct credentials
  Then the API returns HTTP 200 with Data.AccountState.State = "PendingApproval"
  And /auth/complete copies account_state into the cookie and 302-redirects to /account/pending
  And the /account/pending page shows the bilingual "pending approval" message
  And the account email is shown
  And a "Sign out" button posts to /auth/sign-out
```

### E2E-WLG-006 — Account-state route: rejected visitor

```gherkin
Scenario: A rejected visitor lands on the rejected state-banner with the reason
  Given a Visitor account in AccountState = Rejected with a stored rejection reason exists, TwoFactorEnabled = false
  And the browser is on /login
  When the visitor signs in with correct credentials
  Then the API returns HTTP 200 with Data.AccountState.State = "Rejected" and RejectionReason populated
  And /auth/complete copies account_state + rejection_reason(_ar) into the cookie and 302-redirects to /account/rejected
  And the /account/rejected page shows the rejection reason in an error SimfAlert in the active culture
  And a "Sign out" button posts to /auth/sign-out
```

### E2E-WLG-007 — Client validation: both fields empty

```gherkin
Scenario: Submitting an empty form shows inline field errors and fires no request
  Given the browser is on /login with both fields blank
  When the visitor clicks "Sign in"
  Then the "Email address" field shows "Enter your email address." / "أدخل بريدك الإلكتروني."
  And the "Password" field shows "Enter your password." / "أدخل كلمة المرور."
  And NO POST /api/v1/auth/sign-in request fires (client-side guard returns first)
  And the page stays on /login
```

### E2E-WLG-008 — Client validation: email missing `@`

```gherkin
Scenario: An email without @ is rejected client-side
  Given the browser is on /login
  When the visitor fills "Email address" = "visitor.example.com"
  And fills "Password" = "Aa@123456789"
  And clicks "Sign in"
  Then the "Email address" field shows "Enter a valid email address." / "أدخل بريدًا إلكترونيًا صالحًا."
  And NO POST /api/v1/auth/sign-in request fires
  And correcting the email field clears its own error immediately (OnFieldChanged)
```

### E2E-WLG-009 — Bad credentials

```gherkin
Scenario: A wrong password surfaces the generic invalid-credentials banner
  Given an approved Visitor account visitor@example.com exists
  And the browser is on /login
  When the visitor fills "Email address" = "visitor@example.com"
  And fills "Password" = "WrongPassword!1"
  And clicks "Sign in"
  Then POST /api/v1/auth/sign-in returns HTTP 401 with Error.Code = "AUTH_INVALID_CREDENTIALS"
  And an error SimfAlert reads "The email address or password is not correct." / "البريد الإلكتروني أو كلمة المرور غير صحيحة."
  And the message is identical whether the email exists or not (no enumeration oracle)
  And the page stays on /login
  And a "SignIn.BadCredentials" audit row is written
```

### E2E-WLG-010 — Unverified account

```gherkin
Scenario: An account that has not verified its email cannot sign in
  Given a Visitor account in AccountState = Registered (email not verified) exists
  And the browser is on /login
  When the visitor signs in with correct credentials
  Then POST /api/v1/auth/sign-in returns HTTP 403 with Error.Code = "AUTH_EMAIL_NOT_VERIFIED"
  And an error SimfAlert reads "Verify your email address before signing in." / "يرجى التحقق من بريدك الإلكتروني قبل تسجيل الدخول."
  And the page stays on /login
```

### E2E-WLG-011 — Disabled account

```gherkin
Scenario: A disabled account is hard-blocked
  Given a Visitor account in AccountState = Disabled exists
  And the browser is on /login
  When the visitor signs in with correct credentials
  Then POST /api/v1/auth/sign-in returns HTTP 403 with Error.Code = "AUTH_ACCOUNT_DISABLED"
  And an error SimfAlert reads "This account is not active." / "هذا الحساب غير نشط."
  And the page stays on /login
```

### E2E-WLG-012 — Account lockout

```gherkin
Scenario: Five wrong passwords lock the account
  Given an approved Visitor account visitor@example.com exists
  And the browser is on /login
  When the visitor submits the wrong password 5 times in a row
  Then the 6th attempt (even with the correct password) returns HTTP 423 with Error.Code = "AUTH_ACCOUNT_LOCKED"
  And an error SimfAlert reads "The account is locked after too many attempts. Try again later." / "تم قفل الحساب بعد محاولات كثيرة. حاول مرة أخرى لاحقًا."
  And a "SignIn.AccountLockedOut" audit row is written
```

### E2E-WLG-013 — OTP request throttle

```gherkin
Scenario: Too many sign-in codes within the hour are throttled
  Given an approved Visitor account with TwoFactorEnabled = true (email-OTP second factor)
  And 5 sign-in codes have already been issued within the last hour
  And the browser is on /login
  When the visitor signs in correctly a 6th time
  Then POST /api/v1/auth/sign-in returns HTTP 429 with Error.Code = "RATE_LIMIT_EXCEEDED"
  And an error SimfAlert reads "Too many sign-in codes have been requested. Try again later." / "تم طلب رموز تسجيل دخول كثيرة. حاول مرة أخرى لاحقًا."
  And no second-factor ticket is issued (the throttle fires before the ticket)
```

### E2E-WLG-014 — Server / transport down

```gherkin
Scenario: An unreachable API surfaces the bilingual transport-failure banner
  Given the API on http://localhost:5175 is stopped (or returns a non-JSON proxy error page)
  And the browser is on /login
  When the visitor fills correct credentials and clicks "Sign in"
  Then SimfAuthClient maps the transport failure to a failed ApiResult (Code = "INTERNAL_ERROR")
  And an error SimfAlert reads "The SIMF service could not be reached. Please try again." / "تعذّر الوصول إلى خدمة SIMF. حاول مرة أخرى."
  And the page stays on /login
  And the "Sign in" button returns to its idle (non-loading) state
```

### E2E-WLG-015 — Double-submit guard

```gherkin
Scenario: A second click while the request is in flight is ignored
  Given the browser is on /login with valid credentials filled
  When the visitor clicks "Sign in" twice in rapid succession
  Then only ONE POST /api/v1/auth/sign-in request fires (the _loading guard returns early)
  And the button shows its loading label "Signing in" / "جارٍ تسجيل الدخول" while in flight
  And both fields are disabled while loading
```

### E2E-WLG-016 — Theme toggle persists

```gherkin
Scenario: Toggling the theme survives a reload
  Given the browser is on /login in light theme
  When the visitor clicks the SimfThemeToggle
  Then the document switches to dark theme (data-theme="dark") with no flash
  When the page is reloaded
  Then the dark theme is still applied on first paint
```

### E2E-WLG-017 — RTL / Arabic render

```gherkin
Scenario: The Arabic language switch mirrors the sign-in card
  Given the browser is on /login in English
  When the visitor clicks the "Arabic" language switch
  Then the page reloads via /culture?culture=ar&redirectUri=%2Flogin
  And the document is <html dir="rtl" lang="ar">
  And the card title reads "تسجيل الدخول" and supporting text "سجّل الدخول إلى حسابك في الملتقى."
  And the field labels read "البريد الإلكتروني" and "كلمة المرور"
  And the submit button reads "تسجيل الدخول"
  And the "Forgot password?" link reads "نسيت كلمة المرور؟"
  And the brand panel subtitle reads "الملتقى البحري السعودي الدولي 2026"
  And the language switch now offers "English"
```

---

## Implementation notes

- **No CP permission gate.** `/login` is `AllowAnonymous` — there is no
  `RequirePermission`/`PermissionCatalog` entry, so the canonical CP
  "non-admin → /not-permitted" auth-gate scenario does not apply here. The
  access-control coverage is the **P2 audience gate** (E2E-WLG-004) and the
  **account-state routing** (E2E-WLG-005/006/010/011).
- **API integration tests at a lower layer.** `tests/SIMF.Api.Tests/SignInTests.cs`
  already covers the same surface without a browser, including:
  unknown email / wrong password → 401 (`SignIn_with_an_unknown_email_returns_401`,
  `SignIn_with_a_wrong_password_returns_401`), unverified → 403
  (`SignIn_before_email_verification_returns_403`), disabled → 403
  (`SignIn_for_a_disabled_account_returns_403`), lockout after 5
  (`SignIn_locks_the_account_after_five_wrong_passwords`), the visitor
  emailed-code round-trip (`A_visitor_signs_in_and_completes_with_the_emailed_code`),
  the 2FA-off direct-token path (`SignIn_returns_tokens_directly_when_TwoFactorEnabled_is_false_for_a_visitor`),
  the Web audience gate (`Web_audience_rejects_a_user_with_a_CP_role_with_AUTH_WRONG_SURFACE_WEB`),
  the pending/rejected `AccountStateInfo`
  (`SignIn_for_a_pending_visitor_on_Web_is_allowed`,
  `SignIn_for_a_rejected_user_succeeds_with_AccountStateInfo_carrying_the_reason`),
  and the audit rows (`A_completed_sign_in_writes_a_SignInSucceeded_audit_entry`,
  `Bad_credentials_write_a_SignInBadCredentials_audit_entry`). The browser E2E
  scenarios above add the UI-layer assertions (inline validation, bilingual
  banners, navigation, cookie hand-off, theme, RTL) that those API tests cannot reach.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a
  `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) + a
  step-definition class. The Gherkin shape is already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
