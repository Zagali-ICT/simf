# Seat Picker (اختيار المقعد) — mobile `/sessions/:sessionId/pick-seat`

| Field | Value |
|---|---|
| Route | `/sessions/:sessionId/pick-seat` (`RouteNames.seatPicker`, route #109, D-485) · approved Visitor (login-gated) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/sessions/seat_picker_screen.dart` (`SeatPickerScreen`) |
| Widgets | shared `lib/features/sessions/widgets/hall_seat_map.dart` (`HallSeatMapCard`, selectable config — D-600) |
| Figma node | none of its own — the owner directed it to **reuse the My-Seat hall card** (`898:2873`) in a selectable configuration (2026-07-03) |
| Shell | `SimfPageShell` (`SimfTab.sessions`) |
| API | `GET /app/sessions/{id}/seats` (draw) · `POST …/seats/reserve` (tap) · `POST …/seats/reserve-random` (auto-pick) — booking created **Pending**, CP approves |
| Providers | `seatMapRepositoryProvider` |
| Tests | `test/features/sessions/seat_picker_screen_test.dart`; render-lock golden `test/golden/seat_picker_golden_test.dart` (`goldens/seat_picker.png`); E2E [`mobile-seat-picker.md`](../../../tests/e2e/mobile-seat-picker.md) |
| Status | ✅ Real — D-485 (built) → **clean-code frozen (D-600)** |

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
