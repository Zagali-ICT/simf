# Feature Design Specification — Authentication and Login

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-001 |
| Title | Feature Design Specification — Authentication and Login |
| Version | 1.1 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-SRS-001, SIMF-UCS-001, SIMF-API-001, SIMF-DAT-001, SIMF-RPM-001, SIMF-SAD-001, SIMF-SES-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The authentication feature, build-ready. |
| 1.1 | 2026-05-21 | Engineering & Architecture Team | Architecture-review amendment (see Amendment A): account lockout; the visitor email-OTP second factor; second-factor token rules; the superadmin TOTP bootstrap; the forced-password-change state; sessions, admin force-sign-out and the token-revocation security stamp. |

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
  - The password meets the policy: at least 8 characters, at least one letter
    and one digit, and not equal to the email (SIMF-API-001 section 12.5).
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
  type and the granted permissions, expiring 30 minutes after issue;
- a **refresh token** — opaque to the client, stored as a hash, with a 30-day
  life that defines the session length.

### 5.8 Token refresh

- **Trigger:** the client exchanges a refresh token (`POST /auth/refresh`).
- **Processing:** find the refresh token by its hash; check it is valid,
  unexpired and not revoked; revoke it; issue a new access token and a new
  refresh token (rotation); record the new token's `RotatedFromId`.
- **Result:** HTTP 200, a fresh token pair.
- **Failure:** invalid → `AUTH_REFRESH_TOKEN_INVALID` (401); expired (the 30-day
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
| Website | The website sign-in, where the site offers it |

Every screen shows a loading state and a clear error state; a field error is
shown against its field, using the `details` entries from the API response.
All text is localised (Arabic and English); no string is hardcoded.

## 8. Validation rules

| Field | Rule |
|-------|------|
| Email | Required; valid email format; unique at sign-up |
| Password | Required; ≥ 8 characters; ≥ 1 letter and ≥ 1 digit; not equal to the email |
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
| OI-3 | Confirm whether the website offers sign-in, or only the app and Control Panel do | Section 7 |
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
refresh tokens. Because an access token is valid for 30 minutes, the token
carries a **per-user security stamp**; sensitive Control Panel endpoints check
it server-side so that disabling an account or revoking a Control Panel role
takes effect immediately rather than after the token expires.

---

End of document.
