# Use Case Specifications

| Field | Value |
|-------|-------|
| Document ID | SIMF-UCS-001 |
| Title | Use Case Specifications |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Project Owner (MoD / RSNF representative) |
| Date issued | 2026-05-20 |
| Related documents | SIMF-SRS-001, SIMF-RPM-001, SIMF-CON-001, SIMF-API-001, SIMF-UCS-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. Use case catalogue and the detailed specifications for the core use cases. |

---

## 1. Purpose

This document describes how the actors use SIMF — the catalogue of use cases,
and a detailed specification for each of the core ones. It turns the
requirements in SIMF-SRS-001 into the step-by-step flows that drive the screen
design, the API endpoints and the end-to-end tests.

## 2. Scope

Section 4 is the full use case catalogue — every use case, with an identifier
and the requirements it carries. Section 5 gives the detailed specification for
the core use cases: the flows that are central, risky, or the pattern others
follow. The remaining use cases follow the same template and are elaborated, at
that detail, in the per-feature design specifications (SIMF-FDS-NNN).

A detailed use case has a fixed shape: actor, preconditions, the main flow, the
alternate and exception flows, and the postcondition.

## 3. Actors

The actors are the user types in SIMF-RPM-001. In short: **Guest**, **Visitor**,
**Exhibitor**, **Moderator** and **Staff** on the external side; **Admin** and
the organising teams — **Security**, **PR**, **Technical**, **Scientific**,
**Logistics**, **Marketing** — on the Control Panel side. The **System** itself
is a secondary actor where it acts on a timer or an event.

## 4. Use case catalogue

### 4.1 Account and registration

| ID | Use case | Primary actor | Requirements |
|----|----------|---------------|--------------|
| UC-01 | Create an account | Guest | FR-101, FR-102 |
| UC-02 | Verify the email | Guest | FR-102, FR-103 |
| UC-03 | Complete the registration | Visitor / Exhibitor | FR-201–FR-209 |
| UC-04 | Sign in | Visitor / Exhibitor / internal user | FR-104–FR-106 |
| UC-05 | Browse as a guest | Guest | FR-215 |
| UC-06 | Reset a password | Visitor / Exhibitor / internal user | FR-107 |
| UC-07 | View registration status | Visitor / Exhibitor | FR-210 |

### 4.2 Attendee experience

| ID | Use case | Primary actor | Requirements |
|----|----------|---------------|--------------|
| UC-08 | Browse the agenda and a session | Visitor | FR-408, FR-409 |
| UC-09 | Book a session seat | Visitor | FR-501–FR-503, FR-505 |
| UC-10 | Cancel a booking | Visitor | FR-504 |
| UC-11 | View my badge and QR | Visitor / Exhibitor | FR-301, FR-302 |
| UC-12 | Scan another attendee's badge | Visitor / Exhibitor | FR-304 |
| UC-13 | Watch a live session | Visitor | FR-701, FR-702 |
| UC-14 | Ask a question in a session | Visitor | FR-703, FR-704 |
| UC-15 | Comment on a session | Visitor | FR-706, FR-707 |
| UC-16 | Request a one-to-one meeting | Visitor | FR-804 |
| UC-17 | Use the AI assistant | Visitor / Guest | FR-805, FR-806 |
| UC-18 | Browse exhibitors, booths and the venue map | Visitor / Guest | FR-603, FR-605 |
| UC-19 | Receive a notification | Visitor / Exhibitor | FR-901–FR-903 |

### 4.3 Control Panel

| ID | Use case | Primary actor | Requirements |
|----|----------|---------------|--------------|
| UC-20 | Review and decide a registration | Security team | FR-211–FR-214 |
| UC-21 | Approve an exhibitor and assign a booth | PR team | FR-602 |
| UC-22 | Approve a booking | PR team | FR-503 |
| UC-23 | Manage sessions | Scientific team | FR-401–FR-403 |
| UC-24 | Manage halls and seating | Scientific / Logistics | FR-404, FR-405 |
| UC-25 | Manage speakers | Scientific team | FR-406, FR-407 |
| UC-26 | Moderate session comments | PR / Scientific team | FR-706, FR-707 |
| UC-27 | Manage roles and permissions | Technical team / Admin | FR-1201, FR-1202 |
| UC-28 | Manage dynamic content and categories | Technical / Marketing | FR-1203, FR-1204 |
| UC-29 | Manage the FAQ knowledge and AI settings | Technical team | FR-806 |
| UC-30 | View the statistics dashboard | organising teams | FR-1101–FR-1103 |
| UC-31 | Open or close registration | Admin | FR-216 |
| UC-32 | Manage media, news and the archive | Marketing team | FR-1001–FR-1005 |

### 4.4 Field operations

| ID | Use case | Primary actor | Requirements |
|----|----------|---------------|--------------|
| UC-33 | Verify a badge at venue entry | Staff | FR-303 |
| UC-34 | Register a visitor on-site or reprint a badge | Staff | FR-217 |
| UC-35 | Check an attendee in at a hall door | Staff / System | FR-305 |

### 4.5 Moderator

| ID | Use case | Primary actor | Requirements |
|----|----------|---------------|--------------|
| UC-36 | Manage the questions of an assigned session | Moderator | FR-705 |

## 5. Detailed use case specifications

The core use cases below are specified in full. The others in the catalogue
follow the same template at the per-feature stage.

### UC-01 — Create an account

- **Primary actor:** Guest.
- **Preconditions:** the app or website is open; the email is not already
  registered.
- **Main flow:**
  1. The Guest enters an email, a password and a password confirmation.
  2. The system validates the input — a valid email, the password policy met,
     the confirmation matching.
  3. The system creates the account in the Registered state.
  4. The system sends a six-digit verification code to the email.
  5. The system shows the verification step (continues in UC-02).
- **Alternate / exception flows:**
  - A2. The email is already registered — the system reports it and offers
    sign-in.
  - A3. Validation fails — the system shows the field-level errors and the
    Guest corrects them.
- **Postcondition:** an account exists in the Registered state and a code has
  been sent.

### UC-02 — Verify the email

- **Primary actor:** Guest (the new account holder).
- **Preconditions:** an account is in the Registered state; a code was sent.
- **Main flow:**
  1. The Guest enters the six-digit code.
  2. The system checks the code is correct and not expired.
  3. The system moves the account to the EmailVerified state.
  4. The system continues to the registration profile (UC-03).
- **Alternate / exception flows:**
  - A2. The code is wrong — the system reports it; the Guest retries.
  - A3. The code has expired — the Guest requests a new code; the system
    invalidates the old one and sends a new one.
- **Postcondition:** the account is EmailVerified.

### UC-03 — Complete the registration

- **Primary actor:** Visitor or Exhibitor (the verified account holder).
- **Preconditions:** the account is EmailVerified; registration is open.
- **Main flow:**
  1. The user chooses a registration type — Visitor or Other.
  2. The user enters the personal data — the four-part Arabic name, the English
     name per passport, nationality, date of birth, place of birth.
  3. The user enters the identity number — national ID for a Saudi, or a chosen
     passport or Iqama number for a non-Saudi.
  4. The user enters the mobile number inside the Kingdom, and the outside
     number if an overseas visitor.
  5. The user attaches the ID image.
  6. The user selects a venue track / zone.
  7. The user reads the Terms & Conditions and gives consent.
  8. The user submits. The system creates the registration request, sets it to
     PendingApproval, and sends a message with the contact details.
- **Alternate / exception flows:**
  - A1. The user is an Exhibitor — the system also collects the organisation
    details and any companions.
  - A8. Registration has been closed — the system blocks submission and informs
    the user.
- **Postcondition:** a registration request is PendingApproval and visible to
  the Security team.

### UC-04 — Sign in

- **Primary actor:** any registered user.
- **Preconditions:** the account exists and is EmailVerified or beyond.
- **Main flow:**
  1. The user enters email and password.
  2. The system verifies the credentials.
  3. For an internal user, the system requests the TOTP code and verifies it.
  4. The system issues the access and refresh tokens and opens the user's home.
- **Alternate / exception flows:**
  - A2. The credentials are wrong — the system reports a single generic error.
  - A2b. The account is PendingApproval — the system signs the user in to the
    registration-status view with guest-level content only (decision D1).
  - A2c. The account is Disabled or Rejected — the system refuses sign-in with
    a clear reason.
  - A3. The TOTP code is wrong — the system refuses and the internal user
    retries.
- **Postcondition:** the user has a session, with access matching their account
  state and role.

### UC-09 — Book a session seat

- **Primary actor:** Visitor (Approved).
- **Preconditions:** the user is Approved; the session is open for booking; a
  seat is available.
- **Main flow:**
  1. The user opens a session and chooses to book a seat.
  2. The system shows the hall seat map with available seats.
  3. The user selects a seat.
  4. The system checks the session time does not overlap a session the user has
     already booked.
  5. The system creates the booking in the Pending state and tells the user it
     awaits Control Panel approval.
  6. The PR team approves the booking (UC-22); the system confirms it and
     notifies the user.
- **Alternate / exception flows:**
  - A4. The chosen session overlaps an existing booking — the system blocks the
    booking and explains.
  - A3b. The seat was taken in the meantime — the system asks the user to
    choose another.
  - A6. The PR team rejects the booking — the system informs the user.
- **Postcondition:** a booking exists, Pending then Approved, or it was not
  created.

### UC-14 — Ask a question in a session

- **Primary actor:** Visitor (Approved).
- **Preconditions:** the user is verified as arrived at the session hall; the
  session has not ended (decision D5).
- **Main flow:**
  1. The user opens the session's question composer.
  2. The user chooses the recipient — the speaker or the host — and writes the
     question.
  3. The user submits.
  4. The system records the question in the Pending state and tells the user it
     will be reviewed before it goes on air.
  5. The moderator handles the question (UC-36).
- **Alternate / exception flows:**
  - P1. The user has not arrived at the hall, or the session has ended — the
    system does not offer the composer.
- **Postcondition:** a question is recorded for the moderator, or none is.

### UC-15 — Comment on a session

- **Primary actor:** Visitor (Approved).
- **Preconditions:** the user is in the session.
- **Main flow:**
  1. The user writes a comment and submits it.
  2. The system passes the comment through the AI filter.
  3. The system places the comment in the moderation queue with its AI result.
  4. An admin reviews it (UC-26) and approves or discards it.
  5. An approved comment is shown in the session's comment feed.
- **Alternate / exception flows:**
  - A2. The AI filter flags the comment — it still goes to the queue, marked
    flagged; it is never discarded on the AI result alone (decision D5).
- **Postcondition:** the comment is in the queue, then approved and shown, or
  discarded by an admin.

### UC-20 — Review and decide a registration

- **Primary actor:** Security team.
- **Preconditions:** the user holds `registrations.view` and
  `registrations.approve` / `.reject`; one or more requests are PendingApproval.
- **Main flow:**
  1. The Security user opens the registration requests queue.
  2. The user opens a request and reviews the submitted data and attachments.
  3. The user approves the request.
  4. The system prompts for the final user type and the permissions; the user
     sets them.
  5. The system moves the account to Approved, issues the badge, and notifies
     the user.
- **Alternate / exception flows:**
  - A3. The user rejects the request — the system records a reason, sets the
    request to Rejected, and informs the applicant.
  - A1b. The user selects several requests and approves them in bulk
    (FR-212).
- **Postcondition:** each request is Approved — with a badge issued — or
  Rejected.

### UC-21 — Approve an exhibitor and assign a booth

- **Primary actor:** PR team.
- **Preconditions:** the user holds `exhibitors.approve`; an exhibitor request
  is PendingApproval.
- **Main flow:**
  1. The PR user opens the exhibitor request and reviews the organisation
     details.
  2. The PR user approves the exhibitor and, in the same step, assigns the
     booth — its hall and number (decision D3).
  3. The system marks the exhibitor Approved, records the booth, and notifies
     the exhibitor.
- **Alternate / exception flows:**
  - A2. The PR user rejects the exhibitor — the system records a reason and
    informs them.
- **Postcondition:** the exhibitor is Approved with a booth, or Rejected.

### UC-26 — Moderate session comments

- **Primary actor:** an admin with `moderation.act`.
- **Preconditions:** comments are waiting in the moderation queue.
- **Main flow:**
  1. The admin opens the moderation queue; new comments arrive live.
  2. For each comment the admin sees the text and the AI result.
  3. The admin approves the comment, and it appears in the session feed; or the
     admin discards it.
- **Alternate / exception flows:**
  - A3. The admin discards a comment — it does not appear; the action is logged.
- **Postcondition:** each reviewed comment is approved and shown, or discarded.

### UC-33 — Verify a badge at venue entry

- **Primary actor:** Staff.
- **Preconditions:** the Staff user is signed in to the mobile app with field
  permissions; the attendee presents a badge.
- **Main flow:**
  1. The Staff user scans the badge QR or barcode.
  2. The system checks the badge is valid and the holder is Approved.
  3. The system records the venue entry and shows a success result.
  4. The attendee enters.
- **Alternate / exception flows:**
  - A2. The badge is invalid or the holder is not Approved — the system shows a
    clear failure; the attendee is directed to the registration desk (UC-34).
- **Postcondition:** a venue entry is recorded, or entry is refused.

## 6. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Elaborate the remaining catalogue use cases at full detail in the SIMF-FDS-NNN specifications | Section 4 |
| OI-2 | Confirm the moderator's exact in-session controls for UC-36 against the app design | UC-36 |
| OI-3 | Confirm whether on-site registration (UC-34) follows the same approval flow as UC-03 or is approved on the spot | UC-34 |
| OI-4 | Confirm document classification with the owner | Control block |

---

# Part II — Page-level use cases (D-133 slice 8)

> **Authored:** D-133 slice 8 (2026-05-28). Adds use-case specs derived
> from the per-page reference docs under `docs/pages/{cp,web,mobile}/`.
> Each UC names the page that implements it + the E2E catalogue id that
> proves it works. Follows the same actor / preconditions / main flow /
> alternate / exception / postcondition shape as Part I §5.

## 7. Page-level catalogue (cross-reference)

| UC ID | Title | Implementing page(s) | E2E scenario(s) |
|-------|-------|----------------------|-----------------|
| UC-AUTH-SIGNIN-001 | Administrator signs in | [`cp/login.md`](pages/cp/login.md) + [`cp/login-totp.md`](pages/cp/login-totp.md) | E2E-AUTH-001 |
| UC-AUTH-RECOVERY-001 | Administrator signs in via recovery code | [`cp/login-totp.md`](pages/cp/login-totp.md) + [`cp/login-recovery.md`](pages/cp/login-recovery.md) | E2E-AUTH-005 |
| UC-AUTH-PENDING-001 | Pending admin sees holding page | [`cp/login.md`](pages/cp/login.md) + [`cp/auth-pending.md`](pages/cp/auth-pending.md) | E2E-AUTH-006 |
| UC-AUTH-REJECTED-001 | Rejected admin sees reason | [`cp/login.md`](pages/cp/login.md) + [`cp/auth-rejected.md`](pages/cp/auth-rejected.md) | E2E-AUTH-007 |
| UC-AUTH-FORGOT-001 | Forgot + reset password | [`cp/forgot-password.md`](pages/cp/forgot-password.md) + (Website reset) | E2E-AUTH-008 |
| UC-USR-CREATE-001 | Administrator invites another administrator | [`cp/admin-admins.md`](pages/cp/admin-admins.md) | E2E-USR-001 |
| UC-USR-BULK-DELETE-001 | Bulk-delete administrators with reason | [`cp/admin-admins.md`](pages/cp/admin-admins.md) | E2E-USR-002 |
| UC-VIS-WALKIN-001 | Register a walk-in visitor on-site | [`cp/admin-visitors.md`](pages/cp/admin-visitors.md) | E2E-VIS-001 |
| UC-VIS-WALKIN-NONSAUDI-001 | Walk-in non-Saudi visitor with Passport | [`cp/admin-visitors.md`](pages/cp/admin-visitors.md) | E2E-VIS-002 |
| UC-VPN-APPROVE-001 | Approve-with-review pending visitor | [`cp/admin-visitors-pending.md`](pages/cp/admin-visitors-pending.md) | E2E-VPN-001 |
| UC-VPN-REJECT-001 | Reject pending visitor with reason | [`cp/admin-visitors-pending.md`](pages/cp/admin-visitors-pending.md) | E2E-VPN-002 |
| UC-INT-CREATE-001 | Add an interest | [`cp/admin-interests.md`](pages/cp/admin-interests.md) | E2E-INT-001 (Add step) |
| UC-INT-EDIT-001 | Edit an interest | [`cp/admin-interests.md`](pages/cp/admin-interests.md) | E2E-INT-001 (Edit step) |
| UC-INT-DEACTIVATE-001 | Deactivate an interest | [`cp/admin-interests.md`](pages/cp/admin-interests.md) | E2E-INT-001 (Deactivate step) |
| UC-VPT-CREATE-001 | Add a visitor profile-type with PageColor | [`cp/admin-profile-types-visitor.md`](pages/cp/admin-profile-types-visitor.md) | E2E-VPT-001 |
| UC-PRT-REPRINT-001 | Reprint a visitor's badge by QR id | [`cp/admin-print-bag.md`](pages/cp/admin-print-bag.md) | E2E-PRT-001 |
| UC-2FA-RESET-001 | Administrator resets a user's 2FA | [`cp/admin-reset-2fa.md`](pages/cp/admin-reset-2fa.md) | E2E-2FA-001 |
| UC-LOG-TAIL-001 | Tail a project log in the browser | [`cp/admin-logs.md`](pages/cp/admin-logs.md) | E2E-LOG-002 |
| UC-NTF-DISMISS-001 | Dismiss + bulk-dismiss notifications | [`cp/account-notifications.md`](pages/cp/account-notifications.md) | E2E-NTF-004 |
| UC-PRF-AVATAR-001 | Change my avatar (Cropper.Blazor flow) | [`cp/account-profile.md`](pages/cp/account-profile.md) | E2E-PRF-002 |
| UC-WEB-PRF-FILL-001 | Visitor fills profile, gets QR | [`web/account-profile.md`](pages/web/account-profile.md) | E2E-WEB-PRF-001 |
| UC-WEB-NTF-001 | Visitor reads notifications inbox | [`web/account-notifications.md`](pages/web/account-notifications.md) | E2E-WEB-NTF-001 |

## 8. Detailed use cases (slice 8 priority subset)

The 10 highest-priority entries below are fully specified now. The
remaining entries in §7 keep the same shape — fill from the per-page
doc's §1 Purpose + §5 Data flow + §6 Validation + §7 Edge cases when
authoring.

### UC-AUTH-SIGNIN-001 — Administrator signs in

- **Actor:** Administrator (paired authenticator).
- **Preconditions:**
  - Account exists, AccountState = `Approved`, role includes `Administrator`.
  - User knows email + password + has the paired authenticator app.
- **Main flow:**
  1. User opens [`/login`](pages/cp/login.md).
  2. Enters email + password → **Sign in**.
  3. Server validates credentials, checks lockout, issues a
     `SecondFactorToken` ticket (5-min TTL), and redirects to
     [`/login/totp`](pages/cp/login-totp.md).
  4. User opens authenticator, reads the current 6-digit code, submits.
  5. Server validates TOTP (30-s window + 1-step tolerance), issues JWT
     access (30-min) + refresh (14-day) token pair.
  6. CP host persists the pair into the cookie via
     `SimfCookieRefreshHandler.StoreTokens` (D-121) along with `expires_at`.
  7. Browser is redirected to `/` (Dashboard).
- **Alternate flow A — Pending account:** at step 5, if AccountState is
  `PendingApproval`, server returns `AuthRequiresApproval`; redirect to
  [`/auth/pending`](pages/cp/auth-pending.md). See UC-AUTH-PENDING-001.
- **Alternate flow B — Rejected account:** at step 5, redirect to
  [`/auth/rejected`](pages/cp/auth-rejected.md). See UC-AUTH-REJECTED-001.
- **Exception — Wrong password:** at step 3, 401 with bilingual error.
  After 5 failures in 5 min: lockout (15 min). Audit `Auth.SignInFailed`.
- **Exception — Wrong TOTP:** at step 5, 401 with `Account.SignIn.Totp.Invalid`.
  User may retry within the ticket's 5-min TTL.
- **Postcondition:** the user holds an `Administrator`-roled session in
  a cookie that holds a fresh access + refresh pair. The cookie's
  `OnValidatePrincipal` hook (D-121) keeps the access token fresh on
  every request.

### UC-AUTH-RECOVERY-001 — Sign in via recovery code

- **Actor:** Administrator without authenticator access.
- **Preconditions:** account Approved + has unused recovery codes.
- **Main flow:**
  1. After password step, user lands on `/login/totp`.
  2. Clicks **Use a recovery code instead** → routes to
     [`/login/recovery`](pages/cp/login-recovery.md).
  3. Pastes one of the 10 single-use codes.
  4. Server validates + **consumes** the code; mints token pair.
  5. Browser routes to `/`.
- **Exception — Used code:** server returns 401 + bilingual error.
- **Exception — No codes remaining:** user must contact another admin to
  reset 2FA via [`/admin/reset-2fa`](pages/cp/admin-reset-2fa.md)
  (UC-2FA-RESET-001).
- **Postcondition:** signed in; the consumed code is permanently
  unusable. User should re-pair TOTP via
  [`/account/profile`](pages/cp/account-profile.md) → Reset my 2FA to
  generate fresh codes.

### UC-AUTH-FORGOT-001 — Forgot + reset password

- **Actor:** Anyone with a SIMF account email.
- **Preconditions:** none (page is anonymous).
- **Main flow:**
  1. User opens [`/forgot-password`](pages/cp/forgot-password.md).
  2. Enters email → **Send code**.
  3. Server always returns success (anti-enumeration). If the email
     exists, server issues a 6-digit `PasswordReset` code (15-min TTL,
     rate-limited 3/min/email/IP) and emails it.
  4. User retrieves the code from email → opens
     [`/reset-password`](pages/web/reset-password.md) (Web) OR equivalent
     CP page.
  5. Enters code + new password meeting complexity (12+ chars + digit +
     upper + lower + special).
  6. Server validates, replaces password atomically via
     `RemovePasswordAsync` + `AddPasswordAsync` (D-014), clears the
     forced-change flag, **revokes every session for the account**
     (D-019), audits `Auth.PasswordReset`.
  7. User redirects to `/login` → signs in with new password (TOTP still
     required).
- **Alternate flow — Code expired:** at step 5, server rejects with
  bilingual `Auth.ResetCode.Expired`. User starts again from step 1.
- **Exception — Weak new password:** server returns 400 with the
  composite bilingual complexity message.
- **Postcondition:** password updated; all prior sessions for the
  account ended; the user holds a fresh session if they signed in at step 7.

### UC-USR-CREATE-001 — Invite a new administrator

- **Actor:** Administrator (the inviter).
- **Preconditions:** inviter is `Approved` and holds `Administrator` role.
- **Main flow:**
  1. Inviter opens [`/admin/admins`](pages/cp/admin-admins.md) and clicks
     **+ Add**.
  2. The Add modal hosts [`CreateAdminForm`](pages/cp/admin-admins.md#4-ui-affordances).
  3. Inviter fills Email, Display name, Password (12+ chars complexity),
     TOTP-on-first-login (on).
  4. Clicks **Create administrator**.
  5. Server validates with `AdminCreateUserRequestValidator`, creates
     `SimfUser` (state = `Approved` because admin-invited), assigns
     `Administrator` role, mints first-login TOTP-pairing ticket,
     audits `Admin.UserCreated`.
  6. Modal closes; grid reloads; toast `Admin.CreateAdmin.Success`.
  7. Inviter shares the credentials out-of-band; invitee will land on
     [`/account/totp-pairing`](pages/cp/account-totp-pairing.md) on first
     sign-in.
- **Alternate flow — Email exists:** at step 5, server returns 409 +
  `EmailAlreadyExists`. Toast shows the bilingual message; modal stays open.
- **Exception — Self-invite by mistake:** the inviter cannot use their
  own email (same as Email exists path).
- **Postcondition:** new `Administrator` account exists, ready for first
  sign-in; an audit row records who invited whom.

### UC-USR-BULK-DELETE-001 — Bulk-delete administrators with reason

- **Actor:** Administrator (deleter).
- **Preconditions:** deleter is `Approved` and holds `Administrator` role;
  has at least one row selected.
- **Main flow:**
  1. Deleter selects N rows via checkboxes on
     [`/admin/admins`](pages/cp/admin-admins.md).
  2. Clicks toolbar **Delete** → bulk-delete modal opens.
  3. Types a 10–500 character reason.
  4. Clicks **Delete**.
  5. Server iterates the selected ids inside a transaction; for each,
     soft-deletes via `entity.Deactivate()`, audits with the reason.
  6. Self-id in the batch is **silently skipped** + audited
     `Admin.UserSelfDeleteSkipped`.
  7. Toast surfaces `{Deleted}, {Skipped}` counts; grid reloads.
- **Exception — Reason too short / long:** the Submit button is disabled
  client-side; server-side also rejects with bilingual
  `Admin.Bulk.Reason.Invalid`.
- **Exception — Unknown id in the batch:** silently skipped (same as
  self-delete path; no leak).
- **Postcondition:** matching `SimfUser` rows are soft-deleted; the
  deleter's own row, if present, is unchanged.

### UC-VIS-WALKIN-001 — Walk-in visitor registration (Saudi)

- **Actor:** Administrator (registration-desk staff).
- **Preconditions:** desk is signed in; at least one Visitor profile-type
  is seeded under [`/admin/profile-types/visitor`](pages/cp/admin-profile-types-visitor.md).
- **Main flow:**
  1. Desk opens [`/admin/visitors`](pages/cp/admin-visitors.md), clicks
     **+ Add**.
  2. Wizard opens at section 1 (Badge type). Desk picks a colour-coded
     profile-type tile.
  3. Section 2 (Identity): types Name on badge + DOB → English name +
     Arabic name → Place of birth.
  4. Section 3: Saudi toggle on → 10-digit national ID starting with 1.
  5. Section 4: Saudi mobile (+9665XXXXXXXX). Optional email.
  6. Section 5 (optional): ID document upload (≤ 5 MB PNG/JPEG/WebP).
  7. Section 6: up to 10 interest chips.
  8. Clicks **Register**.
  9. Server (`AdminAccountService.RegisterOnSiteAsync`) opens transaction:
     creates `SimfUser` (AccountState = `Approved`), creates
     `UserProfile`, links interests, mints QR via `IQrIdMinter`, audits
     `Admin.WalkInRegistered`. Commits.
  10. If ID document was attached, posts to
      `/admin/visitors/{id}/id-document` (best-effort; failure surfaces
      as `HasIdImage = false` in Details).
  11. `WalkInSuccessModal` renders with the printed badge: profile-type
      colour stripe + Name + QR SVG + QR id. Desk clicks **Print badge**.
- **Alternate flow — Non-Saudi:** at section 3 toggle off → country
  picker + Iqama (10 digits starting with 2) OR Passport (≤ 20 chars).
  See UC-VIS-WALKIN-NONSAUDI-001.
- **Alternate flow — Email blank:** server synthesises
  `walkin-{guid}@simf.local` so Identity has something to anchor.
- **Exception — Cross-kind profile-type id:** server returns 400
  `AdminProfileTypeInvalid`.
- **Exception — Duplicate email (when provided):** server returns 409
  `EmailAlreadyExists`; the wizard stays open.
- **Postcondition:** visitor has an `Approved` account + an active QR
  badge + (optional) encrypted ID image. Desk has a printable badge in
  hand. Audit row records the desk operator + the new visitor.

### UC-VPN-APPROVE-001 — Approve-with-review pending visitor (D-128)

- **Actor:** Administrator.
- **Preconditions:** target visitor is in `PendingApproval`.
- **Main flow:**
  1. Admin opens [`/admin/visitors/pending`](pages/cp/admin-visitors-pending.md).
  2. Clicks **View** OR **Approve** on the target row. Both open the
     same modal preloaded via
     `GET /admin/visitors/{id}/profile-for-approval` (D-124).
  3. Modal shows the full profile: identity + nationality + ID +
     mobile + interests + ID-document image inline if `HasIdImage`.
  4. Admin reads carefully — this is the friction point that prevents
     "approved without looking" (D-128).
  5. Clicks **Confirm and Approve**.
  6. Modal closes; server calls `POST /admin/visitors/{id}/approve` →
     `AccountState = Approved` + QR minted + audit `Admin.UserApproved`.
  7. Toast `Approved {email}`; grid reloads (row vanishes).
- **Alternate flow — Cancel after review:** at step 5, click **Cancel**.
  No state change; modal closes. Admin can proceed to Reject.
- **Exception — Stale row:** between list-load and Confirm, the row was
  already approved/rejected by another admin → server returns 404
  `NotFound`. Toast surfaces the bilingual fallback. Grid reloads.
- **Postcondition:** target's `AccountState = Approved`, QR badge live,
  the audit log records who approved + when + the snapshot the admin saw.

### UC-PRT-REPRINT-001 — Reprint a visitor's badge by QR id

- **Actor:** Administrator (print-desk operator).
- **Preconditions:** operator signed in; visitor's QR id known (scanned
  or hand-typed).
- **Main flow:**
  1. Operator opens [`/admin/print-bag`](pages/cp/admin-print-bag.md).
  2. Scans or types the 12-character QR id.
  3. Clicks **Search**.
  4. Server (`AdminApprovalReadService.LookupByQrIdAsync` — D-130)
     trims + uppercases the id, matches against `UserProfile.QrId`,
     returns the `AdminWalkInRegistrationResponse`.
  5. Page renders the badge (same markup as the walk-in success modal):
     colour stripe + Name + QR SVG (QRCoder, navy `#0B2545`) + QR id.
  6. Operator clicks **Print** → `window.print()` with the `@media print`
     CSS isolating `.simf-walkin-badge`.
  7. Operator clicks **Reset** → form clears + refocuses for the next scan.
- **Exception — Unknown QR id:** server returns 404 `NotFound` → toast
  `Admin.PrintBag.Error.NotFound`. No enumeration leak (same response
  shape as any mismatch).
- **Exception — Empty input:** client-side error `Admin.PrintBag.Error.Required`.
- **Postcondition:** a fresh badge has been printed. **No audit row** for
  the lookup itself today (D-130 caveat — D-109 interceptor fires only
  on writes; lookup is read).

### UC-2FA-RESET-001 — Administrator resets a user's 2FA

- **Actor:** Administrator (resetter).
- **Preconditions:** resetter Approved + Administrator; target user exists.
- **Main flow:**
  1. Resetter opens [`/admin/reset-2fa`](pages/cp/admin-reset-2fa.md).
  2. Types target email substring → picks the match.
  3. Clicks **Reset 2FA** → confirmation modal.
  4. Confirms.
  5. Server wipes target's authenticator + recovery codes + active
     sessions (D-041), emails target out-of-band, audits
     `Admin.UserTwoFactorReset`.
  6. Toast confirms.
- **Exception — Self-reset:** server rejects (self-reset must use
  `/account/profile → Reset my 2FA`).
- **Exception — No match:** "No user matches" toast; no API call fires.
- **Postcondition:** target's 2FA state is empty; their next sign-in
  routes through [`/account/totp-pairing`](pages/cp/account-totp-pairing.md)
  to pair fresh.

### UC-NTF-DISMISS-001 — Dismiss + bulk-dismiss notifications

- **Actor:** Any signed-in user.
- **Preconditions:** none beyond auth.
- **Main flow (per-row):**
  1. User opens [`/account/notifications`](pages/cp/account-notifications.md).
  2. Clicks the 🗑 icon on a row.
  3. Server `DELETE /account/api/notifications/{id}` returns 200; grid
     reloads.
- **Main flow (bulk):**
  1. User ticks N row checkboxes.
  2. Clicks toolbar **Delete**.
  3. Component loops the per-row delete N times (no bulk endpoint).
  4. Toast `Account.Notifications.BulkDismissed` with the count;
     grid reloads.
- **Alternate flow — Mark all read:** user clicks the standalone
  **Mark all as read** button below the grid → server
  `POST /account/api/notifications/read-all` flips every unread to read
  without dismissing. Bell count drops to 0.
- **Postcondition:** dismissed notifications are deleted; unread state
  is updated; bell unread count reflects the new state.

### UC-WEB-PRF-FILL-001 — Visitor fills profile, gets QR

- **Actor:** Visitor (Approved).
- **Preconditions:** visitor has an `Approved` account; lands on the
  Website after sign-in.
- **Main flow:**
  1. Visitor opens [`/account/profile`](pages/web/account-profile.md).
  2. **QR card** is visible at the top with their QR id + SVG.
  3. Fills Identity (Name EN/AR, DisplayName, DOB, place of birth) →
     Nationality + ID (Saudi or Iqama/Passport) → Contact (mobile, email)
     → Interests (up to 10 chips) → ID document upload (optional, ≤ 5 MB).
  4. Clicks **Save** → server validates → row updated → toast
     `Account.Profile.Saved`.
  5. (Optional) Clicks **Notifications** in the header → routes to
     [`/account/notifications`](pages/web/account-notifications.md).
- **Alternate flow — Pending account:** QR card is hidden; profile form
  is editable so the visitor can fill ahead of approval; on approval
  the QR appears next time they load the page (D-046a).
- **Exception — Bad Saudi ID format:** server validation surfaces
  bilingual error.
- **Postcondition:** profile is saved; QR is the operative access key for
  the venue gate.

### UC-AUTH-PENDING-001 — Pending admin sees holding page

- **Actor:** Self-registered administrator candidate whose account is in `PendingApproval`.
- **Preconditions:** account exists; password + TOTP are correct.
- **Main flow:**
  1. User completes `/login` + `/login/totp`.
  2. The server detects `AccountState = PendingApproval` and returns
     `AuthRequiresApproval` instead of token pair.
  3. Browser redirects to [`/auth/pending`](pages/cp/auth-pending.md).
  4. The page renders the holding-page copy + a Sign-out button.
- **Postcondition:** the user has read the holding page; their cookie
  holds the cookie-auth session but no JWT pair (so `[Authorize]` admin
  pages bounce back).

### UC-AUTH-REJECTED-001 — Rejected admin sees reason

- **Actor:** Administrator whose account is in `Rejected`.
- **Preconditions:** account exists; password + TOTP correct;
  `RejectionReason` populated when the admin was rejected (10–500 chars).
- **Main flow:**
  1. User completes /login + /login/totp.
  2. Server returns `AuthRequiresApproval` with `AccountState = Rejected`.
  3. Browser redirects to [`/auth/rejected`](pages/cp/auth-rejected.md).
  4. Page renders the verbatim bilingual `RejectionReason` + the
     `RejectedAt` timestamp + a Sign-out button.
- **Postcondition:** the user has seen the rejection reason; they can
  reach out to the SIMF team out-of-band.

### UC-VIS-WALKIN-NONSAUDI-001 — Walk-in non-Saudi visitor with Passport

- **Actor:** Administrator (registration-desk staff).
- **Preconditions:** identical to UC-VIS-WALKIN-001.
- **Main flow:** identical to UC-VIS-WALKIN-001 except at section 3:
  - desk toggles **Saudi** off → country picker appears + Iqama/Passport
    sub-picker.
  - desk picks country (e.g. "United Kingdom") and toggles **Passport**.
  - desk fills Passport number ≤ 20 chars.
  - The wizard hides the National-ID + Iqama fields so only one ID
    payload reaches the server.
- **Server-side:** `AdminWalkInRegistrationRequestValidator` enforces
  "non-Saudi-implies-Iqama-OR-Passport".
- **Postcondition:** visitor created with `NationalityCode = GB` (in this
  example) + `PassportNumber` set + `NationalId` and `IqamaNumber` null.
  All other steps + audit + QR mint identical to UC-VIS-WALKIN-001.

### UC-VPN-REJECT-001 — Reject pending visitor with reason

- **Actor:** Administrator.
- **Preconditions:** target visitor in `PendingApproval`.
- **Main flow:**
  1. Admin opens [`/admin/visitors/pending`](pages/cp/admin-visitors-pending.md).
  2. Clicks the **Reject** button on the target row.
  3. The reject reason modal opens with a `SimfTextarea` (helper:
     10–500 chars).
  4. Admin types a clear bilingual-friendly reason.
  5. Submit button enables only when the textarea length ∈ [10..500].
  6. Admin clicks **Reject**.
  7. Server: `POST /admin/visitors/{id}/reject` with the reason →
     `AccountState = Rejected`, `RejectionReason` + `RejectionReasonArabic`
     stored, audit `Admin.UserRejected`.
  8. Toast: `Rejected {email}`. Row vanishes from the queue.
- **Exception — Length gate:** typing < 10 or > 500 chars keeps the
  Submit button disabled (client-side).
- **Postcondition:** visitor sees the reason on
  [`/account/rejected`](pages/web/account-rejected.md) on next sign-in.

### UC-INT-CREATE-001 — Add an interest

- **Actor:** Administrator.
- **Preconditions:** signed in + Approved + Administrator role.
- **Main flow:**
  1. Open [`/admin/interests`](pages/cp/admin-interests.md) → click **+ Add**.
  2. Add modal opens (`InterestForm.razor`, Initial = null).
  3. Fill Name (English) 1–128 chars; Name (Arabic) 1–128 chars; Display
     order ≥ 0.
  4. Click **Create interest**.
  5. Server validates with `AdminCreateInterestRequestValidator`, persists,
     audits `Admin.InterestCreated`.
  6. Modal closes; grid reloads; toast `Admin.Interests.Created` with the
     new name.
- **Exception — Duplicate name:** 409 + `ErrorCodes.InterestNameNotUnique`
  → toast shows bilingual server message; modal stays open.
- **Exception — Bad input:** server-side FluentValidation → 400 with
  field-level errors mapped to the bilingual `*.Invalid` resx keys.
- **Postcondition:** the visitor-facing picker picks up the new interest
  on next load.

### UC-INT-EDIT-001 — Edit an interest

- **Actor:** Administrator.
- **Preconditions:** target interest exists.
- **Main flow:**
  1. Click the **Edit** icon on the target row.
  2. Edit modal opens (`InterestForm` Initial = the row), with an extra
     **Active** checkbox visible.
  3. Adjust any field.
  4. Click **Save changes**.
  5. Server validates with `AdminUpdateInterestRequestValidator`, persists,
     audits `Admin.InterestUpdated`.
  6. Modal closes; grid reloads; toast `Admin.Interests.Updated`.
- **Edge case — Deactivating via Edit:** untick the Active checkbox →
  same flow as Save; row pill flips to grey "Inactive".
- **Postcondition:** the row reflects the new values; the visitor picker
  reflects the new Active state on next load.

### UC-INT-DEACTIVATE-001 — Deactivate an interest

- **Actor:** Administrator.
- **Preconditions:** target interest exists + is Active.
- **Main flow:**
  1. Click the **Deactivate** trash icon on the row.
  2. Server: `DELETE /account/api/admin/interests/{id}` → calls
     `entity.Deactivate()` (sets `IsActive = false`), audits
     `Admin.InterestDeactivated`.
  3. Toast `Admin.Interests.Deactivated`.
  4. Row pill flips to grey "Inactive".
- **Postcondition:** existing visitor-interest links survive (soft-delete);
  new visitors don't see the deactivated interest in their picker.
  Reactivate via Edit if needed.

### UC-VPT-CREATE-001 — Add a Visitor profile-type with PageColor

- **Actor:** Administrator.
- **Preconditions:** signed in.
- **Main flow:**
  1. Open [`/admin/profile-types/visitor`](pages/cp/admin-profile-types-visitor.md)
     → click **+ Add**.
  2. Add modal opens (`ProfileTypeForm.razor`).
  3. Fill Name (English) + Name (Arabic) + PageColor + Active.
  4. PageColor uses the D-120 paired text + `<input type="color">` swatch:
     text is the source of truth (accepts `#rrggbb`, 3-digit hex,
     `var(--brand-blue)` CSS variables); the swatch is a visual shortcut
     that writes `#rrggbb` back.
  5. Click **Create**.
  6. Server validates (UserType pinned to Visitor by the route), persists,
     audits `Admin.ProfileTypeCreated`.
  7. Modal closes; grid reloads; the new tile appears in
     `/admin/visitors` walk-in wizard on next load.
- **Exception — Duplicate name within UserType:** 409 + bilingual message.
- **Postcondition:** the new profile-type is selectable in the walk-in
  wizard tile picker.

### UC-LOG-TAIL-001 — Tail a project log

- **Actor:** Administrator.
- **Preconditions:** at least one project log file exists under
  `{Storage:LogDirectory}`.
- **Main flow:**
  1. Open [`/admin/logs`](pages/cp/admin-logs.md).
  2. Pick **Project** (e.g. `Api`) → file select populates with the
     per-day log files (newest first).
  3. Pick **File** (e.g. `2026-05-28.log`) + **Lines** (e.g. 500).
  4. Optionally tick **Auto-refresh** (5-second poll).
  5. The monospaced `<pre>` block renders the tail.
  6. Auto-refresh fires every 5 s while the tab has focus.
- **Postcondition:** admin reads the latest log output; tab focus pause
  prevents wasted polling.

### UC-PRF-AVATAR-001 — Change my avatar (Cropper.Blazor flow)

- **Actor:** Any signed-in user (currently Administrator only — opens up
  when more roles ship).
- **Preconditions:** signed in.
- **Main flow:**
  1. Open [`/account/profile`](pages/cp/account-profile.md) →
     click **Change avatar**.
  2. Pick a PNG / JPEG / WebP ≤ 2 MB.
  3. `SimfImageCropperModal` opens (D-116 visuals + D-122 DI fix +
     D-123 cropperjs load order).
  4. Crop to 256 × 256.
  5. Click **Crop and save**.
  6. Server: `POST /account/api/profile/avatar` with the cropped image
     → stores under user's record, returns the new URL.
  7. Avatar in the profile page + the top header refresh to show the new
     image. No console error (cropper.destroy resolves correctly).
- **Exception — File > 2 MB:** server rejects with bilingual size error.
- **Exception — Wrong type:** server rejects with bilingual type error.
- **Postcondition:** the user's avatar is updated everywhere it renders
  via the cookie/JWT session.

### UC-WEB-NTF-001 — Visitor reads notifications inbox (Web)

- **Actor:** Visitor (Approved).
- **Preconditions:** signed in.
- **Main flow:**
  1. From [`/account/profile`](pages/web/account-profile.md), click the
     **Notifications** link in the header (added by D-132 to close the
     orphan-page gap).
  2. Page renders [`/account/notifications`](pages/web/account-notifications.md)
     with the visitor's notifications.
  3. Visitor may dismiss individual rows, or simply read.
- **Edge case — Empty inbox:** `SimfEmptyState` renders.
- **Postcondition:** read-state may have changed (visitor saw the inbox).

### UC-ROL-CREATE-001 — Create a custom role (D-134 Sprint A)

- **Actor:** Administrator.
- **Preconditions:** signed in + Approved + `Administrator` role.
- **Main flow:**
  1. Open [`/admin/roles`](pages/cp/admin-roles.md) → click **+ Add role**.
  2. Add modal opens (`RoleForm` Initial=null) with one Role-name field.
  3. Fill Name (1–64 chars) → click **Create role**.
  4. Server (`AdminRoleService.CreateAsync`) trims, length-checks,
     uniqueness-checks via `RoleManager.RoleExistsAsync`, creates with
     `IsBaseline = false`, audits `Role.Created`.
  5. Modal closes; grid reloads; success toast.
- **Exception — Duplicate name:** 409 `RoleNameDuplicate` (bilingual);
  modal stays open.
- **Exception — Bad length:** 400 `RoleInvalid` (bilingual).
- **Postcondition:** new `SimfRole` row with `IsBaseline = false`,
  zero users, zero permissions. Ready for the follow-up permission
  editor to grant rights and the follow-up user editor to assign users.

### UC-ROL-RENAME-001 — Rename a custom role

- **Actor:** Administrator.
- **Preconditions:** target custom role exists; `IsBaseline = false`.
- **Main flow:**
  1. Click Edit on the row.
  2. Edit modal opens prefilled.
  3. Change the name → **Save changes**.
  4. Server: rejects if `IsBaseline = true` (409 `RoleIsBaseline`);
     re-checks uniqueness; calls `RoleManager.SetRoleNameAsync` +
     `UpdateAsync`; audits `Role.Updated`.
  5. Modal closes; grid reloads; toast.
- **Alternate flow — Baseline target:** Edit modal renders a SimfAlert
  notice + Close button instead of the form. Server still guards in
  case a hand-crafted PUT arrives.
- **Postcondition:** row name updated everywhere (the `NormalizedName`
  + concurrency stamp are handled by Identity).

### UC-ROL-DELETE-001 — Delete a custom role

- **Actor:** Administrator.
- **Preconditions:** target row is custom (`IsBaseline = false`) AND no
  user currently holds it.
- **Main flow:**
  1. Click the Delete (trash) icon.
  2. Server: rejects baseline (409 `RoleIsBaseline`); counts holders;
     rejects if > 0 (409 `RoleInUse` with the count interpolated);
     cascade-deletes any `RolePermission` rows; calls
     `RoleManager.DeleteAsync`; audits `Role.Deleted`.
  3. Row vanishes; toast.
- **Exception — Baseline:** 409 `RoleIsBaseline`.
- **Exception — In use:** 409 `RoleInUse` with the bilingual holder
  count.
- **Postcondition:** `SimfRole` row + its `RolePermission` grants
  removed; no user references the role.

## 9. How to author the remaining entries

For each row in §7 that doesn't yet have a detailed entry above:

1. Open the implementing page doc (`docs/pages/{cp,web}/{slug}.md`).
2. Read §1 (Purpose), §5 (Data flow), §6 (Validation + error handling),
   and §7 (Edge cases + known limitations).
3. Author a §8 entry in this file following the actor / preconditions /
   main flow / alternate / exception / postcondition shape above.
4. Cross-link to the E2E catalogue entry in `docs/tests/e2e/`.
5. Cross-link from the page doc's §10 use-cases section back here.

The shape stays uniform so a reviewer can scan use cases by ID without
re-reading every page doc.

---

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 8).

End of document.
