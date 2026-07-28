# SIMF — Session Module: Consolidated To-Do + Regression Plan

Last updated: 2026-07-08 · Branch: `refactor/clean-code-cp`

**Source:** owner batch (2026-07-08) — full session-module testing + fixes.
**Grounding:** four code/test maps (app session module, app meeting/rating/badge/agenda/gate,
CP+backend seat/meeting/hall/gate, E2E catalogue). Every "current state" line is anchored to
`file:line`. Every "existing tests" line names real scenario IDs under `docs/tests/e2e/`.

**Legend:** `BUILT` = works, verify only · `BUG` = exists but broken/mis-wired · `PARTIAL` =
partly there · `NEW` = not implemented · `DECISION` = needs owner call before build.

---

## Owner items → status at a glance

| # | Owner ask | Verdict | Owner decision (2026-07-08) |
|---|-----------|---------|--------------------|
| 1 | Sessions not showing / full session module test | **BUG** | **Keep presentations screen, fix why empty** (root cause: `/app/presentations` lists only uploaded files) |
| 2 | Join to session not working | PARTIAL — repro needed | (repro on device) |
| 3 | Scan session | DECISION — no per-session mobile scan screen | default → existing CP hall-arrivals console (confirm) |
| 4 | Select seat + approve from CP + random-not-exceed-max | **BUILT** (cap already enforced) | verify + surface toast + add E2E |
| 5 | Session summary + details + live + "session view in More/side menu" | BUILT; More/side entry removed (D-609) | default → keep summaries reachable, no new More entry (confirm) |
| 6 | Request bi-meeting + CP approve on available slot | **BUG** (app sends hard-coded times backend rejects) | **App shows the speaker's REAL available slots** |
| 7 | Manage hall g2g/g2b/g2vip + scan at gate | **FEATURE** — meeting-in-hall workflow | **See expanded item 7** (meeting types + CP hall-time mgmt + speaker email approve/reject) |
| 8 | Show session rating watched-at time + each date | **FEATURE** — multi-trigger rating | **See expanded item 8** (daily / end-of-event / gate-checkout / live-close triggers) |
| 9 | Open badge from notification on click | **BUILT** (BookingConfirmed/AccountApproved + clickUrl=/badge) | confirm which notification |
| 10 | Agenda design not as in Figma (+ **missing line between from/to time**) | **BUG** — connector collapses on short rows | (fetch node + fix + golden) |
| 12 | Ask-speaker **two modes**: live-in-hall (home menu moderator-only) + pre-question (AI+team+moderator filter) | **FEATURE** | **See item 12** — needs spec first |

**DOC-FIRST GATE (owner 2026-07-08):** the new rules (items 7, 8, 12) are **not in the controlled
docs** yet. They must be authored into the relevant FDS/SRS/UCS + page docs + E2E **before** any build.
Bug fixes (1, 2, 10) update their page doc + E2E in the same changeset. See "Phase 0 — Documentation".

---

## 1. Sessions not showing — routing + data  `BUG`
**Current state**
- Signed-in Visitor/Exhibitor Home "الجلسات" → `SessionPresentationsScreen`
  (`sessionPresentations`, Figma **1388-7621**, *materials/downloads*, empty
  "لا توجد عروض متاحة بعد") — `features/home/widgets/visitor_home.dart:119-130`.
- Guest Home "الجلسات" → `SessionsScreen` (agenda, **883-2308**) — `guest_home.dart:53-55`.
- Staff/Moderator Home "الجلسات" → agenda — `operational_homes.dart:69-77`.
- Agenda loads `GET /app/programme/days`; a session only appears if `IsActive` **and** assigned a
  Hall (memory: programme filters IsActive+Hall) — `sessions_screen.dart:65`,
  `sessions_repository.dart:29-34`.

**OWNER DECISION (2026-07-08):** keep the tile on the presentations screen; **fix why it is empty.**

**ROOT CAUSE (confirmed):** the presentations screen reads `GET /app/presentations`, which returns
**only sessions that have an uploaded presentation FILE** (`SpeakerPresentations`, D-228) —
`PublicPresentationEndpoints.cs:10-14` ("every active session-presentation file"). But the card's
تحميل button opens the **AI summary** (route 34), **not** the file (owner 2026-07-03,
`session_presentations_screen.dart:16-18`) — so the file is not needed for the card to work, yet the
list is gated on one existing. On prod no decks are uploaded → the screen is empty.

**FIX (recommended):** change `IPublicSpeakerPresentationService.ListAsync` / `/app/presentations` to
list **all active, hall-assigned sessions** grouped by day (title + speaker + start), independent of
whether a presentation file exists — matching what the card actually opens (detail + summary). The
`/{id}/file` download stays for sessions that DO have a deck. (Wire contract: `PublicPresentations`
item shape unchanged — additive only.)
**Alternative (data-only):** leave the endpoint file-gated and require CP to upload a presentation
per session — rejected: depends on manual uploads and the card doesn't use the file anyway.

**Existing tests:** `mobile-session-presentations.md` E2E-MOB202-001..007 (esp. -007 empty);
`mobile-agenda.md` E2E-MOB016-*; CP `cp-admin-sessions.md` E2E-SES-001..031 (all `_to author_`).
**Action:** ① widen `/app/presentations` to all active sessions · ② unit+integration test the new
projection · ③ update E2E-MOB202 empty→populated · ④ on-device verify on prod.

## 2. Join to session not working  `PARTIAL`
**Current state** — endpoints exist: `POST /app/sessions/{id}/seats/join` (open),
`/seats/reserve`, `/seats/reserve-random`, `DELETE /seats/mine`
(`seat_map_repository.dart:31-69`). Join CTA (`SessionJoinButton`) renders **only when
`seatMap != null && myCell == null`** — `session_detail_body.dart:125-127`; handler `_join()`
`session_detail_screen.dart:200-241`. So if the session has no hall/seat-layout, **no join button
shows** → looks "not working."
**Existing tests:** `mobile-session-detail.md` E2E-MOB017-022 (Join CTA), `mobile-join-hub.md`
E2E-MOBHUB-001..007.
**Action:** reproduce on device; determine whether the failure is (missing seat map / not-approved
account / endpoint error) and fix root cause; add the missing empty-seat-map behaviour + E2E.

## 3. Scan session  `DECISION`
**Current state** — no dedicated **mobile "scan into a session"** screen. Scanning exists at:
gates (`mobile-gate-scan.md`, `POST /app/gates/{id}/scans`) and CP hall-arrivals
(`cp-admin-hall-arrivals.md`, pick session → scan badge → record arrival, `E2E-HAR-*` all
`_to author_`).
**Decision needed:** does "scan session" mean a **new** staff app screen that scans attendees into a
specific session, or the **existing** CP hall-arrivals door console?
**Existing tests:** `cp-admin-hall-arrivals.md` E2E-HAR-001..014; `cp-admin-attendance.md`
E2E-ATND-* (was `E2E-ATT-*` until the 2026-07-28 renamespace off the attendees roster);
`mobile-gate-scan.md` E2E-MOBGATE-000..004.

## 4. Select seat + CP approval + random-not-exceed-max  `BUILT`
**Current state**
- Seat picker (manual grid + random button) — `seat_picker_screen.dart:184-212`.
- CP approval — `BookingsList.razor` + `POST /admin/bookings/{id}/approve|reject|bulk-approve`
  (perms `Bookings.Approve/Reject`).
- **Max already enforced:** `EnsureSessionHasCapacityAsync` = `min(layoutCap, declaredCap)`;
  `ReserveRandomAsync` throws `SeatSessionFull` when full — `SeatReservationService.cs:891-907,158,222-225`.
**Existing tests:** `mobile-seat-picker.md` E2E-MOBPICK-002/003/005; `mobile-my-seat.md`
E2E-MOB018-009/010; CP `cp-admin-bookings.md` E2E-BKG-001/002/003.
**Action (no new feature):** ① verify hall capacity + seat layout are configured on prod sessions
(a missing cap = "random proceeds past max" symptom) · ② surface `SeatSessionFull` as a clear
bilingual toast in the app · ③ add E2E asserting random respects the capacity ceiling (the current
gap).

## 5. Summary + details + live + "session view in More/side menu"  `BUILT` + `DECISION`
**Current state — all present & wired:**
- Summary list **1388-8392** — `session_summary_list_screen.dart` (`sessionSummaryList`).
- AI summary detail — `session_summary_screen.dart` (`aiSummary`, `GET .../{id}/summary`).
- Session detail — `session_detail_screen.dart`. Live — `live_broadcast_screen.dart` (`liveBroadcast`).
- **Removed under D-609:** My-sessions (#113), Saved-sessions, My-meetings — no More/side-menu
  session entry now (`more_screen.dart:99-103`, `more_menu_items.dart:37-80`). Only "عروض الجلسات"
  (presentations) remains reachable from More per E2E-MOB041-006.
**Decision needed:** restore a "session view" entry in More / side menu, or is the presentations
row the intended one?
**Existing tests:** `mobile-ai-summary.md` E2E-MOB034-*, `mobile-session-summaries.md`
E2E-MOB111-*, `mobile-session-detail.md` E2E-MOB017-*, `mobile-live.md` E2E-MOB025-*.
**Note:** the retired `mobile-my-sessions.md` / `mobile-saved-sessions.md` catalogues are historical
only — exclude from the regression pass.

## 6. Request bi-meeting + CP approve on available slot  `BUG`
**Current state**
- App sheet submits `POST /app/speakers/{id}/meeting-requests` with `slotStart/End`
  (`speakers_repository.dart:53-72`), but the day/time are **hard-coded client-side** — next 7 days
  + 9 hourly chips (`meeting_request_sheet.dart:67-96`); `getAvailableSlots`
  (`GET /app/speakers/{id}/available-slots`) **exists but is unused** (`speakers_repository.dart:39-47`).
- Backend **requires an exact free availability slot** on submit and re-checks on accept
  (`SpeakerMeetingRequestService.cs:124-131, 322-338`). → **A free-picked time that doesn't match a
  real `SpeakerAvailabilityWindow` is rejected.**
- CP respond queue works — `SpeakerMeetingRequestsList.razor` +
  `PUT /admin/speaker-meeting-requests/{id}/respond` (`SpeakerMeetingRequests.Manage`).
**OWNER DECISION (2026-07-08):** the app must fetch + show the speaker's **REAL available slots**
(`getAvailableSlots`) — the user picks from real free slots, so a submission always matches a
`SpeakerAvailabilityWindow` and the backend stops rejecting it. This **reverts the D-703 free-pick
sheet** to a slots-driven picker (see the merged workflow in item 7 — approval is hall-time-aware).
**Action:** ① rebuild `meeting_request_sheet` to call `getAvailableSlots(speakerId)` and render only
real free slots (empty-state when none) · ② remove the hard-coded 7-day / 9-chip client picker ·
③ update `meeting_request_sheet_test` + golden · ④ E2E: pick-real-slot → submit → Pending.
**Existing tests:** `mobile-speaker-profile.md` E2E-MOB020-005/012/017; `cp-admin-speaker-meeting-requests.md`
E2E-SMR-001/002; `cp-admin-speaker-availability.md` E2E-SAV-001..004; `mobile-requests.md` E2E-REQ-*.

## 7. Bilateral-meeting-in-hall workflow (was "g2g/g2b/g2vip")  `FEATURE`
**OWNER CLARIFICATION (2026-07-08) — g2g/g2b/g2vip are MEETING TYPES, not gate tiers:**
- **g2g** = delegation ↔ delegation (الوفود مع بعض) — group-to-group.
- **b2b / speaker-VIP** = a speaker with a VIP visitor — business-to-business.
- Every bilateral meeting **happens in a (meeting) hall**.

**Owner's required workflow:**
1. **CP manages meeting halls** — capacity ("how many persons can be [there]") **and available time
   windows** for meetings. Needs a **clear CP UI/UX to manage a hall's meeting-time slots**.
2. App meeting request (speaker/VIP or delegation) → **admin reviews in CP**; before approving, admin
   **checks the hall's available time**; admin can **approve or reject**.
3. On admin **accept** → **email the speaker** (email from their system profile) with **two links:
   Approve / Reject** in the email.
4. Speaker **approves via email** → the meeting appears in the requester's **"اللقاءات الثنائية"**
   (bilateral meetings) list in the app.

**Current state (what exists to build on):**
- `SpeakerMeetingRequest` + `SpeakerAvailabilityWindow` (speaker-time slots, NOT hall-time) —
  `SpeakerMeetingRequestService.cs`. CP respond queue `SpeakerMeetingRequestsList.razor`.
- Delegation meetings — `cp-admin-delegation-meetings.md` (`DelegationMeeting`); B2B tables —
  `cp-business-meetings.md` / `cp-meeting-tables.md`.
- Halls — `HallsList.razor`, `HallPurpose {General,Booth,Session,Meeting}`, `Hall.Capacity`.
- **Gaps vs owner ask:** (a) availability is speaker-based, not **hall-time-based**; (b) no CP
  hall-meeting-time management UI; (c) no **speaker email confirmation** step with tokenized
  approve/reject links; (d) meeting-type taxonomy (g2g / b2b) not modeled explicitly.

**Sub-tasks (needs its own design + owner approval before build):**
- 7a. CP: manage a meeting hall's capacity + **meeting time-slots** (new UI/UX).
- 7b. Backend: tie a meeting request to a hall + hall time-slot; admin approval validates hall
  availability (not just speaker slot).
- 7c. Backend: on admin-accept, send the speaker a **tokenized email** (approve/reject links) →
  public endpoint that consumes the token → sets Accepted/Rejected.
- 7d. App: approved meeting surfaces in **اللقاءات الثنائية** (requests feed already exists).
- 7e. Model the meeting type (g2g delegation / b2b speaker-VIP) end-to-end.
**Existing tests:** `cp-admin-halls.md` E2E-HAL-*; `cp-admin-speaker-meeting-requests.md` E2E-SMR-*;
`cp-admin-speaker-availability.md` E2E-SAV-*; `cp-admin-delegation-meetings.md` E2E-DLM-*;
`cp-admin-gates-operator.md` E2E-GOP-* (scan at gate). **New E2E needed** for 7a–7e.
**NOTE:** email approve/reject links = an **outward-facing** flow (sends email, public token endpoint)
— design + owner sign-off required before building (schema addition under D-219 lift, re-freeze before
handover).

## 8. Time/event-triggered ratings (was "watched-at + each date")  `FEATURE`
**OWNER CLARIFICATION (2026-07-08) — ratings are triggered at several times/events, not one:**
- **Daily rating** — if the user checked in, prompt **at the end of that day/date**.
- **End-of-exhibition rating** — an overall event rating at the close of the event.
- **End-of-session rating** — triggered **on gate check-out** from the session/hall.
- **Online-session rating** — on the live YouTube-stream page, when the user **backs/closes/ends**
  the stream, show the online-session rating.

So "watched at [time] and each date" = the rating is offered **per date and per session watched**,
fired by the matching time/event.

**Current state** — `RatingResponse` stores `TargetId` (= `Session.Id` for a session-scoped type),
`OverallStars`, answers, comment, inherited `CreatedAt` (submitted-at) — `RatingResponse.cs:27-49`.
One trigger exists: `SessionRatingPromptWorker` fires a "rate this session" notification off
`Session.RatingPromptSent`. No daily / end-of-event / gate-checkout / live-close triggers; no
per-date grouping; the rate UI shows nothing about when/what was watched
(`rate_screen.dart`, `rating_models.dart:71-100`).

**Sub-tasks (needs its own design + owner approval before build):**
- 8a. **Daily** rating trigger — for checked-in users at end of day (worker + notification, keyed per
  date so it fires once/day).
- 8b. **End-of-exhibition** rating trigger — one overall-event prompt at event close.
- 8c. **End-of-session on gate check-out** — a check-out scan (`ScanDirection.Out`) fires that
  session's rating prompt / deep-link.
- 8d. **Live-stream close** — on back/close/end of `live_broadcast_screen`, present the online-session
  rating.
- 8e. Rating list shows **which session + its date/time**, grouped per date.
**Existing tests:** `cp-admin-ratings.md` E2E-RAT-003/004 (submitted-at only); `mobile-rate.md`
E2E-MOB040-010 (session deep-link); `mobile-gate-scan.md` E2E-MOBGATE-* (check-out). **New E2E needed**
for 8a–8e.
**NOTE:** rating-type config (`RatingType`/scope) may already support daily/event/session scopes —
confirm during design before adding schema. Any schema addition is under the D-219 lift, re-freeze
before handover.

## 9. Open badge from notification on click  `BUILT`
**Current state** — `notifications_screen.dart` `_maybeDeepLink` opens `/badge` when
`clickUrl == '/badge'`, or kind is `BookingConfirmed` / `AccountApproved`
(`notifications_screen.dart:167-194`).
**Existing tests:** `mobile-notifications.md` E2E-MOB033-007; `mobile-badge.md` E2E-MOB032-001.
**Action:** confirm which notification the owner taps (e.g. an "your badge is ready" kind); if a new
kind must open the badge, add it to the mapping + backend `clickUrl`.

## 10. Agenda design vs Figma — incl. missing from→to line  `BUG`
**Current state** — `SessionsScreen` built to LIVE frame **883:2308** (`sessions_screen.dart:22`);
header asserts parity but owner reports a mismatch. Sections: search field, white day strip,
day title+logo banner, type tabs (الكل/جلسات/ورش العمل), المواعيد list (first featured).

**SPECIFIC BUG (owner 2026-07-08) — "line missing between from and to time":** the time rail's
from→to connector is an `Expanded` with `vertical: space1` padding
(`session_timeline_row.dart:164-175`). Inside `IntrinsicHeight` the rail height = the (short)
content height, so on **collapsed rows** (short title, no banner/description) the `Expanded` collapses
to ~0px and the 8px padding eats the rest → the connector line disappears. It only shows on the
featured (tall) row. **Fix:** give the connector a **minimum height** (or ensure the row is always
taller than the two stacked time labels) so the line always renders — verify the exact treatment vs
Figma node 1310:3241/3244 before editing.

**Existing tests:** `mobile-agenda.md` E2E-MOB016-014 (visual), `web-programme.md` E2E-WPG-*.
**Action:** ① fetch node 883:2308 + 1310:3241 via Figma MCP → mismatch table (incl. the connector) →
② fix the collapsing connector + any other diffs → ③ regenerate golden → ④ on-device compare. Update
`Page_016` doc + E2E in the same changeset.

## 12. Ask-speaker — TWO modes  `FEATURE`
**OWNER CLARIFICATION (2026-07-08):** asking the speaker/host has **two distinct ways:**
1. **Live inside the session hall** — questions asked during a live session. The **home-menu entry
   for this must be visible to MODERATOR only** (filter the home menu by role = moderator).
2. **Pre-question before the session starts** — questions submitted ahead of time, **filtered/screened
   by AI + the team + the moderator** before being surfaced.

**Current state (what exists):**
- Send-question screen (#26, `send_question_screen`, Figma 934:3636 / 1056:12876), reached from session
  detail's اسأل المحاور — enabled only after the user JOINED the session
  (`session_detail_body.dart:99-107`, `ask_host_card`).
- Moderator Q&A desk (`session_moderate_screen`, `/sessions/:id/moderate`), moderator-EXCLUSIVE
  (`session_detail_screen.dart:342`, D-519). CP question queue (`cp-admin-question-queue`).
- Documented: `FDS-007` §5.2 Session questions, §5.3 moderator + queue, §5.4 two-stage moderation;
  `Page_026` docs. **Gap vs owner ask:** (a) the explicit **live-in-hall vs pre-question** split;
  (b) a **home-menu entry gated to moderator only** for the live in-hall Q&A; (c) **AI screening** of
  pre-questions (AI + team + moderator filter chain).
**Needs a spec first** (see Phase 0-D2), then app + backend (question-mode field + AI screen step +
home-menu role filter) + E2E.
**Existing tests:** `mobile-send-question.md` E2E-MOB026-*; `mobile-session-moderate.md` E2E-MOBMOD-*;
`cp-admin-question-queue.md` E2E-QQU-*; `cp-session-moderate.md` E2E-MOD-*.

---

## Cross-cutting

- **CP E2E authoring debt** — session-chain CP files (`cp-admin-sessions`, `cp-admin-bookings`,
  `cp-admin-sessions-seat-plans`, `cp-admin-halls`, `cp-admin-gates`, `cp-admin-gates-operator`,
  `cp-admin-hall-arrivals`, `cp-admin-ratings`, `cp-admin-session-summaries`) have most rows
  `_to author_` (Gherkin present, browser E2E not driven; lower-layer xUnit green).
- **Removed screens still catalogued** — exclude `mobile-my-sessions.md`, `mobile-saved-sessions.md`,
  `mobile-my-meetings.md` (D-609/D-479) from the regression pass.
- **Pre-commit guard** — restore `FLAG_SECURE` in `MainActivity.kt` (temporarily commented for debug
  capture) before any commit — NCA control.
- **Per-page DoD** for every fix: docs (PAGE-INDEX + per-page) + unit/integration tests + E2E
  catalogue, same changeset; review agents + `simplify`; live on-device render vs Figma.

## Execution order — DOC-FIRST, then build

### Phase 0 — Documentation (author/extend controlled specs; owner sign-off BEFORE build)
The new rules are NOT in the controlled docs (verified 2026-07-08). Each is authored into the real
controlled doc first, reviewed, then built. `SESSION-MODULE-TODO.md` is a working plan, NOT a spec.
- **D0-1 (Item 7)** — extend **FDS-013** (+ FDS-008 app-request side): the app request → admin review
  vs **hall meeting-time availability** → **email speaker (Approve/Reject links)** → speaker confirms →
  shows in **اللقاءات الثنائية**; the **g2g (delegation↔delegation) / b2b (speaker↔VIP)** meeting-type
  taxonomy. + SRS FR codes + UCS + speaker-profile/requests/my-meetings + CP page docs + E2E.
- **D0-2 (Item 12)** — extend **FDS-007 §5.2/5.3** + `Page_026` + session-moderate: the **live-in-hall
  (home menu = moderator-only)** vs **pre-question (AI + team + moderator filter)** two-mode split. + E2E.
- **D0-3 (Item 8)** — add a **rating section** to **FDS-007** (or a new FDS): the multi-trigger model
  (daily / end-of-exhibition / end-of-session-on-gate-checkout / live-stream-close) + per-date list.
  + SRS FR + rate page doc + E2E.
- **D0-4 (bug fixes 1/2/10)** — no new spec; update the page doc + E2E **in the same changeset** as each fix.

### Phase 1 — Build (each item: §11 plan → approve → code → test → review+simplify → commit)
**Wave A — bug fixes (unblocked; docs = page-doc + E2E in-changeset):**
1. **Item 1** — widen `/app/presentations` to all active sessions.
2. **Item 10** — fix the missing from→to line + agenda Figma diff (+ golden).
3. **Item 2** — reproduce + fix join-to-session (missing seat-map path).
4. **Item 4** — surface `SeatSessionFull` toast + add random-cap E2E (server cap already enforced).
5. **Item 9** — confirm badge-from-notification kind; extend mapping if needed.

**Wave B — decision defaults (confirm, then small):**
6. **Item 6** — rebuild the meeting sheet to real available slots (reverts D-703; part of the item-7 spec).
7. **Item 5** — (default) keep summaries reachable; restore a More entry only if owner confirms.
8. **Item 3** — (default) use the existing CP hall-arrivals console; new mobile scan screen only if confirmed.

**Wave C — large features (BUILD ONLY AFTER the Phase-0 spec is approved):**
9. **Item 7** — bilateral-meeting-in-hall workflow (per D0-1 spec). Outward-facing email flow.
10. **Item 12** — ask-speaker two modes (per D0-2 spec).
11. **Item 8** — time/event-triggered ratings (per D0-3 spec). Worker + notification heavy.

**Wave D — regression:**
12. Author the `_to author_` CP E2E rows (sessions/bookings/halls/gates/ratings/summaries) + drive a
   consolidated session-module regression pass on prod.

**Note:** Wave C features involve schema additions (D-219 lift; re-freeze before handover) and an
outward-facing email flow — each gets its Phase-0 spec + a §11 pre-approval plan before any code.
