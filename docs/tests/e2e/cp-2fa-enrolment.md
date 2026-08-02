# E2E test catalogue — `Mandatory two-factor enrolment` (`/login/enrol-2fa`)

> **Authority:** #2 / owner decision **Q1 (2026-07-30) — enrolment-first**.
> The Control Panel must never mint a session on the password alone, and
> nobody may be locked out reaching that state — including the existing
> production super-admin, which is recorded as 2FA-off.

| | |
|--|--|
| **Page** | [`two-factor-enrolment.md`](../../pages/cp/two-factor-enrolment.md) |
| **Route** | `/login/enrol-2fa` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver _(API half: `tests/SIMF.Api.Tests/ControlPanelTwoFactorEnrolmentTests.cs`)_ |
| **Auth setup** | No signed-in session by design. Each scenario starts at `/login` with a CP admin whose account has **no authenticator secret**; the TOTP codes are produced with the `Get-Totp` helper against the secret the page itself hands out. **Never a literal secret in this file.** |
| **Permission** | none — pre-token authentication surface. The two API endpoints it calls (`POST /app/auth/totp/enrolment/start` and `/complete`) are `AllowAnonymous` and are on the reviewed allow-list in `BusinessFlow13PermissionMatrixTests`; the enrolment ticket is the credential. |
| **Last reviewed** | `2026-07-30` |

## What this page is, and what it is not

`/login/enrol-2fa` **creates** an authenticator secret for an account that has
none, against a single-use ticket, before any token exists.
`/account/totp-pairing` (D-096) only **re-renders** an already-active secret for
an admin who is already signed in — it cannot create one, which is precisely why
the existing page could not close #2.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-TFE-001 | Golden path — unenrolled admin enrols and lands in the Control Panel | happy | P0 | **verified 2026-08-01** |
| E2E-TFE-002 | Password alone never yields a session on the Cp audience | security | P0 | **verified 2026-08-01** |
| E2E-TFE-003 | Already-enrolled admin never sees this page | happy | P0 | **verified 2026-08-01** |
| E2E-TFE-004 | Wrong six-digit code is rejected and no session is issued | error | P0 | **verified 2026-08-01** |
| E2E-TFE-005 | Direct navigation with no enrolment ticket bounces to `/login` | auth | P0 | **verified 2026-08-01** |
| E2E-TFE-006 | A spent ticket cannot be replayed | security | P0 | **verified 2026-08-01** |
| E2E-TFE-007 | Recovery codes are shown exactly once and must be acknowledged | happy | P1 | **verified 2026-08-01** |
| E2E-TFE-008 | Client-side validation on the code field | error | P1 | **verified 2026-08-01** |
| E2E-TFE-009 | Expired ticket (>15 min) sends the admin back to sign in | error | P1 | **verified 2026-08-01** |
| E2E-TFE-010 | Server failure on `/complete` surfaces a bilingual error, no session | resilience | P2 | **verified 2026-08-01** |
| E2E-TFE-011 | RTL render in Arabic | i18n | P1 | **verified 2026-08-01** |
| E2E-TFE-012 | A newly CP-provisioned admin completes their first sign-in here | happy | P0 | **verified 2026-08-01** |
| E2E-TFE-013 | The App audience is unaffected by the gate | security | P0 | **verified 2026-08-01** |

### Verification run — 2026-08-01

All thirteen were driven against a throwaway localhost stack (`tools/qa/launch-qa-stack.sh`,
API `:5275` + CP `:5278`, LocalDB `SIMF_QA_*` recreated from empty). Zero console
errors and zero console warnings across the whole session; no horizontal overflow.

The account under test was created through the CP at `/admin/admins` during the run
and confirmed in the database as `TwoFactorEnabled = 1` with **zero** rows in
`AspNetUserTokens` for `AuthenticatorKey` — the exact state that, before #2/#2d,
`SignInService` would have challenged against a secret that does not exist. It
enrolled and reached the dashboard, so the lockout is closed by demonstration and
not only by argument.

Screenshots: [`cp-2fa-enrolment-rtl.png`](../../screenshots/cp-2fa-enrolment-rtl.png)
(stage 1, Arabic) · [`cp-2fa-enrolment-recovery-codes.png`](../../screenshots/cp-2fa-enrolment-recovery-codes.png)
(stage 2).

## Scenarios

### E2E-TFE-001 — Golden path

```gherkin
Feature: Mandatory two-factor enrolment
  As a Control Panel administrator with no authenticator paired
  I want to set one up during sign-in
  So that my admin session is never minted on my password alone

Background:
  Given a Control Panel account "qa.enrol@simf.test" exists with UserType = Admin
  And the account holds the Administrator role
  And the account has NO authenticator secret paired
  And "IdentityLifecycle:RequireControlPanelTwoFactorEnrolment" is true

Scenario: An unenrolled admin enrols and reaches the shell
  Given the administrator is on /login
  When they enter "qa.enrol@simf.test" and the correct password and submit
  Then the browser is at /login/enrol-2fa
  And the page shows a QR code and a base32 key
  And the page shows the six-digit code field labelled "Verification code"
  When they compute the current code from the displayed key with Get-Totp
  And they enter it and press "Confirm and sign in"
  Then the page shows 10 recovery codes and the "shown only once" notice
  When they press "I have saved them — continue"
  Then the browser lands on the Control Panel dashboard at /
  And the account now has TwoFactorEnabled = true with an active secret
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-2fa-enrolment-golden-before.png` (QR + key + code field)
- Screenshot after: `docs/screenshots/cp-2fa-enrolment-golden-after.png` (recovery codes)
- Console errors: 0 expected
- Network failures: 0 expected — in particular `/api/v1/app/auth/totp/enrolment/start` and `/complete` are both 200
- Audit rows: `SignIn.TwoFactorEnrolmentRequired`, then `Totp.EnrolmentConfirmed`,
  `Totp.RecoveryCodesGenerated`, `RefreshToken.Issued`, `SignIn.Succeeded` — all for the same subject

### E2E-TFE-002 — Password alone never yields a session

The defect itself. Assert at the API, because the browser would hide it.

```gherkin
Scenario: The Cp password step returns a challenge, not tokens
  Given the same unenrolled admin
  When POST /api/v1/app/auth/sign-in is called with audience = Cp and the correct password
  Then the response is 200 with success = true
  And data.twoFactorEnrolmentToken is a non-empty string
  And data.tokens is null
  And data.mfaToken is null
  And data.otpToken is null
  And data.mfaRequired is false
```

### E2E-TFE-003 — Already-enrolled admin never sees this page

```gherkin
Scenario: An enrolled admin gets the ordinary TOTP challenge
  Given a Control Panel admin with an authenticator secret already paired
  When they sign in at /login with the correct password
  Then the browser is at /login/totp, not /login/enrol-2fa
  And the sign-in response carries mfaToken and no twoFactorEnrolmentToken
```

### E2E-TFE-004 — Wrong code

```gherkin
Scenario: A wrong first code issues nothing
  Given the administrator is on /login/enrol-2fa with the QR displayed
  When they enter "000000" and press "Confirm and sign in"
  Then an error alert shows "The verification code is not correct."
  And in Arabic it shows "رمز التحقق غير صحيح."
  And the API returned 400 with code TOTP_ENROLMENT_CODE_INVALID
  And no authentication cookie was written
  And the account still has TwoFactorEnabled = false
```

### E2E-TFE-005 — Direct navigation without a ticket

```gherkin
Scenario: The page is not reachable on its own
  Given no sign-in has been started in this browser session
  When the administrator navigates directly to /login/enrol-2fa
  Then they are redirected to /login
  And no QR code is rendered at any point
```

### E2E-TFE-006 — Replay of a spent ticket

```gherkin
Scenario: One ticket, one session
  Given an administrator has completed enrolment with an enrolment ticket
  When POST /api/v1/app/auth/totp/enrolment/start is called again with the same ticket
  Then the response is 400 with code AUTH_TWO_FACTOR_ENROLMENT_REQUIRED
  And the message reads "Two-factor enrolment must be started from a fresh sign-in."
  And in Arabic "يجب بدء تسجيل المصادقة الثنائية من تسجيل دخول جديد."
```

### E2E-TFE-007 — Recovery codes shown once

```gherkin
Scenario: The recovery codes cannot be skipped past
  Given enrolment has just been confirmed
  Then exactly 10 recovery codes are listed, each matching ^[A-Z2-9]{5}-[A-Z2-9]{5}$
  And the QR and the code field are no longer on the page
  And the only action is "I have saved them — continue"
  When the administrator continues and then reloads /login/enrol-2fa
  Then the codes are NOT shown again
  And they are redirected to /login
```

### E2E-TFE-008 — Client-side validation

```gherkin
Scenario Outline: The code field rejects a malformed entry before any request
  Given the administrator is on /login/enrol-2fa with the QR displayed
  When they enter "<entry>" and press "Confirm and sign in"
  Then the field shows "Enter the six-digit code."
  And in Arabic it shows "أدخل الرمز المكوّن من ستة أرقام."
  And no request is made to /api/v1/app/auth/totp/enrolment/complete

  Examples:
    | entry  |
    |        |
    | 123    |
    | abcdef |
```

> **Corrected 2026-08-01 — the over-long case was removed, because it cannot
> happen.** This outline used to carry a `1234567` row. The field is
> `maxlength="6"`, so the browser truncates the entry to `123456` before Blazor
> ever sees it; that is a well-formed six-digit code, so it passes client
> validation and reaches the server, which then answers
> `TOTP_ENROLMENT_CODE_INVALID` — i.e. it exercises TFE-004, not TFE-008.
> Keeping the row would have made a passing scenario assert the wrong thing.
> `inputmode="numeric"` is a soft keyboard hint, not a filter, so `abcdef` does
> still reach the client-side check and is the case worth keeping.

**How "no request is made" is asserted.** The Control Panel is Blazor Server, so
the call to `/complete` is made server-to-server by `SimfAuthClient` and never
appears in the browser's network panel — the only browser traffic is the SignalR
circuit. The assertion is made against the API's own request log instead: after
the three malformed entries the count of `POST …/enrolment/complete` lines was
still zero, and the first and only one appeared when a well-formed code was
submitted.

### E2E-TFE-009 — Expired ticket

```gherkin
Scenario: The enrolment ticket expires after 15 minutes
  Given an administrator received an enrolment ticket more than 15 minutes ago
  When they submit a valid code
  Then the response is 400 with code AUTH_TWO_FACTOR_ENROLMENT_REQUIRED
  And the page shows the bilingual "sign in again to start over" message
  And a link back to /login is present
```

### E2E-TFE-010 — Server 500

```gherkin
Scenario: An upstream failure never leaks a half-session
  Given the API is made to fail on POST /app/auth/totp/enrolment/complete
  When the administrator submits a valid code
  Then the page shows a bilingual error alert and stays on /login/enrol-2fa
  And no authentication cookie is written
  And the account gains no authenticator secret
  And the browser console has 0 uncaught errors
```

> **How it was driven 2026-08-01.** Injecting a literal HTTP 500 would mean
> changing the API, so the API process was **stopped** while an enrolment page
> sat open with a live ticket, and a valid code was then submitted. That takes
> the same branch: `SimfAuthClient` funnels `HttpRequestException`,
> `TaskCanceledException`, `JsonException` and `NotSupportedException` into the
> same `TransportFailure` envelope a 500 produces, so the page sees one failed
> `ApiResult` either way. Observed: the alert **"The SIMF service could not be
> reached. Please try again."** (Arabic sibling
> **"تعذّر الوصول إلى خدمة SIMF. حاول مرة أخرى."** — both live in
> `SimfAuthClient.TransportFailure`), the URL still `/login/enrol-2fa`, the QR
> still rendered, zero recovery codes on the page, and **zero** console errors.
> No session was minted: navigating to `/` afterwards redirected to
> `/login?ReturnUrl=%2F`, and the account still held **zero** `AuthenticatorKey`
> rows — the failure left no half-enrolment behind.

### E2E-TFE-011 — RTL render

```gherkin
Scenario: Arabic renders right-to-left with no overflow
  Given the language switch is set to العربية
  When the administrator reaches /login/enrol-2fa
  Then <html dir> is "rtl"
  And the title reads "إعداد المصادقة الثنائية"
  And the submit button reads "تأكيد وتسجيل الدخول"
  And document.documentElement.scrollWidth equals clientWidth (no horizontal overflow)
  And the QR image and the base32 key are not mirrored or clipped
```

### E2E-TFE-012 — A newly provisioned admin's first sign-in

The #2d half. Without this page, this admin could never sign in at all.

```gherkin
Scenario: A CP-created admin gets in through enrolment
  Given an Administrator creates a new admin on /admin/admins
  Then the new account is created with TwoFactorEnabled = true and no authenticator secret
  When the new admin sets their password from the invite code
  And the creating Administrator approves them
  And the new admin signs in at /login
  Then they are routed to /login/enrol-2fa
  And after enrolling they reach the Control Panel dashboard
```

### E2E-TFE-013 — The App audience is unaffected

```gherkin
Scenario: The mobile contract is untouched
  Given an approved visitor with two-factor turned off
  When POST /api/v1/app/auth/sign-in is called with audience = App
  Then the response carries data.tokens with a non-empty accessToken
  And data.twoFactorEnrolmentToken is null
```

## Automated coverage today

`tests/SIMF.Api.Tests/ControlPanelTwoFactorEnrolmentTests.cs` covers the API
halves of TFE-001, -002, -003, -004, -006, -012 and -013 against a host with the
gate switched on (`ControlPanelTwoFactorApiFactory`).

TFE-005, -007, -008, -009, -010 and -011 remain browser assertions with no
automated coverage. They were **driven by hand on 2026-08-01** (see the
verification-run note above) and pass, but nothing in CI re-runs them, so a
regression in the enrolment page would not fail the build. Automating them is
the open follow-up on this page: the six are ordinary Playwright candidates and
the Gherkin above copies across unchanged.

TFE-009 costs 15 minutes of wall clock by construction — the ticket lifetime is
`SignInService.EnrolmentTicketLifetime` — so an automated version should inject a
`TimeProvider` rather than wait.

---

_Last reviewed:_ `2026-07-30` by Track A (auth & security), fix-all round.
