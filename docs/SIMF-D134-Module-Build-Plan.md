# SIMF D-134 — Module Build Plan

| | |
|--|--|
| **Document ID** | SIMF-D134-MBP-001 |
| **Status** | Draft for owner review |
| **Authored** | 2026-05-29 |
| **Authority** | Decision D-134 (this document) |
| **Related** | D-110 (schema freeze), D-117 + D-132 (canonical CRUD pattern), [`PAGE-INDEX.md`](pages/PAGE-INDEX.md), SIMF-FDS-002 through SIMF-FDS-012 |

## 0. Why this document exists

The Control Panel ships 22 nav entries that currently resolve to
`ModulePlaceholder` (the "Coming soon" stub) — see the D-132 audit and
the rows tagged 🚧 Stub in [`PAGE-INDEX.md`](pages/PAGE-INDEX.md). This
document is the **single master plan** for turning those 22 stubs into
real modules. It does NOT touch source code; it specifies what to build,
in what order, and against which controlled-document requirements.

Per CLAUDE.md §1.9 (NEVER edit code without explicit approval) + §11
(pre-approval block) + §14 (destructive / hard-to-reverse actions), this
plan exists so the owner can approve the work BEFORE any new EF entity,
migration, or endpoint lands.

## 1. The D-110 freeze constraint

D-110 (2026-05-26) froze the EF schema:

> "EF schema — the InitialCreate migrations on both SimfIdentityDbContext
> and SimfAppDbContext capture the final schema. No more schema changes;
> any future column / table / index addition must be argued for as a
> breaking change."

Twenty of the 22 stub modules need **new tables** (Themes, Sessions,
Halls, Speakers, Bookings, Exhibitors, Booths, Sponsors, Venue map,
Live sessions, Moderation, FAQ, AI settings, Media, News, Previous
editions, broadcast Notifications, Configuration, Settings — and
Registration requests + Attendees as queue views over new tables).
**Building them breaches D-110.**

Two modules — **Roles** (`/m/roles`) and **Operation log**
(`/m/operation-log`) — can be implemented over **existing tables**
without a migration. They are explicit Path 2 candidates (§4 below).

**This plan therefore proposes a successor decision — D-135 — that
lifts D-110's freeze in scope for D-134.** §6 below drafts D-135 for
owner review.

## 2. Inventory of the 22 stub modules

Grouped by CP nav section + FDS source. "Path" column maps each module
to one of:

- **P2** — fits existing schema; ship without a migration.
- **P1.lift** — needs a new table; requires D-135 freeze-lift to land.

| # | Module | Route | Nav group | FDS source | Path | Effort |
|---|--------|-------|-----------|------------|------|--------|
| 1 | Registration requests | `/m/registration-requests` | People | SIMF-FDS-002 | P1.lift | M |
| 2 | Attendees | `/m/attendees` | People | SIMF-FDS-003 | P2-derived (view over existing UserProfile + audit) | M |
| 3 | Roles | `/m/roles` | People | SIMF-CPD-001 OI-3 | **P2** | S |
| 4 | Themes | `/m/themes` | Programme | SIMF-FDS-004 §5.1 | P1.lift | S |
| 5 | Sessions | `/m/sessions` | Programme | SIMF-FDS-004 §5.4 | P1.lift | L |
| 6 | Halls | `/m/halls` | Programme | SIMF-FDS-004 §5.2 | P1.lift | S |
| 7 | Speakers | `/m/speakers` | Programme | SIMF-FDS-004 §5.3 | P1.lift | M |
| 8 | Bookings | `/m/bookings` | Programme | SIMF-FDS-005 | P1.lift | L |
| 9 | Exhibitors | `/m/exhibitors` | Exhibition | SIMF-FDS-006 | P1.lift | M |
| 10 | Booths | `/m/booths` | Exhibition | SIMF-FDS-006 | P1.lift | S |
| 11 | Sponsors | `/m/sponsors` | Exhibition | SIMF-FDS-006 | P1.lift | S |
| 12 | Venue map | `/m/venue-map` | Exhibition | SIMF-FDS-006 | P1.lift | M |
| 13 | Live sessions | `/m/live-sessions` | Engagement | SIMF-FDS-007 | P1.lift | L |
| 14 | Moderation | `/m/moderation` | Engagement | SIMF-FDS-007 | P1.lift | M |
| 15 | FAQ | `/m/faq` | Knowledge | SIMF-FDS-008 | P1.lift | S |
| 16 | AI settings | `/m/ai-settings` | Knowledge | SIMF-FDS-008 | P1.lift | M |
| 17 | Media | `/m/media` | Content | SIMF-FDS-010 | P1.lift | M |
| 18 | News | `/m/news` | Content | SIMF-FDS-010 | P1.lift | M |
| 19 | Previous editions | `/m/previous-editions` | Content | SIMF-FDS-010 | P1.lift | S |
| 20 | broadcast Notifications | `/m/notifications` (re-add) | Communications | SIMF-FDS-009 | P1.lift | M |
| 21 | Configuration | `/m/configuration` | System | SIMF-FDS-012 | P1.lift | M |
| 22 | Operation log | `/m/operation-log` | System | existing `OperationLogEntry` | **P2** | S |
| 23 | Settings | `/m/settings` | System | SIMF-FDS-012 | P1.lift | M |

> **Note:** entries 22 + 23 share the System group with the existing
> `/admin/admins`, `/admin/others`, `/admin/visitors`, `/admin/interests`,
> `/admin/profile-types/{visitor,other}`, `/admin/reset-2fa`, `/admin/logs`
> — those are already real and not on this list.

Effort scale: **S** ≤ 1 day, **M** 1–3 days, **L** 3–5 days per module
(counting domain + EF + Application + Api + ApiClient + BFF + Razor page
+ resx + tests + docs as per Developer Guide §24's 22-step checklist).

**Total estimate (P1.lift = 20 modules):** ~60–80 developer-days.
**Path 2 (Roles + Operation log):** ~2 days.

## 3. Per-module specifications

Each spec follows the same shape: **Purpose** (from the FDS) → **Domain
entities** (proposed) → **Endpoints** → **CP page** → **Resx keys**
→ **Dependencies on other modules**. The specs are deliberately
FDS-derivative — see the linked FDS for the authoritative requirements.

### 3.1 People modules

#### 3.1.1 Roles — `/m/roles` (P2 — existing schema)

- **Purpose:** Administrator CRUD over `SimfRole` + the
  `RolePermission` join. List built-in (`IsBaseline = true`) + custom
  roles; create / rename custom roles; assign permissions; assign
  roles to users.
- **Domain (existing — no migration):**
  - `SimfRole` (Identity context) — `Id`, `Name`, `NormalizedName`,
    `IsBaseline`.
  - `Permission` — `Id`, `Page`, `Action`, `Code`, `DisplayName`.
  - `RolePermission` — `RoleId`, `PermissionId`.
- **Endpoints (new — no schema impact):**
  - `POST /api/v1/admin/roles/list` → `ApiResult<GridPage<AdminRoleSummary>>`
  - `POST /api/v1/admin/roles` (create custom)
  - `PUT /api/v1/admin/roles/{id}` (rename)
  - `DELETE /api/v1/admin/roles/{id}` (only when `!IsBaseline` and no
    user holds it)
  - `GET /api/v1/admin/roles/{id}/permissions` → `ApiResult<PermissionGrantSet>`
  - `PUT /api/v1/admin/roles/{id}/permissions` (replace grants)
  - `POST /api/v1/admin/users/{id}/roles` (assign)
  - `DELETE /api/v1/admin/users/{id}/roles/{roleId}` (revoke)
- **CP page:** canonical D-117/D-132 CRUD grid; per-row **Details**
  opens the bilingual permission-tree picker (Page → Action tree built
  from `Permission` rows); per-row Delete only enabled when
  `!IsBaseline && UserCount == 0`.
- **Resx (~25 keys × 2 locales):** `Module.Roles.*`, `Admin.Roles.*`
  (Title, Add, Edit, Details, Delete, Field.{Name, IsBaseline,
  Permissions}, Action.*, NotDeletable.{Baseline,InUse}).
- **Tests:** `tests/SIMF.Api.Tests/AdminRolesTests.cs` — list,
  create custom, rename, delete-baseline-rejected, delete-in-use-rejected,
  assign permission, revoke permission, audit rows.
- **Docs:** `docs/pages/cp/admin-roles.md` (use `_TEMPLATE.md`),
  `Admin-Manual.md § 4.4`, `docs/tests/e2e/cp-admin-roles.md`,
  UCS-001 § UC-ROL-*.
- **Dependencies:** none — pure P2.
- **Risk:** Low (no schema, no migration).

#### 3.1.2 Operation log viewer — `/m/operation-log` (P2 — existing schema)

- **Purpose:** Browse the `OperationLogEntry` audit table. The existing
  `/admin/logs` viewer (D-117 §11.1) is for technical Serilog files;
  this page is for business/audit events (sign-in, registration,
  approval, password change, etc.).
- **Domain (existing — no migration):** `OperationLogEntry` already
  exists in the App context.
- **Endpoints (new):**
  - `POST /api/v1/admin/operation-log/list` with filter
    `{ActorId?, Event?, Outcome?, From?, To?, Search?}` →
    `ApiResult<GridPage<OperationLogSummary>>`.
  - `GET /api/v1/admin/operation-log/{id}` → full detail
    (includes correlation id, source IP, user-agent).
  - `GET /api/v1/admin/operation-log/export` → XLSX of filtered set.
- **CP page:** canonical CRUD list (read-only — no Add/Edit/Delete
  toolbar; only Filter + Details + Export). Per-row Details modal
  shows the full entry. Filter row above the grid.
- **Resx (~20 keys × 2 locales):** `Module.OperationLog.*`,
  `Admin.OperationLog.{Title,Filter.*,Column.*,Details.*}`.
- **Tests:** `AdminOperationLogTests.cs` — list with filter, paging,
  export shape, auth gate.
- **Docs:** `docs/pages/cp/admin-operation-log.md`,
  `Admin-Manual.md § 10.12`, E2E + UCS entries.
- **Dependencies:** none — pure P2.
- **Risk:** Low.

### 3.2 P1.lift modules (require D-135)

The remaining 20 modules each need a new entity (or in some cases a
small entity cluster) and therefore breach D-110. Per-module specs at
the level needed to argue D-135. Format trimmed for brevity — every spec
references its FDS for full requirements.

#### 3.2.1 Registration requests — `/m/registration-requests` (FDS-002)

- **Purpose:** Queue of self-registered visitor + Other accounts in
  `PendingApproval` that haven't filled their profile yet (the existing
  `/admin/{visitors,others}/pending` queues cover the "profile filled,
  awaiting review" case). New shape because the request-only state
  isn't surfaced today.
- **New entity:** `RegistrationRequest { Id, UserId, RequestedAt,
  SubmittedAt?, ChannelKind (Web/Mobile), IpAddress }` — optional
  intermediate step. Alternative: reuse `SimfUser.CreatedAt`
  + virtual filter "no UserProfile row yet". Decision: filter-only
  variant ships zero new tables; recommended.
- **Endpoints:** `POST /admin/registration-requests/list`,
  `POST /admin/registration-requests/{id}/nudge` (sends a follow-up
  email; uses existing `IEmailQueue`).
- **Verdict:** Could ship as **P2-derived** (filter-only) if owner
  accepts no per-request audit row. Otherwise P1.lift for the new entity.

#### 3.2.2 Attendees — `/m/attendees` (FDS-003)

- **Purpose:** Combined attendee roster — every Approved Visitor +
  Approved Other across all profile-types. Pure read view.
- **No new entity needed.** Uses `UserProfile` + `SimfUser` + `ProfileType`.
- **Endpoints:** `POST /admin/attendees/list` with filter
  `{Kind?, ProfileTypeId?, Search?, From?, To?}` →
  `ApiResult<GridPage<AttendeeSummary>>`.
- **Page:** canonical read-only CRUD grid (Filter + Details + Export).
- **Verdict:** **P2-derived** — no schema change.

#### 3.2.3 Themes — `/m/themes` (FDS-004 §5.1)

- **Purpose:** Programme themes / pillars. Parent of sessions.
- **New entity:** `Theme { Id, Code, Name, NameArabic, Description,
  DescriptionArabic, DisplayOrder, PageColor, IsActive, CreatedAt,
  UpdatedAt }`.
- **Endpoints:** canonical CRUD over `/admin/themes/*`.
- **Page:** canonical CRUD grid; PageColor uses the D-120 swatch.

#### 3.2.4 Sessions — `/m/sessions` (FDS-004 §5.4)

- **Purpose:** The programme schedule. Most complex CRUD on the plan.
- **New entities:**
  - `Session { Id, Title, TitleArabic, ThemeId, HallId, StartsAtUtc,
    EndsAtUtc, Capacity, Description, DescriptionArabic,
    IsLive, SecondFactorRequiredForCheckin, ParentSessionId?,
    Status (Draft/Published/Cancelled), CreatedAt, UpdatedAt }`.
  - `SessionSpeaker { SessionId, SpeakerId, IsKeynote, DisplayOrder }`
    (join — speakers have many-to-many with sessions).
- **Endpoints:** full CRUD + `POST /admin/sessions/{id}/publish` +
  `POST /admin/sessions/{id}/cancel` + `POST /admin/sessions/{id}/speakers`.
- **Page:** CRUD list + per-row Details with speakers chip multi-select
  + dedicated Schedule view (timeline by hall).
- **Dependencies:** Themes (3.2.3), Halls (3.2.5), Speakers (3.2.6).

#### 3.2.5 Halls — `/m/halls` (FDS-004 §5.2)

- **New entity:** `Hall { Id, Code, Name, NameArabic, Capacity,
  Floor, EquipmentNotes, IsActive }`.
- **Endpoints:** canonical CRUD.

#### 3.2.6 Speakers — `/m/speakers` (FDS-004 §5.3)

- **New entity:** `Speaker { Id, EnglishName, ArabicName, TitleEn,
  TitleAr, BioEn, BioAr, PhotoUrl, OrganisationEn, OrganisationAr,
  IsActive, DisplayOrder }`.
- **Endpoints:** canonical CRUD + photo upload + per-speaker session list.

#### 3.2.7 Bookings — `/m/bookings` (FDS-005)

- **New entities:**
  - `Booking { Id, UserId, SessionId, BookedAt, Status (Booked/Waitlisted/
    Cancelled/Attended/NoShow), QueuePosition?, NotifiedAt? }`.
  - `Attendance { Id, BookingId, CheckInAt, CheckOutAt?, CheckInQrId,
    CheckedInByUserId }`.
- **Endpoints:** queue browse + bulk check-in + override + waitlist
  promotion + cancel-with-reason.

#### 3.2.8 Exhibitors — `/m/exhibitors` (FDS-006)

- **New entity:** `Exhibitor { Id, CompanyName, CompanyNameArabic,
  ContactName, ContactEmail, ContactPhone, BoothId?, SponsorTierId?,
  LogoUrl, IsActive }`.

#### 3.2.9 Booths — `/m/booths` (FDS-006)

- **New entity:** `Booth { Id, Code, Hall, Position, SizeSqm, Status
  (Available/Reserved/Occupied), ExhibitorId? }`.

#### 3.2.10 Sponsors — `/m/sponsors` (FDS-006)

- **New entities:**
  - `SponsorTier { Id, Name, NameArabic, DisplayOrder, AccentColor }`
    (Platinum / Gold / Silver, etc.).
  - `Sponsor { Id, Name, NameArabic, TierId, LogoUrl, WebsiteUrl,
    IsActive }`.

#### 3.2.11 Venue map — `/m/venue-map` (FDS-006)

- **New entity:** `VenueMapAsset { Id, Hall, FloorImagePath,
  ImageContentType, Width, Height, CreatedAt }` + per-booth coordinate
  overlay (could fold into `Booth.X, Booth.Y`).

#### 3.2.12 Live sessions — `/m/live-sessions` (FDS-007)

- **Purpose:** Real-time view of currently-live sessions; engagement
  controls (poll, Q&A, chat).
- **New entities:**
  - `LiveSessionState { SessionId (FK), CurrentPollId?, QuestionQueueOpen,
    LiveSince, LastEngagementSnapshot }`.
  - `Poll { Id, SessionId, Title, Options[], IsClosed, CreatedAt }`.
  - `Question { Id, SessionId, FromUserId, BodyEn, BodyAr, Votes,
    Status (Pending/Asked/Skipped), AskedAt? }`.
- **Dependencies:** Sessions (3.2.4). Real-time push is not implemented; the
  question stream is read over REST.

#### 3.2.13 Moderation — `/m/moderation` (FDS-007)

- **New entity:** `ContentReport { Id, ReporterUserId, TargetType
  (Question/ChatMessage/Profile), TargetId, ReasonCode, ReasonText,
  Status (Open/Resolved/Dismissed), AssignedToUserId?, CreatedAt,
  ResolvedAt? }`.

#### 3.2.14 FAQ — `/m/faq` (FDS-008)

- **New entities:**
  - `FaqGroup { Id, Title, TitleArabic, DisplayOrder, IsActive }`.
  - `FaqEntry { Id, GroupId, Question, QuestionArabic, Answer,
    AnswerArabic, DisplayOrder, IsActive }`.

#### 3.2.15 AI settings — `/m/ai-settings` (FDS-008)

- **New entity:** `AiAssistantSettings { Id (singleton, value=1), Model,
  Temperature, SystemPromptEn, SystemPromptAr,
  KnowledgeSourceUrlsJson, IsEnabled, UpdatedAt, UpdatedByUserId }`.
- **Page:** single-row settings form, no grid.

#### 3.2.16 Media — `/m/media` (FDS-010)

- **New entities:**
  - `MediaAsset { Id, Kind (Photo/Video), TitleEn, TitleAr,
    DescriptionEn, DescriptionAr, OriginalFileName, StoragePath,
    ContentType, FileSizeBytes, Width?, Height?, DurationSeconds?,
    UploadedAt, UploadedByUserId, IsPublished, PublishedAt? }`.
  - `MediaTag { MediaId, Tag }` (join).

#### 3.2.17 News — `/m/news` (FDS-010)

- **New entity:** `NewsArticle { Id, TitleEn, TitleAr, SlugEn, SlugAr,
  BodyEn, BodyAr, HeroImageId?, AuthorUserId, Status
  (Draft/Published/Archived), PublishedAt?, CreatedAt, UpdatedAt }`.

#### 3.2.18 Previous editions — `/m/previous-editions` (FDS-010)

- **New entity:** `PreviousEdition { Id, Year, ThemeEn, ThemeAr,
  HighlightsEn, HighlightsAr, ArchiveUrl, IsPublished }`.

#### 3.2.19 broadcast Notifications — `/m/notifications` (FDS-009)

- **Purpose:** Admin → audience push. Different from `/account/notifications`
  (the per-user inbox).
- **New entities:**
  - `NotificationBroadcast { Id, TitleEn, TitleAr, BodyEn, BodyAr,
    Severity, AudienceFilterJson (UserTypes, ProfileTypeIds,
    AccountStates, SessionIds), ScheduledAt?, SentAt?, SentCount,
    CreatedByUserId, Status (Draft/Scheduled/Sending/Sent/Cancelled) }`.
- **Dispatcher fan-out** populates the existing per-user `Notification`
  table — uses the existing primitive.

#### 3.2.20 Configuration — `/m/configuration` (FDS-012)

- **Purpose:** Edition-level config (dates, branding, locales,
  feature flags).
- **New entity:** `EditionConfiguration { Id (singleton), Year,
  StartDate, EndDate, TimezoneId, LogoLightUrl, LogoDarkUrl,
  BrandPrimary, DefaultCulture, EnabledLocalesJson, FeatureFlagsJson,
  UpdatedAt, UpdatedByUserId }`.
- **Page:** single-row settings form.

#### 3.2.21 Settings — `/m/settings` (FDS-012)

- **Purpose:** System-level config (SMTP, integrations, file storage,
  rate limits).
- **New entity:** `SystemSettings { Id (singleton), SmtpHost, SmtpPort,
  SmtpUserEncrypted, SmtpPasswordEncrypted, StorageRoot,
  RateLimitProfileJson, IntegrationsJson, UpdatedAt, UpdatedByUserId }`.
- **Security:** secrets encrypted at rest via ASP.NET Data Protection.

## 4. Path 2 (no freeze impact) — what we ship now

Without owner approval to lift D-110, only these can land safely in the
next sprint commit:

| # | Module | Spec § | Sprint scope |
|---|--------|--------|--------------|
| 3 | Roles | 3.1.1 | Full CRUD + permission picker + assign-to-user. |
| 22 | Operation log | 3.1.2 | Read-only viewer + filter + Details modal + Export. |
| 2 | Attendees (variant) | 3.2.2 | Read-only roster view over existing UserProfile. |
| 1 | Registration requests (variant) | 3.2.1 | Filter-only view, no per-request entity. Owner sign-off required on the no-extra-audit-row trade-off. |

The 18 P1.lift modules wait on D-135 (next section). Document-wise, all
22 modules now have full per-page docs + manual chapters once they
ship — the docs scaffolding from D-133 makes that incremental.

## 5. Sprint roadmap (proposed)

Assuming D-135 is approved on the same review cycle as this plan:

### Sprint A (P2-only — 1 week)
- Roles full CRUD
- Operation log viewer
- Attendees roster
- Registration requests (filter-only variant)
- 4 per-page docs + 4 manual chapters + 4 E2E catalogue files + 4 UC entries.

### Sprint B (Programme group — 2 weeks)
- Themes, Halls (in parallel — small)
- Speakers (M — depends on photo upload primitive)
- Sessions (L — depends on Themes + Halls + Speakers)
- Bookings (L — depends on Sessions; ties into D-127's QR badge system)

### Sprint C (Exhibition group — 1.5 weeks)
- Exhibitors, Booths, Sponsors, Venue map.

### Sprint D (Engagement + Knowledge — 2 weeks)
- Live sessions (L — depends on Sessions + RealTime hubs).
- Moderation.
- FAQ.
- AI settings.

### Sprint E (Content + Communications + System — 2 weeks)
- Media, News, Previous editions.
- broadcast Notifications.
- Configuration, Settings.

**Total: ~8.5 weeks of focused development.**

Per-sprint Definition of Done (from CLAUDE.md + Developer Guide §24):
0 Release warnings/errors, all integration tests pass, every shipped
page has a per-page doc + manual chapter + E2E catalogue + UC entry +
DECISIONS_LOG entry, browser smoke captured into `docs/screenshots/`.

## 6. Proposed D-135 — Schema-freeze lift for D-134

> **DRAFT** — paste into `docs/decisions/DECISIONS_LOG.md` if approved.

```
| D-135 | 2026-MM-DD | **D-110 freeze lifted in scope for D-134 module
build-out.** D-134 (see SIMF-D134-MBP-001) requires ~20 new EF entities
across the Programme, Exhibition, Engagement, Knowledge, Content,
Communications, and System bounded contexts. D-110 had frozen the
schema at the InitialCreate baseline because the Login API surface was
the only locked deliverable; D-134's expansion is the planned next
sprint per the SIMF Programme Plan and was always going to land. This
decision unfreezes the SimfAppDbContext schema for the duration of
D-134 sprints A–E. **Constraints carried forward:** (a) every new
entity ships with an EF configuration + migration in the same commit
that adds it; (b) every migration must be reversible (down-script
provided) or explicitly opt out under a per-decision rationale;
(c) `__EFMigrationsHistory_App` is the single migration ledger for the
app context — no parallel migration streams; (d) enum additions remain
additive-only per D-110's still-active enum-freeze clause; (e) every
new entity gets a row-audit row via the existing D-109 interceptor —
no opt-outs without rationale. **Out of scope:** the SimfIdentityDbContext
schema stays frozen (no new Identity-side tables); the existing
contract surface (ApiResult<T>, ErrorCodes, NotificationKind, etc.)
stays frozen except for additive extensions. | The freeze was always
scoped to the Login API; lifting it for the planned Programme + Exhibition
+ Engagement build-out was implicit in the SIMF Programme Plan and is
made explicit here so the team has unambiguous authority to add tables.
The five constraints carried forward preserve every D-110 benefit
(reversible migrations, single ledger, additive enums, row-audited
writes) while removing the no-new-tables block that prevented forward
motion. |
```

## 7. Per-module documentation footprint

For every module shipped, the Developer Guide §24 22-step checklist
fires. The doc artefacts that land per module:

- 1 × per-page doc under `docs/pages/cp/admin-{slug}.md` (~250 lines
  using `_TEMPLATE.md`).
- 1 × Admin Manual chapter (~80–150 lines).
- 1 × E2E catalogue file under `docs/tests/e2e/cp-admin-{slug}.md`
  (~80–150 lines).
- 1 × UCS-001 entry (~30–50 lines).
- 1 × DECISIONS_LOG entry (~50 lines).

So a sprint that ships 5 modules adds ~3 000 lines of documentation
in addition to the source code. The shape is established; the cost is
known.

## 8. Risks + mitigations

| Risk | Mitigation |
|------|------------|
| Schema bloat (20+ tables in one sprint window) | Stage by FDS group (§5 roadmap); each sprint's tables migrate atomically. |
| Cross-module dependency surprises (e.g. Bookings depends on Sessions which depends on Themes/Halls/Speakers) | §5 sequences groups so deps land before consumers. |
| Resx churn (~1 000 new keys across 20 modules × 2 locales) | Translator engagement starts in parallel with Sprint A; AR keys can stub-mirror EN until translated, but EN/AR parity gate must NOT regress. |
| D-127 walk-in wizard depends on profile-types that exist (P2 today) but Sessions / Bookings will need similar tile pickers | Reuse `ProfileTypeForm.razor` shape for the new picker components; lift them into `SIMF.Components` when used by ≥ 2 consumers. |
| `SimfDataGrid` may not cover every page (e.g. timeline-style Sessions view, venue-map editor) | Build per-domain non-grid primitives as new `Simf*` components; gate landing on a Component Catalogue update. |
| Old PendingStaff parity gap (no review modal) is still open | Roll into Sprint A as a small lift; not a new module but a coherent companion fix. |

## 9. What's NOT in this plan

- **Mobile App** (Flutter) — deferred per the Programme Plan; lands as
  its own multi-sprint effort.
- **Statistics & dashboards** (`SIMF-FDS-011`) — not in the 22-stub
  list; rolls in once `Bookings` + `Attendance` are live.
- **Badge & Access control** (`SIMF-FDS-003`) — the walk-in + reprint
  flows already cover this end-to-end via D-127 + D-130; future gate
  scanner integrations attach to existing `UserProfile.QrId`.
- **Refactoring** — D-110's freeze argued out an architectural
  refactor; that's a separate decision and not bundled here.

## 10. Approval gate

Before any source touches the schema:

1. Owner approves this document (D-134 plan) in writing
   (`feature/login-api` PR comment / Slack / email — verbatim
   "approved" suffices).
2. Owner approves the **D-135 draft** in §6 above.
3. The D-135 entry is appended to `DECISIONS_LOG.md` verbatim.
4. Sprint A starts: Path 2 modules + the small Path 2-derived ones.
5. After Sprint A ships clean (0/0 build, all tests pass, all docs
   land, smoke captured), the team moves to Sprint B and the first
   schema migration lands under D-135.

**Without owner approval of both this document AND D-135, no schema
change ships.** Roles + Operation log + Attendees + Registration
requests (filter variant) can ship today on Path 2 alone (no D-135
needed) if the owner approves Sprint A scope.

---

_Last reviewed:_ 2026-05-29 by Claude (D-134 plan kickoff).

_Waiting for owner approval to proceed._
