# E2E test catalogue — `My Seat map` (`my-seat`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> page reuses the already-built per-session seat surface (D-175): the seat-map
> grid `GET /app/sessions/{id}/seats` and the reserve/release endpoints. API
> implementation lives in `tests/SIMF.Api.Tests/SeatReservationsTests.cs`. The
> **Flutter screen is built (D-301)** — **read-only as drawn** (L-4): the grid +
> derived status + navigate (→ Map 15) + native share. The interactive **picker**
> (009/010 reserve/release) is the documented later mode over the same shipped
> endpoints — not wired this page. Widget/model tests in
> `src/Mobile/simf_app/test/features/sessions/my_seat_screen_test.dart`
> (banner+grid+legend, share text, navigate, no-layout, 404, error→retry) and
> `…/seat_map_models_test.dart` (tolerant kind decode, grid + status derivation).
>
> **Figma parity (D-432, 2026-06-16):** the screen is rebuilt to the KSA-Project
> frame **898:2873 "Your seat"** on the shared navy shell — circled-back header,
> the navy "الجلسة / Session" card now showing the **real session title**
> (`sessionTitle` / `sessionTitleArabic`, newly carried on the seat-map response)
> with the الصف (Row) / مقعد (Seat) chips below it, the navy hall card with the
> gold "المسرح · STAGE" band, the A–H seat grid (mine / reserved / available),
> the محجوز / متاح / مقعدك legend, and the two gold actions
> (إرشادي إلى مقعدي / مشاركة الموقع). Behaviour is unchanged from the prior build.

| | |
|--|--|
| **Page** | [`Page_018`](../../App/Page_018/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/sessions/{id}/seats` (grid, approved) · `POST …/seats/reserve` · `…/reserve-random` · `DELETE …/seats/mine` · app screen #18 `/sessions/:sessionId/my-seat` (auth-gated) |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | An **approved Visitor** token (the page is login-only); an **Admin** token only to seed the session, the seat layout and a blocked row. **No literal secrets** (admin TOTP via the `Get-Totp` helper). |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB018-001 | Logged-in viewer gets the full grid (`rowLabels` + `seatsPerRow` + `reservedCells`) | happy | P0 | authored ✓ (`Seat_map_returns_the_layout_and_blocked_row_to_a_viewer`) |
| E2E-MOB018-002 | My seat is highlighted + the banner shows row/seat (`myCell`) | happy | P0 | authored ✓ (`Seat_map_returns_my_cell_for_the_reserver`) |
| E2E-MOB018-003 | An admin-blocked row renders as reserved (`AdminReservedRow`) | edge | P1 | authored ✓ (`Seat_map_returns_the_layout_and_blocked_row_to_a_viewer`) |
| E2E-MOB018-004 | A seat not in `reservedCells` renders available (status derivation) | happy | P1 | authored ✓ (model `reservedKeys`/`isMine` + screen grid) |
| E2E-MOB018-005 | No reservation → `myCell` null → no highlight, "no seat yet" banner | edge | P1 | authored ✓ (`Seat_map_my_cell_is_null…` + model) |
| E2E-MOB018-006 | Guest / unauthenticated → 401, screen gated out | auth | P0 | authored ✓ (`Seat_map_requires_an_approved_account`; route 18 in the gate) |
| E2E-MOB018-007 | `إرشادي إلى مقعدي` → Map (15) | happy | P1 | authored ✓ (screen — `navigate opens the venue map`) |
| E2E-MOB018-008 | `مشاركة الموقع` → native share sheet (seat-location text) | happy | P2 | authored ✓ (screen — `share sends the seat-location text`) |
| E2E-MOB018-009 | Picker: tap a free seat → reserve → grid repaints with `myCell` | happy | P1 | API ✓ (`Visitor_can_self_pick…`); **screen picker deferred (read-only, L-4)** |
| E2E-MOB018-010 | Picker: release my seat → `DELETE …/mine` → seat freed | happy | P2 | API ✓ (`Visitor_can_self_pick…`); **screen picker deferred (read-only, L-4)** |
| E2E-MOB018-011 | Hall with no layout → empty grid → "seat map not available" | edge | P2 | authored ✓ (screen — `an unconfigured hall shows the unavailable state`) |
| E2E-MOB018-012 | RTL render; stage stays top; row/seat are LTR | i18n | P1 | authored (screen RTL-primary; grid canvas forced LTR) |
| E2E-MOB018-013 | Session card shows the real session title from the seat-map response | happy | P0 | authored ✓ (frame 898:2873 — `_SessionCard` title) |
| E2E-MOB018-014 | No `sessionTitle` → card falls back to the seat location (or "no seat yet") | edge | P1 | authored ✓ (`localizedSessionTitle` null fallback) |
| E2E-MOB018-015 | الصف / مقعد chips below the title show the seat row + number (or "—") | happy | P1 | authored ✓ (frame 905:1576 — `_SeatChip`) |
| E2E-MOB018-016 | Stage band shows "المسرح · STAGE" above the A–H grid | happy | P2 | authored ✓ (frame 905:1584 — `_StageBar`) |

## Scenarios

### E2E-MOB018-001 — Full grid to a logged-in viewer

```gherkin
Feature: My Seat map (seat grid)
  As an approved, signed-in visitor
  I want to see the whole hall with every seat's status
  So that I can find my seat

Scenario: The seat map returns the layout and the occupied seats
  Given a session in a hall whose layout is rows A,B with 5 seats each
  And an admin has blocked row "A" and another visitor holds seat B/4
  When the viewer calls GET /api/v1/app/sessions/{id}/seats with an approved token
  Then the response is 200
  And rowLabels is ["A","B"] and seatsPerRow is 5
  And reservedCells contains every A-row seat with kind "AdminReservedRow"
  And reservedCells contains B/4
```

**Evidence:** `SeatReservationsTests.Seat_map_returns_the_layout_and_blocked_row_to_a_viewer` (green).

### E2E-MOB018-002 — My seat highlighted

```gherkin
Scenario: The viewer's own seat is returned as myCell
  Given the viewer has reserved seat "B" / 4
  When they call GET /api/v1/app/sessions/{id}/seats
  Then myCell.rowLabel is "B" and myCell.seatNumber is 4
  And the banner shows "صف B · مقعد 4" and the cell is highlighted in brass
```

**Evidence:** `SeatReservationsTests.Seat_map_returns_my_cell_for_the_reserver` (green).

### E2E-MOB018-003 — Admin-blocked row is reserved

```gherkin
Scenario: A blocked row reads as reserved, not available
  Given an admin has blocked row "A"
  When the seat map is fetched
  Then every "A" cell appears in reservedCells with kind "AdminReservedRow"
  And the grid renders those cells as محجوز (taken), not متاح (available)
```

**Evidence:** `SeatReservationsTests.Seat_map_returns_the_layout_and_blocked_row_to_a_viewer` (green).

### E2E-MOB018-004 — Available derivation

```gherkin
Scenario: A free seat renders as available
  Given seat C/3 is not in reservedCells and is within rowLabels × seatsPerRow
  When the grid renders
  Then C/3 is shown as متاح (available)
```

### E2E-MOB018-005 — No reservation

```gherkin
Scenario: A viewer with no booking sees no highlight
  Given another visitor holds a seat but the viewer holds none
  When the viewer fetches the seat map
  Then myCell is null
  And the grid renders with no highlighted cell and a "no seat yet" banner
```

**Evidence:** `SeatReservationsTests.Seat_map_my_cell_is_null_for_a_caller_without_a_reservation` (green).

### E2E-MOB018-006 — Auth gate

```gherkin
Scenario: An unauthenticated caller cannot read the seat map
  When an anonymous client calls GET /api/v1/app/sessions/{id}/seats with no token
  Then the response is 401
  And the screen is not reachable (the route is auth-gated)
```

**Evidence:** `SeatReservationsTests.Seat_map_requires_an_approved_account` (green).

### E2E-MOB018-007 — Navigate to the map

```gherkin
Scenario: Guide me to my seat opens the venue map
  Given the seat map is shown
  When the user taps "إرشادي إلى مقعدي"
  Then the Venue Map (15) opens
```

### E2E-MOB018-008 — Share

```gherkin
Scenario: Share location opens the native share sheet
  When the user taps "مشاركة الموقع"
  Then the native share sheet opens
  And no network request is made
```

### E2E-MOB018-009 — Picker: reserve a free seat

```gherkin
Scenario: Tapping a free seat books it and repaints the grid
  Given the viewer holds no seat and seat A/3 is free
  When the app calls POST /api/v1/app/sessions/{id}/seats/reserve with row A seat 3
  Then the response is 200 and the reservation is returned (held Pending)
  And re-reading the seat map shows myCell = A/3
```

**Evidence:** `SeatReservationsTests.Visitor_can_self_pick_then_release_their_seat` (green).

### E2E-MOB018-010 — Picker: release my seat

```gherkin
Scenario: Releasing frees the seat for re-booking
  Given the viewer holds seat A/3
  When the app calls DELETE /api/v1/app/sessions/{id}/seats/mine
  Then the response is 200
  And the seat A/3 becomes bookable again
```

**Evidence:** `SeatReservationsTests.Visitor_can_self_pick_then_release_their_seat` (green).

### E2E-MOB018-011 — No layout

```gherkin
Scenario: A hall with no seat layout shows a placeholder
  Given the session's hall has no seat layout configured
  When the seat map is fetched
  Then rowLabels is empty and seatsPerRow is 0
  And the screen shows a "seat map not available yet" placeholder
```

### E2E-MOB018-012 — RTL render

```gherkin
Scenario: The seat map renders right-to-left in Arabic
  Given the device locale is Arabic
  When the seat map renders
  Then the layout and back chevron are right-to-left
  And the stage stays at the top of the hall plan
  And the row letters and seat numbers render left-to-right inside the Arabic labels
```

### E2E-MOB018-013 — Session card shows the real session title

```gherkin
Scenario: The "الجلسة / Session" card shows the title carried on the seat-map response
  Given the seat-map response carries sessionTitle "Maritime Security Panel"
  And sessionTitleArabic "جلسة الأمن البحري"
  When the seat map renders for an Arabic-locale viewer
  Then the navy session card shows the gold label "الجلسة"
  And the card title reads "جلسة الأمن البحري" (the EN locale shows "Maritime Security Panel")
```

**Evidence:** screen `_SessionCard` (frame 905:1556) — `map.localizedSessionTitle(l10n.isArabic)`; model `seat_map_models.dart` decodes `sessionTitle` / `sessionTitleArabic`.

### E2E-MOB018-014 — Title fallback when absent

```gherkin
Scenario: With no session title the card falls back to the seat location, then the no-seat hint
  Given the seat-map response carries no sessionTitle and no sessionTitleArabic
  When the viewer holds seat "C" / 7
  Then the card title falls back to the seat location for C/7
  But when the viewer holds no seat
  Then the card title reads "لا يوجد لديك مقعد بعد" ("You have no seat yet")
```

**Evidence:** screen `_SessionCard` — `localizedSessionTitle(...) ?? (cell != null ? seatLocation : noSeatYet)`.

### E2E-MOB018-015 — Row / seat chips below the title

```gherkin
Scenario: The الصف / مقعد chips below the title show the booked seat
  Given the viewer holds seat "B" / 4
  When the session card renders
  Then a gold-bordered chip shows "الصف B" (Row) at the inline-start
  And a beige-bordered chip shows "مقعد 4" (Seat) at the inline-end
  But when the viewer holds no seat
  Then both chips show the placeholder value "—"
```

**Evidence:** screen `_SeatChip` (frame 905:1576/1577/1579) — `rowChipLabel` "الصف", `seatChipLabel` "مقعد", value or "—".

### E2E-MOB018-016 — Stage band above the grid

```gherkin
Scenario: The gold-bordered stage band sits at the top of the hall plan
  Given the hall card renders its A–H seat grid
  When the viewer reads the hall card
  Then a full-width gold-bordered band shows "المسرح · STAGE"
  And the band stays at the top above the first seat row in both RTL and LTR
```

**Evidence:** screen `_StageBar` (frame 905:1584) — `stageLabelBilingual` "المسرح · STAGE"; the grid is forced LTR so the stage stays on top (L-7).

---

_Last reviewed:_ `2026-06-16` by `SIMF Team`.
