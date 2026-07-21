# Requirements Decision Register

| Field | Value |
|-------|-------|
| Document ID | SIMF-RDR-001 |
| Title | Requirements Decision Register |
| Version | 1.5 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Solution Architect |
| Approver | Project Owner (MoD / RSNF representative) |
| Date issued | 2026-05-20 |
| Related documents | SIMF-PGP-001, SIMF-CON-001, SIMF-SRS-001, SIMF-RPM-001, SIMF-UCS-001, SIMF-DAT-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. Decisions D1–D9 opened. |
| 1.1 | 2026-05-20 | Engineering & Architecture Team | Decisions recorded from the client review: D1–D7 and D9 decided, D8 deferred. Stage 1 requirements closure complete. |
| 1.2 | 2026-05-20 | Engineering & Architecture Team | Opened decisions D10–D12 from the SIMF-RPM-001 permission-matrix review. |
| 1.3 | 2026-05-20 | Engineering & Architecture Team | Decisions D10–D12 recorded from the client review. |
| 1.4 | 2026-05-20 | Engineering & Architecture Team | D1 amended on client instruction: all roles are dynamic and Administrator-managed, superseding the earlier "fixed baseline roles" answer. |
| 1.5 | 2026-07-19 | Apexium | D4 and D11 amended to match the as-built system: attendee seat reservations are confirmed immediately with no Control Panel approval and held provisionally until gate check-in confirms them; the Control Panel booking-approval queue is retained but dormant (always empty). |

---

## 1. Purpose

This register holds the requirement decisions that the SIMF requirements,
roles, use-case and data-model documents cannot be written without. Each
decision is a question, or a set of questions, that only the client and owner
can answer. Until a decision is answered, the work that depends on it stays
blocked, and nothing is filled in by assumption.

The register is the working instrument for Stage 1 of the programme plan
(SIMF-PGP-001) — requirements closure. When every decision here is marked
Decided, Stage 1 is complete.

## 2. How to use this register

Each decision has a record in section 5. To answer one:

1. Read the background and the questions.
2. Where the record lists options, either pick one or describe a different
   answer. The options are there to make the trade-off visible, not to limit
   the answer.
3. Write the answer in the **Decision** field of the record.
4. Set the record's **Status** to Decided and fill in **Decided by** and
   **Date**.

Decisions can be answered one at a time. Each one that closes unblocks a
specific part of the documentation, listed in the record and summarised in
section 6. A partial answer that closes part of a decision is recorded, and the
rest stays open.

A decision, once Decided, is treated as a baseline. Changing it later follows
the freeze governance in SIMF-PGP-001 and SIMF-SES-001.

## 3. Status

| Status | Meaning |
|--------|---------|
| Open | Awaiting the client's answer. |
| Partially decided | Some questions answered, some still open. |
| Decided | Fully answered; the dependent work can proceed. |

## 4. Decision summary

| ID | Decision | Status | Blocks |
|----|----------|--------|--------|
| D1 | User types, permissions and screens | Decided | SIMF-RPM-001, SIMF-SRS-001, SIMF-UCS-001 |
| D2 | Meaning of "direction / track" | Decided | SIMF-SRS-001, SIMF-DAT-001 |
| D3 | Exhibitor, moderator and staff workflows | Decided | SIMF-SRS-001, SIMF-UCS-001 |
| D4 | Booking and attendance; hall-arrival verification | Decided | SIMF-SRS-001, SIMF-DAT-001 |
| D5 | Session questions and the cognitive AI rules | Decided | SIMF-SRS-001, SIMF-DAT-001 |
| D6 | Media coverage, news, statistics, legal text, renamed sections | Decided | SIMF-SRS-001, SIMF-CPD-001 |
| D7 | External providers — AI, live broadcast, WhatsApp | Decided | SIMF-SAD-001 integration detail |
| D8 | SQL Server 2022 edition and licensing | Decided (deferred) | SIMF-OPS-001 |
| D9 | Document classification scheme | Decided | Every document's control block |
| D10 | Exhibitor approval — one stage or two | Decided | SIMF-RPM-001, SIMF-UCS-001 |
| D11 | Permission ownership — bookings, live, moderation, configuration | Decided | SIMF-RPM-001 |
| D12 | PR's registration-view scope | Decided | SIMF-RPM-001 |

## 5. Decision records

### D1 — User types, permissions and screens

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 |
| Blocks | SIMF-RPM-001 (whole document), SIMF-SRS-001 (access rules), SIMF-UCS-001 (actors) |

**Background.** The system has two user models. In the general system (website
and Control Panel): Admin, Visitor, Exhibitor, Staff, Other. Visitors divide
into VIP, Normal, and more later. In the mobile app: Guest, Visitor, Exhibitor,
Moderator, Staff. Each type was described as having its own screens, permissions
and use cases. The roles and permissions document cannot be written until those
are defined.

**Questions.**

1. For each general-system type (Admin, Visitor, Exhibitor, Staff, Other): what
   can it do, and which Control Panel or website areas can it reach?
2. For each mobile-app type (Guest, Visitor, Exhibitor, Moderator, Staff): which
   of the 41 screens can it open, and what can it do on each?
3. What is the "Other" user type for? Give an example of who registers as
   "Other".
4. What are the full Visitor sub-types? VIP and Normal are known; what else?
5. Are the Control Panel teams (Security, PR, Technical, Scientific, Logistics,
   Marketing) fixed roles, or just examples of roles an Admin creates? If fixed,
   what is each team's permission set?

**Question 6 — may an unapproved user sign in?** A registered user is "waiting
for approval" until an Admin approves them. Pick one.

| Option | Behaviour | Pros | Cons |
|--------|-----------|------|------|
| A | Cannot sign in at all until approved | Simplest; no half-state to design | The user cannot see their own request status in the app |
| B | Can sign in, but only sees their registration status and guest-level content | The user can track approval; matches the mockup's "registration status" screen | A second access level to build and test |
| C | Can sign in with full access immediately; approval only affects on-site entry | Smoothest user experience | Weakest control; an unvetted user is inside the app |

**Decision.** Question 6 — Option B. A pending (unapproved) user may sign in and
is shown their registration status plus guest-level content; the rest of the app
unlocks once an Admin approves them. Question 5 — **all roles are dynamic**. The system
seeds a set of starter roles (Security, PR, Technical, Scientific, Logistics,
Marketing) as ordinary, editable data; the Administrator creates, edits, deletes
and assigns every role, and sets each role's permissions, from the Control
Panel. *(Amended 2026-05-20 on client instruction, superseding the earlier
"fixed baseline roles with preset permissions" answer.)* Registration types — at
registration the user picks **Visitor** or **Other**: "Other" covers **Media,
Sponsor, and any future type added in the Control Panel**, and Visitor sub-types
are **VIP and Normal plus any added in the Control Panel**, each type carrying
its own colour. The detailed per-type permission and screen matrix is drafted in
SIMF-RPM-001 and reviewed by the client there.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D2 — Meaning of "direction / track" (التوجه / المسار)

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 |
| Blocks | SIMF-SRS-001 (registration), SIMF-DAT-001 (registration model) |

**Background.** The 2026-05-20 meeting said that after registration the user
picks a "direction / track". The term is not defined. The registration data
model cannot be drawn until it is.

**Question.** What is the "direction / track" the user selects, and what are the
options to choose from?

To make the question concrete, here are four readings the term could carry. The
client confirms one, or describes the real meaning.

| Reading | "Direction / track" would be... |
|---------|-------------------------------|
| A | A thematic interest, aligned to the five forum pillars, used later for matchmaking and recommendations |
| B | A physical track or zone at the venue the visitor is assigned to |
| C | A delegation or group the visitor belongs to |
| D | A registration sub-track that changes which fields and steps the rest of the form shows |

**Decision.** The "direction / track" is a **physical track or zone at the
venue**. After registration the user is associated with a venue track or zone.
The set of tracks/zones is maintained in the Control Panel as dynamic
configuration.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D3 — Exhibitor, moderator and staff workflows

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 |
| Blocks | SIMF-SRS-001, SIMF-UCS-001 |

**Background.** Three roles have a workflow that the meeting named but did not
describe. Each needs its steps written out before its use cases can be.

**Questions.**

1. **Exhibitor approval cycle.** From an exhibitor registering to an approved
   booth: what are the steps? Who reviews an exhibitor? Is a booth assigned
   during approval or separately? Can an exhibitor add companions or staff under
   their organisation, and does that need separate approval?
2. **Moderator workflow.** What does a moderator do before a session (preparing
   questions), during it (putting questions to the speaker, hiding or reordering
   them), and after it? Is a moderator assigned to specific sessions, or to all?
3. **Staff workflow.** What do Staff do in the mobile app, and what do they do
   in the Control Panel? Are field-team activities — badge scanning at entry,
   on-site registration — Staff activities? Is "Staff" one role or several?

**Decision.**
Exhibitor approval — the **PR team** reviews and approves an exhibitor, and the
**booth is assigned during that same approval step**.
Moderator — a moderator is **assigned to specific sessions** and manages the
questions only for the sessions assigned to them.
Staff — Staff perform **field operations only**: badge scanning at entry,
on-site registration, and hall-door check-in. They work in the mobile app and
do not have Control Panel access.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D4 — Booking and attendance; hall-arrival verification

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 |
| Blocks | SIMF-SRS-001, SIMF-DAT-001 |

**Background.** The meeting referred to a "booking confirmed" notification and
reminders for "session started, you did not attend / did not enter". So there is
a booking concept and an attendance concept, neither yet defined.

**Question 1 — what does a user book?**

| Option | The user books... | Notes |
|--------|-------------------|-------|
| A | A seat in a specific session (a seat in a hall) | Matches the seat-map screens in the mockup |
| B | A place in a session, without a specific seat | Simpler; no seat allocation |
| C | Both — sessions, and separately one-to-one meetings | Two booking types to model |

**Question 2 — booking rules.** How many sessions can one user book? Can a
booking be cancelled, and until when? Is a session capped by the hall's seat
count, and what happens when it is full?

**Question 3 — how is hall arrival verified?** This feeds both the attendance
statistics and the rule that opens session questions (D5).

| Option | Verification | Pros | Cons |
|--------|--------------|------|------|
| A | A QR scan at the hall door | Precise; a clear arrival event; reuses the badge QR | Needs a person or a device at every hall door |
| B | A GPS geofence around the hall | No staff or hardware at the door | Less precise indoors; depends on the user granting location |
| C | Both — QR scan as the record, GPS as a backup | Most reliable | Most to build and test |

**Decision.** A user books **a specific seat in a session** (a seat in a hall).
Hall arrival is verified by **both a QR scan at the hall door and a GPS geofence
around the hall**: the geofence detects that a user has entered even when they
did not scan at the door, and the system records an **enter time and a leave
time** per session from it. Booking rules — a user may book **any number of
sessions as long as their times do not overlap**; a booking may be **cancelled
any time before the session starts**. *(Amended 2026-07-19: as built, an
attendee seat reservation is **confirmed immediately, with no Control Panel
approval step**. The reservation is a provisional hold until the attendee
**checks in at the hall gate** (a staff QR scan), which confirms the seat; a
pre-start sweep releases any hold not checked in shortly before the session
starts. The earlier "every booking must be approved in the Control Panel" gate
is **retained but dormant** — nothing creates a pending booking, so the Control
Panel approval queue is always empty.)*

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D5 — Session questions and the cognitive AI rules

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 |
| Blocks | SIMF-SRS-001, SIMF-DAT-001 |

**Background.** Questions to the moderator open and close around a session's
time. Comments pass an AI filter and then an admin review. The cognitive AI has
"two setting levels". The exact rules are needed.

**Question 1 — when do a session's questions open?** The meeting said "on
arrival at the hall" and "5 minutes before the session", and "close at session
end".

| Option | Questions open when... | Notes |
|--------|------------------------|-------|
| A | The user has been verified as arrived at the hall (D4) | Ties questions to physical presence |
| B | It is 5 minutes before the session starts, for anyone | Simple; time only |
| C | Either condition is met — arrival, or 5 minutes before | Most permissive |

Confirm also: do questions close exactly at the scheduled end, or when the
moderator closes the session?

**Question 2 — AI comment filtering.** What does the AI filter check for —
profanity, off-topic content, language, personal data, something else? When the
AI rejects a comment, what happens: is it dropped silently, held for the admin
to see, or returned to the author? When the AI passes a comment, does it still
always go to an admin, or can the AI approve low-risk comments on its own?

**Question 3 — the two AI setting levels.** The meeting said the cognitive AI is
managed from the Control Panel with "two levels" of settings. What are the two
levels, and what does each control? For example, is one level a global on/off
and feature toggle, and the other a per-session or per-feature tuning? Describe
each level.

**Decision.**
Question 1 — Option A. A session's questions open **only after the user is
verified as arrived at the hall** (D4), and they close at session end.
Question 2 — a comment rejected by the AI filter is **held in a queue for an
admin to review**; the admin can still approve or discard it. Every comment
passes through the AI filter and then admin review; the AI does not approve
comments on its own.
Question 3 — the "two levels" are not on/off toggles. They describe how the
cognitive AI knowledge (the FAQ and chat content) is **organised in two
levels**: Level 1 is the group or type — for example, ask about a booth, ask
about events, ask about the launch — and Level 2 is the FAQ entries within each
group. Grouping the FAQ this way lets the assistant search and answer chat
questions more accurately. Both levels are maintained in the Control Panel.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D6 — Media coverage, news, statistics, legal text, renamed sections

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 |
| Blocks | SIMF-SRS-001, SIMF-CPD-001 |

**Background.** Several content and reporting areas were named in the meeting
without their detail.

**Questions.**

1. **Media coverage.** The section covers social media and posts. What content
   types are there (a post, a video, a photo album, a media-partner entry)? What
   fields does each have? Where does the content come from — entered by hand in
   the Control Panel, or pulled from social platforms?
2. **News.** What fields does a news item have (title, body, image, date,
   category)? Are the categories from the mockup — coverage, announcement,
   opening, cooperation — the right set?
3. **Statistics.** SIMF-CON-001 section 7.10 lists the figures seen in the
   source documents. Confirm that list, add anything missing, and say which
   figures appear on which Control Panel dashboard.
4. **Renamed sections.** What are the new names for "Media Coverage" and
   "Profile"?
5. **Legal text.** Who provides the wording for the Terms & Conditions and the
   Policies, and by when? The team builds the screens; it does not write the
   legal content.

**Decision.** Section renames — "Media Coverage" becomes **"Media Center"** and
"Profile" becomes **"My Account"**. Legal text — the **client / owner (MoD /
RSNF) provides** the wording for the Terms & Conditions and the Policies; the
team builds the screens only. The media-coverage content types, the news-item
fields, and the statistics list are proposed by the team in SIMF-SRS-001 and
SIMF-CPD-001 for the client to review, rather than being decided here.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D7 — External providers

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 |
| Blocks | SIMF-SAD-001 integration detail (section 9), parts of SIMF-OPS-001 |

**Background.** Three external services are reached through abstractions in the
architecture, so the build is not blocked waiting for them. But each adapter,
and the operational setup, needs the provider named before the relevant feature
is finished.

**Questions.**

1. **Cognitive AI provider.** Google Gemini was proposed and is not approved.
   Which provider is used, or is an on-premises or sovereign option required for
   a Ministry of Defense system? This also affects what data the AI may process.
2. **Live broadcast platform.** Which platform carries the live session video?
   The geographic restriction (the Riyadh-region rule) must be supportable on
   the chosen platform.
3. **WhatsApp.** Which WhatsApp Business provider is used for notifications, and
   is a WhatsApp Business account already held?

**Decision.** All three external providers — the Cognitive AI provider, the
live-broadcast platform, and the WhatsApp provider — are **deferred**. The
architecture (SIMF-SAD-001 section 9) already isolates each behind an
abstraction, so the build is not blocked. Each provider is chosen before its
feature's integration work begins; that choice is a configuration value and one
adapter, not a redesign.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D8 — SQL Server 2022 edition and licensing

| | |
|--|--|
| Status | Decided (deferred) |
| Raised | 2026-05-20 |
| Blocks | SIMF-OPS-001, environment setup |

**Background.** The database is SQL Server 2022. The edition (Standard or
Enterprise) affects available features and the licence.

**Question.** Which SQL Server 2022 edition is licensed for the production and
non-production environments, and is the licence already in place?

**Decision.** Deferred. The SQL Server 2022 edition (Standard or Enterprise) is
not yet decided; it is confirmed with the host before environment setup. It does
not block requirements work. The architecture and data model are written so they
do not depend on Enterprise-only features unless this is later confirmed.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D9 — Document classification scheme

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 |
| Blocks | The control block of every SIMF document |

**Background.** Every SIMF document carries a classification. SIMF-DMP-001
section 5 currently handles all documents as Confidential pending the owner's
decision. The original NCA standard template carried a "choose classification"
field, so the owner has a defined scheme.

**Questions.** What are the official classification labels for this project,
which label applies to the engineering documentation, and are there handling or
marking rules (page headers or footers) to apply?

**Decision.** SIMF documents are handled and labelled as **Confidential** as the
working default, until the owner confirms an official classification scheme.
This is already the rule in SIMF-DMP-001 section 5. If the owner later issues an
official scheme, the control block of each document is updated to match.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D10 — Exhibitor approval: one stage or two

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 (SIMF-RPM-001 matrix review, Finding 4) |
| Blocks | SIMF-RPM-001 §9, SIMF-UCS-001 UC-21 |

**Background.** Decision D3 gives the PR team exhibitor approval and booth
assignment in one step. The original registration flowcharts, however, show the
Security team as the approval gate for every registration category, including
exhibiting entities (جهات عارضة). An exhibitor may therefore need both a
**Security clearance** and a **PR approval**.

**Question.** Is exhibitor approval one stage or two?

| Option | Flow |
|--------|------|
| A | One stage — the PR team approves the exhibitor and assigns the booth. This is the current proposal in the matrix. |
| B | Two stages — the Security team clears the exhibitor, then the PR team approves and assigns the booth. |

**Decision.** Option A — one stage. The PR team approves the exhibitor and
assigns the booth in a single step, consistent with decision D3. There is no
separate Security clearance stage for exhibitors.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D11 — Permission ownership: bookings, live, moderation, configuration

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 (SIMF-RPM-001 matrix review, Finding 5) |
| Blocks | SIMF-RPM-001 §9 |

**Background.** The matrix review left four ownership points where the proposed
assignment is reasonable but not confirmed.

**Questions.**

1. **Booking approval.** Session-seat bookings must be approved in the Control
   Panel (decision D4). Who approves them — the PR team (current proposal), the
   Scientific team, or a registration-desk / operations role? _(Superseded: the as-built system confirms seat reservations immediately with no Control Panel approval; the approval queue is retained but dormant. See the change history.)_
2. **Live-session management.** Who starts and stops a live broadcast during the
   event — the Scientific team (current proposal) or the Technical team?
3. **Comment moderation.** Who acts on the Control Panel comment moderation
   queue? The current proposal gives it to both PR and Scientific; a single
   owner is preferred. The in-app Moderator handles session questions
   separately.
4. **Configuration split.** `configuration` covers both system configuration and
   the dynamic categories and content. It is proposed for the Technical team
   only, which means Marketing cannot add, say, a news category without
   Technical. Should configuration be split into a system part (Technical) and a
   content / category part open to the content teams?

**Decision.**
1. Booking approval — the **PR team** approves session-seat bookings.
   *(Amended 2026-07-19: as built, attendee seat reservations are **confirmed
   immediately with no Control Panel approval**, and are held provisionally until
   the attendee checks in at the hall gate. The PR booking-approval permission and
   the Control Panel booking-approval queue are **retained but dormant** — the
   queue is always empty because nothing creates a pending booking. Admin
   reserve/block of a specific seat or a whole row for a VIP is confirmed
   immediately.)*
2. Live-session management — the **Scientific team** starts and stops the live
   broadcast.
3. Comment moderation — the **Scientific team** is the single owner of the
   Control Panel comment moderation queue. PR no longer holds it.
4. Configuration — `configuration` is **split**. System / platform
   configuration stays with the Technical team; a separate content-and-
   categories permission lets the content team (Marketing) manage the dynamic
   content blocks and the categories.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

### D12 — PR's registration-view scope

| | |
|--|--|
| Status | Decided |
| Raised | 2026-05-20 (SIMF-RPM-001 matrix review, Finding 6) |
| Blocks | SIMF-RPM-001 §9 |

**Background.** The proposed matrix gives the PR team `view` on all
registrations. The PR team's role is VIP and guest relations.

**Question.** Does the PR team need to view **all** visitor registrations, or
only the **VIP and guest** registrations relevant to its work?

**Decision.** The PR team views **all visitor registrations**. The proposed
matrix cell stands.

**Decided by:** Project Owner  **Date:** 2026-05-20

---

## 6. What each decision unblocks

| When this closes... | This becomes possible |
|---------------------|-----------------------|
| D1 | SIMF-RPM-001 can be written in full; access rules in SIMF-SRS-001; actors in SIMF-UCS-001 |
| D2 | The registration model in SIMF-DAT-001 and the registration requirements in SIMF-SRS-001 |
| D3 | Exhibitor, moderator and staff use cases in SIMF-UCS-001 and their requirements in SIMF-SRS-001 |
| D4 | The booking and attendance model in SIMF-DAT-001 and the related requirements |
| D5 | The engagement and cognitive-AI requirements and model |
| D6 | The media, news and statistics requirements; the Control Panel module list in SIMF-CPD-001 |
| D7 | The integration adapter detail in SIMF-SAD-001 section 9 |
| D8 | The environment specification in SIMF-OPS-001 |
| D9 | The classification field in every document's control block is finalised |
| D10 | The exhibitor cells of the SIMF-RPM-001 matrix and SIMF-UCS-001 UC-21 |
| D11 | The bookings, live, moderation and configuration cells of the SIMF-RPM-001 matrix |
| D12 | The PR registration-view cell of the SIMF-RPM-001 matrix |

D1 to D6 together unblock SIMF-SRS-001, SIMF-RPM-001, SIMF-UCS-001 and
SIMF-DAT-001 — the four documents currently waiting. D7 and D8 are needed later,
for integration detail and operations, and do not block the requirements
documents. D9 is administrative and blocks no engineering work.

## 7. Open items

| ID | Item | Needed for |
|----|------|-----------|
| OI-1 | Confirm who, on the client side, is the authority for each decision | Section 5 sign-offs |
| OI-2 | Agree a target date for closing D1–D6, so the Stage 1 gate has a deadline | SIMF-PGP-001 schedule |

---

End of document.
