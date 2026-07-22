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
| Total items | 43 |
| Open (☐) | 37 |
| In progress (◐) | 0 |
| Done (✅) | 6 |
| Deferred (⏸) | 0 |

_Note: #16 is a single tracked item covering the whole per-feature Flutter clean-code sweep (≈39 features). Items #35-#40 are detailed in the plan (`~/.claude/plans/...`); #40 detailed here in Topic 9; #42-#43 (home greeting + hero) detailed in Topic 10._
_Done (6), shipped to PR: #10 `feat/bulk-badge-email`, #32 `feat/cp-team-roles`, #20+#17 `feat/app-sessions-batch`, #35 `feat/session-summary-video`, #13 `feat/meet-people-partner-directory` (#28 in progress)._

---

# ✅ TO-DO CHECKLIST (grouped by App / CP / Backend, ordered by priority)

> Tick `[x]` when a task is done. Each `#NN` links to its full brief + fix plan in **Part 2 — Details** below.
> Priority: **P1** high · **P2** medium · **P3** low.

## A. APP (Flutter mobile)

**P1 — bugs / blocking**
- [ ] **#9** — Biometric / Face ID login not working; logic incorrect. _(= #25)_
- [ ] **#12** — Face-capture left/right swapped on **Android** (iOS fine): fix Android turn direction + correct prompt image. _(= #26)_
- [ ] **#17** — Session join + seat mechanism: two cases (register-to-attend / join+pick-seat), **no approval**, auto-cancel 3 min before start, session **check-in + check-out**.
- [ ] **#18** — "الانضمام إلى الجلسة" (Join session) button does nothing.
- [ ] **#21** — "مشاركة جهة اتصال" (Share contact) button does nothing.
- [x] **#13** — "Meet people like you": show **only Sponsors + Speakers** (hide Normal/VIP), Other-type opt-in checkbox, viewer→gallery / speaker→details, show exact data. _(CP-managed)_ **Done** on `feat/meet-people-partner-directory` (as-built directory = Sponsors + Speakers + Booth companies + opted-in Other-type members; Normal/VIP excluded; speaker→profile, sponsor→detail, booth→exhibitor; person row non-tappable; CP toggle `PartnerDirectoryEnabled`).

**P2 — updates / UX**
- [ ] **#11** — Session surfaces scope + phase-gated buttons (Home = future + type=Session only; Summary = past; My Sessions; Agenda = all).
- [ ] **#14** — Edit interests from profile (not only at sign-up).
- [ ] **#16** — Clean-code sweep across ~39 features (numbers→tokens, `app_style`, `SimfTokens.surface`, hoist assets, extract private widgets).
- [ ] **#19** — Login-as-**Guest** label (not "Visitor") + fix wrong Arabic translation.
- [ ] **#20** — Agenda viewable **without login** + rename program-icon label to "الأجندة".
- [ ] **#22** — Sign-up (`sign_up_visitor_screen`) category section UI update.
- [ ] **#23** — Session-summary logic update (keep Home Sessions + Summary buttons as-is; reconcile with #11).
- [ ] **#27** — Video > 30 min: the session-extension alert must NOT appear.
- [ ] **#34** — Speaker job title shows English regardless of app language (read `RankArabic`).

**P3 — home cosmetics**
- [x] **#42** — Home greeting: **first name only** (first token) + `مرحبًا` (replacing time-of-day `صباح الخير` + full name). **Built** `39685d58` on `feat/app-home-greeting`; PR pending.
- [x] **#43** — Home **hero = rotating edition banner** (name/theme/dates/location over CP-managed `/app/banners` images), reusing the Banner feature (no new table/migration/freeze-lift). **Built** `feat/app-home-hero`. _(overlaps #40)_

## B. CONTROL PANEL (CP)

**P1**
- [x] **#10** — Bulk profile/badge generation page (pick type + count, anonymous placeholders, issue QRs, email all to one address). **Base + 2026-07-22 batch-builder redesign shipped** (on `/admin/delegates` + `/admin/visitors`); persisted batch / PDF contact-sheet / self-claim = Phases 2–4 (owner-gated) — see Topic 2 #10.
- [ ] **#28** — Meeting/speaker date filter = **forum dates only** + all CP times in **Saudi time** (see #8).
- [ ] **#29** — Workshop management in CP (title / time / allowed count / check-in-out); app shows **title + time only**.
- [ ] **#30** — B2B / B2G bilateral-meeting management in CP + activate VIP↔speaker "send request" button.
- [ ] **#31** — Feed the forum program data (content).
- [ ] **#1** — Upload per-day program images (schema ready, no data uploaded).
- [ ] **#5** — Configure hall seat layouts (halls with no layout have no seat picker).
- [ ] **#33** — Deliver CP user manual (⚠ confirm due date — 19-07-2026 is already past).

**P2**
- [ ] **#32** — CP permissions = **Admin / Security / PR / Scientific** (verify + gap-fill).

## C. BACKEND / CROSS-CUTTING

**P1**
- [ ] **#8** — Store all times as **Saudi wall-clock, no UTC** (needs scope + data-migration decision).
- [ ] **#6** — Remove seat/attendance **approval** workflow (D-227) — no approval (implemented via #17).
- [ ] **#24** — Change-email flow (fixes CP new-account typos + app self-service) + re-verify + uniqueness. _(Identity is frozen)_
- [ ] **#40** — **Dynamic forum dates** — the fixed "23-25 November 2026" is hardcoded in Website/app/CP/seed strings; drive them all from a single dynamic source (OrganizationProfile event dates or ProgrammeDay). _(overlaps #28)_

**P2 — decisions**
- [ ] **#2** — Session↔Day linked by date, not FK — confirm intended.
- [ ] **#3** — Require session `Type` (currently optional) — decide.
- [ ] **#4** — Session allows zero speakers — add min-1 rule (maybe except Event).

**P3 — decisions**
- [ ] **#15** — Generic profile / extra-data for Speaker / Company / Booth / others — design.
- [ ] **#7** — Event / Workshop / Session share one entity — confirm.

---

# Part 2 — Details & fix plans (per item)

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

### [#6] Seat reservation — approval workflow (now: NO approval)
- **Type:** ✨ Update · **Priority:** P1 · **Area:** API / App
- **Finding (D-227):** today a visitor booking (`UserBooking` / `RandomAssignment` / `OpenSeating`) is created `BookingStatus.Pending` and held until an admin **Approves/Rejects** it; Pending holds auto-expire (`ExpiresUtc`).
- **✅ DECISION (owner, 2026-07-20): NO approval.** Neither session-attendance registration nor seat reservation needs admin approval ("لا توجد اعتماد تسجيل حضور جلسة / لا يوجد اعتماد حجز مقعد"). A booking confirms immediately; the only release is the **auto-cancel when the user does not check in** (see #17). → the D-227 Pending→Approve/Reject step must be removed/bypassed for visitor bookings.
- **Status:** ☐ Open — decision locked (remove approval); implemented as part of **#17**

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
| 10 | ✨ Feature | P1 | CP / Badges | Bulk profile + badge (QR) generation page, no user account attached | ◐ Base + redesign shipped; Ph2–4 open |

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
### AS-BUILT (base feature — shipped & merged, D-473 / D-751)

The base bulk generator **already exists and is merged** into the current branch
(originated on `feat/bulk-badge-email`, commit `e5fa0422`). It is not a standalone
"BulkBadge" page — it lives on the **Delegates desk** (`/admin/delegates`) and
posts to a real endpoint:

- **Endpoint:** `POST /api/v1/admin/visitors/bulk-generate`
  (`VisitorBulkEndpoints.BulkGenerateVisitorBadgesEndpoint`), gated by
  `PermissionCatalog.Visitors.BulkGenerate` + `RequireApprovedAccount`.
- **Service:** `AdminAccountService.Bulk.cs → BulkGenerateBadgesAsync` — caps 1000
  badges/request; pre-validates every `ProfileTypeId` (must be `IsForVisitor`);
  per badge creates a synthetic **Approved** `SimfUser` (`badge-{guid}@simf.local`,
  passwordless) + a placeholder `UserProfile` (`NationalityId = 0`, name
  `"{Type} #N"`, `IsDelegate` per request) and mints its QR. **No batch entity** —
  the "batch" lives only in the request DTO (`AdminBulkGenerateBadgesRequest.Batches`).
- **Email (D-751):** when an organiser email is supplied, all QR PNGs are zipped
  (`BuildBadgeZip`, QRCoder `PngByteQRCode`) and emailed via
  `EmailTemplateType.BulkBadgeDelivery`. Mail failure never rolls back the badges.
- **Tests:** `tests/SIMF.Api.Tests/DelegatesAndBulkBadgesTests.cs` (9 facts).

### REDESIGN (2026-07-22, Phase 1 — front-end, behaviour-preserving, shipped)

Owner asked (via `/front-end-design`) to redesign "create new user" professionally
for `/admin/visitors` (single **new** + **bulk**). Delivered without any schema /
API / package change:

- **Single create form** (`WalkInRegistrationForm.razor`) rebuilt to the house
  `SimfFormSection` numbered-card pattern (SpeakersAddEdit parity) with the
  responsive `simf-form__grid`; native select / date / file inputs replaced by
  `SimfSelect` / `SimfDatePicker` / `SimfFileUpload`. Fields, endpoint
  (`.../register-onsite` + deferred uploads) and validation unchanged.
- **Bulk generator** extracted into a reusable component
  (`BulkBadgeGenerator.razor`) and reshaped into the requested **batch-builder**:
  `[profile type ▾] [count] [+ Add]` → a removable batch list (swatch · name ×
  count) + live total → Generate → confirm popup (summary + optional organiser
  email). Same request contract.
- **Surfaced on both** `/admin/delegates` (in place, delegate-flagged by default)
  **and** `/admin/visitors` (a gated **"Bulk add"** toolbar button → dialog).
- **Bug fixed in passing:** the confirm-modal email field was missing
  `ValueExpression` (the D-648 freeze gotcha) — now added.
- **Tests:** `WalkInRegistrationFormTests` (3) + `BulkBadgeGeneratorTests` (5);
  full CP suite 221/221 green; CP `dotnet build -c Release` 0/0.

### Open questions → resolved

1. **"Linked together" (persisted batch)** — NOT persisted today (badges are
   ordinary rows). → **Phase 2** below adds a `BadgeBatch` table + a
   `UserProfile.BadgeBatchId` back-link so a batch can be re-emailed / revoked /
   reported together.
2. **Email format** — as-built = **ZIP of one PNG per badge**. A **PDF
   contact-sheet** is **Phase 3** (no PDF library exists in the solution yet —
   needs a package).
3. **Profile-type source** — **resolved as-built**: the dynamic `UserProfileType`
   catalogue filtered `IsActive && IsVisitor` (mirrors the API guard). No change.
4. **Later self-claim** — **resolved as-built**: already possible via the
   badge-activation flow (`BadgeAuthService`, D-430/D-737/D-738) which "promotes
   in place" a `@simf.local` placeholder when its QR is scanned + activated
   (freeze-safe, no Identity schema change). **Phase 4** only adds capturing the
   claimer's profile data on activation.

### Remaining phases (owner-gated — NOT yet built)

- **Phase 2 — persisted batch:** additive App-DB `BadgeBatch` + `UserProfile.BadgeBatchId`
  (real intra-DB FK). **Needs owner freeze-lift (D-110/D-199 App-additive) + a
  migration** + new `PermissionCatalog` codes for a batch view/re-email/revoke CP
  surface (gate API **and** page).
- **Phase 3 — PDF contact-sheet email:** add a PDF library (e.g. QuestPDF) to
  `SIMF.Infrastructure.csproj`. **Needs owner package approval** (§1.7 csproj +
  §14). The email attachment path is content-type generic, so no sender change.
- **Phase 4 — self-claim profile capture:** extend `badge_activation_screen.dart`
  + `CompleteActivationAsync` to fill the placeholder profile (name / nationality
  / interests) on claim. App + backend, **no migration** (freeze-safe).

- **Confirmed constraints:** admin-only; pure `UserProfile` (App DB) placeholder
  rows keep the D-157 data/identity separation intact (no Identity account with
  real credentials until self-claim).
- **Status:** ◐ In progress — **base feature + Phase-1 redesign shipped**; Phases
  2–4 (persisted batch / PDF / self-claim) remain, each owner-gated per above.

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
- **Report (owner):** during **user create-profile** face recognition, left/right are inverted — "left shows right and right shows left." **Update (owner, 2026-07-20): the direction is wrong on Android and works fine on iOS** ("Face detection: modify direction on Android" / "التأكد من نوع الجهاز عند التقاط الصورة الشخصية وعرض الرسالة التفت يمين / التف يسار بالصورة الصحيحة").
- **Where (verified):** create-profile reuses the **same** liveness flow as My-Area — `sign_up_visitor_screen` imports `identity_verification_screen` (`CapturedSelfie`), and both run `features/myarea/data/liveness.dart`. So a fix covers both surfaces.
- **Root cause (narrowed by the Android-only report):** `liveness.dart` inverts yaw **only on iOS** (`livenessInvertYaw == platform == iOS`) and iOS is correct — so the **Android** yaw-sign / input-image rotation is the wrong one. The Android-only symptom **rules out RTL arrow-mirroring** (that would break iOS too). Likely fix: correct the Android branch (yaw sign and/or the InputImage rotation fed to ML Kit in `identity_verification_screen` on Android), and **detect the device/platform when capturing** so the "turn right / turn left" prompt+arrow match the physical turn. Prior fixes to this exact swap: **D-684**, **PR-103**; a face regression rode in via a router change **D-666** — change carefully.
- **Verification required (app CLAUDE.md §13.3):** reproduce on the **Android tablet** (TXZ-W09) — prompt text + arrow + accepted turn must all agree — then confirm iOS unaffected. A green golden did NOT catch D-666 → needs the flow test + on-device render.
- **Proposed next step:** trace `identity_verification_screen` prompt/arrow rendering + the yaw path on-device, isolate which of the 3 hypotheses holds, then a §11 fix plan + a regression test that pins the direction. **No code yet.**
- **Status:** ☐ Open

---

## Topic 4 — Profiles & attendee discovery

**Reported:** 2026-07-20 (owner batch)

| # | Type | Priority | Area | Title | Status |
|---|------|----------|------|-------|--------|
| 13 | ✨ Update | P1 | App / CP | "Meet People Like You" — filter (Sponsors + Speakers only), Other-type opt-in, result nav | ✅ Done |
| 14 | ✨ Update | P2 | App | User can edit interests from their profile (not only at sign-up) | ☐ Open |
| 15 | ❓ Design | P3 | Domain | Speakers / Companies / Booths / others get a profile for extra data | ☐ Open |

### [#13] "Meet People Like You" — discovery filter, opt-in, navigation
- **Type:** ✨ Update · **Priority:** P1 · **Area:** App + CP
- **Existing:** `RecommendationService`, `UserProfile.ShowInMeetLikeYou` (D-736, default true), interests M-to-M (`UserProfileInterests`).
- **Owner requirements (EN + AR consolidated):**
  1. **Filter — who appears:** ONLY **Sponsors** (shown as the exhibition **company name**) + **Speakers** ("رعاة (اسم الشركة الموجودة في المعرض) + متحدثين"). The two visitor categories **عادي (Normal) + VIP must NOT appear** when pressing "قابل أشخاص مثلك". ("التأكد من عدم ظهور الفئتين عادي - VIP").
  2. **Manageable from the Control Panel** — an admin controls this filter ("مع إمكانية إدارتها من لوحة التحكم").
  3. **Other profile type** gets a **checkbox** for whether the user shows in "Meet People Like You" ("الأخرى - نضيف checkbox هل يظهر على قابل أشخاص مثلك") — surface the existing `ShowInMeetLikeYou` flag on the Interest/profile page; make it apply to the Other type, not visitors-only.
  4. **Show exactly the person's data** ("وبالضبط اعرض بياناته").
  5. **Result navigation:** tapping a matched **viewer** → open **gallery**; tapping a **speaker** → navigate to **speaker details**.
- **Open questions:** (a) "viewer → gallery" — which gallery (the person's media, or the general gallery)? (b) exact CP control — a toggle list of which kinds/categories are discoverable? (c) which speaker fields to show under the `Speaker.AllowsDataSharing` consent gate?
- **Related:** prior "meet-people HELD" note; migration D-736.
- **As-built (Build #13):** the `meet` screen (`/meet`) was reworked from the AI "% match" recommender into a curated + opt-in **partner directory**. New endpoint `GET /api/v1/app/networking/partner-directory` (`RequireApprovedAccount`, no permission code) returns the deduped union of curated **Speakers** (tap → speaker profile), **Sponsors** (tap → sponsor detail), **Booth companies** (tap → exhibitor detail) and opted-in **"Other"-type** accounts (kind=person, non-tappable). Normal/VIP visitors never appear; a person who is also a curated speaker appears once, as the speaker. CP control = `OrganizationProfile.PartnerDirectoryEnabled` (additive migration `AddPartnerDirectoryEnabled`, default true) surfaced on the CP Site-Settings page (`/admin/site-settings`, `Configuration.Edit` - no new permission) and on the public `GET /app/site-settings` payload; off → empty directory + hidden Home tile. The Other-type opt-in is a checkbox on the My-interests edit screen (shown only when `!isForVisitor`) toggling `UserProfile.ShowInMeetLikeYou`. Docs updated: `docs/pages/cp/site-settings.md` (new), `docs/pages/mobile/meet-people/README.md`, `docs/tests/e2e/{cp-site-settings,mobile-meet-people}.md`, `PAGE-INDEX.md`.
- **Status:** ✅ Done - shipped on `feat/meet-people-partner-directory`. Owner open questions (a/b/c above) resolved in the as-built: (a) tapping resolves per kind, and a person shows their own data with no separate gallery screen; (b) CP control is a single on/off toggle on Site-Settings (not a per-kind list); (c) speakers surface the same public summary the `/app/speakers` list uses.

### [#14] Edit interests from profile
- **Type:** ✨ Update · **Priority:** P2 · **Area:** App
- **Requirement:** the user can change their interests from their profile — today interests are picked at sign-up (`sign_up_interests_screen`).
- **Current:** the `UpsertUserProfileRequest` path already carries interests (validator requires 1–10), so this is likely **surfacing an edit-interests UI** in the profile rather than new backend. Verify the profile-edit screen exposes it.
- **Status:** ☐ Open

### [#15] Speakers / Companies / Booths / others — profile for extra data
- **Type:** ❓ Design · **Priority:** P3 · **Area:** Domain
- **Requirement (owner):** Speakers, companies, booths, and "all others" can each have a **profile** — in addition to their current table — to save **extra data**.
- **Existing partial mechanism:** the shared **`Contact` directory** (D-260 / FDS-014) already gives Speaker (and others) a linked record via `ContactId` for logo / name / phones / social / website / location / country — i.e. an "extra data alongside the current table" pattern already exists.
- **Open questions (design, before any plan):** (a) extend the existing Contact-directory link to Company / Booth / etc., or introduce a new generic Profile entity? (b) what "extra data" beyond Contact fields? (c) does "profile" here mean joining the ProfileType/badge system (ties to bulk-badges #10)? (d) schema impact — the App schema is under freeze; needs an owner-approved plan + respects the Identity/App split.
- **Status:** ☐ Open — needs a design decision

---

## Topic 5 — Flutter clean-code / tokenization sweep (per feature)

**Reported:** 2026-07-20 (owner batch) · **one tracked item, per-feature checklist below.**

### [#16] App tokenization + widget-extraction sweep
- **Type:** 🧹 Chore · **Priority:** P2 · **Area:** App (`src/Mobile/simf_app`)
- **Shared rules (apply everywhere):**
  - **Numbers → tokens:** replace hardcoded numbers with `SimfTokens` spacing (e.g. `16` → `SimfTokens.space4`).
  - **Text styles → `app_style.dart`:** replace inline `Text` + `TextStyle` with the custom styles.
  - **Colors → tokens:** replace `Colors.white` (and other hardcoded colors) with `SimfTokens.surface` (create the token if missing).
  - **Assets → `app_assets.dart`:** hoist hardcoded asset paths (icons/images/videos), then reference from there.
  - **Private widgets → files:** extract named `_Private` widgets into their own files under the feature's `widgets/`.
- **⚠ MUST RECONCILE BEFORE STARTING (flag, not a blocker):** `app_style.dart` **does not exist** today; `app/theme/tokens.dart` (`SimfTokens`) and `app/theme/app_assets.dart` **do**. App CLAUDE.md §5.1 says text styles are **named `SimfTokens` styles** and the font is set once in the theme. Creating a rival `app_style.dart` may violate §5.1/§13.4. **Confirm:** create `app_style.dart` as written, OR route "custom text style" through existing `SimfTokens` styles. Same question for "SimfTokens.surface" — confirm the exact token name(s).
- **Process:** per app CLAUDE.md §12 — **one feature at a time**, plan→approve→apply, behavior-preserving, re-lock goldens/tests per touched screen. Not one giant diff.

**Per-feature checklist** (T=text→app_style · C=Colors.white→SimfTokens.surface · A=hoist asset(s) · X=extract private widget(s) · file names as owner specified; `[sic]` = probable typo to correct at build):

| Feature | Fixes | Extract → file / special note | Status |
|---|---|---|---|
| About | numbers | Rename `_Card`→`AboutCard`, `_CardHeading`→`AboutCardHeading` (widgets/about_card.dart) | ☐ |
| Ai_Summary | T, C | — | ☐ |
| Archive | T, C | `_ArchiveBody`→`archive_body.dart` [sic achive], `_GalleryTtle`→`archive_gallery_title.dart`, `_PastSpeakerCard`→`archive_gallery_tile.dart` | ☐ |
| Badge | T, C | replace `Colors.white`+other hardcoded colors with tokens; create tokens if missing | ☐ |
| Booths | T, C, A | A: `assets/icons/nav_location.svg`. X: `_HallBox`→`booths_hall_box.dart`, `_contactBox`→`booths_contact_box.dart` | ☐ |
| Chatbot | T, C | — | ☐ |
| Contact us | T, C | X: `_infoRow`→`contact_us_info_row.dart` [sic jnfo], `_socialButton`→`contact_us_social_button.dart`. Replace `_Field` with `naviFormField` | ☐ |
| Contacts | T | X: `_ChannelRow`→`contacts_channel_row.dart`, `_ErrorSatate`→`contacts_error_state.dart`, `_contacPreviewSheet`→`contacts_preview_sheet.dart` | ☐ |
| Content | T, C | — | ☐ |
| delegations | T, C, A | A: `assets/icons/nav_faq.svg`, `assets/icons/nav_contact.svg`. X: `_DelegationCard`→`delegations_card.dart`, `_SectionHeader`→`delegations_section_header.dart` | ☐ |
| exhibition | T, C, A | A: `assets/icons/auth_globe.svg`, `assets/icons/ic_back.svg`. X: `_LinkRow`→`exhibition_link_row.dart`, `_IdentityCard`→`exhibition_identity_card.dart` [sic cartd] | ☐ |
| exhibitor | — | X: `_Centered`→`exhibitor_centered.dart` [sic centerd] | ☐ |
| Faq | — | X: `_FaqTile`→`faq_tile.dart` [owner wrote faq_title.dart] | ☐ |
| Feedback | T, C | Replace `TextField` with `naviFormField` | ☐ |
| Forum guide | C, A | A: `assets/icons/ic_caret_left.svg` | ☐ |
| Gallery | T, C | X: `_mediaTile`→`gallery_media_tile.dart`, `_PlaceholderBox`→`gallery_placeholder_box.dart` [sic pleace_holder] | ☐ |
| Gates | T, C | X: `_withPendingBanner`→`gates_with_pending_banner.dart`, `_row`→`gates_row.dart` [sic .dat], `_label`→`gates_label.dart` | ☐ |
| Guest | T | bg color must match all-screens bg; back icon must match the other back icons | ☐ |
| Home | T, C, A | A: `assets/icons/ic_caret_left.svg`, `assets/images/discover_hero.jpg`. X: `_HightlighSlide`→`home_high_light_slide.dart`, `_SocialButton`→`home_social_button.dart` | ☐ |
| Live | T, C | X: `_TogglePill`→`live_toggle_pill.dart`, `_CaptionStrip`→`live_caption_strip.dart`, `_MessageSurface`→`live_message_surface.dart` | ☐ |
| Media partners | T, C | — | ☐ |
| Meet | T, C | X: `_topicChip`→`meet_topic_chip.dart` | ☐ |
| Meetings | T, C, A | A: `assets/icons/chevron_left.svg`, `request_new.svg`, `request_log.svg` | ☐ |
| Moderation | T, C | X: `_ActionButton`→`moderation_action_button.dart`, `_Chip`→`moderation_chip.dart` | ☐ |
| More | T, C, A | A: `assets/icons/ic_caret_left.svg` | ☐ |
| Myarea | T, C | `Colors.white70` → add to `SimfTokens` then use | ☐ |
| News | T, C | `Colors.white70` → surface | ☐ |
| Notifications | T, C | — | ☐ |
| Onboarding | T, C, A | A: hardcoded **video** paths | ☐ |
| Question | T, C | file `sned_question_content.dart` [sic send] | ☐ |
| Registration | T, C | — | ☐ |
| Requests | T, A | `Colors.white` + `Colors.transparent` → tokens. A: `request_log.svg`, `request_new.svg`, `chevron_left.svg` | ☐ |
| Sessions | T, A | `Colors.white` → tokens. A: `assets/icons/ic_back.svg` | ☐ |
| Speakers | T, A | `Colors.white` + `Colors.transparent` → tokens. A: `speaker_placeholder.svg`, `ic_back.svg`, `ic_caret_left.svg`. Replace custom text field with `SimfLabeledTextField` (see `sign_up_visitor_screen.dart`) | ☐ |
| Splash | T, C | — | ☐ |
| Sponsors | C, A | A: `assets/icons/ic_back.svg` | ☐ |
| Staff | T, C | — | ☐ |
| VenueMap | T, C | (owner: "VeneMap") | ☐ |

- **Status:** ☐ Open — sweep not started; reconcile `app_style.dart` question first

---

## Topic 6 — Session join & seat mechanism (agreed logic)

**Agreed with owner 2026-07-20.**

| # | Type | Priority | Area | Title | Status |
|---|------|----------|------|-------|--------|
| 17 | ✨ Update | P1 | App / API | Session join + seat mechanism — two cases, no approval, 3-min auto-cancel, check-in/out | ☐ Open |
| 18 | 🐞 Bug | P1 | App | "الانضمام إلى الجلسة" (Join session) button does nothing | ☐ Open |

### [#17] Session join + seat mechanism (agreed)
- **Type:** ✨ Update · **Priority:** P1 · **Area:** App + API
- **Two cases (driven by how the session's seating is managed — maps to `SeatSelectionMode`):**
  - **Case 1 — all sessions in one hall (open / no specific seat):** button reads **"سجل لحضور الجلسة"** (Register to attend). On tap → alert:
    > تم تسجيلك لحضور هذه الجلسة بنجاح. هذا التسجيل لا يعني حجز مقعد أو ضمان الدخول للجلسة، سيتم تأكيد دخولك عند تسجيل الدخول للجلسة
    (Registered — this is NOT a seat reservation or guaranteed entry; entry confirmed at check-in.)
  - **Case 2 — each session in a different hall (assigned seat):** button reads **"الانضمام إلى الجلسة"** (Join). On tap → **seat-selection screen** showing available seats → user picks a seat → **"حجز"** (Reserve) → seat reserved → alert:
    > تم حجز المقعد بنجاح. سيتم إلغاء الحجز في حالة عدم تسجيل الدخول للجلسة قبل 3 دقائق قبل بدء الجلسة لإتاحة المقعد لأشخاص آخرين
    (Seat reserved — cancelled if you don't check in by 3 minutes before start, freeing the seat.)
- **Summary rules (owner):** **no approval** for attendance registration; **no approval** for seat reservation (see #6); two seat cases = **assigned seats / by seat-count** per the session's reservation-management type; **auto-cancel the seat 3 minutes before start** if not checked in.
- **Session check-in AND check-out** required ("تسجيل دخول وكذلك تسجيل خروج من الجلسات").
- **Maps to existing model:** `SeatSelectionMode { AssignedSeat, OpenSeating }` (Hall + per-session override) = the two cases; `SeatReservation.ExpiresUtc` = the auto-cancel guard (but the window must become **"3 min before StartUtc"**, not "created + hold"); session attendance infra partly exists (`HallAttendance` / `HallAttendanceService` / `HallArrivalsConsole` / `GateScan`) — reuse for check-in/out.
- **Deltas to build:** remove approval (#6); wire the two button variants + copy above; seat-picker "حجز" path; expiry worker to release at start-3min; session-level check-in + **check-out**.
- **Status:** ☐ Open

### [#18] "Join session" button not working
- **Type:** 🐞 Bug · **Priority:** P1 · **Area:** App / Sessions
- **Report (owner + screenshot "تفاصيل الجلسة"):** on session detail, the **"الانضمام إلى الجلسة"** button does nothing on tap.
- **Note:** likely resolves alongside #17 (the join flow is being redefined) — but verify the current button isn't dead independently of the redesign (missing onTap / disabled-state / nav route).
- **Status:** ☐ Open

---

## Topic 7 — App bugs & UX items

**Reported:** 2026-07-20 (owner batch + screenshots)

| # | Type | Priority | Area | Title | Status |
|---|------|----------|------|-------|--------|
| 19 | 🐞 Bug | P2 | App / Auth | Login-as-Guest label (not "Visitor") + Arabic translation wrong | ☐ Open |
| 20 | ✨ Update | P2 | App | Agenda accessible without auth + rename program icon label to "الأجندة" | ☐ Open |
| 21 | 🐞 Bug | P1 | App / Contacts | "مشاركة جهة اتصال" (Share contact) button does nothing | ☐ Open |
| 22 | ✨ Update | P2 | App / Sign-up | `sign_up_visitor_screen` category section — update UI | ☐ Open |
| 23 | ✨ Update | P2 | App / Sessions | Session summary logic update (Home Sessions + Summary buttons stay as-is) | ☐ Open |
| 24 | ✨ Update | P1 | App / CP / Identity | User can update email (also fixes CP new-account typos) | ☐ Open |
| 25 | 🐞 Bug | P1 | App / Auth | Activate Face biometric (Face ID) — cross-ref #9 | ☐ Open |
| 26 | 🐞 Bug | P1 | App / Sign-up | Face-capture device-type detection + correct turn image — cross-ref #12 | ☐ Open |
| 27 | 🐞 Bug | P2 | App / Live | Video > 30 min: session-extension alert must NOT appear | ☐ Open |
| 34 | 🐞 Bug | P2 | App / Speakers | Speaker job title shows English regardless of app language | ☐ Open |

### [#19] Login-as-Guest label + Arabic translation
- **Requirement:** the "login as visitor" action should say **Guest** (not Visitor). English is fine; the **Arabic translation is wrong**. Fix the label + the `ar` string in `AppL10n`/resx. Relates to guest mode (`effectiveAppRole`, Home `758-2910`).
- **Status:** ☐ Open

### [#20] Agenda without auth + rename to "الأجندة"
- **Requirement:** (a) the Agenda must be **viewable without login** ("Agenda can access without auth") — relates to the login-gate (D-576/D-577) and #11; (b) change the label under the program icon to **"الأجندة"** ("تعديل الاسم الموجود أسفل الأيقونة ... ليكون الأجندة").
- **Status:** ☐ Open

### [#21] "Share contact" button not working
- **Report (owner + screenshots):** on the profile ("الملف الشخصي") the **"مشاركة جهة اتصال"** button does nothing; the target is the **"شارك جهة اتصالي"** QR / vCard share screen. Trace the button's onTap/nav and the share-contact flow (`Contacts` / `SavedContact` / `VisitorShareToken`).
- **Status:** ☐ Open

### [#22] Sign-up category section UI
- **Requirement:** update the UI of the **category** section on `sign_up_visitor_screen`. Relates to interests (#14). Needs the target design — confirm the Figma node / intended layout.
- **Status:** ☐ Open

### [#23] Session summary logic update
- **Requirement:** "Session summary update logic." **And (owner F8):** the **Sessions button on the home screen and the Session-summary button stay as they are** ("زر الجلسات ... وزر ملخص الجلسات تبقى على ما هي عليه").
- **⚠ Reconcile with #11:** #11 specified Home = future + type=Session and Summary = past-only; F8 says keep those buttons as-is. Confirm which wins, and what exactly "summary logic" should change (the subtitle→AI→committee→publish pipeline?).
- **Status:** ☐ Open — needs clarification

### [#24] User can update email
- **Requirement:** allow editing the email — specifically to fix mistakes made when creating a new account **via the Control Panel** ("تجنبا للأخطاء ... عند إضافة حساب جديد عن طريق لوحة التحكم").
- **Note:** email lives on the **Identity** DB (frozen, D-110). Email is already a column, so likely no schema change — but needs a change-email endpoint + **uniqueness check** + **re-verification** (OTP) + CP action. Flag for the plan.
- **Status:** ☐ Open

### [#25] Activate Face biometric (Face ID) — see #9
- **Requirement:** "التأكد من تفعيل بصمة الوجه" / "Face ID is not working." Same as **#9** (biometric login broken). Track the fix under #9; this row is the owner's explicit call-out.
- **Status:** ☐ Open

### [#26] Face-capture device-type + correct turn image — see #12
- **Requirement:** detect device type on selfie capture and show the correct "turn right / turn left" prompt image. Same as **#12** (Android direction). Track the fix under #12.
- **Status:** ☐ Open

### [#27] Video > 30 min — no session-extension alert
- **Requirement:** add a video **longer than 30 minutes** to test the session and ensure the **session-extension alert does not appear** ("إضافة فيديو أكثر من 30 دقيقة ... وضمان عدم ظهور رسالة التنبيه الخاصة بتمديد الجلسة"). Find the >30-min / extension-alert threshold (likely in the live/recording player or a session-duration guard) and confirm behaviour with a long video.
- **Status:** ☐ Open

### [#34] Speaker job title shows English in both languages
- **Report (owner + Speakers screenshot):** the job title / rank under each speaker name renders in **English even when the app is Arabic**.
- **Likely cause:** `Speaker` has `Rank` + `RankArabic`; the app is showing `Rank` unconditionally, or `RankArabic` is empty in the data. Verify the app reads the locale-correct field AND that `RankArabic` is populated (data vs. code).
- **Status:** ☐ Open

---

## Topic 8 — Control Panel management & delivery (engineer notes)

**Source:** owner note-set "الملاحظات التي سوف يتم تزويد المهندس مهند بها" (2026-07-20).

| # | Type | Priority | Area | Title | Status |
|---|------|----------|------|-------|--------|
| 28 | ✨ Update | P1 | CP | Meeting/speaker date filter = forum dates only + all CP times in Saudi time | ☐ Open |
| 29 | ✨ Feature | P1 | CP / App | Workshop management in CP (title/time/count/check-in-out); app shows title+time only | ☐ Open |
| 30 | ✨ Feature | P1 | CP / App | B2B / B2G bilateral-meeting management in CP + activate VIP↔speaker "send request" | ☐ Open |
| 31 | 🗂 Data | P1 | CP / Content | Feed the forum program data | ☐ Open |
| 32 | ❓ Verify | P2 | CP / Security | CP permissions = Admin / Security / PR / Scientific | ☐ Open |
| 33 | 📄 Docs | P1 | CP | Deliver CP user manual (owner due date 19-07-2026 — already past) | ☐ Open |

### [#28] Meeting/speaker date filter + Saudi time in CP
- **Requirement:** (a) in the CP, the speaker-meeting scheduling must filter **by the forum dates only** ("فلتر ... بمقابلة المتحدثين بتاريخ الملتقى فقط") — the date picker cannot pick dates outside the event; (b) **all CP times in Saudi time** ("التأكد من أن تكون الأوقات في لوحة التحكم بالتوقيت السعودي") — see the timezone decision **#8**.
- **Status:** ☐ Open

### [#29] Workshop management in CP
- **Requirement:** manage **workshops** from the CP — **title, time, allowed count**, and **check-in / check-out**. In the **app**, a workshop shows **only its title + time** ("يقتصر بعرض عنوان ورشة العمل والوقت").
- **Note:** workshops today are `Session.Type == Workshop`. Confirm whether "allowed count" + check-in/out reuse the session capacity + `HallAttendance`, or workshops need their own management surface.
- **Status:** ☐ Open

### [#30] Bilateral meetings (B2B / B2G) management + VIP↔speaker request
- **Requirement:** manage the **bilateral meetings (B2B - B2G)** from the CP; and **activate the "send request" button** in the **VIP↔speaker** bilateral meetings ("تفعيل زر إرسال الطلب في اللقاءات الثنائية الخاصة بـ VIP مع المتحدثين").
- **Maps to:** `BusinessMeeting`, `SpeakerMeetingRequest`, `DelegationMeetingRequest`, `MeetingActionToken`, `HallAvailabilityWindow`. Verify the CP management surface + the app send-request button wiring.
- **Status:** ☐ Open

### [#31] Feed the forum program data
- **Requirement:** populate the forum program content ("تغذية البيانات الخاصة ببرنامج الملتقى"). Overlaps day-images (#1) and hall seat-layouts (#5) — content/data task, not code.
- **Status:** ☐ Open

### [#32] CP permissions — four roles
- **Requirement:** CP permissions = **Admin**, **Security team**, **PR team**, **Scientific team**.
- **Verify vs existing:** SIMF already has a per-page/action permission system (roles-only, JWT-baked, `Administrator = "*"`). Check whether Security / PR / Scientific roles + their `BaselineRoles` mappings exist in `PermissionCatalog`, and gap-fill.
- **Status:** ☐ Open

### [#33] CP user manual delivery
- **Requirement:** deliver the **Control Panel user manual** by **Sunday 19-07-2026** ("بحد أقصى يوم الأحد الموافق 19-07-2026").
- **⚠ Date already past** (today is 2026-07-20) — confirm the real deadline (likely a typo for a later date). Docs deliverable, not code.
- **Status:** ☐ Open

---

## Topic 9 — Dynamic forum dates

**Reported:** 2026-07-20 (owner)

### [#40] Forum dates must be dynamic (not the fixed "23-25 November 2026")
- **Type:** ✨ Update · **Priority:** P1 · **Area:** cross-cutting (Backend seed + Website + App + CP)
- **Requirement (owner):** "The forum dates are fixed: 23 to 25 November 2026. This is not correct, must be changed dynamic."
- **Finding (verified — hardcoded "23-25 November 2026" display strings):**
  - `src/Backend/SIMF.Infrastructure/Identity/IdentitySeeder.cs:740-741` — seeded org-profile event-date label (EN + AR).
  - `src/Website/SIMF.Web/Resources/Strings.resx` (+ `.ar.resx`) — `Landing.Subnav.Date`, `Speakers.Band.Date`, `Landing.MetaDescription`.
  - `src/ControlPanel/SIMF.ControlPanel/Resources/Strings.resx` (+ `.ar.resx`) — `Admin.SpeakerAvailability.BadDateRange` (overlaps **#28** — the dynamic bound message replaces this static string).
  - `src/Mobile/simf_app/lib/app/localization/app_l10n.dart:689` — home edition label.
  - NOT this: `Website/.../Landing.razor.cs:51/56/61` are PAST editions (2019/2022/2024) — legitimately fixed history.
- **Dynamic source (exists):** `OrganizationProfile.EventStartDate/EventEndDate` (App-DB config, CP-editable, `OrganizationProfileMapper`) — currently a stale placeholder. Alternative: `ProgrammeDay` MIN/MAX (what #28 uses).
- **Open decision:** source = (a) OrganizationProfile config (recommended for display) vs (b) ProgrammeDay-derived. Confirm.
- **Fix plan (pending decision):** set the real event dates in the config; add a shared bilingual date-range formatter; replace every hardcoded resx/l10n/seed string with a render of the dynamic source; expose to app/website via the OrganizationProfile API. Build in Phase 2 after #28 lands (shared CP message).
- **Status:** ☐ Open — needs source decision (a/b)

---

## Topic 10 — Home screen (greeting + hero edition banner)

**Reported:** 2026-07-21 (owner batch + screenshot)

| # | Type | Priority | Area | Title | Status |
|---|------|----------|------|-------|--------|
| 42 | ✨ Update | P3 | App / Home | Greeting shows **first name only** + friendlier wording (`مرحبًا` + first name) | ☐ Open |
| 43 | ✨ Update | P2 | App / Home (+ Backend/CP) | Home **hero = live forum-edition banner** (title/theme/dates/location) + rotating image | ☐ Open |

### [#42] Home greeting — first name only + friendlier wording
- **Type:** ✨ Update · **Priority:** P3 · **Area:** App / Home
- **Report (owner + screenshot):** the home greeting reads `صباح الخير` (time-of-day) over the person's **full** name `هيفاء عبدالله ابراهيم العتيبي 👋`. Owner wants **the first name only** (`عرض الاسم الاول فقط`) and a friendlier greeting. **Owner chose `مرحبًا` + first name** (over `أهلاً`).
- **Current state (verified):**
  - `features/home/widgets/greeting_header.dart` — `nameLine` renders the **full** name + 👋; the greeting line calls `homeGreeting(l10n, now)` = time-of-day.
  - `features/home/home_greeting.dart` — `homeGreeting()` returns `greetingMorning` (`صباح الخير`) / `greetingEvening` (`مساء الخير`) by hour.
  - `features/home/widgets/visitor_home.dart` passes a now-dead `now` param down for the golden's fixed clock.
  - `app/localization/app_l10n.dart` — `greetingMorning` / `greetingEvening`.
- **Fix plan (done):** added `greetingWelcome` l10n (`مرحبًا` / `Welcome`); `greeting_header.dart` greeting → `l10n.greetingWelcome`, `nameLine` → **first token** (`name.trim().split(' ').first`); dropped the dead `now` seam + `homeGreeting` import (kept `homeGreeting()` as a tested utility); re-locked the signed-in golden (guest byte-identical) + updated 4 home tests + home README/E2E.
- **Owner decision (2026-07-21):** **accept first-token** rule. Known limitation accepted: a space-separated compound Arabic name (`عبد الله`) shows only `عبد`; joined names (`عبدالله`) render correctly.
- **Status:** ✅ Built (`39685d58`, `feat/app-home-greeting`) — `flutter analyze` 0 errors; `router_role_matrix` 8/8; `home_screen_test` +32 (only the **pre-existing** avatar-tap harness test stays red — fails on base too, unrelated); golden re-locked + visually confirmed `مرحبًا` + first name. PR pending owner confirm. On-device render pending.

### [#43] Home hero = live forum-edition banner + rotating image
- **Type:** ✨ Update · **Priority:** P2 · **Area:** App / Home (+ Backend / CP)
- **Report (owner + screenshot):** the hero currently shows a generic `اكتشف السعودية / تعال واكتشف جديدك المفضل` card. It should instead show the **forum edition**:
  - Title: `الملتقى البحري السعودي الدولي الرابع`
  - Theme / subtitle: `مستقبل أمن قاع البحار وسلاسل الإمداد في بيئة عالمية متغيرة`
  - `📅 23–25 نوفمبر 2026`
  - `📍 الرياض – المملكة العربية السعودية`
  - And **the image rotates** (`والصورة تتغير`).
- **Owner claim:** "all those data already come from the backend." → **verified TRUE at the API level, with one real app-side gap (dates dropped) + no rotating-image source.**
- **Finding (verified 2026-07-21):**
  - **Current hero is 100% hardcoded.** `DiscoverHeroBanner` ([home_banners.dart:99-163](../../src/Mobile/simf_app/lib/features/home/widgets/home_banners.dart)) is a single static `StatelessWidget`: title/subtitle are l10n literals (`discoverSection` `اكتشف السعودية` / `discoverBannerSubtitle` `تعال واكتشف جديدك المفضل`), image is bundled `assets/images/discover_hero.jpg`, tap opens News. Rendered only on signed-in home (`visitor_home.dart:61`). Nothing backend-driven.
  - **The API already serves the edition data.** `GET /api/v1/app/organization-profile` (`OrganizationProfileResponse`, D-495, anonymous + cached) returns `Title/TitleArabic`, `Name/NameArabic`, **`EventStartDate`/`EventEndDate` = real 2026-11-23..25** (corrected by D-755 seeder; a shared bilingual `EventDateRange` formatter already renders `23-25 نوفمبر 2026`), `LocationText/LocationTextArabic`, `Status`+`CurrentYear`, `LogoUrl`, `LiveStreamUrl`, social, aboutItems, details. The app already fetches + caches this app-wide via `orgProfileProvider` (warmed at splash).
  - **App-side gap #1 — dates dropped.** The Flutter `OrgProfile.fromJson` ([core/organization_profile/organization_profile.dart](../../src/Mobile/simf_app/lib/core/organization_profile/organization_profile.dart)) decodes title/location/status/year/slogan but **does NOT decode `eventStartDate`/`eventEndDate`** → the app can't render the real date range yet. Additive decode-only fix (D-219-safe) + a Dart `EventDateRange` (mirror the C# one; `core/utils/gregorian_month_names.dart` already exists).
  - **App-side gap #2 — hero ignores the profile.** Only `follow_us_section.dart` reads `orgProfileProvider` (social only). The hero must `ref.watch(orgProfileProvider)` and render title + theme + date-range + location.
  - **Theme field.** `مستقبل أمن قاع البحار وسلاسل الإمداد...` is currently seeded only as **Website** landing content-blocks + app l10n literals — NOT on the org-profile row the app reads. Natural home = `OrganizationProfile.Title/TitleArabic` (CP-editable) → needs the theme text entered in the CP (data step), then the hero renders it.
  - **Rotating image — no backend source.** A real auto-advancing carousel already exists (`HighlightsCarousel`: PageView + 4s Timer + dots) but it is fed by **news** images (`/app/news`). No dedicated hero-image collection exists.
- **Overlap with #40:** same date source (D-755 `EventStartDate/EventEndDate`). #40's app-side piece (render the dynamic date in the app) is **subsumed by this hero** — this is where the app finally surfaces the dynamic range.
- **Decisions (owner, 2026-07-21):**
  - **D-A — image source = REUSE the existing `Banner` feature + add CP upload.** A late audit found a fully-built, app-surfaced banner feature (`Banner` entity + CP `BannersList/AddEdit` CRUD + `Banners.*` permissions + public `GET /app/banners`), unused by the app. Reusing it **supersedes the earlier "new EditionHeroImage table" pick** and drops the new table, migration and freeze-lift entirely.
  - **D-B — theme field = `Title/TitleArabic`**; **D-C — edition name incl. `الرابع` = `Name/NameArabic`** (both CP-editable data entry; the hero renders whatever the profile holds).
- **As-built (2026-07-21; branch `feat/app-home-hero`, stacked on #42):**
  1. **Backend (code-only, NO migration/schema/freeze-lift):** `AssetCategory.Banner`(=8, append-only) wired into `AssetService.CategoryToService` + `OwnerIsActiveAsync` (public serve only while the banner is active AND within `[StartUtc,EndUtc]`, matching `/app/banners`) + `AssetPermissionRegistry` (gated by `Banners.View/Edit`) + the Media-Library owner-name resolver. `FileService.Banner`/`FileOwnerEntityType.Banner`/its `PublicImage` policy already existed. `8a76a03f` (+ review fix `843c72c3`).
  2. **CP:** `SimfImageUpload` (`Category=Banner`) added to `BannersAddEdit` (edit-only) → `POST /admin/assets/Banner/{id}/image`, reusing `Banners.Edit` (no new page/permission/nav). `204415ae`.
  3. **App:** new `features/banners/data` (`PublicBannerItem` + `bannersProvider`); `OrgProfile` decodes `eventStartDate`/`eventEndDate` (append-only) + `eventDateRange()`; new `core/utils/event_date_range.dart` (mirrors C# `EventDateRange`); new `HomeHeroBanner` (rotating PageView + 4s timer + shared `CarouselDots`) overlaying name/theme/date-range/location, falling back to the static discover photo when empty (home golden byte-identical). `fc0d88b0` + `2dbfc8df` + `843c72c3`.
  4. **Data/CP (owner):** enter the theme into `Title/TitleArabic`, the full name incl. `الرابع` into `Name/NameArabic`, and upload the hero images in the Banners CP page (dates/location already correct via D-755).
  5. **DoD:** home README + `mobile-home.md` + `cp-admin-banners.md` E2E updated; unit/widget tests (event-date, banner model, hero rotation/overlay) + backend asset tests. DECISIONS_LOG left to the owner (owner edits it live).
- **Overlap with #40:** same date source (D-755); this hero is where the app finally renders the dynamic range.
- **Verify:** `dotnet build -c Release` 0/0; asset guard + endpoint tests 32/32 → 17/17; CP Banners + nav-permission 9/9; `flutter analyze` 0 errors; home tests `+34`/hero `5/5`/`router_role_matrix` 8/8 (only the **pre-existing** avatar-tap harness test red); home golden byte-identical.
- **Status:** ✅ Built (`feat/app-home-hero`, 5 commits) — **no new table, no migration, no freeze-lift** (reused Banner). PR pending owner confirm; on-device render pending.

---
```
(New topics/items append below this line.)
```
