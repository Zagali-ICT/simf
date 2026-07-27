# E2E — Mobile · Seat picker (D-485)

| Field | Value |
|-------|-------|
| **Route** | `seatPicker` — `/sessions/:sessionId/pick-seat` (approved Visitor) |
| **Source** | [`seat_picker_screen.dart`](../../../src/Mobile/simf_app/lib/features/sessions/seat_picker_screen.dart) |
| **API** | `GET /app/sessions/{id}/seats` (draw) · `POST /app/sessions/{id}/seats/reserve` · `POST …/seats/reserve-random` |
| **Reached from** | The session page's **Select my seat** Join CTA (assigned-seat sessions only) |

The seat picker draws the hall grid for an **assigned-seat** session and lets the
attendee tap an **available** seat (or auto-pick) to hold it. The reservation is
**confirmed on create** (reservation-only, 2026-07-18 — `Status = Approved`, no
Control Panel approval step); it stays a **provisional hold** until the visitor
**checks in at the hall gate** (staff QR scan), which confirms the seat, and a
pre-start sweep releases any hold not checked in shortly before the session starts.
No notification is sent on reserving. On success the app shows a **one-button info
alert** (D-750, `seatReservedAlertBody`) explaining that the hold is released if the
visitor does not check in by 3 minutes before the session starts, to free the seat
for others; on **OK** the picker pops back so the session page reloads to show the
reservation. **A8 (2026-07-27):** the screen doc comment and `joinSeatHint` were still
describing the removed approval step ("then await approval"); both now state that the
seat is held immediately. The old Control Panel approval queue (list-pending / approve /
reject) is **retained but dormant** — nothing creates a Pending attendee booking, so it is
always empty.

> **Reserve-success alert (D-750, 2026-07-20).** The owner's exact reserve-success
> copy landed as a **one-button alert** (`seatReservedAlertBody`) shown on a
> successful hold, replacing the old *"Reserved — pending approval"* snackbar; the
> picker pops back only after the alert's **OK** is tapped. The wire/data assertions
> (`Status=Approved`, no `BookingConfirmed` on reserve) are unchanged.

## Coverage matrix

| ID | Scenario | Type | Pri | Status |
|----|----------|------|-----|--------|
| E2E-MOBPICK-001 | Grid renders from `rowLabels × seatsPerRow`; the session title + "tap an available seat" hint show; reserved / own / available seats are coloured (محجوز · مقعدك · متاح) | happy | P1 | authored ✓ (widget — grid + hint + auto-pick CTA) |
| E2E-MOBPICK-002 | Tapping an **available** seat reserves it (`POST …/seats/reserve` row+seat, confirmed on create — not pending approval), then shows a one-button success **alert** (D-750 `seatReservedAlertBody`: the 3-min pre-start check-in hold rule); on **OK** the picker pops back (session page reloads → reservation card) | happy | P0 | authored ✓ (widget — tap A2 → reserveSeat row=A seat=2 → alert → OK → pop) |
| E2E-MOBPICK-003 | The **Auto-pick** button reserves the first free seat (`POST …/seats/reserve-random`) | happy | P1 | authored ✓ (widget — random CTA → reserveRandom) |
| E2E-MOBPICK-004 | Reserved / own seats are **inert** (not tappable); only available seats hold | happy | P1 | authored ✓ (widget — only `available` wires the gesture) |
| E2E-MOBPICK-005 | A generic reserve failure (e.g. seat taken, 409 `SEAT_ALREADY_RESERVED`) shows the "Couldn't reserve that seat" toast and keeps the picker usable — `_busy` resets, no pop | error | P1 | authored ✓ (widget — `failReserve` → toast + grid still present) |
| E2E-MOBPICK-006 | No layout / 404 / load error → the empty / not-found / error+retry states | edge | P2 | authored ✓ (screen states mirror My-Seat) |
| E2E-MOBPICK-007 | Approved-only — a guest / pending account is redirected to sign-in (route auth gate) or 401/403s at the seat call | auth | P1 | covered (router gate 109 + server) |
| E2E-MOBPICK-008 | **Auto-pick on a FULL session (item 4)** — when `reserve-random` returns `409 SEAT_SESSION_FULL` (the session/hall capacity cap the CP enforces), the picker shows the specific **"No places remain"** message (not the generic failure) and stays usable | error | P0 | authored ✓ (widget — `randomSessionFull` → "No places remain" toast, picker still present) |
| E2E-MOBPICK-009 | **Concurrent bookings never overbook (M-2)** — when several visitors race `reserve` / `reserve-random`, the server's post-insert backstop guarantees the active count never exceeds the declared capacity (CapacityOverride ?? Hall.Capacity); the losers get `409 SEAT_SESSION_FULL` | conflict | P1 | authored ✓ (API) |
| E2E-MOBPICK-010 | **A stale pending hold auto-expires (M-6)** — a Pending seat hold the CP never decides is auto-released after its hold window, freeing the seat for others | happy | P2 | authored ✓ (API worker) |
| E2E-MOBPICK-011 | **Ragged layout + seat numbers + tap->select->confirm (D-767):** the picker renders each row at its own `seatCounts[i]` width, each seat shows its number, tap SELECTS (no reserve yet) and shows the `seatPickerSelectedChip` chip, then "Confirm my seat" (`seatPickerConfirmCta`) reserves | happy | P1 | authored ✓ (widget `hall_seat_map_test.dart` + `seat_picker_screen_test.dart`; regenerated golden `seat_picker.png`) |

## Scenarios

### E2E-MOBPICK-002 — Reserve an available seat

```gherkin
Scenario: Picking a specific seat in an assigned-seat session
  Given an approved visitor on the seat picker for an assigned-seat session
  And seat A1 is reserved and A2 is available
  When they tap seat A2
  Then POST /app/sessions/{id}/seats/reserve is called with rowLabel=A, seatNumber=2
  And the reservation is confirmed on create (Status = Approved, no Control Panel approval)
  And a one-button success alert is shown (D-750, not a snackbar) carrying
    seatReservedAlertBody — "تم حجز المقعد بنجاح سيتم الغاء الحجز في حالة عدم تسجيل
    الدخول للجلسة قبل 3 دقائق قبل بدء الجلسة …" / "Seat reserved successfully. The
    reservation will be cancelled if you do not check in by 3 minutes before the
    session starts, to free the seat for others." (no "pending approval")
  And no notification is sent on reserving
  And on OK the picker pops back so the session page reloads to the reservation card
  And the seat stays a provisional hold until it is confirmed at the hall-gate check-in
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
  Given a visitor's Pending seat hold whose Expires has passed
  And a second Pending hold whose window is still in the future
  When the PendingBookingExpiryWorker scan runs
  Then only the expired hold is released (ReleasedAt set, Status = Cancelled)
  And the freed seat can be reserved again
  And the future-dated hold, an Approved hold and an admin block are untouched
```

**Evidence captured:**
- API integration tests: `PendingBookingExpiryWorkerTests.Expiry_scan_releases_only_past_pending_holds`, `SeatReservationsTests.Reserving_stamps_an_expiry_on_the_hold`
- Reserve/random/join stamp `Expires = CreatedAt + 24h`; an admin-reserved row never expires (Expires null)

### E2E-MOBPICK-011 - Ragged layout, seat numbers, tap->select->confirm (D-767)

```gherkin
Scenario: A ragged layout renders, seats show numbers, and a two-step confirm reserves
  Given an approved visitor on the seat picker for an assigned-seat session
  And the seat-map response carries rowLabels ["VIP","A","B"] and seatCounts [4,10,10]
    (seatsPerRow = 10 = max(seatCounts), the append-only uniform fallback)
  When the grid renders
  Then row VIP draws 4 seats, row A 10, row B 10 (SessionSeatMap.seatsInRow(i))
  And each seat cell shows its seat number (tokens seatNumberOnDark / seatNumberOnGold)
  And reserved / your-seat cells show the a11y state icon (token seatStateIconSize) with a
    Semantics label reusing legendReserved / legendMine + the seat id
  When the visitor taps the available seat "A7"
  Then the seat is SELECTED and highlighted, and NO reserve call fires yet
  And a chip above the auto-pick button reads seatPickerSelectedChip("A", 7)
    = "Selected: Row A . Seat 7" / "المقعد المختار: الصف A . مقعد 7"
  When the visitor taps "Confirm my seat" (seatPickerConfirmCta = "Confirm my seat" / "تأكيد المقعد")
  Then POST /app/sessions/{id}/seats/reserve fires with rowLabel="A", seatNumber=7
  And on success the D-750 one-button alert shows (seatReservedAlertBody), then on OK the picker pops back
```

**Evidence captured:**
- Wire/model grounded: `SessionSeatMap.SeatCounts` (contract) -> `seat_map_models.dart` `seatCounts` / `seatsInRow(i)` / `maxSeatsPerRow` / widened `hasLayout`; tokens `seatNumberSize` / `seatStateIconSize` / `seatNumberOnDark` / `seatNumberOnGold`; l10n `seatPickerSelectedChip(row, seat)` = "Selected: Row {row} . Seat {seat}" / "المقعد المختار: الصف {row} . مقعد {seat}" and `seatPickerConfirmCta` = "Confirm my seat" / "تأكيد المقعد".
- **Implementation status (2026-07-25) - IMPLEMENTED (green).**
  The wire (`seatCounts`), the model (`seatsInRow` / `maxSeatsPerRow`), the tokens and
  the l10n above have landed AND the RENDER is wired: `hall_seat_map.dart` draws each
  row at its own `seatsInRow(i)` width, every available cell shows its seat NUMBER
  (`seatNumberOnDark` / `seatNumberOnGold`) and reserved / your-seat cells show the a11y
  state icon with a Semantics label, and `seat_picker_screen.dart` is a two-step
  tap->select->confirm (a `_selectedRow` / `_selectedSeat` selection + the
  `seatPickerSelectedChip` chip + the "Confirm my seat" `seatPickerConfirmCta` CTA that
  fires the reserve). Covered by the app widget tests `hall_seat_map_test.dart` +
  `seat_picker_screen_test.dart` and the regenerated golden `seat_picker.png`. See
  DECISIONS_LOG D-767.

---

_Last reviewed:_ `2026-07-25` by `Claude` (D-767 - added E2E-MOBPICK-011 for the ragged
layout + seat numbers + tap->select->confirm chip. Implemented and green: the
wire/model/tokens/l10n landed AND `hall_seat_map.dart` render + `seat_picker_screen.dart`
chip are wired, covered by `hall_seat_map_test.dart` + `seat_picker_screen_test.dart` and
the regenerated golden `seat_picker.png`).
Prior `2026-07-20` by `Apexium` (D-750 — a successful hold now shows the owner's one-button reserve-success alert `seatReservedAlertBody` (the 3-min pre-start check-in hold rule) instead of the "Reserved — pending approval" snackbar; the picker pops back only after OK. E2E-MOBPICK-002 reworded). Prior review `2026-07-19` by `Apexium` (reservation-only correction — reserve auto-confirms, no "pending approval" toast, no BookingConfirmed on reserve; confirmation is the hall-gate check-in; CP approval queue retained but dormant). Prior `2026-07-08` by `SIMF Team`.
