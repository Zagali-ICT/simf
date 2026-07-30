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
>
> **P3 per-element polish (2026-06-16):** the seat/row chip **values** (مقعد 12 /
> الصف B) are now both **white**, matching frame 905:1577/1579 — only the leading
> label word (مقعد / الصف) is gold. (The seat-chip value previously rendered gold;
> the `_SeatChip.valueIsGold` flag was removed as both values are white.)
>
> **Full 898:2873 parity (2026-06-19, commit `60458a5`; device-verified on
> TXZ W09):** the hall grid now draws **square** seats (≤20px) sized to the row
> width and centred — they fill a phone (frame width) but never stretch into wide
> rectangles on a tablet, with **no horizontal scroll**. **Available** seats are a
> transparent square with a beige hairline (the navyDeep card shows through),
> **reserved** a darker filled square, **mine** gold. The **legend is forced LTR**
> so it reads محجوز · متاح · مقعدك (label-then-swatch) like the frame instead of
> mirroring with the RTL page. The "إرشادي إلى مقعدي" button uses the exact
> `iconamoon:location` glyph (`assets/icons/ic_location.svg` via `SimfSvgIcon`).
> Behaviour is unchanged.

| | |
|--|--|
| **Page** | [`Page_018`](../../App/Page_018/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/sessions/{id}/seats` (grid, approved) · `POST …/seats/reserve` · `…/reserve-random` · **`POST …/seats/move`** (B1 change seat) · `DELETE …/seats/mine` · app screen #18 `/sessions/:sessionId/my-seat` (auth-gated) |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | An **approved Visitor** token (the page is login-only); an **Admin** token only to seed the session, the seat layout and a blocked row. **No literal secrets** (admin TOTP via the `Get-Totp` helper). |
| **Last reviewed** | 2026-06-19 |

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
| E2E-MOB018-017 | Legend reads محجوز · متاح · مقعدك (LTR, not mirrored); seat fills — available = beige-outline transparent, reserved = darker filled, mine = gold; seats are squares with no h-scroll | i18n/visual | P2 | authored ✓ (frame 907:1591 — `_Legend` forced LTR; `_SeatBox` fills; `_SeatRow` square clamp) |
| E2E-MOB018-018 | **Ragged read-only render (D-767):** a variable hall layout draws each row at its own `seatCounts[i]` width and highlights the viewer's own seat with its number + state icon | visual | P1 | authored ✓ (widget `my_seat_screen_test.dart` via the shared `hall_seat_map`; regenerated golden `my_seat_898-2873.png`) |
| E2E-MOB018-019 | **Change seat (B1):** a seat-specific hold shows a full-width **تغيير المقعد / Change seat** action under the frame's two CTAs; it opens the seat picker (109) in CHANGE mode, and a successful move (`true` pop) re-reads the grid so the new seat is drawn. An **open-seating** join (general admission) gets **no** such action | happy | P0 | authored ✓ (widget `my_seat_screen_test.dart` — CTA → picker → re-read; open-seating → no CTA; regenerated golden `my_seat_898-2873.png`) |
| E2E-MOB018-020 | **A12 / DEF-SEA-002 - the fourth seat state.** A held seat whose holder has scanned in at the hall gate draws CONFIRMED (green fill + `how_to_reg`, announced "Confirmed A1") and is visually distinct from a merely reserved seat (navy fill + close, "Reserved A2"); the تم التأكيد legend entry appears only while the hall holds one | visual | P1 | authored (widget `hall_seat_map_test.dart` + model `seat_map_models_test.dart`) |
| E2E-MOB018-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB018-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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
  Then the response is 200 and the reservation is returned (reserved and confirmed,
    Status=Approved — reservation-only, no CP approval step)
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

### E2E-MOB018-017 — Legend order + seat-state colours (frame 907:1591)

```gherkin
Scenario: The legend reads left-to-right and the seats carry the frame colours
  Given the seat map renders for an Arabic-locale (RTL) viewer
  Then the legend reads, left-to-right, "محجوز" (a darker filled swatch),
    then "متاح" (a beige-outline transparent swatch), then "مقعدك" (a gold swatch)
  And the legend does NOT mirror with the RTL page (its region is forced LTR)
  And in the grid an available seat is a transparent beige-outline square,
    a reserved seat a darker filled square, and my seat a gold square
  And every seat is a square that fits the row width with no horizontal scroll
```

**Evidence:** screen `_Legend` (frame 907:1591, forced `Directionality.ltr`, children محجوز/متاح/مقعدك) + `_SeatBox` (available `Colors.transparent` + beige border, reserved `navy` fill, mine `accent`); square sizing in `_SeatRow` (`LayoutBuilder` clamp ≤20, centred). Device-verified on TXZ W09 (commit `60458a5`).

### E2E-MOB018-018 - Ragged read-only render + own-seat number/icon (D-767)

```gherkin
Scenario: A variable layout renders ragged and my seat shows its number + state icon
  Given an approved visitor holds seat "A" / 7 in a hall whose layout is
    rowLabels ["VIP","A","B"] with seatCounts [4,10,10]
  When GET /api/v1/app/sessions/{id}/seats returns 200 with seatCounts [4,10,10] and myCell A/7
    (seatsPerRow = 10 = max(seatCounts), the append-only uniform fallback)
  And the My-Seat hall card renders (read-only; onSeatTap null)
  Then row VIP draws 4 seats, row A 10, row B 10 (SessionSeatMap.seatsInRow(i)), each showing its number
  And my seat A7 is the gold "mine" cell showing its number (token seatNumberOnGold) and the your-seat state icon
  And the grid stays forced-LTR (L-7) with no horizontal scroll, the short VIP row centred under the stage
```

**Evidence:**
- Wire/model grounded: `SessionSeatMap.SeatCounts` -> `seat_map_models.dart` `seatCounts` / `seatsInRow(i)` / `maxSeatsPerRow`; tokens `seatNumberOnGold` / `seatStateIconSize`. `my_seat_screen.dart` inherits all of this through the shared `HallSeatMapCard(map: map, l10n: l10n)` (no functional change of its own).
- **Implementation status (2026-07-25) - IMPLEMENTED (green).**
  The wire, model and tokens above have landed AND `hall_seat_map.dart` is wired for
  D-767: it draws each row at its own `seatsInRow(i)` width, every cell shows its seat
  number, and the viewer's own seat shows its number (`seatNumberOnGold`) plus the
  your-seat state icon (`seatStateIconSize`), with the short VIP row centred under the
  stage and no horizontal scroll. `my_seat_screen.dart` inherits all of this through the
  shared `HallSeatMapCard`. Covered by the app widget test `my_seat_screen_test.dart`
  and the regenerated golden `my_seat_898-2873.png`. See DECISIONS_LOG D-767.

### E2E-MOB018-019 - Change seat (B1)

```gherkin
Scenario: Changing seat from the My-Seat page
  Given an approved visitor whose myCell is the seat-specific reservation B2
  When the My-Seat page renders
  Then a full-width "تغيير المقعد" / "Change seat" action shows under the
    إرشادي إلى مقعدي / مشاركة الموقع row, its visible label being its accessible name
  When they tap it
  Then the seat picker route (109) opens for the same session, in CHANGE mode
  And when the picker pops `true` (the move landed) the seat map is re-read so the
    page redraws on the NEW seat

Scenario: General admission has no seat to move
  Given an approved visitor whose myCell.kind is OpenSeating (no row/seat)
  When the My-Seat page renders
  Then the share/navigate actions still show
  And NO "تغيير المقعد" / "Change seat" action is offered
```

**Evidence:**
- App widget tests: `my_seat_screen_test.dart` — "B1 — the change-seat action opens the picker and re-reads the grid when the move lands" and "B1 — an open-seating join offers no change-seat action" (both on a surface tall enough to build the lazily-built action area).
- Server side + the move rules: `docs/tests/e2e/mobile-seat-picker.md` E2E-MOBPICK-012..016 and `tests/SIMF.Api.Tests/SeatChangeTests.cs`.
- Golden `my_seat_898-2873.png` regenerated to include the new action.


---

_Last reviewed:_ `2026-07-27` by `Claude` (B1 change seat — added E2E-MOB018-019 for the تغيير المقعد action that opens the picker in CHANGE mode over the new atomic `POST …/seats/move`).
Prior `2026-07-25` by `Claude` (D-767 - added E2E-MOB018-018 for the ragged
### E2E-MOB018-019 - The fourth seat state renders (A12 / DEF-SEA-002)

```gherkin
Feature: A confirmed seat is visible in the app, not just in the Control Panel
  As an attendee (or a staff member at the seating desk)
  I want a seat whose holder has already arrived to look different from one merely held
  So that the hall map means the same thing in the app as it does on the CP live-hall page

Background:
  Given a session whose hall layout is rows "A" x 3 seats
  And seat A1 is held by an attendee who has an OPEN hall-attendance row (scanned in)
  And seat A2 is held by an attendee who has NOT scanned in

Scenario: The two held seats read differently
  When GET /api/v1/app/sessions/{id}/seats returns 200 with reservedCells
    [{rowLabel:"A",seatNumber:1,checkedIn:true},{rowLabel:"A",seatNumber:2,checkedIn:false}]
  And the hall card renders
  Then seat A1 draws the CONFIRMED state - the seatConfirmed green fill and the how_to_reg glyph
  And its Semantics announce "تم التأكيد A1" / "Confirmed A1"
  And seat A2 keeps the RESERVED state - the navy fill and the close glyph - announcing "محجوز A2" / "Reserved A2"
  And the legend gains a fourth entry "تم التأكيد" / "Confirmed" with the green swatch

Scenario: A hall nobody has entered keeps the shipped three-item legend
  When every reservedCell carries checkedIn = false (or omits the key entirely)
  Then no confirmed seat is drawn and the legend still reads محجوز · متاح · مقعدك only

Scenario: An older server that omits the key is safe
  When a reservedCell has no "checkedIn" key at all
  Then the seat decodes as not-yet-arrived and draws RESERVED (never confirmed)
```

**Evidence:**
- Root cause: `SeatCell.fromJson` never read the `checkedIn` wire key (shipped since Wave 2), so the app could not tell the two states apart even though `SessionSeatCell.CheckedIn` was on the response and the CP live-hall map rendered all four states.
- Fix: `seat_map_models.dart` decodes `checkedIn` and adds `reservedByKey()` + `hasConfirmed`; `hall_seat_map.dart` adds `_SeatStatus.confirmed` (fill `SimfTokens.seatConfirmed`, glyph `Icons.how_to_reg`, its own Semantics label) and the conditional legend entry; token `SimfTokens.seatConfirmed = #4FA37D` mirrors the CP `--color-seat-confirmed`.
- Tests: `test/features/sessions/widgets/hall_seat_map_test.dart` (two new cases - proven red before the fix: "Found 0 widgets with a semantics label named Confirmed A1") and `test/features/sessions/seat_map_models_test.dart` (four new cases). Both green; the whole `test/features/sessions` run is 154 passed with the 2 pre-existing `my_seat_screen_test.dart` failures.
- The same card serves the seat picker and the staff seating desk, so all three surfaces gain the state.

---

_Last reviewed:_ `2026-07-27` by `Claude` (A12 / DEF-SEA-002 - added E2E-MOB018-019 for the confirmed seat state, the fourth state the app could not render because `checkedIn` was dropped on decode). Prior: `2026-07-25` by `Claude` (D-767 - added E2E-MOB018-018 for the ragged
read-only render + own-seat number/icon. Implemented and green: the wire/model/tokens
landed AND `hall_seat_map.dart` render is wired, covered by `my_seat_screen_test.dart`
and the regenerated golden `my_seat_898-2873.png`). Prior: `2026-06-19` by `SIMF Team`.
