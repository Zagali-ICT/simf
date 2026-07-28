# My Seat (مقعدي) — mobile `/sessions/:sessionId/my-seat`

| Field | Value |
|---|---|
| Route | `/sessions/:sessionId/my-seat` (`RouteNames.mySeat`, page #18) · approved Visitor (login-gated) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/sessions/my_seat_screen.dart` (`MySeatScreen`) |
| Widgets | shared `lib/features/sessions/widgets/hall_seat_map.dart` (`HallSeatMapCard`, read-only config — D-600); screen-local `_SessionCard`/`_SeatChip`/`_Actions` |
| Figma node | `898:2873` ("Your seat", KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`) |
| Shell | `SimfPageShell` (`SimfTab.sessions`, title مقعدي) |
| API | `GET /app/sessions/{id}/seats` (approved-only; one read draws the whole hall). The **change-seat** action navigates to the picker, which owns `POST …/seats/move` |
| Providers | `seatMapRepositoryProvider` · `seatShareProvider` (native share, E3) |
| Tests | `test/features/sessions/my_seat_screen_test.dart`; golden `test/golden/my_seat_golden_test.dart` (`goldens/my_seat_898-2873.png`); E2E [`mobile-my-seat.md`](../../../tests/e2e/mobile-my-seat.md) |
| Legacy detail | `docs/App/Page_018/` — retained as the detailed historical spec |
| Status | ✅ Real — D-301 (built) → 898-2873 full parity (2026-06-19, device-verified) → **clean-code frozen (D-600)** → B1 change-seat action added (2026-07-27, owner request; golden re-locked) |

## 1. Purpose
The caller's seat for one session: the الجلسة card (session title + الصف/مقعد
chips), the read-only hall map (stage band, A–H grid with mine/reserved/available
derivation, legend), and the guide-me / share-location actions.

## 2. Audience & access
Approved Visitor only (the seat endpoint 401/403s otherwise; router-gated).

## 3. UI & behaviour
- One read draws everything; seat statuses are **derived** (mine / reserved /
  available — Page_018 L-2). Read-only: no seat is tappable here.
- The hall map is the shared `HallSeatMapCard` in its **read-only defaults**
  (beige available border, 20px seat cap per frame 902:1406, 14px legend
  swatches) — the same component the seat picker configures for selection
  (owner directive 2026-07-03, D-600).
- Grid geometry never mirrors in RTL (stage at top, venue order, L-7); the
  legend reads LTR (محجوز · متاح · مقعدك) exactly as the frame.
- States: loading spinner · 404 not-found · error+retry · no-layout (L-6).

## 4. Button / action audit (Level F, 2026-07-03)
| Control | Handler | Backend |
|---|---|---|
| Back (circled) | `backOrHome` | — |
| إرشادي إلى مقعدي (gold) | push `venueMap` #15 | — |
| مشاركة الموقع (outlined) | native share sheet (E3), seat text | client-local |
| تغيير المقعد (outlined, full width — B1) | push `seatPicker` #109 in CHANGE mode; on a `true` pop, `ref.invalidate(seatMapProvider)` | the picker calls `POST …/seats/move` |
| Retry | `_load()` re-fetch | `GET …/seats` |

Disabled share when no seat is held. All data repo-backed.

## 5. Clean-code freeze (D-600)
641 → 382 lines: the hall card (stage bar, grid rows, seat boxes, legend) moved
to the shared `HallSeatMapCard` — the seat picker's near-wholesale duplicate of
it was deleted in the same change. Golden captured at the exact frame size
(375×939) and overlay-verified against 898:2873 (legend crops pixel-order
identical; occupancy scatter is fixture data). Behaviour byte-identical.

## 6. Change seat (B1, owner request — 2026-07-27)
The owner's flow list names "change seat", which did not exist anywhere: this
page was read-only and the only path was cancel-then-rebook (lossy, and
impossible once the session starts). A visitor holding a **seat-specific**
reservation now gets a full-width **تغيير المقعد / Change seat** action on its
own line under the frame's two CTAs — the shipped `908:1733` action row is
untouched, so the frame parity holds and only the added line is new in the
re-locked golden. Its visible label is its accessible name (the leading icon is
decorative), and it inherits the page's RTL direction.

Tapping it pushes the existing **seat picker (#109)**, which detects the held
seat and opens in CHANGE mode (confirm step naming the old and new seat, then
the atomic `POST …/seats/move`) — no second picker was built. A `true` pop means
the move landed, so this page invalidates `seatMapProvider` and redraws on the
new seat. An **open-seating** join (general admission, no row/seat) is offered
no such action. Full behaviour, rules and the timing decision:
[`docs/pages/mobile/seat-picker/README.md`](../seat-picker/README.md) §6.
