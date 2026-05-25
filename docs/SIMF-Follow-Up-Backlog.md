# SIMF — Follow-Up Backlog (post-Sprint-1, post-R3, post-second-review)

**Status:** Queued items not closed by the H1 → H27 + R1 → R3g sweep.
**Last updated:** 2026-05-26

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

---

## 2. Architectural refactor sprint (R4–R6)

Three Architecture SEV-1s remain after R1–R3 + H20+H21:

- **Arch SEV-1.1** — `SimfUser : IdentityUser<Guid>` makes Domain
  depend on `Microsoft.Extensions.Identity.Stores`. The interface
  contract no longer leaks Identity types (H21 / D-082 fixed
  `UserOperationResult`) but the `SimfUser` POCO still inherits the
  framework type. **R5** in the refactor plan; ~1 week.
- **Arch SEV-1.3** — Four bounded contexts (Identity, UserProfile,
  Interests, Notifications) share `SimfIdentityDbContext`. **R6** in
  the refactor plan; ~1 week. Can run in parallel with R5.
- **Arch SEV-1.6** — Five `*Service` classes live in Infrastructure
  but are pure orchestration code (use case logic). **R4** in the
  refactor plan; size revised UP by the post-R3 review to **3-4 days**
  (the original 1-2 day estimate didn't size the read-side repository
  creation work each service needs first).

Plus two natural follow-ups surfaced by the post-R3 review:

- **Arch SEV-1.2 follow-up** — `AdminAccountService` implementation
  is still one 1091-line class implementing all five interfaces split
  in R2 (D-075). Splitting the impl into 5 cohesive 150-250-line
  classes is the rest of the work. Add the docstring constraint
  (post-R3 review SEV-2.1) noting that all five interface
  registrations MUST resolve to the same scoped instance until the
  impl split lands.
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

Single-commit-sized items the next sprint can pick up in any order:

### 3.1 Cancellation-token propagation on `IUserAccountRepository`

(Post-R3 review Finding I.) The repository methods declare
`CancellationToken cancellationToken = default` but the pass-through
implementation never forwards the token to `UserManager` (which
doesn't accept tokens on its public API). Fully honouring it requires
the R5-level swap to a Domain-owned user store. Until then, the
interface keeps the parameter so call sites can pass it and a future
Identity-replacement honours it — `UserAccountRepository` swallows it
deliberately, documented in the class docstring.

### 3.2 Audit-log fire-and-forget channel

H26 (D-086) capped per-IP bearer-rejection audit writes; the bigger
shape — every `IAuditLog.WriteAsync` becomes a non-blocking channel
write drained by a background worker (mirroring `EmailQueue` +
`EmailBackgroundService`) — is queued. Useful for every audit-write
on the request path, not just bearer rejections.

### 3.3 Per-IP rate-limit on bearer-protected endpoints

(Post-R3 review Security SEV-2.1.) The `"auth"` rate-limit policy is
bound only to `/auth/*` endpoints; bearer-protected routes have no
per-route rate limit. A global default policy (per-IP, generous cap)
would close the gap.

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

### 3.8 `myComment.txt` drain

(Sprint 1 §3.9.) Still uncommitted; still the owner's working note.
Move the items to a tracked backlog or close them as done.

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
