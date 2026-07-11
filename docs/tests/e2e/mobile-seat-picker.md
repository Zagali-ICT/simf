# E2E — Mobile · Seat picker (D-485)

| Field | Value |
|-------|-------|
| **Route** | `seatPicker` — `/sessions/:sessionId/pick-seat` (approved Visitor) |
| **Source** | [`seat_picker_screen.dart`](../../../src/Mobile/simf_app/lib/features/sessions/seat_picker_screen.dart) |
| **API** | `GET /app/sessions/{id}/seats` (draw) · `POST /app/sessions/{id}/seats/reserve` · `POST …/seats/reserve-random` |
| **Reached from** | The session page's **Select my seat** Join CTA (assigned-seat sessions only) |

The seat picker draws the hall grid for an **assigned-seat** session and lets the
attendee tap an **available** seat (or auto-pick) to hold it. The booking is
created **Pending** — the Control Panel approves it, and the approved/rejected
notification arrives in the inbox. On success the picker pops back so the session
page reloads to show the held reservation.

## Coverage matrix

| ID | Scenario | Type | Pri | Status |
|----|----------|------|-----|--------|
| E2E-MOBPICK-001 | Grid renders from `rowLabels × seatsPerRow`; the session title + "tap an available seat" hint show; reserved / own / available seats are coloured (محجوز · مقعدك · متاح) | happy | P1 | authored ✓ (widget — grid + hint + auto-pick CTA) |
| E2E-MOBPICK-002 | Tapping an **available** seat reserves it (`POST …/seats/reserve` row+seat), shows the "Reserved — pending approval" toast, and pops back (session page reloads → reservation card) | happy | P0 | authored ✓ (widget — tap A2 → reserveSeat row=A seat=2) |
| E2E-MOBPICK-003 | The **Auto-pick** button reserves the first free seat (`POST …/seats/reserve-random`) | happy | P1 | authored ✓ (widget — random CTA → reserveRandom) |
| E2E-MOBPICK-004 | Reserved / own seats are **inert** (not tappable); only available seats hold | happy | P1 | authored ✓ (widget — only `available` wires the gesture) |
| E2E-MOBPICK-005 | A generic reserve failure (e.g. seat taken, 409 `SEAT_ALREADY_RESERVED`) shows the "Couldn't reserve that seat" toast and keeps the picker usable — `_busy` resets, no pop | error | P1 | authored ✓ (widget — `failReserve` → toast + grid still present) |
| E2E-MOBPICK-006 | No layout / 404 / load error → the empty / not-found / error+retry states | edge | P2 | authored ✓ (screen states mirror My-Seat) |
| E2E-MOBPICK-007 | Approved-only — a guest / pending account is redirected to sign-in (route auth gate) or 401/403s at the seat call | auth | P1 | covered (router gate 109 + server) |
| E2E-MOBPICK-008 | **Auto-pick on a FULL session (item 4)** — when `reserve-random` returns `409 SEAT_SESSION_FULL` (the session/hall capacity cap the CP enforces), the picker shows the specific **"No places remain"** message (not the generic failure) and stays usable | error | P0 | authored ✓ (widget — `randomSessionFull` → "No places remain" toast, picker still present) |
| E2E-MOBPICK-009 | **Concurrent bookings never overbook (M-2)** — when several visitors race `reserve` / `reserve-random`, the server's post-insert backstop guarantees the active count never exceeds the declared capacity (CapacityOverride ?? Hall.Capacity); the losers get `409 SEAT_SESSION_FULL` | conflict | P1 | authored ✓ (API) |
| E2E-MOBPICK-010 | **A stale pending hold auto-expires (M-6)** — a Pending seat hold the CP never decides is auto-released after its hold window, freeing the seat for others | happy | P2 | authored ✓ (API worker) |

## Scenarios

### E2E-MOBPICK-002 — Reserve an available seat

```gherkin
Scenario: Picking a specific seat in an assigned-seat session
  Given an approved visitor on the seat picker for an assigned-seat session
  And seat A1 is reserved and A2 is available
  When they tap seat A2
  Then POST /app/sessions/{id}/seats/reserve is called with rowLabel=A, seatNumber=2
  And a "Reserved — pending approval" toast is shown
  And the picker pops back so the session page reloads to the reservation card
  And on the Control Panel's approval the visitor gets a BookingConfirmed notification
```

### E2E-MOBPICK-005 — Reserve failure keeps the picker usable

```gherkin
Scenario: A taken seat surfaces an error without freezing the screen
  Given an approved visitor on the seat picker
  When they tap a seat and the reserve fails (409 SEAT_ALREADY_RESERVED)
  Then a "Couldn't reserve that seat" toast is shown
  And the grid is still visible and the picker is not frozen (they can retry)
```

### E2E-MOBPICK-008 — Auto-pick respects the capacity maximum (item 4)

```gherkin
Scenario: Auto-pick on a full session says "no places remain"
  Given an approved visitor on the seat picker for a session at its capacity cap
  When they tap "Auto-pick a seat" and reserve-random returns 409 SEAT_SESSION_FULL
  Then the "No places remain" (لا توجد أماكن متبقية) message is shown — not the
    generic "Couldn't reserve that seat"
  And the picker stays usable (no pop, not frozen)
```

### E2E-MOBPICK-009 — Concurrent bookings never overbook (M-2)

```gherkin
Scenario: A race cannot push the session past its declared capacity
  Given a session whose effective capacity is 2 (CapacityOverride below the layout)
  When five approved visitors fire reserve-random at the same instant
  Then at most two receive 200 and the rest receive 409 SEAT_SESSION_FULL
  And the session's active reservation count is <= 2 (never overbooked)
  # The fail-closed backstop guarantees active <= cap; it may over-correct to
  # fewer under true parallelism, so no exact surviving count is asserted.
```

**Evidence captured:**
- API integration tests: `SeatReservationsTests.Concurrent_reserve_random_never_exceeds_capacity_override`, `SeatReservationsTests.Open_seating_join_capacity_is_enforced_under_concurrency`, `SeatReservationsTests.Capacity_override_below_layout_blocks_the_over_cap_reserve` (run on real SQL Server LocalDB)

### E2E-MOBPICK-010 — A stale pending hold auto-expires (M-6)

```gherkin
Scenario: The expiry worker releases a hold the CP never decided
  Given a visitor's Pending seat hold whose ExpiresUtc has passed
  And a second Pending hold whose window is still in the future
  When the PendingBookingExpiryWorker scan runs
  Then only the expired hold is released (ReleasedAt set, Status = Cancelled)
  And the freed seat can be reserved again
  And the future-dated hold, an Approved hold and an admin block are untouched
```

**Evidence captured:**
- API integration tests: `PendingBookingExpiryWorkerTests.Expiry_scan_releases_only_past_pending_holds`, `SeatReservationsTests.Reserving_stamps_an_expiry_on_the_hold`
- Reserve/random/join stamp `ExpiresUtc = CreatedAt + 24h`; an admin-reserved row never expires (ExpiresUtc null)

---

_Last reviewed:_ `2026-07-08` by `SIMF Team`.
