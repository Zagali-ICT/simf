# SIMF — Consolidated Requirements & Test Plan (2026-07-18) — v2

Status: **Scope refined against code (10-agent deep pass). Waves reordered.
4 build-gating decisions open (bottom). Awaiting owner "go" per wave.**

v2 supersedes v1. It folds in: 7 verification scouts + 3 adversarial critics
(completeness / technical-risk / test-plan). Every file path below was read this
session. Where v1 was wrong, the correction is marked **[FIX]**.

Precondition (owner rule): main in sync with origin before work starts.
**Verified 2026-07-18: local `main` == `origin/main` == `7c8ca4b8` (PR 100).**
Caveat: 40+ active worktrees exist under `.claude/worktrees/`; **each wave
re-syncs `main` and branches fresh** — the "in sync" check is point-in-time.

Binding rules (unchanged): checkpoint commit before edits; one issue per change;
review agents + `simplify` before commit; unit + integration + build 0/0 + live
device/DOM evidence before "done"; E2E catalogue + docs same changeset (D-246);
never touch `DECISIONS_LOG.md` in code commits; Identity schema + frozen enums
untouched; mobile wire contract append-only; App-DB additive migrations only (D-219).

---

## What changed in v2 (material corrections)

1. **[FIX] Meet-people filter targets non-existent profile types.** Seed has only
   8 types: Normal, Staff, Moderator, Media, **Sponsor**, **Exhibitor**, VVIP, VIP
   (`IdentitySeeder.cs:164-220`). **Speaker and Company do NOT exist.** Speakers are a
   separate `Speaker` entity (optional `Speaker.UserProfileId`); companies are
   `ExhibitorMembership`. → **Open decision Q-B.**
2. **[FIX] Booking-approval removal blast radius is large**, not 3 files. It breaks
   `BookingApprovalTests.cs`, `SeatReservationsTests.cs`, `PendingBookingExpiryWorkerTests.cs`;
   retires `Bookings.Approve/Reject` codes + the Reject contracts; orphans
   `NotificationKind.BookingRejected(42)` (kept, frozen — left unused); turns
   `PendingBookingExpiryWorker` into dead code; and changes shipped-app seat-card
   semantics (apps read `SessionSeatCell.Status` for the D-572 hint). 3 create paths
   set `Status=Pending`+`ExpiresUtc` — all must flip (`ReserveAsync`,
   `JoinOpenSeatingAsync`, `PickRandomSeat`).
3. **[FIX] The 4th seat state (تم التأكيد / checked-in) is NOT derivable from
   `Status`.** It needs a new appended `bool CheckedIn` on `SessionSeatCell`,
   server-computed by left-joining open `HallAttendance` rows. Additive; note the
   4-arg cell ctor at `SeatReservationService.cs:587` must change.
4. **[FIX] Walk-in-claims-freed-seat has no mechanism today.** Reserve is blocked
   after session start (`ReleaseMineAsync`/reserve guards throw `BookingSessionStarted`).
   A new "reserve while session is live" path is required. → **Open decision Q-A.**
5. **[FIX] Timezone must go FIRST.** The event-date meeting bounds and every booking
   screen render times; fixing the UTC↔Saudi convention after them forces rework.
   Timezone is now **Wave 1**.
6. **[FIX] Real event dates are missing.** `OrganizationProfile.EventStartDate/EndDate`
   hold a **stale placeholder** (2026-01-01 … 2026-04-30) from EF seed; the real
   20–22 Nov 2026 lives only in a free-text `OrganizationDetails` row.
   `BusinessMeetingService` deliberately does not gate on them yet (comment lines
   874-880). So "restrict meeting pickers to forum dates" **requires setting the real
   dates first**.
7. **[FIX] Bulk-add-by-count already has a CP surface** — `DelegatesPage.razor`
   (`/admin/delegates`, permission `Visitors.BulkGenerate`) → `POST /admin/visitors/bulk-generate`
   (`BulkBadgeBatch{ProfileTypeId,Count}`). Item shrinks to "verify + expose on the
   Visitors list if the desk wants it there."
8. **[FIX] Chatbot backend already exists.** `POST /app/ai/faq` (`AskFaqEndpoint`,
   AllowAnonymous) is wired to the real AI orchestrator but the `faq-answer` prompt is
   pinned to the **Echo** provider. Real work = app-side repository + route the prompt
   to `Anthropic` + verify `Ai:Anthropic:ApiKey` is populated + (optional) ground on
   `FaqEntry`. Much smaller than "replace a stub."
9. **[FIX] Profile-type dropdown is already white** (`sign_up_visitor_screen.dart:996`
   sets `dropdownColor: SimfTokens.surface`). The genuinely-black menus are
   `staff/register_visitor_screen.dart:733,850` (omit `dropdownColor` → fall back to
   the dark theme's `canvasColor`). Central fix = set `canvasColor` on `SimfTheme.dark()`.
10. **[FIX] Meet-people card cannot navigate today.** `Recommendation` carries only
    `userProfileId` — no `speakerId`/`boothId` — and there is no attendee-detail route.
    Tap-to-navigate needs appended target-id fields on `RecommendationEntry`.
11. **[FIX] Guest login wording is already «الدخول كضيف»** (correct ضيف, `app_l10n.dart:490`).
    The only guest/visitor string on the sign-in screen. → verify on device which word
    the owner sees as wrong (Q-D context: "login").
12. **[FIX] Job title is a single free-text field** (no Arabic/English pair). "Always
    English" is a data/entry rule, not a render toggle — render already shows the stored
    value verbatim. → **Open decision Q-C (data source).**
13. **[FIX] iOS project does not exist in the repo** → iOS Face ID cannot be built/tested
    here. Android biometric works. → **Open decision (iOS scope).**
14. **[FIX] GateOperator role already exists** in `AppRoles.CpRoles` and overlaps the
    proposed SecurityTeam. Must be reconciled. → **Open decision (roles).**
15. **[FIX] Adding roles is boot-fragile**: new roles must be in `AppRoles.CpRoles`
    (else silently un-granted) AND the new BaselineRoles lists must be declared **before**
    `All` in `PermissionCatalog.cs` (static-init order, hazard noted at lines 701-704),
    with exact code names (there is **no `GateScan` permission**; use `Gates.Operate` etc.).
16. **[FIX] Clean-code is interleaved, not batched-last.** Waves 1-4 build new Dart in
    files the sweep must clean; cleaning them twice re-locks 62 goldens twice. So: files a
    wave touches are brought to clean-code standard **within that wave**; the standalone
    sweep (Wave 5) covers only the untouched (cold) features.

---

## Owner decisions already recorded (v1)

Q3 meet-people = ProfileType flag only, enabled for Speaker/Exhibitor/Sponsor/Company
(see Q-B — 2 of 4 don't exist). Q4 confirmed = hall check-in. Q5 summary logic = skip.
Q6 dropdown-black = centralized-style fix. Q7 app email change = not needed. Q8 rename
tile = skip. Q9 = add security + scientific roles at seeding (see roles decision). Q10
guest wording = login screen. Timezone = whole system local (storage UTC). AI FAQ = make
real with the provided key. No approvals — reservation only. Check-in = staff scans QR at
hall gate, valid through the session; check-out→re-check-in must not double-count.
Auto-release all unclaimed reservations 3 min before start. New CP live hall page. Home
Sessions + Session-Summary buttons unchanged. Job title in English both languages. Q2
agenda-without-auth = still pending (excluded).

## Decisions taken in v2 (reasonable defaults — veto any)

- **DT-1 "غير متاح" = admin-blocked** (`Kind == AdminReservedRow`).
- **DT-2 OpenSeating rendering:** OpenSeating sessions have no seat grid; the 4 "states"
  collapse to **counts** on the CP live page (registered / checked-in) + an attendee list;
  the app shows the register button + confirmation copy (no grid). AssignedSeat sessions
  get the full 4-state grid.
- **DT-3 Retire, don't delete, `PendingBookingExpiryWorker`:** repurpose it into the
  T-3-minutes release worker (keep one worker) so there is no two-worker overlap; its
  24h `ExpiresUtc` trigger is replaced by "session starts within 3 min AND holder has no
  open `HallAttendance`."
- **DT-4 Reminder vs release:** the T-30 reminder already fires for un-released holders.
  Accept the known interaction (a reminded holder who never arrives is released at T-3);
  the release notification (`BookingReleased`) explains it. No reminder rescind.
- **DT-5 Real event dates** are set in `OrganizationProfile.EventStartDate/EndDate`
  (via the CP org-profile page + the seed SQL) as the prerequisite to W3 meeting bounds.
- **DT-6 Meet-people tap:** speaker person → speaker detail; exhibitor/company person →
  exhibition (booth) detail (the "gallery/معرض" surface). Needs appended target ids.

---

## Wave 0 — Urgent (deadline Sunday 2026-07-19)

**W0-1 CP user manual.** `docs/manuals/Admin-Manual.md` exists (Jun 30). Review against
current CP pages, fill gaps, export to delivery format. Risk: none (docs).

---

## Wave 1 — Timezone: whole system displays Saudi local (storage stays UTC) — FIRST

Rationale: dependency root for W3 meeting bounds + all booking-screen times.

- **W1-1 CP central helper.** New `DateTimeOffset`→Saudi extension in
  `src/Shared/SIMF.Common/` (Windows tz id `"Arab Standard Time"`, UTC+3, no DST;
  fallback if the id is missing). No CP date helper exists today.
- **W1-2 CP sweep.** Route every render site through it: **43 `.UtcDateTime` hits / 25
  files**, **11 `.ToLocalTime()` / 8 files**, **5 `.LocalDateTime` / 2 files**, plus the
  `HH:mm`/`ToString("yyyy...")` formatters (48 files total). **8 `datetime-local` input
  files must convert BACK to UTC on save** (Sessions/Banners/BusinessMeetings/Hall/
  MeetingTables/Operations/SpeakerAvailability add/edit). Risk: **wide silent-failure
  surface** — a missed site shows the wrong time and won't fail the build → mitigated by
  a grep-complete inventory + per-page DOM check.
- **W1-3 App.** Add `toRiyadh()` in `lib/core/utils/` (beside the month/weekday helpers)
  and route the ~20 `.toLocal()` sites + the high-leverage `startLocal`/`endLocal` getters
  (`session_models.dart`, `my_sessions_models.dart`, `speaker_models.dart`) through it.
  Phase/"is-live" comparisons stay UTC. Risk: **goldens showing times re-lock**
  (`--update-goldens`, expected).

---

## Wave 2 — Session join / seat booking (the agreed mechanism)

Order within the wave fixed per critic: **check-in correctness (W2-6) is settled before
the release worker (W2-5) keys off it.**

1. **W2-1 Reservation-only (remove approval).** All 3 create paths write `Status=Approved`;
   `BookingConfirmed` fires on create. CP Bookings page → reservation list + release only
   (approve/reject/bulk-approve gone). Full consumer list handled: endpoints, service +
   helpers, `SimfAdminClient`, CP BFF, permission codes, i18n, and the 3 test suites
   above. `BookingStatus.Approved`-on-create is **intentional** under "no approvals"
   (documented so no gate creeps back). Risk: **breaking** (intended).
2. **W2-2 Case 1 — OpenSeating:** button **«سجل لحضور الجلسة»** → existing `join` →
   exact copy «تم تسجيلك لحضور هذه الجلسة بنجاح. هذا التسجيل لا يعني حجز مقعد أو ضمان
   الدخول للجلسة، سيتم تأكيد دخولك عند تسجيل الدخول للجلسة» (+EN). Risk: none.
3. **W2-3 Case 2 — AssignedSeat:** button **«الانضمام إلى الجلسة»** → seat picker →
   «حجز» → exact copy «تم حجز المقعد بنجاح سيتم الغاء الحجز في حالة عدم تسجيل الدخول
   للجلسة قبل 3 دقائق قبل بدء الجلسة لاتاحة المقعد لأشخاص اخرين» (+EN). Button/message
   chosen from the session's effective `SeatSelectionMode`. Risk: none.
4. **W2-4 Four seat states (AssignedSeat grid):** متاح / غير متاح (`AdminReservedRow`) /
   محجوز (`Approved` + not checked-in) / تم التأكيد (`Approved` + open `HallAttendance`).
   Add appended `bool CheckedIn` to `SessionSeatCell`; update all construction sites
   (incl. the 4-arg ctor). App legend + `hall_seat_map.dart`. Risk: none (append-only).
5. **W2-6 Check-in / check-out correctness (do before W2-5):** staff scans visitor QR at
   the hall gate (app generic gate scanner on a Both-mode hall-door gate → server chains
   to `HallAttendance`; OR CP `HallArrivalsConsole` QR). Verify: valid only within
   `[start−15m, end+15m]` (`EnsureSessionLiveNow`); check-out→re-check-in re-opens a fresh
   row but aggregates count **distinct users** (verify `SessionAttendanceService`
   Distinct queries hold). Risk: none (verify + assert).
6. **W2-5 T-3-minute auto-release (repurpose `PendingBookingExpiryWorker`, DT-3):** for
   sessions starting within 3 min, release every `Approved` hold whose holder has **no**
   open `HallAttendance`; fire `BookingReleased`; **exclude checked-in seats**. Freed
   seats → متاح. Risk: performance (1-min indexed poll).
7. **W2-7 Walk-in claims a freed seat (Q-A = staff-assign).** Staff scans the visitor badge
   QR at the hall gate; the system assigns a free seat (and check-in in the same step).
   Requires a new staff assign endpoint + gate UI, operating during the live window (past
   the `BookingSessionStarted` guard), capacity-checked. Risk: none (additive endpoint).
8. **W2-8 NEW CP page — live hall/session status.** One page (mirrors the app seat map):
   full 4-state grid (AssignedSeat) or live counts (OpenSeating, DT-2) + everyone
   currently in the hall (open `HallAttendance`) with full visitor data + live counts +
   release/check-out actions. New `PermissionCatalog` code(s) declared before `All`, seeded,
   API+page gated, nav entry — satisfying `CpNavigationPermissionTests` + `PermissionCatalogTests`.
   Risk: security (new gated admin surface). **No new tables/columns.**

---

## Wave 3 — CP + backend features

1. **W3-1 Real event dates (DT-5, prerequisite to W3-2).** Set
   `OrganizationProfile.EventStartDate/EndDate` to the real window (CP org-profile page +
   seed SQL). Risk: none.
2. **W3-2 Meeting time restricted to forum dates.** Enforce min/max on the
   `datetime-local`/`<select>` slot inputs (`BusinessMeetingsList`, hall/speaker
   available-slots builders) and **server-side range-reject** in `ValidateSlot`
   (BusinessMeeting), the delegation submit/respond blocks, and the two availability
   services — all keyed off the now-real event dates; handle null dates gracefully. Risk: none.
3. **W3-3/W3-4 Meet-people = partner directory (Q-B → Option 2, pending owner confirm).**
   Replaces the ProfileType-flag design. New backend query/endpoint (do NOT overload the
   interest-scored `MeetPeopleLikeYouAsync`) that unions `Speaker` / `Sponsor` / `Exhibitor`
   projections (App-DB, single context), each entry carrying `Kind` + the source-entity id
   + name EN/AR + tier/role label + logo/photo. New/extended contract in `Recommendations.cs`
   (append-only). Optional additive `ShowInMeetPeople` on the partner entities (or reuse
   `Speaker.AllowsDataSharing`). App: `meet_models.dart` gains `kind` + `targetId`;
   `meet_repository.dart` points at the new endpoint; `meet_match_card.dart` gains `onTap` →
   `speakerProfile`/`exhibitorDetail`/`sponsorDetail` by `kind`, and replaces the "% تطابق"
   block with a tier/role chip for partner rows. Interest engine left intact as a possible
   secondary "people like you" section. Gated on the 5 owner clarifications above. Risk:
   none (additive App migration + append-only wire).
5. **W3-5 Bulk-add-by-count.** Verify `DelegatesPage` bulk flow end-to-end; if the desk
   wants it on the Visitors list, add the entry there (permission `Visitors.BulkGenerate`
   already exists). Risk: none.
6. **W3-6 Team roles at seeding (needs roles decision).** Add `SecurityTeam` +
   `ScientificTeam` to `AppRoles` **and** `AppRoles.CpRoles`; declare their BaselineRoles
   lists **before** `All`; attach exact codes — SecurityTeam: `Gates.*`, `Attendance.View`,
   `HallArrivals.*`, `Operations.*`, `Statistics.View`; ScientificTeam: `Sessions.*`,
   `ProgrammeDays.*`, `ProgrammeTimeline.View`, `SessionCategories.*`, `Themes.*`,
   `SessionSummaries.*`, `Questions.*`, `SessionModerators.*`, `SessionModeration.Moderate`,
   `Speakers.*`, `Halls.*`, `SeatLayouts.*`, `SeatPlans.*`, `Bookings.*`. Reconcile the
   existing `GateOperator` role (roles decision). Seeder is idempotent on existing prod
   (adds rows/grants on reboot) but **seeds no memberships** and takes effect **after
   admins re-login**. Risk: security (mapping) + deploy (re-login).
7. **W3-7 Workshops management.** CP manages workshops (title/time/`CapacityOverride`/
   check-in via Wave 2) filtered by `SessionType.Workshop` (all four fields already on
   `SessionsAddEdit`); app shows **title + time only** for workshops. Risk: none.
8. **W3-8 AI chatbot made real (smaller than v1).** App: add `features/chatbot/data`
   repository → existing `POST /app/ai/faq`; swap a real `ChatbotResponder` behind the
   existing `chatbotResponderProvider` seam. Backend: route the `faq-answer` prompt to
   `AiProvider.Anthropic` (CP prompt provider or `Ai:DefaultProvider`); verify
   `Ai:Anthropic:ApiKey` is populated (503 `AI_PROVIDER_NOT_CONFIGURED` if empty);
   graceful bilingual fallback on 429/5xx/timeout. Optional: ground on `FaqEntry` (RAG)
   like `AdminSessionSummaryService` injects transcripts. Key stays server-side; never
   printed/committed. Reuses existing input caps (16/64/4000), per-prompt max-tokens,
   per-IP rate limit, `AiInvocation` logging + dashboard. Risk: security (key handling).
9. **W3-9 Programme content feed.** Extend `SIMF_App_Programme.sql` (5 placeholders) with
   the real client programme when it arrives (blocked on client data). Risk: none.
10. **W3-10 B2B/B2C/G2B** — already built (`BusinessMeeting` + `BusinessMeetingsList`).
    Test-only.
11. Home «الجلسات» + «ملخص الجلسات» — unchanged (owner).

### Added 2026-07-19 (owner batch — Q&A / summary / rating). Build after verifying the existing implementations (question-queue, session-summaries, rating-config) against each spec below; several overlap and need deltas, not greenfield.

12. **W3-11 Q&A — two submission paths converging on the moderator (owner 2026-07-19).**
    Two ways a visitor submits a question:
    - **(a) Live in-hall Q&A (during the session):** only a visitor **currently in the
      hall** — an open `HallAttendance` row for that session, i.e. checked in — may ask.
      The question goes **straight to the moderator** → accept / reject (no AI, no
      scientific filter).
    - **(b) Pre-Ask (before the session):** open to submit ahead of time → pipeline
      **AI filter → Scientific-team filter → Moderator** accept/reject. Each stage can
      drop/hold; only questions surviving all three reach the moderator as "accepted".
    **As-built (audit 2026-07-19):** the whole pipeline already exists (D-212/D-233) —
    app `send_question_screen` → `SessionQuestionService.SubmitAsync` (runs the dynamic
    `question-filter` AI on **every** submission, advisory only, off by default) →
    `QuestionPhase.Pre/Live` set but **both phases run one pipeline** → Scientific
    **Committee** queue `QuestionQueueList` (approve/hide/escalate, `Questions.*`) →
    per-session **Moderator desk** `SessionModerationDesk` (push/reorder/hide, Approved-only).
    Live attendance gate exists in `SubmitAsync` but checks **any** `HallAttendance` row and
    **only** when the hall has a geofence.
    **Decisions (owner 2026-07-19):** map **Committee = scientific-team filter**, **per-session
    desk = moderator**. Deltas: (1) branch on `Phase` — **Live** skips AI + Committee and goes
    straight to the Moderator desk, which **gains accept/reject** (today push-only); (2) tighten
    the live gate to an **open** `HallAttendance` (`LeaveUtc == null`) and apply it even to
    geofence-less halls; (3) **Pre-Ask** runs AI-filter → Committee → desk (turn the AI filter
    on for pre-ask and make the AI dynamic prompt persist input+output per W3-14). Perms:
    `Questions.*` + `SessionModeration.Moderate` (exist). Risk: mostly logic; possibly one
    additive column; no new enum (Status already Pending/Approved/Hidden).
13. **W3-12 Session summary — YouTube subtitle → AI draft → scientific-team edit (owner
    2026-07-19; reverses the earlier "Q5 summary logic = skip").** Pipeline: pull the
    **subtitle/transcript from the session's YouTube** (`Session.LiveStreamUrl`, YouTube per
    D-349) → **AI** drafts the محضر/summary from the subtitle → **Scientific team** reviews +
    **edits** before publish (ties to the app's existing published-summary gate). Verify vs
    `AdminSessionSummaryService` + CP session-summaries + D-578 (subtitle→AI محضر already
    partly built). **As-built (audit 2026-07-19):** subtitle fetch already exists
    (`YoutubeTranscriptService`, D-578; innertube+JSON3, SSRF-hardened, saved onto
    `Session.LiveCaptions`, cap 2048); AI draft via the dynamic `session-summary` prompt →
    `SessionSummary.FullTextArabic`; a full **Draft → SubmitForReview → Approve → Publish**
    review workflow (D-472) already exists. **Gap:** `SetPublishedAsync` gates Publish only
    on `now >= StartUtc` — it does **not** require `ApprovedAt`, so an unreviewed draft can
    reach the app. **Decision (owner 2026-07-19):** scientific edit + review is **mandatory
    before publish** — hard-gate `SetPublishedAsync` on `ApprovedAt != null`. Deltas: the
    hard-gate; preserve the pristine AI draft as a read-only snapshot distinct from the
    edited text (W3-14); consider raising the 2048 `LiveCaptions` cap for long transcripts
    (owner nod — touches a frozen-ish column). On-prem YouTube egress is blocked → paste/
    upload fallback already handled. Risk: logic + one additive snapshot column (+ optional
    cap change).
14. **W3-13 Dynamic rating — seed 4 scopes + attendance-gating (owner 2026-07-19).** Dynamic
    rating exists (rating-config, D-496). **Seed** four rating targets/scopes: **App**,
    **Session**, **Day** (programme day), **Overall exhibition** (all days). **All rating is
    attendance-based** — a user may rate a target only if they attended it: Session = has an
    (open or closed) `HallAttendance` for that session; Day = has attendance that day;
    Overall = has any attendance across the event; App = any approved attendee (confirm).
    **As-built (audit 2026-07-19):** the dynamic model is fully built (D-496) —
    `RatingType`(Code/Scope) → `RatingQuestionGroup` → `RatingQuestion` + `RatingResponse`/
    `RatingAnswer`, unique `(UserId, RatingTypeId, TargetId)`. `RatingScope` already has
    **Global / PerSession / PerDay** (no new enum — App+Overall = Global). `RatingSeeder` (C#)
    seeds **five** system types: App(Global), Session(PerSession)+3 Qs, Day(PerDay),
    Event(الملتقى, Global), Exhibition(المعرض, Global). **There is NO attendance gate on submit**
    — `ResolveTargetAsync` only checks the target exists+IsActive; the app `rate_screen` always
    loads the form. **Decisions (owner 2026-07-19):** collapse the five to the **four** named
    scopes (App / Session / Day / **Overall**, merging Event+Exhibition → Overall — say the word
    to keep both); **all four attendance-gated on `HallAttendance` (in-hall check-in)** — Session
    = an `HallAttendance` for that session; Day = a check-in that programme day; App + Overall =
    **any** check-in (no no-shows). Deltas: add the eligibility check to `SubmitAsync` **and** hide/
    disable the app rate surface when not attended; reshape the seeder to 4. Seed convention
    (D-718): scopes = C# auto-seed; question copy = SQL. Risk: logic + seeder reshape; no schema.
15. **W3-14 AI transparency — the team sees the raw text before any AI step (cross-cutting
    W3-11 + W3-12; owner rule 2026-07-19).** In **every** AI-processing case the relevant
    team (Scientific) can **view the raw source text before the AI updates it** — the raw
    question text and the raw YouTube subtitle are persisted and shown alongside/prior to the
    AI output, so a human always sees the input and the AI never silently overwrites without
    the raw visible. Applies wherever AI drafts/filters content.
    **As-built (audit 2026-07-19):** the raw "before" text already persists **unredacted** in
    the domain tables — `Session.LiveCaptions` (subtitle) and `SessionQuestion.QuestionText`
    (verbatim; AI never rewrites it, only tags `AiFilterVerdict`). The central `AiService` also
    saves input+output per call to `AiInvocation`, but that copy is **PII-redacted** (NCA posture)
    and lives in the telemetry log. **Decision (owner 2026-07-19):** the AI is a **dynamic prompt**
    applied to the text with **before+after saved** on every AI op — satisfied for source text by
    the domain tables; deltas are **UI + a snapshot**: surface the raw subtitle in the summary
    editor and preserve the **pristine AI draft** as a read-only snapshot so the edited-vs-AI diff
    is always visible. **No NCA/redaction override needed.** Risk: display + one additive snapshot
    column (shared with W3-12).

---

## Wave 4 — App fixes

1. **W4-1 Face-liveness direction on Android.** Make the D-684 mirror swap in
   `identity_verification_screen.dart` (`_stepPrompt`/`_stepLeading`) platform-conditional
   (iOS correct today). Verify on device. Risk: none.
2. **W4-2 Face/registration submit error — DIAGNOSED (prod-state / transport, not a HEAD
   code bug).** The face capture is a separate on-device step; the failing call on submit is
   the 3rd of 3, `POST /app/account/user-profile` (`upsertMyProfile` →
   `UserProfileService.UpsertMineAsync`) — the id-image/avatar uploads have their own
   specific error messages, so the generic dialog can only come from the create call.
   Ranked causes: **(#1)** `dbo.RegistrationReferenceSequence` missing again on prod
   `Simf_Data` — the sequence **is** created by migration `20260713121810_20260712001` at
   HEAD, but prod has **no `__EFMigrationsHistory` table**, so a re-migrate can drop it; a
   documented recurrence (`SIMF-Round1-Run-Log.md:308-331`), surfaces as "حدث خطأ غير
   متوقع / An unexpected error occurred." **(#2)** another create-path 500 (Identity
   transaction, or the PII blind-index if the encryption key is missing on prod). **(#3)**
   real transport failure — surfaces as "تعذر الاتصال بالخادم / check your internet"; prod
   base URL uses an **underscore hostname** `simf_api.zagali-ict.com` (spec-invalid, some
   Android stacks reject) on a **self-signed cert** (app trusts all, so TLS isn't the app
   blocker; a real CA cert on a valid host is still owed).
   **OWNER CONFIRMED the on-device text = "could not reach the server, please try again."**
   That is the app's `errorServerUnavailable`/`networkErrorBody` path (non-JSON response or
   transport failure) — **NOT** the "unexpected error occurred" a caught backend 500
   (missing sequence #1 / create-path 500 #2) would produce. So the cause is
   **infrastructure (#3/#4)**: the create `POST /app/account/user-profile` gets a
   reverse-proxy 502/504, a timeout, or a non-JSON body. The same screen's earlier GETs
   succeed (form loads), so it is specific to that POST. Next steps (ops): pull the prod
   **reverse-proxy + API logs** for `POST /api/v1/app/account/user-profile` at a failed
   attempt — 502 (backend crash/restart), 504 (create path hangs — cross-DB
   transaction/sequence/deadlock), or a non-JSON error page? Verify the create path isn't
   timing out and the underscore host `simf_api.zagali-ict.com` + self-signed cert aren't
   mishandled by the proxy/DNS. Cheap parallel rule-out: `SELECT 1 FROM sys.sequences WHERE
   name='RegistrationReferenceSequence'` on prod `Simf_Data`. Code hardening (either way):
   surface the real backend status/code instead of masking every failure as "couldn't reach
   the server". iOS Face ID (no `ios/` project) deferred. Risk: ops (prod infra) + hardening.
3. **W4-3 30+ min video.** Live player has a 60s keep-alive → no session-extension warning;
   ensure any other video surface has it; run the real 30-min test. Mostly test.
4. **W4-4 Dropdown black background (centralized).** Add `canvasColor` to `SimfTheme.dark()`
   in `app_theme.dart` (covers every `DropdownButtonFormField` that omits `dropdownColor`,
   incl. staff nationality/organisation menus); verify the profile-type menu (already white)
   on a fresh build. Golden impact only if a dropdown is open in a golden (none). Risk: none.
5. **W4-5 Guest login wording.** Verify the sign-in screen on device; the only candidate is
   `guestSignInLink` = «الدخول كضيف» (`app_l10n.dart:490`). Fix the exact word the owner
   flags. Risk: none.
6. **W4-6 Staff/moderator pages verification (incl. Q1 tablet exhibitor check).** Verify
   each exists per Figma + test on device: gate scan (`gates/gate_scan_screen.dart`,
   758:4651), staff register-visitor (`staff/register_visitor_screen.dart`, 1467:12357),
   moderator (`moderation/session_moderate_screen.dart`, 1461:12227), exhibitor scan +
   my-visitors (`exhibitor/` — **built but unmapped in Figma node map**), booth/exhibitor
   detail (1439:11881). Note: **no dedicated hall-scan screen** — hall check-in rides the
   generic gate scanner (Both-mode hall gate) or CP `HallArrivalsConsole`. Fix the stale
   node-map path (`features/profile/…` → `features/account/…`). Risk: none.
7. **W4-7 Job title in English (needs Q-C).** Single free-text field, no AR/EN pair, render
   is already verbatim. "Always English" = an entry-side rule (enforce English at CP+app
   entry) or a second column / translation. Data decision = **Q-C**. Risk: none (render).

---

## Wave 5 — Clean-code sweep (COLD features only; hot files cleaned in their wave)

Foundation (single commit): create `lib/app/theme/app_style.dart` (central text styles;
migrate the 3 styles now inside `SimfTokens`); add `SimfTokens.surface70`
(≈`Color(0xB3FFFFFF)`) + `SimfTokens.transparent`; fill `AppAssets` with every inventoried
literal (icons, `discover_hero.jpg`, 3 onboarding videos, world map, social + home-tile icons).

Then per **cold** feature (one commit each), with the **verified real names** (v1's list had
drift): `Colors.white/white70/transparent`→tokens; inline `TextStyle(`→`app_style.dart`;
hardcoded numbers→`SimfTokens.spaceN` (`space4`=16 exists); `assets/…`→`AppAssets`; extract
private widgets to correctly-spelled files (`archive_body.dart` [note: `_ArchiveBody` lives
in the screen file], `archive_gallery_tile.dart` [it's `_GalleryTile`, a tile],
`faq_tile.dart`, `gallery_placeholder_box.dart` + `gallery_media_tile.dart` [`_MediaTile`],
`exhibition_identity_card.dart` + `exhibition_link_row.dart` [both in `entity_detail_scaffold.dart`],
`booths_hall_box.dart` + `booths_contact_box.dart` [`_ContactBox`], `contact_us_info_row.dart`
[`_InfoRow`] + `contact_us_social_button.dart` [`_SocialButton`], `contacts_channel_row.dart` +
`contacts_error_state.dart` [`_ErrorState`] + `contacts_preview_sheet.dart` [`_ContactPreviewSheet`],
`gates_*` [three are **methods** not classes], `live_*`, `meet_topic_chip.dart` [`_TopicChip`],
`moderation_*`, `home_high_light_slide.dart` [`_HighlightSlide`] + `home_social_button.dart`);
rename `_Card→AboutCard`/`_CardHeading→AboutCardHeading` (in **`about_cards.dart`**, plural);
promote `NaviFormField` to `core/widgets` and use it in contact_us + feedback; replace the two
builder-method fields in `speakers/widgets/meeting_request_sheet.dart` with `SimfLabeledTextField`.

**Verified deviations (no action):** delegations has **no `_SectionHeader`** and
`DelegationCard` is already public; guest already uses shared `SimfPageShell` (no divergence);
`send_question_content.dart` already correct.

Gate: behaviour-preserving; tests + goldens green/updated. Risk: none.

---

## Wave 6 — Test plan (per D-246 with each item + a final regression round)

**Stale scenarios to REWRITE when approval is removed (W2-1):**
`cp-admin-bookings.md` E2E-BKG-001/002/003/004/005/006/007/008/010/013/014 (full rewrite to
reservation-list + release; BKG-009/011/012/015 survive); `mobile-my-seat.md` MOB018-009
("held Pending") + legend 3→4 states (MOB018-017); `mobile-seat-picker.md` header +
MOBPICK-001 legend + MOBPICK-002 (pending toast) + MOBPICK-010 (24h expiry → T-3);
`mobile-session-detail.md` MOB017-016/020/022. **Do NOT touch** speaker-meeting / document /
badge Accept-Reject (those are meeting/doc approvals, not booking).

**New scenarios (house id style) — the 4-state matrix + edges:**
MOB018-018 (تم التأكيد + legend); MOBPICK-011 (reserve→Approved immediately, exact copy,
BookingConfirmed on create); MOBPICK-012 (4-state legend); MOB017-028 (OpenSeating register
copy, no "pending"); HLS-001..004 (CP live page: grid 4 states / in-hall list+data / release+
check-out+auth gate / live محجوز→تم التأكيد on gate scan); HAR-018 (check-out→re-check-in = 1
distinct); HAR-019 (check-in outside window rejected); SEAT-T3-001 (release non-confirmed,
skip confirmed, BookingReleased); SEAT-T3-002 (walk-in reselect at gate → تم التأكيد);
SEAT-T3-003 (freed-seat race, one wins); MOBPICK-013 (same-seat concurrent → 409, no Pending);
MOBGATE-006 (offline scan queue, no double check-in); MOB036-007 (real Claude answer) +
MOB036-008 (API 429/5xx/absent-key → bilingual fallback); ROL-025/026 (SecurityTeam denied
science modules; ScientificTeam denied gates/attendance — nav hidden + API 403);
TZ-001 (same UTC row → Saudi local in CP + app; near-midnight correct day; missing-tz fallback);
BMT-020 (null event dates degrade safely + server range-reject); plus ShowInMeetPeople negative
(a false-flag type excluded), bulk-add count=0/neg/large + gate.
**Define first (DT-2):** how the 4 states render for OpenSeating before authoring that half.

**Test book + device round:** regenerate `SIMF-Production-Readiness-TestBook.xlsx` via
`tools/testbook/build_testbook.py`; add journeys to `SIMF-Business-Flows.md`. TXZ-W09 vs
production round must add: each of the 4 states observed live; live محجوز→تم التأكيد after a
real gate scan; T-3 release + gate reselect; check-out→re-check-in = 1 on the CP counter; the
new CP live page (DOM/screenshot); CP+app both render Saudi local; Android face-direction;
biometric (Android; iOS blocked); chatbot live answer + fallback; job-title-in-English;
dropdown fix; guest wording; offline hall scan.

**Per-change gates:** unit + integration green; `dotnet build -c Release` 0/0; `flutter
analyze` + `test`; goldens; live DOM/screenshot for every CP page touched; review agents +
`simplify` before every commit.

---

## Decisions resolved (owner, 2026-07-18 round 2)

- **Q-A RESOLVED = staff-assign.** The walk-in / bumped visitor gets a freed seat when
  **staff scans the visitor badge QR at the hall gate** and the system assigns a free seat.
  W2-7 = a staff-driven assign endpoint + gate UI (not visitor self-select). Still needs a
  path that operates during the live window (past the `BookingSessionStarted` guard).
- **Q-D RESOLVED = keep both.** Keep `GateOperator` (app gate operators) AND add
  `SecurityTeam` (broader CP role). No migration of existing gate operators. W3-6 seeds
  two new roles (Security + Scientific) beside the three existing.
- **Q-B RESOLVED → meet-people is HELD (owner, do not build now).** When resumed, build it
  as the partner directory (Option 2) below, including **Sponsors** (speakers + companies +
  sponsors), and **keep an interest-based "people like you" attendee section as secondary**.
  Rationale retained below.
- **Q-B analysis (for when resumed) → Option 2 (partner directory).** The original
  ProfileType-flag idea (Option 1) is structurally
  unworkable: Sponsors have **no** user account, external Speakers and Exhibitor companies
  have **no** account/interests, so the interest-scored `RecommendationService` candidate
  pool (Approved `SimfUser` + `UserProfile` + shared interest) can never surface them, and
  the pool key (`UserProfile.Id`) is not the `speakerId`/`boothId` the tap needs.
  **Recommendation:** build the "meet" list as a **partner directory drawn from the
  `Speaker` / `Sponsor` / `Exhibitor` App-DB tables** (a new query/endpoint, not the
  interest scorer), each row carrying a `Kind` + source-entity id for tap-to-detail; keep
  the interest engine ("people like you") as a separate concern. Additive App migration
  only (optional `ShowInMeetPeople` on the partner entities — or reuse Speaker's existing
  `AllowsDataSharing` consent); Identity untouched; wire append-only.
  **Owner clarifications before build:** (1) should Sponsors (org, no person) appear as a
  "meet" card at all, or only Speakers + Exhibitor companies? (2) Exhibitor tap →
  `exhibitorDetail` (`/exhibitors/:boothId`) or the gallery/exhibition screen? (3) drop the
  "% تطابق"/interest-reason for partner cards (recommended — meaningless for orgs) and show
  tier/role instead? (4) keep attendee interest-matching as a secondary section, or make the
  screen purely a partners directory? (5) order partners by tier/display-order or soft-sort
  by the caller's interests?
- **Biometric REFRAMED (not iOS).** Owner: the face reads and data is entered correctly,
  then a **"cannot connect to server / network" error** appears. So the defect is a
  **server/network failure on submit**, not iOS Face ID. W4-2 is now a root-cause diagnosis
  of the face-capture/registration submit path (strong lead: the known
  `RegistrationReferenceSequence`-missing-migration bug → "can't create user"). iOS project
  absence is a separate, deferred concern (owner has no Mac).

## Still blocked on owner (non-gating)

- **Q2** agenda-without-auth (reverses D-576) — pending.
- **Q-C** job-title data source (enforce English entry vs add a column) — W4-7.
- **W3-9** real programme content — blocked on client data.
