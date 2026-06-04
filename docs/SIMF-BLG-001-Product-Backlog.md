# Product Backlog

| Field | Value |
|-------|-------|
| Document ID | SIMF-BLG-001 |
| Title | Product Backlog |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Project Owner (MoD / RSNF representative) |
| Date issued | 2026-05-21 |
| Related documents | SIMF-PEP-001, SIMF-SRS-001, SIMF-TST-001, the SIMF-FDS series |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-21 | Engineering & Architecture Team | First issue. |

---

## 1. Purpose

This is the SIMF product backlog — the epics and user stories the team builds,
drawn from the twelve feature design specifications. It is written to be loaded
into Azure DevOps Boards and worked sprint by sprint.

## 2. Scope

The backlog covers a foundation epic, one epic per feature, and a hardening and
release epic. Each epic holds user stories. Tasks — the developer-level
breakdown of a story — are created under each story in Azure DevOps at sprint
planning; this document goes to the story level.

## 3. How the backlog works

- **Epic → story → task.** An epic is a feature; a story is a unit of value a
  team can deliver and demo; a task is the developer work under a story.
- **Story form.** Each story is written as *As a [role], I want [capability],
  so that [benefit]*, stated here in brief.
- **Acceptance.** A story's acceptance criteria are the relevant items in its
  feature design specification (the SIMF-FDS series); a story is done when those
  pass and it meets the definition of done (SIMF-SES-001 section 14).
- **Traceability.** Each story names the feature spec it comes from; through
  the spec it traces to the requirements (`FR-`) and use cases (`UC-`).
- **Estimation and priority** are set at backlog refinement; the sprint column
  reflects the plan in SIMF-PEP-001 section 5.
- **Every feature story is delivered as API + Backend + Control Panel** (and a
  Mobile App slice where applicable), per SIMF-PGP-001.

## 4. Epics

| Epic | Title | Feature spec | Sprint |
|------|-------|--------------|--------|
| EP-00 | Foundation & DevOps | SES / SAD / API / OPS | 1 |
| EP-01 | Authentication & Login | SIMF-FDS-001 | 1 |
| EP-02 | Registration & Approval | SIMF-FDS-002 | 2 |
| EP-03 | Badge & Access Control | SIMF-FDS-003 | 2 |
| EP-04 | Forum Programme | SIMF-FDS-004 | 3 |
| EP-05 | Bookings & Attendance | SIMF-FDS-005 | 4 |
| EP-06 | Exhibition | SIMF-FDS-006 | 4 |
| EP-07 | Engagement | SIMF-FDS-007 | 5 |
| EP-08 | Networking & Cognitive AI | SIMF-FDS-008 | 6 |
| EP-09 | Notifications | SIMF-FDS-009 | 6 |
| EP-10 | Media, News & Archive | SIMF-FDS-010 | 7 |
| EP-11 | Statistics & Dashboards | SIMF-FDS-011 | 7 |
| EP-12 | Control Panel Configuration | SIMF-FDS-012 | 7 |
| EP-13 | Hardening, Security & Release | OPS / TST | 8–9 |

## 5. The backlog

### EP-00 — Foundation & DevOps (Sprint 1)

| ID | Story | Traces to |
|----|-------|-----------|
| US-001 | As an engineer, I want the solution scaffold and repository structure, so that the team builds in one consistent shape | SES-001, SAD-001 |
| US-002 | As a DevOps engineer, I want Azure DevOps, the CI/CD pipeline and the four environments, so that builds promote test-gated | OPS-001 |
| US-003 | As an engineer, I want the `ApiResult<T>` envelope, the error model and the standard middleware, so that every endpoint behaves the same | API-001 |
| US-004 | As an engineer, I want the localisation baseline (Arabic/English, RTL/LTR), so that every surface is bilingual from the start | SAD-001, MAA-001 |
| US-005 | As an engineer, I want `theme.tokens.css` built from the visual identity, so that the Control Panel follows the brand | VID-001, CPD-001 |
| US-006 | As an engineer, I want the Control Panel shell — layout, navigation, theming, multi-theme — so that every CP screen sits in one frame | CPD-001 |
| US-007 | As an engineer, I want the Flutter app base — structure, routing, networking, theming — so that screens can be built and integrated | MAA-001 |

### EP-01 — Authentication & Login (Sprint 1)

| ID | Story | Traces to |
|----|-------|-----------|
| US-010 | As a guest, I want to create an account with email and password, so that I can begin registering | FDS-001 §5.1 |
| US-011 | As a new user, I want to verify my email with a code, so that my account is confirmed | FDS-001 §5.2–5.3 |
| US-012 | As a registered user, I want to sign in with email and password, so that I can use the system | FDS-001 §5.4–5.5 |
| US-013 | As an internal user, I want a TOTP second factor at sign-in, so that the Control Panel is protected | FDS-001 §5.6 |
| US-014 | As a signed-in user, I want my session to refresh, so that I stay signed in without re-entering my password | FDS-001 §5.7–5.8 |
| US-015 | As a user, I want to sign out, so that my session ends | FDS-001 §5.9 |
| US-016 | As a user, I want to reset a forgotten password, so that I can recover my account | FDS-001 §5.10 |

### EP-02 — Registration & Approval (Sprint 2)

| ID | Story | Traces to |
|----|-------|-----------|
| US-020 | As a verified user, I want to complete a Visitor or "Other" registration, so that I can request to attend | FDS-002 §5.1–5.5, §5.8–5.9 |
| US-021 | As an exhibitor, I want to register my organisation, so that we can exhibit | FDS-002 §5.7 |
| US-022 | As a user, I want photo identity verification, with the alternative for the documented exception, so that my identity is confirmed | FDS-002 §5.6 |
| US-023 | As an applicant, I want to track my registration status, so that I know where my request stands | FDS-002 §5.10 |
| US-024 | As the Security team, I want to review and approve or reject registrations, including in bulk, so that attendees are vetted | FDS-002 §6.1 |
| US-025 | As the PR team, I want to approve an exhibitor and assign a booth, so that the exhibitor is confirmed | FDS-002 §6.2 |
| US-026 | As the system, I want to issue a badge on approval, so that the attendee has an entry badge | FDS-002 §6.4 |
| US-027 | As Staff, I want on-site registration and badge reprint, so that an attendee without a badge can be served | FDS-002 §7 |
| US-028 | As an Admin, I want to open and close registration, so that registration runs only when intended | FDS-002 §8 |
| US-029 | As an Administrator, I want to create internal users who enrol TOTP, so that the organising teams can sign in | FDS-002 §9 |

### EP-03 — Badge & Access Control (Sprint 2)

| ID | Story | Traces to |
|----|-------|-----------|
| US-030 | As an attendee, I want to see my badge and QR, so that I can enter the venue | FDS-003 §5.1 |
| US-031 | As Staff, I want to verify a badge at venue entry, so that only valid badge-holders enter | FDS-003 §5.2 |
| US-032 | As an attendee, I want to scan another attendee's badge, so that I can save them as a contact | FDS-003 §5.3 |
| US-033 | As the system, I want to record hall arrival by QR scan and GPS geofence with enter/leave times, so that attendance is tracked | FDS-003 §5.4 |
| US-034 | As the system, I want hall-arrival records available to statistics and the engagement gate, so that downstream features can use them | FDS-003 §5.5 |

### EP-04 — Forum Programme (Sprint 3)

| ID | Story | Traces to |
|----|-------|-----------|
| US-040 | As the Scientific team, I want to manage themes and sub-topics, so that the programme is structured | FDS-004 §5.1 |
| US-041 | As the Scientific team, I want to manage halls and their seating, so that sessions have venues | FDS-004 §5.2 |
| US-042 | As the Scientific team, I want to manage speaker profiles and presentations, so that speakers are presented | FDS-004 §5.3 |
| US-043 | As the Scientific team, I want to create and manage sessions, live or non-live, so that the programme is built | FDS-004 §5.4 |
| US-044 | As an attendee, I want to browse the agenda by day and search it, so that I can plan my time | FDS-004 §6.1 |
| US-045 | As an attendee, I want a session detail with speakers, so that I know what a session covers | FDS-004 §6.2 |
| US-046 | As an attendee, I want to add a session to my calendar and set a reminder, so that I do not miss it | FDS-004 §6.3 |

### EP-05 — Bookings & Attendance (Sprint 4)

| ID | Story | Traces to |
|----|-------|-----------|
| US-050 | As an attendee, I want to book a seat in a session, so that I have a place | FDS-005 §5.1 |
| US-051 | As the system, I want to block overlapping bookings, so that an attendee is not double-booked | FDS-005 §5.1 |
| US-052 | As the PR team, I want to approve or reject bookings, so that bookings are controlled | FDS-005 §5.2 |
| US-053 | As an attendee, I want to cancel a booking before the session starts, so that I can change my plan | FDS-005 §5.3 |
| US-054 | As an attendee, I want to see my seat on the hall map, so that I can find it | FDS-005 §5.4 |
| US-055 | As the system, I want session attendance from the hall-arrival records, so that attendance is reported | FDS-005 §5.6 |

### EP-06 — Exhibition (Sprint 4)

| ID | Story | Traces to |
|----|-------|-----------|
| US-060 | As the PR team, I want to manage the booth directory, so that exhibitors are listed | FDS-006 §5.1 |
| US-061 | As an attendee, I want to browse and search booths and get directions, so that I can find an exhibitor | FDS-006 §5.1 |
| US-062 | As the PR team, I want to manage sponsors and their tiers, so that sponsors are presented | FDS-006 §5.2 |
| US-063 | As the Logistics team, I want to manage the venue map nodes, so that the venue is mapped | FDS-006 §5.3 |
| US-064 | As an attendee, I want an interactive 3D venue map with navigation, so that I can move around the venue | FDS-006 §5.3 |

### EP-07 — Engagement (Sprint 5)

| ID | Story | Traces to |
|----|-------|-----------|
| US-070 | As the Scientific team, I want to start and stop a live broadcast, so that a session is streamed | FDS-007 §5.1 |
| US-071 | As an attendee, I want to watch the live broadcast with captions and a language choice, within the region restriction, so that I can follow a session | FDS-007 §5.1 |
| US-072 | As an attendee, I want to ask a question once I have arrived at the hall, so that I can take part | FDS-007 §5.2 |
| US-073 | As a moderator, I want to manage the questions for my assigned sessions, so that questions are curated | FDS-007 §5.3 |
| US-074 | As an attendee, I want to comment on a session, so that I can engage | FDS-007 §5.4 |
| US-075 | As an admin, I want to moderate comments after the AI filter, so that only suitable comments are shown | FDS-007 §5.4 |

### EP-08 — Networking & Cognitive AI (Sprint 6)

| ID | Story | Traces to |
|----|-------|-----------|
| US-080 | As an attendee, I want to choose interests, so that I get relevant suggestions | FDS-008 §5.1 |
| US-081 | As an attendee, I want to be matched with people like me, so that I can network | FDS-008 §5.2 |
| US-082 | As an attendee, I want to send a one-to-one meeting request, so that I can meet someone | FDS-008 §5.3 |
| US-083 | As the PR team, I want to approve meeting requests, so that meetings are mediated | FDS-008 §5.3 |
| US-084 | As an attendee, I want an AI assistant backed by the two-level FAQ, so that I get answers about the forum | FDS-008 §5.4 |
| US-085 | As the Technical team, I want to manage the FAQ groups and entries and the AI settings, so that the assistant is maintained | FDS-008 §5.4, §5.7 |
| US-086 | As an attendee, I want an AI session summary, so that I can catch up on a session | FDS-008 §5.5 |
| US-087 | As an attendee with accessibility needs, I want the accessibility AI aids, so that I can take part | FDS-008 §5.6 |

### EP-09 — Notifications (Sprint 6)

| ID | Story | Traces to |
|----|-------|-----------|
| US-090 | As an engineer, I want the notification abstraction and the four channel adapters, so that features can notify users | FDS-009 §5.1–5.2 |
| US-091 | As an attendee, I want an in-app inbox with unread counts, so that I see my notifications | FDS-009 §5.5 |
| US-092 | As the system, I want to send the registration, session, VIP and meeting notifications, so that users are kept informed | FDS-009 §5.3 |
| US-093 | As the system, I want to send the booking and attendance reminders, so that attendees are prompted | FDS-009 §5.6 |
| US-094 | As an Admin, I want the channel mix per notification type to be configuration, so that channels change without a release | FDS-009 §5.4 |

### EP-10 — Media, News & Archive (Sprint 7)

| ID | Story | Traces to |
|----|-------|-----------|
| US-100 | As the Marketing team, I want to manage the Media Center content and partners, so that media coverage is published | FDS-010 §5.1 |
| US-101 | As the Marketing team, I want to manage news items with categories, so that news is published | FDS-010 §5.2 |
| US-102 | As the Marketing team, I want to manage previous editions with their stats and content, so that the archive is maintained | FDS-010 §5.3 |
| US-103 | As an Admin, I want to control archive visibility, so that the current edition appears only after the event | FDS-010 §5.3 |
| US-104 | As an attendee, I want to browse news, the gallery, partners and the archive, so that I can follow the forum | FDS-010 §7 |

### EP-11 — Statistics & Dashboards (Sprint 7)

| ID | Story | Traces to |
|----|-------|-----------|
| US-110 | As an organiser, I want the per-day statistics, so that I can track each forum day | FDS-011 §5.2 |
| US-111 | As an organiser, I want the overall statistics, so that I can see the forum at a glance | FDS-011 §5.3 |
| US-112 | As an organiser, I want live attendance on the dashboard, so that I see who is in the venue now | FDS-011 §5.4 |
| US-113 | As an organiser, I want GPS-presence movement views, so that I understand attendee flow | FDS-011 §5.5 |

### EP-12 — Control Panel Configuration (Sprint 7)

| ID | Story | Traces to |
|----|-------|-----------|
| US-120 | As a content user, I want to manage dynamic content blocks, so that content changes without a release | FDS-012 §5.1 |
| US-121 | As a content user, I want to manage categories, labels and colours, so that the dynamic lists are maintained | FDS-012 §5.2 |
| US-122 | As a content user, I want to manage venue tracks, so that the registration track list is current | FDS-012 §5.3 |
| US-123 | As an Administrator, I want to manage roles and permissions with the page-and-action model, so that access is controlled | RPM-001 §8, §12 |
| US-124 | As an organiser, I want to view the operation log, so that every change is auditable | FDS-012 §5.6 |

### EP-13 — Hardening, Security & Release (Sprints 8–9)

| ID | Story | Traces to |
|----|-------|-----------|
| US-130 | As the team, I want the mobile-app visual design applied, so that the app matches the designer's design | MAA-001 §12 |
| US-131 | As the QA team, I want the continuous-testing pass completed, so that defects are found and fixed | TST-001 |
| US-132 | As the Security team, I want the penetration test and the MoD cyber-centre review passed, so that the system is cleared | OPS-001 §12, TST-001 §9 |
| US-133 | As the team, I want the load and traffic tests passed, so that the system is proven under load | OPS-001 §11 |
| US-134 | As the DevOps engineer, I want the live environment ready and the apps published to the stores, so that the system can go live | OPS-001 §4, §8 |
| US-135 | As the Project Owner, I want UAT signed off, so that the system is accepted | TST-001 §12 |

## 6. Sprint allocation summary

| Sprint | Epics |
|--------|-------|
| 1 | EP-00, EP-01 |
| 2 | EP-02, EP-03 |
| 3 | EP-04 |
| 4 | EP-05, EP-06 |
| 5 | EP-07 |
| 6 | EP-08, EP-09 |
| 7 | EP-10, EP-11, EP-12 |
| 8–9 | EP-13 |

The Backend and APIs run continuously across all sprints; the continuous
testing runs across Sprints 6–8 (SIMF-PEP-001 section 5).

## 7. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Story estimation and final priority at backlog refinement | Section 5 |
| OI-2 | Task breakdown under each story, created in Azure DevOps at sprint planning | Section 2 |
| OI-3 | Confirm the backlog against the client's review of the documents before Sprint 1 | All |
| OI-4 | Confirm document classification with the owner | Control block |

---

End of document.
