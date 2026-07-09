# Feature Design Specification — Engagement

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-007 |
| Title | Feature Design Specification — Engagement |
| Version | 1.1 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-FDS-003, SIMF-FDS-004, SIMF-SRS-001, SIMF-UCS-001, SIMF-DAT-001, SIMF-RDR-001, SIMF-SAD-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The engagement feature, build-ready. |
| 1.1 | 2026-05-21 | Engineering & Architecture Team | Architecture-review amendment (see Amendment A): SignalR group fan-out and comment batching, the backplane as a deferred decision, graceful degradation. |
| 1.2-DRAFT | 2026-07-08 | Engineering & Architecture Team | **DRAFT amendment (D0-2, Amendment B) — pending owner sign-off.** Folds the already-built 3-stage Q&A pipeline (AI→Scientific Committee→per-session Moderator; Pre/Live phases — completion-programme D-212/D-233/D-236/D-271/D-519) into this spec and defines the owner's item 12 ("ask a speaker — two ways") against it. **Finding: item 12 is built end-to-end;** only small deltas remain (real-AI wiring vs stub; a distinct pre-session ask entry; reproduce the "not working" report). Open items in §B.5 — **owner-resolved 2026-07-08** (wire real AI; add pre-session entry). |
| 1.3-DRAFT | 2026-07-08 | Engineering & Architecture Team | **DRAFT amendment (D0-3, Amendment C) — pending owner sign-off.** The multi-trigger ratings home (owner item 8). Records the built ratings system (completion-programme D-677/D-678/D-679/D-680/D-690) and maps the owner's 4 time-triggers onto it. **Finding: 2 of 4 triggers built** (daily-if-checked-in + end-of-programme, D-679); **2 gaps** — rate-on-gate-checkout (GAP-A) + rate-on-live-close (GAP-B) — plus a "watched at time/date" display. Open items in §C.5. |

---

## 1. Purpose

This is the build-ready specification for engagement during a session — the
live broadcast, the questions an attendee puts to the moderator, and the
comments, with their moderation. It is how the audience takes part in a
session.

## 2. Scope

The feature covers:

- the live broadcast of a session, with AI translation or captions and a
  language choice,
- the geographic restriction on the live stream,
- session questions — composing a question, the recipient, and the rule that
  gates when questions are open,
- the moderator's handling of questions for the sessions assigned to them,
- comments — composing a comment and the two-stage moderation, AI then admin.

It does **not** define sessions — that is the Forum Programme feature
(SIMF-FDS-004). It does not produce the hall-arrival record it depends on —
that is Badge & Access Control (SIMF-FDS-003). The **AI session summary** is
the Networking & Cognitive AI feature (SIMF-FDS-008), not this one.

The standalone mockup screens for "send question", "request interview" and
"audience comments" (26–28) were removed as screens; the question and comment
**features remain**, integrated into the live and session experience, and the
interview-request feature is dropped (SIMF-CON-001 section 14).

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-701 live broadcast, AI translation, language | UC-13 Watch a live session |
| FR-702 the Riyadh-region restriction | UC-13 |
| FR-703 ask a question, recipient | UC-14 Ask a question in a session |
| FR-704 questions gated by hall arrival, closed at session end | UC-14 |
| FR-705 the moderator handles questions | UC-36 Manage the questions of an assigned session |
| FR-706, FR-707 comments and two-stage moderation | UC-15 Comment, UC-26 Moderate comments |

Decision **D5** governs the question timing and the comment moderation.

## 4. Feature overview

```
Session marked live (FDS-004)
        │
        ▼
  Live broadcast ── AI translation/captions ── Riyadh-region restriction
        │
        ├─▶ Questions  ─▶ gated by hall arrival ─▶ Moderator handles
        │
        └─▶ Comments   ─▶ AI filter ─▶ Admin review ─▶ shown in the feed
```

Real-time updates — the question stream, the comment feed, the moderation queue
— run over SignalR (SIMF-SAD-001 section 6.4).

## 5. Detailed behaviour

### 5.1 Live broadcast

- A session marked **live** (SIMF-FDS-004) can be broadcast. A user holding the
  Live Sessions page with the Manage broadcast action — the Scientific team in
  the suggested configuration (decision D11) — starts and stops the broadcast.
- The `LiveSessionState` records whether the session is broadcasting, when it
  started, and the caption language.
- The attendee watches the stream on the live screen (mockup Screen 25): the
  video, a red live indicator, and the live session's title and speakers.
- **AI translation / captions.** A caption track of the spoken content is shown
  over the video, with a **language picker** so the attendee chooses the caption
  language. The translation is produced through the cognitive-AI abstraction
  (SIMF-SAD-001 section 9.2); the provider is deferred (decision D7).
- **Geographic restriction.** The live stream is available only within the
  Riyadh region (FR-702). An attendee outside the region sees the restriction
  notice instead of the stream.
- The video stream itself is embedded from the live-broadcast platform; the
  platform is deferred and reached through an abstraction (decision D7).

### 5.2 Session questions

- An attendee can put a **question** to the moderator during a session
  (`UC-14`). The attendee chooses the recipient — the **speaker** or the
  **host** — and writes the question.
- **When questions are open.** A session's questions open for an attendee only
  **after that attendee is verified as arrived at the hall** — they have a
  `HallAttendance` enter record for the session (SIMF-FDS-003) — and they
  **close at session end** (decision D5). Until the attendee has arrived, the
  question composer is not offered.
- A submitted question is recorded as `Pending` and the attendee is told it
  will be reviewed before it goes on air.
- A question moves through the states: `Pending` → `OnAir`, `Answered`, or
  `Hidden`, set by the moderator (section 5.3).

### 5.3 The moderator and the question queue

- A **Moderator** is a mobile-app role assigned to specific sessions (decision
  D3, SIMF-RPM-001). A moderator handles questions only for their assigned
  sessions (`UC-36`).
- For an assigned session the moderator can: view the incoming questions,
  **order** them, **hide** an unsuitable one, put a question **on air** to the
  speaker, and mark a question **answered**.
- The question stream updates live for the moderator over SignalR.

### 5.4 Comments and two-stage moderation

- An attendee can post a **comment** on a session (`UC-15`).
- Every comment passes **two gates**, in order (decision D5):
  1. **AI filter.** The comment is checked by the cognitive-AI filter; the
     result — `Passed` or `Flagged` — is recorded with the comment. A flagged
     comment is **not discarded**; it still goes to the queue, marked flagged.
  2. **Admin review.** The comment waits in the **Comment Moderation queue** in
     the Control Panel. A user holding the Comment Moderation page with the
     Moderate action — the Scientific team, the single owner (decision D11) —
     approves or discards it.
- The AI never approves a comment on its own; every comment reaches an admin.
- An **approved** comment appears in the session's comment feed; a **discarded**
  comment does not. The moderation queue and the comment feed update live over
  SignalR (`UC-26`, SIMF-CPD-001 section 13.5).

## 6. Data

The feature uses these entities from SIMF-DAT-001 section 5.6: `LiveSessionState`,
`SessionQuestion`, `Comment`. It reads `Session`, `User` and `HallAttendance`.
A `Comment` always carries both an `AiResult` and an `AdminDecision`.

## 7. User interface

| Surface | Screens |
|---------|---------|
| Mobile app | Screen 25 the live broadcast; the question composer and the comment feed, integrated into the live/session experience |
| Mobile app (Moderator) | The moderator's question queue for an assigned session |
| Control Panel | Live Sessions (start/stop the broadcast); the Comment Moderation queue, per SIMF-CPD-001 section 13.5 |

Mobile visuals are the external designer's; Control Panel screens follow
SIMF-CPD-001. Captions and all text are localised; loading, empty and error
states are present; the live screens reflect the connection state rather than
showing stale data.

## 8. Validation rules

| Item | Rule |
|------|------|
| Start broadcast | The session is marked live; the user holds Manage broadcast |
| Question recipient | Required; speaker or host |
| Question text | Required; non-empty |
| Question availability | The attendee has a hall-arrival record for the session; the session has not ended |
| Comment text | Required; non-empty |
| Comment moderation | A comment is shown only after an admin approves it |

## 9. Security and privacy considerations

- The live stream enforces the Riyadh-region restriction server-side; the
  client does not decide eligibility.
- Question and comment submission is tied to the signed-in, Approved attendee.
- A question is gated on a real hall-arrival record; the client cannot bypass
  the gate.
- Comment text is user content in one language; it is stored with its language
  and screened by the AI filter before any display.
- Moderator and admin actions on questions and comments — hide, put on air,
  approve, discard — are written to the operation log.
- The cognitive-AI filter and translation run through an abstraction; the
  provider is given only the text it needs (decision D7, SIMF-SAD-001 section
  9.2).

## 10. Acceptance criteria

1. A live session can be started and stopped by a user with the Manage
   broadcast permission; the attendee sees the stream.
2. Captions are shown with a language picker; the attendee can change the
   caption language.
3. An attendee outside the Riyadh region sees the restriction notice, not the
   stream.
4. The question composer is offered only after the attendee has a hall-arrival
   record for the session, and not after the session ends.
5. A submitted question is `Pending` and the attendee is told it will be
   reviewed.
6. A moderator sees, orders, hides, puts on air and answers questions for their
   assigned sessions only, with live updates.
7. Every comment is screened by the AI filter, then waits in the moderation
   queue; a flagged comment is queued, not discarded.
8. An admin approves or discards a queued comment; the AI never approves on its
   own; an approved comment appears in the feed.
9. The moderation queue and the comment feed update live.
10. All screens render in Arabic (RTL) and English (LTR); no hardcoded text.
11. The build is clean and the feature has unit, integration and end-to-end
    tests that pass.

## 11. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Start a live broadcast | `LiveSessionState` broadcasting; attendees see the stream |
| T-02 | Change the caption language | captions switch language |
| T-03 | Open the live stream from outside the Riyadh region | restriction notice shown; no stream |
| T-04 | Ask a question with a hall-arrival record | question recorded `Pending` |
| T-05 | Try to ask a question without a hall-arrival record | the composer is not offered |
| T-06 | Try to ask a question after the session ended | questions closed |
| T-07 | Moderator orders, hides and puts a question on air | states update; only assigned sessions |
| T-08 | Moderator opens a session not assigned to them | no access to its question queue |
| T-09 | Post a clean comment | AI result `Passed`; queued for admin |
| T-10 | Post a comment the AI flags | AI result `Flagged`; queued, not discarded |
| T-11 | Admin approves a queued comment | comment appears in the feed |
| T-12 | Admin discards a queued comment | comment does not appear; action logged |
| T-13 | Two attendees comment while a moderator watches | the queue updates live for the moderator |
| T-14 | Render the engagement screens in Arabic and English | correct layout and direction; no hardcoded text |

## 12. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm the live-broadcast platform and the AI translation provider as decision D7 closes | Section 5.1 |
| OI-2 | Confirm how the Riyadh-region restriction is determined — device location, IP region, or both | Section 5.1 |
| OI-3 | Confirm the AI comment-filter rules — what it screens for — with the client | Section 5.4 |
| OI-4 | Confirm whether a moderator may also act from the Control Panel, or only the app | Section 5.3 |
| OI-5 | Confirm document classification with the owner | Control block |

---

## Amendment A — Architecture review (2026-05-21)

The scalability review of 2026-05-21 amends this feature.

**SignalR at scale.** The live-session hub sends to **groups**, and the comment
and question feed is **batched** — updates coalesced every 1–2 seconds rather
than pushed per message — to bound the fan-out cost when a session has tens of
thousands of viewers. A per-session concurrent-connection target is set and
load-tested (SIMF-OPS-001 Amendment A.1). For a multi-node production
deployment a SignalR **backplane** is required — the deferred scale-out
decision in SIMF-SAD-001 Amendment A.3. The video stream stays embedded from
the external platform, so SignalR carries only question / comment / state
messages.

**Graceful degradation.** If the AI comment filter is unavailable, comments
still queue for admin review — the AI never auto-approves, so admin moderation
is unaffected.

---

## Amendment B — the two ask-a-speaker modes & the 3-stage Q&A pipeline (D0-2, BUILT 2026-07-09 — D-714)

> **AS-BUILT (2026-07-09, D-714):** both approved deltas shipped. **GAP-2** — the
> session-detail ask card now reads a distinct pre-session label ("اطرح سؤالاً قبل
> الجلسة") while the session is upcoming and "اسأل المحاور" once live, so mode-B is
> visibly separate; the backend still derives the phase + window. **GAP-1** — the
> real `AiQuestionFilter` (central `IAiService` + the seeded `question-filter`
> prompt, JSON `{allowed,reason}` → advisory `ai-clean`/`ai-flagged`, safe
> `ai-unavailable` fallback) is wired behind `SessionQuestions:AiFilterEnabled`
> (**default = stub** so the PoC needs no key; flip on when a key is provisioned).
> **GAP-3** — reproduced as the by-design arrival gate + [start−5min, end] window
> (D-242/D-271), no fix. See DECISIONS_LOG D-714.
>
> This amendment folds the already-built Q&A pipeline
> (completion-programme D-212/D-233/D-236/D-271/D-519) into this feature spec and
> defines the **owner's item 12** ("ask a speaker — two ways") against it.
> **Finding: item 12 was already built end-to-end;** the deltas were small (§B.4).
>
> **Owner resolutions (2026-07-08):** **wire the real AI** for question filtering
> (OI-B1 — built config-gated, default stub); **add a distinct pre-session ask
> entry** (OI-B2 — built). GAP-3: reproduced, by design (no fix).

### B.1 What the owner asked (item 12, 2026-07-08, verbatim intent)

> *"Ask a speaker, two ways: (A) **live inside the session hall** — the home menu
> must be filtered by moderator only; and (B) a **pre-question before the session
> started** — filtered by AI and team and moderator."*

### B.2 As-is — the built 3-stage pipeline (grounded; do not rebuild)

The Q&A is a **3-stage pipeline for BOTH phases** — the owner's "AI + team +
moderator" is exactly the built `AI (advisory) → Scientific Committee → per-session
Moderator desk` (D-212):

| Piece | Where (file:line) | State |
|-------|-------------------|-------|
| **Phase (Pre vs Live)** — the "two modes" | `QuestionPhase{Pre=0,Live=1}` (`QuestionPhase.cs`); `SessionQuestion.Phase` (`SessionQuestion.cs:69`) **set by the backend at submit from the session's start** — one app screen, not two | **Built** (D-233). |
| **Stage 1 — AI (advisory)** | `IQuestionAiFilter`/`StubQuestionAiFilter` → `SessionQuestion.AiFilterVerdict` (`SessionQuestionService.cs:152`); advisory only, never auto-hides | **Built but a STUB** (D-236/D-239) — returns `stub-clean`; a real model is a **DI/config swap**, no code (GAP-1). |
| **Stage 2 — Scientific Committee ("team")** | `QuestionStatus{Pending,Approved,Hidden}` (`QuestionStatus.cs`); CP `/admin/questions/queue` + approve/hide/**escalate-to-role** (`SessionQuestionCommitteeEndpoints.cs:20/36/61/86`); CP page `QuestionQueueList.razor` | **Built** (D-212). The "team" = the الفريق العلمي **role** (a permission bundle, D-207/D-208), not new infra. |
| **Stage 3 — per-session Moderator desk** | app desk `GET /app/sessions/{id}/questions/moderate` + hide/push/reorder (`SessionQuestionEndpoints.cs:100/132/164/196`), gated by `SessionModeratorAuth` = Administrator **or** a `SessionModerator` grant | **Built** (D-169). Distinct from `MobileAppRole.Moderator` (per `SessionQuestion.cs:19-23`). |
| **Moderator-only home** (mode A entry) | `home_screen.dart:66` → `AppRole.moderator` gets `ModeratorHome` (`operational_homes.dart:52-70`, → sessions list → detail → Q&A desk); route #104 = `{moderator}` **exclusive** | **Built** (D-519) — the "home menu filtered by moderator only" is already satisfied. |
| **Attendee ask screen** | `send_question_screen.dart` / `send_question_content.dart`; the session-detail `ask_host_card.dart` (Speaker vs Host recipient, `SessionQuestionRecipient`) | **Built.** Serves both phases (the phase is backend-derived). |
| **Question window + arrival gate** | open **5 min before** `StartUtc`, **close at** `EndUtc` (D-271); submission gated on hall-arrival (`HallAttendance`, D-242 geofence + `IsAtVenue` fallback) | **Built.** |
| **Page docs** | `Page_026` (questions), `Page_025` (live) authored (D-271) | **Built.** |

### B.3 Mapping the owner's two modes onto the built pipeline

| Owner's mode | Built reality |
|--------------|---------------|
| **(A) live in-hall, moderator-only home** | The attendee asks from the live/session screen (Phase=`Live`, arrival-gated); the **moderator** runs the desk from the moderator-only home (`ModeratorHome`). Already built. |
| **(B) pre-question, AI+team+moderator** | The **same** ask screen submitted **before** `StartUtc` → Phase=`Pre`; it flows through the identical 3-stage pipeline (AI→Committee→Moderator). Already built — the phase is derived, so there is no separate "pre-question" screen today. |

### B.4 Deltas — ✅ BUILT (D-714)

- **GAP-1 (AI wiring) ✅.** `AiQuestionFilter` routes stage 1 through the central
  `IAiService` + the seeded `question-filter` prompt (JSON `{allowed,reason}` →
  advisory `ai-clean`/`ai-flagged`, `ai-unavailable` fallback). Config-gated by
  `SessionQuestions:AiFilterEnabled` — **default = stub** (the PoC needs no key);
  flip on when a key is provisioned. Advisory only (never changes Status). Verified
  offline via a fake `IAiService` (`QuestionAiFilterTests`).
- **GAP-2 (Mode-B reachability) ✅.** The session-detail ask card now reads the
  distinct pre-session label ("اطرح سؤالاً قبل الجلسة") while the session is upcoming
  (`now < startUtc`) and "اسأل المحاور" once live, so the two modes are visibly
  separate; the phase + window stay backend-derived (D-271). App-only.
- **GAP-3 (verify not-broken).** The owner reported "ask speaker not working." Every
  layer above is built, so the likely causes are an **older build** (cf. D-702 item
  10), the **arrival gate** (no `HallAttendance` ⇒ composer hidden — by design), or
  the **window** (opens 5 min before start / closes at end). **Action:** reproduce on
  the current build before any change — do not "fix" a working gate.

### B.5 Open items

**Resolved (owner, 2026-07-08):**

| # | Item | Resolution |
|---|------|-----------|
| **OI-B1** | Real AI vs stub for question filtering | **Wire the real `IAiService`-backed filter now** (mirror the D-578 summary), behind the same seam. **Blocker:** a real AI key must be provisioned/rotated first (the standing security item) — the DI swap lands once the key is available; until then the stub stands so nothing is blocked. |
| **OI-B2** | Distinct pre-session ask entry vs single flow | **Add a clear pre-session "ask a question" entry** on an upcoming (not-yet-live) session's detail, so the two modes are visibly separate; it flows through the same AI→Committee→Moderator pipeline (Phase=`Pre`). |

**Proceeding on the documented recommendation:**

| # | Item | Default taken |
|---|------|---------------|
| **OI-B3** | "team" = the الفريق العلمي Scientific-Committee role + CP `/admin/questions/queue` | Confirmed as built stage 2. |
| **OI-B4** | Arrival-gate + 5-min/close-at-end window are intended (not the "not working" bug) | Keep as built (D-271); **reproduce the "not working" report on the current build first** — do not "fix" a working gate. |

### B.6 Definition of Done (only if a delta is approved — same changeset)

Whichever deltas the owner approves: DI swap + a real-provider test for GAP-1;
the pre-session ask entry + widget test + `Page_026`/E2E update for GAP-2; a
reproduce-then-fix note for GAP-3. No schema change (the pipeline data model is
already shipped, D-233); no new enum. `DECISIONS_LOG.md` entry; this Amendment B
flipped from DRAFT to built with an As-built note.

---

## Amendment C — the multi-trigger ratings (D0-3, BUILT 2026-07-09 — D-712/D-713)

> **AS-BUILT (2026-07-09):** both gaps shipped. **GAP-B** rate-on-live-close =
> **D-712** (app `live_broadcast_screen` dispose → `/rate?code=Session`, eligible
> attendee + live feed, shared dedup). **GAP-A** rate-on-**hall-departure** +
> the "watched at" header = **D-713**. **Correction to §C.2/§C.4:** the DRAFT
> premise that a **gate Out-scan closes a `SessionAttendance`** is **false** in
> the built code — a `Gate` is **venue-level and sessionless** (`GateOperatorService`
> writes only a `GateScan`), and `SessionAttendance` is a read-only CP aggregate
> over `HallAttendance`, not an entity. The real per-session "leave" signal is
> `HallAttendanceService.RecordDepartureAsync` (closes `HallAttendance.LeaveUtc`
> for a known (session,user)). Owner (2026-07-09) chose the **hall-departure
> hook**: departure now fires a `SessionRatingRequest` for that exact session,
> deduped one-per-(user,session) via `NotificationRequest.DeduplicateByRelatedEntity`
> shared with the clock-end worker. OI-C1 therefore **dissolves** — the departure
> already knows the exact session, so no "which of several sessions in the hall"
> mapping is needed. The header sources the session's own title + date (appended
> to `RatingFormView`), the OI-C3 per-rating v1. See DECISIONS_LOG D-712/D-713.

> **STATUS (original DRAFT note): build-ready — owner said "build item 8"
> (2026-07-09); the §C.5 open items proceed on the documented recommendations.**
> Ratings/feedback are not
> currently owned by a dedicated FDS — they were built across the completion-
> programme decisions (D-677/D-678 notification+deep-link, D-679 day/programme
> prompts, D-680 dynamic page, D-690 rate-after-view). Per the owner's Phase-0 plan
> this section is the ratings home. It defines the **owner's item 8** ("show rate
> when a session is watched, at time and each date — many rate triggers based on
> time") against the built system. **Finding: 2 of the 4 owner triggers are already
> built; 2 are gaps (GAP-A rate-on-gate-checkout, GAP-B rate-on-live-close) + a
> "watched at" header.** Cross-references SIMF-FDS-003 (gate scan / attendance) and
> SIMF-FDS-011 (statistics consuming the ratings).
>
> **OI resolutions (recommendation, owner did not override):** OI-C1 = the session
> active at the scan time (else the most recent attended in that hall today);
> OI-C2 = reuse the `Session` rating code (one prompt per session per user, dedup
> shared with GAP-A/B + D-690); OI-C3 = a per-rating "watched at" header; OI-C4 =
> keep the built triggers, add only GAP-A/B.

### C.1 What the owner asked (item 8, 2026-07-08, verbatim intent)

> *"Show rate when a session is watched, at the time and each date. We have many
> rate triggers based on time: (1) **daily** — if you checked in, at the end of the
> date; (2) **end of exhibition**; (3) **end of session on checkout from the gate**;
> (4) **online session** — on the live-YouTube stream page, after back / close / end,
> show the rate for the online session."*

### C.2 As-is — the built ratings system (grounded)

| Piece | Where (file:line) | State |
|-------|-------------------|-------|
| **Rating scopes** | `RatingScope{Global=0, PerSession=1, PerDay=2}` (`RatingScope.cs`) | **Built** (D-679). |
| **Seeded rating types** | `App`, `Session` (PerSession), `Day` (PerDay), `Event` + `Exhibition` (Global) — `RatingSeeder`; resolved by `RatingFormService.ResolveTargetAsync` | **Built** (D-679). |
| **Dynamic rating page** | app `/rate?code={code}&targetId={id}` — code-agnostic; proven for Event/Exhibition/Day | **Built** (D-680). |
| **Notification + deep-link** | kinds `DayRatingRequest`/`EventRatingRequest`/`AppRatingRequest`/`ExhibitionRatingRequest` (46-49) + `SessionRatingRequest`; `clickUrl` → `/rate?code=…` | **Built** (D-677/D-678). |
| **Trigger — end-of-day (per checked-in attendee)** | `ProgrammeRatingPromptWorker` end-of-day scan → `DayRatingRequest` to everyone with a Check-In gate scan that event-local day; per-day dedup (`ProgrammeDay.RatingPromptSentUtc`) | **Built** (D-679). |
| **Trigger — end-of-programme (Event+Exhibition+App)** | `ProgrammeRatingPromptWorker` end-of-programme trio to every ever-checked-in attendee; once-only marker (`SystemSettings` `ProgramEndRatingSentUtc`) | **Built** (D-679). |
| **Trigger — end-of-session (clock)** | `SessionRatingPromptWorker` — sessions whose `EndUtc` is within a 6h back-fill → `SessionRatingRequest` to every attendee with an active seat; dedup `Session.RatingPromptSentUtc` | **Built.** |
| **Trigger — session view-leave (app)** | `SessionRatePromptTracker` — `session_detail_screen` fires `/rate?code=Session&targetId={id}` once per session on a real leave, only for an approved attendee of an **ended** session | **Built** (D-690). |
| **Venue gate scan (In/Out)** | `GateScan.Direction` (`ScanDirection`), `Gate.DirectionMode{In,Out,Both}` — a `Gate` is a **venue-level** access point with **no hall/session link** (`GateOperatorService` writes only a `GateScan`). | **Built** — but sessionless, so it is **not** the rate-on-checkout hook (corrected from the DRAFT). |
| **Hall/session attendance close** | `HallAttendanceService.RecordDepartureAsync` sets `HallAttendance.LeaveUtc` for a known (session,user), via `POST /app/sessions/{id}/departure`; `SessionAttendance` is the read-only CP aggregate over these rows. | **Built** — this is the real per-session "leave" signal → **GAP-A** now hooks it (D-713). |
| **Live / YouTube screen** | `live_broadcast_screen.dart` (`youtube_player_iframe`, D-349) | **Built + rate trigger on leave (D-712 GAP-B).** |

### C.3 The owner's 4 triggers → built reality

| # | Owner trigger | Built? |
|---|---------------|--------|
| 1 | **Daily** at end-of-date **if checked in** → rate the day | ✅ **Built** — `ProgrammeRatingPromptWorker` end-of-day (D-679). |
| 2 | **End of exhibition** → rate event/exhibition | ✅ **Built** — end-of-programme trio (D-679). |
| 3 | **End of session on checkout** → rate the session | ✅ **Built (D-713 GAP-A)** — leaving the hall (`RecordDepartureAsync`) now fires the session rating, alongside the built **clock-end** (`SessionRatingPromptWorker`) + **app-view-leave** (D-690). The literal *gate* Out-scan is venue-level/sessionless, so the hall-departure close is the correct hook. |
| 4 | **Online session, live-stream close** → rate the online session | ✅ **Built (D-712 GAP-B)** — the live screen fires the rating on leave. |

### C.4 Deltas (the new work) — ✅ ALL BUILT

- **GAP-A — rate-on-hall-departure ✅ (D-713).** When an attendee's departure
  closes their `HallAttendance` (`RecordDepartureAsync` sets `LeaveUtc`),
  `HallAttendanceService` fires the **session rating** for that exact (session,user)
  — an in-app `SessionRatingRequest` deep-linking to `/rate?code=Session&targetId={id}`.
  **OI-C1 dissolved:** the departure already carries the exact `sessionId`, so no
  "which of several sessions in the hall" mapping is needed. Deduped
  one-per-(user,session) via the new opt-in `NotificationRequest.DeduplicateByRelatedEntity`
  (dispatcher skips the write when a same-(user,kind,entity) notification exists —
  `INotificationRepository.ExistsForUserAsync`, a single-context Identity-DB query,
  D-157 clean); the clock-end `SessionRatingPromptWorker` sets the same flag, so an
  early-leave + a later clock-end scan can't double-prompt. *(The literal "gate
  Out-scan" hook the DRAFT assumed does not exist — a gate is venue-level/sessionless.)*
- **GAP-B — rate-on-live-close ✅ (D-712).** On the live/YouTube screen **leave**
  (`dispose`), the rating fires once via the D-690 pattern for `live_broadcast_screen.dart`,
  eligibility-gated (signed-in approved attendee **and** the session carried a live
  feed). **OI-C2:** reuses the **`Session` code** (one rating per session regardless
  of channel); dedup shared with GAP-A + D-690 so watching online then walking out
  isn't prompted twice.
- **"Watched at" header ✅ (D-713).** The rating screen shows a per-session context
  chip — "شاهدت «{session}» · {date}" (`rate_screen._WatchedHeader` + `rateWatchedAt`).
  **OI-C3:** the per-rating header (not a watch-history list). Sourced from 3
  **appended** `RatingFormView` fields (`TargetName`/`TargetNameArabic`/`TargetStartUtc`,
  the session's own title + start), so no per-user watch timestamp is plumbed.

### C.5 Open items — OWNER DECISIONS

| # | Item | Recommendation |
|---|------|----------------|
| **OI-C1** | Gate Out-scan → which session to rate when a hall hosts several? | The session active at the scan time (else the most recent attended in that hall today). |
| **OI-C2** | Online-session rating — reuse the `Session` code or a distinct online code? | Reuse `Session`; share dedup with GAP-A + D-690 (one prompt per session per user). |
| **OI-C3** | "Watched at time/date" — per-rating header, or a watch-history list? | Per-rating header for v1. |
| **OI-C4** | Confirm the built end-of-day + end-of-programme + clock-end + view-leave triggers stay as-is (only add GAP-A/B). | Keep the built triggers; add only the two gaps. |

### C.6 Definition of Done (only if a delta is approved — same changeset)

For the approved gaps: the gate-checkout rating hook in `GateOperatorService` /
attendance close (GAP-A) with per-(session,user) dedup + integration tests; the
live-screen leave-fires-`/rate` hook (GAP-B) + a widget test (fires once, eligible
only); the watch-context header + a test. Reuse the built `SessionRatingRequest`
kind + `/rate` route (no new enum unless OI-C2 chooses a distinct code); no schema
change beyond a dedup guard if needed. E2E + `Page` doc updates; `DECISIONS_LOG.md`
entry; this Amendment C flipped from DRAFT to built with an As-built note.

---

End of document.
