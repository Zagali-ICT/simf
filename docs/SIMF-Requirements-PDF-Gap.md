# SIMF Requirements — Stakeholder PDF Gap Analysis

| Field | Value |
|-------|-------|
| Document ID | SIMF-Requirements-PDF-Gap |
| Version | 1.0 |
| Status | Authoritative for gap-closure planning |
| Author | SIMF Engineering Team |
| Date | 2026-05-29 |
| Source PDF | `D:\SIMF\System\15-04-2024\متطلبات التطبيق والموقع الإلكتروني للملتقى + ملاحظات على Mockup.pdf` |
| Related documents | All current `docs/SIMF-*` controlled documents, `DECISIONS_LOG.md` D-001 → D-161 |

## 1. Purpose and authority

This document records the stakeholder requirements extracted from the source
PDF in section 2 below, compares each requirement against what is already
shipped on the `feature/login-api` branch in section 3, flags every conflict
with the prior controlled documents in section 4, and proposes a
priority-ranked phased plan to close the remaining gaps in section 5.

The source PDF lives under `D:\SIMF\System\15-04-2024\` — the folder that
`CLAUDE.md` flags as "superseded drafts." The owner has expressly designated
**this PDF** as **authoritative** for forward-looking planning. The other
files in that folder remain superseded. Where this PDF conflicts with any
prior `docs/SIMF-*` controlled document, this PDF wins for the affected item
and the controlled document is updated in the same sprint that closes the
gap.

## 2. Requirements extracted from the source PDF

### 2.1 Admin Panel — Dynamic content (PDF §1)

- The admin panel must let an operator edit every piece of app and website
  content without a code change:
  - Primary and secondary headings
  - Body text and descriptive copy
  - Logos and images
  - In-app welcome message
  - Banners and announcements
  - Section names and labels
  - Brand colour tokens
  - Page management (the page entities themselves are editable, not only their content)

### 2.2 Categories and labels (PDF §2)

- Every category and label is editable from the admin panel:
  - Registration types
  - Section names
  - Page names
  - User categories
  - Interests
  - Sessions
- Each category supports add / hide-or-delete / colour-edit at runtime.

### 2.3 Registration control (PDF §3)

- Admin can open and close registration.
- Registration auto-closes at the end of the forum's last day.
- Manual override of the open / closed state at any time.

### 2.4 Archive management (PDF §4)

- The 2026 archive does **not** appear inside the app until the forum ends.
- Archive visibility is controlled from the admin panel.

### 2.5 Sign-in and account creation (PDF §5)

- Sign-in methods:
  - Email
  - **Face ID (biometric)**
- Verification:
  - 6-digit OTP delivered to email.
- Registration types:
  - **Visitor** and **Other**.
  - "Other" contains at least: **Media (إعلامي)** and **Sponsor (راعي)**.
  - New types can be added in future; admin can edit them and assign a
    distinct colour per type.

### 2.6 Account creation fields (PDF §6)

- Optional fields:
  - **Job title**
  - Profile photo
- Required fields (every registration):
  - Full name in Arabic, four parts (رباعياً)
  - English name as printed on passport
  - Nationality
  - Date of birth
  - Place of birth
  - National ID (Saudi citizens)
  - Iqama number (residents)
  - Passport number
  - Email
  - Mobile inside the Kingdom
  - Mobile outside the Kingdom (for foreign visitors)
- Required attachments — selected by user type:
  - National ID document
  - Iqama document
  - Passport
- The required attachment is selected from the user's registration type.

### 2.7 Roles and permissions (PDF §7)

The system supports multiple roles with admin-curated permissions and the
ability to add new roles. The PDF names four roles:

#### 2.7.1 Security team (الفريق الأمني)

- View the full visitor list.
- Review registrant data.
- Accept or reject a visitor.
- **"Select all"** for bulk approvals.

#### 2.7.2 Session moderator (المحاور)

- View the questions submitted during a live session.
- Push questions to the speakers.
- Hide, delete, and reorder questions during the session.

#### 2.7.3 Public relations team

- View VIP and guest data.
- Manage invitations.
- Track attendance and confirmations.
- Manage guest-targeted messages and notifications.

#### 2.7.4 Technical team

- System settings.
- Users and permissions management.
- System monitoring and technical support.
- View technical logs.
- Control general features and settings.

### 2.8 Interests and smart recommendations (PDF §8)

- "My interests" page at the start of registration.
- Sample options: cybersecurity, IT, investment and entrepreneurship,
  maritime navigation, others.
- Interests later drive:
  - Suggesting similar people.
  - The **"Meet people like you"** discovery feature.

### 2.9 Sessions and halls (PDF §9)

- Rename "Agenda" → **"Sessions"** in the UI.
- Halls and seating capacity managed from the admin panel:
  - Specify seat count when creating a hall or session.
  - Edit seat count later.
  - Expand or contract capacity flexibly.
- Worked example: 30 seats this year, 40 seats next year.

### 2.10 Geofencing and feature gating (PDF §10)

- **"Ask a question"** is visible only to users physically inside the forum
  area or inside a session.
- Gating uses geolocation **or** sign-in permissions.

### 2.11 Mockup modifications (PDF §11)

References Mockup pages by number:

| Mockup page | Required change                                  |
|-------------|--------------------------------------------------|
| 7           | Flagged for discussion and review (not actioned) |
| 18          | Hall / session creation by seat capacity (see §2.9) |
| 21          | Needs additional explanation before implementation |
| 26 and 27   | **Delete:** public comments and meeting request  |
| 39          | **Delete:** entire page                          |

The Mockup file is not in this repository; actioning these items requires the
Mockup deliverable from the external UI/UX designer.

### 2.12 General technical notes (PDF §12)

- All data and content must be dynamic and editable.
- The system must be flexible and expandable.
- Content and permissions are managed without programmer intervention.
- An operation log is preferred for changes and approvals.
- Security standards and user-data protection are respected.

## 3. Gap against the current `feature/login-api` baseline

### 3.1 Shipped (no work needed)

| PDF requirement                                  | Shipped as                                                  |
|---|---|
| Email + 6-digit OTP sign-in (§2.5)                | `SignInService` + `IAccountCodeRepository`                  |
| Visitor / Other user types (§2.5)                 | `UserType` enum (D-048)                                     |
| Admin-curated ProfileTypes per user type (§2.2, §2.5) | `ProfileType` lookup CRUD (D-115)                       |
| Distinct colour per ProfileType (§2.2, §2.5)      | `ProfileType.PageColor` column + CP picker (D-120)          |
| Required reg fields — Arabic + English name, nationality, DOB, NationalId, Iqama, Passport, Email, Saudi/Intl mobile (§2.6) | `UserProfile` columns (D-046, D-152) |
| ID-image upload, encrypted at rest (§2.6)         | `EncryptedUserIdDocumentStorage` (D-046b)                   |
| Avatar / profile photo (§2.6)                     | Avatar storage and CRUD (D-039)                             |
| Approval workflow + reject reason (§2.7.1)        | `AccountState` transitions, bilingual rejection (D-051)     |
| Permission framework (§2.7)                       | `Permission` + `RolePermission` (D-148)                     |
| Interests at registration (§2.8)                  | M-to-M `UserProfile.Interests` (P9, D-050)                  |
| Hall entity with seat capacity (§2.9)             | `Hall` admin CRUD (D-134)                                   |
| Operation log (§2.12)                             | `OperationLogEntry` + `RowAudit` + actor snapshot (D-109, D-158) |
| Roles (RBAC) framework (§2.7)                     | `SimfRole` + roles claim (D-040)                            |

### 3.2 Partial — divergent shape, needs alignment

| PDF requirement | Current state | Gap |
|---|---|---|
| "Select all" bulk approve (§2.7.1) | Per-row approve/reject only | New CP affordance + service `BulkApproveAsync`. Recorded as **OI-9** in FDS-002. |
| Job title (§2.6 optional) | Not modeled | Add `JobTitle string?` to `UserProfile`; admin form + visitor form pick it up. |
| Rename "Agenda" → "Sessions" (§2.9) | App-DB carries `Theme` for content pillars; a Session entity does not exist yet. The PDF's "Sessions" is the actual scheduled talk, not the theme. | Add a `Session` entity (with `HallId`, `SpeakerId`, time window) and a CP CRUD. Keep `Theme` as the pillar concept, distinct from a `Session`. |
| **Moderator naming collision** (§2.7.2 vs D-161) | PDF §2.7.2 المحاور = session-question moderator (Q&A in a live session). D-161 `MobileAppRole.Moderator` = mobile-app content/user authority (broader). | These are two different roles. Rename one. Recommendation: keep `MobileAppRole.Moderator` for the in-app authority, introduce a separate `SessionModerator` permission (on a per-session basis) for the question-moderation workflow. |
| Smart recommendations (§2.8) | Interests data captured; engine not built | Build "Meet people like you" matcher service over the Interests table. |
| Bulk row colour per registration type (§2.2) | `ProfileType.PageColor` exists | Confirm the visitor-app surface actually consumes the colour; document in CPD-001. |

### 3.3 Missing entirely — needs design + build

- **Dynamic content CMS** (§2.1) — `ContentBlock` (or similar) table of key/value markdown blocks, editable from CP; admin form per page region; site/app reads via a typed contract. Includes welcome message, banners, announcements, brand colour tokens, page management.
- **Registration open/close toggle** (§2.3) — `RegistrationGate` table (or a singleton row) with `IsOpen`, `AutoCloseUtc`, `LastChangedAt`. Sign-up endpoints honour the gate. Admin toggle in CP. Background worker enforces the `AutoCloseUtc` flip.
- **Archive visibility toggle** (§2.4) — single `ArchiveVisibility` switch. App contract `GET /archive/visibility`.
- **Face ID sign-in** (§2.5) — Flutter-side biometric unlock that releases a stored refresh token; backend stays JWT-based. Requires a "device key" registration ceremony on the first sign-in.
- **Session moderator role + question workflow** (§2.7.2) — `SessionQuestion` table, public submission endpoint (gated by §2.10 below), moderator endpoints (list/hide/reorder/push to speaker). Maps to a per-session permission grant — distinct from `MobileAppRole.Moderator`.
- **Public-relations team role** (§2.7.3) — `Invitation` table, VIP list, attendance tracking, guest-targeted messaging. New role + permissions. Out of scope of the gates module; lives alongside notifications.
- **"Meet people like you" engine** (§2.8) — interest-intersection ranker. Read-only service over `UserProfile.Interests`.
- **Sessions module** (§2.9) — `Session` entity (`HallId`, `SpeakerIds[]`, time window, capacity inherited from hall or overridden), CRUD in CP, public read for the app.
- **Geofenced "Ask a question"** (§2.10) — accepts client lat/lon + a server-side boundary geometry, or relies on a venue-WiFi SSID hint. Decide which input the staff app actually has on hand.
- **Default seed of "Media" and "Sponsor" Other-tier ProfileTypes** (§2.5) — extend `IdentitySeeder.SeedProfileTypesAsync`.
- **Mockup modifications** (§2.11) — pages 7 / 21 / 26 / 27 / 39 — requires the Mockup file from the UI/UX designer.

## 4. Conflicts with prior controlled documents

### 4.1 `SIMF-FDS-002` — open items partially closed by this PDF

- **OI-6** ("decide the dynamic ProfileTypes seed set for v1") is **partially closed** by this PDF: Other-tier seeds expand to include `Media` and `Sponsor`. Visitor-tier seeds remain owner-pick.
- **OI-9** ("bulk approve / bulk reject on the pending pages") is **mandated** by this PDF (§2.7.1 "Select all"). The OI moves from "decide whether" to "scheduled — see this gap doc §5 phase 2".

### 4.2 `SIMF-FDS-002` Amendment B and D-161 — naming collision

- The "Moderator" wording in this PDF (§2.7.2) refers to a session-question moderator, not the broader in-app content authority `MobileAppRole.Moderator` introduced by D-161. These are two distinct concepts and must not share a single label in any user-facing surface or controlled document. See gap §5 phase 2 for the rename plan.

### 4.3 `SIMF-CPD-001` — Themes vs Sessions

- The CP carries "Themes & pillars" as the editable label for `Theme` rows. The PDF's "Sessions" (§2.9) is the run-of-show schedule of talks, not the thematic pillars. Both concepts can coexist; the documentation must be explicit. CPD-001 needs a section that distinguishes the two.

### 4.4 `SIMF-MAA-001` §8.1 (mobile_app_role claim)

- The four-tier claim (None / Visitor / Staff / Moderator) shipped under D-161 is **not contradicted** by this PDF. The PDF's session-moderator authority is a per-session permission grant, not a global JWT claim. The MAA-001 §8.1 mapping stays as is; the session-moderator workflow consumes a separate authorization handler.

### 4.5 `SIMF-OLD-DRAFT-CONFLICTS.md`

- The `15-04-2024` folder is otherwise still "superseded drafts" per `CLAUDE.md`. This single PDF is promoted to authoritative for forward planning. The conflicts file should record this exception so future engineers do not silently skip it.

## 5. Phased plan to close the gaps

Phases are priority-ranked. Within a phase, items can ship in any order; the
phase as a whole closes a coherent set of capabilities. No phase below has
been started, scoped, or approved for build — this document only proposes
the order. The owner approves each phase before it begins.

### Phase G1 — Quick wins (≤ 1 day of work each)

1. `UserProfile.JobTitle string?` column + admin form field + visitor form
   field. Migration on `SimfIdentityDbContext`. Resx EN + AR.
2. Default seed entries for `Media (إعلامي)` and `Sponsor (راعي)` Other-tier
   `ProfileTypes` in `IdentitySeeder.SeedProfileTypesAsync`, both with
   `MobileAppRole = None` and a distinct `PageColor`.
3. CPD-001 documentation update — distinguish `Theme` (pillar) from
   `Session` (scheduled talk), and confirm the "Themes & pillars" label
   stays for the pillar concept.
4. FDS-002 OI-6 + OI-9 status update reflecting this gap doc.

### Phase G2 — Bulk approve + name disambiguation

1. **Bulk approve** for the security team:
   - New service method `BulkApproveAsync(ids, actorUserId, ct)`.
   - New endpoint `POST /api/v1/admin/visitors/bulk-approve`.
   - CP grid affordance — "Select all" + bulk action toolbar on
     `PendingVisitors.razor`.
   - Resx EN + AR.
   - Tests covering: 100-row batch, partial failure (one of N invalid),
     audit log (one row per subject).
2. **Session-moderator vs MobileAppRole.Moderator** rename:
   - Introduce a per-session `Permission.SessionModerate` permission and a
     per-session `SessionModerator` assignment table.
   - Document the distinction in MAA-001 §8.1 (add a callout) and FDS-002.
3. Add the doc-only note recording that `D:\SIMF\System\15-04-2024\` is
   superseded **except** for the named requirements PDF.

### Phase G3 — Sessions module

1. `Session` entity in `SimfAppDbContext` — `Id`, `Code`, `Title`,
   `TitleArabic`, `HallId` (FK), `StartUtc`, `EndUtc`, `CapacityOverride`,
   `IsActive`, `CreatedAt`.
2. M-to-M `Session ↔ Speaker` via `SessionSpeaker` join. FK to the existing
   `Speaker` entity.
3. M-to-M `Session ↔ Theme` via `SessionTheme` join (so a session inherits
   colour tokens from its pillar).
4. Admin CRUD: list / add / edit / details / deactivate.
5. CP UI: `SessionsList.razor` + `SessionForm.razor` with hall + speaker +
   theme pickers.
6. Resx EN + AR for the new keys.
7. Rename "Agenda" → "Sessions" on the public-facing surfaces (mobile app +
   website); CP label stays "Sessions" already.

### Phase G4 — Registration gate + archive visibility

1. `RegistrationGate` singleton row in `SimfAppDbContext` — `IsOpen`,
   `AutoCloseUtc`, `LastChangedAt`, `LastChangedByUserId`.
2. Sign-up endpoint reads the gate; returns a typed
   `REGISTRATION_CLOSED` 403 when closed.
3. Admin endpoint `PUT /api/v1/admin/registration-gate` to toggle / set
   `AutoCloseUtc`.
4. Background worker that flips `IsOpen=false` when `AutoCloseUtc` passes.
5. `ArchiveVisibility` singleton with `IsVisible`. Public `GET
   /api/v1/archive/visibility`.
6. CP toggles for both, with audit-log entries.
7. Resx EN + AR.

### Phase G5 — Public-relations role + invitations

1. New baseline role `PublicRelations` + permissions:
   `Invitations.Manage`, `Vips.View`, `Vips.Notify`.
2. `Invitation` entity — sent-by, sent-to (UserProfileId), state
   (`Pending`/`Confirmed`/`Declined`), notes.
3. Admin CRUD for invitations.
4. VIP-list view filtered to `ProfileType.Name in {VVIP, VIP}`.
5. Outbound message stub — reuses the existing `EmailQueue` + future SMS
   adapter.

### Phase G6 — Session question moderation

1. `SessionQuestion` entity — `SessionId`, `SubmittedByUserId`,
   `QuestionText`, `Order`, `IsHidden`, `IsPushed`, `CreatedAt`.
2. Public submission endpoint `POST
   /api/v1/sessions/{sessionId}/questions`, gated by the §G7 geofence /
   registration check.
3. Moderator endpoints: list / hide / reorder / push.
4. Per-session permission grant: assign moderators per `Session.Id`.
5. CP page for session moderators to manage live Q&A.

### Phase G7 — Geofencing for "Ask a question"

1. Decide the input source — client lat/lon, venue-WiFi SSID, or a manual
   "I'm at the venue" toggle. The PDF leaves the decision open between
   geolocation and sign-in permissions; the owner chooses.
2. `VenueBoundary` table or hard-coded polygon per `Hall`. Server-side
   point-in-polygon check on every question submission.
3. Error code `OUT_OF_VENUE` returned when the check fails.

### Phase G8 — Dynamic content CMS

1. `ContentBlock` entity — `Key` (unique slug, e.g. `home.welcome.title`),
   `ContentEn`, `ContentAr`, `LastUpdatedByUserId`, `LastUpdatedAt`.
2. Admin CRUD + a per-page editor surface in CP that groups blocks by page.
3. App + website read via a typed contract; the client caches with an
   `If-Modified-Since` header on each fetch.
4. Banners + announcements as a separate `Banner` entity with
   start / end timestamps and ordering.
5. Brand-colour tokens land on the existing `theme.tokens.css` editor flow
   (CPD-001 §8.4 already anticipates this).

### Phase G9 — Smart recommendations engine

1. Read-only service `IRecommendationService.MeetPeopleLikeYouAsync(userId,
   ct)` — returns top N users with the highest interest-intersection score
   plus profile-type compatibility.
2. Public endpoint `GET /api/v1/account/recommendations/meet-like-you`.
3. Flutter screen wired to it.
4. Cache the materialised matches per user with a 24-hour TTL.

### Phase G10 — Face ID sign-in

1. Device-key registration ceremony on first successful sign-in: app
   generates an asymmetric key pair, stores the private half behind the
   device biometric, sends the public half to a new
   `POST /api/v1/auth/device-keys` endpoint.
2. Subsequent sign-in: app signs a server-issued challenge with the private
   key after a biometric prompt, posts to
   `POST /api/v1/auth/sign-in-with-device-key`. Backend verifies the
   signature against the stored public key and issues a normal JWT pair.
3. Device-key revocation surface (admin + self-service).

### Phase G11 — Mockup-driven changes

1. Obtain the Mockup file from the external UI/UX designer (deliverable
   gate).
2. Action pages 7 / 21 / 26 / 27 / 39 per PDF §2.11.

## 6. Open items raised by this gap document

| ID | Open item | Phase to resolve |
|----|-----------|------------------|
| G-OI-1 | Confirm "Sessions" replaces "Agenda" on **every** user-facing surface (app, website, CP labels). | G3 |
| G-OI-2 | Decide the source of truth for "is the user inside the venue" — geolocation, venue-WiFi SSID, or a self-asserted toggle. | G7 |
| G-OI-3 | Decide whether brand-colour tokens edit lands on `theme.tokens.css` directly (developer-mediated) or through a CP token editor (admin-mediated). | G8 |
| G-OI-4 | Decide whether the public-relations team gets its own CP layout or shares the existing System group. | G5 |
| G-OI-5 | Decide the session-capacity precedence: hall-default vs session-override. | G3 |
| G-OI-6 | Mockup file is missing from the repository; secure it from the designer before phase G11 starts. | G11 |

---

End of document.
