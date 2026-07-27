# Seat Picker (اختيار المقعد) — mobile `/sessions/:sessionId/pick-seat`

| Field | Value |
|---|---|
| Route | `/sessions/:sessionId/pick-seat` (`RouteNames.seatPicker`, route #109, D-485) · approved Visitor (login-gated) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/sessions/seat_picker_screen.dart` (`SeatPickerScreen`) |
| Widgets | shared `lib/features/sessions/widgets/hall_seat_map.dart` (`HallSeatMapCard`, selectable config — D-600) |
| Figma node | none of its own — the owner directed it to **reuse the My-Seat hall card** (`898:2873`) in a selectable configuration (2026-07-03) |
| Shell | `SimfPageShell` (`SimfTab.sessions`) |
| API | `GET /app/sessions/{id}/seats` (draw) · `POST …/seats/reserve` (tap) · `POST …/seats/reserve-random` (auto-pick) · **`POST …/seats/move`** (B1 — change an existing seat, atomic) |
| Providers | `seatMapRepositoryProvider` |
| Tests | `test/features/sessions/seat_picker_screen_test.dart`; render-lock golden `test/golden/seat_picker_golden_test.dart` (`goldens/seat_picker.png`); E2E [`mobile-seat-picker.md`](../../../tests/e2e/mobile-seat-picker.md) |
| Status | ✅ Real — D-485 (built) → **clean-code frozen (D-600)** → B1 change-seat mode added (2026-07-27, a bug-fix/owner-request change, allowed under §13.3) |

## 1. Purpose
The assigned-seat join flow: tap an available seat (or auto-pick) to hold a
Pending reservation; pops `true` on success so the session page reloads.

## 2. Audience & access
Approved Visitor only; reached from the session-detail Join CTA when the
session is assigned-seat mode.

## 3. UI & behaviour
- The hall map is the shared `HallSeatMapCard` in its **selectable
  configuration**: `onSeatTap` set (available seats tappable with a
  `rowLabel+seat` Semantics button label; reserved/own inert), **gold**
  available-border as the tappable cue, 26px seat cap, 16px legend swatches —
  its pre-consolidation render, preserved (D-600).
- `busy` freezes the grid during a reserve call; `_hold` guards double-taps,
  resets busy in `finally`, and only pops/toasts when still mounted.
- States: loading · 404 · error+retry · no-layout.

## 4. Button / action audit (Level F, 2026-07-03)
| Control | Handler | Backend |
|---|---|---|
| Back (circled) | `backOrHome` | — |
| Available seat | confirmless reserve | `POST …/seats/reserve` |
| اختيار عشوائي (auto-pick CTA) | random hold | `POST …/seats/reserve-random` |
| Retry | `_load()` re-fetch | `GET …/seats` |

Success toast + pop(true); failure toast keeps the picker usable. All repo-backed.

## 5. Clean-code freeze (D-600)
468 → 218 lines: `_PickerHallCard`/`_PickerRow`/`_PickerSeat`/`_PickerLegend`
(a near-wholesale duplicate of the My-Seat hall card) deleted for the shared
`HallSeatMapCard` — one canonical seat map, configured per screen (owner
directive). Render-lock golden added; behaviour byte-identical (11/11 module
tests, including the tap-reserve and busy-reset paths).

## 6. Change-seat mode (B1, owner request — 2026-07-27)
The owner's flow list names "change seat", which did not exist: `/my-seat` was
read-only, the Join CTA became an inert card once a seat was held, and a second
reserve returned `409 SEAT_ALREADY_OWNED_BY_SESSION`. The only path was
cancel-then-rebook, which is lossy (the old seat is released and can be taken in
the gap) and impossible once the session starts.

This screen is now also the **destination chooser** for a move — no second
picker. When the seat map carries a **seat-specific** `myCell` it opens in
CHANGE mode:

| Element | Pick mode | Change mode |
|---|---|---|
| Shell + header title | `seatPickerTitle` | `seatChangeTitle` |
| Hint | `seatPickerHint` | `seatChangeHint` + the seat being left (`seatLocation`) |
| Primary CTA | `seatPickerConfirmCta` → `POST …/seats/reserve` | `seatChangeConfirmCta` → confirm dialog → `POST …/seats/move` |
| Auto-pick CTA | shown | **hidden** (a move is deliberate, not a lottery) |
| Success | `seatReservedAlertBody` | `seatChangedAlertBody(row, seat)` |

The confirm step is the shared `SimfConfirmDialog`, titled
`seatChangeConfirmTitle` with `seatChangeConfirmBody(fromRow, fromSeat, toRow,
toSeat)` naming **both** seats; its action reads `seatChangeCta` so the two
on-screen buttons never carry the same words. An **open-seating** join has no
seat to move and keeps the ordinary reserve behaviour.

**Atomicity.** `SeatReservationService.MoveAsync` acquires the destination and
releases the source inside ONE serializable transaction, run through the EF
execution strategy (the `InsertHoldWithinCapacityAsync` shape): the release is
saved first (the per-visitor filtered unique index would otherwise reject the new
row), then the insert; the filtered unique index on
`(SessionId, RowLabel, SeatNumber)` is the backstop, and firing it rolls the
release back with it. A lost race therefore leaves the visitor on their original
seat, and the app says so (`seatChangeTaken`) instead of showing a bare failure.

**Rules re-run on the destination:** seat bounds, seat **tier** eligibility via
the one shared rule (a non-VIP visitor cannot move into a VIP seat; nobody
self-moves into a VVIP seat), and — deliberately — the **cancel** timing
boundary: a self-service move is refused once the session has STARTED
(`409 BOOKING_SESSION_STARTED`), because from that moment the staff seating desk
works off the seat plan on the floor and the pre-start no-show sweep has already
redistributed the free seats. Capacity is untouched (a move is net-zero).
