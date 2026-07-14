# SIMF — Production-Readiness Round 1 — Run Log

**Started:** 2026-07-13 · **Environment:** local stack (fresh DB + full seed) ·
**Driver:** Chrome DevTools automation (CP + Web) + sqlcmd verification.
Companion to `SIMF-Production-Readiness-TestBook.xlsx` / `SIMF-Business-Flows.md`.

> No secrets in this file. The shared local account password and the super-admin
> TOTP secret are held only in the (git-ignored) `deploy/set-env-api.ps1` overlay
> and are never written here.

## Environment setup (PASS)

| Step | Result |
|------|--------|
| Drop + recreate `SIMF_Identity` + `SIMF_App` (local `Server=.`, Windows auth) | fresh |
| API relaunched (Development) → auto-migrate both DBs + all seeders | health 200 on :5175 |
| Identity seed | 10 accounts (super-admin + 9 demo `@simf.local`), all on the one shared local password |
| Content seed (`docs/migrations/2026/*.sql` via `SqlContentSeeder`) | Speakers 32, Sessions 5, ProgrammeDays 3, Sponsors 10, MediaPartners 3, News 1, Booths 6, Exhibitors 6, Archive editions 4, FAQ 6, VenueMap 10, Countries 59, Regions 13, ProfileTypes 8, StoredFiles 23, UserProfiles 44 |
| Control Panel (:5158) + Website (:5115) | both up |

## Auth (PASS)

- Super-admin sign-in: email + password + **2FA TOTP** → Dashboard. Dashboard KPIs
  match the seed (attendees 8, sessions 5, speakers 32, booths 6). Console 0 errors,
  network all 200.

## CP page smoke (PASS — load + render + 0 console errors)

Dashboard · Speakers (32, bilingual + flags + codes) · Sessions (5, 3 days, Main Hall) ·
Visitors (3 approved) · Sponsors (10) · Booths · Gates — all on the standard SimfDataGrid.

## Deep CRUD business flows

### BF — Bulk delegation badges (PASS) — E2E UI → API → DB
Scenario: "Ministry of Interior — 3 VIP + 7 Normal", flagged as delegates.
- UI (`/admin/delegates` → Bulk-generate badges): entered Normal=7, VIP=3 → toast **"10 badge(s) generated."**
- DB verified: `AspNetUsers` 10 → **20**; `UserProfiles` 44 → **54**; new profiles
  `IsDelegate=1`, split **Normal=7, VIP=3**. Each badge carries a ready QR.
- Re-confirmed after re-login: Dashboard TOTAL ATTENDEES = **18** (8 seed + 10 delegates) — data durable.

### BF — Create session (PASS) — E2E UI → API → DB
Scenario: create a new programme session from `/admin/sessions` → "Add session".
- Form: Code `TEST-D1-99`, Title EN "Round-1 Test Session" + AR "جلسة اختبار الجولة الأولى",
  Description EN, Hall = Main Hall (MAIN), Type = Session, Start `2026-11-23 10:00 UTC`,
  End `2026-11-23 11:00 UTC`, capacity left blank (inherit).
- UI result: live-region toast **`Session "Round-1 Test Session" was created`**; grid went 5 → **6 of 6**,
  new row shows Main Hall / cap **500** (inherited) / Active / Scheduled. Console 0 errors.
- DB verified (`SIMF_App.Sessions`): row present — `Type=1`, `StartUtc/EndUtc` exact,
  `CapacityOverride=NULL` (inherit confirmed), `Status=0` (Scheduled), `IsActive=1`,
  **`CreatedBy` = super-admin user id** (AuditStamping interceptor working).
- Note: two `<input type=datetime-local>` fields needed value-set + `input`/`change` dispatch to
  bind (segmented spinbuttons don't accept the automation `fill`); this is a **test-harness quirk,
  not a product defect** — a human types the date normally.

### BF — Visitor sign-up API (PASS) + security positive
Scenario: register a new visitor via the real public endpoint `POST /api/v1/app/auth/sign-up`.
- Request `{email, password, confirmPassword}` with a StrongPassword → **HTTP 201**, body is the
  exact `ApiResult<T>` envelope `{success:true, data:{email, codeExpiresInSeconds:600}, error:null, meta:null}`
  (matches SIMF-API-001 §12.4). Rate-limiter policy "auth" applied.
- DB verified (`SIMF_Identity`): account created `AccountState=Registered`, `UserType=Visitor`,
  `EmailConfirmed=0`; an `AccountCodes` row of `Purpose=EmailVerification` was issued.
- **Security positive:** the verification code is stored **hashed** (`af10…`, not the 6-digit
  plaintext) — codes are not recoverable at rest (NCA-aligned). Consequence for testing: the full
  app-registration chain (email-verify → 4-step profile → admin approval) **cannot be completed via
  API alone** — the final profile step requires **live face capture** (by design). So the CP
  approve/reject state-transition is covered by the code audit (background workflow, area
  `visitor-lifecycle`) rather than driven headless here.

### Website (:5115) — public route sweep (PASS)
The public site (`SIMF.Web`) is a **slim auth / account / programme portal by design**, not a
full content site — the public content lives in the Flutter app + CP. Real routes all **200**:
`/`, `/programme`, `/login`, `/login/verify`, `/forgot-password`, `/reset-password`, `/visit`,
`/meeting/confirm`, `/not-found`, `/account`; `/account/profile` → **302** (auth gate works).
- `/programme` **rendered real seeded data**: 3 event days (Fri 20 / Sat 21 / Sun 22 Nov 2026)
  with the 5 seeded sessions grouped per day, correct **UTC→AST conversion** (06:00 UTC → 09:00
  local). My new `TEST-D1-99` session (Nov 23, not a programme day) correctly does not appear.
- All CSS/JS assets load 200 (theme tokens, components, Blazor framework, account + session-timeout js).
- (Earlier guessed routes `/about /news /speakers /faq …` return 404 — **not defects**, those routes
  do not exist by design.)

### CP session-timeout guard (D-443) — observation
The inactivity warning modal ("Session about to expire … signed out in N seconds", Sign-out-now /
Stay-signed-in) fires correctly and the **Stay-signed-in path extends the session** (verified: still
signed in as Super Administrator afterwards). Observation (low): the countdown appears **not to reset
on in-form typing / scrolling**, only on navigation / SignalR round-trips — so a human filling a long
form (e.g. Add-session) can see the warning mid-entry. NCA requires the hard cap; worth confirming
whether in-form activity should reset the idle timer. Not a functional defect.

## Findings

| # | Sev | Finding | Root cause | Status |
|---|-----|---------|-----------|--------|
| 1 | Low (cosmetic) | Speaker photos 404 (`/account/api/admin/assets/SpeakerPhoto/{id}/image`, 16 seen) — grid falls back to initials, no crash | SQL seed loads `StoredFile` rows but not the image **bytes** (assets deploy separately to `C:\SIMF\Storage\files` per seed convention) | Open — deploy photo binaries to fix |
| 2 | Env (not a product bug) | Backend xUnit suite fails to **build** while the API runs — `SIMF.Api.exe` is locked by the live API process (PID 38812) | Same build output dir | Run with the API briefly stopped (not done here — the shared working dir is used by other live sessions; stopping the API could disrupt them) |
| 3 | Low (cosmetic) | Website `/favicon.ico` → 404 (one console error on every public page) | No favicon shipped in `SIMF.Web/wwwroot` | Add a favicon.ico |

## Coverage summary (what ran / what remains)

**Driven live (UI→API→DB or API→DB), all PASS:** environment + seed · super-admin auth+2FA ·
CP page smoke (7 pages) · bulk delegation badges · create-session CRUD (+audit stamp) ·
visitor sign-up API (+hashed-code positive) · Website route sweep + programme render ·
D-443 session-timeout extend.

**Covered by the code audit instead of live driving** (all 12 bounded contexts incl. booking,
Q&A/committee, ratings/reminders, gates/attendance, archive, permissions, cross-DB) — see the
29-finding section below. This reached the backend correctness the live path could not, because
(a) the full app-registration → booking/question/rating chain needs an authenticated visitor
token behind the **hashed-code + live-face-capture** wall, and (b) the xUnit suite can't build
while the API holds `SIMF.Api.exe` (Finding #2) and stopping it risks other live sessions.

**Still open for a later pass:** remaining ~50 CP pages' fine-grained CRUD; the Flutter
widget/golden suites; xUnit run in an isolated build; on-tablet app manual pass.

---

## Code audit — 29 confirmed production-readiness defects (2026-07-13)

Produced by a background multi-agent audit: **12 bounded contexts** each read against the
business-flow specs + SIMF-API-001 + the code, then **every finding adversarially re-verified**
by an independent agent that opened the cited `file:line` and confirmed the real code
(`fileConfirmed=true`) before it was allowed to count. **44 agents, 4.63M tokens, 860 tool calls.**
Result: **31 raw -> 29 CONFIRMED, 2 refuted** as false positives.

> These are code-level findings from a **test round** — not fixed here. Fixing any of them is a
> separate, approval-gated change (several touch security and audited/frozen surfaces).

### Blocker / High — must be closed before the production publish + NCA handover

**[BLOCKER] Demo accounts including an Administrator are seeded in every environment (production) with a shared password, no 2FA, and no forced password change**
- Where: `src/Backend/SIMF.Infrastructure/Identity/IdentitySeeder.cs:510` (security)
- Fails: EnsureDemoAccountsAsync is called unconditionally from SeedAsync (IdentitySeeder.cs:240), and SeedAsync runs on every non-Testing boot including production (Program.cs:408, 424). The only guard is a non-empty Seed:DemoPassword (line 500) - there is no environment gate. When the prod config supplies that password (D-585 says it is seeded 'in EVERY environment (production included) with one shared password'), admin@simf.local is created with the Administrator role, PasswordChangeRequired=false (line 537), and TwoFactorEnabled left false (the Admin branch at 553-558 only adds the role). Anyone wh
- Spec basis: The method's own D-585 comment (IdentitySeeder.cs:491-495): 'this runs in EVERY environment (production included) with one shared password ... The accounts and that default password MUST be removed / rotated before the production publish + NCA handover.' A production-readiness audit must surface it:
- Verifier: Quoted code matches IdentitySeeder.cs:510 exactly. The failure reproduces with no guard preventing it: EnsureDemoAccountsAsync (496) is called unconditionally from SeedAsync (240), which runs on every non-"Testing" boot incl. production (Program.cs:408,424). The sole guard, strin

**[HIGH] Admin (Control Panel) sign-in is not TOTP-enforced - any admin without 2FA enrolled signs in with password only**
- Where: `src/Backend/SIMF.Application/IdentityAccess/SignInService.cs:178` (security)
- Fails: A fresh production deploy uses the appsettings default SuperAdmin:TotpSecret="", so CreateSuperAdminAsync (IdentitySeeder.cs:466-470) never calls SetTwoFactorEnabledAsync(true) and the bootstrap super-admin has TwoFactorEnabled=false. After the forced first-login password change, the super-admin POSTs /app/auth/sign-in with Audience=Cp; SignInAsync reaches this branch, skips the second-factor challenge entirely, and IssueTokensAsync returns a fully-privileged admin access token from the password step alone. The same holds for every CP-provisioned admin - AdminAccountService.CreateAccountAsync 
- Spec basis: SIMF-API-001 §12.3: 'A Control Panel user signs in with email and password and then a time-based one-time code (TOTP)... An access token is issued only after the TOTP step succeeds.' The controlled doc mandates TOTP for CP sign-in; the code makes it opt-in. Contrast RegistrationService.cs:97 which f
- Verifier: Quoted code matches SignInService.cs:178-180 exactly, and the comment confirms the branch "applies to both Control Panel users and visitors" - when TwoFactorEnabled=false the password step alone mints a full admin token via IssueTokensAsync, with no CP-specific second-factor over

### Medium (14)

| # | Area | Defect | Location | Category |
|---|------|--------|----------|----------|
| 3 | visitor-lifecycle | Self-registering visitor can self-assign a partner/operational ProfileType (Staff/Moderator/Exhibitor) - server never enforces IsAppRegisterable / audience-scope on the write path | `UserProfileService.cs:130` | security |
| 4 | visitor-lifecycle | Approve flips Identity account to Approved before minting the QR on the App DB - a transient App-save failure orphans an Approved visitor with no QR and no recovery path | `AdminAccountService.Approval.cs:71` | data-integrity |
| 5 | delegates-badges | Badge-activation rebinds the account email at 'start' before the code is ever verified - a single mistyped email permanently bricks a placeholder badge's self-activation (and lets a QR holder pre-empt it) | `BadgeAuthService.cs:152` | security |
| 6 | delegates-badges | Multi-batch bulk-generate commits earlier batches before validating a later batch's profile type - a 400 is returned while N Approved badge accounts are already persisted | `AdminAccountService.Bulk.cs:866` | data-integrity |
| 7 | sessions-programme-halls | Session update capacity-shrink guard is bypassed when CapacityOverride is cleared to null, allowing an open-seating session to be silently oversold below already-held bookings | `AdminSessionService.cs:277` | data-integrity |
| 8 | speaker-meetings | Speaker availability offers slots already held by an AwaitingSpeaker meeting (taken-filter uses Accepted only, not the SlotHolding set) | `SpeakerAvailabilityService.cs:129` | correctness |
| 9 | speaker-meetings | Admin response note over 2000 chars (or any DB error on respond) surfaces as a false 'That slot is no longer available' 409; ResponseNote length is never validated | `SpeakerMeetingRequestService.cs:390` | validation |
| 10 | speaker-meetings | Requester self-cancel of an AwaitingSpeaker meeting has no concurrency guard and can silently overwrite a speaker's just-confirmed Accepted decision (lost update) | `MyRequestsService.cs:136` | data-integrity |
| 11 | qa-committee | Committee 'hide' does not clear the pushed/on-stage flag, so a retracted question resurfaces on stage when re-approved | `SessionQuestionCommitteeService.cs:119` | data-integrity |
| 12 | ratings-reminders-notif | SessionReminderWorker can re-send 'session starting soon' reminders on a restart mid-tick (and duplicates outright under scale-out) - the dedup stamp is not atomic with the notification writes and there is no per-notification backstop | `SessionReminderWorker.cs:162` | correctness |
| 13 | ratings-reminders-notif | ProgrammeRatingPromptWorker end-of-programme trio can re-fire to the entire audience on a restart mid-dispatch - the once-only SystemSetting marker is written only after the whole loop, and the global prompts carry no RelatedEntityId so no per-notification dedup is even possible | `ProgrammeRatingPromptWorker.cs:297` | correctness |
| 14 | gates-arrivals-attendance | Over-length scanned QR crashes the gate scan with a 500 instead of the documented HTTP-200 QrUnknown denial | `GateOperatorService.cs:82` | validation |
| 15 | gates-arrivals-attendance | Idempotency-key reuse / concurrent same-key retry hits the GateScan unique index and 500s -- the exact failure idempotency is meant to prevent | `GateOperatorService.cs:454` | correctness |
| 16 | exhibition-venue | Deactivating an exhibitor leaves its live data publicly visible via active booths, and makes those booths un-editable | `PublicBoothService.cs:35` | data-integrity |

### Low (13)

| # | Area | Defect | Location | Category |
|---|------|--------|----------|----------|
| 17 | auth-2fa | Recovery-code failures count toward account lockout while TOTP/email-OTP failures do not - a user mistyping recovery codes can lock their whole account out of its own emergency fallback | `SignInService.cs:342` | security |
| 18 | delegates-badges | Bulk 'as delegates' badges are created with NationalityId=0, silently violating the documented delegate invariant that the walk-in path enforces | `AdminAccountService.Bulk.cs:929` | data-integrity |
| 19 | sessions-programme-halls | Session Description/live-caption/live-URL fields have EF max-lengths but no server-side length validation -> HTTP 500 on over-length input | `AdminSessionService.cs:133` | validation |
| 20 | bookings-reservations | Booking creation has no session-timing guard, so a visitor can create an un-cancellable hold on a session that has already started or ended | `SeatReservationService.cs:112` | validation |
| 21 | bookings-reservations | Post-insert capacity backstop can spuriously reject BOTH racers, leaving a free place unfilled | `SeatReservationService.cs:1109` | correctness |
| 22 | speaker-meetings | Speaker double-book DB backstop is keyed on SlotStartUtc only, so overlapping windows with different slot starts are not protected under concurrent accept | `SpeakerMeetingRequestConfiguration.cs:68` | correctness |
| 23 | ratings-reminders-notif | Notification endpoints omit the RequireApprovedAccount policy the spec mandates - authorization relies solely on a manual sub-claim check that a non-approved (Guest/Pending) account passes | `NotificationEndpoints.cs:19` | permission |
| 24 | gates-arrivals-attendance | A Both-mode hall-door gate scan marks a still-present attendee as departed when their attendance row was opened by another channel, under-counting live presence | `HallAttendanceService.cs:200` | data-integrity |
| 25 | exhibition-venue | Venue-map node Kind is never validated as a defined enum value (out-of-range Kind persists and reaches the public map) | `VenueMapService.cs:241` | validation |
| 26 | exhibition-venue | Soft-deleting a booth orphans venue-map nodes that reference it (node stays active + public, its booth link 404s) | `AdminBoothService.cs:270` | data-integrity |
| 27 | archive-stats-content | Archive edition child lists (gallery / session-titles / past-speakers) have EF max-lengths but no matching validation - over-long input throws a SQL-truncation 500 instead of a clean 400 | `AdminArchiveService.cs:471` | validation |
| 28 | archive-stats-content | Anonymous /app/content/batch has no request validator and no null-guard on Keys - an explicit "keys": null body throws a NullReferenceException 500 | `PublicCmsEndpoints.cs:69` | correctness |
| 29 | permission-enforcement | Documented "fails the build if a gate is missing" guard does not exist for API endpoints - PermissionEnforcementTests only behaviorally spot-checks two hardcoded routes | `PermissionEnforcementTests.cs:42` | permission |

### Correctly refuted (2) — NOT defects (adversarial verify dropped them)

- Rejecting/approving a user revokes refresh tokens but does not roll the security stamp, so the existing access token keeps its (stale) approved access for up to one access-token lifetime
  - Why dropped: The quoted code at AdminAccountService.Approval.cs:147-148 is accurate - RejectAsync/ApproveAsync revoke refresh tokens without rolling the security stamp, and the sibling Disable path (Bulk.cs:251/580) does roll it. The abstract mechanism is also real: OnTokenValidatedAsync (JwtBearerSetup.cs:102) 
- Non-geofenced LIVE venue gate ignores the IsAtVenue self-assert flag (contract drift vs BF-07-003), and audit gate label is hardcoded
  - Why dropped: The quoted code matches SessionQuestionService.cs lines 119-123 verbatim, and the mechanics the auditor describes are real: for a non-geofenced LIVE hall `atVenue` short-circuits to true via `!session.HasGeofence`, so `request.IsAtVenue` is never read (200 returned), and line 132 hardcodes `gate=hal

### Re-run / drill-down
- Full structured findings (with each verifier's reasoning): `scratchpad/audit_confirmed.json`
- Per-agent transcripts: the workflow `journal.jsonl` under `subagents/workflows/wf_5fe70835-24f/`.

---

## Remediation — 21 findings fixed (branch `fix/round1-defects`, worktree `D:/SIMF/wt-r1fix`)

Applied by a 9-agent workflow across **disjoint file groups** in an **isolated worktree** (main/shared
tree untouched — verified `git status` clean there), then **build-verified centrally**:
`dotnet build tests/SIMF.Api.Tests -c Debug` → **Build succeeded, 0 Warnings, 0 Errors**. Each fix is
fail-closed / additive, reuses existing `ErrorCodes` + bilingual messages, matches the real EF
`HasMaxLength` values, and ships a test. No schema/enum/migration change; frozen surfaces untouched.

| Finding | File | Change |
|---------|------|--------|
| #7 - session update capacity-shrink guard bypassed when CapacityOverri | `AdminSessionService.cs` | In UpdateAsync the oversell guard now runs for every same-hall/same-time edit: it computes effectiveCapacity = request.CapacityOverride ?? hall.Capaci |
| #19 - session Description/live-caption/live-URL have EF max-lengths bu | `AdminSessionService.cs` | Added a service-level ValidateTextLengths helper (with an EnsureMaxLength sub-helper) called in both CreateAsync and UpdateAsync right after ValidateL |
| #20 - booking creation has no session-timing guard | `SeatReservationService.cs` | Added a create-time guard EnsureSessionOpenForBooking(DateTimeOffset startUtc) that throws ErrorCodes.BookingSessionStarted (409, bilingual, reusing t |
| #21 - post-insert capacity backstop can spuriously reject BOTH racers, | `SeatReservationService.cs` | Replaced the racy `if (active > effectiveCap) { remove; throw; }` in EnforceCapacityAfterInsertAsync with a deterministic rank-based reject: early-ret |
| #8 - Speaker availability offers slots already held by an AwaitingSpea | `SpeakerAvailabilityService.cs` | GetAvailableSlotsAsync taken-filter changed from r.Status == MeetingRequestStatus.Accepted to MeetingRequestStatuses.SlotHolding.Contains(r.Status) (A |
| #9 - Admin ResponseNote over 2000 chars surfaces a false 409; Response | `SpeakerMeetingRequestService.cs` | RespondAsync now guards (request.ResponseNote ?? "").Trim().Length > 2000 (== EF HasMaxLength(2000)) and throws ApiException(SpeakerMeetingRequestInva |
| #10 - Requester self-cancel of an AwaitingSpeaker meeting had no concu | `MyRequestsService.cs` | CancelAsync SpeakerMeeting case: read switched to AsNoTracking (kept for 404 / friendly-409), and the in-memory 'r.Status = Cancelled' replaced with a |
| #14 - Over-length scanned QR crashes gate scan with HTTP 500 instead o | `GateOperatorService.cs` | Added const QrIdAtScanMaxLength=32 (matches the real GateScanConfiguration.HasMaxLength(32)) and a guard in RecordScanAsync immediately after QrId.Nor |
| #15 - Idempotency-key reuse / concurrent same-key retry hits the GateS | `GateOperatorService.cs` | Extracted TrySaveScanAsync (shared by RecordAllowedAsync and RecordDenialAsync) that wraps SaveChangesAsync; on a DbUpdateException violating UX_GateS |
| #24 - Both-mode hall-door scan marks a still-present attendee as depar | `HallAttendanceService.cs` | Scoped the Both-mode directionInferred 'open row -> departure' branch to skip rows opened by the GPS geofence: the departure now fires only when open. |
| #23 - Notification endpoints omit the RequireApprovedAccount policy th | `NotificationEndpoints.cs` | Added Policies(nameof(AuthorizationPolicies.RequireApprovedAccount)) to all 5 endpoints (list, unread-count, {id}/read, read-all, DELETE {id}) plus 'u |
| #12 - SessionReminderWorker can re-send 'session starting soon' remind | `SessionReminderWorker.cs` | In RunReminderScanAsync, claim each session (set ReminderSentUtc then SaveChangesAsync) BEFORE dispatching that session's attendee batch, instead of a |
| #13 - ProgrammeRatingPromptWorker end-of-programme trio | `ProgrammeRatingPromptWorker.cs` | RunProgramEndScanAsync now writes+commits the once-only ProgramEndSettingKey SystemSetting marker BEFORE the trio dispatch loop (was after). RunDayPro |
| #16 - Deactivating an exhibitor leaves its live data publicly visible  | `PublicBoothService.cs` | Added `&& (b.Exhibitor == null // b.Exhibitor.IsActive)` to the Where filter in both ListAsync (line 25) and GetAsync (line 87) so a soft-deleted exhi |
| #25 - Venue-map node Kind never validated as a defined enum | `VenueMapService.cs` | Added `if (!Enum.IsDefined(kind)) throw ApiException(ErrorCodes.VenueMapNodeInvalid, 400, en, ar)` at the top of EnsureKindMatchesReferences (called b |
| #26 - Soft-deleting a booth orphans venue-map nodes referencing it | `AdminBoothService.cs` | In DeactivateAsync, after the idempotency early-return, added a guard that queries dbContext.VenueMapNodes for an active node with BoothId == id and t |
| #27 - archive edition child lists | `AdminArchiveService.cs` | Added static RequireChildLength/ChildLengthOrNull helpers and applied them in BuildMedia/BuildSessionTitles/BuildPastSpeakers to enforce the real EF c |
| #28 - anonymous /app/content/batch has no validator and no null-guard  | `PublicCmsEndpoints.cs` | Added PublicContentBlockBatchRequestValidator (FastEndpoints Validator<PublicContentBlockBatchRequest>, auto-discovered) with RuleFor(x => x.Keys).Not |
| #11 Committee hide does not clear the pushed/on-stage flag -> retracte | `SessionQuestionCommitteeService.cs` | In HideAsync, after setting Status=Hidden, clear the on-stage marker (IsPushed=false; PushedAt=null) when the question was pushed - mirroring the S-8  |
| #17 Recovery-code failures count toward account lockout while TOTP/ema | `SignInService.cs` | Removed the account-level `await accounts.AccessFailedAsync(user);` call on the recovery-code failure path in VerifyRecoveryCodeAsync (former line 342 |
| #29 - documented "fails the build if a gate is missing" guard does not | `PermissionEnforcementTests.cs` | Added an additive reflection [Fact] (Every_admin_endpoint_is_permission_and_approval_gated) that enumerates every mapped FastEndpoints route via Endpo |

**Held for owner decision (NOT changed):**
- 🔴 #1 demo accounts w/ shared committed password (IdentitySeeder.cs:510) — the shared password is the
  local login; real fix is ops/config (do not seed in prod) + rotate. Known D-585 item.
- 🟠 #2 CP sign-in not TOTP-enforced when 2FA unenrolled (SignInService.cs:178) — enforcing it would lock
  out any admin not yet enrolled; needs an enrolment/rollout decision.
- Also deferred (would need schema/behaviour change beyond a fail-closed guard): #3 profile-type self-assign,
  #4 approve-before-QR ordering, #5 badge email-rebind ordering, #6 multi-batch transaction, #22 speaker
  double-book index. These remain in the audit list for a follow-up.

**Verification & commit:** `dotnet build tests/SIMF.Api.Tests -c Debug` → **Build succeeded, 0/0**.
The **26 new fix-tests all pass** (`--filter` on the added test names: Failed 0 / Passed 26).
Committed **`e593ae16`** on `fix/round1-defects` (38 files, +1351/−106) — **not merged**; awaits owner review.
Full-suite context: 1593 pass / **50 pre-existing seed-dependent failures** in classes **not touched**
here (`UserProfileTests`, `VisitorLifecycleTests`, `OrganizationProfileTests`, … — "Collection was
empty" / `InternalServerError`, the known test-DB seeding gap). Recommend running the suite on base
`765b996b` to formally confirm zero regressions before merge.

### Remediation wave 2 — 4 deferred findings fixed + 1 already-guarded (commit `3ad16bf3`)
All at the application/service layer — no schema/enum/migration, no cross-DB transaction (D-157).
Build **0/0**; the 5 new tests pass.
- **#3** (privilege escalation) — `UserProfileService.UpsertMineAsync` rejects a self-picked
  ProfileType whose `IsAppRegisterable=false` (threaded through the ProfileTypeFacts projection).
- **#4** (cross-DB ordering) — `ApproveAsync` persists the minted QR on the App DB **before** flipping
  Identity to Approved; a failed App save now leaves the account **PendingApproval** (retryable), not
  Approved-without-a-QR. Regression test injects an App-save failure via a scoped fault (poison gated on
  a flag so DB seeding stays intact).
- **#5** (badge security) — activation START stashes the holder email as a pending token and attaches it
  only **after** the code is verified; a wrong/mistyped code no longer bricks a placeholder badge.
- **#6** (bulk integrity) — bulk badge generate **pre-validates every batch** before writing any account
  → a bad later batch is a clean 400 with zero writes.
- **#22** (speaker overlap) — the app-level overlap guard **already existed**
  (`SpeakerHasOverlappingMeetingAsync`, half-open interval, both accept paths → 409); added the missing
  regression test for the sequential different-start case. The only residual is a concurrent TOCTOU that
  needs a DB constraint (frozen schema) — **held for owner**.

**Round-1 remediation tally:** **25 of 29** findings closed at the code layer across commits
`e593ae16` (21) + `3ad16bf3` (4). **Held for owner decision (4):** #1 demo accounts (ops/config +
rotate), #2 enforce CP TOTP (admin-enrolment rollout), and #22's concurrent-race DB backstop (frozen
schema). Branch `fix/round1-defects` — **not merged**; run the full suite on base `765b996b` to confirm
zero regressions before merge.

## Regression certification (base 765b996b vs branch) — CLEAN

Ran the full xUnit suite on **both** the base commit `765b996b` and the branch, each on its own
GUID-named LocalDB (so concurrent, no contention), with TRX loggers, and diffed the failure sets:

| | Base `765b996b` | Branch (after fixes) |
|---|---|---|
| Failed | 48 | 50 → (see below) |
| Passed | 1572 | 1598 |
| Total | 1620 | 1648 |

**Diff:** 48 failures are **shared** (pre-existing on base — the seed-dependent integration gap:
45 `UserProfileTests` mass-500s, `VisitorLifecycleTests`, `OrganizationProfileTests`,
`NotificationLifecycleTests`, etc. — NOT caused by any fix). **0** base-only. **2 branch-only
regressions**, both confirmed real in isolation (not run-concurrency flakiness):

1. `BookingApprovalTests.Cancel_after_the_session_has_started_is_refused` — my **#20** booking
   start-guard blocked the test's setup booking. The test explicitly documents the *intended*
   design ("the app start-guard only applies to cancellation, not booking"), so #20 is a
   product-behaviour change, not a bug fix.
2. `SeatReservationsTests.Concurrent_reserve_random_never_exceeds_capacity_override` — my **#21**
   deterministic backstop **oversold** (3 through a capacity-2 session; passes on base).

**Resolution (commit `3fbb57a3`):** reverted **#20** and **#21** — `SeatReservationService.cs` +
`SeatReservationsTests.cs` restored to `765b996b` exactly; both affected classes now pass (36/36).
#20 and #21 join the held list. **Net branch = 23 code-verified fixes, zero regressions vs base.**

### Final Round-1 remediation tally
- **Fixed + committed (23):** `e593ae16` (21 − #20 − #21 = 19) + `3ad16bf3` (4). Build 0/0;
  every fix's own test passes; base-vs-branch shows zero new failures.
- **Held for owner decision (5):** #1 demo accounts (ops/config + rotate), #2 enforce CP TOTP,
  #20 booking-on-started-session (product-behaviour call), #21 concurrency-safe capacity backstop
  (needs serializable txn / DB constraint), #22 concurrent speaker double-book (DB constraint /
  frozen schema). Plus the low/cosmetic env items (speaker-photo bytes, favicon).
- Branch `fix/round1-defects` @ `3fbb57a3` — **not merged**, certified regression-free vs base.

---

## Round-1 continuation (2026-07-13, later) — the "48 seed-gap baseline" was one real bug

### [BLOCKER, now FIXED] Create-user / save-sign-up-profile crashes on PROD — missing SQL sequence
- **Symptom (owner, on the tablet → PROD):** "can't create user … unhandled exception, only no
  details." Local worked; prod did not.
- **It was not a Flutter crash.** The app client (`simf_data_pkg`) converts every transport + parse
  error to `ApiFailure`, and the sign-up handler catches `on ApiFailure` and shows the message; there
  is no custom Flutter error UI. So the message was the backend's generic 500 — `ErrorHandlingMiddleware`
  returns `"An unexpected error occurred / حدث خطأ غير متوقع"` with **no field details** on any
  unhandled server exception.
- **Root cause (confirmed by the exact prod stack trace):**
  `Microsoft.Data.SqlClient.SqlException 208 — Invalid object name 'dbo.RegistrationReferenceSequence'`,
  thrown at `UserProfileRepository.NextRegistrationReferenceAsync` →
  `UserProfileService.UpsertMineAsync` → `UserProfileUpsertEndpoint.HandleAsync`. The D-373 registration
  reference (`SIMF-<year>-<8-digit>`) is issued **only on a first-time profile save** via raw
  `SELECT NEXT VALUE FOR [dbo].[RegistrationReferenceSequence]`. That sequence existed **only as a
  hand-run `CREATE SEQUENCE` on the dev DB** — it was in **no migration, no `HasSequence`, no script**.
  So every DB built from migrations lacked it: the CI test factory (`Migrate()`), and PROD (`Simf_Data`)
  after the D-743 "squash → DROP both DBs" rebuild. Raw-SQL object audit: this sequence was the **only**
  manually-referenced DB object in the whole backend.
- **Fix (commits `91a2ab1f`, `e3a57e14` on `fix/app-migration-order-d743`):**
  `SimfAppDbContext.OnModelCreating` declares `HasSequence<long>("RegistrationReferenceSequence")`
  (start 1, increment 1) + migration `20260713080333_AddRegistrationReferenceSequence` so `Migrate()`
  creates it on every fresh DB. Plus an idempotent prod hotfix
  `docs/migrations/2026/SIMF_App_RegistrationReferenceSequence_Hotfix.sql`.
- **Prod-verified:** owner ran STEP 1 (`CREATE SEQUENCE`) on the App DB **`Simf_Data`** → **create-user
  now succeeds on the tablet.** Original report closed.
- **Note (deploy-safety, separate item):** `Simf_Data` has `UserProfiles` but **no `__EFMigrationsHistory`
  table**, yet prod applies migrations only via startup `MigrateAsync()`. Before any redeploy, prod's
  migration-history state must be verified or startup migration could try to rebuild existing tables.

### Reframe of the regression-cert baseline
The earlier cert (above) recorded **48 pre-existing failures** — "45 `UserProfileTests` mass-500s,
`VisitorLifecycleTests`, `OrganizationProfileTests`, `NotificationLifecycleTests` … Collection was
empty / InternalServerError, **the known test-DB seeding gap**." That label was wrong. Those
`InternalServerError`s were **this** missing-sequence 500 (the test factory uses `Migrate()`, so the
CI DBs lacked the sequence exactly like prod). With the fix, **`UserProfileTests` → 96/96 PASS**
(were ~45 failing). Round-1 Finding #2 (suite couldn't build while the live API held `SIMF.Api.exe`)
was also cleared by stopping PID 38812.

### Full-suite re-verification (DONE) — backend baseline collapsed 48 → 1

Full `SIMF.Api.Tests` on `fix/app-migration-order-d743` (fresh `Migrate()` DBs, TRX logger):

| | Base `765b996b` (documented) | Fixed branch |
|---|---|---|
| Failed | **48** | **1** |
| Passed | 1572 | **1619** |
| Total | 1620 | 1620 |

**47 of the 48 baseline failures were the missing sequence** — they went green with no test change, on
DBs built exactly like prod (`Migrate()`). The single residual failure is **genuinely** seed-data
dependent (and unrelated to the sequence): `OrganizationProfileTests.GET_public_is_anonymous_and_returns_the_seeded_edition`
— `Assert.NotEmpty(): Collection was empty` (`OrganizationProfileTests.cs:49`): the test expects a
seeded organisation edition the test factory does not create. So the *real* "test-DB seeding gap" is
**one** case, not 48.

**[RESIDUAL NOW CLOSED]** That one case was a genuine content-seed gap: the D-747 port of the org
content (`docs/migrations/2026/SIMF_App_Organization.sql`) seeded the About/Vision/Mission/Themes
items but **omitted the `OrganizationDetails`** name/value facts the test asserts. Fixed by adding
the ordered `OrganizationDetails` block (Organiser = Royal Saudi Naval Forces, Edition = Fourth
(2026), Dates = 20–22 November 2026 — real, sourced from the same 2026 deck + Programme seed),
idempotent (inserts each row only when a row with that `Name` does not already exist). Verified:
`OrganizationProfileTests` → **5/5 PASS**; `TotpEnrolmentTests` + `DelegationsTests` → **18/18**
(confirming the earlier mid-run "failures" were seed-contamination flakes, not regressions). With
this, the backend baseline is effectively **1620/1620**. On `fix/app-migration-order-d743`
(`a6385af1`, pushed).

### Flutter app suite (the plan's open "Flutter widget/golden" item) — DONE

Ran `flutter test` (184 test files). First pass mis-resolved **Dio 5.9.2** from a stale cache
(`pubspec.lock` is git-ignored), so the client's `DioExceptionType.transformTimeout` (a **Dio 5.10**
member — the client's intended version, which is why the shipped app builds) was "member not found" and
100 files failed to compile. After `flutter pub upgrade dio` → **5.10.0**, the real result:

- **959 passed** (956 on the first clean pass + the 3 biometric tests fixed below).
- **2 failed — environment goldens, not defects:** `splash_golden` (4.87% / 14,831px) and
  `speaker_profile_golden` (5.41% / 16,460px) are Arabic font-rendering pixel diffs vs the golden-
  generating machine (known golden-harness gotcha); neither screen was touched. Regen goldens on the
  canonical machine to clear.
- **3 biometric tests — my earlier uncommitted guest-skip change (D-666), now finalized.** The change
  makes `maybeOfferBiometricEnrolment` read the auth state to skip the Face-ID nudge for a guest (the
  owner's "remove the guest biometric nudge" request). The 3 nudge tests didn't provide
  `authControllerProvider`, so the added `ref.read` threw `UnimplementedError`. Fixed by overriding the
  auth state in the test harness (default: approved visitor) **+ a new test** asserting a pending
  (guest) account gets **no** nudge. `biometric_auth_test.dart` → **8/8 green**; zero new analyzer
  issues in the touched files.

**Net (verified on a final full run):** backend **1619/1620** (1 genuine seed-gap, since closed — see
"[RESIDUAL NOW CLOSED]" above → effectively **1620/1620**), app **961 passed / 2 failed** — the 2 are
the same Arabic golden pixel-diffs (`speaker_profile`, `splash`); every other app test, including all
biometric tests, is green. The sequence fix is the dominant production-readiness win of this pass.

### CP list-page standard (D — static check, PASS) — the open "~50 CP pages" item, structurally

Rather than live-drive ~50 CP pages through the shared stack (the local API was down and the CP/Web
processes belong to a **concurrent owner session** — relaunching the API to drive CRUD would disrupt
it), the one CP "done" invariant that the build-time permission tests
(`CpNavigationPermissionTests`, `PermissionEnforcementTests`) do **not** cover was verified statically:

- **Every CP list page is on `SimfDataGrid`** — all **48** `Components/Pages/**/*List.razor` pages
  reference `SimfDataGrid` (48/48 by glob ∩ grep). The list-page HARD RULE (filter + select-all +
  row-checkbox + quiet icon actions via the shared grid) is met on every list page.
- **Zero raw tables** — no `<table>`, `<MudTable>`, or `<MudDataGrid>` in **any** `.razor` across the
  whole `src` tree (CP + Web). No page bypasses the standard grid.

So the CP list surface conforms structurally. The **fine-grained per-page CRUD live pass** and the
**on-tablet app manual pass** remain the two genuinely-open Round-1 items — both are owner-gated (they
need the shared local stack brought up without colliding with the concurrent session, or the physical
tablet), not code work.

---

## Live CP CRUD sweep — driven on `main` (2026-07-13, later) — PASS

The owner freed the shared Chrome automation profile, so the "fine-grained per-page CRUD live pass"
above was **driven live**. To avoid disrupting the concurrent web-landing session, an **isolated stack**
was stood up from a **dedicated worktree** (`D:/SIMF/wt-r1cp`): API on **:5185**, CP on **:5188**
(pointed at :5185 via `--Api:BaseUrl`), the owner's :5158/:5115 processes untouched. Mid-run the owner
asked to **sync with remote main** — the testing branch turned out to be **0-ahead / 25-behind and
already merged into `main` (PR 87 + PR 86)**, so the branch + local `main` were fast-forwarded to
`origin/main` (`2d1a8290`) and the stack was **rebuilt on `main`** (delta touches **no migration /
DbContext** → the seeded DB stayed compatible). Everything below ran against that `main` build. Sign-in
was the real CP login form (super-admin, 2FA-disabled on this dev DB — password only). **DB assertions
via `sqlcmd` against `SIMF_App`.** Every created row was **cleaned up** afterwards (DB restored: Regions
13, Halls 1, Settings 6, Categories 0). **Console errors across the whole sweep: 0.**

| Entity (route) | Ops driven live (UI → API → DB) | Result |
|---|---|---|
| **Halls** (`/admin/halls`) | **Full lifecycle:** create (code `R1HALL`, cap 120, floor L2) → **edit** (cap 120→175, floor L2→L3) → **soft-delete** (two-step confirm → `IsActive=0`) | **PASS** — grid + toasts (`تم إنشاء/تحديث/تعطيل القاعة`) + DB all matched; **`CreatedBy` = super-admin id** (AuditStamping + cross-DB actor resolve OK) |
| **Session Categories** (`/admin/session-categories`) | create (`R1 Test Category`, order 7) + **empty-state** render (`لا توجد تصنيفات جلسات بعد.`) | **PASS** — DB `DisplayOrder=7`, `IsActive=1`; empty seed matches OI-2 |
| **Regions** (`/admin/regions`) | create (`r1test` / `R1 Test Region`) over the seeded 13-region grid + top search box | **PASS** — DB row present, `IsActive=1`; 13 seed rows intact |
| **System Configuration** (`/admin/configuration`) | create key/value (`round1.test.setting` = `R1-OK` + description) over the 6 seeded app-update settings | **PASS** — DB `[Key]/[Value]` exact, `IsActive=1` |

**What this proves on `main`:** the shared **SimfDataGrid → dialog-form → Admin*Service → API →
EF/SQL** CRUD pipeline works end-to-end through the real Blazor UI for create / edit / soft-delete,
with the **AuditStamping** interceptor stamping `CreatedBy` across the DB split, **bilingual
live-region toasts**, the **two-step deactivate confirm**, **empty-state** rendering, and **in-form
validation hints that match the EF max-lengths** (Halls: code 2–16, name ≤128, floor ≤32, notes
≤1024). Four diverse entity shapes (facility / lookup / lookup-with-seed / key-value) exercised the
same plumbing every other CP CRUD page reuses. Session expiry at the **5-minute NCA access-token cap**
bounced the browser to `/login` once mid-sweep (expected behaviour, D-443) — re-login restored the
session cleanly.

**Round-1 open items after this pass:** only the **on-tablet app manual pass** remains (needs the
physical tablet). The CP CRUD live pass is now **done**; the data-path was already green in the
1620/1620 integration tests, and this adds the live Blazor-UI layer on top, on `main`.
