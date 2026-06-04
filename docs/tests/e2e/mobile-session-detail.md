# E2E test catalogue — `Session detail` (`session-detail`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> page reuses already-built endpoints (D-265): the public session reads (D-199 /
> D-252) and the per-session seat-map `MyCell` (D-175). API implementations live in
> `tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs` and
> `tests/SIMF.Api.Tests/SeatReservationsTests.cs`.

| | |
|--|--|
| **Page** | [`Page_017`](../../App/Page_017/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/programme/sessions/{id}` (detail, anon) · `GET /api/v1/app/sessions/{id}/seats` (my-seat, approved) · app screen #17 `/sessions/:sessionId` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **Anonymous** for the detail. An **approved Visitor** token (seeded + a held reservation) for the my-seat card; an **Admin** token only to seed the session + seat layout. **No literal secrets** (admin TOTP via the `Get-Totp` helper). |
| **Last reviewed** | 2026-06-03 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB017-001 | Anonymous opens the detail → title/time/hall/category/description/speakers | happy | P0 | authored ✓ (`Public_list_returns_active_sessions_to_an_anonymous_caller` + detail read) |
| E2E-MOB017-002 | First paint renders from the cached p16 item — no fetch required | happy | P0 | authored (screen) |
| E2E-MOB017-003 | Logged-in reserver → `مقعدي` card shows Row + Seat from `MyCell` | happy | P0 | authored ✓ (`Seat_map_returns_my_cell_for_the_reserver`) |
| E2E-MOB017-004 | Approved caller with no booking → `MyCell` null → no card | edge | P1 | authored ✓ (`Seat_map_my_cell_is_null_for_a_caller_without_a_reservation`) |
| E2E-MOB017-005 | Guest / unauthenticated → seat endpoint 401 → no card (detail still renders) | auth | P0 | authored ✓ (`Seat_map_requires_an_approved_account`) |
| E2E-MOB017-006 | `عرض ←` → My Seat map (18) reuses the same seat-map payload | happy | P1 | authored (screen) |
| E2E-MOB017-007 | Tap a speaker card → Speaker profile (20) | happy | P2 | authored (screen) |
| E2E-MOB017-008 | `أضف إلى تقويمي` builds a calendar event offline (client-local, no call) | happy | P1 | authored (screen) |
| E2E-MOB017-009 | `تذكير` schedules a local notification (client-local, no call) | happy | P1 | authored (screen) |
| E2E-MOB017-010 | Stale tap onto a soft-deleted session → detail 404 → "removed" state | error | P1 | authored ✓ (covered by the delete/404 detail test) |
| E2E-MOB017-011 | RTL render; the row letter + seat number are LTR inside the Arabic line | i18n | P1 | authored (screen) |

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

### E2E-MOB017-002 — First paint from cache

```gherkin
Scenario: Tapping an agenda row renders the detail from the cache
  Given the whole programme is cached from one fetch (Page_016)
  When the user taps a session row
  Then Session detail (17) renders the header, tags, description and speaker cards immediately
  And no GET /app/programme/sessions/{id} call is required for the first paint
  And the live seat count may refresh in the background
```

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

### E2E-MOB017-009 — Reminder (client-local)

```gherkin
Scenario: Reminder schedules a local notification with no server call
  Given the session start time is in the future
  When the user taps "تذكير"
  Then the app schedules a local notification before the session starts
  And no network request is made
```

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

_Last reviewed:_ `2026-06-03` by `SIMF Team`.
