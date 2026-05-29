# Feature Design Specification — Registration and Approval

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-002 |
| Title | Feature Design Specification — Registration and Approval |
| Version | 2.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Last updated | 2026-05-24 |
| Related documents | SIMF-FDS-001, SIMF-SRS-001, SIMF-UCS-001, SIMF-API-001, SIMF-DAT-001, SIMF-RPM-001, SIMF-CON-001, SIMF-CPD-001, docs/decisions/DECISIONS_LOG.md |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The registration and approval feature, build-ready. |
| 2.0 | 2026-05-24 | Engineering & Architecture Team | **Amendment A — Implementation update.** Documents what was built between 2026-05-22 and 2026-05-24: admin-create-user flow with a 7-day invite code (D-042); the admin user-management page split — staff and visitors live on separate pages, no mixing (P3); the visitor / staff approval workflow with `PendingApproval → Approved/Rejected (with a 10–500 char reason)` (P4a + P4b); the **QR id minted on approval** instead of on create (D-046a + P4a); the visitor-profile feature — service, the curated 60-country ISO 3166-1 list, the encrypted-at-rest ID image (AES-GCM, content-type byte prefix, in-file nonce + tag) (D-046b); the Website cookie + `/account/visitor-profile` page (D-046c); the strict Saudi national-id starts-with-1 and Iqama starts-with-2 validator rules (P5); the bulk-admin features — bulk-delete, duplicate, Excel import / export (D-044b); and the avatar storage migration from DB bytes to the filesystem (D-039). Records the open P7 rework that will introduce the `UserType` model and the `ProfileTypes` lookup. |
| 2.1 | 2026-05-29 | Engineering & Architecture Team | **Amendment B — Mobile-app role mapping on ProfileType (D-161).** Adds the per-`ProfileType.MobileAppRole` admin-curated column and the resolved `mobile_app_role` JWT claim. Closes the half of OI-6 about which Other-tier subtypes map to which in-app authority. |

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

## Amendment A — Implementation update (2026-05-24)

This amendment captures what was actually built between 2026-05-22 and
2026-05-24. The v1.0 body remains the source of truth for the
*specification*; this amendment is the source of truth for the
*implementation* where the two have refined each other. Every claim is
traceable to a decision row in `docs/decisions/DECISIONS_LOG.md` (the
`D-NNN` references) and to the relevant commit on `feature/login-api`.

### A.1 Admin-create-user flow (D-042)

The v1.0 body §9 said internal-user onboarding starts with an
Administrator creating an account from the Control Panel. That is built —
with a specific shape:

- **Endpoint:** `POST /api/v1/admin/staff` (the staff endpoint after the
  P3 split — see A.2 below), Administrator-only.
- **Optional `GrantAdministratorRole` flag** — defaults to `false`. When
  `true`, the new user is added to the `Administrator` RBAC role; today
  no other CP role exists for the v1.0 surface, so this is the only
  fine-grained option.
- **No password is set.** The new user receives an **email invitation**
  carrying a 7-day single-use code (purpose: `PasswordReset`); the user
  follows the link to `/reset-password`, enters email + code + a new
  password, and signs in normally.
- **Self-issue and duplicate-email cases** both produce a clear error
  (`AUTH_ACCOUNT_NOT_FOUND` / `ADMIN_EMAIL_ALREADY_REGISTERED`).
- **Audit:** every creation writes `Admin.UserCreated` (and
  `Admin.UserCreateFailed` on rejection) carrying both `ActorUserId` (the
  admin) and `SubjectUserId` (the new user).

Why 7 days: matches the GitHub / Microsoft 365 invite-link convention.
Self-service forgot-password keeps its short 10-minute code because the
user is actively waiting there.

Why no plain password at creation: the OWASP / NIST 800-63B baseline
for admin-issued accounts — the admin is never exposed to a plaintext
password.

There is **no companion endpoint** that lets the admin pick the
password directly. Open item OI-5 below covers the user-type / subtype
picker that P7 introduces.

### A.2 The admin user-management page split — staff vs visitors (P3)

The customer explicitly required (verbatim): *"in CP there are 2
different pages for create user, one for admin and one for Visitor.
DON'T MIX."* This is built.

- **`/admin/staff`** — list page for users that hold at least one CP
  role (Administrator today; Staff / Scientific / Security from the
  later P4 — pending P7 rework, see A.6).
- **`/admin/staff/new`** — create form for a staff member. Email,
  display name, optional `GrantAdministratorRole` checkbox.
- **`/admin/visitors`** — list page for users without any CP role.
- **`/admin/visitors/new`** — create form for a visitor. Email, display
  name only — no role checkbox.

The API was split in lock-step:

| URL (before P3) | URL (after P3) | Notes |
|---|---|---|
| `POST /admin/users` | `POST /admin/staff` | Creates a staff user. |
| (new) | `POST /admin/visitors` | Creates a visitor. |
| `POST /admin/users/list` | `POST /admin/staff/list` | Lists staff (role-filtered). |
| (new) | `POST /admin/visitors/list` | Lists visitors (no CP role). |
| `POST /admin/users/{id}/reset-two-factor` | `POST /admin/staff/reset-two-factor` | Same body. |
| `POST /admin/users/bulk-delete` | `POST /admin/staff/bulk-delete` | (A.7) |
| `POST /admin/users/export` / `/import` / `/duplicate` | `POST /admin/staff/export` / `/import` / `/duplicate` | (A.7) |

The nav under the **System** group is `Staff` + `Pending staff` +
`Visitors` + `Pending visitors` + `Reset user 2FA` + `Logs` (P6) +
`Configuration` / `Operation log` / `Settings` (placeholders).

### A.3 The visitor / staff approval workflow (P4)

The v1.0 body §6.1 specified the **approval flow** but did not specify
the **lifecycle hook** for QR-id mint, did not specify which CP role
approves visitors versus staff, and did not specify the form of a
rejection reason. Amendment A pins all three:

#### A.3.1 State transitions on the AdminAccountService

- **Create** (`/admin/staff` or `/admin/visitors`) — lands in
  `AccountState.PendingApproval`. **The QR id is NOT minted at create
  time.**
- **Approve staff** (`POST /admin/staff/{id}/approve`) —
  Administrator-only. Flips `AccountState` to `Approved` and **mints
  the QR id at this transition**.
- **Approve visitor** (`POST /admin/visitors/{id}/approve`) — any CP
  role today (`AdministratorOnly` policy after P7). Same state flip
  and QR mint.
- **Reject staff** (`POST /admin/staff/{id}/reject`) and
  **Reject visitor** (`POST /admin/visitors/{id}/reject`) — both
  require a **10–500 char free-text reason** captured in the audit
  row's `Detail` field. Flip `AccountState` to `Rejected`.
- **List pending** — `POST /admin/staff/pending/list` and
  `POST /admin/visitors/pending/list` return a `GridQuery`-paged
  `AdminPendingUserSummary` list filtered to PendingApproval rows.

A re-approve / re-reject returns **409 `ADMIN_USER_NOT_PENDING`** — the
service refuses to flip a user that is not in `PendingApproval`.

#### A.3.2 Sign-in implications

The v1.0 body §5 (D-010) said a `PendingApproval` user may sign in
with limited access. The implementation **refines** this:

- A `PendingApproval` user on the **Control Panel** surface (audience
  `cp`) is **refused** sign-in with `AUTH_ACCOUNT_NOT_APPROVED` (403).
  A pending CP user cannot do anything until approved; blocking the
  sign-in is the honest answer.
- A `PendingApproval` user on the **Web** or **App** surface is
  **allowed** and sees a "pending" UI on `/account/visitor-profile`.
  D-010 stands here.

#### A.3.3 CP pages (P4b)

- `/admin/staff/pending` — Administrator-only. SimfDataGrid over
  pending-staff rows; per-row Approve + Reject buttons; Reject opens a
  SimfModal with a `SimfTextarea` for the reason (10–500 chars, the
  Submit button is disabled until the reason is in range).
- `/admin/visitors/pending` — any CP role today (`AdministratorOnly`
  after P7). Same shape.
- Both pages show a toast on success and refresh the grid so the
  just-handled row drops out of view.

#### A.3.4 Audit events

Every action writes one row with `ActorUserId` + `SubjectUserId`:

| Event type | Detail field |
|---|---|
| `Admin.StaffApproved` | The newly-minted QR id |
| `Admin.StaffRejected` | The rejection reason |
| `Admin.VisitorApproved` | The newly-minted QR id |
| `Admin.VisitorRejected` | The rejection reason |

### A.4 QR id format and life cycle (D-046a + P4)

Every account carries a `SimfUser.QrId` — a **12-character Crockford
base32 token** (alphabet `23456789ABCDEFGHJKMNPQRSTVWXYZ`, no
`0/1/I/L/O/U`), unique across the system, indexed with a SQL filtered
unique index (`[QrId] IS NOT NULL`).

- **Generation:** `IQrIdMinter` uses `RandomNumberGenerator.Fill` and
  retries on the (negligible) chance of a DB clash.
- **When:** at the **`PendingApproval → Approved` transition**, not at
  create time (D-046a, P4).
- **Shared alphabet:** the same Crockford base32 set that TOTP recovery
  codes use (A.5 in SIMF-FDS-001 Amendment B), so a person who reads a
  QR id or a recovery code off paper has the same OCR-resistant rules.

### A.5 Visitor profile + encrypted ID image (D-046b)

The `VisitorProfile` entity holds the fields the v1.0 body §5.2–5.4
listed — Saudi or non-Saudi flag, ID image, etc. — plus:

- Visitor type (radio: Visitor / Exhibitor / Press),
- Arabic name + English name + place of birth,
- Nationality (ISO 3166-1 code, from the curated 60-entry
  `SIMF.Common.Countries` list with EN + AR names),
- Date of birth,
- Conditional **national ID** (Saudi) or **Iqama** / **passport**
  (non-Saudi) — see A.6 for the validation rules,
- Saudi mobile + international mobile (permissive, see below),
- ID image — relative path on disk.

#### A.5.1 Encrypted-at-rest ID image (AES-GCM)

The ID-image file format is, on disk, exactly:

```
[ 1 byte content-type code ] [ 12-byte nonce ] [ 16-byte tag ] [ N-byte ciphertext ]
```

- **Cipher:** AES-256-GCM.
- **Key:** a per-installation 32-byte symmetric key supplied via
  `Storage:VisitorIdEncryptionKey` (base64); a **startup gate** refuses
  to boot the API if the key is missing or shorter than 32 bytes,
  mirroring the JWT signing-key check.
- **Filename:** `{userId:N}.bin` — server-controlled, no path
  traversal possible.
- **Magic-byte gate:** the upload endpoint sniffs the JPEG /
  PNG / WEBP magic bytes before passing the buffer to the cipher
  (CWE-1236 defence — a polyglot upload is rejected before it touches
  the user's row).
- **Storage path:** `{Storage:VisitorIdBase}/{userId:N}.bin`.

The cipher choice and key shape were approved by the customer in
mid-design as part of the D-046b discussion. A per-user key derived
via HKDF was considered and rejected for v1 — it would couple the
cipher to the user id and complicate a future
admin-impersonation-with-audit flow without adding meaningful
protection.

#### A.5.2 Endpoints

| Endpoint | Method | Notes |
|---|---|---|
| `/api/v1/account/visitor-profile` | GET | Get the signed-in user's visitor profile. |
| `/api/v1/account/visitor-profile` | POST | Upsert the signed-in user's visitor profile. |
| `/api/v1/account/visitor-profile/countries` | GET | The curated 60-entry country list (EN + AR names). |
| `/api/v1/account/visitor-profile/id-image` | POST | Multipart, 5 MB cap, magic-byte gate. |
| `/api/v1/account/visitor-profile/id-image` | GET | Streams the decrypted bytes back. |

Audit: `VisitorProfile.Saved`, `VisitorProfile.IdImageUploaded`,
`VisitorProfile.IdImageRejected`.

#### A.5.3 The Website page (D-046c)

`/account/visitor-profile` on the Website — `InteractiveServerNoPrerender`,
`[Authorize]` — assembles the D-044(c) primitives (SimfRadioGroup,
SimfTextField, SimfSelect for countries, SimfDatePicker,
SimfPhoneInput × 2, SimfFileUpload, SimfAlert) and renders a
**server-side SVG QR via QRCoder** showing the QR id once the account
is Approved. Conditional rendering: Saudi → national-id field;
non-Saudi → Iqama + Passport fields. The page reloads from the API on
save so the QR and the profile state stay in sync.

### A.6 ID-document validation (P5)

The v1.0 body §5.3 and the v1.0 §12 validation table specified the
**document choice** but not the per-document format rules. P5 pins
them:

- **Saudi national ID** — exactly 10 digits, **starts with `1`**.
- **Iqama** (non-Saudi residing in Saudi Arabia) — exactly 10 digits,
  **starts with `2`**.
- **Passport** (non-Saudi without an Iqama) — free-form, the v1.0 §12
  "as written" rule applies.
- **Phone numbers** — kept permissive (`+?\d{1,4}[-\s]?\d{4,15}`) per
  the customer's instruction. The customer's visitor population is
  international + Saudi; a stricter rule would wrongly reject a UK or
  US number at the form level.

Error codes:
- `VISITOR_NATIONALITY_UNKNOWN` — the supplied country ISO code is
  not in the curated list.
- `VALIDATION_FAILED` — every other validator rejection, with one
  `details` entry per field.

### A.7 Bulk admin actions on users (D-044b)

Beyond the per-row Approve / Reject of A.3, the admin user pages
offer bulk actions, all gated by `AdministratorOnly`:

- **`POST /admin/staff/bulk-delete`** — soft-deletes (sets
  `AccountState = Disabled` + revokes refresh tokens + rolls the
  security stamp) one or many users. **One audit row per subject**
  (`Admin.UserDeleted`) so SOC review sees every deletion. A
  mandatory free-text reason (10–500 chars). Self-delete and
  admin-vs-administrator-delete are rejected silently per target
  (counted as `Skipped`); the batch does not fail.
- **`POST /admin/staff/duplicate`** — creates a copy of an existing
  user with a new email, the same display-name pattern and the same
  Administrator-role membership; a fresh 7-day invite email.
  Duplicate-email yields 409.
- **`POST /admin/staff/export`** — returns the bytes of an XLSX
  workbook with the selected users (or every user matching the
  optional `GridQuery` when `Ids` is empty). The `Users` sheet has
  the columns the customer asked for. Every string cell is
  prefix-sanitised against Excel formula injection (CWE-1236) — a
  leading `= + - @ \t \r` is escaped with a `'`.
- **`POST /admin/staff/import`** — bulk-creates users from an XLSX
  upload. 5 MB cap; ZIP magic-byte sniff; strict sheet-name match
  (`Users`); 5 000-row import cap; one audit row per imported
  subject.

The CP grid (`SimfDataGrid`) wires Select-All / Add / Edit / Delete /
Copy / Paste / Duplicate / Import / Export from the same toolbar.

### A.8 Avatar storage migration (D-039)

The v1.0 body did not say where avatars are stored. Initial build
held them as `varbinary(max)` on `SimfUser`; that inflated every
profile fetch by 33 % (base64 in JSON) and made the top-bar avatar
pre-fetch a multi-megabyte payload per circuit boot.

The implementation now stores avatars on the **filesystem** under a
configured `Storage:AvatarBase` directory (the same convention the
IBS V10 `UploadCarImagesEndpoint` uses). `SimfUser.AvatarPath` is the
relative path; the API exposes an authenticated streamer
`GET /api/v1/account/avatar/{userId:guid}` that resolves the row,
opens the file via `IAvatarStorage`, and streams it back with the
right `Content-Type` and `Cache-Control: private, max-age=300`. The
CP proxies that endpoint at `GET /account/api/avatar/{userId}`.

`ProfileResponse.AvatarUrl` / `AvatarResponse.AvatarUrl` carry a
`?v={UpdatedAt.UtcTicks}` cache-buster — a fresh upload always
defeats the browser cache.

Validation: same shape as the visitor ID image — 2 MB cap; PNG /
JPEG / WebP allow-list; magic-byte sniff against the declared MIME.
Audit: `Avatar.Updated`, `Avatar.Rejected`.

### A.9 The User Type model — open question (P7)

The v1.0 body and v1.1 of SIMF-FDS-001 refer to a *final user type*
set at approval (§6.3). The implementation today does not yet have a
hardcoded `UserType` enum on `SimfUser`; the proxy used in the audience
gate (P2) is "has any RBAC role" vs "has none".

**The customer's instruction (2026-05-24)** clarifies the model that
P7 will introduce:

| `UserType` | Where they sign in | RBAC roles? | Subtype |
|---|---|---|---|
| `Admin` | CP only | **Yes** — every action gated by a CP role | None |
| `Other` | App only | **No** | FK → `ProfileTypes` lookup (dynamic) |
| `Visitor` | App only | **No** | FK → `ProfileTypes` lookup (dynamic) |

`ProfileTypes` schema (new lookup):

| Column | Notes |
|---|---|
| `Id` | Guid PK |
| `Name` | bilingual (EN + AR) display label — VVIP, VIP, Gold, Staff, Exhibitor, Sponsor, … |
| `PageColor` | UI / bag-colour token used by the app |
| `UserType` | discriminator — `Admin` / `Other` / `Visitor` (Admin retained for future use; today only Other and Visitor have ProfileTypes) |
| `IsActive` | soft-delete flag |

P7's scope:
1. Add `UserType` + `ProfileTypeId` to `SimfUser` with an EF migration.
2. Create the `ProfileTypes` table + seeder for the v1 set.
3. **Drop** the P4 `Staff / Scientific / Security` RBAC roles —
   they become rows in `ProfileTypes` with `UserType = Other`.
4. Rekey the P2 audience gate off `UserType` (cp → Admin, app →
   Visitor + Other).
5. Rekey the P3 staff / visitor list split off `UserType`.
6. Collapse the P4 `TeamMember` policy back to `AdministratorOnly`
   for every approval endpoint.

Migration rule for existing rows: users with the `Administrator` RBAC
role → `UserType = Admin`; **everyone else → `UserType = Visitor`**
(the safe default; the small number of `Other` users get reclassified
manually after deployment). The customer confirmed this rule
on 2026-05-24.

P7 has **not** been built yet. This amendment records the design so
the owner can approve / amend it before the rework is implemented.

### A.10 Implementation decisions index

The decisions that fed this amendment.

| ID | Date | Subject | FDS section |
|---|---|---|---|
| D-039 | 2026-05-23 | Avatar storage migration (DB → filesystem) | A.8 |
| D-042 | 2026-05-23 | Admin creates a CP user; 7-day invite | A.1 |
| D-043 | 2026-05-23 | Admin pages aligned with CP layout conventions | A.2 |
| D-044(a/b/c) | 2026-05-23 | `SimfDataGrid` v1 / v2 + bulk admin actions + visitor-profile primitives | A.2, A.5, A.7 |
| D-045 | 2026-05-23 | Stage 7 five-agent review SEV-1/2 fixes | A.5, A.7 |
| D-046(a/b/c) | 2026-05-23 | QR id on approval; visitor-profile service + encrypted ID image; Website cookie + page | A.4, A.5 |
| **P1** | 2026-05-24 | Web login: Arabic label removed from EN language switch | n/a (SIMF-FDS-001 Amendment B) |
| **P3** | 2026-05-24 | CP page split — staff vs visitors ("don't mix") | A.2 |
| **P4a** | 2026-05-24 | Approval workflow backend + reviewer-role split | A.3 |
| **P4b** | 2026-05-24 | CP pending pages (staff / visitors) | A.3.3 |
| **P5** | 2026-05-24 | Strict Saudi national-ID + Iqama validator prefixes | A.6 |
| D-047 | 2026-05-24 | Per-project log files + CP viewer | n/a (orthogonal) |

### A.11 Open items added by Amendment A

| ID | Item | Affects |
|---|---|---|
| OI-5 | Approve and ship **P7** — `UserType` enum + `ProfileTypes` lookup; rework P3/P4/P2 off it. | A.2, A.3, A.9 |
| OI-6 | Decide the **dynamic ProfileTypes seed set** for v1: which Visitor subtypes (VVIP, VIP, Gold, …) and which Other subtypes (Staff, Exhibitor, Sponsor, Media, …) ship at first deployment. Each row needs a `PageColor`. | A.9 |
| OI-7 | Replace the curated 60-entry country list (`SIMF.Common.Countries`) with the full ISO 3166-1 set when an unmatched code is requested by a visitor. | A.5 |
| OI-8 | Bring SIMF-API-001 to Amendment B in lock-step with this amendment — the new admin endpoints (A.2, A.3, A.7), the visitor-profile endpoints (A.5), the avatar streamer (A.8). | SIMF-API-001 §12 |
| OI-9 | Decide whether bulk-approve / bulk-reject is needed on the pending pages (today: per-row only). | A.3.3 |

---

## Amendment B — Mobile-app role mapping on ProfileType (D-161, 2026-05-29)

The mobile-app authority a signed-in user carries is admin-curated data, not a
hardcoded list. `ProfileType` gains a non-nullable `MobileAppRole` column
(default `None`) which is the source of truth for the per-`ProfileType`
mapping; the resolved value travels on every JWT as the `mobile_app_role`
claim.

**Resolution rules at JWT issue time:**

| `SimfUser.UserType` | `mobile_app_role` claim                                                         |
|---------------------|---------------------------------------------------------------------------------|
| `Visitor`           | `Visitor` (resolved from `UserType`; ignores `ProfileType.MobileAppRole`)       |
| `Admin`             | `None` (admins do not use the mobile app)                                       |
| `Other`             | The assigned `ProfileType.MobileAppRole`, or `None` when no profile type is set |

**Allowed values on `ProfileType.MobileAppRole`:** `None`, `Staff`, `Moderator`.
The wire layer rejects `Visitor` with a 400 — that value is computed from
`UserType` and may never be written per-`ProfileType` row.

**Seed.** The Identity seeder ships `Staff (Other) → MobileAppRole.Staff`.
Every other operational mapping (Volunteer → Staff, Programme Coordinator →
Moderator, Operations Lead → Moderator, Exhibitor / Sponsor / Speaker → None,
…) is admin-curated at runtime from the Control Panel. This closes the OI-6
half about "which Other types are Moderator vs Staff" — the answer is "the
admin decides per row, the seed names only the canonical Staff row."

**Admin CRUD.** `AdminCreateProfileTypeRequest`,
`AdminUpdateProfileTypeRequest`, and `AdminProfileTypeSummary` carry the field
on the wire. The CP picker / form UI is a follow-up; today admins can curate
the value via the API.

**Mobile consumption** is documented in SIMF-MAA-001 §8.1; the JWT claim is
also reflected in SIMF-API-001 §12.2.

---

End of document.
