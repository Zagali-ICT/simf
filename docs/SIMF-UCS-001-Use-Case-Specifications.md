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

End of document.
