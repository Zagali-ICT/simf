# Software Requirements Specification

| Field | Value |
|-------|-------|
| Document ID | SIMF-SRS-001 |
| Title | Software Requirements Specification |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Project Owner (MoD / RSNF representative) |
| Date issued | 2026-05-20 |
| Related documents | SIMF-CON-001, SIMF-RDR-001, SIMF-RPM-001, SIMF-DAT-001, SIMF-SAD-001, SIMF-API-001, SIMF-UCS-001 |

> Readable rendition of `SIMF-SRS-001`. This is the approved requirements baseline; every
> `FR-/NFR-/EIR-/OI-` statement below is kept **verbatim** from the original, so the binding
> "shall" wording and the stable identifiers are unchanged. Only the narrative introduction and
> overall-description prose have been lightly reworded for readability. The original remains the
> authoritative record.

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. Functional and non-functional requirements, drawn from the concept baseline and the closed decision gates. |

---

## 1. Introduction

### 1.1 Purpose

This document states what the SIMF system must do. It is the agreed list of functional and
non-functional requirements. Each requirement has an identifier, so it can be traced into design,
code and tests. The audience is the engineering team, the QA team, and the client reviewers who
sign off scope.

### 1.2 Scope

The requirements cover the whole SIMF system for SIMF 2026 — the backend and API, the Control
Panel, the public website, and the mobile app.

This document states the requirements only. It does not design the solution (SIMF-SAD-001), the
screens (SIMF-CPD-001 and the mobile mockup), or the data (SIMF-DAT-001). Each feature's detailed
acceptance criteria are elaborated in the per-feature design specifications (SIMF-FDS-NNN); this
document sits one level above them.

### 1.3 Sources

Every requirement traces to the concept baseline (SIMF-CON-001) and the closed decision register
(SIMF-RDR-001). Nothing here is invented. Where a point was deferred or proposed-for-review, the
requirement says so and references the open item.

### 1.4 Requirement identifiers

Functional requirements are `FR-<area><nn>`. Non-functional requirements are `NFR-<nn>`. An
identifier, once issued, is stable. Each requirement uses "shall" for a binding obligation.

## 2. Overall description

### 2.1 Product

SIMF is an integrated forum and exhibition management system for the Saudi International Maritime
Forum 2026. It runs the full event lifecycle: registration and security vetting, badge and access
control, the programme and live engagement, networking, media, and statistics. One backend serves
three surfaces — a public website, a Control Panel, and a mobile app.

### 2.2 Users

The user types are defined in SIMF-RPM-001. Internal: Admins and the organising teams. External:
Guests, Visitors (VIP, Normal and further sub-types), Exhibitors, the "Other" types (Media, Sponsor
and further types), Moderators, and Staff.

### 2.3 Constraints

- The technology stack is fixed (SIMF-SAD-001 section 2.2): .NET 10, FastEndpoints, Blazor with
  MudBlazor, Flutter, SQL Server 2022.
- The system is single-tenant and on-premises.
- The forum dates — 23–25 November 2026 — are immovable.
- The system must satisfy the NCA security standards (NFR section).
- Arabic is the primary language; Arabic and English are both fully supported.

### 2.4 Assumptions and dependencies

- The external UI/UX designer delivers the mobile app's visual design.
- The cognitive AI provider, the live-broadcast platform and the WhatsApp provider are deferred and
  reached through abstractions (decision D7).
- The legal text for the Terms & Conditions and the Policies is supplied by the client (decision
  D6).

## 3. Functional requirements

### 3.1 Accounts and authentication (FR-1xx)

- **FR-101** The system shall let a person create an account with an email
  address, a password and a password confirmation.
- **FR-102** The system shall send a six-digit verification code to the email
  and shall require the code to be entered before the account proceeds.
- **FR-103** The system shall let a user request a new verification code, and
  shall invalidate the previous one when it does.
- **FR-104** The system shall authenticate a user by email and password. It
  shall not offer Nafath or Face ID sign-in (decision: SIMF-RDR-001 context).
- **FR-105** The system shall require a TOTP second factor for every Control
  Panel (internal user) sign-in.
- **FR-106** The system shall issue a short-lived access token and a rotating
  refresh token on sign-in, per SIMF-API-001.
- **FR-107** The system shall provide a password-reset flow by email
  verification code, per SIMF-API-001 section 12.7.
- **FR-108** The system shall not validate the format of a mobile phone number
  on registration (decision: SIMF-RDR-001 context).

### 3.2 Registration and approval (FR-2xx)

- **FR-201** The system shall let a verified user complete a registration by
  choosing a registration type of **Visitor** or **Other**.
- **FR-202** The system shall collect the registration fields: the four-part
  Arabic name; the English name as in the passport; nationality; date of birth;
  place of birth.
- **FR-203** The system shall collect an identity number — the national ID for
  Saudis, or a passport number or Iqama number chosen by non-Saudis.
- **FR-204** The system shall collect a mobile number inside the Kingdom and,
  for overseas visitors, a mobile number outside the Kingdom.
- **FR-205** The system shall accept an ID image as an attachment, and shall
  support further attachment types added later.
- **FR-206** The system shall let the user select a venue **track / zone** after
  registration (decision D2).
- **FR-207** The system shall accept optional fields — job title and a personal
  photo.
- **FR-208** The system shall present the Terms & Conditions and require the
  user's consent before the registration is submitted.
- **FR-209** On submission the system shall set the request to "waiting for
  approval" and shall send the user a message with the contact details.
- **FR-210** The system shall show the user the status of their registration
  request through the review stages.
- **FR-211** The system shall let the Security team view registration requests,
  review the submitted data, and approve or reject each request.
- **FR-212** The system shall provide a select-all control so the Security team
  can approve requests in bulk.
- **FR-213** On approval the system shall let the Admin set the final user type
  and the user's permissions.
- **FR-214** On rejection the system shall record a reason and inform the user.
- **FR-215** The system shall let a person use the mobile app as a **Guest**
  without registering.
- **FR-216** The system shall let registration be opened and closed from the
  Control Panel, shall support an automatic close at the end of the last forum
  day, and shall allow a manual override at any time.
- **FR-217** The system shall support on-site registration and badge reprint for
  a person who arrives without a badge.

### 3.3 Badge and access control (FR-3xx)

- **FR-301** The system shall issue an entry badge with a unique reference
  number and a QR code to an approved user.
- **FR-302** The system shall colour the badge by the user's category.
- **FR-303** The system shall verify a badge QR or barcode at venue entry and
  record the entry.
- **FR-304** The system shall let one attendee scan another attendee's badge QR
  to save that person as a contact.
- **FR-305** The system shall record a user's arrival at a session hall by both
  a QR scan at the hall door and a GPS geofence around the hall, and shall hold
  an enter time and a leave time per session (decision D4).

### 3.4 Forum programme (FR-4xx)

- **FR-401** The system shall let the Scientific team manage the five themes /
  pillars and their sub-topics.
- **FR-402** The system shall let the Scientific team create and manage
  sessions, each with a title, description, theme, hall, category, start and end
  time, and speakers, in Arabic and English.
- **FR-403** The system shall mark a session as live or non-live.
- **FR-404** The system shall let halls be created with a defined seating
  capacity, and shall allow the capacity to be changed.
- **FR-405** The system shall hold a seat grid per hall and support seat
  assignment.
- **FR-406** The system shall manage speaker profiles — name, rank, bio,
  qualifications, training experience, awards, photo and country flag — and
  shall link speakers to sessions.
- **FR-407** The system shall hold the presentation files speakers present.
- **FR-408** The system shall present the agenda to attendees, filterable by
  day, with search, and shall let an attendee open a session's detail.
- **FR-409** The system shall let an attendee add a session to their device
  calendar and set a reminder.

### 3.5 Bookings and attendance (FR-5xx)

- **FR-501** The system shall let an attendee book a specific seat in a session.
- **FR-502** The system shall allow an attendee to hold any number of bookings
  provided the session times do not overlap.
- **FR-503** The system shall require every booking to be approved in the
  Control Panel before it is confirmed (decision D4).
- **FR-504** The system shall let an attendee cancel a booking any time before
  the session starts.
- **FR-505** The system shall show an attendee their assigned seat and a seat
  map for the hall.
- **FR-506** The system shall track session attendance from the hall-arrival
  records (FR-305).

### 3.6 Exhibition (FR-6xx)

- **FR-601** The system shall let an organisation register as an Exhibitor with
  the organisation's details.
- **FR-602** The system shall let the PR team review and approve an exhibitor
  and assign the booth in the same step (decision D3).
- **FR-603** The system shall maintain a booth directory — hall, booth number,
  logo, descriptor, contact, phone and email.
- **FR-604** The system shall maintain sponsors in three tiers — Strategic,
  Premium, Gold.
- **FR-605** The system shall present an interactive 3D venue map of halls,
  zones and booths, with navigation to a booth or to an assigned seat.

### 3.7 Engagement and live sessions (FR-7xx)

- **FR-701** The system shall stream a live session and shall show AI
  translation or live captions with a language choice.
- **FR-702** The system shall restrict the live stream to the Riyadh region.
- **FR-703** The system shall let an attendee send a question to the moderator
  during a session, addressed to the speaker or the host.
- **FR-704** The system shall open a session's questions only after the
  attendee is verified as arrived at the hall, and shall close them at session
  end (decision D5).
- **FR-705** The system shall let a moderator, for the sessions assigned to
  them, view, order, hide and put questions to the speaker.
- **FR-706** The system shall accept attendee comments on a session and shall
  pass each comment through an AI filter and then an admin review before it is
  shown (decision D5).
- **FR-707** The system shall hold an AI-rejected comment in a queue for an
  admin, who can approve or discard it; the system shall not discard a comment
  on the AI result alone.
- **FR-708** The system shall produce an AI-generated summary of a session —
  key points, recommendations and a transcript.

### 3.8 Networking and cognitive AI (FR-8xx)

- **FR-801** The system shall let a user choose interests, and shall maintain
  the interest list as a dynamic category.
- **FR-802** The system shall suggest other attendees to a user based on shared
  interests and shared sessions, with a match score.
- **FR-803** When a match score reaches 80% or more, the system shall send the
  user a session recommendation and a push notification.
- **FR-804** The system shall let an attendee send a one-to-one meeting request,
  and shall route it to the PR team for approval.
- **FR-805** The system shall provide a cognitive-AI assistant that answers
  attendee questions, backed by a FAQ knowledge base.
- **FR-806** The system shall organise the FAQ knowledge in two levels — groups,
  then entries within a group — and shall let the Technical team manage both
  from the Control Panel (decision D5).
- **FR-807** The system shall provide accessibility AI — sign-language and
  speech conversion, and live captions.
- **FR-808** The system shall reach the cognitive-AI provider through an
  abstraction so the provider can be set without a code change (decision D7).

### 3.9 Notifications (FR-9xx)

- **FR-901** The system shall deliver notifications over four channels —
  in-app, email, SMS and WhatsApp — through one notification abstraction.
- **FR-902** The system shall send registration and approval updates, session
  reminders, VIP invitations and meeting confirmations.
- **FR-903** The system shall send a booking-confirmed notification and shall
  send reminders when a session has started and the user has not attended or
  has not entered (decision D6 / SIMF-CON-001 section 7.7).
- **FR-904** The system shall let the channel mix for a notification type be set
  by configuration, not by code.

### 3.10 Media, news and archive (FR-10xx)

- **FR-1001** The system shall let the Marketing team manage a Media Center
  section — media coverage, social content and posts — from the Control Panel.
- **FR-1002** The system shall let the Marketing team manage news items, each
  with a category.
- **FR-1003** The system shall maintain a photo and video gallery and a media
  partners directory.
- **FR-1004** The system shall hold previous editions of the forum, each with a
  title, brief, sessions, place, time, image, video, previous speakers and
  statistics.
- **FR-1005** The system shall control the visibility of the current edition's
  archive from the Control Panel, so it appears only after the event ends.
- **FR-1006** The exact field set of media and news items shall be confirmed per
  decision D6; see open item OI-1.

### 3.11 Statistics (FR-11xx)

- **FR-1101** The system shall present per-day statistics — registrations,
  badges printed, registered VIP count, media badges printed, total check-ins.
- **FR-1102** The system shall present overall statistics — themes, topics,
  speakers, participating countries, total registrations and badges, attendance
  per day, total attendance, broadcast hours and total audience questions.
- **FR-1103** The system shall track attendee movement, dwell time and routes
  inside the venue from GPS presence, and reflect attendance live on the
  Control Panel dashboard.
- **FR-1104** The exact statistics list and the dashboards shall be confirmed
  per decision D6; see open item OI-1.

### 3.12 Control Panel and configuration (FR-12xx)

- **FR-1201** The system shall run the Control Panel on roles and permissions
  per SIMF-RPM-001; each user shall see only what their role permits.
- **FR-1202** The system shall let an Admin add roles and adjust permissions
  from the Control Panel.
- **FR-1203** The system shall let content — titles, texts, the in-app welcome
  message, banners, logos and images, section labels and page content — be
  edited from the Control Panel without a code change.
- **FR-1204** The system shall let dynamic categories — registration types,
  user sub-types, session categories, interests, and others — be added, hidden
  or deleted from the Control Panel, each with its own colour.
- **FR-1205** The system shall record changes and approvals in an operation log.
- **FR-1206** The system shall apply the visual identity in SIMF-VID-001 across
  the Control Panel, the website and the app.
- **FR-1207** The system shall present the Policies and the Terms & Conditions,
  using legal text supplied by the client (decision D6).

## 4. Non-functional requirements

- **NFR-01 Security.** The system shall meet the NCA Secure Application
  Development Standard and the controls it references — ECC-1:2018,
  CSCC-1:2019, the OWASP Top 10 and OWASP ASVS. Defence-in-depth is specified in
  SIMF-SAD-001 section 8 and SIMF-SES-001 section 12.
- **NFR-02 Authorisation.** Every endpoint and screen shall require a
  permission. Only sign-in, sign-up and password reset are anonymous.
- **NFR-03 Audit.** The system shall log security-relevant actions — sign-in,
  permission change, approval, configuration change — with enough context to
  reconstruct events.
- **NFR-04 Performance.** The system shall sustain the load profile in the
  technical requirements: a new registered user roughly every 30 seconds, and a
  pre-launch traffic test under real load.
- **NFR-05 Availability.** The system shall stay available through the three
  forum days, with health checks, a reverse proxy, and a retained last
  known-good build for rollback (SIMF-SAD-001 section 10).
- **NFR-06 Localisation.** The system shall be fully bilingual — Arabic
  (primary, RTL) and English (LTR). No user-facing string is hardcoded. Dates
  display as `dd-MM-yyyy` with Latin digits.
- **NFR-07 Usability and accessibility.** The mobile app shall provide the
  accessibility settings in the 41-screen scope. Colour shall never be the only
  signal of state. The interfaces shall be operable by keyboard where they run
  in a browser.
- **NFR-08 Compatibility.** The website and Control Panel shall run on current
  mainstream browsers; the mobile app shall run on supported Android and iOS
  versions.
- **NFR-09 Maintainability.** The system shall follow SIMF-SES-001 — DDD
  layering, zero-warning builds, tests per change, no duplication.
- **NFR-10 Configurability.** Content, categories, roles and permissions shall
  be changeable from the Control Panel without a release.
- **NFR-11 Data protection.** Personal data — identity numbers, contact
  details, attachments — shall be encrypted at rest and in transit, and handled
  per the NCA standard.

## 5. External interface requirements

- **EIR-01** The system shall expose its functions through the versioned API in
  SIMF-API-001; the website, Control Panel and app are its only clients.
- **EIR-02** The system shall integrate email, SMS and WhatsApp gateways for
  notifications, each behind the notification abstraction.
- **EIR-03** The system shall embed the live-broadcast platform's stream.
- **EIR-04** The system shall integrate a map and location service for the venue
  map and GPS presence.
- **EIR-05** The system shall expose a `/health` endpoint for monitoring.

## 6. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm the media and news field sets and the statistics list with the client (decision D6) | FR-1006, FR-1104 |
| OI-2 | Confirm the external providers as decision D7 closes | FR-808, EIR-02, EIR-03, EIR-04 |
| OI-3 | Per-feature acceptance criteria are elaborated in the SIMF-FDS-NNN specifications | Section 3 |
| OI-4 | Confirm document classification with the owner | Control block |

---

End of document.
