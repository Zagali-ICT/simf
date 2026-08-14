# SIMF — Follow-Up Backlog (post-Sprint-1, post-R3, post-second-review)

**Status:** Queued items not closed by the H1 → H27 + R1 → R3g sweep.
**Last updated:** 2026-05-31

This is the consolidated backlog that survives Sprint 1 closure plus
the two 5-agent review passes (the post-H1/H2/H3 review and the post-R3
review). Each item is sized realistically and tagged with the right
sprint shape, so the next iteration starts from a single source-of-
truth artefact rather than rereading the decisions log.

The items naturally fall into four buckets:

1. **Operational decisions** — waiting on the owner's call; cannot be
   closed by code alone.
2. **Architectural refactor sprint** — the three remaining
   Architecture SEV-1s plus their natural follow-ups; sized in
   `docs/SIMF-Architecture-Refactor-Plan.md` (R4 → R6).
3. **Quality + observability follow-ups** — single-commit-sized items
   that are not blocking sign-off but should land before the next
   programme increment closes.
4. **Programme plan** — the next module increment per
   `docs/SIMF-Program-Plan.md`.

---

## 1. Operational decisions (owner only)

### 1.1 Committed secrets in `appsettings*.json` — DECISION required

`src/Backend/SIMF.Api/appsettings.json` carries the super-admin temp
password, the TOTP seed, and the JWT signing key.
`src/Backend/SIMF.Api/appsettings.Development.json` carries the AES key
for the ID-document storage. The three options are:

- **(a) Rotate everything + scrub git history** (`git filter-repo`
  / BFG, force-push). Costs a single co-ordination point with anyone
  who has a local clone; future-proof.
- **(b) `.gitignore` the affected files + accept the leak in
  history**. Cheaper, leaves the bare values in `git log -p` forever.
- **(c) Deploy with env-var-only secrets + accept the committed values
  as stale local-dev convenience**. Cheapest, treats the committed
  values as known-public test credentials.

Recommendation: **(a)** before any push to a remote. Until then, do
not push `feature/login-api` to origin.

### 1.2 Push `feature/login-api` to origin

39 local commits ahead of `origin/feature/login-api` at the time of
writing (a count that grows with each follow-up commit). Push gated
by §1.1.

### 1.3 D-211 programme — deferred items (owner / external) — DECISION required

The "finish all remaining stubs + open gap items" programme (D-211)
deferred three items that cannot proceed without owner input or external
procurement. They stay here until resolved:

- **GPS geofence chain** (FR-305 hall-arrival, FR-506 session attendance,
  FR-1103 movement/dwell) **and** question-gating-on-verified-arrival
  (FR-704) — **blocked on G-OI-2**: the venue-boundary data model + source
  (per-hall polygon GeoJSON vs. rectangular bounds vs. external map data).
  Only the seam exists today; no `VenueBoundary` table, no geofence check.
- **Real live-video provider** — **blocked on D7**: external streaming
  provider procurement + keys. The seam/stub (`mkp_live.dart`, CP
  `LiveSessions`) ships now; the live provider is swapped in when bought.
- **Exact statistics metric list** — **blocked on D6**: the live-counts
  dashboard (`/admin/statistics`) ships; the final metric set awaits the
  owner's list.

Device-calendar add (FR-409) is Flutter-app work and is tracked in the
mobile workstream, not this CP/backend programme.

---

## 2. Architectural refactor sprint (R4–R6)

Three Architecture SEV-1s remain after R1–R3 + H20+H21:

- **Arch SEV-1.1** — `SimfUser : IdentityUser<Guid>` makes Domain
  depend on `Microsoft.Extensions.Identity.Stores`. The interface
  contract no longer leaks Identity types (H21 / D-082 fixed
  `UserOperationResult`) and the EF-tracked entity is now the
  Infrastructure-owned `IdentitySimfUser` shim (R5a — D-090). The
  Domain `SimfUser` POCO still inherits the framework type; R5f does
  the actual split. Remaining R5 slices (R5b–R5g) sized in the refactor
  plan; total remaining effort ~1 week, slice-by-slice tractable in
  single commits.
- **Arch SEV-1.3** — Four bounded contexts (Identity, UserProfile,
  Interests, Notifications) share `SimfIdentityDbContext`. **R6** in
  the refactor plan; ~1 week. Can run in parallel with R5.
- **Arch SEV-1.6** — Five `*Service` classes live in Infrastructure
  but are pure orchestration code (use case logic). **R4** in the
  refactor plan; size revised UP by the post-R3 review to **3-4 days**
  (the original 1-2 day estimate didn't size the read-side repository
  creation work each service needs first).

Plus two natural follow-ups surfaced by the post-R3 review:

- **Arch SEV-1.2 follow-up** — **partially closed (D-209).** The
  `AdminAccountService` implementation (which had grown to ~1900 lines)
  was split into cohesive **partial-class files** (`.cs` + `.Approval.cs`
  + `.Bulk.cs` + `.Roles.cs`) — navigability only, no behaviour/DI change;
  it remains one type backing all interfaces from one scoped instance.
  **Deferred post-event (owner):** the Infra→Application *move* of this
  service (the R4 remainder) — it is security-critical and would need
  brand-new `RoleManager`/`UserRoles` Application abstractions, judged
  too high-risk to land right before the event. A future move could
  also graduate the partial files into genuinely separate classes +
  a shared scope/role-resolver collaborator.
- **`IUserAccountRepository` granularity** (post-R3 review SEV-1.2 —
  Architecture lens) — the 22-method aggregate is a method-for-method
  port of `UserManager`. The reviewer's recommendation: after R5, split
  into role-cohesive contracts (`IUserAccountLookup`,
  `IUserCredentialStore`, `IUserLockoutTracker`, `IUserRoleStore`,
  `IUserTotpTokenStore`). Each Application service injects only the
  seams it uses. Calling this **R3.5** in the sequencing — happens
  after R5 because R5 fixes the bigger leak first.

Recommended order: **R3.5 + R4 first** (smaller, R3 already closed the
hard prerequisite), then **R5 + R6 in parallel** for the bigger
contract changes. Total sprint shape: ~2 weeks. The next programme
increment (the User Management module per the programme plan) is best
held until R3.5 + R4 land — its new endpoints would otherwise be
written against the post-R3 contract shape and need a placement
rewrite immediately.

---

## 3. Quality + observability follow-ups

**Re-audited 2026-05-31 (post-D-210) and deferred as a group** — owner call,
event-deadline-aware. Outcome of the re-audit: **3.3 is already done** (H29/D-088
global per-IP cap), **3.1 is addressed** (H28/D-088 entry-throw; full mid-op
support is blocked on replacing ASP.NET Identity, not on R5), **3.5** stays
deferred (no production signal). The genuinely-open items — **3.2** audit
fire-and-forget channel (M, a latency optimisation with no reported latency
problem; audit is already best-effort), **3.4** notification outbox (M, spans two
DBs), **3.6** Website skip-link (S, but WCAG 2.4.1 already met by the `<main>`
landmark), **3.7** full bUnit harness (M, base harness + markup tests already
exist) — carry no event-blocking value, so they wait for a non-deadline window.

Single-commit-sized items the next sprint can pick up in any order:

### 3.1 Cancellation-token propagation on `IUserAccountRepository` — ✅ ADDRESSED (H28 / D-088)

(Post-R3 review Finding I.) **Addressed at the honest minimum.** Every
`UserAccountRepository` method now calls
`cancellationToken.ThrowIfCancellationRequested()` at entry, so a pre-cancelled
call fails fast. *True mid-operation* cancellation is blocked by `UserManager`
(its public API takes no token) — and **R5 did NOT unblock this** (R5 made the
Domain `SimfUser` a POCO, but `UserManager` is still the backing store), so full
support requires replacing ASP.NET Identity entirely (its own epic, not this
item). The entry-throw is documented in the class docstring as the deliberate
boundary.

### 3.2 Audit-log fire-and-forget channel

H26 (D-086) capped per-IP bearer-rejection audit writes; the bigger
shape — every `IAuditLog.WriteAsync` becomes a non-blocking channel
write drained by a background worker (mirroring `EmailQueue` +
`EmailBackgroundService`) — is queued. Useful for every audit-write
on the request path, not just bearer rejections.

### 3.3 Per-IP rate-limit on bearer-protected endpoints — ✅ DONE (H29 / D-088)

(Post-R3 review Security SEV-2.1.) **Closed.** `Program.cs` now installs a
`GlobalLimiter` (`PartitionedRateLimiter` keyed per-IP, 600/min default) that
applies to **every** request, stacking with the per-route `"auth"` /
`"auth-email"` caps. The original gap (bearer-protected routes had no per-IP
cap) no longer exists.

### 3.4 Notification dispatch outbox

(Sprint 1 §3.8.) `INotificationDispatcher.DispatchAsync` lives outside
the DB transaction by design (H10/D-065 + H23/D-083). A dispatch
failure means the first notification is silently missed; admin can
re-notify by re-rejecting / re-approving. Outbox-style guarantee is
queued for a later operations sprint.

### 3.5 No-IP rate-limit partition tightening

(Sprint 1 §3.7.) H7 left the `?? "unknown"` null-IP fallback unchanged
because TestServer sets no `Connection.RemoteIpAddress` and tightening
broke the test suite. The per-email partition (H7 / D-062) and the
per-IP audit throttle (H26 / D-086) together bound the realistic
attack shapes, so this is low-priority. Revisit if a production signal
shows misrouted no-IP traffic abusing the fallback.

### 3.6 Website skip-link

(Sprint 1 §3.3.) H9 added skip-to-main-content to the CP shell only.
The Website has no comparable navigation block today, so the skip-link
is lower priority. Add when the public-site nav is finalised.

### 3.7 Full bUnit harness

(Sprint 1 §3.4.) H17 ships markup-source assertions, not runtime
tests. A bUnit harness with mocked `IJSRuntime` /
`NavigationManager` / `AuthenticationStateProvider` would prove the
runtime behaviour (Escape closes the dropdown, focus jumps to `<main>`
on skip-link, etc.). Scope: a separate test-tooling increment.

### 3.10 `BadgeBatch.CountsSummary` was English-only in an Arabic UI — DONE

Found 2026-08-14 driving `/admin/visitors/badge-batches` in Arabic: the
**Contents** column rendered `Normal × 4 + VIP × 3` with the **English**
tier names while the rest of the page was Arabic, because
`CountsSummary` is a single denormalised English string with no Arabic
side to fall back to.

Fixed the same day, by the first of the two shapes this item proposed
and **without a schema change**: the breakdown is derived on read from
the member rows, carrying BOTH names per tier
(`AdminBadgeBatchSummary.Tiers`), and the page composes it in the
language being read. One grouped query per page of the list, not one
per row. The stored string stays as the audit record and as the
fallback for an order whose members are gone, so nothing gained a
second writable copy.

Two things the live check caught that the tests had not:

- Deriving tiers for the **direct-registration** order replaced its
  short prose with every profile type present — nine entries and
  growing with each registrant, in a column sized for "Normal × 4".
  It is not a badge order, so it is excluded and carries
  `IsDirectRegistration` instead.
- Its stored prose is the English literal "Direct registration", so
  falling back to it left one English cell in an otherwise Arabic
  table. It now renders a localised label
  (`Admin.BadgeBatches.DirectRegistration`).

`MergeCountsSummary` still parses its own `" × "`-delimited output to
maintain the stored string. That is now display-irrelevant, and could
be retired if the stored summary is ever dropped — which would be a
schema change and is not proposed here.

### 3.8 `myComment.txt` drain — TRIAGED (H31 — D-089)

The owner's working note at repo root was triaged item-by-item; the
file itself is left untouched per the standing "never commit
`myComment.txt`" rule. Status per line:

**Already done — close in the file when convenient:**
- L7  "No warnings, clean code" — every Debug + Release build is 0/0.
- L13 "Add user type admin" — done in P7 (D-048); three-UserType model.
- L15 "ApiError MessageArabic" — done in D-030.
- L18-29 "Visitor profile fields (Arabic/English name, nationality, ID,
  mobile in/out KSA, attachment, QR id)" — done in P8 (D-049,
  UserProfile rename) + P4 (D-046 b, encrypted ID image) + P3 (QR id).
- L34 "CP admin add/delete/approve" — done across P7 / P4 / D-044 b
  (bulk delete, duplicate, import/export) + the H1 / R2 work.
- L35 "If 2FA not true don't ask OTP/TOTP" — done in D-033.
- L37 "Avatar to directory not DB" — done in D-039 + R1 (D-074) typed
  `Storage:AvatarBase`.

**Needs the owner's decision — not code-fixable:**
- L2  "All enums in a shared project" — Domain enums in Domain is
  normal DDD; the move is a deliberate departure. Owner confirms.
- L11 "15-04-2024 is authoritative" vs current controlled docs — the
  project CLAUDE.md already marks the folder superseded; owner
  confirms which wins.
- L14 "Why `AspNet` prefix on role tables?" — that's the Identity
  default schema; renaming requires a migration and an Identity-store
  rebind. Owner confirms whether to take the cost.

**Needs access to the IBS reference project — design deliverables:**
- L3  `[Resource(...)]` per-enum localisation pattern (point at IBS code).
- L4  IBS log handling — backend + frontend + real-time CP view + download.
- L6  Full user/role/type management plan from IBS (waits on gate D1).
- L30 Email config from IBS.
- L31 Serilog file layout + viewer from IBS.

**Real follow-up commits — promoted to Bucket 3:**
- L12 "QR-for-Google-Authenticator endpoint" — `TotpEnrollmentService.SetupAsync`
  already returns the otpauth URI + QR SVG, but no admin / seeder
  exposure of the super-admin's QR for first-time pairing. **Add a CP
  /admin/2fa-pairing page** that renders the seeded admin's QR.
- L16 `SimfDataGrid` full spec — row filter, server-side pagination,
  right-click menu, fixed action button column, action bar with
  Select-All / Add / Edit / Delete / Copy / Paste / Duplicate / Import
  / Export. The current grid covers list + filter + sort + paging;
  the action-row + clipboard + import bits are not built.
- L17 Grey theme (dark + light + grey). Tokens layer addition.
- L32 Email distribution list — Support / IT recipients for
  failure-alert emails. Hook into the existing `Email.EnqueueFailed`
  audit (H10 — D-065).
- L33 "All customer emails saved to in-app notification too." Today
  P12 dispatches in-app + email on lifecycle events (D-053 / D-054).
  The expansion is to do the same for the credential-flow code-issue
  emails (password reset, email-verification, sign-in OTP — currently
  email-only).
- L36 The seeded TOTP secret `[REDACTED - supply via SIMF_API_SuperAdmin__TotpSecret]`
  — owner reported "not working". `TotpVerifier` uses `OtpNet` which
  defaults to UTC + 30-second window. The secret IS the
  super-admin's; the test infrastructure verifies the same code path
  works. Worth a short debug session — likely either a clock-skew
  issue on the owner's authenticator or the secret in `appsettings`
  was edited and the literal value here is stale.

**Not yet started — Flutter app, separate programme stage:**
- L8  App sign-up + OTP confirm + profile + interests + await approval.
- L9  App menu switch by UserType.
- L10 Admin-added users without confirm (later CP module).

### 3.9 Operator (admin) user manual — access-control chapter

Companion to the developer guide
(`docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`, shipped 2026-05-31).
The dev guide is code-facing; the **operator** still has no non-technical
chapter for the access-control surface now that the Access control nav
group + role→permission editor + user→role assignment shipped
(D-207 / D-208). Author the already-planned **Admin-Manual §4.4 "Roles &
permissions"** chapter in `docs/manuals/Admin-Manual.md`: how an
administrator creates a role, grants it permissions, assigns roles to a
user, resets a user's 2FA, and what each guard / refusal message means —
task-first with screenshots, matching the manual's existing chapter shape.
Deferred from the permissions / gaps / E2E plan on owner instruction
(2026-05-31).

---

## 4. Programme plan

Per `docs/SIMF-Program-Plan.md` the next increment is the **User
Management module** — admin self-service for the `Other` /
`Visitor` user types, permission-driven navigation filtering (gate
D1 / SIMF-CPD-001 OI-3), and the User Management UI sat on the
closed Login API + R1–R3 architectural foundation.

Recommendation per the refactor plan: hold the increment until
R3.5 + R4 land. The new endpoints would otherwise be written
against the post-R3 contract and need a placement rewrite
immediately. R5 + R6 can run alongside since their contracts stay
backwards-compatible through the migration.

---

## Footnote — decision-log range

Sprint 1 closure: D-001 → D-072 (`SIMF-Sprint1-Login-API-Completion.md`
captures this). Post-closure work: D-074 → D-086 (R1 → R3g + H19 →
H27; D-073 intentionally skipped per H18's commit message). The
next sprint will start a fresh range from D-087.
