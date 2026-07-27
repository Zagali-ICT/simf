# E2E test catalogue — `Session detail` (`session-detail`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> page reuses already-built endpoints (D-265): the public session reads (D-199 /
> D-252) and the per-session seat-map `MyCell` (D-175). API implementations live in
> `tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs` and
> `tests/SIMF.Api.Tests/SeatReservationsTests.cs`. The **Flutter screen is built
> (D-300)** — widget/model tests in
> `src/Mobile/simf_app/test/features/sessions/session_detail_screen_test.dart`
> (detail render, guest→no card, reserver→card, no-booking→no card, speaker→profile,
> add-to-calendar toast, reminder-deferred toast, 404, error→retry) and
> `…/session_detail_models_test.dart` (`SessionDetail`/`MySeat` decode).

> **Reservation-only (2026-07-19).** Reserving / joining **confirms on
> create** (`Status = Approved`, no CP approval step) — there is no attendee-booking
> approval step in the flow. The reservation is a **provisional hold**: it is
> confirmed at the hall gate when staff scan the attendee's badge on check-in
> (`CheckedIn`), and a pre-start sweep releases any hold not checked in shortly before
> the session starts. No notification is sent on reserving; the app shows an inline
> success message. The old Control-Panel approval-queue surface (list-pending /
> approve / reject / bulk-approve, the CP Bookings page, the Approve/Reject
> permissions) is **retained but dormant** — nothing creates a Pending booking, so the
> queue is always empty. The join/reserve scenarios below have been reworded to this
> behaviour; the owner's exact two-case reserve-success messages + the 4-state seat map
> are finalised in the **app-side reservation-only slice**. The wire/data behaviour
> (auto-confirmed, no `BookingConfirmed` on reserve) applies now.
>
> **As-built deviations (D-300):** (1) the screen **fetches the detail by id**
> (`GET …/sessions/{id}`, deep-link / cold-start safe) rather than threading the
> p16 in-memory cache — the cross-screen cache is a later optimization; the detail
> is a superset of the list item. (2) **Add-to-calendar is real** (`add_2_calendar`,
> intent-based, no Android permission); the **Reminder is deferred** to the
> notifications platform pass — the CTA shows an interim notice (the server worker
> D-217 is the production reminder path). Speaker photo/flag render as
> initials/text until the asset pass (SIMF-VID-001).
>
> **Figma re-skin (frame 889:2450 "Session detail", restructured — D-449):** the
> page now matches the updated KSA-Project frame — a navy header card (gold index
> badge + title + clock/calendar meta line, 889:2716) whose action row carries
> **رابط الجلسة / Session link** (beige hairline; shown only when the session has a
> live feed — `liveStreamUrl` non-null — opening Live 25) and **ملخص الجلسة /
> Session summary** (gold hairline; opens AI summary 34) — the prior hall/category
> tag pills are **removed** (889:2715); the وصف الجلسة / Description card (889:2719);
> the المتحدثون / Speakers cards now showing a **40×40 photo + the country flag
> emoji** beside the name (889:2722/1060:12892), the role driving only the host
> sub-label; the **اسأل المحاور / Ask the host** card (centred user glyph → Send
> question 26, 1056:12876); the مقعدي / My seat card with the gold marker + chevron
> (889:2761); and the تذكير + أضف إلى تقويمي CTA row (897:2872). Scenarios
> E2E-MOB017-012..021 cover the new sections; the prior behaviour scenarios (001–011)
> remain valid. The flag renders from `PublicSessionSpeaker.CountryId` via the new
> `core/country_flag.dart` ISO-3166 numeric→emoji helper.
>
> **Public again (D-750, 2026-07-20, REVERSES D-576):** the Session-detail
> *screen* (#17) is **public** — a signed-out guest can open `/sessions/{id}` and
> read the session without signing in (restoring the D-199 public design). The
> join / ask sections stay hidden for a guest (the seat map is approved-only, so
> `seatMap == null` → no Join CTA, no reservation card, and the ask card is
> disabled), so the "guest" paths below (001, 005, 024) are the live behaviour,
> not just API-/widget-level guarantees. The now-public access is covered by the
> updated E2E-MOB017-025.
>
> **Session-state gating (owner 2026-07-14, supersedes the 2026-06-30 "always
> both active"):** the two header actions now gate on the session's phase
> (`SessionPhase` = upcoming/live/ended from `[start, end)`). **ملخص الجلسة**
> is active only once the session has ENDED (a future/live session has no محضر);
> **رابط الجلسة** only while the session is LIVE **and** carries a `liveStreamUrl`.
> Both slots always render (layout unchanged); a gated-off button greys out
> (navyDisabled tokens) and its tap is inert. The **Join** CTA likewise drops once
> the session has ended. Behaviour tests:
> `session_detail_body_test.dart` (future→summary inactive / ended→active /
> live→live active / future+feed→live inactive); the golden
> `session_detail_889-2450` renders an upcoming session, so both actions show
> greyed. Covered by E2E-MOB017-018/019/027.

| | |
|--|--|
| **Page** | [`Page_017`](../../App/Page_017/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/programme/sessions/{id}` (detail, anon) · `GET /api/v1/app/sessions/{id}/seats` (my-seat, approved) · app screen #17 `/sessions/:sessionId` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **Public screen (D-750, reverses D-576)** — the Session-detail screen is open to a signed-out guest; the join/ask sections stay hidden until an **approved Visitor** token is used (the my-seat / reservation card + the Join CTA need a held reservation on an approved account). An **Admin** token only to seed the session + seat layout. **No literal secrets** (admin TOTP via the `Get-Totp` helper). |
| **Last reviewed** | 2026-07-20 (D-750 — screen public again; case-1 register label + registered alert) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB017-001 | Anonymous opens the detail → title/time/hall/category/description/speakers | happy | P0 | authored ✓ (`Public_list_returns_active_sessions_to_an_anonymous_caller` + detail read) |
| E2E-MOB017-002 | Open by id → detail fetched (`GET …/sessions/{id}`), deep-link/cold-start safe | happy | P0 | authored ✓ (screen — `renders the detail`; as-built fetch-by-id, D-300) |
| E2E-MOB017-003 | Logged-in reserver → `مقعدي` card shows Row + Seat from `MyCell` | happy | P0 | authored ✓ (`Seat_map_returns_my_cell_for_the_reserver` + screen `…sees the seat card`) |
| E2E-MOB017-004 | Approved caller with no booking → `MyCell` null → no card | edge | P1 | authored ✓ (`Seat_map_my_cell_is_null…` + screen `…no reservation sees no card`) |
| E2E-MOB017-005 | Guest / unauthenticated → no seat call → no card (detail still renders) | auth | P0 | authored ✓ (`Seat_map_requires_an_approved_account` + screen `a guest sees no my-seat card`) |
| E2E-MOB017-006 | `عرض ←` → My Seat map (18) | happy | P1 | authored ✓ (screen — seat card `View` routes to `/sessions/:id/my-seat`) |
| E2E-MOB017-007 | Tap a speaker card → Speaker profile (20) | happy | P2 | authored ✓ (screen — `tapping a speaker navigates to the speaker profile`) |
| E2E-MOB017-008 | `أضف إلى تقويمي` builds a calendar event (client-local, no call) | happy | P1 | authored ✓ (screen — `Add-to-calendar shows the success toast`; `add_2_calendar`, D-300) |
| E2E-MOB017-009 | `تذكير` — interim notice (real scheduling deferred to the notifications pass) | happy | P2 | authored ✓ (screen — `Reminder shows the deferred-notice toast`, D-300) |
| E2E-MOB017-010 | Stale tap onto a soft-deleted session → detail 404 → "not found" state | error | P1 | authored ✓ (`ProgrammeSessionsTests` 404 + screen `a 404 shows the not-found state`) |
| E2E-MOB017-011 | RTL render; the row letter + seat number are LTR inside the Arabic line | i18n | P1 | authored (screen RTL-primary; LTR row/seat deferred to designer) |
| E2E-MOB017-012 | Header card — gold index badge (code, LTR), title, clock/calendar meta line, action buttons (no tag pills) | happy | P0 | authored ✓ (Figma 889:2716 re-skin) |
| E2E-MOB017-013 | وصف الجلسة / Description card renders the localized description; hidden when null | happy | P1 | authored ✓ (Figma 889:2719 re-skin) |
| E2E-MOB017-014 | المتحدثون speaker card → 40×40 photo + country flag beside the name | happy | P0 | authored ✓ (Figma 889:2722 re-skin; photo+flag) |
| E2E-MOB017-015 | المتحدثون host card → المضيف/Host sub-line (`SessionSpeakerRole.host`) | happy | P0 | authored ✓ (Figma 889:2737 re-skin; real role) |
| E2E-MOB017-016 | **Reservation card (D-485)** — a held (provisional) booking shows الصف · مقعد (or "general admission" for an open-seating join) + a "show your badge at the gate" check-in hint + a Cancel action; a seat booking's chevron/marker opens the seat map (18), an open-seating join has no map link | happy | P1 | authored ✓ (widget — reservation card: seat/general-admission + check-in hint + cancel) |
| E2E-MOB017-017 | CTA row — تذكير (outlined) + أضف إلى تقويمي (gold) order and toasts | happy | P1 | authored ✓ (Figma 897:2872 re-skin) |
| E2E-MOB017-018 | رابط الجلسة — **state-gated (owner 2026-07-14)**: both header buttons keep their slots, but رابط الجلسة is ACTIVE only while the session is LIVE **and** carries a `liveStreamUrl` (streaming) — greyed/inert otherwise; when active it opens Live (25) | happy | P1 | authored ✓ (`…session link opens the live screen while the session is live + streaming`; body-gate tests future+feed→inactive) |
| E2E-MOB017-019 | ملخص الجلسة — **state-gated (owner 2026-07-14)**: ACTIVE only once the session has ENDED (a future/live session has no محضر → greyed/inert); when active it opens AI summary (34) | happy | P1 | authored ✓ (`…summary button opens the AI session summary once the session has ended`; body-gate tests future→inactive / ended→active) |
| E2E-MOB017-020 | اسأل المحاور card — **NO join gate (#7 / D-733 superseded #3)**: enabled (opens Send question #26) for **any approved account** whose seat map has loaded, with or without a booking — the owner dropped the D-485 join/booking requirement for the pre-start ask ("anyone before start"). Only a **guest / pending** account (no seat map) sees it disabled, as the sign-in nudge. The `askHostJoinFirst` ("Join the session to ask a question") string is consequently **unreferenced by design** — see the A21 note below | happy/auth | P1 | authored ✓ (Figma 1056:12876; `#3 — a joined user can ask: the ask card opens send-question` + `#7 — an approved user can ask a FUTURE session without a booking (no join gate)`) |
| E2E-MOB017-026 | **Pre-session ask label (D-714 GAP-2)** — while the session is **upcoming** (`now < start`) the ask card reads the distinct pre-session label "اطرح سؤالاً قبل الجلسة" / "Ask a question before it starts" (mode B, `Phase=Pre`); once **live/started** it reverts to "اسأل المحاور" / "Ask the host" (mode A). The backend derives the phase + enforces the [start−5min, end] window either way | happy/i18n | P1 | authored ✓ (screen `a live (already started) session shows the "Ask the host" label` + the ask-label tests; golden `session_detail_889-2450` shows the pre-session label) |
| E2E-MOB017-021 | Speaker country flag — `CountryId` 682 → 🇸🇦 emoji beside the name | happy | P2 | authored ✓ (`…renders its flag emoji`; `core/country_flag.dart`) |
| E2E-MOB017-022 | **Join CTA (D-485; label + alert D-750)** — an approved user with no reservation sees a Join section, branched by the session's effective mode: assigned-seat → "الانضمام إلى الجلسة" / "Join the session" opens the seat picker; open-seating → the CTA reads "سجل لحضور الجلسة" / "Register to attend the session" and confirms then joins (Approved — confirmed immediately, no CP approval), showing a one-button success **alert** (`joinOpenSuccessBody`: "registering is not a seat reservation … confirmed at check-in") instead of a snackbar | happy | P1 | authored ✓ (widget — assigned→picker / open→register-label+confirm→join+success-alert; `D-750 — signed-in, open-seating …`) |
| E2E-MOB017-023 | **Cancel booking (D-485)** — the reservation card's Cancel confirms, then releases the held seat (`DELETE …/seats/mine`) and the section returns to the Join CTA | happy | P2 | authored ✓ (widget — `releaseMine`) |
| E2E-MOB017-024 | **Join is approved-only (D-485)** — a guest / pending account sees no join section (the seat endpoint 401/403s → null) | auth | P1 | authored ✓ (`…a guest sees no join section`) |
| E2E-MOB017-025 | **No-layout session joins (D-706)** — an assigned-seat session with **no seat layout** (a hall left on the default with no rows laid out) reports its effective mode as **OpenSeating**, so the Join CTA is a one-tap join (not an empty seat picker) and `…/seats/join` is accepted (Approved — confirmed immediately). Fixes "join session not working" | happy | P0 | authored ✓ (API `Join_succeeds_on_an_assigned_seat_session_that_has_no_layout`) |
| E2E-MOB017-025 | **Public screen (D-750, reverses D-576):** a signed-out guest navigating to `/sessions/{id}` opens the detail (no redirect); the join/ask sections stay hidden for a guest (`seatMap == null`) | auth | P0 | authored ✓ (router-gate `D-750 — a signed-out guest hitting /sessions or a session detail is NOT redirected`; `routePathRequiresAuth('/sessions/:sessionId')` is FALSE) |
| E2E-MOB017-027 | **Join gate (owner 2026-07-14):** the Join CTA is only offered while the session has NOT ended — an ended session drops the join section ("open now to join" is a live/upcoming state) | happy | P1 | authored ✓ (body-gate `phase != SessionPhase.ended`; screen join tests unaffected on upcoming fixtures) |
| E2E-MOB017-029 | **DEF-MOD-003/004 — the attendee-only affordances are not offered to an operational role:** the اسأل المحاور card and the Join / my-seat section open routes gated to Visitor+Exhibitor (#26 send-question, #18 my-seat, #109 seat picker), so a signed-in **Staff / Moderator** is shown neither (their seat map is not even fetched). A **guest** still sees the ask card DISABLED — that is the sign-in nudge, not a dead control. The routing rule is unchanged; the UI now matches it (`routeAllowsRole`) | auth | P0 | authored ✓ (screen `DEF-MOD-003/004: a MODERATOR is offered neither the ask card nor the join CTA`; body `DEF-MOD-003: a role that cannot open send-question is not offered the ask card at all`) |
| E2E-MOB017-030 | **DEF-MOD-008 — the moderate action reads the ROUTER's effective role:** an **unapproved** moderator (D-666: presents as guest via `effectiveAppRole`) is NOT shown the "إدارة الأسئلة" Q&A-desk action; an approved moderator still is. Previously the screen read the raw `appRole` and the router bounced the tap to Home | auth | P0 | authored ✓ (screen `DEF-MOD-008: an UNAPPROVED moderator is not shown the Q&A desk action`) |
| E2E-MOB017-028 | **Seat-map load failure retry (#18, owner 2026-07-21):** an **approved** attendee whose seat-map fetch FAILS (transport / 5xx) gets a "seat map failed to load" error + a **Retry** where the Join CTA would be — instead of a silently-absent Join button — and Retry re-runs the load. Distinct from E2E-MOB017-024: an approved account reaches the seat endpoint, so a null map means the fetch failed (not the guest/pending 403 that legitimately hides the join). Not offered on an ENDED session. | error | P1 | authored ✓ (screen `#18 — an approved account whose seat map FAILS…`; body `#18 seat-map load failure` ×2) |

## Scenarios

### E2E-MOB017-001 — Anonymous session detail

```gherkin
Feature: Session detail (public)
  As a guest (not logged in)
  I want to read one session in full
  So that I can decide to attend it

Scenario: The detail is readable without a token
  Given an active session "Opening Keynote" in "Main Hall" with a description and one speaker
  When an anonymous client calls GET /api/v1/app/programme/sessions/{id}
  Then the response is 200
  And it carries title, hall (EN+AR), the time window, the category tag, the description and the ordered speakers
```

**Evidence:** `ProgrammeSessionsTests` (the public detail read; the list+detail
share the projection asserted by
`Public_list_item_carries_the_body_and_speaker_cards`).

### E2E-MOB017-002 — Open by id (as-built, D-300)

```gherkin
Scenario: Opening the detail fetches the full session by id
  Given a session id (from an agenda tap, a deep link, or a cold start)
  When Session detail (17) opens
  Then it calls GET /app/programme/sessions/{id} and renders the header, tags,
       description and speaker cards
  And it works even when no p16 cache exists (deep-link / cold-start safe)
```

> **As-built (D-300):** the screen fetches the detail by id (the detail is a
> superset of the cached list item) rather than threading the p16 in-memory cache;
> cross-screen caching is a later optimization. The documented cache-first first
> paint (Page_017_Logic L-1, "may call detail") remains a valid future enhancement.

### E2E-MOB017-003 — My-seat card from MyCell (D-175)

```gherkin
Scenario: A reserver sees their row + seat
  Given an approved visitor who has reserved seat "B" / 4 for the session
  When the app calls GET /api/v1/app/sessions/{id}/seats with the visitor's token
  Then the response is 200
  And myCell.rowLabel is "B" and myCell.seatNumber is 4 and myCell.kind is "UserBooking"
  And the same seat appears in reservedCells
  And the screen shows "الصف B · مقعد 12"-style card
```

**Evidence:** `SeatReservationsTests.Seat_map_returns_my_cell_for_the_reserver` (green).

### E2E-MOB017-004 — No booking → no card

```gherkin
Scenario: A signed-in approved caller with no booking sees no seat card
  Given another visitor has booked a seat in the session
  And the caller is an approved visitor with no booking of their own
  When the app calls GET /api/v1/app/sessions/{id}/seats with the caller's token
  Then myCell is null
  And the other visitor's seat still appears in reservedCells
  And the screen renders the detail with no مقعدي card
```

**Evidence:** `SeatReservationsTests.Seat_map_my_cell_is_null_for_a_caller_without_a_reservation` (green).

### E2E-MOB017-005 — Guest → no card (auth gate)

```gherkin
Scenario: An unauthenticated caller cannot read the seat map
  Given an active session
  When an anonymous client calls GET /api/v1/app/sessions/{id}/seats with no token
  Then the response is 401
  And the app simply omits the مقعدي card (the anonymous detail still renders)
```

**Evidence:** `SeatReservationsTests.Seat_map_requires_an_approved_account` (green).

### E2E-MOB017-006 — View seat → My Seat (18)

```gherkin
Scenario: عرض ← opens the seat map screen from the same payload
  Given the مقعدي card is shown (myCell present)
  When the user taps "عرض ←"
  Then My Seat map (18) opens at /sessions/{sessionId}/my-seat
  And it renders the hall grid from the same SessionSeatMap (rowLabels, seatsPerRow, reservedCells, myCell)
  And no second seat-map fetch is required
```

### E2E-MOB017-007 — Speaker → profile (20)

```gherkin
Scenario: Tapping a speaker opens the speaker profile
  Given the detail lists an ordered set of speaker cards
  When the user taps a speaker card
  Then Speaker profile (20) opens at /speakers/{speakerId}
```

### E2E-MOB017-008 — Add to calendar (client-local)

```gherkin
Scenario: Add-to-calendar builds an event with no server call
  Given the session is cached (title, start, end, hall)
  And the device is offline
  When the user taps "أضف إلى تقويمي"
  Then the app builds one calendar event (title, start, end, location = hall) and hands it to the OS
  And no network request is made
```

### E2E-MOB017-009 — Reminder (interim — deferred, D-300)

```gherkin
Scenario: Reminder shows the deferred notice until the notifications pass
  When the user taps "تذكير"
  Then an interim notice is shown ("Reminders arrive with notifications setup")
  And no network request is made
```

> **As-built (D-300):** real local-notification scheduling is deferred to the
> notifications/platform-config pass (the regenerated `android/` strips the
> required manifest receivers + exact-alarm permission); the server reminder
> worker (D-217) is the production reminder path. The CTA is wired and shows the
> interim notice today.

### E2E-MOB017-010 — Soft-deleted session → 404

```gherkin
Scenario: A stale cached tap onto a removed session 404s
  Given a session that was active and is then soft-deleted by an admin
  When the app calls GET /api/v1/app/programme/sessions/{id}
  Then the response is 404 (SessionNotFound)
  And the screen shows a "session removed / not found" state
```

**Evidence:** `ProgrammeSessionsTests` (the detail 404 path on a missing /
soft-deleted session).

### E2E-MOB017-011 — RTL render

```gherkin
Scenario: The session detail renders right-to-left in Arabic
  Given the device locale is Arabic
  When the detail renders
  Then the layout, back chevron and عرض ← link are right-to-left
  And inside the seat card the row letter ("B") and seat number ("12") render left-to-right
  And times render in the device timezone
```

### E2E-MOB017-012 — Header card (Figma 889:2716/889:2715)

```gherkin
Scenario: The navy header card shows the index badge, title, meta line and action buttons
  Given session code "02" titled "ابتكارات الدفاع البحري" / "Naval Defence Innovations"
  And it runs 09:00 — 10:30 on Tuesday 16 Jun in hall "القاعة الرئيسية" / "Main Hall"
  When Session detail (17) renders the header card
  Then a gold 40×40 index badge shows "02" left-to-right
  And the session title "ابتكارات الدفاع البحري" reads beside the badge
  And the meta line shows a clock "09:00 — 10:30" (LTR) · a separator dot · a calendar "الثلاثاء · 16 يونيو"
  And an action row shows "ملخص الجلسة" (gold hairline) — and "رابط الجلسة" (beige hairline) only when the session has a live feed
  And the prior hall/category tag pills are NOT rendered (removed in the restructure)
```

### E2E-MOB017-013 — Description card (Figma 889:2719)

```gherkin
Scenario: The وصف الجلسة card renders the localized description, and is hidden when empty
  Given the session carries a description "جلسة حول أحدث تقنيات الدفاع البحري"
  When the detail renders in Arabic
  Then a section heading "وصف الجلسة" (EN "Description") appears
  And a navy card below it shows the description text "جلسة حول أحدث تقنيات الدفاع البحري"
  And given another session whose description is null
  Then neither the "وصف الجلسة" heading nor the description card is shown
```

### E2E-MOB017-014 — Speaker card → photo + country flag

```gherkin
Scenario: A speaker shows a 40×40 photo and the country flag beside the name
  Given the session lists a speaker "د. سالم العتيبي" / "Dr. Salem Al-Otaibi"
  And their role is SessionSpeakerRole.speaker with title "Captain" and CountryId 682 (Saudi Arabia)
  When the المتحدثون / Speakers section renders the speaker card
  Then a 40×40 rounded photo (SpeakerPhoto asset, beige hairline) sits at the inline-start (physical right under RTL)
  And the name line shows "د. سالم العتيبي" followed by the flag "🇸🇦"
  And the sub-line shows "Captain" and does NOT contain "المضيف" / "Host"
  And the sub-line no longer carries the country name (the flag carries the country)
  When the user taps the card
  Then Speaker profile (20) opens at /speakers/{speakerId}
```

### E2E-MOB017-015 — Host card → المضيف sub-line (real SessionSpeakerRole)

```gherkin
Scenario: A host (SessionSpeakerRole.host) shows the Host sub-label
  Given the session lists a host "أ. منى الشهري" / "Ms. Mona Al-Shehri"
  And their role is SessionSpeakerRole.host
  When the المتحدثون / Speakers section renders the host card
  Then the sub-line ends with "المضيف" (EN "Host")
  And the host marker is driven by the REAL role, not the list position
```

### E2E-MOB017-016 — My-seat card (Figma 889:2761)

```gherkin
Scenario: The مقعدي card shows row · seat, the badge hint and the gold marker
  Given an approved visitor holds reserved seat row "B" / 4 for the session
  When the مقعدي / My seat section renders
  Then the heading "مقعدي" (EN "My seat") appears
  And the card shows "الصف B · مقعد 4" (EN "Row B · Seat 4") over the hint "تأكد من إبراز بطاقتك عند الدخول" (EN "Show your badge at entry")
  And a forward chevron sits at the inline start and a gold filled marker box (labelled "عرض" / "View") at the inline end
  When the user taps the card
  Then My Seat map (18) opens at /sessions/{sessionId}/my-seat
```

### E2E-MOB017-017 — CTA row (Figma 897:2872)

```gherkin
Scenario: The تذكير + أضف إلى تقويمي buttons render in order and fire the right toasts
  Given the detail is loaded
  When the CTA row renders in Arabic (RTL)
  Then an outlined "تذكير" (EN "Reminder") button with a clock icon sits at the inline start (visually right)
  And a gold filled "أضف إلى تقويمي" (EN "Add to calendar") button with a calendar icon fills the remaining width (visually left)
  When the user taps "أضف إلى تقويمي" and the OS accepts the event
  Then the snackbar shows "تمت إضافة الجلسة إلى تقويمك" (EN "Added to your calendar")
  When the user taps "تذكير"
  Then the interim snackbar shows "ستتوفر التذكيرات مع إعداد الإشعارات." (EN "Reminders arrive with notifications setup.")
  And no network request is made by either CTA
```

### E2E-MOB017-018 — رابط الجلسة → Live (Figma 889:2715, state-gated owner 2026-07-14)

```gherkin
Scenario: The session-link button is active only while the session is live + streaming
  Given a session that is LIVE now (now within [start, end]) with a non-null liveStreamUrl
  When Session detail (17) renders the header card
  Then a "رابط الجلسة" (EN "Session link") button is shown and active
  When the user taps it
  Then Live broadcast (25) opens at /live?sessionId={id}
  # Both header buttons keep their slots (layout unchanged); a gated-off button
  # is greyed (navyDisabled tokens) and its tap is inert.
  Given a FUTURE session with a liveStreamUrl (the feed is not live yet)
  Then the "رابط الجلسة" button is present but INACTIVE (greyed; tapping does nothing)
  Given a session with no liveStreamUrl
  Then the "رابط الجلسة" button is INACTIVE
```

### E2E-MOB017-019 — ملخص الجلسة → AI summary (Figma 889:2715, state-gated owner 2026-07-14)

```gherkin
Scenario: The session-summary button is active only once the session has ended
  Given a FUTURE (or live) session detail
  When the header card renders
  Then a "ملخص الجلسة" (EN "Session summary") button is present but INACTIVE (greyed) —
    there is no محضر for a session that has not finished, so tapping does nothing
  Given a session that has ENDED
  Then the "ملخص الجلسة" button is active
  When the user taps it
  Then AI session summary (34) opens at /ai-summary?sessionId={id}
  And the summary screen 404s gracefully until the Committee publishes the summary
  # The summary stays reachable during a live window via the Session-summaries
  # list (#111), which filters on hasPublishedSummary (E2E-MOB111-010).
```

### E2E-MOB017-020 — اسأل المحاور → Send question (Figma 1056:12876)

```gherkin
Scenario: The ask-the-host card opens send-question for everyone
  Given any loaded session detail
  When the body renders between the speakers and the my-seat sections
  Then a navy card with a centred user glyph over "اسأل المحاور" (EN "Ask the host") is shown
  When the user taps it
  Then Send question (26) opens at /live/question?sessionId={id}
  And a guest tapping it is routed to sign-in by the auth gate (the route is login-only)

Scenario: No join/booking gate on the pre-start ask (#7 / D-733)
  Given an APPROVED account on an upcoming session
  And the account holds NO reservation (the seat map loaded, myCell is null)
  Then the ask card is ENABLED
  And no "Join the session to ask a question" hint is shown
```

**A21 (2026-07-27) — refuted, no change made.** The QA note read the card's
`enabled: seatMap != null` as a bug that made the `askHostJoinFirst` hint
unreachable, and asked for the "join the session first" gate to be restored.
It is **unreachable by design**: owner batch item **#7 (D-733, 2026-07-10)**
explicitly dropped the D-485 join/booking requirement for the pre-start ask
("anyone before start"), and `session_detail_screen_test.dart` guards that with
`#7 — an approved user can ask a FUTURE session without a booking (no join
gate)`, which asserts the hint is **not** shown. Re-gating on `seatMap?.myCell`
reverses D-733 and turns that regression test red. The stale artefact was this
row's own "gated on joining (#3)" wording, corrected above. The now-unreferenced
`askHostJoinFirst` string is left in place for the owner to retire or reuse — a
dead string is not a reason to reverse a shipped decision.

### E2E-MOB017-021 — Speaker country flag (core/country_flag.dart)

```gherkin
Scenario: A speaker's ISO 3166-1 numeric country code renders as a flag emoji
  Given a speaker whose CountryId is 682 (Saudi Arabia)
  When the speaker card renders
  Then the flag "🇸🇦" (U+1F1F8 U+1F1E6) appears beside the name
  And given a speaker whose CountryId is null or unassigned
  Then no flag (and no tofu box / wrong flag) is rendered
```

### E2E-MOB017-022 — Join this session (D-485)

```gherkin
Scenario: An approved attendee with no reservation joins, branched by mode
  Given an approved visitor on a session detail page holding no reservation
  When the session's effective seat-selection mode is AssignedSeat
  Then the Join CTA reads "الانضمام إلى الجلسة" / "Join the session"
  And tapping it opens the seat picker (case-2, assigned seat)
  When the mode is OpenSeating (general admission)
  Then the Join CTA reads "سجل لحضور الجلسة" / "Register to attend the session" (case-1, D-750)
  And tapping it shows a "Join this session?" confirm dialog
  And confirming sends the join (created Approved — confirmed immediately, no
    Control Panel approval step)
  And on success a one-button info alert is shown (not a snackbar) carrying
    joinOpenSuccessBody — "تم تسجيلك لحضور هذه الجلسة بنجاح. هذا التسجيل لا يعني
    حجز مقعد أو ضمان الدخول للجلسة …" / "You have successfully registered to attend
    this session. This registration does not reserve a seat or guarantee entry;
    your entry will be confirmed at session check-in." — dismissed by its OK button
  And the reservation is a provisional hold, confirmed at the hall gate when staff
    scan the badge on check-in (CheckedIn); a pre-start sweep releases any hold not
    checked in shortly before the session starts
  # No BookingConfirmed is sent on reserving. The old Control-Panel approval queue
  # (approve / reject → BookingConfirmed / BookingRejected) is retained but dormant —
  # nothing creates a Pending booking, so the queue is always empty.
  # A guest / pending account sees no join section (the seat endpoint 401/403s).
```

### E2E-MOB017-023 — Cancel a held booking (D-485)

```gherkin
Scenario: Cancelling a held reservation from the session page
  Given an approved visitor whose session detail shows a held reservation card
  When they tap "Cancel booking" and confirm
  Then the held seat is released (DELETE /app/sessions/{id}/seats/mine)
  And the section returns to the Join CTA
```

### E2E-MOB017-025 — Public screen (D-750, reverses D-576)

```gherkin
Feature: Session-detail screen — public access (D-750)
  As a signed-out guest
  I want to open a session and read it without signing in
  So that the programme is browsable before login (owner, D-750; reverses D-576)

Scenario: A guest opening a session detail sees the detail (no redirect)
  Given the app is signed out (a guest)
  When the app navigates to /sessions/{id} (row tap, deep link or cold start)
  Then the Session-detail screen renders (there is no redirect to sign-in)
  And the title / time / hall / description / speakers all show
  And no Join CTA, no reservation card and a disabled ask card are shown
    (the seat map is approved-only, so seatMap == null for a guest)
  # The detail read (GET /app/programme/sessions/{id}) stays AllowAnonymous.
  # My seat (18) stays attendee-gated, so a guest still cannot open the seat map.
```

**Evidence:** router-gate test `D-750 — a signed-out guest hitting /sessions or a
session detail is NOT redirected`; `routePathRequiresAuth('/sessions/:sessionId')`
is FALSE. Guest-no-join is `a guest sees no join section`.

### E2E-MOB017-027 — Join gate on an ended session (owner 2026-07-14)

```gherkin
Scenario: An ended session offers no Join CTA
  Given an approved visitor on a session that has ENDED (end in the past)
  And the visitor holds no reservation
  Then the "Join this session" section is NOT shown (you cannot join an over session)
  Given the same visitor on an UPCOMING or LIVE session with no reservation
  Then the Join CTA is shown (branched by the session's seat-selection mode)
```

**Evidence:** body-gate `else if (seatMap != null && phase != SessionPhase.ended)`;
the existing join screen tests use upcoming fixtures (join still offered).

### E2E-MOB017-028 — Seat-map load failure shows a retry (not a missing Join button)

```gherkin
Scenario: An approved attendee whose seat map fails to load can retry
  Given an approved visitor opens an UPCOMING session detail
  And the seat-map read (GET /app/sessions/{id}/seats) FAILS (transport / 5xx)
  Then the Join CTA is NOT shown
  And a "Could not load the seat map." message with a "Retry" button is shown in its place
  When the visitor taps Retry
  Then the screen re-runs its load (re-fetches the detail + seat map)

Scenario: An ENDED session with a failed seat map shows no retry
  Given the same approved visitor on an ENDED session whose seat map fails to load
  Then no seat-map error/retry is shown (the session can no longer be joined)
```

**Evidence:** screen `#18 — an approved account whose seat map FAILS to load sees the
error + retry`; body `#18 seat-map load failure` (error+retry on upcoming, nothing on
ended). An approved account reaches the seat endpoint, so a null map = a real failure —
distinct from the guest/pending 403 in E2E-MOB017-024.

### E2E-MOB017-029 / -030 — DEF-MOD-003/004/008 role-gated affordances

```gherkin
Scenario: A Moderator is offered no attendee-only affordance
  Given a signed-in APPROVED account whose app role is Moderator
  When they open /sessions/{id}
  Then the اسأل المحاور card is NOT rendered
  And no Join CTA / my-seat card is rendered
  And the seat map is not even fetched (no GET …/seats)
  And no "seat map failed to load" retry appears
  And the "إدارة الأسئلة" Q&A-desk action IS shown (route #104 allows them)

Scenario: A guest still gets the disabled ask nudge
  Given a signed-out visitor opens /sessions/{id}
  Then the اسأل المحاور card IS rendered, DISABLED (unchanged behaviour)

Scenario: An unapproved moderator is not offered the desk
  Given a signed-in moderator whose registration status is Pending
    (so effectiveAppRole collapses to guest — D-666)
  When they open /sessions/{id}
  Then the "إدارة الأسئلة" action is NOT shown
  # Previously it was shown and the router bounced the tap back to Home.
```

---

_Last reviewed:_ `2026-07-26` by `Claude` — **DEF-MOD-003/004/008: the ask card and
the join / my-seat section are gated with `routeAllowsRole` (the router's own table)
so an operational role is never offered a control that bounces; the screen now reads
`effectiveAppRole` (the router's role) instead of the raw `appRole`. New
E2E-MOB017-029/-030.**
_Prior:_ `2026-07-21` by `Apexium` — **#18 (owner 2026-07-21): an APPROVED
attendee whose seat-map fetch fails now shows a "seat map failed to load" error +
Retry where the Join CTA would be, instead of a silently-absent Join button
(`_seatMapError` on the screen drives a body error branch, reusing `l10n.seatMapError`).
New E2E-MOB017-028.**
_Prior:_ `2026-07-20` by `Apexium` — **D-750: the Session-detail screen is
public again (reverses the D-576 login-gate) — a guest opens the detail without
signing in (join/ask hidden while `seatMap == null`); the open-seating (case-1) Join
CTA now reads "سجل لحضور الجلسة" / "Register to attend the session" and shows a
one-button success alert (`joinOpenSuccessBody`) instead of a snackbar.
E2E-MOB017-022 / -025 reworded.**
_Prior:_ `2026-07-19` by `Apexium` — **reservation-only correction: a seat
reservation / open-seating join now confirms on create (`Status = Approved`, no
Control Panel approval step); the reservation is a provisional hold confirmed at the
hall gate on check-in (`CheckedIn`), with a pre-start sweep releasing any hold not
checked in. No `BookingConfirmed` on reserve; the app shows an inline success message.
The old CP approval queue is retained but dormant (always empty). Scenarios 016 / 022 /
025 reworded off the "pending approval" copy.**
_Prior:_ `2026-07-14` by `SIMF Team` — **owner state-gating: the two
header actions (ملخص الجلسة / رابط الجلسة) and the Join CTA now gate on the
session phase (upcoming/live/ended); a future session's summary button is
inactive; the live link is active only while live+streaming; an ended session
drops Join. Shared `SessionPhase` + `SessionStateChip`. E2E-MOB017-018/019/027.**
_Prior:_ `2026-07-10` by `SIMF Team` — **#7 (D-733): the "اسأل المحاور"
ask card is now FUTURE-ONLY — shown (and, for any approved account, enabled
without a booking) only while `start` is in the future; it is HIDDEN once the
session is live or ended (asking during a live session moves to the
live-broadcast screen; a past session's view is a recording, not a live
broadcast). Widget tests: approved-user-can-ask-future / live-hides / past-hides.**
_Prior:_ `2026-07-08`.
