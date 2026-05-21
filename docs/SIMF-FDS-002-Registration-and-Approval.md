# Feature Design Specification — Registration and Approval

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-002 |
| Title | Feature Design Specification — Registration and Approval |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-FDS-001, SIMF-SRS-001, SIMF-UCS-001, SIMF-API-001, SIMF-DAT-001, SIMF-RPM-001, SIMF-CON-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The registration and approval feature, build-ready. |

---

## 1. Purpose

This is the build-ready specification for the SIMF registration and approval
feature. It takes an account that has verified its email and carries it through
registration, the organisers' review, the assignment of a final user type, and
the issue of an entry badge. It is the second feature design specification and
follows directly from authentication (SIMF-FDS-001).

## 2. Scope

The feature covers:

- choosing a registration type and completing the registration profile,
- the data collected — personal, identity, contact, attachments, the venue
  track,
- identity photo verification,
- the exhibitor branch (the extra organisation data),
- terms consent and submission,
- registration status tracking,
- the Security team's review and decision for visitors and the "Other" types,
- the PR team's approval of exhibitors and booth assignment,
- the assignment of the final user type and the issue of the entry badge,
- on-site registration and badge reprint,
- opening and closing registration,
- internal-user onboarding, including TOTP enrolment.

It does **not** cover badge scanning at venue entry, attendee-to-attendee
contact exchange, or hall-arrival check-in — those are the Badge & Access
Control feature (a later SIMF-FDS). This feature **issues** the badge; it does
not operate it.

It begins where SIMF-FDS-001 ends: an account in the **EmailVerified** state.

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-201–FR-209 registration | UC-03 Complete the registration |
| FR-210 status tracking | UC-07 View registration status |
| FR-211–FR-214 Security review and decision | UC-20 Review and decide a registration |
| FR-216 registration open/close | UC-31 Open or close registration |
| FR-217 on-site registration / badge reprint | UC-34 Register on-site / reprint |
| FR-301, FR-302 badge issue and colour | (badge issued on approval) |
| FR-601, FR-602 exhibitor registration and approval | UC-21 Approve an exhibitor |
| FR-105 internal-user TOTP | UC-04 (the enrolment side) |

## 4. Feature overview

The feature moves an account through the registration states from
SIMF-RPM-001 section 6:

```
EmailVerified ──submit──▶ PendingApproval ──approve──▶ Approved ──▶ badge issued
                                  │
                                  └──reject──▶ Rejected
```

A person completes the registration profile, which puts the account into
**PendingApproval**. An organiser reviews it. On approval the organiser sets the
**final user type**, the account becomes **Approved**, and an entry badge is
issued. On rejection the account becomes **Rejected** with a recorded reason.

## 5. Detailed behaviour — registration

### 5.1 Registration type

- The user chooses **Visitor** or **Other**, or registers as an **Exhibitor**.
- "Other" resolves to a sub-type — Media, Sponsor, or another type — and Visitor
  to a sub-type later confirmed at approval. The available types are dynamic
  data from the `Category` table (SIMF-DAT-001 section 5.12); the form reads
  them at runtime, each with its colour.
- The choice branches the rest of the form: a Visitor or "Other" user sees the
  personal-data path (5.2–5.6); an Exhibitor additionally sees the organisation
  path (5.7).

### 5.2 Personal data

Collected for every registration:

- full four-part name in Arabic,
- name in English as written in the passport,
- nationality,
- date of birth,
- place of birth.

Optional: job title, personal photo.

### 5.3 Identity data

- A Saudi national enters the **national ID** number.
- A non-Saudi chooses the document type — **passport** or **Iqama** — and enters
  that number.

### 5.4 Contact data

- mobile number inside the Kingdom,
- mobile number outside the Kingdom, for an overseas visitor.

Phone numbers are **not** format-validated (FR-108); they are stored as entered.

### 5.5 Attachments

- The ID image is uploaded as an attachment.
- The system supports further attachment types, added later, selected by user
  type. Each attachment is stored as an `Asset` linked to the registration
  request (SIMF-DAT-001 sections 5.2, 5.12).

### 5.6 Identity photo verification

The registration captures a photo for identity verification. Anti-spoofing
checks apply to a camera capture. There is a documented exception: women are
not asked to use the camera and are verified through an alternative the
organisers control, without a photo upload (SIMF-CON-001 section 11.2, the
mockup's alternate Screen 6). The form offers the alternative path for that
case.

### 5.7 Exhibitor organisation data

An Exhibitor registration additionally collects:

- the organisation, company or sponsor name,
- country, organisation type, sector,
- a short organisation bio,
- the commercial registration number,
- accompanying delegates (companions), each added as a `Companion` record.

### 5.8 Venue track

After the data is entered, the user selects a **venue track / zone** — the
"direction / track" of decision D2 — from the tracks maintained in the Control
Panel.

### 5.9 Terms and submission

- The user reads the Terms & Conditions and gives consent; consent is required
  to submit.
- On submission the system creates a `RegistrationRequest`, sets the account to
  **PendingApproval**, sends the user a message with the contact details, and
  writes an entry to the operation log.
- All registration fields are mandatory except those marked optional in 5.2;
  the form does not submit until they are complete.

### 5.10 Registration status

The user can view where their request stands — the stages from SIMF-CON-001
section 7.1: data sent, email confirmed, SIMF security review, account
activation. The status updates as the organisers act.

## 6. Detailed behaviour — approval

### 6.1 Security review and decision (visitors and "Other")

- A user holding the Registration Requests page with the Approve and Reject
  actions (SIMF-RPM-001 section 8; the Security team in the suggested
  configuration) opens the registration requests queue.
- For one request: the reviewer reads the submitted data and the attachments,
  checks the identity and photo, and either approves or rejects.
- **On approve:** the reviewer sets the **final user type** (the specific
  sub-type); the account becomes **Approved**; a badge is issued (section 6.4);
  the user is notified.
- **On reject:** the reviewer records a reason; the account becomes
  **Rejected**; the user is informed. Rejection is used on a suspected forgery
  or a data mismatch.
- **Bulk approval:** the queue offers a select-all control so a reviewer can
  approve many requests at once (FR-212).

### 6.2 Exhibitor approval (PR)

- Exhibitor approval is **one stage** (decision D10). A user holding the
  Exhibitors page with the Approve and Reject actions — the PR team in the
  suggested configuration — reviews the exhibitor request.
- On approve, the reviewer **assigns the booth** (its hall and number) in the
  same step; the account becomes Approved; a badge is issued; the exhibitor is
  notified.
- On reject, a reason is recorded and the exhibitor is informed.

### 6.3 The final user type

The registration type the user picked (Visitor / Other / Exhibitor) is their
intent. The **final user type** is set by the organiser at approval and is what
drives the user's permissions and app access (SIMF-RPM-001 section 5.3).

### 6.4 Badge issue

On approval the system issues a `Badge`: a unique reference number in the form
`SIMF-2026-xxxx`, a QR payload, and a colour taken from the user's category
(FR-301, FR-302). The badge becomes visible to the user in the app. Operating
the badge — scanning it at entry, contact exchange — is the Badge & Access
Control feature.

## 7. On-site registration and badge reprint

At the registration desk, Staff handle a person who arrives without a badge
(FR-217, UC-34):

- Staff search the system for the person.
- If the person is already registered, Staff **reprint** the badge.
- If not, Staff complete an **on-site registration**. Whether an on-site
  registration is approved on the spot or follows the same Security review as
  an online one is open item OI-1.

## 8. Registration open and close

- A user holding the System Configuration page can open and close registration
  (FR-216).
- Registration **closes automatically** at the end of the last forum day.
- The open/closed state is held in the `RegistrationControl` configuration
  (SIMF-DAT-001 section 5.12). When registration is closed, the registration
  form is not offered and a submission is refused with a clear message.

## 9. Internal-user onboarding and TOTP enrolment

Internal users — the Administrator and the organising teams — are not
self-registered. An Administrator creates an internal-user account from the
Control Panel and assigns one or more roles (SIMF-RPM-001 section 12).

On first sign-in, an internal user **enrols a TOTP authenticator**: the system
generates a TOTP secret, shows it as a QR code for an authenticator app, and
confirms enrolment once the user enters a correct code. From then on, every
Control Panel sign-in requires the TOTP step (SIMF-FDS-001 section 5.6). The
TOTP secret is stored as `TotpSecret` against the user.

## 10. Data

The feature uses these entities from SIMF-DAT-001: `User`,
`RegistrationRequest`, `AttendeeProfile`, `ExhibitorProfile`, `Companion`,
`Attachment`, `Asset`, `Badge`, `TotpSecret`, `Category`, `VenueTrack`,
`RegistrationControl`, `OperationLog`.

`RegistrationRequest.Status` follows the account states; the final user type is
a `Category` of kind VisitorSubType or OtherType set on approval.

## 11. User interface

| Surface | Screens |
|---------|---------|
| Mobile app | Screen 4 Sign-up step 1, Screen 5 Sign-up details (type + personal data), Screen 6 verification (email OTP and the alternate photo-verification variant), Screen 7 Visitor seat/row pick, Screen 8 Sponsor/exhibitor details, Screen 9 Terms, Screen 10 Registration confirmed, Screen 11 Registration status |
| Control Panel | Registration Requests queue and detail; the Exhibitors queue and detail; the on-site registration screen; System Configuration for the registration open/close control; internal-user management |
| Website | The website registration form (registration is offered on the website) |

Mobile visuals are the external designer's; Control Panel screens follow
SIMF-CPD-001. Every screen has loading and error states; field errors show
against their field; all text is localised, Arabic and English.

## 12. Validation rules

| Field | Rule |
|-------|------|
| Registration type | Required; one of the active types |
| Arabic full name | Required; four parts |
| English name | Required; as in the passport |
| Nationality | Required |
| Date of birth | Required; a valid past date |
| Place of birth | Required |
| National ID | Required for a Saudi national |
| Document type + number | Required for a non-Saudi; passport or Iqama |
| Mobile inside KSA | Required; stored as entered, no format check |
| Mobile outside KSA | Required for an overseas visitor; no format check |
| ID image | Required attachment |
| Venue track | Required; one of the configured tracks |
| Exhibitor organisation fields | Required on the exhibitor branch |
| Terms consent | Required; must be given to submit |
| Rejection reason | Required when a request is rejected |

Validation failures return `VALIDATION_FAILED` with one `details` entry per
field (SIMF-API-001 section 7).

## 13. Security considerations

- Registration data is personal data — identity numbers, contact details,
  attachments — and is encrypted at rest and in transit (NFR-11).
- Identity verification, the photo check and anti-spoofing reduce fraudulent
  registration; a suspected forgery is a rejection reason.
- The approval, rejection, final-type assignment, badge issue, registration
  open/close, and internal-user creation are all written to the operation log.
- The Registration Requests and Exhibitors pages are permission-controlled; a
  reviewer sees them only with the right role.
- An attachment upload is restricted to expected file types and a size limit.

## 14. Acceptance criteria

1. An EmailVerified user can complete a Visitor, "Other" or Exhibitor
   registration; on submit the account is PendingApproval and a message is sent.
2. The form branches correctly by registration type, and the identity fields
   adapt to Saudi vs non-Saudi.
3. The photo-verification alternative is offered for the documented exception.
4. A mandatory field left empty, or terms not consented, blocks submission with
   a clear error.
5. The Security team can review a request, approve it with a final user type,
   or reject it with a reason; bulk approval works.
6. The PR team can approve an exhibitor and assign a booth in one step, or
   reject with a reason.
7. On approval the account becomes Approved and a badge is issued with a unique
   reference and a category colour.
8. The user can see their registration status through the stages.
9. On-site registration and badge reprint work at the registration desk.
10. Registration can be opened and closed, closes automatically at the end of
    the last forum day, and a closed state blocks submission.
11. An Administrator can create an internal user; that user enrols TOTP on first
    sign-in.
12. All screens render in Arabic (RTL) and English (LTR); no hardcoded text.
13. The build is clean and the feature has unit, integration and end-to-end
    tests that pass.

## 15. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Complete a Visitor registration with valid data | account PendingApproval; confirmation message sent |
| T-02 | Complete an Exhibitor registration | organisation data captured; PendingApproval |
| T-03 | Saudi vs non-Saudi identity branch | national ID vs passport/Iqama choice shown correctly |
| T-04 | Submit with a mandatory field empty | submission blocked; field error |
| T-05 | Submit without terms consent | submission blocked |
| T-06 | Photo verification — standard and the women exception | both paths complete verification |
| T-07 | Security approves a visitor with a final type | account Approved; badge issued; user notified |
| T-08 | Security rejects a request with a reason | account Rejected; reason recorded; user informed |
| T-09 | Bulk-approve several requests | all selected requests Approved |
| T-10 | PR approves an exhibitor and assigns a booth | exhibitor Approved; booth recorded; badge issued |
| T-11 | View registration status across the stages | status reflects the organisers' actions |
| T-12 | On-site: search an already-registered visitor | badge reprinted |
| T-13 | On-site: register a new visitor | on-site registration created |
| T-14 | Close registration, then attempt to submit | submission refused with a clear message |
| T-15 | Registration auto-closes at the end of the last forum day | the form is no longer offered |
| T-16 | Create an internal user, then first sign-in | TOTP enrolment completes; later sign-ins require TOTP |
| T-17 | Render every registration screen in Arabic and English | correct layout and direction; no hardcoded text |

## 16. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm whether on-site registration is approved on the spot or follows the standard Security review (SIMF-UCS-001 OI-3) | Section 7 |
| OI-2 | Confirm the attachment file types and the size limit | Sections 5.5, 13 |
| OI-3 | Confirm the Visitor seat/row pick on mockup Screen 7 — flagged for review in SIMF-CON-001 §14 | Section 11 |
| OI-4 | Confirm document classification with the owner | Control block |

---

End of document.
