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
| E2E-MOBPICK-005 | A reserve failure (seat taken / session full) shows the error toast and keeps the picker usable — `_busy` resets, no pop | error | P1 | authored ✓ (widget — `failReserve` → toast + grid still present) |
| E2E-MOBPICK-006 | No layout / 404 / load error → the empty / not-found / error+retry states | edge | P2 | authored ✓ (screen states mirror My-Seat) |
| E2E-MOBPICK-007 | Approved-only — a guest / pending account is redirected to sign-in (route auth gate) or 401/403s at the seat call | auth | P1 | covered (router gate 109 + server) |

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

---

_Last reviewed:_ `2026-06-21` by `SIMF Team`.
