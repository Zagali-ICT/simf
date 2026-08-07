# Feature Design Specification — Authentication and Login

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-001 |
| Title | Feature Design Specification — Authentication and Login |
| Version | 2.1 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Last updated | 2026-07-27 |
| Related documents | SIMF-SRS-001, SIMF-UCS-001, SIMF-API-001, SIMF-DAT-001, SIMF-RPM-001, SIMF-SAD-001, SIMF-SES-001, SIMF-CPD-001, docs/decisions/DECISIONS_LOG.md |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The authentication feature, build-ready. |
| 1.1 | 2026-05-21 | Engineering & Architecture Team | Architecture-review amendment (see Amendment A): account lockout; the visitor email-OTP second factor; second-factor token rules; the superadmin TOTP bootstrap; the forced-password-change state; sessions, admin force-sign-out and the token-revocation security stamp. |
| 2.1 | 2026-07-27 | Engineering & Architecture Team | **Amendment C — OI-3 closed on the negative (D-774).** The owner decided the public Website ships **no sign-in and no account area**, so OI-3 is resolved and every description of a Website sign-in in this document is corrected: §7 (user interface), B.2 (the Website cookie scheme, retired), B.8 (the second-factor bypass) and B.12 (the audience gate). Reverses D-018; supersedes the `/account` landing of D-024. The `SignInAudience.Web` value and the `/api/v1/app/auth/*` endpoints are unchanged — only the Website's own sign-in surface is gone. |
| 2.0 | 2026-05-24 | Engineering & Architecture Team | **Amendment B — Implementation update.** Captures everything built between 2026-05-22 and 2026-05-24: bilingual error messages (D-030); the CP cookie-auth + ticket hand-off pattern (D-026, D-029, D-037); TOTP enrolment two-slot pattern (D-036) and the time-source / secret-format fix (D-034); TOTP recovery codes — 10 single-use Crockford codes (D-040); the `TwoFactorEnabled=false` bypass for visitors and non-enrolled admins (D-033); admin-driven 2FA reset with a mandatory reason (D-041); the `change-password` endpoint; the sign-in **audience gate** (cp / web / app — P2); the **PendingApproval-blocks-CP-allows-Web** rule introduced with the approval workflow (P4); the password-reset code reuse policy; and the full implemented endpoint surface (Amendment B section B.13). Records the open P7 rework that will introduce the `UserType` model and `ProfileType` lookup. |

---

## 1. Purpose

This is the build-ready specification for the SIMF authentication feature. It
takes the requirements, use cases, API contract and data model that already
exist and turns them into one document a developer builds and a tester verifies
from. It is the first feature design specification, because authentication —
the Login API — is the build kickoff item in SIMF-PGP-001.

## 2. Scope

The feature covers everything a user does to get into SIMF and stay in:

- creating an account,
- verifying the email,
- signing in (email and password),
- the second factor (TOTP) for internal users,
- keeping a session alive (token refresh) and ending it (sign-out),
- resetting a forgotten password.

It does **not** cover completing the registration profile, the security
approval of a registration, or badge issue — those are separate features
(registration is SIMF-FDS-002, planned). This feature gets an account to the
**EmailVerified** state and, for an already-approved user, signs them in.

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-101 account creation | UC-01 Create an account |
| FR-102 email verification code | UC-02 Verify the email |
| FR-103 resend the code | UC-02 (alternate flow) |
| FR-104 email/password sign-in; no Nafath, no Face ID | UC-04 Sign in |
| FR-105 TOTP for internal users | UC-04 (admin flow) |
| FR-106 access and refresh tokens | UC-04 |
| FR-107 password reset | UC-06 Reset a password |

The non-functional requirements that bear on this feature: NFR-01/02/03
(security, authorisation, audit), NFR-04 (performance under the load profile)
and NFR-06 (localisation).

## 4. Feature overview

Authentication moves an account through the early states defined in
SIMF-RPM-001 section 6:

```
Sign up            Verify email           Sign in
   │                    │                    │
   ▼                    ▼                    ▼
Registered ──────▶ EmailVerified ─────▶ (session issued)
```

Sign-up creates the account in **Registered**. Email verification moves it to
**EmailVerified**. Sign-in issues a session to an account that is EmailVerified
or beyond, with the access it is entitled to for its account state.

The API contract for every endpoint in this feature is in SIMF-API-001 section
12. This document specifies the behaviour behind that contract — the rules, the
processing and the edge cases — and does not restate the request and response
bodies.

## 5. Detailed behaviour

Each flow below lists its trigger, its rules, its processing steps, its result,
and its failure handling.

### 5.1 Sign-up

- **Trigger:** the user submits an email, a password and a password
  confirmation (`POST /auth/sign-up`).
- **Rules:**
  - The email is a valid email address.
  - The email is not already attached to an account.
  - The password meets the policy: 8 to 128 characters with at least one
    upper-case letter, one lower-case letter, one digit and one special
    character; no three identical characters in a row; no three-character
    sequential run; not a common or leet-spelled dictionary password; and not
    equal to the email address or its local part (SIMF-API-001 section 12.5,
    hardened to NCA A7-10 / A7-28 / A7-29). The single source of truth is
    `SIMF.Common.PasswordPolicy`.
  - The confirmation equals the password.
- **Processing:**
  1. Validate the input against the rules above.
  2. Create a `User` in the `Registered` state, storing the password as a hash
     (the ASP.NET Core Identity hasher; never the plain password).
  3. Generate a six-digit numeric verification code.
  4. Store the code as an account code with `Purpose = EmailVerification`, a
     created time, and an expiry 10 minutes ahead.
  5. Send the code to the email through the notification service (email
     channel).
  6. Write a sign-up entry to the operation log.
- **Result:** HTTP 201; the response states the code's lifetime in seconds.
- **Failure:**
  - Invalid input → `VALIDATION_FAILED` (400), one `details` entry per field.
  - Email already registered → `AUTH_EMAIL_ALREADY_REGISTERED` (409).

### 5.2 Email verification

- **Trigger:** the user submits the email and the six-digit code
  (`POST /auth/verify-email`).
- **Rules:**
  - The email identifies an account in the `Registered` state.
  - The code matches the most recent unconsumed code for that account.
  - The code has not expired.
- **Processing:**
  1. Find the account and its latest `EmailVerification` code.
  2. Check the code matches, is unconsumed and unexpired.
  3. Mark the code consumed.
  4. Move the account from `Registered` to `EmailVerified`.
  5. Write a verification entry to the operation log.
- **Result:** HTTP 200; the response confirms the email is verified. The client
  continues to the registration profile (SIMF-FDS-002).
- **Failure:**
  - Wrong code → `AUTH_CODE_INVALID` (400).
  - Expired code → `AUTH_CODE_EXPIRED` (400).
  - No such account → `AUTH_ACCOUNT_NOT_FOUND` (404).

### 5.3 Resend the verification code

- **Trigger:** the user asks for a new code (`POST /auth/resend-code`).
- **Rules:** the email identifies a `Registered` account; the caller is within
  the resend rate limit.
- **Processing:** invalidate any unconsumed `EmailVerification` code for the
  account; generate, store and send a new one; reset the 10-minute expiry.
- **Result:** HTTP 200; the response states the new code's lifetime.
- **Failure:** no such account → `AUTH_ACCOUNT_NOT_FOUND` (404); rate limit
  exceeded → `RATE_LIMIT_EXCEEDED` (429).

### 5.4 Sign-in

- **Trigger:** the user submits an email and a password (`POST /auth/sign-in`).
- **Processing:**
  1. Find the account by email.
  2. Verify the password against the stored hash.
  3. If the account does not exist or the password is wrong, fail with one
     generic error — the response never says which of the two was wrong.
  4. Branch on the account state (section 5.5).
  5. Branch on whether the user is an internal user (section 5.6).
  6. On success, issue the tokens (section 5.7), and write a sign-in entry to
     the operation log.
- **Result:** HTTP 200 — either the token payload, or, for an internal user, an
  `mfaRequired` result (section 5.6).
- **Failure:** see section 5.5 and the error table in section 9.

### 5.5 Account-state handling at sign-in

Sign-in behaviour depends on the account state (SIMF-RPM-001 section 6):

| State | Sign-in result |
|-------|----------------|
| Registered | Refused — `AUTH_EMAIL_NOT_VERIFIED` (403). The email is not verified. |
| EmailVerified | Allowed. The user has not completed registration; the client takes them to the profile step. |
| PendingApproval | **Allowed**, with limited access. A session is issued whose claims grant only the registration-status view and guest-level content (decision D1). |
| Approved | Allowed. Full access for the user's final type. |
| Rejected | Refused — `AUTH_ACCOUNT_NOT_APPROVED` (403), with the rejection made clear. |
| Disabled | Refused — `AUTH_ACCOUNT_DISABLED` (403). |

The access token carries the account state and the granted permissions, so each
client enforces the same limits without a second call.

### 5.6 Internal users and the second factor

An internal user — anyone holding a Control Panel role — must pass a TOTP second
factor (FR-105, SIMF-API-001 section 12.3).

- When sign-in (section 5.4) succeeds for an internal user, the API does **not**
  issue tokens. It returns `mfaRequired: true` and a short-lived `mfaToken`.
- The user submits the `mfaToken` and the six-digit TOTP code from their
  authenticator app (`POST /auth/verify-totp`).
- The API checks the `mfaToken` is valid and unexpired, and the TOTP code is
  correct against the user's stored TOTP secret.
- On success the API issues the tokens (section 5.7).
- **Failure:** invalid or expired `mfaToken` → `AUTH_MFA_TOKEN_INVALID` /
  `AUTH_MFA_TOKEN_EXPIRED` (400); wrong code → `AUTH_TOTP_INVALID` (400).

First-time TOTP enrolment — issuing the TOTP secret and the QR for the
authenticator app — is part of internal-user onboarding and is specified with
SIMF-FDS-002; this feature assumes the secret exists for an internal user.

### 5.7 Tokens

On a successful sign-in, the API issues (SIMF-API-001 section 12.2):

- an **access token** — a JWT carrying the user id, the account state, the user
  type and the granted permissions, expiring 5 minutes after issue (D-443);
- a **refresh token** — opaque to the client, stored as a hash, defining the
  session length: an **absolute 24-hour** life from sign-in. Rotation carries
  the original deadline forward (it does not slide), so the session is capped at
  24 hours even for a continuously active user (D-443).

### 5.8 Token refresh

- **Trigger:** the client exchanges a refresh token (`POST /auth/refresh`).
- **Processing:** find the refresh token by its hash; check it is valid,
  unexpired and not revoked; revoke it; issue a new access token and a new
  refresh token (rotation); record the new token's `RotatedFromId`.
- **Result:** HTTP 200, a fresh token pair.
- **Failure:** invalid → `AUTH_REFRESH_TOKEN_INVALID` (401); expired (the 24-hour
  session has ended) → `AUTH_REFRESH_TOKEN_EXPIRED` (401) — the user signs in
  again.

A refresh token that is presented after it has already been rotated is treated
as invalid, and is logged, because reuse can indicate a stolen token.

### 5.9 Sign-out

- **Trigger:** the user signs out (`POST /auth/sign-out`); requires a valid
  access token.
- **Processing:** revoke the supplied refresh token so it cannot be used again;
  write a sign-out entry to the operation log.
- **Result:** HTTP 200.

### 5.10 Password reset

The password-reset flow follows SIMF-API-001 section 12.7.

- **Forgot password:** the user submits their email. If an account exists, the
  API sends a six-digit reset code to the email; the response is the **same**
  whether or not the account exists, so the endpoint does not reveal who has an
  account.
- **Reset password:** the user submits the email, the reset code, and a new
  password. The API checks the code matches and is unexpired, applies the
  password policy to the new password, stores the new hash, and invalidates the
  reset code and every active refresh token for that account.
- **Failure:** wrong reset code → `AUTH_RESET_CODE_INVALID` (400); expired →
  `AUTH_RESET_CODE_EXPIRED` (400).

## 6. Data

The feature uses these entities from SIMF-DAT-001 section 5.1: `User`,
`RefreshToken`, `EmailVerificationCode` and `TotpSecret`.

One adjustment is needed. The password-reset flow (section 5.10) needs a stored
reset code. Rather than a second code table, the account-code entity is
generalised: `EmailVerificationCode` becomes an account-code entity with a
`Purpose` field — `EmailVerification` or `PasswordReset` — and the rest of its
columns unchanged. This is recorded as open item OI-1 against SIMF-DAT-001.

## 7. User interface

The feature appears on these screens. The visual design of the mobile screens
is the external designer's; the Control Panel screens follow SIMF-CPD-001.

| Surface | Screens |
|---------|---------|
| Mobile app | Screen 3 Login · Screen 4 Sign-up step 1 · Screen 6 Email OTP verification (the mockup also holds an alternate Screen 6 photo-verification variant, which belongs to registration, not this feature) |
| Control Panel | The sign-in screen and the TOTP step |
| Website | **None — the public Website carries no sign-in and no account area (D-774, 2026-07-27; OI-3 closed).** Its only authentication-adjacent page is `/meeting/confirm`, which is anonymous and token-addressed (the opaque token in the emailed link is the sole credential). |

Every screen shows a loading state and a clear error state; a field error is
shown against its field, using the `details` entries from the API response.
All text is localised (Arabic and English); no string is hardcoded.

## 8. Validation rules

| Field | Rule |
|-------|------|
| Email | Required; valid email format; unique at sign-up |
| Password | Required; 8–128 characters; ≥ 1 upper-case, ≥ 1 lower-case, ≥ 1 digit, ≥ 1 special character; no 3 identical characters in a row; no 3-character sequential run; not a common/leet dictionary password; not equal to the email or its local part (`SIMF.Common.PasswordPolicy`) |
| Confirm password | Required; equals the password |
| Verification code | Required; exactly 6 digits; matches the issued code; unexpired |
| TOTP code | Required; exactly 6 digits; valid for the current time window |
| Reset code | Required; exactly 6 digits; matches the issued reset code; unexpired |

Validation failures return `VALIDATION_FAILED` with one `details` entry per
field; the field names match the request body exactly (SIMF-API-001 section 7).

## 9. Security considerations

- Passwords are stored only as hashes (ASP.NET Core Identity hasher); the plain
  password is never logged or stored.
- The invalid-credentials response is generic — it never reveals whether the
  email exists or the password was wrong.
- The forgot-password response does not reveal whether an account exists.
- The authentication endpoints carry tighter rate limits than the rest of the
  API; an exceeded limit returns `RATE_LIMIT_EXCEEDED` (429).
- Verification, TOTP and reset codes are six digits, single-use, and expire;
  a consumed or expired code is rejected.
- Refresh tokens are stored as hashes, rotate on every use, and a reused token
  is rejected and logged.
- Sign-in, sign-out, password change and a reused-token event are written to
  the operation log (NFR-03).
- Every authentication endpoint is anonymous by necessity and is on the short
  approved anonymous list; no other endpoint is opened (SIMF-SES-001 section 12).

## 10. Acceptance criteria

The feature is accepted when all of the following hold:

1. A new user can sign up, receive a code, verify the email, and the account
   reaches `EmailVerified`.
2. Sign-up is refused for a duplicate email, and for a password that fails the
   policy, with clear field errors.
3. A verification code that is wrong, expired or already used is rejected; a
   resent code works and invalidates the previous one.
4. An `EmailVerified` or `Approved` user can sign in and receives a valid token
   pair; the access token carries the correct state and permissions.
5. A `Registered` user cannot sign in; a `PendingApproval` user signs in to the
   limited status view; a `Disabled` or `Rejected` user cannot sign in.
6. An internal user must pass the TOTP step; tokens are issued only after it.
7. Wrong credentials give one generic error that does not reveal the cause.
8. A refresh token rotates correctly; an expired or reused refresh token is
   rejected; sign-out invalidates the refresh token.
9. Password reset works end to end, does not reveal account existence, and
   invalidates active sessions on success.
10. All authentication screens render correctly in Arabic (RTL) and English
    (LTR) with no hardcoded text.
11. The endpoints enforce their rate limits.
12. The build is clean (zero warnings) and the feature has unit, integration
    and end-to-end tests that pass (SIMF-SES-001 section 11).

## 11. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Sign up with valid new details | 201; account `Registered`; code emailed |
| T-02 | Sign up with an already-registered email | 409 `AUTH_EMAIL_ALREADY_REGISTERED` |
| T-03 | Sign up with a weak password / mismatched confirmation | 400 `VALIDATION_FAILED` with field errors |
| T-04 | Verify with the correct code | 200; account `EmailVerified` |
| T-05 | Verify with a wrong, expired or used code | 400 `AUTH_CODE_INVALID` / `AUTH_CODE_EXPIRED` |
| T-06 | Resend the code, then verify with the new code | new code works; the old one is rejected |
| T-07 | Sign in as an `EmailVerified` / `Approved` user | 200; valid token pair |
| T-08 | Sign in as a `Registered` user | 403 `AUTH_EMAIL_NOT_VERIFIED` |
| T-09 | Sign in as a `PendingApproval` user | 200; session limited to the status view |
| T-10 | Sign in as a `Disabled` / `Rejected` user | 403 |
| T-11 | Sign in with a wrong password | 401 `AUTH_INVALID_CREDENTIALS`, generic |
| T-12 | Internal user sign-in then TOTP | password step returns `mfaRequired`; tokens issued only after a correct TOTP |
| T-13 | Internal user with a wrong TOTP code | 400 `AUTH_TOTP_INVALID`; no tokens |
| T-14 | Refresh with a valid refresh token | 200; new rotated pair |
| T-15 | Refresh with an expired or reused token | 401; reuse is logged |
| T-16 | Sign out, then reuse the refresh token | the refresh token is rejected |
| T-17 | Forgot password for an existing and a non-existing email | identical response in both cases |
| T-18 | Reset the password with a valid code | password changes; active sessions are invalidated |
| T-19 | Exceed the sign-in rate limit | 429 `RATE_LIMIT_EXCEEDED` |
| T-20 | Render every auth screen in Arabic and English | correct layout and direction; no hardcoded text |

## 12. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Generalise `EmailVerificationCode` in SIMF-DAT-001 to an account-code entity with a `Purpose` field (email verification / password reset) | Section 6 |
| OI-2 | Confirm the verification/reset code lifetime (10 minutes assumed) and the auth rate-limit values with the owner | Sections 5, 9 |
| ~~OI-3~~ | **CLOSED 2026-07-27 by D-774 — the website offers NO sign-in; only the app and the Control Panel do.** Asked directly, the owner answered "remove auth from web", so the Website's `/login`, `/login/verify`, `/forgot-password`, `/reset-password` and `/account/*` routes were deleted together with the Website-local cookie scheme and its auth plumbing. This closes OI-3 on the negative, which is the exit D-018 recorded in advance. `/meeting/confirm` is deliberately kept (anonymous, token-addressed). See `docs/decisions/DECISIONS_LOG.md` **D-774** (and D-018, D-024, both marked superseded there). | Section 7, B.2, B.8, B.12 |
| OI-4 | Confirm document classification with the owner | Control block |

---

## Amendment A — Architecture review (2026-05-21)

The authentication design review of 2026-05-21 amends this feature
specification. The changes below are authoritative.

### A.1 Account lockout and brute-force control
ASP.NET Core Identity lockout is enabled — a configured failed-attempt
threshold and lockout window on the password step. Every verification, reset,
TOTP and email-OTP code additionally carries a per-code attempt counter and is
invalidated after a small number of failed attempts, independent of its time
expiry. A locked account is reported with `AUTH_ACCOUNT_LOCKED`.

### A.2 The visitor second factor
§5.6 specified TOTP for internal users only. The feature now also gives
**visitors a second factor by email OTP**: a visitor's password step returns
`mfaRequired` with an `otpToken` and emails a six-digit code, completed at
`POST /auth/verify-otp` (SIMF-API-001 Amendment A.1). Admins keep TOTP.

### A.3 Second-factor tokens
The `mfaToken` (admin) and `otpToken` (visitor) are short-lived (2–5 minutes),
single-use, stored hashed, invalidated after the per-code attempt cap, and
bound to the originating sign-in.

### A.4 The superadmin TOTP bootstrap
The seeded administrator `superadmin@zagali-ict.com` is created **with its TOTP
secret already provisioned**; the secret / QR is delivered to the operator
out-of-band through the `set-env-*` script. This removes the bootstrap deadlock
— the system is administrable from first run. First-sign-in TOTP enrolment for
other internal users remains with SIMF-FDS-002.

### A.5 Forced password change
A seeded or admin-created account holding a temporary password is in a
**password-change-required** state. The only action it may take is
`change-password`; every other protected endpoint returns
`AUTH_PASSWORD_CHANGE_REQUIRED` until the password is changed.

### A.6 Sessions and revocation
Concurrent sessions are allowed (web, app, Control Panel); sign-out is
per-device. An admin can **force-sign-out a user** — revoke all of that user's
refresh tokens. Because an access token is valid for only 5 minutes (D-443), the token
carries a **per-user security stamp**; sensitive Control Panel endpoints check
it server-side so that disabling an account or revoking a Control Panel role
takes effect immediately rather than after the token expires.

---

## Amendment B — Implementation update (2026-05-24)

This amendment captures everything actually built between 2026-05-22 and
2026-05-24. The v1.1 body of this document remains the source of truth for the
*specification*; this amendment is the source of truth for the *implementation*
where the two have refined each other. Every claim below is traceable to a
decision row in `docs/decisions/DECISIONS_LOG.md` (the `D-NNN` references) and
to the relevant commit on `feature/login-api`.

### B.1 Bilingual error messages (D-030)

§9 of the v1.1 body said the API would negotiate one language per request.
**This is reversed.** Every `ApiError` and `ApiErrorDetail` now carries
**both** the English message and the Arabic message at all times. The client
picks the right one with the user's culture. The two messages travel as
`message` (EN) and `messageArabic` (AR) on every error envelope; both are
**required** fields. SIMF-API-001 §7 is updated in a separate amendment.

Why: a single auth call serves any client culture, removes cross-call
language drift during a language switch, and matches the customer's "apply
multi-language in error, mandatory" instruction.

Identity error descriptions raised by ASP.NET Core Identity itself are
mapped to Arabic by `IdentityErrorTranslator` keyed on the Identity error
code.

### B.2 The Control Panel session — cookie + ticket hand-off (D-026, D-029, D-037)

The Control Panel issues a **persistent authentication cookie** on sign-in.
A Blazor interactive circuit cannot write a cookie (the response has begun by
the time the circuit handles the user interaction), so the interactive
verification page stashes the completed token pair in a short-lived,
single-use, server-side ticket and **full-page-navigates to a completion
endpoint** that issues the cookie.

The cookie carries:
- the user identity (claims set: `NameIdentifier`, `Email`, `Name`, every
  Control Panel role the user holds),
- (encrypted) the SIMF API access + refresh tokens, so the CP can call the
  API on behalf of the user without re-prompting.

Cookie shape: `HttpOnly`, `SameSite=Lax`, **8 h sliding expiration**. The CP
defaults to **deny** for every page (`[Authorize]` in `Components/_Imports.razor`);
the anonymous surface is the sign-in pages, password-reset, error and
not-found pages, the `/auth/complete` ticket-handoff endpoint, and the
`/culture` language-cookie endpoint.

**RETIRED 2026-07-27 (D-774).** The Website (SIMF.Web) gained the same
cookie-auth shape under D-046(c) so that `/account/visitor-profile` could
render server-side with the user's API tokens kept out of the browser. That
whole surface is gone: with the Website sign-in removed (OI-3 closed on the
negative), the `simf.web.auth` cookie scheme, `AddAuthorization`,
`UseAuthentication` / `UseAuthorization`, the ticket-handoff and
`/account/api/*` proxy endpoints and `SimfCookieRefreshHandler` were all
deleted, because nothing signed in on the Website any more. **The Control
Panel is unaffected** — it keeps the ASP.NET Core Identity default cookie
scheme described above, and it is now the only cookie-authenticated SIMF
surface.

### B.3 The CP proxy pattern (D-037)

A Control Panel page that needs to call the SIMF API does **not** call the
API directly from the Blazor circuit. Each call goes through a same-origin
**CP proxy endpoint** under `/account/api/…`. The browser sends the auth
cookie (same-origin → automatic); the proxy reads the access token from the
cookie's stored auth tokens and forwards the request to the API; the proxy
returns the upstream HTTP status verbatim so the page can react to 401 /
423 / 429 distinctly.

Why: this keeps the access token entirely server-side (the Blazor page
never sees it) and is the same pattern as the existing `/auth/sign-out`
form-post. A future `IAccessTokenSource` populated via `CircuitHandler`
could let pages call the API directly; until then the proxy stays.

### B.4 TOTP enrolment two-slot pattern (D-036)

Authenticator-app enrolment uses a **two-slot pattern**: a freshly issued
secret is stashed under the SIMF-owned provider token
`[SIMF]/PendingAuthenticatorKey` and only promoted to ASP.NET Core
Identity's active slot `[AspNetUserStore]/AuthenticatorKey` (and
`TwoFactorEnabled = true`) **once the user proves the QR was paired** by
submitting a first valid code.

Disable removes the active slot and clears the per-user
`LastUsedTotpTimestep` (the replay guard) so a re-enrol starts from a clean
state. The QR is rendered server-side as SVG via `QRCoder` — no client-side
QR JavaScript dependency, no plain-text secret in HTML outside the QR
bytes.

A wrong code at enrolment confirm now calls
`UserManager.AccessFailedAsync`, so brute-force at enrolment is bounded by
the account lockout budget (matches §A.1).

### B.5 TOTP secret format and time source (D-034)

`TotpVerifier` normalises a TOTP secret before Base32 decoding — strips
whitespace, uppercases — so a key that an authenticator app shows with
spaces (e.g. `dbji csx7 c3mj s2qa …`) decodes correctly. The TOTP time
source is **UTC** (Otp.NET uses `DateTimeOffset.UtcNow`); the verifier
checks the surrounding ± 1 time-step window.

### B.6 TOTP recovery codes (D-040)

TOTP enrolment issues **ten single-use recovery codes** as a
lost-authenticator fallback. Codes are 10-character Crockford-base32
(no `0/O/1/I/L/U`) printed as `XXXXX-XXXXX`; ~49 bits of entropy each;
stored as SHA-256 hex hashes in the `TotpRecoveryCodes` table; **revealed
plaintext exactly once** in the `TotpConfirmResponse` (and in
`RecoveryCodesResponse` on regenerate) and never again.

A user signs in with a recovery code at the new
`POST /api/v1/auth/verify-recovery-code` endpoint using the same MFA-token
ticket the TOTP step uses — a recovery code is an *alternative* second
factor, **not a 2FA bypass**. Wrong recovery-code attempts call
`AccessFailedAsync` (same lockout budget as a wrong TOTP), and the
ticket's per-code attempt cap bounds brute force per session. Regenerating
wipes the previous batch atomically; disabling 2FA wipes the codes too.

Audit events: `Totp.RecoveryCodesGenerated`,
`Totp.RecoveryCodesRegenerated`, `Totp.RecoveryCodeUsed`,
`Totp.RecoveryCodeFailed`. The profile page surfaces "X of 10 remaining"
with a "Regenerate" button and a low-codes warning when ≤ 3 remain.

### B.7 The second-factor flavour is chosen by enrolment, not role (D-040)

Amendment A.2 said TOTP is for internal users and email-OTP is for
visitors. This is **refined**: the second-factor kind is now TOTP for
**any user with an authenticator key paired**, not only role-holders, so a
visitor who enrols in 2FA from the profile page actually signs in via
TOTP (and can recover via recovery code). The visitor email-OTP path is
preserved for users with no authenticator key.

The legacy role-only path stays as a fallback for any pre-enrolment user
who carries a role but has not paired an authenticator.

### B.8 The `TwoFactorEnabled = false` bypass (D-033)

The sign-in second factor is **skipped** when the account has
`TwoFactorEnabled = false`. The API issues tokens directly on the password
step and `SignInResponse.Tokens` carries them; `MfaRequired` is `false`,
`MfaToken` and `OtpToken` are null. This applies to **both** Control Panel
users (today: TOTP) and visitors (today: email OTP).

Why: a visitor who has not opted into 2FA must not be forced through it.
The client sign-in surface branches on `MfaRequired`: tokens present →
complete sign-in; otherwise → continue to the second-factor page. Since
D-774 (2026-07-27) those surfaces are the **Control Panel sign-in page** and
the **Flutter app**; the Website no longer signs anyone in, so it no longer
takes part in this branch.

### B.9 Admin-driven 2FA reset (D-041)

Recovery from a lost authenticator **and** lost recovery codes is
**admin-driven**, not self-service email. A new
`POST /api/v1/admin/staff/reset-two-factor` endpoint, gated on the
`Administrator` role, wipes the target's authenticator key, recovery
codes, `TwoFactorEnabled` flag, security stamp and refresh tokens, and
queues a notification email to the target.

Rules:
- The actor cannot reset themselves (use the profile-page Disable
  instead).
- The actor cannot reset another `Administrator` (separation of
  privileges — the super-admin's recovery path stays out-of-band via the
  seeder + `appsettings.json` re-pair).
- A **mandatory free-text reason** (10–500 chars) is captured in the
  audit row alongside both actor and subject user ids (the new
  `OperationLog.ActorUserId` column).

The CP page is `/admin/reset-2fa` with a `[Authorize(Roles = "Administrator")]`
gate, a confirmation dialog, and bilingual strings. Operator-level SQL
reset stays documented as the fallback for the super-administrator.

Audit events: `Admin.TwoFactorReset`, `Admin.TwoFactorResetFailed`. An
admin-reset failure (self-reset, admin-vs-admin, missing target) emits
the failed event with the error code so a SIEM can alert on abuse
attempts.

### B.10 The `change-password` endpoint

A new `POST /api/v1/account/change-password` endpoint (authenticated)
lets a signed-in user change their own password. The endpoint:
- accepts `currentPassword`, `newPassword`, `confirmPassword`;
- applies the same password policy as sign-up (§5.1);
- updates the hash, rolls the security stamp, and revokes every active
  refresh token for the user (so other devices are signed out);
- audits `PasswordChange.Succeeded` or `PasswordChange.Failed`.

The CP profile page calls this through the proxy (B.3), then triggers a
client-side sign-out so the now-stale CP cookie is replaced.

### B.11 Sign-out and the SameSite-Lax CSRF stance (D-029)

The sign-out endpoint accepts only `POST`. The CP profile page invokes it
via a hidden form-post (`form.method = "POST"; form.action = "/auth/sign-out";`)
rather than `Nav.NavigateTo(…)` (which would issue a `GET` and 404 against
the POST-only route, leaving the cookie alive).

Because the CP cookie is `SameSite=Lax`, a cross-site multipart POST never
carries it — that defeats CSRF without an antiforgery token. This same
stance is reused for the avatar upload and the visitor ID-image upload.
If the cookie is ever made `SameSite=None`, both `/auth/sign-out` and
those multipart upload endpoints need an antiforgery token.

### B.12 Sign-in audience gate — `cp` / `web` / `app` (P2)

A `SignInRequest.Audience` field — enum `Web (0, default) / Cp (1) /
App (2)` — was added so the API can enforce that **only users with a CP
role sign in to the Control Panel**, and only **visitors** sign in to the
Web / App surfaces. A mismatch returns **403** with either
`AUTH_WRONG_SURFACE_CP` or `AUTH_WRONG_SURFACE_WEB`, and writes one
`SignIn.WrongSurface` audit row carrying the actor email and the
attempted audience.

The gate runs **after** the password and account-state checks so the
response can't be used as a credential-existence oracle. The CP sign-in
page sets `Audience = Cp` and the Flutter app sets `Audience = App`
(visitor-only).

**Since D-774 (2026-07-27) no first-party client sets `Audience = Web`.**
The Website has no sign-in, so nothing selects that surface deliberately.
The `SignInAudience.Web` enum value and its `AUTH_WRONG_SURFACE_WEB` error
code are **retained**, not removed: the enum is frozen against rename and
reorder under D-110, `Web = 0` is persisted in existing
`SignIn.WrongSurface` audit rows, and the `/api/v1/app/auth/*` endpoints
that accept it were not touched.

Note that `Web` is still the **wire default** — `SignInRequest.Audience`
initialises to `SignInAudience.Web` (`SIMF.Contracts/Authentication/SignIn.cs`),
deliberately, because it is the least-privileged surface: a caller that
omits the field is treated as a visitor and can never reach the Control
Panel. So `Web` is a reserved, no-longer-deliberately-selected audience that
remains the safe fallback, **not** a dead value to be deleted.

This implements the customer's instruction: "never any user type other
than super admin can access CP, and same for WEB/APP."

> **Open question — P7.** The audience gate currently classifies a user
> as "staff" if they hold any RBAC role and as "visitor" otherwise. The
> P7 rework (planned, not yet shipped) replaces this with a hardcoded
> `UserType` enum (Admin / Other / Visitor) on `SimfUser`, so:
> * `cp` → `UserType = Admin` only;
> * `web` → `UserType in (Visitor, Other)`;
> * `app` → `UserType in (Visitor, Other)`.
>
> RBAC roles will then apply **only** to `UserType = Admin`. The
> previously-planned "reviewer roles" (Staff / Scientific / Security)
> from P4 are dropped — they become rows in a new `ProfileTypes` lookup,
> not ASP.NET Identity roles. P7 awaits owner sign-off.

### B.13 The implemented endpoint surface

Every authentication-related endpoint actually shipped on
`feature/login-api`, with its policy, rate-limit, and audit events.

| Endpoint | Method | Auth | Rate-limit | Notes & main audit events |
|---|---|---|---|---|
| `/auth/sign-up` | POST | anonymous | `auth` | `SignUp.Succeeded` / `SignUp.DuplicateEmail` |
| `/auth/verify-email` | POST | anonymous | `auth` | `EmailVerification.*` (5 outcomes) |
| `/auth/resend-code` | POST | anonymous | `auth` | `ResendCode.*` (4 outcomes) |
| `/auth/sign-in` | POST | anonymous | `auth` | Carries `Audience` (B.12). `SignIn.BadCredentials` / `SignIn.AccountLockedOut` / `SignIn.StateBlocked` / `SignIn.SecondFactorIssued` / `SignIn.WrongSurface` / `SignIn.Succeeded` |
| `/auth/verify-totp` | POST | anonymous | `auth` | TOTP step. `SignIn.SecondFactorFailed/Rejected/Succeeded` |
| `/auth/verify-otp` | POST | anonymous | `auth` | Email-OTP step. Same audit family as TOTP |
| `/auth/verify-recovery-code` | POST | anonymous | `auth` | B.6. `Totp.RecoveryCode*` |
| `/auth/refresh` | POST | anonymous (presents refresh token) | `auth` | `RefreshToken.Issued/Rotated/Reused/Rejected` |
| `/auth/sign-out` | POST | bearer | `auth` | B.11. `SignOut.Succeeded` |
| `/auth/forgot-password` | POST | anonymous | `auth` | `ForgotPassword.Requested` |
| `/auth/reset-password` | POST | anonymous | `auth` | `PasswordReset.*` (5 outcomes) |
| `/account/change-password` | POST | bearer | `auth` | B.10. `PasswordChange.Succeeded/Failed` |
| `/account/profile` | GET | bearer | `auth` | the signed-in user's profile (incl. 2FA status, recovery-code count, avatar URL) |
| `/account/avatar` | POST / DELETE / GET | bearer | `auth` | D-039 filesystem storage |
| `/account/totp/setup` | POST | bearer | `auth` | B.4. `Totp.EnrolmentStarted` |
| `/account/totp/confirm` | POST | bearer | `auth` | B.4. `Totp.EnrolmentConfirmed/Failed` |
| `/account/totp/disable` | POST | bearer | `auth` | `Totp.Disabled/DisableFailed` |
| `/account/recovery-codes/regenerate` | POST | bearer | `auth` | B.6. `Totp.RecoveryCodesRegenerated` |
| `/admin/staff/reset-two-factor` | POST | bearer + `AdministratorOnly` | `auth` | B.9. `Admin.TwoFactorReset/Failed` |

The full request / response shape for each endpoint lives in SIMF-API-001
(Amendment B is owed there in lock-step with this one).

### B.14 Implementation decisions index

The decisions that fed this amendment, in chronological order. Each row
is a single decision in `docs/decisions/DECISIONS_LOG.md` — the row
there is the authoritative narrative; this index is the cross-reference
back to the FDS section.

| ID | Date | Subject | FDS section |
|---|---|---|---|
| D-022 → D-028 | 2026-05-22 | Frontend login increment — login UX, brand, language switch, CP base shell | §7 (UI), B.2, B.3 |
| D-029 | 2026-05-22 | CP default-deny + anonymous surface | B.2 |
| D-030 | 2026-05-23 | Bilingual error messages | B.1 |
| D-031 | 2026-05-23 | Login pages localised (EN/AR) | §7 |
| D-032 | 2026-05-23 | Brand panel — SIMF logo + wordmark | §7 |
| D-033 | 2026-05-23 | `TwoFactorEnabled=false` bypass | B.8 |
| D-034 | 2026-05-23 | TOTP secret format + UTC time source | B.5 |
| D-035 → D-039 | 2026-05-23 | Avatar storage migration (DB → filesystem); related cookie / proxy hardening | B.3 |
| D-036 | 2026-05-23 | TOTP enrolment two-slot pattern | B.4 |
| D-037 | 2026-05-23 | CP proxy pattern | B.3 |
| D-038 | 2026-05-23 | Five-agent review SEV-1/2 fixes for Part B Stage 2 | B.3, B.4, B.8 |
| D-040 | 2026-05-23 | TOTP recovery codes — 10 single-use Crockford | B.6, B.7 |
| D-041 | 2026-05-23 | Admin-driven 2FA reset | B.9 |
| D-046(a/b/c) | 2026-05-23 | Visitor QR id (Crockford 12-char), visitor-profile service + encrypted ID image, Website cookie + visitor-profile page | SIMF-FDS-002 Amendment A |
| **P1** | 2026-05-24 | Web login: Arabic label removed from EN language switch | §7 |
| **P2** | 2026-05-24 | Sign-in audience gate | B.12 |
| **P3** | 2026-05-24 | CP page split — staff vs visitors ("don't mix") | SIMF-FDS-002 Amendment A |
| **P4** | 2026-05-24 | Approval workflow + reviewer-role split | SIMF-FDS-002 Amendment A |
| **P5** | 2026-05-24 | Saudi national-ID + Iqama validator prefix rules | SIMF-FDS-002 Amendment A |
| **P6** | 2026-05-24 | Per-project log files + CP log viewer | n/a (orthogonal) |
| D-047 | 2026-05-24 | Per-project logs + CP viewer | n/a (orthogonal; SIMF-SAD-001 owed) |

### B.15 Open items added by Amendment B

| ID | Item | Affects |
|---|---|---|
| OI-5 | Approve and ship **P7** — replace the audience gate's "any RBAC role = staff" proxy with a hardcoded `UserType` enum on `SimfUser`, and replace the P4 reviewer roles (Staff / Scientific / Security) with rows in a new `ProfileTypes` lookup. | §5.5, B.12 |
| OI-6 | Bring SIMF-API-001 to Amendment B in lock-step with this amendment — the response envelope changes (D-030), the audience field (P2), the new endpoints (B.13). | SIMF-API-001 §7, §12 |
| OI-7 | Decide whether to make `MfaToken` and `OtpToken` ciphers in transit (today they are opaque random tokens hashed at rest; transit security relies on TLS). | B.6 |
| OI-8 | Document the **Flutter app** privilege enum (`Guest=0 / Visitor=1 / Staff / …`) — separate from the CP `UserType` — when SIMF-MAA-001 enters Amendment A. | n/a (Flutter scope) |

---

End of document.
