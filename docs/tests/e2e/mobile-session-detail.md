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

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
