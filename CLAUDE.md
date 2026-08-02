# SIMF — Project Instructions

Last updated: 2026-05-31

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

Sprint 1 — Login API + frontend login + visitor lifecycle + hardening — has shipped on
`feature/login-api`. Backend (FastEndpoints + EF Core + SQL Server), Control Panel
(Blazor Server), Website (Blazor SSR + interactive auth islands), and shared component
library + typed API client are all in. Decisions log: `docs/decisions/DECISIONS_LOG.md`
runs D-001 through D-072. Sprint completion artefact:
`docs/SIMF-Sprint1-Login-API-Completion.md`. Outstanding items the sprint accepted /
deferred (committed secrets rotation, architectural refactor, Website skip-link, full
bUnit harness, end-to-end lifecycle test, no-IP rate-limit hardening) are listed there.

Next stage per the programme plan is the User Management module increment on top of
the closed Login API foundation.

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
   **`SimfDataGrid` renders itself** cannot be wrapped from the page, so name the
   code on the grid instead — `AddPermission` (which also covers Duplicate and
   Paste) / `EditPermission` / `DeletePermission` / `ImportPermission` /
   `ExportPermission` / `ApprovePermission` / `RejectPermission` (D-830). In both
   cases use the permission that gates the **endpoint the button calls**, which is
   often not the page's own code and not a name you can guess: on a page hosting
   two grids over two resources they differ, and on `/admin/others/pending` the
   bulk and per-row Approve use different codes because the API does.

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
