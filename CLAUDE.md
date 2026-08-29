# SIMF — Project Instructions

Last updated: 2026-08-12

The global rule set at `~/.claude/CLAUDE.md` (§0–§20) applies in full. This file
adds SIMF-specific pointers only. It does not restate or override the global rules.

## Authoritative source of truth

SIMF's binding rules live in the controlled documents under `docs/`. Read the
relevant document before writing code or design. A controlled document overrides
any older draft, prompt, chat note, or assumption.

| Document | Governs |
|----------|---------|
| `docs/SIMF-SES-001-Software-Engineering-Standards.md` | Engineering rulebook — structure, DDD layering, conventions, naming, source control, code review, testing, security baseline, Definition of Done, freeze |
| `docs/SIMF-API-001-API-Specification.md` | API contract — `ApiResult<T>` envelope, standard headers, error model, HTTP status codes, pagination, authentication endpoints |
| `docs/SIMF-SAD-001-Software-Architecture-Document.md` | Architecture — modular monolith, bounded contexts, security, integration, deployment |
| `docs/SIMF-MAA-001-Mobile-Application-Architecture.md` | Flutter app architecture (Android + iOS) |
| `docs/SIMF-REG-001-Registration-Rules.md` | Registration and profile rules — which fields the app demands, required vs needed-to-complete, the photo rules, approval before badge, the two desk modes. Every rule names where it is enforced and which test pins it |
| `docs/SIMF-DMP-001-Documentation-Management-Plan.md` | Documentation management |
| `docs/SIMF-Program-Plan.md` | Programme plan, stages and gates |

If two documents disagree, the more specific one wins for its area; if it is still
unclear, ask — do not guess.

## Superseded material — do NOT use

The files under `D:\SIMF\System\15-04-2024\` (`final-prompt.md`, `my-style (1).md`,
`professional-coding-agent-prompt.md`) are an early draft. Several of their rules
contradict the current controlled docs — for example HTTP-200-always, the old
response envelope, phone-OTP registration, a `Smif*` component library, and
Flutter-on-Web. They are NOT a source of truth. Use only the `docs/` controlled
documents. The conflict list is recorded in `SIMF-OLD-DRAFT-CONFLICTS.md`.

## Status

The programme is long past its initial login increment. If you find "Sprint 1", the
`feature/login-api` branch, or "next stage is User Management" in an older document,
treat it as superseded history — `docs/SIMF-Sprint1-Login-API-Completion.md` is kept
as the artefact of that increment, not as a description of where the work stands.

Solution layout (`SIMF.slnx`) — use this to route before searching the tree:

| Area | Projects |
|------|----------|
| Backend | `src/Backend/` — `SIMF.Api` (FastEndpoints), `SIMF.Application`, `SIMF.Domain`, `SIMF.Infrastructure` (EF Core + SQL Server) |
| Presentation | `src/ControlPanel/` (Blazor Server), `src/Website/` (Blazor SSR), `src/Mobile/simf_app` (Flutter), `src/Edge/SIMF.MobileEdge` (mobile presentation tier) |
| Shared | `src/Shared/` — `SIMF.Common`, `SIMF.Contracts`, `SIMF.Components`, `SIMF.ApiClient` |
| Tests | `tests/` — Api, ApiClient, Application, BadgeDesk, ControlPanel, Domain, E2E, Web, plus `perf` |

Conventions are enforced by a tool, not by prose: `tool/conventions` runs in CI, and
`docs/quality/convention-report.md` records its current state.

**Do not quote a decision-id range in this file.** `docs/decisions/DECISIONS_LOG.md` is
the live record — read its "Reading an ID" preamble first, because ids collide and one
number can label several unrelated decisions. The authoritative counts are pinned in
`tests/SIMF.Domain.Tests/DecisionsLogIntegrityTests.cs`, so they fail the build rather
than rot inside a sentence here.

For what is in flight right now, read the git log and the decisions log. A prose status
paragraph goes stale faster than anyone maintains it, which is exactly how the previous
version of this section came to describe a branch that had merged months earlier.

## Access control — per-page/per-action permissions (D-207 / D-208)

The Control Panel and admin API enforce a **per-page/per-action permission system**:
assignment is **roles-only**, permission codes are baked into the JWT, and
`Administrator = "*"` (wildcard). The single source of truth is the catalogue in
`src/Shared/SIMF.Common/PermissionCatalog.cs`. The full design + workflow + the
step-by-step playbook are in `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`
(companion: `docs/SIMF-Permission-Catalogue.md`).

**HARD RULE — a new CP page or admin API action is NOT "done" until its permission
exists, is seeded, and gates BOTH the API and the CP.** Whenever you add a Control
Panel page or a new admin endpoint/action you MUST:

1. Add the `const` code(s) to the right nested class in `PermissionCatalog` (format `Page.Action`).
2. Add `new(...)` entries to `PermissionCatalog.All` (`BaselineRoles` usually `AdminOnly`) — the seeder is idempotent, so **no migration** (the `Permission`/`RolePermission` tables pre-exist).
3. Gate the API endpoint(s): `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.X.Y), nameof(AuthorizationPolicies.RequireApprovedAccount))`.
4. Gate the CP page: `@attribute [RequirePermission(PermissionCatalog.X.Y)]`.
5. Set the `CpNavigation` item's `RequiredPermission` (`null` only for the dashboard / `IsStub` placeholders).
6. Gate every action control. Buttons the **page** writes (including inside
   `<RowActions>` and modals) get wrapped in
   `<AuthorizedAction Permission="PermissionCatalog.X.Y">`. Buttons
   **a shared component renders itself** cannot be wrapped from the page, so name
   the code on that component instead — on `SimfDataGrid`: `AddPermission` (which
   also covers Duplicate and Paste) / `EditPermission` / `DeletePermission` /
   `ImportPermission` / `ExportPermission` / `ApprovePermission` /
   `RejectPermission` (D-830); on `SimfConfirm`: `Permission`, which gates the
   Confirm button and never Cancel (D-831). In every case use the permission that
   gates the **endpoint the button calls**, which is often not the page's own code
   and not a name you can guess: on a page hosting two grids over two resources
   they differ; on `/admin/others/pending` the bulk and per-row Approve use
   different codes because the API does; and a dialog named "this will unpublish"
   may still only call the plain save endpoint.

   Never put a Razor comment `@* … *@` **inside a component tag's attribute list**.
   It compiles and then throws at render time, because Razor reads it as an
   attribute name (D-831). Put it on the line above the tag.

`tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` and
`tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` fail the build if a gate is
missing. An ungated admin page/endpoint is reachable by **any** signed-in admin
regardless of role — treat a missing permission as a security defect.

## E2E test-case catalogue (D-133 / D-245)

SIMF keeps a **per-page End-to-End test-case catalogue** under `docs/tests/e2e/`
(index: `docs/tests/e2e/README.md`; template: `_TEMPLATE.md`). One file per page
(`{cp|web|mobile}-{slug}.md`) with a Coverage matrix + concrete, data-bearing
**Gherkin** scenarios (stable ids `E2E-{NS}-{NNN}`, runner-agnostic). **Purpose:**
after a batch of fixes, an agent reads every case and drives each page — enters
real data, performs each CRUD/action, asserts each expected outcome — as a full
regression pass that proves production-readiness. The auth-setup line uses the
`Get-Totp` helper, **never a literal secret**.

**HARD RULE — a new CP page, app screen, Website page, or admin API action is NOT
"done" until its catalogue file exists, is authored (not a stub), and is indexed.**
Whenever you add or materially change a page/action you MUST:

1. Author/update `docs/tests/e2e/{cp|web|mobile}-{slug}.md` from `_TEMPLATE.md`
   (cover the golden CRUD path + every distinct function on the page + empty /
   auth-gate / validation / conflict / server-500 / RTL), grounded in the real
   fields, buttons, permissions, error codes and bilingual toast text.
2. Add the row to `docs/tests/e2e/README.md` (route → file → scenario id range).
3. Cross-link the route in `docs/pages/PAGE-INDEX.md` (doc + test columns) and
   the per-page reference doc under `docs/pages/{cp|web}/{slug}.md`.

Treat a shipped page with no authored catalogue file as an incomplete change —
the catalogue is the executable proof the page still works end-to-end.

**Companion DoD rule (D-246):** at the end of ANY new function / page / API,
update the docs (`PAGE-INDEX.md` + the per-page reference doc) **and** add the
unit + integration tests **and** the E2E catalogue file — in the same changeset.
A change is not "done" without all three.

## Data ↔ Identity DB separation (D-157 / D-246 — PERMANENT)

The system uses **two physically separated SQL Server databases** — `SIMF_Identity`
(`SimfIdentityDbContext` — users, roles, permissions, 2FA, tokens) and `SIMF_App`
(`SimfAppDbContext` — everything else). This is permanent (D-157, reaffirmed
D-246; it superseded the old one-shared-DB design C-1).

**HARD RULES — never regress this:**

1. **No cross-database relation / FK.** Never add an EF navigation or
   `HasForeignKey` between an App entity and an Identity entity (either
   direction). A cross-DB reference is a **bare `Guid`** (logical FK) resolved
   on read with a second query on the other context — never a DB constraint,
   never a cross-DB JOIN.
2. **No duplicated data.** Never persist a copy of Identity-owned data inside
   `SIMF_App` (or vice versa); resolve it on read. The **only** allowed copies
   are the existing immutable **audit snapshots** (`OperationLog` / `RowAudit` /
   `GateScan` capture the actor's display-name/email at write time so the audit
   trail is self-contained) — do not extend that pattern to live data.
3. **No cross-database transaction.** A unit of work touches one context/database
   at a time; there is no distributed transaction spanning both.

The two connection strings (`SimfIdentityDb` + `SimfAppDb`) may point at the same
instance or separate physical servers. The Identity schema is also frozen (D-110).

## FREEZE — D-110 baseline (2026-05-26)

The following surface is **frozen** as of commit `67e2263` and must NOT be
changed without explicit owner approval:

- **EF schema** — the `InitialCreate` migrations on both `SimfIdentityDbContext`
  and `SimfAppDbContext` capture the final schema. No more schema changes;
  any future column / table / index addition must be argued for as a breaking
  change.
- **Enum names + values** — every enum in `src/Shared/SIMF.Common/Enums/`
  (SignInAudience, AccountState, AccountCodePurpose, SecondFactorKind,
  UserType, AuditOutcome, RowAuditOperation, NotificationKind,
  NotificationSeverity) is frozen against **rename** and **reorder** of
  existing values. **Additive** new values (appending a new case with a
  new integer that doesn't conflict) ARE allowed as long as they don't
  shadow an existing name or value — used in D-111 to extend
  NotificationKind without breaking the wire contract.
- **Migration history** — only one `InitialCreate` per context. No further
  migrations land without owner approval.

Frontend additions, new resx strings (more languages), new endpoints,
non-schema bug fixes, and additive Options-section keys remain in scope
for normal development. The freeze applies to the persistence and enum
contract surface only.

### D-186 partial lift (2026-05-30)

Owner authorised one targeted lift of the D-110 freeze: the structural
collapse of `UserType` from `(Visitor, Other, Admin)` to `(Visitor, Admin)`
and the addition of `ProfileType.IsVisitor`. This required removing
`UserType.Other` (value `1`) and landing two new migrations on top of
the InitialCreate baseline (`App/D186_AddProfileTypeIsVisitor` +
`Identity/D186_FoldOtherUsersIntoVisitor`). Admin stayed at integer
value `2`; the `1` slot is reserved. See `docs/decisions/DECISIONS_LOG.md`
D-186 for the full rationale. No other freeze items are lifted; future
schema or enum changes still require explicit owner approval.

### D-199 broad lift (2026-05-30)

Owner authorised a broad lift of the D-110 freeze to deliver the full
App + CP + API for the event push. New EF tables/columns on
`SimfAppDbContext` are now permitted for these new/extended event modules:
News, Media gallery, Media partners, Booths (Exhibition), Sponsors,
Archive editions, Audience comments, Ratings/Feedback, Statistics
snapshots, and Live-session columns. Each lands as **additive** tables via
new migrations (one consolidated migration per build wave). The
**Identity** schema stays frozen, and the existing enums stay frozen
against **rename/reorder** (additive new values still allowed). See
`docs/decisions/DECISIONS_LOG.md` D-199 for the rationale and the four
owner decisions taken with it (freeze lift; provider-stub for live/AI;
exhibitor/sponsor = CP-only Company + accounts; 2D venue map).

### D-211 programme freeze-lift (2026-05-31)

Owner authorised a further freeze-lift to deliver the "finish all
remaining stubs + open gap items" programme. New **additive** EF
tables/columns on `SimfAppDbContext` are now permitted for: FAQ
(`FaqGroup` + `FaqEntry`), the Booking approval workflow, Speaker
presentation-files, System Configuration, Venue-Map 2D nodes, and
Networking connections — each as a consolidated additive migration per
feature. The **Identity** schema stays frozen and the existing enums
stay frozen against **rename/reorder** (additive new values still
allowed). Three items were **deferred** with the same decision and are
NOT in scope: the GPS geofence → arrival → attendance → movement chain
(FR-305/506/1103) + question-gating-on-arrival (FR-704), pending the
**G-OI-2** venue-boundary decision; a real live-video provider, pending
external procurement (**D7**); and the exact statistics metric list,
pending **D6**. See `docs/decisions/DECISIONS_LOG.md` D-211. No other
freeze items are lifted; future schema/enum changes beyond this named
list still require explicit owner approval.

**Update (D-349, 2026-06-08):** D7 (a live-video provider) is **resolved for the
proof of concept** — the live + sign-language feeds use **YouTube** (via
`youtube_player_iframe`, with HLS/MP4 kept as a fallback); no schema change (the
URL already lives on `Session.LiveStreamUrl`). The other two D-211 deferrals
(G-OI-2 geofence chain, D6 statistics list) remain open. See
`docs/decisions/DECISIONS_LOG.md` D-349.

**As-built (P2 wave, 2026-06-02):** four of the D-211 named items shipped as
additive migrations — D-227 Booking approval workflow (`SeatReservation`
+Status/+review columns, `App/D227`), D-228 Speaker presentation-files
(`SpeakerPresentations` table + `ISpeakerPresentationStorage`, `App/D228`),
D-229 System Configuration (`SystemSettings` key/value table, `App/D229`),
D-230 Venue-Map 2D nodes (`VenueMapNodes` table + `VenueMapNodeKind` enum,
`App/D230`). New `NotificationKind.BookingRejected=42` (additive value,
persisted by name). Identity schema + existing enum names/values untouched;
shipped mobile wire contracts preserved (append-only). FAQ shipped earlier
(D-218); Networking shipped earlier (D-224). See DECISIONS_LOG D-227..D-230.

### D-217 session-reminder freeze-lift (2026-06-01)

Owner pre-authorised the automated session-reminder scheduler, which
needs one **additive nullable column `Session.ReminderSentUtc`** on
`SimfAppDbContext` (migration `App/D217_AddSessionReminderSentUtc`) as the
worker's once-only dedup guard. Two **additive** `NotificationKind` values
were also added — `BookingConfirmed = 40` and `SessionReminder = 41`
(persisted by name; no wire/schema impact). The **Identity** schema stays
frozen and the existing enums stay frozen against **rename/reorder**
(additive new values still allowed). See `docs/decisions/DECISIONS_LOG.md`
D-217. No other freeze items are lifted; future schema/enum changes beyond
this named column still require explicit owner approval.

### D-219 audit-driven build-wave freeze-lift (2026-06-01)

Owner directive "remove any freeze now" lifted the D-110 freeze as an active
gate for the **audit-driven build wave** — new **additive** EF tables/columns
on `SimfAppDbContext` are permitted for the wave's data-model items
(Organisation lookup [D-220], Booth↔Company + booth-officer contact, audience
comment likes, networking connections, session speaker/host role + session
type, and further audit-surfaced additions), each as an additive migration.
The **Identity** schema stays frozen and the existing enums stay frozen against
**rename/reorder** (additive values still allowed) — no wave item needs Identity
changes, and that surface backs the shipped mobile app + the NCA posture. The
freeze must be **re-instated before the production publish / handover**, and
even with the schema freeze lifted the **shipped mobile wire contract** (public
JSON field names the app decodes) must be preserved. See
`docs/decisions/DECISIONS_LOG.md` D-219 (+ D-220..D-226 as-built). As-built:
D-220 Organisation lookup, D-221 `UserProfile.OrganisationId`+Gender,
D-222 Booth→Company+officer, D-223 audience-comment likes, D-224 networking
connections, D-225 session speaker/host role, D-226 dynamic `SessionCategory`
lookup + `Session.CategoryId` (built as a team-seeded lookup per FDS-004 §5.4 —
NOT a fixed enum; the table ships empty pending the client's category list,
OI-2). The freeze must be re-instated before the production publish / handover.

### D-881 profile / edition / badge programme freeze-lift (2026-08-13)

Owner authorised a lift for the profile-owned-admission programme **and**
confirmed there is no data worth preserving on either database, so both EF
migration histories are **regenerated from scratch** rather than migrated
forward. There is still exactly one `InitialCreate` per context, as D-110
requires — it simply describes a different schema.

This lift is **not additive**, which is why it needed its own decision rather
than riding on D-219. Two existing columns invert their nullability, and no
additive lift can express that. Covered on `SimfAppDbContext`:

- `UserProfile.UserId` -> **nullable**, with a **filtered** unique index
  (D-877). The filter is not optional: SQL Server treats NULLs as equal in a
  unique index, so nullability alone admits exactly one userless profile.
- A profile-owned admission state, replacing `SimfUser.AccountState` on every
  admission path (D-877). A **relocation, not a copy** — D-157 still forbids a
  second writable copy of one fact across the two databases.
- `BadgeBatch` gains a bilingual name; `UserProfile.BadgeBatchId` becomes
  **required** with a seeded default order (D-878).
- `EventEdition` + `EditionId` columns for the yearly lifecycle (D-879).
- `UserProfile.QrId` widens past `nvarchar(16)` to hold an encrypted badge
  (D-880).

`SimfIdentityDbContext` is also regenerated, the owner having already reset it.

**Unchanged by this lift:** the **shipped mobile wire contract** stays
append-only regardless of the schema being open — the app decodes JSON field
names, and no amount of migration freedom makes renaming one safe. Existing
enums stay frozen against rename/reorder; additive values remain allowed. The
freeze must be **re-instated before the production publish / handover**, per
D-219's standing requirement. See `docs/decisions/DECISIONS_LOG.md` D-877..D-881.

### D-895 RE-INSTATEMENT — the freeze is ON again (2026-08-14)

Owner directive. **Every lift above is CLOSED.** D-186, D-199, D-211, D-217,
D-219 and D-881 are spent and are kept below only as history. In particular
D-219's open-ended *"and further audit-surfaced additions"* — the standing
authorisation that everything schema-shaped since 2026-06-01 rode on — is
terminated. From now on a schema or enum change needs a **new, named lift**,
argued for on its own.

**The surface, as observed at re-instatement.** `origin/main` at
`96feb6cb` carries exactly one migration per context under
`src/Backend/SIMF.Infrastructure/Persistence/Migrations/`:

| Context | Migration |
|---|---|
| `SimfAppDbContext` | `App/20260814150708_InitialCreate` |
| `SimfIdentityDbContext` | `Identity/20260814115334_InitialCreate` |

The App id moved from `20260814115348` when the image pipeline landed under the
carve-out below and re-minted it — which is the carve-out working as written,
not a breach. Identity was untouched by that work.

Both are create-only — no `Alter` / `Add` / `Rename` / `Drop` — so the pair IS
the schema. **The rule is what is frozen, not those two ids:** they were minted
by a merge and seventeen different ids have existed on `main`, seven of them
inside two days. The rule is *one `InitialCreate` per context, and no schema
change without a named lift.* The ids are recorded as state, so a reader can
tell whether they are still looking at the sealed schema.

**Scope.** EF migrations under that path only. `docs/migrations/2026/*.sql` is a
separate hand-run channel for content seeds and is not frozen by this — but note
`SIMF_App_RegistrationReferenceSequence_Hotfix.sql` carries real DDL and is
superseded by the App `InitialCreate`.

**Enums: unchanged, and the wording corrected.** Rename and reorder stay
forbidden; **additive values remain allowed**. That allowance is deliberate —
every use of it so far (D-111, D-217's 40/41, D-230's 42) persists by name or
appends a fresh integer, so it never touched the wire contract, and the mobile
risk is *rename*, which stays banned. Withdrawing it would put a new
notification kind behind an owner lift for no contract benefit. Two corrections:
the frozen surface is the **whole `src/Shared/SIMF.Common/Enums/` directory**
(58 enums), not the nine names D-110 listed as if exhaustive; and D-110's
`UserType.Other` reference is stale — D-186 removed it and reserved slot `1`.

**One carve-out, already sanctioned — mostly spent, and it closes itself.** The
per-entity image pipeline (`docs/SIMF-Remaining-Work-Register.md` §2.1) adds App
tables/columns and was explicitly slated to land *before* the freeze-seal;
`feat/media-one-store` carried the bulk of it, converting `*RelativePath`
strings to `*FileId` FKs — which **drops columns** and re-minted the App
migration id in the table above.

**SPENT as of 2026-08-16.** The two pointers this paragraph used to hold open —
`ArchivePastSpeaker.PhotoRelativePath` and `ArchiveMediaItem.Url` — are converted
to `PhotoFileId` and `MediaFileId`, and `MediaPointerRatchetTests.KnownRemaining`
is empty, which is the closing condition the next paragraph names.

That sentence outlived the work by weeks and cost real time: it is the reason a
reader of this file kept finding those two entities and concluding the conversion
was unfinished, while the code had already done it. If a carve-out's closing
condition is met, close it here in the same changeset.

It closes on evidence rather than on a date: the remaining pointers are counted
down by `tests/SIMF.Domain.Tests/MediaPointerRatchetTests.cs`, whose list only
ever shrinks, and **when that list empties the carve-out is spent**. Any pointer
conversion beyond those two needs a new lift. **Nothing else** gets this
treatment.

**Named as NOT built, so nobody reads a lift above as still open:**

- **D-880's `QrId` widening never happened.** `UserProfile.QrId` is still
  `nvarchar(16)`. Widening it for an encrypted badge needs a new lift.
- **D-877's admission relocation is half done, and LESS done than this file
  used to claim.** D-929 made the READ path single-source; **eight days later
  `8dc33eddd` put the account veto back**, and `QrResolver` now reads
  `userRow.AccountState == Disabled ? Disabled : profileRow.AdmissionState`
  again. That reversal is deliberate and defensible — an account disabled for
  fraud should not walk through a gate because a profile row still says
  Approved — but **no decision row recorded it**, and three documents went on
  asserting the single-source read for eleven days. Corrected here on
  2026-08-29; see D-948. The WRITE path is still a dual-write — approve and reject
  set `SimfUser.AccountState` as well. Finishing it means **dropping an Identity
  column**, so it needs a new lift, and it is not obvious it should be dropped:
  an account can be disabled for reasons unrelated to attending, and a walk-in
  is admitted with no `SimfUser` row at all. Until then the D-157 "no duplicated
  data" rule is bent on this one fact — read admission from the PROFILE.
- **D-199's "statistics snapshots" were never built** — statistics are computed
  live. If D6 lands wanting persisted snapshots, that is a new lift.
- **G-OI-2** (geofence → arrival → attendance chain, FR-305/506/1103, and
  question-gating FR-704) and **D6** (the statistics metric list) remain open
  decisions. Some geofence *columns* exist; whether the behaviour is complete was
  not established here, so treat it as open.

**Also frozen by not being mentioned:** four stale branches (`refactor-code`,
`refactor-code-2`, `refactor/app-clean-code-3`, `state-management-refactor`)
still carry the pre-squash 15-migration App stack. Merging any of them
resurrects deleted migrations and breaks the one-per-context rule.

**This is enforced by a test, not by this paragraph** —
`tests/SIMF.Domain.Tests/SchemaFreezeTests.cs` fails the build if a second
migration appears in either folder. Before this, nothing anywhere pinned the
freeze; it was prose, which is exactly how six lifts accumulated without the
baseline text ever being corrected.

### D-924 named lift — the domain-model audit programme (2026-08-15/16)

The first lift taken under D-895's "new, named lift" rule, and it was recorded at
the **end** of the programme rather than the start. The owner instructed a deep
audit of the domain model for normalisation and duplication defects; fixing what
it found changed the App schema across seven branches, and no lift row was taken
while that ran.

`docs/decisions/DECISIONS_LOG.md` D-924 enumerates every change, per branch. In
outline: a `ProfileIdentityDocuments` child table, `UserProfile.MobileNumber`,
`BadgeBatchItems`, the three per-kind identity-number columns and their digests
dropped, the constraint sweep's CHECK constraints and filtered unique indexes,
`VisitorShareToken.TokenHash`, `StoredFile.KekVersion`, two AI CHECK constraints,
one dead index leg dropped and one collapsed index restored. On the Identity
side, only `Permission.Page` / `.Action` / `.DisplayName` were dropped.

Both histories were **regenerated**, not extended, so the one-`InitialCreate`-per-
context rule holds and `SchemaFreezeTests` is unchanged. Enums are untouched, and
the shipped mobile wire contract stays append-only — now pinned by
`tests/SIMF.Api.Tests/AppWireContractPinTests.cs` rather than by review.

### D-926 named lift — the file store owns a file's facts, once

Nine columns dropped: `Session`'s five `Recording*` metadata columns and
`SpeakerPresentation`'s `FileName` / `ContentType` / `SizeBytes` /
`UploadedByUserId`. Both entities already pointed at `StoredFiles` with a real
FK; they now read the name, media type, size and uploader through a navigation
rather than keeping their own copy.

Those copies were not merely redundant. `IFileService` canonicalises a media type
on upload and the copies kept the client's raw string, so the two disagreed for
any non-canonical upload — and the copy was what the admin grid displayed.

**The path half of the rule is complete and separately guarded.** No
`*RelativePath` or `*StoredFileName` column remains in the domain;
`MediaPointerRatchetTests` holds that line and now also fails the build when an
entity carries a `*FileId` alongside a fact the store already records.

One loss, recorded rather than glossed: `CK_SpeakerPresentations_SizeBytes` went
with its column and had no home on the store, whose `SizeBytes` is nullable for
external links. **Closed by D-929** as `CK_StoredFiles_SizeBytes`
(`[SizeBytes] IS NULL OR [SizeBytes] > 0`), which tolerates NULL and so guards
every file service rather than presentations alone.

### D-945 named lift — the cross-profile duplicate-identity constraint is gone (2026-08-29)

Owner instruction. One index dropped on `SimfAppDbContext`:
**`IX_ProfileIdentityDocuments_NumberHash`**, the UNIQUE digest index that made a
national ID / Iqama / passport unique ACROSS profiles. Regenerated through
`tools/migrations/Regenerate-Migration.ps1`, so there is still exactly one
`InitialCreate` per context at the pinned `00000000000000` id and
`SchemaFreezeTests` is unchanged.

**`IX_ProfileIdentityDocuments_ProfileId_Kind` SURVIVES** and is not part of this
lift. It bounds a SINGLE profile to one document per kind, was never the
registration blocker, and dropping it would let one profile hold two passports
with the read path forced to choose. Do not read "the duplicate-identity
constraint was removed" as covering both.

The `NumberHash` **column is kept** though nothing reads it now:
`ProfileIdentityDocument.Number` is AES-GCM encrypted under a random nonce and can
never be equality-queried, so the digest is the only seam a future
document-number lookup could use. Dropping it is a separate decision.

**Shipped with its hand-run delta**,
`docs/migrations/2026/SIMF_App_D945_DropIdentityDocumentUniqueIndex_Hotfix.sql`. A
regenerated migration is a no-op against a database that already has one, so
without the delta the index survives on production and keeps rejecting exactly
the registrations this lift exists to allow. D-944 learned that the expensive way
eight days earlier.

**The thing worth remembering is how the constraint got here.** It was added on
2026-07-12 (`24aaf88ca`, `7fb2c6358`) labelled "H-1", inside a wave called "W4
on-site remediation" that a review pass generated for itself. No decision row was
ever taken, and no document in the repository defines W4's H-1. A
schema-affecting constraint therefore entered the system with no record anyone
could later question, and the first person to question it was a user blocked by
it seven weeks later. A guard nobody can trace is a guard nobody can weigh.

Enums are untouched, and the shipped mobile wire contract stays append-only.
`ErrorCodes.DuplicateIdentity` is removed, but it is an error code the server
emitted rather than a field the app decodes, and no client parses it.

### D-944 named lift — the organisation a visitor types (2026-08-28)

One additive nullable column on `SimfAppDbContext`: **`UserProfile.OrganisationOther`**,
`nvarchar(150)`, plus one seeded row (`Organisation.OtherId`, via `HasData`).
Regenerated through `tools/migrations/Regenerate-Migration.ps1`, so there is
still exactly one `InitialCreate` per context at the pinned `00000000000000` id
and `SchemaFreezeTests` is unchanged.

**Why it needed a lift rather than riding an older one.** D-895 terminated
D-219's open-ended *"and further audit-surfaced additions"*, so every schema
change since is argued on its own. This is that argument, and it is small: one
column, no nullability inversion, no drop, no index.

**Why the column and not just the seeded row.** Organisation is required on the
form (D-221) and the list is a curated government import, so a visitor whose
employer was absent could not finish registering — the picker said "no matches"
and stopped. The seeded "Other" row alone fixes that and records *"47 people work
at Other"*. The column alone would leave `OrganisationId` null and force every
existing join, grid and export over it to learn a second path. Both together
keep reporting working unchanged **and** capture the answer.

**Rejected:** letting the app create `Organisation` rows on the fly. It fills a
curated, government-sourced list with "google", "Google" and "GOOGLE Inc". These
rows are read as free text; a human reconciles them later.

150 is not arbitrary — it matches `Organisation.NameArabic`'s own ceiling, so a
value later promoted into the lookup cannot be truncated on the way in. The
seeded row deliberately carries **no `CommercialRegistration`**: the government
Excel import matches on that column, so a re-import can never update or
duplicate it.

Enums are untouched, and the shipped mobile wire contract stays append-only —
`OrganisationPickerItem.IsOther` is appended last.

### D-935 named lift — Arabic collation, one-active-per-owner, encrypted transcript (2026-08-19)

Owner directive, taken as a single lift because all three need the same
regeneration. Both histories are regenerated through
`tools/migrations/Regenerate-Migration.ps1`, so there is still exactly one
`InitialCreate` per context at the pinned `00000000000000` id and
`SchemaFreezeTests` is unchanged.

- **`Arabic_CI_AI` on every `*Arabic` string column** — 81 on `SimfAppDbContext`,
  2 on `SimfIdentityDbContext`. Applied by a loop over the built model in
  `OnModelCreating`, not per configuration class, so a column added later
  inherits it without anyone remembering to ask.

  **Know what this does and does not fold before promising it to anyone.** It
  folds the **alef maksura onto the yeh**, so `مصطفى` and `مصطفي` match for
  search, equality and the unique indexes. It does **NOT** fold a precomposed
  alef-hamza onto a bare alef: `أحمد` is still not found by searching `احمد`.
  Accent-insensitivity discards a *secondary* weight and only the decomposed
  sequence `U+0627 U+0654` has one, while the precomposed `U+0623` carries a
  primary weight of its own — and precomposed is what every Arabic keyboard
  emits. `tests/SIMF.Api.Tests/ArabicCollationTests.cs` pins both the fold and
  the non-fold, deliberately, so nobody re-derives this from the name of the
  collation. Closing the hamza half needs a normalised shadow column written on
  the way in, which is a schema change and its own lift.
- **`StoredFiles` filtered UNIQUE index on `(Service, OwnerEntityId)`**, predicate
  `[IsActive] = 1 AND [OwnerEntityId] IS NOT NULL AND [Service] IN (...)`. The
  service list is generated from `FileServicePolicies.SingleActivePerOwner`, so
  **adding a service to that set is a schema change** and needs the migration
  regenerated. It is a deliberate SUBSET: galleries, identity documents and
  speaker presentations are many-per-owner.
- **`AiChatMessage.Content` encrypted at rest** (the existing AES-GCM PII
  converter) and widened to `nvarchar(max)`, the base64 envelope not fitting the
  4000-character `nvarchar` ceiling.

**Two traps this lift walked into, recorded so the next one does not.**

1. **A collation is part of an expression's type.** SQL Server refuses to
   evaluate an operand pair whose collations disagree, so a single
   `COALESCE(Name, NameArabic)` in the contact-card read began answering **500**
   the moment the Arabic side was collated differently. Before adding a
   collation anywhere, sweep for column-to-column `??`, concatenation, `UNION`
   and comparison across the boundary. `?? string.Empty` is safe (a literal
   yields to the column), column-`??`-column is not.
2. **A unique index dictates write ORDER.** `AssetService` inserted the
   replacement and retired the previous row afterwards; with the index that
   inserts a second active row and raises 2627. The retire now runs FIRST, in
   `StoredFileService`, through `ExecuteUpdateAsync` because the change tracker
   promises no ordering between an UPDATE and an INSERT in one `SaveChanges`.
   The byte unlink is deferred until after the replacement commits, so a failed
   upload leaves the previous file recoverable rather than destroying it to make
   room for something that never landed.

Enums are untouched, and the shipped mobile wire contract stays append-only.
The freeze must still be re-instated before the production publish / handover,
per D-219's standing requirement. See `docs/decisions/DECISIONS_LOG.md` D-935.

### D-929 named lift — the deferred audit findings, worked to the end

The audit programme parked what it could not fix in its own lane in
`docs/planning/domain-audit-follow-ups-2026-08-16.md`. That file is now the
record of what closed and what is still open, and it says which is which.

Three schema changes: `HallAttendances.HallId` dropped with its FK and
`IX_HallAttendances_HallId_Leave`; `IX_SessionSummaries_IsActive_PublishedAt`
dropped; `CK_StoredFiles_SizeBytes` added. One enum change:
`FileService.CompanyLogo` removed, its integer slot **reserved** on the D-186
precedent because a persisted integer that changes meaning is the failure the
enum freeze exists to prevent.

Dropping a column and dropping its EF configuration are one change, not two.
Remove `HasOne(...)`/`HasForeignKey(...)` while the property and navigation
remain and EF rebuilds the relationship by convention — with **Cascade** where
the configuration said **Restrict**. It compiles, and the migration is where you
would find out.

**Still open, deliberately:** D-877's admission WRITE path (above), and D-880's
`QrId` widening, which nothing writes an encrypted badge into yet — widening it
now buys a wider column and no behaviour.
### D-925 — the migration id is PINNED, and you regenerate with the script

`00000000000000_InitialCreate`, on both contexts. Regenerate with
`tools/migrations/Regenerate-Migration.ps1`, never with a bare
`dotnet ef migrations add`, which stamps a fresh timestamp.

This exists because `main` was left unable to compile **twice on 2026-08-16** —
three `InitialCreate` classes in one namespace each time. It is a merge *success*,
not a merge failure: a timestamped id gives every branch its own filename, so two
branches that both regenerate merge without conflicting and both files survive. No
pull-request gate can see it, because neither PR is individually wrong; the
breakage exists only after both land. A pinned id makes them write the same path,
so git raises a real conflict — which you resolve by running the script once on
the merged model, the only correct resolution anyway.

`SchemaFreezeTests` fails the build on any other id, and on a filename that has
drifted from the `[Migration("...")]` attribute EF actually reads.

---

## Security: the anonymous surface (moved here 2026-08-12)

This lived in the global ~/.claude/CLAUDE.md, where it loaded in every project and
told non-SIMF sessions about a SIMF test file. It is SIMF-specific and belongs here.

## 4) Security Rules (Required)

- **No AllowAnonymous outside the authentication surface.** The test is not a fixed list of endpoint names — it is: **can this endpoint's caller possibly hold a bearer token yet?** If yes, gate it. If no, it belongs to the authentication surface and must carry its **own** credential instead (an emailed code, a reset token, a refresh token, a badge/activation code, a device-key challenge signature).

   This rule previously read "except SignIn / SignUp / ForgotPassword". That wording was wrong in practice and was corrected on 2026-07-29 after SIMF's BF-13 permission matrix was executed: the anonymous **auth** surface was 17 endpoints then and is **20** now, and every entry beyond the three is legitimate — email verification, the second factor (OTP + TOTP + recovery code), mandatory TOTP enrolment, password reset / forced change, token refresh, badge onboarding, and device-key challenge + sign-in. Each runs **before** a bearer token exists, so requiring one would break sign-up, 2FA and password reset outright. Enforcing the literal three-name list would have meant breaking authentication to satisfy a rule.

   **Do not treat that number as fixed.** It is a count of a list that grows with the auth surface, and it has now drifted twice (17, then 19, while the array held more). The allow-list in the test is the authority; this sentence is a description of it, and the two are updated together or not at all.

   **Pin the surface with a test, not a comment.** Enumerate the mapped anonymous endpoints and assert the set equals a reviewed allow-list with a per-entry justification (see `tests/SIMF.Api.Tests/BusinessFlow13PermissionMatrixTests.cs`), so a **new** unauthenticated entry point fails the build and has to be argued for. A prose rule cannot detect the 21st; a test can.

   That allow-list covers `/auth/` only, which is why it is paired with `No_endpoint_outside_the_authentication_surface_is_anonymous` in the same file. That guard is absolute rather than allow-listed: **no `/admin/` route may be anonymous**, and **no endpoint may carry both `AllowAnonymous` and an authorization policy** — the second resolves in favour of anonymous, so such an endpoint reads as gated to a reviewer while being open in fact.

- Authentication is not enough; enforce authorization consistently (permissions per manifest / rules).



---

