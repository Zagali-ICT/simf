# SIMF — Executable E2E / QA Test Plan

Owner-requested durable artefact (2026-07-26). The point of this document: after **any** change, hand
it to an agent and say *"run this plan"* — with no further explanation. Everything needed to execute
is here, including the test data.

> Status legend used throughout: **PASS** verified this round · **BLOCKED** cannot run until a listed
> defect or seed gap is fixed · **N/A** deferred feature.

---

## 1. How to run

### 1.1 Stack
Everything runs on a **throwaway local QA stack** — never production.

| Service | URL | Notes |
|---------|-----|-------|
| API | `http://localhost:5275` | `/health` must return `Healthy` |
| Control Panel | `http://localhost:5278` | Blazor Server |
| Website | `http://localhost:5280` | Blazor SSR |
| SMTP capture sink | `127.0.0.1:2525` | `smtp_sink.py` — appends every message to `mail.log`; this is how OTPs and notification emails are read |
| Databases | `(localdb)\MSSQLLocalDB` → `SIMF_QA_Identity`, `SIMF_QA_App` | throwaway; safe to drop and re-seed |

Start the sink **before** the API, or credential emails fail with `SocketException 10061`.

### 1.2 Driving the Control Panel / Website
Chrome DevTools MCP. Element sweeps enumerate `button / a[href] / input / select / img` and assert
accessible name, enabled-or-correctly-gated state, and no broken images (`SimfImageThumb --broken`,
`naturalWidth === 0`, plus a network check for 404 assets).

### 1.3 Driving the Flutter app on the tablet
The HUAWEI TXZ-W09 returns a **black frame** for `screencap` on the Flutter surface, so visual
assertions use the **rendered semantics tree** instead — which is strictly better for element sweeps
because it exposes every node's label, clickability, enabled state and bounds.

```
adb reverse tcp:5275 tcp:5275      # device localhost -> QA API
adb reverse tcp:5280 tcp:5280
python uidrv.py sweep <name>        # snapshot + inventory JSON/XML into app-sweeps/
python uidrv.py dump                # human-readable control list
python walk.py steps-<file>.txt     # scripted multi-screen walk
```

Helper scripts live in the QA evidence folder: `uidrv.py`, `walk.py`, `role_signin.py`,
`complete_demo.py`, `signin_device.py`, `smtp_sink.py`, `seed_pending.py`, `get_otp.py`.

**Build the APK from the same commit as the running API.** A `main`-built APK against a
feature-branch API produced a false "session times are epoch" result (see BUG-011). Build with:

```
flutter build apk --debug --dart-define=SIMF_API_BASE=http://localhost:5275/api/v1 --dart-define=SIMF_BUILD=dev
adb uninstall com.simrsnf.simf       # required if the installed build has a higher versionCode
adb uninstall dod.simf.visitor_app   # once, on any device carrying a pre-2026-08-22 build
adb uninstall com.example.simf_app   # once, on any device still carrying a pre-D-867 build
adb install -r <apk>
```

`adb shell input text` is **ASCII-only** — it cannot type Arabic. Arabic-only fields (e.g. "Full name
(Arabic)", which filters to `[ء-ي\s]`) must be filled by hand on the device, or the case driven
through the API instead. This is a harness limit, not a defect.

---

## 2. Test data appendix

> Passwords are never written down. The seeded demo accounts share one password held in the
> **`SIMF_Seed__DemoPassword`** environment variable (User scope). The QA visitor's password is held
> with the QA evidence, not in this document.

### 2.1 Accounts

| Email | App role | State | Purpose / notes |
|-------|----------|-------|-----------------|
| `admin@simf.local` | — (CP Admin) | Approved | Control-Panel admin. `UserType.Admin` |
| `staff@simf.local` | **Staff** | Approved | Gate scanner + walk-in registration desk |
| `moderator@simf.local` | **Moderator** | Approved | Session question moderation |
| `exhibitor@simf.local` | **Exhibitor** | Approved | Badge scan → My Visitors |
| `visitor@simf.local` | Visitor | Approved | Ordinary attendee |
| `vip@simf.local` | Visitor | Approved | VIP profile type |
| `vvip@simf.local` | Visitor | Approved | VVIP profile type |
| `media@simf.local` / `sponsor@simf.local` | Visitor | Approved | Media / Sponsor profile types |
| `qa.pend3.0726@zagali-test.local` | Visitor | **Approved**, badge `FF3W2RXA3BFX` | The main end-to-end test visitor (created through the real sign-up + approval flow) |
| `qa.reject.0726@zagali-test.local` | Visitor | **Rejected** | Rejection-path fixture |
| `qa.walkin.0726@zagali-test.local` | Visitor | **PendingApproval**, no badge | Created via the staff walk-in desk |

**Before using any demo account in the app** it must satisfy the server's `profileComplete` rule
(names + ≥1 interest + ID document + face photo if male) or the app parks it on "Create profile".
Run `python complete_demo.py <email>`; for `moderator@` and `exhibitor@` an **interest** must also be
added (see BUG-022).

### 2.2 Identifiers

| Thing | Value |
|-------|-------|
| ProfileType `Normal` | `C90DCA1C-CE11-468E-8334-B55917BD71C3` |
| Organisation "Saudi Ports Authority (Mawani)" | `A2D84555-C33C-45EE-BF36-03A70AF9307E` |
| Organisation "Saudi Arabian Military Industries (SAMI)" | `AADA2C33-4E7C-407D-B9DE-07AEC7B624F0` |
| An interest (for profile upserts) | `1C5FDD65-86F0-4581-9487-8A5373AC66F6` |
| Session `D1-01` "الاستقبال والتسجيل" | `7efdfd03-8c13-4da9-9d8a-8b44eefc7476`, 2026-11-23 07:00→08:15 +03:00 |
| Test visitor badge QR | `FF3W2RXA3BFX` |

### 2.3 Valid sample values

| Field | Valid | Invalid (for negative cases) |
|-------|-------|------------------------------|
| Saudi national ID | `1122334459`, `1000000016`, `1000000024` — 10 digits, starts with `1`, **passes Luhn** | `1122334455` (right shape, fails Luhn) |
| Iqama | `2` + 9 digits, passes Luhn | any Luhn failure |
| Passport | 6–9 alphanumerics | `ABC` (too short) |
| Arabic name | `زائر ميداني` — Arabic letters + spaces, **≥2 parts** | `منسّق تجريبي` (contains a shadda → rejected, BUG-021); `زائر` (one part) |
| English name | `QA Walkin Visitor` — Latin + spaces, ≥2 parts | `QA1` |

### 2.4 Seeded content (as verified)

Speakers 32 · Booths 6 · Media partners 3 · News 1 · Programme days 3 (23/24/25 Nov 2026) ·
Sessions codes `D1-01…` · Halls 1 · Banners 0.

### 2.5 Configuration that is **NOT** seeded — seed it before the operational suites

`Gates = 0`, `GateAssignments = 0`, `GateProfileTypeAllow = 0`, `SessionModerators = 0`,
`HallSeatLayouts = 0`, `SeatReservations = 0`.

Consequence: the gate scanner, the moderation desk and the seat picker **cannot be exercised** on a
fresh QA database — they correctly show "not assigned to any gate", "لست محاوِرًا لهذه الجلسة" and an
empty seat map respectively. See **BUG-023**. Any full run must first create: a gate + a gate
assignment for `staff@simf.local`, a session-moderator assignment for `moderator@simf.local`, and a
hall seat layout.

---

## 3. Known-blocked cases

A runner should expect these to fail and must **not** re-report them as new defects.

| Blocked area | Blocking defect |
|---|---|
| Gate scan end-to-end | **BUG-018** (CP picker offers only Admin accounts) + **BUG-023** (no gates seeded) |
| Moderation desk actions | **BUG-023** (no `SessionModerators` row) |
| Seat picker / seat map | **BUG-023** (no `HallSeatLayout`) |
| "Book a seat" hub from Profile | **BUG-016** (route shadowed by `/sessions/:sessionId`) |
| Any demo account's first app launch | **BUG-022** (accounts start `profileComplete = false`) |
| Every logo / speaker photo image assertion | **BUG-001** (seeded asset GUIDs have no stored bytes → 404) |
| Exhibitor "email the lead" | **BUG-024** (not implemented) |
| Website sign-in / account journeys | **BUG-008** (owner directive: the area is to be removed) |

**Added by the §6.5–6.15 authoring pass (2026-07-26).** Each was confirmed by reading the
source on `origin/main` (`707d0ee6`); every one has a case below that is expected to FAIL until
the capability is built. Do not re-report them.

| Blocked area | Blocking defect | Verified in |
|---|---|---|
| Seat **type** (VVIP / VIP / Normal) anywhere | **BUG-028** — a seat has no type. `SeatReservation` carries `RowLabel` + `SeatNumber` + `Kind`; `HallSeatLayout` carries `RowLabels` + `SeatsPerRow` + `SeatCounts`. "VVIP" can only be typed as a **row label** | `SIMF.Domain/SeatReservations/SeatReservation.cs`, `HallSeatLayout.cs` |
| "A VVIP seat is reserved by default when the session's seats are defined" | **BUG-029** — `SeatReservationService.SetLayoutAsync` writes rows + counts only; it creates no `AdminReservedRow` rows. The admin must block the row by hand afterwards | `SIMF.Infrastructure/SeatReservations/SeatReservationService.cs:411` |
| Seat eligibility by profile type (VIP → VIP seat, others → non-VIP) | **BUG-030** — `ReserveAsync` / `ReserveRandomAsync` / `JoinOpenSeatingAsync` never read the caller's `UserProfile.ProfileTypeId`. Any approved account can take any free seat, including one in a row labelled `VIP` | `SeatReservationService.cs:128`, `:211`, `:260` |
| Per-seat free-text admin hint ("هذا المقعد مخصص للوزير") | **BUG-031** — no hint/note column exists on `SeatReservation` or `HallSeatLayout`, and `/admin/sessions/seat-plans` has no such input | `SeatReservation.cs`, `SessionSeatPlan.razor` |
| Staff tablet **seating-assistant** screen (scan a badge → find that guest's seat; tap a seat → who holds it) | **BUG-032** — `lib/features/staff/` contains only `register_visitor_screen.dart`; there is no seating screen and no staff-facing seat endpoint | `src/Mobile/simf_app/lib/features/staff/` |
| Logo **fit-to-box** | **BUG-033** — every logo surface renders `BoxFit.cover` (crop-to-fill), not `BoxFit.contain` | `sponsor_logo.dart:45`, `entity_logo_image.dart:71`, `partner_card.dart:76`, `booth_company_header.dart:156`, `simf_identity_cell.dart:167` |
| Logo **tap → full size** | **BUG-034** — no logo is wrapped in a tap target and the app has exactly one `InteractiveViewer` (the venue map). No full-screen image viewer exists | grep `InteractiveViewer` → `venuemap/venue_map_screen.dart:270` only |
| Editing the phone number on an existing profile | **BUG-035** — there is no profile-edit surface. `/my-area` renders a read-only identity card; `/sign-up/visitor` does **not** prefill (it only calls `getProfileTypes`), so re-submitting it would overwrite the whole profile; the CP shows `SaudiMobile` read-only on the view pages and only sets it at walk-in creation | `my_area_screen.dart`, `sign_up_visitor_screen.dart:164–199`, `VisitorsViewDelete.razor:51` |
| Changing a seat in place ("change seat") | **BUG-036** — once a reservation exists the session page replaces the Join CTA with the seat card; the only route to a different seat is cancel → re-book, and `ReserveAsync` 409s `SEAT_ALREADY_OWNED_BY_SESSION` while the old hold lives | `session_detail_body.dart:137–162`, `SeatReservationService.cs:141` |

---

## 4. Regression suite (minimum after any change)

Ordered, ~30 minutes:

1. `GET /health` → `Healthy`; CP and Website return 200.
2. CP super-admin sign-in (TOTP) → dashboard renders, 0 console errors.
3. CP element sweep on 5 representative pages (a `SimfDataGrid` list, a CRUD form, the roles editor, the venue map, the AI dashboard).
4. CP CRUD golden path on `/admin/interests` (create → duplicate-name conflict → edit → soft-delete → filter).
5. Registration + approval: `seed_pending.py` → account appears in `/admin/visitors/pending` → Approve → toast + badge QR issued.
6. App: build matched APK, install, first-run onboarding, sign in as `qa.pend3.0726` (OTP via the sink) → Home.
7. App element sweep: Home, Sessions, Session detail (assert real times, **not** `01 يناير` / `03:00`), Agenda, Badge (QR renders), Profile, Notifications.
8. App English/LTR switch → Home re-renders fully translated, 0 unnamed controls.
9. Staff: sign in as `staff@simf.local` → Staff home shows **Gate scanner** + **Register a visitor**.
10. Exhibitor: scan `FF3W2RXA3BFX` → 200 → My Visitors count increments by 1; re-scan → still 1; unknown QR → 404.
11. Session registration: session detail → "سجل لحضور الجلسة" → confirm → `SeatReservations` +1.

---

## 5. Owner-request traceability

Every request and question the owner raised, mapped to its coverage. This is the checklist to re-run:
nothing here may be silently dropped in a future round.

Legend: **TESTED** driven live this round · **BLOCKED** cannot run until the named defect/seed gap is
cleared · **AUDIT** answered by source audit (no runtime path exists yet) · **REQUEST** a new
capability to build, captured as a gap.

| # | Owner's words | Coverage | Status | Cases | Findings |
|---|---------------|----------|--------|-------|----------|
| R1 | "did you test staff … scan qr, new user" | Staff home; gate scanner; walk-in registration | **TESTED** / scan **BLOCKED** | `E2E-STF-*` | Staff home + both tiles work. Scan blocked by BUG-018 + BUG-023. New user created → BUG-020 |
| R2 | "did you test modlator, an thire related screnn" | Moderator home, session-detail "إدارة الأسئلة", moderation desk | **TESTED** / actions **BLOCKED** | `E2E-MOD-*` | Desk guards correctly; no `SessionModerators` row → BUG-023 |
| R3 | "and options based on filter button" | Agenda/programme filter chips | **TESTED** | `E2E-MOD-*` | Options are **الكل / جلسات / ورش العمل**; the selected chip is intentionally non-clickable |
| R4 | "did you test hal check in/out, session check in/out" | Hall attendance + session registration | **PARTIAL** | `E2E-CHK-*` | Session **registration** works (`/seats/join` → 200, `SeatReservations` +1). Arrival check-in/out rides on gate scanning → BLOCKED by BUG-023. Confirmed three distinct concepts: registration ≠ seat reservation ≠ arrival check-in |
| R5 | "share user info for exibor by scan badget and add to my contact list" | `POST /app/exhibitor/visitors/scan` → My Visitors | **TESTED** | `E2E-EXH-*` | Works; duplicate scan idempotent; unknown QR → 404 |
| R6 | "and sned to exibtor email?" | e-mail of the captured lead | **REQUEST** | `E2E-EXH-*` | **Not implemented** — zero mail in the sink after a scan → **BUG-024** |
| R7 | "add to my contact list" (which list?) | My Visitors vs My Contacts | **AUDIT** | — | Two separate features → **BUG-025**, needs an owner ruling |
| R8 | "Make the Logo size fit to box size in all logo view, and on-press … full size" | every logo surface: sponsor, exhibitor/company, media-partner, booth, org | **AUTHORED** → **§6.6** | `E2E-LGO-001…010` | Both halves **NOT built**: every logo is `BoxFit.cover` (crop) → **BUG-033**; no logo is tappable and the app has no image viewer → **BUG-034**. Assets also 404 (BUG-001), so fit is judged on the widget, not the render |
| R9 | "Add Edit phone number in my profile, no verfiy, ONLY VALIDATE" | profile phone edit | **AUTHORED** → **§6.7** | `E2E-PHN-001…009` | The validation rules exist and are triple-locked (app regex = server regex); the **edit surface does not** → **BUG-035** |
| R10 | "on seat, there are seat type (VVIP, VIP, Normal) - not exist Only Lable" | seat entity / layout model | **AUTHORED** → **§6.5** | `E2E-SEA-001…004` | Confirmed: a seat has **no type** — only a row label → **BUG-028** |
| R11 | "defualt when define seat at session must be as vvip reservated" | seat definition default | **AUTHORED** → **§6.5** | `E2E-SEA-005, 006` | Not built → **BUG-029**; the manual workaround (admin blocks the row) is exercised so the target behaviour is testable today |
| R12 | "ALLOW VIP IN VIP SEAT, AND ALL OTHER VISTORS TYPE INTO OTHER SEATS" | seat eligibility by profile type | **AUTHORED** → **§6.5** | `E2E-SEA-007…010` | Not built → **BUG-030**; the reserve endpoints never read the caller's profile type |
| R13 | "add on tablet screen for seating guest … copy seat page … assegin it to staff … scan badget qr or press on seat and found info (id, name, photo)" | new Staff seating-assistant screen | **AUTHORED** → **§6.5** | `E2E-SEA-015…019` | Screen does not exist → **BUG-032** |
| R14 | "on vvip Seat, no registeration, the admin can add name of guest manuly as hint in cp (هذا المقعد مخصص للوزير)" | per-seat free-text hint in CP | **AUTHORED** → **§6.5** | `E2E-SEA-011…014` | No per-seat metadata field → **BUG-031** |
| R15 | "on App on boarding pages, there are vedio in background not exist/not working" | onboarding background video | **AUTHORED** → **§6.8** | `E2E-ONB-001…011` | The three assets DO ship (`assets/videos/onboard_0{1,2,3}.mp4`, 4.6 MB each, all three byte-identical) and the code plays them muted + looping under a 90% navy overlay; failures are device-side decoding (D-768) and the "3 identical clips" content gap |
| R16 | "the desgin of this page is not match our desgin tokens" (staff Register-a-visitor) | design-token compliance | **TESTED** | `E2E-STF-*` | **BUG-019c/19i** — hardcoded dimensions; screen re-implements the shared field system |
| R17 | "Lanaguag btn not as we have" | language toggle component | **TESTED** | `E2E-STF-*` | **BUG-019a** — page-local `IconButton` instead of shared `SimfLanguageToggle` |
| R18 | "rtl/ltr txt alaign not correct" | direction handling | **TESTED** | `E2E-STF-*` | **BUG-019b** — hardcoded `TextDirection.ltr` mixed with `TextAlign.end` |
| R19 | "some txt/lable showed in multi line … Must simplfy lable" | label lengths | **TESTED** | `E2E-STF-*` | **BUG-019k** — "Attachments (ID / Iqama / passport image)" measures h=117 vs 58 for every other label |
| R20 | "for attach img must allow to read from cam or attach from file" | attachment sources | **TESTED** | `E2E-STF-*` | **BUG-019f** — gallery only; no `ImageSource.camera` anywhere in `lib/` |
| R21 | "even for Dropdownlist not as in create profile dorpdown, background color not correct" | dropdown + field styling | **TESTED** | `E2E-STF-*` | **BUG-019j** raw `DropdownButtonFormField` vs shared `SimfPickerField`; **BUG-019i** shared decoration is `filled: false`, the local copy is `filled: true` |
| R22 | "check validtion as on create profil" | submit-time validation | **TESTED** | `E2E-STF-*` | **BUG-019l** pristine form submits with no message; **BUG-019m** national-ID message omits the Luhn rule |
| R23 | "test creat a user" | walk-in creation end to end | **TESTED** | `E2E-STF-*` | 200 + user created → **BUG-020** (lands `PendingApproval`, no badge) |
| R24 | "must add bugs you found intto bug logs" | bug register | **DONE** | — | `BUG-001…025` in `BUG-LOG.md`, mirrored into the QA report |
| R25 | "I NEED FULL E2E or QA Test plan … saved with all test data" | this document | **DONE** | — | §1 run procedure, §2 test data, §3 known-blocked, §4 regression suite, §6 per-area cases |
| R26 | "page by page rtl/ltr, lable, usability, ui ux improvment, **for cp only**" | every CP route walked individually in both directions | **PLANNED** | `CP16-001…004, 006` | §6.16 with a per-route tracking table |
| R27 | "remove any AI sample in txt" | no AI-generated sample/placeholder copy anywhere in the CP | **PLANNED** | `CP16-005` | Interpreted as: no AI-drafted filler/sample strings left in labels, help text, empty states or seeded rows; each instance logged with page + string + source, then removed |
| R28 | "session … define, register, seats reservation, attending, rate, change seat, cancel, changes of hall or time, cancel session, streaming, ask speakers, translate, sumary" | full session lifecycle | **AUTHORED** → **§6.9, §6.10, §6.11, §6.12** | `E2E-SESD-001…018`, `E2E-SESP-001…016`, `E2E-SESL-001…016`, `E2E-RAT-001…013` | Define / change hall / change time / cancel = §6.9 · register + reserve + change seat + cancel + attend = §6.10 · streaming + ask + translate + summary = §6.11 · rate = §6.12. **"Change seat" has no in-place path** → BUG-036 |
| R29 | "meeting (speaker and delagate): request, approve/cancelation, confirm/rejection, attend, email notifcations" | both meeting tracks | **AUTHORED** → **§6.13, §6.14** | `E2E-MTGS-001…018`, `E2E-MTGD-001…017` | Each transition's exact notification + email recipient is asserted against `mail.log` |
| R30 | "mange halls, reservation, monitor attending, assgib hall" | hall management | **AUTHORED** → **§6.15** | `E2E-HAL-001…018` | Hall CRUD + seat layout + availability windows + assign-to-session + the two attendance monitors |
| R31 | "this test must be full test: flow, qa, validation" | every area covered on all three dimensions | **DONE** | — | §6.0 defines the dimensions; every section §6.1–§6.16 carries a **Dim map** naming which case ids cover FLOW, QA and VALIDATION |

---

## 6. Per-area test cases

### 6.0 The three test dimensions — every area is covered by all three

A "full test" of an area means **all three** of the following. A section is not complete until each
dimension has cases.

| Dim | Means | Typical assertions |
|-----|-------|--------------------|
| **FLOW** | The end-to-end journey and its state machine, across every actor and surface | each transition persists; the correct row is written; the next actor sees the right state; notifications/emails fire where specified |
| **QA** | What the user actually sees and can operate | every control present, named and enabled-or-correctly-gated; no broken images; no layout overflow; RTL **and** LTR; tablet + phone width; 0 console/log errors; correct empty states; local time, never UTC |
| **VALIDATION** | Everything that should be refused | required fields; format rules; boundary lengths; duplicates/conflicts (409); permission/role refusals (403); not-found (404); stale/expired state; server error handling; bilingual error text |

Each case below carries a **Dim** tag. When a flow step does not exist at all, the case is still
listed with `Expected today: FAILS` plus the blocking id — so coverage stays visible instead of
silently disappearing.

### Section index — every flow the owner named

| § | Area | Flows covered | Status |
|---|------|---------------|--------|
| 6.1 | Staff — gate scan + walk-in registration | scan QR, register new user, design/validation of the desk | **authored** |
| 6.2 | Moderator | home, session questions, filter-button options | **authored** |
| 6.3 | Check-in semantics | session registration vs seat reservation vs arrival check-in/out | **authored** |
| 6.4 | Exhibitor | badge scan → My Visitors → (missing) email | **authored** |
| 6.5 | Seats + seat types | seat definition, VVIP/VIP/Normal types, VVIP-reserved default, VIP-in-VIP-seat eligibility, VVIP manual CP hint, staff seating tablet screen | **authored** |
| 6.6 | Logos + images | fit-to-box on every logo surface, tap-to-full-size, authenticated vs anonymous image loading, broken/missing asset behaviour | **authored** |
| 6.7 | Profile — phone number | add/edit phone, **validate only — no verification step**, boundaries, RTL | **authored** |
| 6.8 | Onboarding | the background video (present, muted, looping, covers the frame, fallback) + the rest of the first-run flow | **authored** |
| 6.9 | Session — define + change (CP) | define a session, assign a hall, set the time, publish, **change hall**, **change time**, **cancel session** — and what each does to registrations, seat reservations and notifications | **authored** |
| 6.10 | Session — participation (app) | register to attend, reserve a seat, **change seat**, **cancel a reservation**, arrival check-in / check-out — keeping the three concepts distinct | **authored** |
| 6.11 | Session — live + content | **streaming**, **asking the speaker (3-stage Q&A)**, **translation / captions**, **session summary (محضر)** | **authored** |
| 6.12 | Rating | who may rate, when (attendance-gated), the rating types + scopes, one-per-scope | **authored** |
| 6.13 | Meetings — **speakers** | request → approve / reject → confirm / cancel → attend, and every email each transition sends | **authored** |
| 6.14 | Meetings — **delegations** | the same lifecycle for delegation meetings, incl. host/requester rules and the other-party confirm | **authored** |
| 6.15 | Halls | manage halls, seat layout, availability windows, **assign a hall to a session**, hall clash, **monitor attendance** | **authored** |
| 6.16 | **CP page-by-page RTL/LTR + labels + usability + UI/UX** (Control Panel **only**) | every CP page walked individually in both directions; label quality; usability friction; UI/UX improvement list; **no AI sample text left in any page** | **authored** (below) |
| 6.17 | Cross-cutting | auth + 2FA, permissions matrix, RTL/LTR parity, element sweep, local-time rule | **authored** (see §4 + the QA report) |

**Numbering note (2026-07-26).** The owner's flow list fixed §6.5–§6.15 to the eleven areas above,
which pushed the former "6.15 Cross-cutting" row to **§6.17**. Nothing was dropped; the cross-cutting
coverage is unchanged and still lives in §4 (regression suite) plus the QA report.

**Bug ids.** `BUG-001…BUG-027` were raised by earlier rounds. `BUG-028…BUG-036` were raised by this
authoring pass and are listed in §3 with the file:line that proves each one.

### 6.1 Staff — gate scanning + walk-in registration (R1, R16–R23)

Precondition for all: `staff@simf.local` signed in on the tablet **and** `profileComplete = true`
(run `complete_demo.py staff@simf.local` first — see BUG-022).

**Dim map** — FLOW: 003–007, 010, 011 · QA: 001, 002, 013, 014, 015 · VALIDATION: 008, 009, 012.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-STF-001 | Staff home shows only operational actions | app | Sign in as staff → observe Home | Exactly two tiles: **Gate scanner**, **Register a visitor**; no visitor tiles; 0 unnamed controls | `staff@simf.local` | — |
| E2E-STF-002 | Staff cannot reach visitor-only screens | app | From Staff home try to open `/sessions/join`, `/rate` | Router bounces to `/` | — | — |
| E2E-STF-003 | Gate scanner without an assignment | app | Tap **Gate scanner** | Screen "QR scan — staff" + **"You are not assigned to any gate."**; no camera error | — | — |
| E2E-STF-004 | Gate scanner with an assignment | app | Seed a gate + assign staff → reopen | Gate picker + entry/exit (دخول/خروج) selector, then the camera viewfinder | seed a `Gate` + `GateAssignment` | BUG-018, BUG-023 |
| E2E-STF-005 | Entry scan of a valid badge | app | Scan `FF3W2RXA3BFX` at an assigned gate | 200; a `GateScan` row is written; success card shows the visitor's name | badge `FF3W2RXA3BFX` | BUG-023 |
| E2E-STF-006 | Re-scan the same badge | app | Scan the same badge twice | Second scan is handled explicitly (duplicate/last-seen), never a crash | — | BUG-023 |
| E2E-STF-007 | Scan an unknown QR | app | Scan `ZZZZZZZZZZZZ` | Clear bilingual "badge not found" message | — | BUG-023 |
| E2E-STF-008 | Walk-in form — pristine submit | app | Open **Register a visitor** → tap submit with everything empty | **Expected today: FAILS.** No validation message appears (BUG-019l). Target: every required field flags and the view scrolls to the first error | — | BUG-019l |
| E2E-STF-009 | Walk-in form — field validation | app | Enter national ID `1122334455` | Rejected. Message must state the checksum rule, not only "10 digits starting with 1" (BUG-019m) | `1122334455` invalid / `1122334459` valid | BUG-019m |
| E2E-STF-010 | Walk-in golden path | app+api | Fill email, mobile, Arabic + English name, gender, nationality, national ID, job titles, organisation, both attachments → submit | 200; visitor created with the `Normal` profile type | see §2.3; org `A2D84555-…` | — |
| E2E-STF-011 | Walk-in outcome is usable at the gate | api | Inspect the created account | **Expected today: FAILS.** Account is `PendingApproval` with `QrId = NULL` → cannot be scanned in (BUG-020) | `qa.walkin.0726@zagali-test.local` | BUG-020 |
| E2E-STF-012 | Attachment sources | app | Tap "Attach file" and "Attach personal photo" | **Expected today: FAILS.** Gallery only; owner requires camera **or** file (BUG-019f) | — | BUG-019f |
| E2E-STF-013 | Design-system compliance | app | Compare against `/sign-up/visitor` | Same field decoration (`simfFieldDecoration`, `filled:false`), same `SimfFieldLabel`, same `SimfPickerField` dropdowns, same shared `SimfLanguageToggle` | — | BUG-019a/i/j |
| E2E-STF-014 | Direction + label sanity | app | Switch to Arabic, then English | No hardcoded LTR blocks; no label wraps to 2 lines (measure node height: all labels ≈58 px) | — | BUG-019b, BUG-019k |
| E2E-STF-015 | Accessibility | app | Sweep the screen | Every input and the submit button has an accessible name (currently 9 unnamed) | — | BUG-019h |

### 6.2 Moderator — screens + filter options (R2, R3)

Precondition: `moderator@simf.local` with `profileComplete = true` (needs an interest added — BUG-022).

**Dim map** — FLOW: 004, 006, 007 · QA: 001, 002, 003 · VALIDATION: 005 (role/assignment refusal).

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-MOD-001 | Moderator home | app | Sign in → Home | One tile "الجلسات / إدارة الأسئلة" + the 5-tab bottom nav; 0 unnamed controls | `moderator@simf.local` | — |
| E2E-MOD-002 | Programme filter options | app | Open the tile | Day strip (only 23/24/25 Nov clickable) and filter chips **الكل / جلسات / ورش العمل**; the selected chip is non-clickable by design | — | — |
| E2E-MOD-003 | Filter narrows the list | app | Tap **ورش العمل**, then **جلسات** | The session list changes to match the chosen type each time | — | — |
| E2E-MOD-004 | Moderation entry point | app | Open a session | A header action **إدارة الأسئلة** is present (absent for a plain visitor) | session `D1-01` | — |
| E2E-MOD-005 | Unassigned moderator is refused | app | Tap إدارة الأسئلة for a session they don't moderate | **"لست محاوِرًا لهذه الجلسة."** and no question data | — | — |
| E2E-MOD-006 | Assigned moderator sees the queue | app | Seed a `SessionModerators` row → reopen | The question list renders with its state filter | — | BUG-023 |
| E2E-MOD-007 | Question lifecycle | app | Approve / reject / mark answered a question | Each transition persists and the list re-filters | — | BUG-023 |

### 6.3 Check-in / registration semantics (R4)

**These are three different things — do not conflate them.**

**Dim map** — FLOW: 001, 003, 004, 005 · QA: 001 (dialog copy + local time) · VALIDATION: 002 (duplicate), 006 (attendance gate).

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-CHK-001 | Session **registration** | app | Session detail → "سجل لحضور الجلسة" → **انضمام** | Confirm dialog says the request goes to management; `POST /app/sessions/{id}/seats/join` → **200**; `SeatReservations` +1; success copy states this is **not** a seat reservation nor guaranteed entry | session `7efdfd03-…` | — |
| E2E-CHK-002 | Registration is idempotent | app | Register for the same session twice | No duplicate reservation row | — | — |
| E2E-CHK-003 | Seat **reservation** | app | Pick an actual seat | Requires a `HallSeatLayout`; currently `rowLabels: []`, `seatsPerRow: 0` | hall count = 1 | BUG-023 |
| E2E-CHK-004 | **Arrival** check-in at a gate | app | Staff scans the visitor at an assigned gate | A `GateScan` / `HallAttendance` row is written with direction | — | BUG-018, BUG-023 |
| E2E-CHK-005 | Check-**out** | app | Staff scans the same visitor with direction = exit | Exit recorded; dwell time derivable | — | BUG-023 |
| E2E-CHK-006 | Attendance gates rating | app | As a visitor who never attended, open Rate | "يمكنك تقييم ما حضرته فقط." (verified working) | — | — |

### 6.4 Exhibitor — badge scan → lead capture (R5, R6, R7)

Precondition: `exhibitor@simf.local` with `profileComplete = true` (needs an interest — BUG-022).

**Dim map** — FLOW: 001, 002, 006, 007 · QA: 002 (card fields + local time) · VALIDATION: 003 (duplicate), 004 (404), 005 (403 role refusal).

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-EXH-001 | Scan a valid visitor badge | api/app | `POST /app/exhibitor/visitors/scan` `{qrId, note}` | **200** + visitor card (name, Arabic name, organisation) | `FF3W2RXA3BFX`, note "QA lead capture" | — |
| E2E-EXH-002 | Capture appears in My Visitors | api/app | `GET /app/exhibitor/visitors` | 1 row with `scannedAt` + the note + the card | — | — |
| E2E-EXH-003 | Duplicate scan is idempotent | api | Scan the same badge again | 200 and the list stays at 1 row | — | — |
| E2E-EXH-004 | Unknown QR rejected | api | Scan `ZZZZZZZZZZZZ` | **404 `NOT_FOUND`** | — | — |
| E2E-EXH-005 | A visitor cannot use the endpoint | api | Call the scan endpoint with a visitor token | 403 | `visitor@simf.local` | — |
| E2E-EXH-006 | Lead is emailed to the exhibitor | api | Scan, then read `mail.log` | **Expected today: FAILS.** Zero mail is sent (BUG-024). Target: the captured card is emailed to the exhibitor | sink on :2525 | BUG-024 |
| E2E-EXH-007 | Lead reaches the contact list | app | Open My Contacts after a scan | Owner ruling needed: today the capture lands in **My Visitors**, a different list (BUG-025) | — | BUG-025 |

### 6.5 Seats + seat types (R10, R11, R12, R13, R14)

**What the code actually is** (read on `origin/main` `707d0ee6`, so a runner is not surprised):

* A hall gets **one** `HallSeatLayout` — `RowLabels` (CSV, 1–26 unique labels of 1–8 chars),
  `SeatsPerRow` (1–80) and, since D-767, an optional per-row `SeatCounts` CSV that makes the grid
  ragged. Edited at **`/admin/halls/seat-layouts`**, saved by `PUT /admin/halls/{hallId}/seat-layout`.
* A session inherits its hall's `SeatSelectionMode` unless it overrides it. **A hall with no layout
  is forced to OpenSeating** (`SeatReservationService.EffectiveMode`), whatever the mode says.
* A booking is a `SeatReservation` row: `RowLabel` + `SeatNumber` (both **null** for an
  open-seating join), `Kind` ∈ {UserBooking, RandomAssignment, AdminReservedRow, OpenSeating},
  `Status` (created **Approved** — there is no approval step since 2026-07-18) and `Expires`
  (= session `Start − 3 min`, the no-show release deadline).
* **There is no seat type.** "VVIP" / "VIP" can only be typed as a row *label*.

**Fixture to build before running this section** (the QA DB ships `HallSeatLayouts = 0`, §2.5):

```
# 1. a hall with a small, checkable capacity  (seatSelectionMode is an INT on the wire:
#    0 = AssignedSeat, 1 = OpenSeating)
POST /admin/halls        { code:"QA-H2", name:"QA Hall 2", nameArabic:"قاعة الاختبار 2",
                           capacity: 30, seatSelectionMode: 0 }
# 2. a ragged layout: VVIP row of 4, VIP row of 6, then two normal rows of 10
PUT  /admin/halls/{QA-H2}/seat-layout
     { rowLabels:["VVIP","VIP","A","B"], seatsPerRow:10, seatCounts:[4,6,10,10] }
# 3. a session in that hall  (code QA-S1, 2026-11-24 09:00→10:00 +03:00)
POST /admin/sessions     { code:"QA-S1", hallId:{QA-H2}, type:"Session", speakers:[…1…] }
```

Layout capacity = `4+6+10+10 = 30` = the hall capacity, so `SEAT_CAPACITY_EXCEEDED` is one seat away
and easy to trigger.

**Dim map** — FLOW: 001, 005, 006, 015–019 · QA: 002, 003, 004, 011, 012, 020, 021 ·
VALIDATION: 007–010, 013, 014, 022–026.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-SEA-001 | Define a session's seats | CP | `/admin/halls/seat-layouts` → pick **QA-H2 — QA Hall 2 (cap 30)** → Row labels = `VVIP,VIP,A,B` → per-row seat inputs 4 / 6 / 10 / 10 → **Save** | 200; "Hall capacity 30 / Layout capacity 30"; the preview grid draws a 4-seat VVIP row above a 6-seat VIP row above two 10-seat rows; `GET …/seat-layout` reads back `seatCounts:[4,6,10,10]` | QA-H2 | — |
| E2E-SEA-002 | The layout is what the visitor sees | app | Sign in as `qa.pend3.0726` → `/sessions/{QA-S1}` → **الانضمام إلى الجلسة** → the picker opens | `/sessions/{QA-S1}/pick-seat` renders the same ragged grid: the VVIP row draws 4 squares, VIP 6, A and B 10 each; the legend reads **محجوز · متاح · مقعدك**; the stage band reads **المسرح · STAGE** | QA-S1 | — |
| E2E-SEA-003 | Seat types exist as a concept | app + CP | Look for any VVIP / VIP / Normal *tier* marking on a seat — in the picker legend, the CP seat plan legend, and `GET /app/sessions/{id}/seats` | **Expected today: FAILS.** The wire has `rowLabel`, `seatNumber`, `kind`, `status`, `checkedIn` — no tier. The CP legend is Free / User / Admin / Random, i.e. *kind*, not *type*. The only tier signal is the operator having typed `VVIP` into a row label (**BUG-028**) | — | BUG-028 |
| E2E-SEA-004 | A multi-character row label stays readable | app | Render the picker on the ragged layout above | The row-label column widens to the longest label (`VVIP` → 4 × `seatRowLabelCharWidth`), so `VVIP` renders on ONE line, and every row's seats still align column-for-column | — | — |
| E2E-SEA-005 | VVIP seats are reserved by default | CP | Immediately after E2E-SEA-001, without any further action, open `/admin/sessions/seat-plans` → pick **QA-S1** | **Expected today: FAILS.** All 30 seats are free. `SetLayoutAsync` writes only the layout; it creates no `AdminReservedRow`. Target: the 4 VVIP seats are already blocked when the seats are defined (**BUG-029**) | — | BUG-029 |
| E2E-SEA-006 | Manual VVIP hold (today's workaround) | CP + app | `/admin/sessions/seat-plans` → QA-S1 → type `VVIP` in **Reserve row** → Reserve row. Then re-open the app picker | 4 `AdminReservedRow` rows written (`POST /admin/sessions/{id}/seats/reserve-row`); the CP grid paints the VVIP row with the admin swatch; in the app those 4 seats render **محجوز** (✕ glyph) and are **not tappable** | — | — |
| E2E-SEA-007 | A single VIP hold | CP | On the seat grid click free seat **VIP-3** | `POST …/seats/reserve-seat` → 200; one `AdminReservedRow` on (VIP, 3); clicking it again releases it (`DELETE …/seats/{id}`) | — | — |
| E2E-SEA-008 | Re-blocking a taken seat | api | Have `visitor@simf.local` reserve `A-1`, then `POST /admin/sessions/{QA-S1}/seats/reserve-seat {rowLabel:"A", seatNumber:1}` | **409 `SEAT_ALREADY_RESERVED`** — "That seat is already reserved." / "هذا المقعد محجوز بالفعل." | — | — |
| E2E-SEA-009 | Out-of-bounds seat on a ragged row | api | As `visitor@simf.local`: `POST /app/sessions/{QA-S1}/seats/reserve {rowLabel:"VVIP", seatNumber:5}` (the VVIP row has 4) | **400 `SEAT_OUT_OF_BOUNDS`** — "Seat number must be between 1 and 4." / "يجب أن يكون رقم المقعد بين 1 و 4." Proves the bound is the row's own count, not `seatsPerRow` (10) | — | — |
| E2E-SEA-010 | Unknown row | api | `…/seats/reserve {rowLabel:"Z", seatNumber:1}` | **400 `SEAT_OUT_OF_BOUNDS`** — "Row 'Z' is not in the hall layout." / "الصف 'Z' غير موجود في مخطط القاعة." | — | — |
| E2E-SEA-011 | VIP may sit in a VIP seat | app | Sign in as `vip@simf.local` → QA-S1 picker → tap **VIP-1** → **تأكيد المقعد** | 200 and the seat is held. (Passes — but for the wrong reason: nothing checked the profile type) | `vip@simf.local` | — |
| E2E-SEA-012 | A NON-VIP must NOT take a VIP seat | app | Sign in as `visitor@simf.local` (profile type **Normal**, `C90DCA1C-…`) → QA-S1 picker → tap **VIP-2** → **تأكيد المقعد** | **Expected today: FAILS.** 200 — the seat is granted. `ReserveAsync` never reads `UserProfile.ProfileTypeId`. Target: 403 with a bilingual "this seat tier is not available to your pass" (**BUG-030**) | `visitor@simf.local` | BUG-030 |
| E2E-SEA-013 | A VVIP seat must not be self-reservable at all | app | Sign in as `vvip@simf.local`, then as `visitor@simf.local`; each taps **VVIP-1** with the row NOT admin-blocked | **Expected today: FAILS.** Both succeed. Owner rule: a VVIP seat carries no self-registration by anyone; it is admin-assigned only (**BUG-029** + **BUG-030**) | `vvip@simf.local` | BUG-029, BUG-030 |
| E2E-SEA-014 | Auto-pick respects tiers | app | As `visitor@simf.local` tap **اختيار تلقائي** repeatedly on a nearly-full QA-S1 | **Expected today: FAILS.** `ReserveRandomAsync` picks from every free seat in the grid, so a Normal visitor can be auto-seated in VVIP/VIP. Target: the random pool excludes tiers the caller is not eligible for (**BUG-030**) | — | BUG-030 |
| E2E-SEA-015 | Per-seat admin hint | CP | `/admin/sessions/seat-plans` → QA-S1 → hover / click **VVIP-1** and look for a free-text note field | **Expected today: FAILS.** The only per-seat affordance is the tooltip built by `SeatTitle(...)` and a click that reserves/releases. No hint column exists on `SeatReservation` (**BUG-031**). Target: an admin can type `هذا المقعد مخصص للوزير` on one seat | — | BUG-031 |
| E2E-SEA-016 | The hint reaches the seating staff | app (staff) | With a hint set on VVIP-1, open the staff seating screen | **Expected today: FAILS** — both the hint (BUG-031) and the screen (BUG-032) are missing | — | BUG-031, BUG-032 |
| E2E-SEA-017 | Staff seating screen exists | app (staff) | Sign in as `staff@simf.local` → Home | **Expected today: FAILS.** Exactly two tiles — **Gate scanner** and **Register a visitor** (see E2E-STF-001). There is no seating tile and no `lib/features/staff/*seating*` screen (**BUG-032**) | `staff@simf.local` | BUG-032 |
| E2E-SEA-018 | Staff: scan a badge → find that guest's seat | app (staff) | Scan `FF3W2RXA3BFX` on the seating screen | **Expected today: FAILS** (BUG-032). Target: the seat map highlights that visitor's seat for the selected session and states row + seat, e.g. "الصف A · مقعد 1" | `FF3W2RXA3BFX` | BUG-032 |
| E2E-SEA-019 | Staff: tap a seat → who is in it | app (staff) | Tap an occupied seat | **Expected today: FAILS** (BUG-032). Target: a card with the holder's **reference id, name and photo**. Note the shipped seat wire returns only `reservationId` + `kind` + `status` + `checkedIn` — it carries **no holder identity**, so this needs a new staff-scoped endpoint, not just a screen | — | BUG-032 |
| E2E-SEA-020 | Four seat states render distinctly | app + CP | With QA-S1 holding: an admin block on VVIP-1, a held booking on A-1, a **checked-in** booking on A-2 (record an arrival first, §6.10), and A-3 free | The app draws: available (bordered, numbered), محجوز (navy fill + ✕), مقعدك (gold + ✓). `GET …/seats` returns `checkedIn:true` only for A-2's cell. The CP grid paints free / user / admin / random with its four swatches | — | — |
| E2E-SEA-021 | Wide hall scrolls, narrow hall centres | app | Set QA-H2 to `rowLabels:["A"], seatsPerRow:40` (capacity permitting), reopen the picker; then back to 10 | 40 seats: the grid scrolls horizontally with a visible scrollbar and seats stay full size (never shrunk). 10 seats: the grid is centred with no horizontal scrollbar. In **both** locales the grid geometry stays LTR (it is force-wrapped in `Directionality.ltr`) | — | — |
| E2E-SEA-022 | Layout capacity guard | CP/api | `PUT /admin/halls/{QA-H2}/seat-layout { rowLabels:["A","B"], seatsPerRow:20 }` (=40 > 30) | **400 `SEAT_CAPACITY_EXCEEDED`** — "Layout capacity (40) exceeds hall capacity (30)." / "السعة المقترحة (40) تتجاوز سعة القاعة (30)." The CP shows the red capacity notice but keeps **Save** enabled on purpose (the server owns the rule) | — | — |
| E2E-SEA-023 | Row-label validation | api | Send `rowLabels:["A","a"]` (duplicate ignoring case), then 27 labels, then a 9-char label | Each → **400 `SEAT_LAYOUT_INVALID`** "Row labels must be 1–26 unique entries of 1–8 chars each." / "يجب أن تكون رموز الصفوف بين 1 و 26 إدخالاً فريداً بطول 1 إلى 8 محارف." | — | — |
| E2E-SEA-024 | Seat-count validation | api | `seatCounts:[4,6,10]` against 4 rows; then `seatCounts:[0,6,10,10]`; then `seatsPerRow:81` with no counts | 1st → 400 "Seat counts (3) must match the number of rows (4)." 2nd → 400 "Each row's seat count must be between 1 and 80." 3rd → 400 "Seats per row must be between 1 and 80." All `SEAT_LAYOUT_INVALID`, all bilingual | — | — |
| E2E-SEA-025 | Shrinking a layout that has bookings | api | With A-9 held, `PUT …/seat-layout { rowLabels:["VVIP","VIP","A","B"], seatCounts:[4,6,8,10] }` | **409 `SEAT_LAYOUT_HAS_RESERVATIONS`** — "This layout change would strand active seat reservations…" / "سيؤدي تغيير المخطط إلى إلغاء حجوزات مقاعد نشطة…" Release A-9 and the same call succeeds | — | — |
| E2E-SEA-026 | A hall with no layout has no picker | app | Point QA-S1 at the seeded hall (which has no `HallSeatLayout`) and reopen the session | The effective mode falls back to **OpenSeating**, so the CTA reads **سجل لحضور الجلسة** and there is no picker; `POST …/seats/reserve` on it → **409 `OPEN_SEATING_ONLY`** "هذه الجلسة بمقاعد مفتوحة — انضم فقط دون اختيار مقعد." | seeded hall | — |
| E2E-SEA-027 | Permission gate on the seat admin | api | Call `PUT /admin/halls/{id}/seat-layout` and `POST /admin/sessions/{id}/seats/reserve-row` with a token that lacks `SeatLayouts.Edit` / `SeatPlans.Edit` | 403 on each. `GET …/seat-layout` needs `SeatLayouts.View`; `/admin/sessions/{id}/seats/list` needs `SeatPlans.View` | — | — |

### 6.6 Logos + images (R8)

**Every logo surface in the app** and what it renders today:

| Surface | Widget | Source URL | Fit |
|---|---|---|---|
| Sponsor tile / hero | `SponsorLogo` | `{base}/app/assets/SponsorLogo/{id}/image` (anonymous, D-357) | `BoxFit.cover` |
| Exhibitor + company detail | `EntityLogoImage` | ExhibitorLogo, then the company logo as `fallbackUrl` | `BoxFit.cover` |
| Media partner card | `_PartnerLogo` | partner logo URL | `BoxFit.cover` |
| Booth company header | `_LogoTile` | company logo URL | `BoxFit.cover` |
| Any identity row (speaker, delegate, contact) | `SimfIdentityCell._LogoOrInitials` | avatar / logo URL | `BoxFit.cover` |
| Gallery tile | `GalleryMediaTile` | media URL | `BoxFit.cover` |

**Dim map** — FLOW: 009, 010 · QA: 001–006, 008 · VALIDATION: 007, 011, 012.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-LGO-001 | Logo fits the box (sponsors) | app | Open **الرعاة** and inspect a sponsor tile against a deliberately non-square logo (upload a 1200×200 wordmark to a sponsor first) | **Expected today: FAILS.** `BoxFit.cover` crops the wordmark to the square tile — the ends of the name are cut off. Owner rule: fit to the box (`BoxFit.contain`) with letterboxing, never crop (**BUG-033**) | a 1200×200 PNG | BUG-033 |
| E2E-LGO-002 | Logo fits the box (exhibitor / company) | app | **المعرض** → an exhibitor detail card | Same crop; same target (**BUG-033**) | — | BUG-033 |
| E2E-LGO-003 | Logo fits the box (media partner) | app | **شركاء الإعلام** | Same crop (**BUG-033**) | — | BUG-033 |
| E2E-LGO-004 | Logo fits the box (booth) | app | **الأجنحة** → a booth with a company | Same crop (**BUG-033**) | — | BUG-033 |
| E2E-LGO-005 | Logo fits the box (organisation / hero) | app | Home hero + any org logo surface | Same crop (**BUG-033**) | — | BUG-033 |
| E2E-LGO-006 | Tap a logo → full size | app | Tap each logo in 001–005 | **Expected today: FAILS.** No logo is inside a tap target; grep confirms the app's only `InteractiveViewer` is the venue map. Nothing opens (**BUG-034**). Target: a full-screen, pinch-zoomable view with a close control and a working back gesture | — | BUG-034 |
| E2E-LGO-007 | Broken / missing asset | app | Open any sponsor whose asset GUID has no stored bytes (the seeded state — BUG-001) | The image 404s and each widget falls back **without an exception and without an error glyph**: `SponsorLogo` → the derived initials (e.g. "SA"), `EntityLogoImage` → its `fallbackUrl` then the initials tile, `SimfIdentityCell` → initials. No red error box, no infinite spinner | seeded sponsors | BUG-001 (asset bytes) |
| E2E-LGO-008 | Loading state | app | Throttle the network to Slow-3G and reopen **الرعاة** | The initials placeholder shows while loading (`loadingBuilder`), then swaps to the image with **no layout jump** (`gaplessPlayback: true`); the tile never collapses to zero height | — | — |
| E2E-LGO-009 | Anonymous vs authenticated image loads | app + api | `curl` the sponsor asset URL with **no** Authorization header; then fetch a **user avatar** the same way | Sponsor/partner/booth logos are public (D-357) → 200 with no token. A user avatar is bearer-protected → 401 without a token; in the app it is fetched with an authenticated client and rendered from bytes (never a bare `Image.network`) | — | — |
| E2E-LGO-010 | Logo upload round-trip | CP + app | CP → an exhibitor / booth → upload a logo (D-764 `ExhibitorLogo` / `BoothLogo` asset categories) → save → reopen the app screen | The new logo appears within one refresh; the old initials fallback is gone; the CP list thumbnail also updates | a 512×512 PNG | — |
| E2E-LGO-011 | Oversized / wrong-type upload | CP | Upload a 20 MB TIFF as a sponsor logo | Rejected with a bilingual validation message naming the allowed types and the size cap — never a 500 and never a silently truncated file | 20 MB TIFF | — |
| E2E-LGO-012 | RTL | app | Switch the app to Arabic and repeat 001–005 | The logo box does not mirror its aspect handling; only the surrounding row order flips. No logo is stretched or squashed in one direction only | — | — |

### 6.7 Profile — phone number (R9)

**The rule the owner set: validate, do NOT verify.** No OTP, no SMS, no confirmation step — the
number is accepted the moment it matches the shape.

**What exists today.** The shapes are triple-locked and identical on both sides:

* app `lib/core/validation/phone_validation.dart` — Saudi `^(05\d{8}|\+9665\d{8})$`,
  international `^\+[1-9]\d{7,14}$`, after `normalizePhone()` (Arabic-Indic digits folded to Western,
  spaces + dashes stripped, a leading `00` rewritten to `+`).
* server `UpsertUserProfileRequestValidator` — the same two regexes and the same normalisation, on
  `SaudiMobile` and `InternationalMobile`. Both fields are **optional** at the field level.
* There is **no verification step anywhere** — which is exactly what the owner asked for.

**What is missing:** an edit surface. `/my-area` is a read-only identity card;
`/sign-up/visitor` does not prefill an existing profile; the CP shows `SaudiMobile` read-only.

**Dim map** — FLOW: 001, 002, 003 · QA: 004, 008, 009 · VALIDATION: 005, 006, 007.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-PHN-001 | Add a phone at profile creation | app | Sign up a fresh visitor → `/sign-up/visitor` → **رقم الجوال** = `0512345678` → complete + submit | 200; `UserProfiles.SaudiMobile = '0512345678'`; **no OTP screen, no SMS, no confirmation step** appears at any point | a fresh `qa.phone.<date>@zagali-test.local` | — |
| E2E-PHN-002 | Edit the phone afterwards | app | Sign in as `qa.pend3.0726` → `/my-area` → look for an edit affordance on the phone | **Expected today: FAILS.** `/my-area` ("الملف الشخصى") renders a read-only identity card + counters + badge; there is no profile-edit entry and no phone row to tap (**BUG-035**) | `qa.pend3.0726` | BUG-035 |
| E2E-PHN-003 | The only existing write path is destructive | app | Deep-link a signed-in, complete account to `/sign-up/visitor` | **Expected today: FAILS.** The screen opens EMPTY — it only calls `getProfileTypes`, it never loads the current profile — and submitting runs `upsertMyProfile`, which overwrites every field. So "just change my phone" would wipe the rest of the profile (**BUG-035**). Do not perform this on a fixture you still need | — | BUG-035 |
| E2E-PHN-004 | Admin cannot fix it either | CP | `/admin/visitors` → open the visitor → look at the mobile | It renders read-only ("Saudi mobile" on the view page) and there is no editable field; the number is only settable at walk-in creation on `/admin/visitors/walk-in`. Confirms there is **no** phone-edit path on either surface (**BUG-035**) | — | BUG-035 |
| E2E-PHN-005 | Saudi shapes accepted | app + api | Enter each of `0512345678`, `+966512345678`, `00966512345678`, `٠٥١٢٣٤٥٦٧٨` (Arabic-Indic), `05 12 34 56 78`, `05-12-34-56-78` | All six accepted; all six submitted as the same canonical value. The `00…` form is rewritten to `+966512345678` before it leaves the device | — | — |
| E2E-PHN-006 | Saudi shapes rejected | app + api | Enter `0412345678` (not 05), `051234567` (9 digits), `+9664 12345678`, `12345678` | Rejected inline with **"أدخل الرقم بصيغة 05XXXXXXXX أو +9665XXXXXXXX أو 009665XXXXXXXX"** / "Enter as 05XXXXXXXX or +9665XXXXXXXX or 009665XXXXXXXX". If the client is bypassed, the server returns 400 with **"يجب أن يكون رقم الجوال السعودي بصيغة 05XXXXXXXX أو +9665XXXXXXXX."** | — | — |
| E2E-PHN-007 | International boundaries | api | `POST /app/account/profile` with `internationalMobile` = `+1234567` (7 digits — one under), `+12345678` (8 — the minimum), `+123456789012345` (15 — the maximum), `+1234567890123456` (16 — one over), `+0123456789` (leading zero) | Accept only the 8- and 15-digit values. The rest → 400 with **"يجب أن يكون رقم الجوال الدولي بالصيغة الدولية ‎+‎ يليها رمز الدولة والرقم (E.164)."** | — | — |
| E2E-PHN-008 | Blank is allowed | api | Submit with both mobile fields empty (keeping every other required field) | Accepted — both mobile rules short-circuit on `string.IsNullOrEmpty(value)`. Note the composite profile-completeness rule may still bar the account elsewhere; this case only asserts the phone rule | — | — |
| E2E-PHN-009 | RTL rendering of the field | app | With the app in Arabic, type `+966512345678` into **رقم الجوال** | The label sits at the inline start; the number itself renders **left-to-right** and the `+` stays at the head (it must not jump to the end); the keyboard is `TextInputType.phone`; the inline error text is right-aligned Arabic | — | — |

### 6.8 Onboarding (R15)

**What ships.** `/onboarding` is a three-step first-run carousel (`OnboardingScreen`). Each step
loads its own asset video (`assets/videos/onboard_01.mp4` … `onboard_03.mp4`) through
`VideoPlayerController.asset`, then `setLooping(true)` → `setVolume(0)` → `play()`. `OnboardingBackground`
draws it inside `FittedBox(fit: BoxFit.cover)` under a 90%-navy `ColoredBox`. If `initialize()`
throws, the controller is disposed and step 1 falls back to `assets/images/onboarding_world_map.jpg`
(steps 2–3 to plain navy). Only one decoder is alive at a time.

**Verified on this base:** all three files exist (4,596,937 bytes each) and are declared under
`assets/videos/` in `pubspec.yaml`. The repo also carries the D-768 vendored `video_player_android`
(`third_party/video_player_android`) patched with `setEnableDecoderFallback(true)` for the faulty
HiSilicon hardware decoder — the root cause of "the video does not work" on the HUAWEI tablet.

**Dim map** — FLOW: 001, 006, 007, 008, 009 · QA: 002, 003, 004, 005, 010 · VALIDATION: 011.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-ONB-001 | Onboarding shows on first run only | app | `adb uninstall com.simrsnf.simf` → install → launch | Splash → **/onboarding**. Complete or skip it, kill and relaunch: the app goes straight to sign-in (`StorageKeys.onboardingCompleted`) | — | — |
| E2E-ONB-002 | The background video is present and playing | app | On step 1 sweep the semantics tree and take an `adb shell dumpsys media.player` / SurfaceFlinger reading | An active media surface exists and its position advances between two samples ≥2 s apart. The tablet returns a black `screencap` for the Flutter surface (§1.3), so playback is asserted from the media stack, **not** from a screenshot | — | — |
| E2E-ONB-003 | The video is muted | app | Play step 1 with the device volume up | Silence. `setVolume(0)` is called before `play()`; no audio-focus change and no media notification appears | — | — |
| E2E-ONB-004 | The video loops | app | Watch one step for longer than the clip length (~2× its duration) | It restarts seamlessly and keeps playing; no black frame at the loop point and no "replay" control | — | — |
| E2E-ONB-005 | The video covers the frame | app | Rotate the tablet and compare portrait vs landscape | The clip always fills the screen with no letterbox bars and no distortion (`FittedBox(cover)` + `Positioned.fill`); the navy 90% overlay covers the whole frame so the copy stays legible over any frame of the clip | — | — |
| E2E-ONB-006 | Decoder failure falls back cleanly | app | Force the failure path: install a build whose `assets/videos/onboard_01.mp4` is a 0-byte file (or run on the emulator where the decoder is unavailable) | **No crash, no error text.** Step 1 shows the world-map photo under the navy overlay; steps 2–3 show plain navy. Every control still works | a 0-byte mp4 | — |
| E2E-ONB-007 | Swiping swaps the video | app | Swipe step 1 → 2 → 3 → back to 1 | Each swap disposes the previous controller before creating the next (one decoder at a time); no audio, no stacked surfaces, no memory growth across 10 swipes; a fast swipe back does not leave the wrong clip playing | — | — |
| E2E-ONB-008 | Next / Skip / Back | app | Steps 1 and 2: **تخطي** is present. Step 3: it is replaced by an equal-height spacer. **التالي** on step 3 finishes. The back chevron is hidden on step 1 and present on 2–3 | Skip and finish both set `onboardingCompleted` and route to sign-in; the layout does not shift when تخطي disappears on the last step | — | — |
| E2E-ONB-009 | Language toggle | app | Tap the globe in the top bar | The copy switches AR ↔ EN in place, the choice persists (`localeController`), and the persisted locale is still in effect after onboarding completes on the sign-in screen | — | — |
| E2E-ONB-010 | Copy + dots | app | Read all three steps in both languages | The title is the SAME on all three steps by design (Figma 148:22 / 159:943 / 159:1053); only the body changes. Three pill dots track the active step. No raw key, no mojibake, no text clipped by the carousel's fixed height | — | — |
| E2E-ONB-011 | Content gap: the three clips are identical | asset | `certutil -hashfile assets/videos/onboard_0{1,2,3}.mp4 SHA256` | All three hashes match — the same hero clip ships as all three placeholders (documented in the source as "the owner replaces 02/03 in place later"). Not a code defect; raise it as **content pending** so the owner supplies clips 2 and 3 | — | — |

### 6.9 Session — define + change (CP) (R28)

Endpoints: `POST /admin/sessions` (perm `Sessions.Create`) · `PUT /admin/sessions/{id}`
(`Sessions.Edit`) · `DELETE /admin/sessions/{id}` (`Sessions.Delete`, soft-delete) ·
`PUT /admin/sessions/{id}/status` (`Sessions.Publish`). Page: `/admin/sessions` → the
Add/Edit form (`SessionsAddEdit`).

**The rule that matters most:** in `AdminSessionService.UpdateAsync`, `hallChanged || timeChanged`
**cascade-releases every active `SeatReservation` on that session** in the same unit of work
(`ReleasedAt` set, `Status = Cancelled`) and then notifies each affected visitor. Deactivating a
session does the opposite — it is **blocked** while any visitor booking is still held.

**Dim map** — FLOW: 001, 002, 007–012, 016 · QA: 003, 013, 017, 018 · VALIDATION: 004, 005, 006, 014, 015.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-SESD-001 | Define a session (golden path) | CP | `/admin/sessions` → **New** → Code `QA-S2`, Title `QA Session 2`, Title (Arabic) `جلسة اختبار 2`, Hall `QA-H2`, Type `Session`, Start `2026-11-24 11:00`, End `2026-11-24 12:00`, add one speaker → Submit | 200; the row appears in the grid with the code and the **Saudi-time** start; `Sessions` +1 with `IsActive = true`, `Status = Scheduled` | QA-H2 | — |
| E2E-SESD-002 | Every field round-trips on edit | CP | Re-open QA-S2, change only the Title, save, re-open | Live stream URL, sign-language URL, captions (both languages), session **Type**, seat-mode override, Language label and the outcome bullets all survive — the historically silent-drop fields (D-439) are on the update DTO | — | — |
| E2E-SESD-003 | Times are Saudi time, never UTC | CP | Read the Start/End labels on the Add/Edit form (`Admin.Sessions.Field.StartUtc/EndUtc`), the grid column headers and the view page (`Admin.Sessions.Column.StartUtc/EndUtc`), in **both** languages | All read **"Start (Saudi time)" / "End (Saudi time)"** and **"البدء (بتوقيت السعودية)" / "الانتهاء (بتوقيت السعودية)"**. No user-facing "UTC" appears anywhere, and the value shown equals the value entered (+03:00), per D-219. **This supersedes BUG-007** — verified fixed on `707d0ee6` | — | — |
| E2E-SESD-004 | Required-field validation | CP | Submit with an empty Code, then a blank Type, then a non-Event session with no speaker | 400 each: `SESSION_TYPE_REQUIRED` "A session type is required (Workshop, Session or Event)." / "نوع الجلسة مطلوب (ورشة عمل أو جلسة أو حدث)."; `SESSION_SPEAKER_REQUIRED` "A non-event session must have at least one speaker." / "يجب أن يكون للجلسة (غير الحدث) متحدّث واحد على الأقل." Each message is visible inline — not only in the console | — | — |
| E2E-SESD-005 | Duplicate code | CP | Create a second session with code `QA-S2` | **409 `SESSION_CODE_DUPLICATE`** — "A session with code 'QA-S2' already exists." / "توجد جلسة بالرمز 'QA-S2' بالفعل." | — | — |
| E2E-SESD-006 | Boundary lengths | CP | Code 17 chars (max 16), Title 257 (max 256), Description 2049 (max 2048), an outcome bullet of 513 (max 512) | Each is refused with its own bilingual message; the field `MaxLength` also stops the typing at the cap (validation triple-lock: UI = FluentValidation = EF) | — | — |
| E2E-SESD-007 | Assign a hall | CP | Set QA-S2's Hall to QA-H2 and save; then open the app session page | The app's seat map reports `hallId = QA-H2` and the picker draws QA-H2's layout; `GET /app/sessions/{QA-S2}/seats` returns that hall's `rowLabels` | — | — |
| E2E-SESD-008 | **Change the hall** — bookings are released | CP + app + api | Have `qa.pend3.0726` reserve A-1 on QA-S2. Create a second hall `QA-H3` (cap 30 + a layout). CP → QA-S2 → Hall = QA-H3 → Save. Then inspect | 200. `SeatReservations` for QA-S2: **every** active row now has `ReleasedAt` set and `Status = Cancelled`. The visitor receives an in-app notification `BookingRejected` titled **"تم إلغاء حجز المقعد"** with the body "تم تغيير موعد أو قاعة جلسة \"…\"، لذا تم إلغاء حجز مقعدك. يرجى الحجز من جديد." **`SendEmail = false`** — so `mail.log` must show **no** new message for this transition | QA-S2, QA-H3 | — |
| E2E-SESD-009 | **Change the time** — same cascade | CP | Re-book A-1, then move Start/End by one hour and save | Identical outcome to 008: all held seats released + the same notification. Confirms the release is driven by `hallChanged \|\| timeChanged`, not by the hall alone | — | — |
| E2E-SESD-010 | An admin row-block is released too, silently | CP | Block row `VVIP` on QA-S2, then change the time | The `AdminReservedRow` rows are also released (they are active reservations), but **no** notification is dispatched for them — `ReservedForUserId` is null, so there is nobody to notify. The admin must re-block the row after the move | — | — |
| E2E-SESD-011 | The visitor's view after the move | app | As `qa.pend3.0726` reopen the session detail | The **مقعدي** card is gone and the Join CTA is back; the notification inbox shows the release message; re-booking works and gets a new seat | — | — |
| E2E-SESD-012 | **Cancel the session** | CP | With one active visitor booking on QA-S2, click Delete | **409 `SESSION_HAS_ACTIVE_BOOKINGS`** — "This session has 1 active booking(s) — cancel or reject them before deleting it." / "لهذه الجلسة 1 حجز نشط — يجب إلغاؤها أو رفضها قبل حذفها." Release the booking from `/admin/sessions/seat-plans`, retry → 200, `IsActive = false`, the row leaves the app programme | — | — |
| E2E-SESD-013 | Cancelling is idempotent + soft | api | `DELETE /admin/sessions/{QA-S2}` twice | Both 200; the row is soft-deleted (`IsActive = false`), never physically removed; the second call is a no-op | — | — |
| E2E-SESD-014 | Deactivate-via-edit is guarded the same way | CP | Re-activate QA-S2, add a booking, then uncheck **Active** on the edit form and save | Same **409 `SESSION_HAS_ACTIVE_BOOKINGS`** — the guard is not bypassable through the update path | — | — |
| E2E-SESD-015 | Capacity cannot drop below held seats | CP | With 5 seats held and the hall + time unchanged, set Capacity override = 3 | **409 `SESSION_CAPACITY_BELOW_BOOKINGS`** — "The effective capacity (3) is below the 5 seat(s) already held." / "السعة الفعّالة (3) أقل من 5 مقعد محجوز بالفعل." Clearing the override to blank is checked the same way (it falls back to the hall capacity) | — | — |
| E2E-SESD-016 | Hall/time clash | CP | Create QA-S3 in QA-H2 overlapping QA-S2's window | **409 `SESSION_HALL_TIME_OVERLAP`**. A title-only edit of a session that already overlaps a legacy sibling must still save (the check only runs when the slot actually moves) | — | — |
| E2E-SESD-017 | Lifecycle status | CP/api | `PUT /admin/sessions/{id}/status` Scheduled→Held→Recorded→Published, then try Scheduled→Published directly | The adjacent moves succeed; the skip → **400 `SESSION_STATUS_TRANSITION_INVALID`**. The endpoint is gated by `Sessions.Publish`, which is deliberately separate from `Sessions.Edit` — a token with Edit but not Publish gets 403 | — | — |
| E2E-SESD-018 | Permission gates | api | Call create / update / delete / status with a token missing the matching permission | 403 on each; `POST /admin/sessions/list` needs `Sessions.View`. The CP nav item `/admin/sessions` is hidden for a role without `Sessions.View` | — | — |

### 6.10 Session — participation (app) (R28, R4)

**The three concepts must not be conflated** (this is §6.3's rule, restated here with the
participation cases):

1. **Session registration** — an OpenSeating join. `POST /app/sessions/{id}/seats/join`. Writes a
   `SeatReservation` with `RowLabel = null`, `SeatNumber = null`, `Kind = OpenSeating`. The app's own
   success copy states it: *"تم تسجيلك لحضور هذه الجلسة بنجاح. هذا التسجيل لا يعني حجز مقعد أو ضمان
   الدخول للجلسة، سيتم تأكيد دخولك عند تسجيل الدخول للجلسة"*.
2. **Seat reservation** — an AssignedSeat pick. `POST …/seats/reserve` (or `…/reserve-random`).
   Writes a specific row + seat. Success copy: *"تم حجز المقعد بنجاح سيتم الغاء الحجز في حالة عدم تسجيل
   الدخول للجلسة قبل 3 دقائق قبل بدء الجلسة لاتاحة المقعد لأشخاص اخرين"*.
3. **Arrival check-in** — an operator scans the badge at the hall door
   (`POST /admin/sessions/{id}/arrivals`) or the venue gate. Writes `HallAttendance` / `GateScan`.
   **The visitor's own app has no self check-in** — grep confirms `ScanDirection.checkIn` exists only
   inside `features/gates/` (the staff scanner).

**Dim map** — FLOW: 001–006, 010–013 · QA: 007, 014, 015 · VALIDATION: 008, 009, 016.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-SESP-001 | Register to attend (open seating) | app | As `qa.pend3.0726` open a session in a hall with **no** layout → the CTA reads **سجل لحضور الجلسة** → tap → the confirm dialog **تأكيد الانضمام** / "سيتم إرسال طلب انضمامك إلى الإدارة للموافقة." → **انضمام** | `POST …/seats/join` → 200; one `SeatReservation` (`kind = OpenSeating`, `status = Approved`, `rowLabel`/`seatNumber` null, `expires = start − 3 min`); the success alert is the "not a seat reservation" copy above | session `7efdfd03-…` | — |
| E2E-SESP-002 | Registration is idempotent | app/api | Call `…/seats/join` twice | 2nd → **409 `SEAT_ALREADY_OWNED_BY_SESSION`** "You already have a booking for this session." / "لديك حجز بالفعل لهذه الجلسة." — no second row. The app surfaces the server's own localized reason, not a generic failure | — | — |
| E2E-SESP-003 | Reserve a specific seat | app | On QA-S1 (assigned seat) tap **الانضمام إلى الجلسة** → the picker opens → tap **A-1** → the chip reads **المقعد المختار: الصف A · مقعد 1** → **تأكيد المقعد** | `POST …/seats/reserve` → 200; the info dialog shows the 3-minute check-in warning; on dismiss the picker pops and the session page reloads showing the **مقعدي** card with "الصف A · مقعد 1" | QA-S1 | — |
| E2E-SESP-004 | Auto-pick | app | On the picker tap **اختيار تلقائي** | `POST …/seats/reserve-random` → 200 with some free seat. On a full session the failure copy is the dedicated **"لا توجد أماكن متبقية"** (`SEAT_SESSION_FULL`), not the generic seat error | — | — |
| E2E-SESP-005 | Two-step select → confirm | app | Tap A-2, then A-3, then Confirm | Only the last tap is committed (one hold on A-3). **تأكيد المقعد** is disabled until a seat is selected; both CTAs are disabled while a call is in flight | — | — |
| E2E-SESP-006 | **Change seat** | app | Holding A-3, reopen the session and try to move to A-4 | **Expected today: FAILS.** With a reservation in hand the Join CTA is replaced by the seat card, so the picker is unreachable; calling `…/seats/reserve` directly → **409 `SEAT_ALREADY_OWNED_BY_SESSION`**. The only route is cancel → re-book, which loses the seat to anyone faster (**BUG-036**). Target: an in-place change that swaps the hold atomically | — | BUG-036 |
| E2E-SESP-007 | View my seat on the map | app | From the seat card tap **عرض** | `/sessions/{id}/my-seat` renders the read-only hall card with the held seat gold + ✓ and the capacity line "محجوز N من M". For an **OpenSeating** booking the View link is absent by design (there is no seat to show) | — | — |
| E2E-SESP-008 | Double-booking across sessions | api | Reserve on QA-S1 (09:00–10:00), then try any seat on a different session overlapping that window | **409 `BOOKING_OVERLAP`** — "You already have a booking for another session at this time." / "لديك حجز لجلسة أخرى في نفس الوقت." Released/cancelled holds do not block | — | — |
| E2E-SESP-009 | Booking an ended session | api | Reserve on a session whose End is in the past | **409 `BOOKING_SESSION_ENDED`** — "This session has ended; you can no longer book a seat." / "انتهت هذه الجلسة، ولم يعد بإمكانك حجز مقعد." A session that has merely **started** is still bookable (a walk-in can join) | — | — |
| E2E-SESP-010 | **Cancel a reservation** | app | On the session page tap **إلغاء** under the calendar/reminder row → confirm **إلغاء الحجز** / "سيتم إلغاء حجزك لهذه الجلسة." | `DELETE …/seats/mine` → 200; toast **"تم إلغاء الحجز"**; the row keeps `ReleasedAt` + `Status = Cancelled` (audit trail preserved) and the seat becomes available again in the picker | — | — |
| E2E-SESP-011 | Cancel after the session starts | api | Cancel once `now >= Start` | **409 `BOOKING_SESSION_STARTED`** — "You cannot cancel a booking after the session has started." / "لا يمكنك إلغاء الحجز بعد بدء الجلسة." The app shows that server message verbatim | — | — |
| E2E-SESP-012 | Cancel with nothing held | api | `DELETE …/seats/mine` with no reservation | **404 `SEAT_RESERVATION_NOT_FOUND`** — "You do not have a seat to release in this session." / "ليس لديك مقعد للإلغاء في هذه الجلسة." | — | — |
| E2E-SESP-013 | No-show auto-release | api | Reserve a seat on a session starting in ~5 minutes and do **not** check in. Wait past `Start − 3 min` (the worker runs once a minute) | The hold is released (`Status = Cancelled`) and the holder gets a `BookingReleased` notification. A holder **with** a `HallAttendance` row is kept. A booking created at or after the deadline (a walk-in) is exempt — it is not a no-show | — | — |
| E2E-SESP-014 | **Arrival check-in** | CP + api | `/admin/hall-arrivals` → pick the session → scan `FF3W2RXA3BFX` (`POST /admin/sessions/{id}/arrivals`) | 200 with the resolved attendee; a `HallAttendance` row is written; `GET /app/sessions/{id}/seats` now returns `checkedIn: true` on that holder's cell and the CP live-hall view shows them inside | `FF3W2RXA3BFX` | BUG-018/BUG-023 for the *gate* path; the hall-arrivals console path is independent |
| E2E-SESP-015 | **Check-out** | CP | Scan the same badge on `POST /admin/sessions/{id}/departures` | The open `HallAttendance` row is closed (`Leave` set); the seat's `checkedIn` flips back to false; dwell time is derivable from enter/leave | — | — |
| E2E-SESP-016 | Registration ≠ reservation ≠ check-in | api | For one visitor, do all three on one session and read the DB | Registration/reservation both live in `SeatReservations` (distinguished by `Kind` and by whether row+seat are null); arrival lives in `HallAttendances`. Cancelling the reservation does **not** delete the attendance row, and checking in does **not** create a reservation. The app copy states the distinction (see the two success strings above) | — | — |

### 6.11 Session — live: streaming, Q&A, translation, summary (R28)

**The Q&A pipeline is 3-stage and the phase ROUTES the question**
(`SessionQuestionService.SubmitAsync`):

* **Pre** (`now < Start`) → the AI filter screens it (advisory only, never blocks) → the row lands
  `Status = Pending` for the **Scientific Committee** (`/admin/question-queue`) → once approved it
  reaches the **per-session moderator desk**.
* **Live** (`now >= Start`) → owner directive: **no AI, no committee**. The row lands
  `Status = Approved` straight on the moderator desk for push (accept) / hide (reject).
* Once live, the venue gate is real: if the hall has a geofence, the caller must have a
  `HallAttendance` row for that session. With no geofence, remote Q&A is allowed (the old client
  `isAtVenue` self-assert is no longer trusted).

**Dim map** — FLOW: 001, 002, 006–010, 015 · QA: 003, 004, 005, 013, 014, 016 · VALIDATION: 011, 012.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-SESL-001 | Configure the live feed | CP | `/admin/sessions` → QA-S1 → **Live stream URL** = a YouTube watch URL; **Sign-language URL** = a second YouTube URL; save | Both round-trip on the PUT (D-439); `GET /app/programme/sessions/{id}` returns `liveStreamUrl` + `liveSignLanguageUrl` | a YouTube URL | — |
| E2E-SESL-002 | Watch the stream | app | `/live?sessionId={QA-S1}` signed in | The YouTube IFrame player loads (a non-YouTube URL would use `video_player` instead), the **مباشر** badge shows, and the **يُبث الآن** block names the session | — | — |
| E2E-SESL-003 | Sign-language toggle | app | With both feeds set, use the **البث / لغة الإشارة** toggle | The player swaps feeds in place; with only one feed configured the toggle is absent | — | — |
| E2E-SESL-004 | Not-live and recording states | app | Open `/live?sessionId=` for (a) a session with no feed and no recording, (b) one with a recording only, (c) with no `sessionId` at all | (a) the "not live / scheduled" band, (b) the "recording available" note, (c) **"افتح جلسة لمشاهدة البث"** — the empty state, with **no** network call fired | — | — |
| E2E-SESL-005 | Login gate | app | Open `/live?sessionId=…` signed **out** | The route still loads but shows the in-screen prompt **"سجّل الدخول لمشاهدة البث المباشر"** plus a Sign-in button — never a player and never a redirect loop (D-577) | — | — |
| E2E-SESL-006 | Ask BEFORE the session (Pre) | app | On a **future** session's detail tap **اطرح سؤالاً قبل الجلسة** → type a question → **إرسال السؤال** | `POST /app/sessions/{id}/questions` → 200. Row: `Phase = Pre`, `Status = Pending`, `AiFilterVerdict` populated (advisory). Toast **"تم إرسال سؤالك"**. It does **not** appear on the moderator desk yet | session `QA-S2` | — |
| E2E-SESL-007 | Pre-ask needs a booking | app | On a future session, before joining, look at the ask card | It is disabled with the hint **"انضم إلى الجلسة لطرح سؤال"**; after joining it becomes tappable | — | — |
| E2E-SESL-008 | Stage 2 — the Committee | CP | `/admin/question-queue` (perm `Questions.View`) → find the Pre question → Approve; on a second one → Hide | Approve → `Status = Approved` and it now appears on the moderator desk. Hide → `Status = Hidden`, retained for audit, never displayed. The AI verdict is shown as advice and does not pre-decide either action | — | — |
| E2E-SESL-009 | Ask DURING the session (Live) | app | Once the session has started, `/live/question` → recipient **المتحدث** or **المضيف** → send | 200; the row lands `Phase = Live`, `Status = Approved` — straight on the moderator desk, bypassing AI and the Committee | — | — |
| E2E-SESL-010 | Stage 3 — the moderator | app | As an **assigned** moderator open `/sessions/{id}/moderate` → push a question, hide another, reorder | `PUT …/questions/{qid}/push` / `…/hide` / `…/reorder` each 200 and persist; the list re-filters. Unmoderated rows tie at `Order = 0` and sort FIFO by `CreatedAt` until someone reorders | `moderator@simf.local` | BUG-023 (no `SessionModerators` row is seeded) |
| E2E-SESL-011 | Q&A window | api | Submit on a session that has **ended**; then on an inactive (`IsActive = false`) session | Ended → **400 `SESSION_NOT_LIVE_FOR_QUESTIONS`** "The session is over and no longer accepting questions." / "انتهت الجلسة ولم تعد تستقبل الأسئلة." Inactive → the same code with "The session is not active." / "الجلسة غير مفعّلة." The app's own copy for a closed window is "الأسئلة مفتوحة فقط من 5 دقائق قبل بدء الجلسة حتى انتهائها." | — | — |
| E2E-SESL-012 | Question text validation | api | Submit `""`, then a 1001-character question | Both → **400 `SESSION_QUESTION_INVALID`** "Question text must be between 1 and 1000 characters." / "يجب أن يتراوح طول نص السؤال بين 1 و 1000 حرف." The app blocks an empty send first with **"اكتب سؤالك أولاً"** | — | — |
| E2E-SESL-013 | Venue gate on a live question | api | On a session whose hall HAS a geofence, submit a live question as a user with **no** `HallAttendance` row | **403 `NOT_AT_VENUE`** — "You must have arrived at the hall to ask a question." / "يجب أن تكون قد وصلت إلى القاعة لطرح سؤال." An `AuditEntry` is written with `ErrorCode = NOT_AT_VENUE`. Check the visitor in (E2E-SESP-014) and the same submit succeeds. On a hall with **no** geofence the same call is accepted | — | — |
| E2E-SESL-014 | Translation / captions | CP + app | Set **Live captions** (English) and **Live captions (Arabic)** on QA-S1; also try the `.srt` import and **Fetch from video** | The app renders the caption text in the gold-bordered strip under the player, in the user's language. With no captions set it shows the placeholder **"تظهر هنا الترجمة الحية للكلام…"**. **Note for the runner:** this is a *manual/stub* caption provider, not live machine translation — assert the text you entered, do not expect real-time speech translation | — | — |
| E2E-SESL-015 | Session summary (محضر) | CP + app | `/admin/session-summaries` → draft/generate the summary for QA-S1 → publish. Then in the app open `/session-summaries` → the session → `/ai-summary` | Only sessions with a **published** summary appear in the list (otherwise the empty state is "لا توجد ملخصات منشورة بعد."). The detail shows the summary sections plus, when uploaded, **التسجيل الكامل** and **ملخص الجلسة (فيديو)** | — | — |
| E2E-SESL-016 | Summary list tabs + search | app | On `/session-summaries` use **الكل / جلساتي / المفضلة** and the search box | Each tab filters correctly (جلساتي = booked sessions, المفضلة = hearted); a no-match search shows **"لا توجد نتائج مطابقة."**; the "الملخص متاح" chip appears only on sessions that have one | — | — |

### 6.12 Rating (R28)

`GET /app/feedback/form` (by `code` or `ratingTypeId`, plus `targetId`) then
`POST /app/feedback/submit`. The seeded rating types are **App** (Global), **Session**
(PerSession), **Day** (PerDay), **Event** (Global) and **Exhibition** (Global).

**Attendance gate** (`RatingFormService.IsAttendedAsync`): PerSession requires a `HallAttendance`
row for that exact session; PerDay requires a hall check-in on a session that event-local day **or**
an allowed venue-gate `CheckIn` scan that day; Global requires any hall check-in or any allowed
gate `CheckIn` scan.

**Dim map** — FLOW: 001, 002, 003, 008 · QA: 004, 005, 011, 012, 013 · VALIDATION: 006, 007, 009, 010.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-RAT-001 | Rate the forum (Global) | app | As a visitor who **has** a gate check-in or a hall check-in: **المزيد** → **تقييم** (`/rate`, defaults to `code=App`) → pick 4 stars → answer the per-question rows → **إرسال التقييم** | 200; toast **"شكراً لتقييمك"**; one `RatingResponse` with `TargetId` = the empty-Guid sentinel | any attended account | — |
| E2E-RAT-002 | Rate a session (PerSession) | app | Check in to QA-S1 (E2E-SESP-014), leave, then open `/rate?code=Session&targetId={QA-S1}` | The form loads with `isEligible = true` and the context header naming the session and when it was held; submitting → 200 with `TargetId = QA-S1` | QA-S1 | — |
| E2E-RAT-003 | Rate a day (PerDay) | app | `/rate?code=Day&targetId={programmeDayId}` for a day the visitor attended | Accepted. For a day with no hall check-in **and** no allowed gate CheckIn scan → refused per E2E-RAT-006 | a `ProgrammeDay` id | — |
| E2E-RAT-004 | Who may rate — signed out | app | Open `/rate` signed out | 401 from the endpoint (`RequireApprovedAccount`); the app routes to sign-in rather than rendering an empty form | — | — |
| E2E-RAT-005 | Who may rate — pending account | api | Call `GET /app/feedback/form` with a `PendingApproval` account's token | 403 — the policy requires an approved account | `qa.walkin.0726` | — |
| E2E-RAT-006 | Attendance gate | app + api | As `visitor@simf.local` with **no** attendance at all, open `/rate?code=Session&targetId={QA-S1}` and try to submit | The form returns `isEligible = false`, so the app blocks the round-trip and shows **"لا يمكنك تقييم عنصر لم تحضره."**; forcing the POST → **403 `RATING_NOT_ATTENDED`** with the same bilingual pair ("You can only rate something you attended.") | — | — |
| E2E-RAT-007 | Target rules | api | `code=Session` with **no** `targetId`; then `code=App` **with** a `targetId`; then `code=Session` with a random GUID | 1st → **400 `RATING_TARGET_REQUIRED`** "A target is required to rate this." / "يجب تحديد العنصر المراد تقييمه." 2nd → **400 `RATING_TARGET_REQUIRED`** "This rating type cannot target an entity." / "لا يمكن ربط هذا النوع من التقييم بعنصر." 3rd → **404 `RATING_TARGET_NOT_FOUND`** "The session to rate was not found." / "لم يتم العثور على الجلسة المراد تقييمها." | — | — |
| E2E-RAT-008 | One submission per scope (upsert, not duplicate) | app + api | Submit the App rating twice with different stars | Both 200; **one** `RatingResponse` row for that (user, type, target) — the second submission **updates** it. Re-opening the form prefills the saved answers. Repeat for `code=Session` on two different sessions: two rows, one per target | — | — |
| E2E-RAT-009 | Star boundaries | api | `overallStars` = 0, 6, then a per-question `stars` = 0 and 6 | Each → 400 with "Stars must be between 1 and 5." / "يجب أن يكون التقييم بين 1 و 5." When the type collects an overall score, omitting it → 400 "An overall rating is required." / "التقييم العام مطلوب." | — | — |
| E2E-RAT-010 | Comment boundary | api | A 2001-character comment (EF cap is 2000) | 400 from the FluentValidation `MaximumLength(2000)` — never a DB truncation error | — | — |
| E2E-RAT-011 | Required questions | app | Leave a required question unanswered and submit | Blocked with **"يرجى الإجابة عن الأسئلة المطلوبة"**-class copy (`rateRequiredQuestions`); the submit button stays inert until the form is complete | — | — |
| E2E-RAT-012 | Unknown / inactive type | api | `code=NotARealType` | **404 `RATING_TYPE_NOT_FOUND`** — "The rating type was not found." / "لم يتم العثور على نوع التقييم." | — | — |
| E2E-RAT-013 | Admin sees the results | CP | `/admin/ratings` (perm `Ratings.View`) and its KPI panel | The submitted responses list with the average-overall headline; the KPI view shows per-type counts, the overall average and per-question averages. `/admin/rating-config` (perm `RatingConfig.View`) is where the types / groups / questions are curated | — | — |

### 6.13 Meetings with SPEAKERS (R29)

Lifecycle (`SpeakerMeetingRequest.Status`): **Pending** → the admin responds → **AwaitingSpeaker**
(Approve + a bound hall slot, awaiting the speaker's own double-opt-in) **or** **Accepted**
(Confirm-verbal, or a legacy accept with no hall) **or** **Rejected** → an operator checks it in →
**Done**. `Cancelled` is the requester cancelling their own still-Pending request from
`/requests` in the app.

**Preconditions.** The requester needs `UserProfile.AllowsSpeakerMeeting = true` (admin-assigned —
this replaced the old VIP-tier test). The target speaker needs `AllowsMeetingRequests = true` and,
for the emails to be checkable, a non-empty `Speaker.Email`. `MeetingLinks:PublicWebBaseUrl` must be
configured or the confirmation links are skipped (a warning is logged and the email is not sent).

**Every email this track sends** — assert each against `mail.log`:

| Transition | In-app notification | Email |
|---|---|---|
| Submit | — | none |
| Approve (Accepted + `hallId`, `verbalConfirmed = false`) → AwaitingSpeaker | requester: `MeetingScheduled` "Meeting request approved" / "تمت الموافقة على طلب المقابلة" | **2 emails**: the requester (via the dispatcher, `SendEmail = true`) **and** the speaker — subject **"SIMF — please confirm a meeting request"** containing an **Approve** and a **Decline** link, "These links expire in 72 hours and each can be used once." |
| Confirm-verbal (Accepted + `hallId` + `verbalConfirmed = true`) | requester: `MeetingScheduled` "Meeting request accepted" / "تم قبول طلب المقابلة" | requester email + speaker email **"SIMF — a meeting request was accepted"** |
| Accept with no hall | same as Confirm-verbal | same as Confirm-verbal |
| Reject | requester: `MeetingCancelled` "Meeting request declined" / "تم رفض طلب المقابلة" | requester email only; **no** speaker email |
| Speaker clicks Approve / Decline | the requester's confirmed/declined outcome | per the token flow |
| Re-send confirmation | — | a fresh speaker link email; the previous token pair is killed first |
| Check-in → Done | — | none |

**Dim map** — FLOW: 001, 005–011, 015, 016 · QA: 002, 012, 017, 018 · VALIDATION: 003, 004, 013, 014.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-MTGS-001 | Request a speaker meeting | app | As a user with `AllowsSpeakerMeeting = true`: **اللقاءات الثنائية** → **طلب مقابلة متحدث** → pick a speaker → subject `QA speaker meeting` → pick a date card then a time chip → **إرسال الطلب** | `POST /app/speakers/{id}/meeting-requests` → 200; `Status = Pending`; toast **"تم إرسال طلب المقابلة"**; the row appears in **طلباتي** with the chip **قيد المراجعة**; `AvailabilityWindowId` is resolved from the picked slot. **`mail.log` gains nothing** | an account whose `UserProfile.AllowsSpeakerMeeting` an admin has set to `true` (set it on `vip@simf.local` first — the flag is per-user, **not** derived from the VIP tier) + a speaker with `AllowsMeetingRequests = true` and a non-empty `Email` | — |
| E2E-MTGS-002 | The slot picker | app | Open the request sheet for a speaker with no availability windows | **"لا توجد فترات متاحة حالياً"**; picking a time before a date shows **"الرجاء اختيار التاريخ أولاً"**; submitting with neither shows **"الرجاء اختيار التاريخ والوقت"**. An already-reserved slot is not offered | — | — |
| E2E-MTGS-003 | Not permitted | api | Submit as an account with `AllowsSpeakerMeeting = false` | **403 `FORBIDDEN`** — "Requesting a speaker meeting is not enabled for your account." / "طلب مقابلة المتحدّث غير مُفعَّل لحسابك." The app hides the CTA for such accounts and shows **"اللقاءات الثنائية متاحة للحسابات المصرَّح لها فقط"** | `visitor@simf.local` — the flag **defaults to false**, so an untouched account is the natural negative fixture | — |
| E2E-MTGS-004 | Field + target validation | api | Empty `requesterName`; a 1001-char `subject`; a `slotEnd <= slotStart`; a speaker with `allowsMeetingRequests = false`; an inactive speaker id | 400 `SPEAKER_MEETING_REQUEST_INVALID` "Requester name must be between 1 and 128 characters." / "يجب أن يتراوح طول اسم مقدّم الطلب بين 1 و 128 حرفاً."; 400 same code "Subject must be between 1 and 1000 characters."; 400 same code "A valid meeting slot (start and end) is required." / "يلزم اختيار فترة اجتماع صحيحة (بداية ونهاية)."; **409 `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED`** "هذا المتحدّث لا يقبل طلبات المقابلة."; **404 `SPEAKER_NOT_FOUND`** | — | — |
| E2E-MTGS-005 | Re-submitting MOVES the request | api | Submit again to the same speaker with a different slot and subject while the first is still Pending | 200 with the **same** request id — the open Pending request is updated in place (R8 rule). No duplicate row, no 409 | — | — |
| E2E-MTGS-006 | The admin desk | CP | `/admin/speaker-meeting-requests` (perm `SpeakerMeetingRequests.View`) → filter status = Pending → open the row | The detail shows the speaker, the requester name, the subject, the slot in **Saudi time** and the requester's email; an audit `SpeakerMeetingRequest.Viewed` event is written for the email disclosure (D-185) | — | — |
| E2E-MTGS-007 | **Approve** (bind a hall) | CP | On the Pending row choose **Approve**, pick a hall + a free hall slot (+ optionally a meeting table), leave "verbally confirmed" unticked → save | `PUT …/{id}/respond` → 200; `Status = AwaitingSpeaker`; `HallId`/`SlotStart`/`SlotEnd` bound; a token PAIR is committed **atomically** with the status. `mail.log` gains **two** messages: the requester's approval notice and the speaker's **"SIMF — please confirm a meeting request"** carrying an Approve link and a Decline link | — | needs `MeetingLinks:PublicWebBaseUrl` set |
| E2E-MTGS-008 | The speaker's link — preview then act | api | `GET /app/meeting-actions/{token}` (anonymous), then `POST /app/meeting-actions/{token}` | GET previews **without consuming** the token (safe for email prefetch); POST consumes it and applies the decision — Approve → `Status = Accepted` + `SpeakerDecisionAt`; Decline → the meeting does not go ahead. The requester is notified of the outcome | — | — |
| E2E-MTGS-009 | Token misuse | api | POST the same token twice; POST the *other* token of the pair afterwards; POST a random string; POST an expired (>72 h) token | Every one → **404 `MEETING_ACTION_TOKEN_INVALID`** "This link is no longer valid." / "لم يعد هذا الرابط صالحاً." — a single **neutral** error that never reveals whether the token was unknown, used, expired or already decided. The endpoint is rate-limited | — | — |
| E2E-MTGS-010 | **Confirm** (the admin already has the speaker's word) | CP | On a Pending row choose **Confirm** with a hall + slot and "verbally confirmed" ticked | `Status = Accepted` immediately, `SpeakerDecisionAt` stamped, **no** token minted and **no** speaker confirmation link email. `mail.log` gains the requester's acceptance notice **and** the speaker's "a meeting request was accepted" mail | — | — |
| E2E-MTGS-011 | Finalise an approved request | CP | On an **AwaitingSpeaker** row press **Confirm** with `hallId = null` | Allowed — it keeps the already-bound slot and moves to `Accepted`. Sending a plain Accept (no hall, not verbal) on an AwaitingSpeaker row → **409 `APP_REQUEST_ALREADY_RESPONDED`** | — | — |
| E2E-MTGS-012 | **Reject** | CP | On a Pending row choose Reject with the note `Speaker unavailable` | `Status = Rejected`, `ResponseNote` stored, `RespondedAt`/`RespondedByUserId` set. The requester gets `MeetingCancelled` **"تم رفض طلب المقابلة"** in-app **and** by email; the speaker gets **nothing**. The app row's chip flips to **مرفوض** | — | — |
| E2E-MTGS-013 | Respond guards | api | Respond twice to the same request; respond with `status = Pending`; respond with a 2001-char note | 2nd respond → **409 `APP_REQUEST_ALREADY_RESPONDED`** "تمت معالجة طلب المقابلة هذا بالفعل."; `Pending` → **400 `SPEAKER_MEETING_REQUEST_STATUS_INVALID`** "Response status must be Accepted or Rejected." / "يجب أن تكون حالة الردّ مقبولة أو مرفوضة."; the long note → **400 `SPEAKER_MEETING_REQUEST_INVALID`** "The response note must be 2000 characters or fewer." (it must NOT surface as the misleading "slot no longer available" 409) | — | — |
| E2E-MTGS-014 | Double-booking a speaker | api | Approve two different requests for the **same** speaker on overlapping slots (run them concurrently to exercise the Serializable path) | Exactly one wins; the loser → **409 `SPEAKER_MEETING_REQUEST_INVALID`** "That slot is no longer available." / "لم تعد هذه الفترة متاحة." The same 409 is returned when the *requester* already has a live meeting then: "The requester already has a meeting booked at that time." / "لدى مقدّم الطلب اجتماع محجوز بالفعل في هذا الوقت." | — | — |
| E2E-MTGS-015 | Re-send the confirmation | CP | On an AwaitingSpeaker row press **Re-send confirmation** | `POST …/{id}/resend-confirmation` → 200; any live token is invalidated first and a fresh pair minted; a new speaker email goes out. On a row in any other state → **409 `SPEAKER_MEETING_REQUEST_STATUS_INVALID`** "Only a request awaiting the speaker's confirmation can be re-sent." | — | — |
| E2E-MTGS-016 | **Attend** (check-in) | CP | On an **Accepted** row press **Check in** | `POST …/{id}/check-in` → 200; `Status = Done`, `CheckedInAt` + `CheckedInByUserId` stamped. On a row that is not Accepted → **409 `APP_REQUEST_ALREADY_RESPONDED`** "Only a confirmed meeting can be checked in." / "لا يمكن تسجيل الحضور إلا لاجتماع مؤكَّد." A Done meeting still HOLDS its (past) slot | — | — |
| E2E-MTGS-017 | The requester's own view | app | Follow one request through **طلبات** end to end | The chips move قيد المراجعة → مقبول (or مرفوض). **AwaitingSpeaker** is deliberately folded back to *pending* and **Done** back to *accepted* on the app wire, so the app never sees a status outside 0–3. Cancelling a still-Pending request from the app (**إلغاء الطلب** → "هل تريد إلغاء هذا الطلب؟") sets `Cancelled` and toasts "تم إلغاء الطلب" | — | — |
| E2E-MTGS-018 | Permission gates | api | Call list / get / respond / resend / check-in without the matching permission | list + get need `SpeakerMeetingRequests.View`; respond, resend and check-in need `SpeakerMeetingRequests.Manage`. Each returns 403 without it | — | — |

### 6.14 Meetings with DELEGATIONS (R29)

Same status enum, different actors. A **delegate** requests that *their* delegation meets another
**invited** country's delegation; the admin approves it onto a hall slot; then **any eligible member
of the TARGET delegation** confirms it — in the app by tapping the notification, or by the emailed
link.

**Host / requester rules** (verified in `DelegationMeetingRequestService.SubmitAsync`):

* The requester needs `UserProfile.AllowsDelegationMeeting = true` — this alone authorises the request.
* The requester's **nationality** is recorded as the requesting country; it must be set and active but
  **need not be an invited delegation** (D-768 — KSA is the forum's *host/owner*, deliberately not
  flagged `Country.IsInvited`, and must still be able to request meetings).
* The **target** country must exist, be active **and** be `IsInvited`.
* You cannot request a meeting with your own country.
* Attendee count 1–100; subject 1–1000 chars.

**Every email this track sends:**

| Transition | In-app notification | Email |
|---|---|---|
| Submit | — | none |
| **Approve** (Accepted + `hallId`, not verbal) → AwaitingSpeaker | requester: `MeetingScheduled` "Delegation meeting approved" / "تمت الموافقة على اجتماع الوفد"; **every eligible target-delegation member**: `MeetingRequested` "Delegation meeting request" / "طلب اجتماع وفد" | requester email; **one email per target member** — subject **"SIMF — please confirm a delegation meeting"** with a single **Confirm** link, "This link expires in 72 hours and can be used once." (the members' in-app card carries `SendEmail = false` because the link email already went) |
| **Confirm-verbal** → Accepted | requester: `MeetingScheduled` "Delegation meeting confirmed" / "تم تأكيد اجتماع الوفد" | requester email; target members are **not** re-notified |
| Target member confirms (app tap or link) | requester: `MeetingRequestConfirmed` "Delegation meeting confirmed" / "تم تأكيد اجتماع الوفد" | requester email |
| **Cancel** from Pending → Rejected | requester: `MeetingCancelled` "Delegation meeting declined" / "تم رفض اجتماع الوفد" | in-app + email to the requester |
| **Cancel** after approval → Cancelled | requester as above **plus** a retraction to every target member: `MeetingCancelled` "Delegation meeting cancelled" / "تم إلغاء اجتماع الوفد" | the retraction rides the dispatcher's email path |
| Check-in → Done | — | none |

**Dim map** — FLOW: 001, 005–010, 014, 015 · QA: 002, 011, 016, 017 · VALIDATION: 003, 004, 012, 013.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-MTGD-001 | Request a delegation meeting | app | As a delegate with `AllowsDelegationMeeting = true`: **اللقاءات الثنائية** → **طلب اجتماع وفد** → **اختر الوفد** = an invited country → **عدد الحضور** `5` → subject `QA delegation meeting` → pick a slot → **إرسال الطلب** | `POST /app/delegation-meeting-requests` → 200; `Status = Pending`; `RequestingCountryId` = the requester's nationality; the row shows in **طلباتي** as **طلب اجتماع وفد** / **قيد المراجعة**. `mail.log` unchanged | a delegate account + an `IsInvited` country | — |
| E2E-MTGD-002 | No delegations available | app | Open the sheet in a database with no `IsInvited` countries | **"لا توجد وفود متاحة"** — an empty picker, not an error toast | — | — |
| E2E-MTGD-003 | Not permitted / bad target | api | Submit with `AllowsDelegationMeeting = false`; then to a country that is not `IsInvited`; then to your **own** country; then with no active nationality on the profile | 403 `FORBIDDEN` "Requesting a delegation meeting is not enabled for your account." / "طلب اجتماع وفد غير مُفعَّل لحسابك."; **400 `DELEGATE_COUNTRY_NOT_INVITED`** "The target country is not an invited delegation." / "الدولة المستهدفة ليست من الوفود المدعوّة."; **400 `DELEGATION_MEETING_REQUEST_INVALID`** "A delegation cannot request a meeting with itself." / "لا يمكن للوفد طلب اجتماع مع نفسه."; **400 `DELEGATE_COUNTRY_NOT_INVITED`** "Your account has no active nationality set." / "لا توجد جنسية مفعّلة على حسابك." The app's own copies are "غير مصرَّح لك بطلب اجتماعات الوفود" and "هذا الوفد غير متاح للاجتماعات" | — | — |
| E2E-MTGD-004 | Field boundaries | api | `attendeeCount` = 0, then 101, then a non-numeric; subject `""`, then 1001 chars; `slotStart` set with `slotEnd` missing | Counts → **400 `DELEGATION_MEETING_REQUEST_INVALID`** "Attendee count must be between 1 and 100." / "يجب أن يتراوح عدد الحضور بين 1 و100."; subject → same code "Subject must be between 1 and 1000 characters."; half a slot → same code "A valid meeting slot (start and end) is required." The app blocks a bad count first with **"أدخل عدد حضور صحيحاً"** | — | — |
| E2E-MTGD-005 | Re-submitting MOVES the request | api | Submit again to the same target while Pending, with a new slot and count | 200 with the **same** id; the open Pending request is updated in place — no duplicate, no 409 | — | — |
| E2E-MTGD-006 | The admin desk | CP | `/admin/delegation-meetings` (perm `DelegationMeetings.View`) → open a Pending row | Requesting country, target country, attendee count, subject, slot in Saudi time and the requester email; an `AdminDelegationMeetingRequestViewed` audit event is written for the disclosure | — | — |
| E2E-MTGD-007 | **Approve** | CP | Choose Approve, bind a hall + a free slot (+ optional meeting table), leave verbal unticked | `Status = AwaitingSpeaker`; a single-use confirm token is committed in the same unit of work. `mail.log` gains the requester's approval mail **plus one mail per eligible target-delegation member** with the Confirm link. Each target member also gets an in-app `MeetingRequested` card | — | needs `MeetingLinks:PublicWebBaseUrl` |
| E2E-MTGD-008 | The other party confirms in the app | app | Sign in as a **target-delegation member** (their nationality = the target country **and** `AllowsDelegationMeeting = true`) → open the notification → **تأكيد الاجتماع** | `POST /app/delegation-meeting-requests/{id}/confirm` → 200; `Status = Accepted`, `ConfirmedAt`/`ConfirmedByUserId` stamped; toast **"تم تأكيد الاجتماع"**; the requester gets `MeetingRequestConfirmed` in-app + email | — | — |
| E2E-MTGD-009 | Only the target side may confirm | api | Call confirm as the requester; then as a member of an unrelated country; then as a target-country user **without** `AllowsDelegationMeeting` | 403 each — "You are not permitted to confirm this meeting." / "غير مسموح لك بتأكيد هذا الاجتماع." | — | — |
| E2E-MTGD-010 | Confirm races / wrong state | api | Two eligible target members confirm at the same instant; then confirm a request that is Pending / Accepted / Rejected | The first wins; every other call → **409 `APP_REQUEST_ALREADY_RESPONDED`** "This meeting is not awaiting confirmation." / "هذا الاجتماع ليس بانتظار التأكيد." (the app's own copy is "هذا الاجتماع ليس بانتظار التأكيد") | — | — |
| E2E-MTGD-011 | The confirm response leaks nothing | api | Read the JSON returned by the app confirm call | `requesterEmail` is **null** — the field is stripped for app callers (only the admin desks see it, and only with an audit event). Treat any non-null value here as a security defect | — | — |
| E2E-MTGD-012 | **Confirm-verbal** by the admin | CP | On a Pending row press Confirm with a hall + slot and verbal ticked; separately, press Confirm with `hallId = null` on an **AwaitingSpeaker** row | Both reach `Status = Accepted`. The AwaitingSpeaker path keeps the already-bound slot — it must NOT 409, and it must NOT re-run the "slot is in the past" guard (that guard only applies to a legacy accept from Pending) | — | — |
| E2E-MTGD-013 | Legacy accept with a past slot | api | Accept a Pending, slot-bearing request with **no** hall, where `slotStart < now` | **400 `DELEGATION_MEETING_REQUEST_INVALID`** — "The proposed meeting slot is in the past." / "فترة الاجتماع المقترحة في الماضي." | — | — |
| E2E-MTGD-014 | **Cancel** from Pending | CP | Reject a Pending row with a justification note | `Status = Rejected`; the requester gets `MeetingCancelled` in-app + email; the target members were never told about it, so they get **nothing** | — | — |
| E2E-MTGD-015 | **Cancel** after approval | CP | Reject a row that is AwaitingSpeaker (or Accepted) | `Status = Cancelled`; `HallId` and `MeetingTableId` are cleared so the hall slot frees up; the requester is notified; **and** every target member gets the retraction "تم إلغاء اجتماع الوفد" so nobody is left tapping a stale confirm prompt. Cancelling a Rejected / Cancelled / Done row → **409 `APP_REQUEST_ALREADY_RESPONDED`** | — | — |
| E2E-MTGD-016 | **Attend** (check-in) | CP | On an Accepted row press Check in | `POST …/{id}/check-in` → 200; `Status = Done` + `CheckedInAt`/`CheckedInByUserId`. Any non-Accepted state → **409** "Only a confirmed meeting can be checked in." / "لا يمكن تسجيل الحضور إلا لاجتماع مؤكَّد." | — | — |
| E2E-MTGD-017 | Hall double-booking | api | Approve two delegation meetings onto the same hall slot | One wins; the other → 409 (the `(HallId, SlotStart)` filtered-unique index is the equal-start backstop). The same slot must also not collide with a **speaker** meeting bound to that hall — the two tracks share the hall availability | — | — |

### 6.15 Halls (R30)

Pages: `/admin/halls` (CRUD, perm `Halls.View` / `.Create` / `.Edit` / `.Delete`) ·
`/admin/halls/seat-layouts` (`SeatLayouts.*`) · `/admin/hall-availability`
(`SpeakerMeetingRequests.Manage`) · `/admin/hall-arrivals` (`HallArrivals.View` / `.Record`) ·
`/admin/attendance` and `/admin/sessions/live-hall` (`Attendance.View`).

**Dim map** — FLOW: 001, 006–011, 014–016 · QA: 002, 012, 017, 018 · VALIDATION: 003, 004, 005, 013.

| id | title | surface | steps | expected | data | blocked-by |
|----|-------|---------|-------|----------|------|------------|
| E2E-HAL-001 | Create a hall | CP | `/admin/halls` → New → Code `QA-H2`, Name (English) `QA Hall 2`, Name (Arabic) `قاعة الاختبار 2`, Capacity `30`, Floor `Ground`, Seat selection `Assigned seat` → Save. (The **Active** checkbox is edit-only by design — a new hall is created active) | 200; the row appears in the `SimfDataGrid`; the hall is now selectable on the session form's **Hall** picker | — | — |
| E2E-HAL-002 | The page is a real list page | CP | Inspect `/admin/halls` | It is a `SimfDataGrid` — per-column filter, select-all + row checkboxes, quiet icon actions (view / edit / delete) — not a raw table (the CP list-page standard) | — | — |
| E2E-HAL-003 | Code + name validation | CP | Code `Q` (1 char), then 17 chars, then a duplicate of an existing code; blank English name; a 129-char Arabic name | "Code must be between 2 and 16 characters."; a duplicate → a 409-class conflict message; "English name is required (1–128 characters)."; "Arabic name is required (1–128 characters)." Each is bilingual and shown inline | — | — |
| E2E-HAL-004 | Capacity validation | CP | Capacity `-1`, then `abc`, then `0` | −1 and `abc` → "Capacity must be zero or a positive integer."; `0` is **accepted** (the rule is ≥ 0) — but a 0-capacity hall then makes every session in it full, so assert `SEAT_SESSION_FULL` on the first booking attempt | — | — |
| E2E-HAL-005 | Geofence validation | CP | Set latitude only; then lat `91`; then radius `0`; then radius `100001`; then all three blank | The first four → "The geofence needs a valid latitude (−90..90), longitude (−180..180) and radius (greater than 0, up to 100000 m) — set all three or leave all empty."; all three blank is valid and means **QR-scan-only arrivals** | — | — |
| E2E-HAL-006 | Seat layout for the hall | CP | `/admin/halls/seat-layouts` → QA-H2 → define rows + per-row counts → Save | Covered in depth by **E2E-SEA-001 … 026** — including the capacity guard, the label/count validation and the "would strand active reservations" 409 | — | — |
| E2E-HAL-007 | Hall reservation — availability windows | CP | `/admin/hall-availability` → QA-H2 → add a window `2026-11-24 09:00 → 17:00 (+03:00)` → save; then `GET /admin/halls/{QA-H2}/available-slots` for that day | The window persists; the free-slot list excludes any slot already held by a speaker or delegation meeting bound to that hall (statuses Accepted / AwaitingSpeaker / Done all HOLD a slot) | — | — |
| E2E-HAL-008 | Hall reservation — clash | api | Bind two meetings to the same hall + start | One wins; the other → 409 (see E2E-MTGD-017). Releasing the winner (cancel) frees the slot and it re-appears in `available-slots` | — | — |
| E2E-HAL-009 | **Assign a hall to a session** | CP | `/admin/sessions` → QA-S2 → Hall = QA-H2 → Save | The session moves to that hall; the app's seat map now reports QA-H2's layout and capacity. Assigning a hall that would overlap another session in the same hall → **409 `SESSION_HALL_TIME_OVERLAP`** | — | — |
| E2E-HAL-010 | Re-assigning releases the seats | CP | See **E2E-SESD-008** | Every held seat on that session is released and each affected visitor is notified in-app (no email) | — | — |
| E2E-HAL-011 | Hall purpose | CP | `PUT /admin/halls/{id}/purpose` → `Meeting`; then try to bind a **session** to a Meeting-only hall and a **meeting table** to a Session-only hall | The purpose persists and the CP surfaces the matching tools. A meeting/table may only target a `Meeting` or `General` hall — anything else is refused with a bilingual message | — | — |
| E2E-HAL-012 | Seat-selection mode | CP + app | Set QA-H2 to **Open seating**, leave the session override blank, open the session in the app; then set the session override to **Assigned seat** | Hall mode drives the CTA (**سجل لحضور الجلسة**); the session override wins over it (**الانضمام إلى الجلسة** + the picker). A hall with **no layout** is forced to open seating regardless of either setting | — | — |
| E2E-HAL-013 | Delete a hall in use | CP | Delete QA-H2 while QA-S2 still points at it | The delete is refused / the hall is soft-deleted without orphaning the session — assert that `/admin/sessions` still resolves QA-S2's hall name and that the hall no longer offers itself on the session picker. A soft-deleted hall must never leave a session with a dangling `HallId` | — | — |
| E2E-HAL-014 | **Monitor attendance** — arrivals console | CP | `/admin/hall-arrivals` → select QA-S1 → scan `FF3W2RXA3BFX` → then scan it again on the departures action | `POST /admin/sessions/{id}/arrivals` writes a `HallAttendance` row with `Method = QrScan`; the console shows the resolved attendee. The departure closes the row (`Leave` set). Both need `HallArrivals.Record`; an unknown QR is refused with a clear bilingual not-found | `FF3W2RXA3BFX` | — |
| E2E-HAL-015 | **Monitor attendance** — live hall view | CP | `/admin/sessions/live-hall` → QA-S1 | The 4-state seat grid (available / unavailable / reserved / confirmed) plus who is currently inside. A holder who checked in flips from "reserved" to "confirmed"; checking out flips them back | — | — |
| E2E-HAL-016 | **Monitor attendance** — dashboard | CP | `/admin/attendance` (perm `Attendance.View`) | The read-only per-session attendance figures over `HallAttendance`. Cross-check one session's count against the rows you created in 014 — they must agree | — | — |
| E2E-HAL-017 | Empty states | CP | Open `/admin/halls` with zero halls, `/admin/halls/seat-layouts` with zero halls, `/admin/hall-arrivals` with no scans, `/admin/attendance` with no attendance | Each shows its own empty state telling the operator what to do next — never a blank panel, never a spinner that never resolves, never a raw exception | — | — |
| E2E-HAL-018 | Permission gates | api | Call each hall endpoint without its permission | halls list/get → `Halls.View`; create/edit/delete → the matching `Halls.*`; seat layout read/write → `SeatLayouts.View`/`.Edit`; availability windows → `SpeakerMeetingRequests.Manage`; arrivals/departures → `HallArrivals.Record`; the attendance dashboards → `Attendance.View`. Each returns 403 without it, and the CP nav item is hidden | — | — |

### 6.16 Control Panel — page-by-page RTL/LTR, labels, usability, UI/UX (CP ONLY)

Owner requirement (R26): a final pass that walks **every Control Panel page one at a time** and judges
it as a *product*, not just as a set of working controls. **Scope is the Control Panel only** — the
app and the Website are out of scope for this section.

**Dim map** — QA: CP16-001…004 · VALIDATION: CP16-005 · FLOW: CP16-006 (usability is judged on the
real task flow, not a static screenshot).

**Method.** Sign in as super-admin, then for **each** route in the CP navigation: load it in **Arabic
(RTL)**, capture the element inventory + a screenshot, switch to **English (LTR)** and repeat. Record
one row per page in the tracking table below. Pages with a data grid are also checked with **0 rows**
(empty state) and with rows present.

| id | Dim | Check | Pass criteria |
|----|-----|-------|---------------|
| CP16-001 | QA | **RTL correctness** | In Arabic: text right-aligned; the whole layout mirrors (nav, table column order, icon side, chevrons, breadcrumbs); no Latin-only block stuck LTR inside an RTL page; numbers/dates render correctly; **no horizontal overflow** (`scrollWidth === clientWidth`) |
| CP16-002 | QA | **LTR correctness** | In English the mirror image of the above; nothing left mirrored from RTL; no clipped or truncated control |
| CP16-003 | QA | **Labels** | Every label, column header, button, tooltip, placeholder and page title is: translated in **both** languages (no raw resx key, no `Â·`-style mojibake — see BUG-002), consistent in terminology across pages (one word per concept), fits on **one line** (no 2-line wrap — see BUG-019k), and unambiguous without needing the row's data |
| CP16-004 | QA | **UI/UX improvement list** | Per page, record concrete improvements: visual hierarchy, spacing/alignment against the design tokens, primary action discoverability, column choice and order, use of colour/status chips, density on a 1080p screen. Output is a prioritised improvement list, not a pass/fail |
| CP16-007 | VALIDATION | **Design-system compliance** (objective, from the `front-end-design` skill + CLAUDE.md §8) | **No inline `style="…"`** except the accepted pattern of injecting a *runtime* value into a CSS custom property; **zero hardcoded hex colours** — MudBlazor `Color` enum or a `theme.tokens.css` variable; **zero hardcoded `font-family`**; no duplicate `:root` / `[data-theme="dark"]` blocks outside `theme.tokens.css`; **BEM** class naming; CSS confined to `app.css` (resets) / `theme.tokens.css` (tokens) / `theme.overrides.css` (overrides); **no scoped `.razor.css` in the Website project** |
| CP16-005 | VALIDATION | **No AI sample text** | **No AI-generated sample/placeholder/demo text is visible anywhere in the CP** — no lorem-style filler, no "sample"/"example"/"demo" AI copy, no leftover AI-drafted seed strings in labels, help text, empty states or seeded content rows. Every instance found is logged with page + exact string + its source (resx / seeder / DB row) and removed |
| CP16-006 | FLOW | **Usability on the real task** | For each page perform its primary task end to end and record friction: clicks-to-complete, whether the destructive action is confirmed, whether feedback (toast/inline) is immediate and specific, whether the empty state tells the user what to do next, whether the user is bounced or loses input (see BUG-005 re-login) |

**Per-page tracking table** — one row per CP route; fill on each run.

| Route | RTL | LTR | Labels | No AI text | Usability | UI/UX notes |
|-------|-----|-----|--------|-----------|-----------|-------------|
| `/admin` (dashboard) | | | | | | |
| `/admin/visitors/pending` | | | | | | |
| `/admin/interests` | | | | | | |
| `/admin/sessions` | | | | | | |
| `/admin/gates` | | | | | | |
| `/admin/halls` | | | | | | |
| `/admin/speakers` | | | | | | |
| `/admin/contacts` | | | | | | |
| `/admin/ai/prompts` | | | | | | |
| … *(one row per route — the CP has ~79 business pages; the full route list is enumerated from `CpNavigation.cs` at run time)* | | | | | | |

**Already-known inputs to this pass** (do not re-discover): BUG-002 `Â·` title mojibake on 7 list
pages; BUG-006 Contacts "Name (Arabic)" sort is a no-op; BUG-007 Sessions grid shows a user-facing
"Start (UTC)" label; BUG-004 three picker forms give no visible validation message; BUG-005 repeated
forced re-login mid-task; BUG-001 broken images on speakers / media-library / speaker-presentations.

> **Correction (2026-07-26, from the §6.9 authoring pass): BUG-007 is already fixed on `707d0ee6`.**
> `Admin.Sessions.Column.StartUtc/EndUtc` and `Admin.Sessions.Field.StartUtc/EndUtc` now resolve to
> "Start (Saudi time)" / "End (Saudi time)" and "البدء (بتوقيت السعودية)" / "الانتهاء (بتوقيت السعودية)".
> The **resx key names** still carry the misleading `Utc` suffix — that is a naming wart, not a
> user-facing defect. Re-verify with `E2E-SESD-003` rather than re-reporting BUG-007.

**CP16-007 baseline — already scanned (2026-07-26), so the pass starts from here:**

| Check | Result |
|-------|--------|
| Hardcoded hex colours in CP CSS | **0** — clean. Tokens are genuinely respected |
| Hardcoded `font-family` | **0** — both occurrences (`wwwroot/app.css:17,31`) correctly use `var(--font-family-base)` |
| Inline `style="…"` in CP `.razor` | **6 found, 5 are the accepted pattern** (injecting a runtime colour into a CSS custom property: `--simf-badge-color`, `--simf-walkin-card-color`, `--simf-bulk-swatch-color` in `PrintBag.razor:45`, `WalkInSuccessModal.razor:19`, `WalkInRegistrationForm.razor:65`, `ThemesList.razor:83`, `BulkBadgeGenerator.razor:64`). **1 is a real violation → BUG-026**: `GateOperatorConsole.razor:29` `style="margin-top:1rem"` |
| Scoped `.razor.css` in the **Website** project | **1 violation → BUG-027**: `Website/SIMF.Web/Components/Layout/MainLayout.razor.css` — CLAUDE.md §8 says centralise in the Theme |
| Scoped `.razor.css` in the **Control Panel** | 10 files. The §8 rule names the *Web* project only, so these are **not** flagged — confirm with the owner whether the rule extends to the CP |

### 6.17 Cross-cutting

Renumbered from 6.15 when the owner's flow list took §6.5–§6.15 (see the numbering note under §6.0).
The content is unchanged and is executed by two things already in this document, so nothing here
duplicates them:

* **§4 Regression suite** — health, CP sign-in with TOTP, element sweeps, the CRUD golden path,
  registration + approval, the app build/install/sign-in, the app element sweep, the English/LTR
  switch, staff, exhibitor and session registration. Run it after **any** change.
* **The permissions matrix** — every section above ends with a permission-gate case
  (`E2E-SEA-027`, `E2E-SESD-018`, `E2E-MTGS-018`, `E2E-HAL-018`, …). A CP page or admin action with
  no permission is a **security** defect, not a cosmetic one.
* **The local-time rule (D-219)** — Saudi +03:00, 12-hour clock, no user-facing "UTC" anywhere. It
  is asserted at each surface that shows a time: `E2E-SESD-003` (CP session form + grid),
  `E2E-MTGS-006` / `E2E-MTGD-006` (the meeting desks), and the app element sweep in §4 step 7
  (session times must not render as `01 يناير` / `03:00`).
* **RTL ↔ LTR parity** — §6.16 owns the Control Panel exhaustively; the app is covered per-surface
  (`E2E-SEA-021`, `E2E-LGO-012`, `E2E-PHN-009`, `E2E-ONB-009`).

Anything cross-cutting that is *not* covered by the above belongs in the QA report, not here.
