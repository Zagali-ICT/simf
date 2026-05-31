# SIMF — Completion Programme Plan (remaining scope, phased)

**Status:** Draft for owner approval — 2026-05-31
**Authored after:** D-211 (freeze-lift + 3 deferrals) and the 2026-05-31 owner
clarification of the Sessions / Q&A / AI-summary design.
**Purpose:** ONE consolidated plan for *all* remaining scope, organised into
phases that can be delivered and signed off one at a time. This is the tracker;
it does not restate the controlled FDS docs, it references them.

---

## 0. Governing principles (apply to every phase)

- **Roles-only permissions (D-207/D-208).** "Scientific Committee" (الفريق
  العلمي) is a **role = a bundle of permission codes**, NOT new infrastructure
  and NOT the super-admin. Every programme/session/Q&A-filter action is a
  permission code that the owner assigns to that role. `Administrator = "*"`
  stays the wildcard; the Committee role holds only what it is granted.
- **HARD RULE.** A new CP page or admin action is not "done" until its
  permission code exists, is seeded, and gates **both** API and CP.
- **Freeze.** D-110 baseline; D-211 lifted a *named* set of additive tables.
  Anything beyond that named set needs explicit owner approval before it lands.
- **Definition of done per phase.** Release build 0/0, all suites green,
  `simplify` pass applied, decisions-log entry, commit (push when asked).
- **Deferred-blocked items stay deferred** until their blocker clears
  (G-OI-2, D7, D6) — see §6.

---

## 1. Phase map (delivery order)

| Phase | Theme | Schema? | Gate |
|-------|-------|---------|------|
| **P1** | Quick wins — no schema | no | none (start now) |
| **P2** | New-table feature modules | yes (D-211) | freeze-lift already granted |
| **P3** | Sessions & Programme completion + Q&A pipeline + broadcast | yes | needs §2 broadcast confirm + SessionQuestion columns (D-212) |
| **P4** | AI features — session summary (محضر) + advisory filters | yes | needs AI-provider availability confirm |
| **P5** | Deferred / blocked register (not built this programme) | — | owner / external |

Phases run in order; within a phase, items are independent and each ends in its
own commit.

---

## 2. Session broadcast — recommendation (answer to "live or record, and how?")

**Question put to me:** should sessions be **live** or **recorded**, and how do
we do the broadcast?

### Options

- **A — Recorded → published (VOD).** The session is recorded (venue AV /
  external), the file is uploaded/linked in the CP by the Scientific Committee,
  and **published after the session ends**. The app plays the recording. AI
  translation, sign-language, and the محضر summary are generated **from the
  recording** (offline) and reviewed before publish.
- **B — True live streaming.** Real-time RTMP/HLS via an external provider, live
  captions/translation, realtime geofence + realtime Q&A moderation.
- **C — Hybrid.** In-venue is live; the remote/app experience is the recording,
  published shortly after.

### Recommendation: **Option A (recorded → published), with a live-ready seam**

| Why A fits SIMF | |
|---|---|
| **Deadline** | No realtime streaming infra, no external provider procurement on the critical path. |
| **NCA / security** | Everything is **vetted before it goes public** — the right posture for a high-profile MoD/RSNF event. Live = unvetted content on air. |
| **On-prem** | VOD playback behind the reverse proxy is simple; realtime streaming is not. |
| **AI quality** | Translation / sign-language / محضر generated from the finished recording are higher quality and reviewable, vs. error-prone realtime. |
| **Already deferred** | The real live-video provider (D7) and the realtime geofence (G-OI-2) are already deferred — Option A needs neither. |

**How the broadcast works under Option A:**

1. **Session lifecycle** gains a status: `Scheduled → Held → Recorded → Published`
   (+ `PublishedAt`). The Committee role drives the transitions in the CP.
2. **Media asset** — each session can carry a recording reference (uploaded file
   or linked URL) + poster, set in the CP.
3. **On publish**, AI (Phase 4) drafts the محضر summary + translation/captions
   from the recording transcript; the Committee reviews/edits, then publishes.
4. **App** session detail shows the recording player + the AI summary + the
   recorded, filtered Q&A (attributed to askers).
5. **Live-ready seam** — the existing Live Sessions stub stays in place. If a
   *new live requirement* appears, live streaming plugs into the same session
   entity (status `Live`) behind the deferred D7 provider, without redesign.

> The mockup's "● LIVE" screen + realtime AI translation (screens 25–26) are
> re-cast as **recorded-playback** features under Option A. If you want true
> live, pick Option B and we add D7 (provider) + G-OI-2 (geofence) back to scope.

**This is a recommendation — §2 needs your confirm before P3 builds.**

---

## 3. Phase 1 — Quick wins (no schema, start immediately)

| # | Item | Permissions | Tests | Notes |
|---|------|-------------|-------|-------|
| 1.1 | Tidy the 2 superseded nav stubs (registration-requests → pending queues; exhibitors → Companies) | n/a | nav test | relabel/point only; no deletion of real pages |
| 1.2 | **Permission grant editor + assign-to-user/role UI** (FR-1201/1202) | reuse `Roles.*` | api+cp | **enables the Scientific-Committee role config** |
| 1.3 | Bulk admin/staff pending-queue parity + per-user Edit (B1, FR-212) | existing | api | symmetric to D-210 |
| 1.4 | Seat-grid visual renderer (FR-405/505) | existing | cp | backend already done |
| 1.5 | CMS markdown rendering + brand-colour tokens (FR-1203) | existing | web | rendering only |
| 1.6 | XLSX export + date filters (operation-log, attendees) (FR-1205) | existing | api | export logic |
| 1.7 | Notification trigger events (session-reminder, booking-confirmed) (FR-902/903) | existing | api | additive enum values only (freeze-safe) |

---

## 4. Phase 2 — New-table feature modules (freeze-lift granted, D-211)

| # | Item | New tables | Permissions | Notes |
|---|------|-----------|-------------|-------|
| 2.1 | **FAQ management** (the `/m/faq` stub) | `FaqGroup`, `FaqEntry` | `Faq.*` (new) | two-level group→entry |
| 2.2 | **Booking approval workflow** (the `/m/bookings` stub) | booking state/approval | `Bookings.*` (new) | queue + confirm/cancel |
| 2.3 | **Speaker presentation-file** upload (FR-407) | file/asset | reuse `Speakers.*` | |
| 2.4 | **System Configuration** page (collapses `/m/configuration` + `/m/settings`) | config table | `Configuration.*` (new) | registration toggles already exist (`/admin/operations`) |
| 2.5 | **Venue Map 2D editor** (the `/m/venue-map` stub) | `VenueMapNode` | `VenueMap.*` (new) | 2D (per D-199) |
| 2.6 | **Networking "Connect"** enable | connection/request | `Networking.*` (new) | un-disable the app button |

Each lands as one additive migration on `SimfAppDbContext`; Identity schema +
existing enum names/values stay frozen.

---

## 5. Phase 3 — Sessions & Programme + Q&A pipeline + broadcast

Depends on §2 (broadcast confirm) and adds the `SessionQuestion` columns
approved on 2026-05-31 (recorded as **D-212**).

### 5.1 Scientific Committee role + Q&A permissions
- New permission codes `Questions.View / Moderate / Escalate` seeded
  (idempotent, no migration — the `Permission` tables pre-exist).
- No new role infra — the owner creates a **"Scientific Committee"** role via
  the Phase-1 grant editor and assigns it the programme + Q&A codes
  (`Sessions.*`, `Themes.*`, `Halls.*`, `Speakers.*`, `ProgrammeTimeline.*`,
  `Questions.*`, …).

### 5.2 Session lifecycle + recorded-broadcast (Option A)
- Add `Status` (`Scheduled/Held/Recorded/Published`) + `PublishedAt` + a
  recording/media reference to `Session` (additive migration).
- CP: the Committee sets the recording + publishes; app shows recording on the
  published session.

### 5.3 Q&A — pre-questions + live-questions, 3-stage pipeline
- Add to `SessionQuestion`: `Phase` (`Pre`/`Live`), `Status`
  (`Pending/Approved/Hidden`), `AiFilterVerdict`, `AssignedToRole?`,
  `EscalatedByUserId?`, `EscalatedAt?` (additive migration; new enums).
- **Pipeline (all questions, pre and live):**
  1. **AI — advisory** (`IQuestionAiFilter`, Phase 4 wiring): tags a verdict,
     **never auto-hides**; admin sees everything in the CP.
  2. **Scientific Committee — central queue**: the role filters
     (approve / hide / escalate-to-role). This is the new central admin queue.
  3. **Moderator — final**: the existing per-session moderator desk
     (`/sessions/{id}/moderate`) shows the **approved** set and does the live
     push/reorder. **Kept**, now fed by the Committee-approved queue.
- **Routing "to admin by team"** = escalate `AssignedToRole` to a role (team),
  not an individual.

### 5.4 Recorded Q&A archive
- Answered/approved questions are shown **with the published session**,
  attributed to the asker (the data is already attributed via
  `SubmittedByUserId`; this adds the read view).

### 5.5 Request Interview (طلب مقابلة, mockup screen 27) — **scope to confirm**
- There is a `MeetingRequest` module, but it appears to be the **delegation**
  meeting-request (D-183), not the screen-27 "interview with the speaker".
  **Confirm:** reuse/extend `MeetingRequest`, build a new session
  interview-request, or defer.

---

## 6. Phase 4 — AI features (session summary محضر + advisory filters)

| # | Item | New tables | Notes |
|---|------|-----------|-------|
| 4.1 | **AI session summary / محضر** (mockup screen 34): key points, recommendations, speakers, full text | `SessionSummary` | generated from the recording; Committee reviews/edits before publish; app screen 34 reads it |
| 4.2 | **Wire `IQuestionAiFilter`** (advisory) into question submit | — | mirrors the built `ICommentAiFilter` seam |
| 4.3 | (Comments AI filter already stubbed — confirm provider) | — | `StubCommentAiFilter` ships today |

**AI provider note (needs confirm):** the AI module has a provider seam
(`AiPrompt.Provider`), but the comment filter is a **stub** today. The محضر +
question filter ship **behind the seam with a stub** (manual summary entry /
pass-through verdict) and the **real AI provider plugs in when keys are
procured** — same deferral shape as the live provider (D7). **Confirm whether a
real AI provider + keys are available now, or we ship the seam + stub.**

---

## 7. Phase 5 — Deferred / blocked register (NOT built this programme)

| Item | Blocker |
|------|---------|
| GPS geofence → arrival → session-attendance → movement/dwell (FR-305/506/1103) + question-gating-on-arrival (FR-704) | **G-OI-2** venue-boundary decision |
| Real live-video provider (true live, Option B) | **D7** external procurement — only if owner picks live |
| Exact statistics metric list (FR-1101–1104) | **D6** metric list |
| Device-calendar add (FR-409) | Flutter app — **mobile workstream**, not CP/backend |

---

## 8. Open confirmations before P3/P4 build

1. **Broadcast model** — confirm **Option A (recorded → published)**, or pick B/C.
2. **Request Interview (screen 27)** — reuse `MeetingRequest`, new feature, or defer.
3. **AI provider** — real provider + keys available now, or ship seam + stub.

P1 and P2 are unblocked and need none of the above — approval to start P1 is enough to begin.
