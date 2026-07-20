# SIMF — Bugs & Updates TODO

> Working tracker for reported bugs and requested updates.
> Created: 2026-07-20 · Branch: `feat/worker-ops-monitor`

**Status legend:** ☐ Open · ◐ In progress · ✅ Done · ⏸ Deferred · ❌ Won't fix

**Priority legend:** P0 Critical · P1 High · P2 Medium · P3 Low

**Type legend:** 🐞 Bug · ✨ Update/Feature · 🧹 Chore · 📄 Docs · ❓ Verify/Decision · 🗂 Data/Content

---

## Summary

| Metric | Count |
|--------|-------|
| Total items | 12 |
| Open (☐) | 12 |
| In progress (◐) | 0 |
| Done (✅) | 0 |
| Deferred (⏸) | 0 |

---

## Topic 1 — Programme model check (Day / Session / Hall / Seat / Speaker)

**Requirement checked (2026-07-20):** _Per programme date: a Day Title + Day image;
each day has a programme of items that may be Event / Workshop / Session; each
session has a hall and a seat; a user can pre-reserve; each session has a speaker;
each speaker has a profile._

**Result:** the data model already covers every piece of this requirement — and is
more complete than stated. The items below are **gaps to fill / decisions to
confirm**, not a broken model.

### Already built (verified in code)

| Requirement | Backed by | Where |
|---|---|---|
| Day Title (bilingual) | `ProgrammeDay.Title` / `TitleArabic` | `src/Backend/SIMF.Domain/Programme/ProgrammeDay.cs` |
| Day image | `AssetCategory.ProgrammeDayImage` (StoredFile keyed by day Id — no column) | `src/Shared/SIMF.Common/Enums/AssetCategory.cs` |
| Item type Event/Workshop/Session | `SessionType { Workshop=0, Session=1, Event=2 }` on `Session.Type` | `src/Shared/SIMF.Common/Enums/SessionType.cs` |
| Session → hall | `Session.HallId` → `Hall` (real FK, same DbContext) | `src/Backend/SIMF.Domain/Programme/Session.cs` |
| Session → seat | `HallSeatLayout` (rows × seats) + `Session.CapacityOverride` + `SeatSelectionMode` | `src/Backend/SIMF.Domain/SeatReservations/HallSeatLayout.cs` |
| Pre-reservation | `SeatReservation` (+ approval workflow) | `src/Backend/SIMF.Domain/SeatReservations/SeatReservation.cs` |
| Session → speaker | `SessionSpeaker` M-to-M (+ `Role`, `DisplayOrder`) | `src/Backend/SIMF.Domain/Programme/Session.cs` |
| Speaker profile | `Speaker` (bio, quals, photo, socials, optional `UserProfileId`) | `src/Backend/SIMF.Domain/Programme/Speaker.cs` |

---

## Items

| # | Type | Priority | Area | Title | Status |
|---|------|----------|------|-------|--------|
| 1 | 🗂 Data/Content | P1 | CP / Content | Per-day images not uploaded yet (schema ready, no data) | ☐ Open |
| 2 | ❓ Verify/Decision | P2 | Domain / App | Session↔Day linked by date, not FK — confirm intended | ☐ Open |
| 3 | ❓ Verify/Decision | P2 | API / Domain | `Session.Type` is optional — a session need not be Event/Workshop/Session | ☐ Open |
| 4 | 🐞 Bug/Gap | P2 | API / Domain | A session can be saved with **zero speakers** (no min-1 rule) | ☐ Open |
| 5 | 🗂 Data/Content | P1 | CP / Config | Halls with no `HallSeatLayout` have no seat picker — verify all seat-halls seeded | ☐ Open |
| 6 | ❓ Verify/Decision | P2 | API / App | Pre-reservation needs admin approval (Pending→Approved) — confirm | ☐ Open |
| 7 | ❓ Verify/Decision | P3 | Domain | Event/Workshop/Session share one entity + all carry hall/seat/speaker | ☐ Open |

---

## Detailed notes

### [#1] Per-day images not uploaded yet
- **Type:** 🗂 Data/Content · **Priority:** P1 · **Area:** CP / Content
- **Finding:** the day banner/logo is a `StoredFile` asset (`AssetCategory.ProgrammeDayImage`, value 6) owned by the `ProgrammeDay.Id`, uploaded from the CP Programme-Days add/edit page. The **schema and upload path exist**; there is simply no image data for the days yet.
- **Not a code gap** — it is a content/data task.
- **Fix plan (no code):** upload each day's image via CP → Programme → Days (edit) → image field, OR seed `StoredFile` rows per the seed convention (`docs/migrations/<year>/`). Confirm target image spec (size/ratio) against the Figma "تفاصيل اليوم" banner (883:2308).
- **Status:** ☐ Open

### [#2] Session ↔ Day is matched by date, not a foreign key
- **Type:** ❓ Verify/Decision · **Priority:** P2 · **Area:** Domain / App
- **Finding (by design):** `ProgrammeDay` has **no FK** from `Session`. The agenda buckets sessions under a day by matching the session's event-local (+03:00) start date to `ProgrammeDay.Date` (`ProgrammeSessionService`/`AdminSessionService` group by `StartUtc.ToOffset(+03:00)`).
- **Implication to confirm:** a session on a date with **no** `ProgrammeDay` row still renders, but under a bare date header (no title, no image). A `ProgrammeDay` with no sessions renders its title+banner with an empty list.
- **Decision needed:** is date-matching acceptable, or do you want a hard day→sessions parent-child link? (Changing to an FK is a schema change against the D-110/D-199 surface — needs owner approval.)
- **Status:** ☐ Open

### [#3] `Session.Type` is optional — item is not forced to be Event/Workshop/Session
- **Type:** ❓ Verify/Decision · **Priority:** P2 · **Area:** API / Domain
- **Finding:** `Session.Type` is `SessionType?` (nullable). `AdminCreateSessionRequest.Type` / `AdminUpdateSessionRequest.Type` are nullable and `AdminSessionService` sets it directly with no required-type check. Untyped sessions appear only under the app's "All / الكل" tab.
- **Decision needed:** your requirement says each programme item "may be Event/Workshop/Session". If a type must be **mandatory**, add a `NotNull` validation on create/update (app-layer only, no schema change). If optional is fine, no action.
- **Proposed fix (pending decision):** add a FluentValidation rule (or service guard) requiring `Type` on `AdminCreateSessionRequest`/`AdminUpdateSessionRequest`; add UI required marker on `SessionsAddEdit.razor`; add unit test.
- **Status:** ☐ Open

### [#4] A session can be saved with zero speakers
- **Type:** 🐞 Bug/Gap · **Priority:** P2 · **Area:** API / Domain
- **Finding (verified):** `AdminSessionService.EnsureSpeakersExistAsync(entries, …)` returns early when `entries.Count == 0` — it only checks that *supplied* speakers exist. There is **no minimum-speaker rule**, so a session can be created/updated with an empty speaker list. `AdminCreateSessionRequest.Speakers` defaults to an empty list.
- **Decision needed:** "each session has a speaker" — is ≥1 speaker a hard rule? Note some kinds (e.g. an `Event` like an opening ceremony) may legitimately have none — so the rule may need to be per-`Type`.
- **Proposed fix (pending decision):** enforce `Speakers.Count >= 1` (optionally exempt `Type == Event`) in `AdminSessionService.CreateAsync`/`UpdateAsync` via `DataValidationException`; mirror in `SessionsAddEdit.razor`; add unit + integration test; update the session E2E catalogue.
- **Status:** ☐ Open

### [#5] Halls without a seat layout have no seat picker
- **Type:** 🗂 Data/Content · **Priority:** P1 · **Area:** CP / Config
- **Finding (by design):** `HallSeatLayout` is an **optional** 1:1 with `Hall`. A hall with no layout has no per-seat grid and falls back to random/capacity-only allocation against `Hall.Capacity`. So "each session has a seat (to pick)" only holds where the hall has a layout **and** the hall/session `SeatSelectionMode` is `AssignedSeat`.
- **Action:** verify every hall that hosts seat-selection sessions has a `HallSeatLayout` configured (CP → Halls → seat layout), and that `RowLabels.Count × SeatsPerRow ≤ Hall.Capacity`.
- **Not a code gap** — config/data. Only becomes a code item if you want to *block* assigning an `AssignedSeat` session to a hall with no layout.
- **Status:** ☐ Open

### [#6] Pre-reservation requires admin approval
- **Type:** ❓ Verify/Decision · **Priority:** P2 · **Area:** API / App
- **Finding (D-227):** a visitor booking (`UserBooking` / `RandomAssignment` / `OpenSeating`) is created `BookingStatus.Pending` and holds the seat until an admin **Approves or Rejects** it in the CP; admin-blocked rows are created `Approved`. Pending holds auto-expire (`ExpiresUtc`).
- **Confirm:** is the approve/reject step intended for the user's "pre-reserve" flow, or should a visitor booking confirm instantly? (This is an existing, deliberate workflow — flagged only to confirm it matches intent.)
- **Status:** ☐ Open

### [#7] Event / Workshop / Session share one entity and all carry hall/seat/speaker
- **Type:** ❓ Verify/Decision · **Priority:** P3 · **Area:** Domain
- **Finding:** Event, Workshop and Session are one `Session` entity distinguished by `Type`. All three therefore optionally carry hall, seat layout, speakers, reservation, live stream, etc.
- **Confirm:** if an `Event` should **not** have seats/speakers/reservation (or a `Workshop` has different rules), decide whether `Type` should gate those fields/flows. Currently everything is flexible/optional, so the model already allows an Event with no seats or speakers — it is just not *enforced*.
- **Status:** ☐ Open

---

## Topic 2 — Time zone · Biometric · Bulk profile/badge generation

**Reported:** 2026-07-20 (owner batch)

### Items

| # | Type | Priority | Area | Title | Status |
|---|------|----------|------|-------|--------|
| 8 | ✨ Update | P1 | Whole system | Store & handle **all** times in Saudi local time (AST, UTC+3) — no UTC | ☐ Open |
| 9 | 🐞 Bug | P1 | App / Auth | Biometric login not working / logic incorrect | ☐ Open |
| 10 | ✨ Feature | P1 | CP / Badges | Bulk profile + badge (QR) generation page, no user account attached | ☐ Open |

---

### [#8] All times in Saudi local time — no UTC
- **Type:** ✨ Update · **Priority:** P1 · **Area:** cross-cutting (Domain / Workers / Audit / App / CP)
- **Requirement (owner):** "All time in system is saved as Local Saudi time zone, no any UTC."
- **Current state (verified):** times are stored **UTC** — `Session.StartUtc` / `EndUtc` (`DateTimeOffset`), audit stamping via `UtcNow`, all workers use `UtcNow` (`SessionReminderWorker`, `PendingBookingExpiryWorker`, rating-prompt workers, etc.). The agenda/booking layers convert UTC → `+03:00` (`EventOffset`) **only for display/grouping** (`ProgrammeSessionService`, `AdminSessionService`, `SeatReservationService`). So today: **storage = UTC, display = Saudi local**.
- **Prior work:** a Saudi-local effort exists (branch `feat/local-time-saudi`, "CP timezone" wave) — verify its current state before starting; don't duplicate.
- **✅ DECISION (owner, 2026-07-20): (b) Store wall-clock Saudi local in the DB — no UTC anywhere.**
- **What (b) means technically:** Saudi Arabia has **no DST** and a fixed +3 offset, so wall-clock Saudi is unambiguous. Recommended implementation: stamp every timestamp as `DateTimeOffset` at **+03:00** (never `UtcNow`) via one `SimfClock` / `TimeZoneInfo("Arab Standard Time")` seam, and treat all stored values as Saudi local. (Keeping `DateTimeOffset` at +03:00 already satisfies "no UTC"; a column rename `*Utc → *Local` is optional clarity, not required.)
- **Surfaces to change:** `AuditStamping` interceptor, all workers (`SessionReminderWorker`, `PendingBookingExpiryWorker`, `ProgrammeRatingPromptWorker`, `SessionRatingPromptWorker`, `HallAttendanceCloseoutWorker`, `MeetingAwaitingSpeakerExpiryWorker`, `RegistrationGateAutoCloseWorker`), token/OTP expiry (NCA 5min/24h caps, D-443), `Session.StartUtc/EndUtc` + every `*Utc` / `DateTimeOffset` field, reminder/rating dedup guards, EF default-value stamps.
- **⚠ Two things to settle before a change plan:**
  1. **Freeze approval** — `*Utc` columns are on the D-110/D-199 freeze surface. Reinterpreting/renaming them needs explicit owner sign-off in the §11 plan.
  2. **Existing-data conversion** — rows already hold **UTC instants**; switching to Saudi wall-clock means existing values must be shifted **+3h** (a one-time data migration) or they will render 3 hours early. Must be planned, not silent.
- **Open questions:** (1) scope — everything (audit/tokens/worker schedules) or just user-facing programme/session/booking times? (2) rename `*Utc` columns or keep the names and just change the stored offset? (3) is a +3h back-fill of existing data required, or is prod data disposable (per the deploy "drop DBs" note)?
- **Next step:** confirm the 3 open questions → I write the §11 pre-approval plan (incl. freeze-lift request + data-migration) → approve → build. **No code yet.**
- **Status:** ☐ Open — decision (b) locked; scope Qs pending

### [#9] Biometric login not working / logic incorrect
- **Type:** 🐞 Bug · **Priority:** P1 · **Area:** App / Auth
- **Report (owner):** "Biometric not working and logic is incorrect."
- **Context (to re-verify in code before fixing):** biometric touches several prior items — biometric-gated OTP (D-486), the QR-login + banking-style biometric flow (D-737/738), and device-key handling (`DeviceKey`). Needs a fresh trace of the current flow.
- **Needed from owner to reproduce (blocking a fix plan):**
  1. Which flow — first enrol, unlock on launch, biometric-gated OTP, or QR-login?
  2. Which device / OS (tablet TXZ-W09 arm64?) and is it hitting **prod** or local?
  3. What actually happens — no prompt, prompt then error, wrong user, loops, silent fail? Exact on-screen/toast text.
  4. Expected behaviour in your words.
- **Proposed next step:** once the flow is identified, trace `simf_app` biometric service + the device-key / token exchange end-to-end, state root cause, then propose a fix (no code until approved).
- **Status:** ☐ Open

### [#10] Bulk profile + badge (QR) generation page
- **Type:** ✨ Feature · **Priority:** P1 · **Area:** CP / Badges / Email
- **Requirement (owner):** a CP **bulk-add** page that generates profiles **without any associated user account** — just simple placeholder data. The admin picks a **profile type** and a **count**, presses Add (repeat for more types), then the system generates them all, links them together as one batch, and **issues a badge (QR) for each**. Example: select **VIP**, count **5**, press Add → 5 VIP badge profiles; then select another type + count, and so on.
  - Must support **all profile types** (Visitor / Other / Delegate).
  - A **"Bulk add"** button opens a **popup**; the popup **must have an email field** so all the generated QRs/badges can be emailed out in one go.
- **Sketch of the flow (to confirm):**
  1. CP page → rows of `[Profile type ▾] [Count] [Add]`; build a batch list (e.g. VIP×5, Delegate×3).
  2. "Bulk add" → popup with the batch summary + an **email** input.
  3. On confirm: generate N profiles per type (no `SimfUser` link), tag them with one batch id, generate each badge QR, and email all QRs to the address.
- **✅ DECISION (owner, 2026-07-20): anonymous placeholders.** Each generated badge is a placeholder (e.g. "VIP #1..#5") with **no `SimfUser`/Identity account** and no per-person name/email. All generated QRs are emailed to the **one organiser address** entered in the popup. → resolves the "no user associated" + email-destination questions.
- **Open questions (still blocking design):**
  1. **"Linked together"** — what does the link mean? One shared batch/group id on the profiles (so a batch can be re-emailed / revoked / reported together)? Confirm.
  2. **Badge/QR format in the email** — one PDF contact-sheet of all QRs, or individual PNG attachments? Reuse the existing badge-QR renderer (square + `tryHarder`, per the QR-decodability fix) and `StoredFile` path.
  3. **Profile-type source** — the dynamic `UserProfileType` catalogue, or a fixed Visitor / Other / Delegate set? (Owner wording lists those three.)
  4. **Later claim** — can an anonymous badge later be attached to a real person/account (self-claim by scanning), or does it stay anonymous for the whole event?
- **Confirmed constraints:** new CP page ⇒ new `PermissionCatalog` code + seed + gate on API **and** page (project HARD RULE), admin-only; pure `UserProfile` (App DB) rows keep D-157 data/identity separation intact (no Identity account created).
- **Proposed next step:** answer the 4 open questions → I write the §11 pre-approval plan (App-DB placeholder entity/batch + migration, generate endpoint + permission, CP page + popup, badge/QR render + single-email send, docs + E2E + tests) → you approve → build. **No code yet.**
- **Status:** ☐ Open — anonymous-placeholder decision locked; 4 Qs pending

---

## Topic 3 — Session surfaces (phase-gated UX) · Face-capture left/right

**Reported:** 2026-07-20 (owner batch)

### Items

| # | Type | Priority | Area | Title | Status |
|---|------|----------|------|-------|--------|
| 11 | ❓ Verify/Update | P2 | App / Sessions | Four session surfaces must scope + phase-gate buttons per the matrix below | ☐ Open |
| 12 | 🐞 Bug | P1 | App / Sign-up | Face-capture left/right prompts swapped in create-profile (left shows right, right shows left) | ☐ Open |

---

### [#11] Session surfaces — scope + phase-gated buttons
- **Type:** ❓ Verify/Update · **Priority:** P2 · **Area:** App / Sessions
- **Requirement (owner):** each session surface shows a different slice, and buttons activate by the session's state:

| Surface | Scope shown | Buttons |
|---|---|---|
| **Home** | **Future only**, **type = Session only** (not Workshop/Event) | Details, **Join / Select seat**. No summary, no live link *until it starts* |
| **Session Summary** page | **Past only**, **all types** | **No join, no seat, no future actions** — summary + details only |
| **My Sessions** | The user's own sessions | (their bookings) |
| **Agenda** | **All** sessions | per state |

- **Owner's example (upcoming Session):** no summary yet (not started), no live link yet, but **must** show Details + Join/Select-seat. Summary page (past): none of the future actions.
- **Current state (verified):** the gating primitive **already exists** — `SessionPhase { upcoming, live, ended }` (`features/sessions/data/session_lifecycle.dart`) plus capability flags `hasPublishedSummary` / `hasLiveStream` / `hasRecording`. Doc says all four surfaces (`session_detail_body`, `session summaries`, `my_sessions_screen`, agenda) are meant to gate off this one rule.
- **So this is verify-and-fix, not new architecture.** What to check on each surface:
  1. **Home** applies BOTH filters — `phase == upcoming` AND `type == Session` (exclude Workshop/Event). ← most likely gap.
  2. **Summary page** shows only `phase == ended`, all types, and hides Join/Seat/Live entirely.
  3. Button visibility everywhere matches the matrix: `upcoming` → Details + Join/Seat, no Summary/Live; `live` → Live (if `hasLiveStream`) + Join; `ended` → Summary (if `hasPublishedSummary`) + Details, no Join/Seat/Live.
- **Related prior work:** session-state-gating (owner 2026-07-14, merged PR 93 + PR 96) and login-gate (D-576/D-577). Reconcile this matrix against what those shipped.
- **Proposed next step:** I trace the four screens + the Home/Summary providers against this matrix, produce a mismatch list, then a §11 plan for the deltas (+ goldens/tests). **No code yet.**
- **Status:** ☐ Open

### [#12] Face-capture left/right prompts swapped (create profile)
- **Type:** 🐞 Bug · **Priority:** P1 · **Area:** App / Sign-up (create profile) · also affects My-Area avatar
- **Report (owner):** during **user create-profile** face recognition, left/right are inverted — "left shows right and right shows left."
- **Where (verified):** create-profile reuses the **same** liveness flow as My-Area — `sign_up_visitor_screen` imports `identity_verification_screen` (`CapturedSelfie`), and both run `features/myarea/data/liveness.dart` (`LivenessStep.turnRight/turnLeft`, `livenessPromptDirection`, `livenessStepSatisfied`, `livenessInvertYaw`). So a fix covers both surfaces.
- **⚠ Known-fragile area:** this exact "prompt swap" was fixed twice before — **D-684** and **PR-103** — and a face-capture regression rode in via a router change (**D-666**). `liveness.dart` explicitly documents the invariant and inverts yaw **only on iOS** (`livenessInvertYaw == platform == iOS`); the detection gate itself looks correct and is unit-tested.
- **Hypotheses for the current swap (do not assume — verify on-device):**
  1. **RTL arrow mirroring** — the app is Arabic RTL-first; a directional arrow icon in `identity_verification_screen` may auto-mirror, so "turn right" renders a left-pointing arrow (matches "left↔right" symptom exactly). Prompt text vs arrow may disagree.
  2. **Android yaw-sign** — owner is on an **Android tablet** (TXZ-W09). Inversion is iOS-only; if this device/orientation delivers `headEulerAngleY` with the opposite sign, the gate accepts the wrong physical turn.
  3. **Prompt/step label** vs the required physical direction mismatched after a recent change.
- **Verification required (per app CLAUDE.md §13.3 blast-radius):** reproduce on the actual device, check prompt text + arrow + accepted turn together; a green golden did NOT catch D-666 — needs the flow test + on-device render.
- **Needed from owner:** confirm it's create-profile (sign-up) and the device/OS (Android tablet?), and whether the **arrow**, the **text**, or the **accepted turn** is the wrong one (or all).
- **Proposed next step:** trace `identity_verification_screen` prompt/arrow rendering + the yaw path on-device, isolate which of the 3 hypotheses holds, then a §11 fix plan + a regression test that pins the direction. **No code yet.**
- **Status:** ☐ Open

---
```
(New topics/items append below this line.)
```
