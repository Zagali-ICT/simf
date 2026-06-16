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
> **Figma re-skin (frame 889:2450 "Session detail"):** the page now matches its
> KSA-Project Figma frame — a navy header card (gold index badge + title +
> clock/calendar meta line + hall/category tag pills, 889:2716), the وصف الجلسة /
> Description card (889:2719), the المتحدثون / Speakers cards whose gold-tinted
> role box renders an **anchor for a speaker / star for the host** driven by the
> real `SessionSpeakerRole` (889:2722/889:2757), the مقعدي / My seat card with the
> gold marker + chevron (889:2761), and the تذكير + أضف إلى تقويمي CTA row
> (897:2872). Scenarios E2E-MOB017-012..017 cover the new sections; the prior
> behaviour scenarios (001–011) remain valid.

| | |
|--|--|
| **Page** | [`Page_017`](../../App/Page_017/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/programme/sessions/{id}` (detail, anon) · `GET /api/v1/app/sessions/{id}/seats` (my-seat, approved) · app screen #17 `/sessions/:sessionId` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **Anonymous** for the detail. An **approved Visitor** token (seeded + a held reservation) for the my-seat card; an **Admin** token only to seed the session + seat layout. **No literal secrets** (admin TOTP via the `Get-Totp` helper). |
| **Last reviewed** | 2026-06-05 |

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
| E2E-MOB017-012 | Header card — gold index badge (code, LTR), title, clock/calendar meta line, hall + category tag pills | happy | P0 | authored ✓ (Figma 889:2716 re-skin) |
| E2E-MOB017-013 | وصف الجلسة / Description card renders the localized description; hidden when null | happy | P1 | authored ✓ (Figma 889:2719 re-skin) |
| E2E-MOB017-014 | المتحدثون speaker card → anchor role box for a speaker (`SessionSpeakerRole.speaker`) | happy | P0 | authored ✓ (Figma 889:2722 re-skin; real role) |
| E2E-MOB017-015 | المتحدثون host card → star role box + المضيف/Host sub-line (`SessionSpeakerRole.host`) | happy | P0 | authored ✓ (Figma 889:2757 re-skin; real role) |
| E2E-MOB017-016 | مقعدي card — row · seat line + badge hint + gold marker; chevron/marker opens seat map (18) | happy | P1 | authored ✓ (Figma 889:2761 re-skin) |
| E2E-MOB017-017 | CTA row — تذكير (outlined) + أضف إلى تقويمي (gold) order and toasts | happy | P1 | authored ✓ (Figma 897:2872 re-skin) |

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

### E2E-MOB017-012 — Header card (Figma 889:2716)

```gherkin
Scenario: The navy header card shows the index badge, title, meta line and tag pills
  Given session code "02" titled "ابتكارات الدفاع البحري" / "Naval Defence Innovations"
  And it runs 09:00 — 10:30 on Tuesday 16 Jun in hall "القاعة الرئيسية" / "Main Hall"
  And its category is "تقنية" / "Technology"
  When Session detail (17) renders the header card
  Then a gold 40×40 index badge shows "02" left-to-right
  And the session title "ابتكارات الدفاع البحري" reads beside the badge
  And the meta line shows a clock "09:00 — 10:30" (LTR) · a separator dot · a calendar "الثلاثاء · 16 يونيو"
  And a gold-accented place pill "القاعة الرئيسية" and a hairline category pill "تقنية" render below
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

### E2E-MOB017-014 — Speaker card → anchor role box (real SessionSpeakerRole)

```gherkin
Scenario: A speaker (SessionSpeakerRole.speaker) shows the gold anchor box
  Given the session lists a speaker "د. سالم العتيبي" / "Dr. Salem Al-Otaibi"
  And their role is SessionSpeakerRole.speaker with title "Captain" and country "السعودية" / "Saudi Arabia"
  When the المتحدثون / Speakers section renders the speaker card
  Then the card shows the name "د. سالم العتيبي" over the sub-line "Captain · السعودية"
  And the gold-tinted 44×44 role box on the inline-end carries the anchor glyph (NOT a star)
  And the sub-line does NOT contain "المضيف" / "Host"
  When the user taps the card
  Then Speaker profile (20) opens at /speakers/{speakerId}
```

### E2E-MOB017-015 — Host card → star role box + المضيف sub-line

```gherkin
Scenario: A host (SessionSpeakerRole.host) shows the gold star box and the Host tag
  Given the session lists a host "أ. منى الشهري" / "Ms. Mona Al-Shehri"
  And their role is SessionSpeakerRole.host
  When the المتحدثون / Speakers section renders the host card
  Then the gold-tinted role box carries the star glyph (Icons.star_outline), NOT the anchor
  And the sub-line ends with "المضيف" (EN "Host")
  And the anchor/star box is driven by the REAL role, not the list position
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

---

_Last reviewed:_ `2026-06-16` by `SIMF Team`.
