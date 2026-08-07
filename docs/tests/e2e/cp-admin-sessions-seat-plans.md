# E2E test catalogue — Session seat plans (`/admin/sessions/seat-plans`)

| | |
|--|--|
| **Page** | [`cp/admin-sessions-seat-plans.md`](../../pages/cp/admin-sessions-seat-plans.md) _(authored D-767, 2026-07-25; page shape also captured inline below from `SessionSeatPlan.razor`, D-182 / D-215)_ |
| **Route** | `/admin/sessions/seat-plans` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-27 (DEF-SEA-001 / A11 holder + release confirmation) |

> **Page shape (read from `Components/Pages/Admin/SessionSeatPlan.razor`, D-182 + the P1.4/D-215 visual grid).**
> This is **not** a CRUD grid. It is a **session picker + seat tool**:
>
> 1. A `<select>` **"Select a session"** dropdown — populated from active
>    sessions, each option labelled `{Code} — {Title}`.
> 2. Once a session is chosen, a single text input **"Row to reserve (must
>    exist in the hall layout)"** + a **"Reserve row"** button (`ReserveRowAsync`).
> 3. Then **one of three** renders, depending on the selected session's hall:
>    - **Visual seat grid** (`_layout` present & has rows) — one `<button>` per
>      seat, coloured per `SeatReservationKind`; a **free** seat is clickable to
>      **reserve it for a VIP** (a single admin block, 2026-07-18) and a
>      **reserved** seat is clickable to **release** it. A **legend** (Free /
>      User / Admin / Random) + an `{N} active reservation(s)` summary line follow.
>    - **`SimfEmptyState`** "No active reservations on this session." — when the
>      hall has **no layout** AND there are zero reservations.
>    - **Fallback table** (Row / Seat / Kind / Actions, with a **"Release"**
>      button per row) — when the hall has no layout but reservations exist.
> 4. A top-of-surface `SimfAlert` toast (`success` / `error`) carries every
>    outcome message; it is cleared on session change.
>
> **Actions on the page:** (a) select a session, (b) reserve a whole row,
> (c) reserve a single free seat for a VIP (tap a free seat in the grid),
> (d) release one reservation — either by clicking a reserved seat in the grid,
> or via the table's "Release" button. There is **no Add / Edit / Details modal** and
> **no client-side validation** — every rule is enforced server-side and
> surfaced as an `error` toast via `Error.MessageForCurrentCulture()`.
>
> **Permission gate:** view = `PermissionCatalog.SeatPlans.View` (the
> `@attribute [RequirePermission(PermissionCatalog.SeatPlans.View)]` on the
> page); the row-reserve + release writes are gated by `SeatPlans.Edit` on the
> API. **`RequiredPermission` on the `Module.SessionSeatPlans` nav item =
> `SeatPlans.View`.** A signed-in admin lacking `SeatPlans.View` lands on
> `/not-permitted`.
>
> **Backend routes (via the CP BFF `/account/api/...`, all `simfAccount.*` JS interop):**
> - `POST /account/api/admin/sessions/list` → `ApiResult<GridPage<AdminSessionSummary>>` (fill the dropdown; `Top = 200`, filtered client-side to `IsActive`, ordered by `Code`).
> - `GET  /account/api/admin/halls/{hallId}/seat-layout` → `ApiResult<HallSeatLayoutSnapshot>` (load the grid; a 404/missing layout is **not** an error — page falls back to list/empty).
> - `POST /account/api/admin/sessions/{sessionId}/seats/list` → `ApiResult<GridPage<SessionSeatCell>>` (`Top = 500`; the active reservations).
> - `POST /account/api/admin/sessions/{sessionId}/seats/reserve-row` → `ApiResult<bool>` (Reserve row; `SeatPlans.Edit`; rate-limited under the `auth` limiter).
> - `POST /account/api/admin/sessions/{sessionId}/seats/reserve-seat` → `ApiResult<bool>` (Reserve ONE seat for a VIP; body `{"rowLabel","seatNumber"}`; `SeatPlans.Edit`; `auth` limiter).
> - `DELETE /account/api/admin/sessions/{sessionId}/seats/{reservationId}` → `ApiResult<bool>` (Release one reservation; `SeatPlans.Edit`; `auth` limiter).
>
> **Server error codes (from `SeatReservationService`, surfaced as the `error` toast):**
> - Reserve a row not in the hall layout → **`SeatOutOfBounds`** HTTP 400 — "Row '{row}' is not in the hall layout." / "الصف '{row}' غير موجود في مخطط القاعة."
> - Release a reservation id that does not exist / wrong session → **`SeatReservationNotFound`** HTTP 404 — "Seat reservation not found." / "لم يتم العثور على حجز المقعد."
> - Layout edit guards (`SeatLayoutInvalid`, `SeatCapacityExceeded`) belong to the **Hall seat layouts** page, not this one.
>
> **Known localization gap (real, verified):** the four legend strings
> (`Admin.SessionSeatPlans.Legend.Free|User|Admin|Random`) and the grid seat
> tooltip (`Admin.SessionSeatPlans.Seat.ReservedTitle`) are **referenced in the
> `.razor` but have NO entry in `Strings.resx` or `Strings.ar.resx`**. The
> `IStringLocalizer` therefore falls back to rendering the **resource key text
> itself** (e.g. the legend shows literally `Admin.SessionSeatPlans.Legend.Free`).
> E2E-SSP-009 asserts this so the gap is tracked rather than silently shipped.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SSP-001 | Golden path — pick session → reserve a row → release a seat (round-trip) | happy | P0 | _to author_ |
| E2E-SSP-002 | Select a session loads its reservations + seat grid | happy | P1 | _to author_ |
| E2E-SSP-003 | Reserve a whole row paints it admin-reserved + blocks visitor pick | happy | P1 | _to author_ |
| E2E-SSP-004 | Release one reservation by clicking a reserved seat in the grid | happy | P1 | _to author_ |
| E2E-SSP-005 | Fallback table render (hall with no layout) + table "Release" action | happy | P1 | _to author_ |
| E2E-SSP-006 | Empty state — session whose hall has no layout and no reservations | happy | P1 | _to author_ |
| E2E-SSP-007 | "No sessions available" empty state when no active sessions exist | happy | P2 | _to author_ |
| E2E-SSP-008 | Auth gate — admin lacking `SeatPlans.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SSP-009 | Validation — reserve a row not in the hall layout → `SeatOutOfBounds` 400 toast | error | P1 | _to author_ |
| E2E-SSP-010 | Conflict — reserve an already-admin-reserved row is idempotent (no duplicate) | error | P1 | _to author_ |
| E2E-SSP-011 | Release a stale/missing reservation → `SeatReservationNotFound` 404 toast | error | P2 | _to author_ |
| E2E-SSP-012 | Server 500 on `/seats/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-SSP-013 | RTL / Arabic render mirrors the page + grid + legend | i18n | P1 | _to author_ |
| E2E-SSP-014 | Legend + seat-tooltip resx keys missing → keys render literally (gap guard) | i18n | P2 | _to author_ |
| E2E-SSP-015 | VIP seat — tap a free seat → reserved as an admin block; a visitor can't book it; out-of-bounds seat → 400 | happy | P1 | authored ✓ (API `Admin_can_reserve_a_single_seat_for_a_vip` + `Admin_reserve_seat_out_of_bounds_is_400`) |
| E2E-SSP-016 | Ragged grid (D-767): a variable hall layout renders each row at its own `SeatCounts[i]` width; reserve-row / reserve-seat / release still work per-row; a short-row out-of-bounds seat -> 400 SEAT_OUT_OF_BOUNDS | happy | P1 | _to author_ (CP + service coded) |
| E2E-SSP-017 | **DEF-SEA-001** - clicking a held seat opens a confirmation naming the seat + holder; it does NOT release on the click | destructive | P1 | authored (bUnit `SessionSeatPlanReleaseConfirmTests`) |
| E2E-SSP-018 | **DEF-SEA-001** - the plan names every holder (roster + tooltip); a VVIP admin block shows its guest note, a note-less block shows "Admin block (no attendee)" | happy | P1 | authored (API `Seat_plan_list_names_the_holder_...` + `..._vvip_guest_note_...`) |
| E2E-SSP-019 | **A11** - the State column reads Confirmed for a checked-in holder and Reserved for one who has not scanned in (no longer the record defaults) | happy | P2 | authored (API `Seat_plan_list_names_the_holder_and_ships_the_real_status_and_check_in`) |
| E2E-SSP-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-SSP-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-SSP-001 — Golden path (pick → reserve row → release)

```gherkin
Feature: Session seat plan round-trip
  As an Administrator with SeatPlans.Edit
  I want to reserve a whole row and release individual seats for a session
  So that protocol seating is enforced before visitors self-pick

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (TOTP from the Get-Totp helper)
  And at least one active session exists whose hall has a seat layout (e.g. rows "A,B,C", 10 seats per row)
  And they have landed on /admin/sessions/seat-plans

Scenario: Reserve row B then release one of its seats
  Given the "Select a session" dropdown lists options labelled "{Code} — {Title}"
  When the administrator selects the session "S-001 — Opening Keynote"
  Then a POST /account/api/admin/sessions/{sessionId}/seats/list fires and returns 200
  And a GET /account/api/admin/halls/{hallId}/seat-layout fires and returns 200
  And the visual seat grid renders one button per seat for rows A, B, C
  And the summary line reads "0 active reservation(s)"

  When the administrator types "B" into "Row to reserve (must exist in the hall layout)"
  And clicks "Reserve row"
  Then a POST /account/api/admin/sessions/{sessionId}/seats/reserve-row with body {"rowLabel":"B"} fires and returns 200
  And a green toast reads "Row reserved." / "تم حجز الصف."
  And the "Row to reserve" input clears
  And every seat in row B now carries the admin-reserved colour class (seatgrid__seat--admin)
  And the summary line reads "10 active reservation(s)"

  When the administrator clicks seat "B5" in the grid
  Then a DELETE /account/api/admin/sessions/{sessionId}/seats/{reservationId} fires and returns 200
  And a green toast reads "Reservation released." / "تم إلغاء الحجز."
  And seat B5 returns to the free colour (no seatgrid__seat--reserved class)
  And the summary line reads "9 active reservation(s)"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-sessions-seat-plans-001-before.png` (empty grid, "0 active reservation(s)")
- Screenshot after reserve: `docs/screenshots/cp-admin-sessions-seat-plans-001-row-reserved.png` (row B painted admin colour)
- Screenshot after release: `docs/screenshots/cp-admin-sessions-seat-plans-001-after.png` (B5 freed, "9 active reservation(s)")
- Console errors: 0 expected
- Network: every `/account/api/admin/sessions/...` and `/account/api/admin/halls/...` call returns 200
- Audit row: `RowAudit` Insert rows for the AdminReservedRow reservations + a soft-release Update on the released row, with the actor id

### E2E-SSP-002 — Select a session loads reservations + grid

```gherkin
Scenario: Choosing a session loads its seat data
  Given the administrator is on /admin/sessions/seat-plans with no session selected
  And the "Reserve row" input and seat area are hidden (no session picked yet)
  When they select a session from the dropdown
  Then the loading text "Loading…" / "جارٍ التحميل…" appears briefly
  And POST /account/api/admin/sessions/{sessionId}/seats/list returns 200 with the SessionSeatCell list
  And GET /account/api/admin/halls/{hallId}/seat-layout returns 200 (or 404 → list fallback)
  And the "Row to reserve" input + "Reserve row" button become visible
  And any stale success toast from a previously-selected session is cleared
```

### E2E-SSP-003 — Reserve a whole row

```gherkin
Scenario: Reserve row A as admin-reserved
  Given a session is selected whose hall layout includes row "A"
  When the administrator types "A" into the row input and clicks "Reserve row"
  Then POST /seats/reserve-row {"rowLabel":"A"} returns 200
  And a green toast reads "Row reserved." / "تم حجز الصف."
  And the reservations reload and every seat in row A renders Kind=AdminReservedRow
  And in the fallback table view each row-A cell shows Kind "AdminReservedRow"
  And those seats are now off-limits to visitor self-pick (server marks them SEAT_ALREADY_RESERVED for visitors)
```

### E2E-SSP-004 — Release a seat from the grid

```gherkin
Scenario: Click a reserved seat to release it
  Given the seat grid is rendered and row B is fully admin-reserved
  When the administrator clicks the reserved seat button "B3"
  Then a DELETE /seats/{reservationId} for B3's ReservationId fires and returns 200
  And a green toast reads "Reservation released." / "تم إلغاء الحجز."
  And seat B3 is re-rendered as free (no reserved class)
  And a free seat is now clickable to reserve it for a VIP (2026-07-18); only the _busy flag disables seats
  And the summary count decrements by 1
```

### E2E-SSP-005 — Fallback table render (no hall layout)

```gherkin
Scenario: Hall without a layout falls back to the reservation table
  Given a session is selected whose hall has NO seat layout (GET /seat-layout returns no RowLabels)
  And that session has at least one active reservation
  When the page finishes loading
  Then the visual seat grid is NOT rendered
  And a table with columns Row / Seat / Kind / Actions is shown
  And each row has a "Release" / "إلغاء" button
  And the summary line reads "{N} active reservation(s)" / "{N} حجز نشط"

  When the administrator clicks "Release" on the first row
  Then DELETE /seats/{reservationId} returns 200
  And a green toast reads "Reservation released."
  And the table reloads with that row removed
```

### E2E-SSP-006 — Empty state (no layout, no reservations)

```gherkin
Scenario: SimfEmptyState when a session has neither a layout nor reservations
  Given a session is selected whose hall has no seat layout
  And that session has zero active reservations
  When the page finishes loading
  Then neither the grid nor the table renders
  And the SimfEmptyState shows "No active reservations on this session." / "لا توجد حجوزات نشطة لهذه الجلسة."
  And the "Reserve row" input + button remain visible (the admin can still seed a row, if the layout existed)
```

### E2E-SSP-007 — No sessions at all

```gherkin
Scenario: No active sessions hides the picker entirely
  Given the database has no active sessions (POST /admin/sessions/list returns an empty Items array)
  When the administrator opens /admin/sessions/seat-plans
  Then the SimfEmptyState shows "No sessions available." / "لا توجد جلسات متاحة."
  And the session "Select a session" dropdown is NOT rendered
  And no error toast appears
```

### E2E-SSP-008 — Auth gate

```gherkin
Scenario: Admin lacking SeatPlans.View is denied
  Given a signed-in admin whose roles grant no SeatPlans.View permission
  When they navigate to /admin/sessions/seat-plans
  Then they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/sessions/list request fires
  And the "Module.SessionSeatPlans" nav item is hidden for that user (RequiredPermission = SeatPlans.View)
```

### E2E-SSP-009 — Validation: row not in the layout

```gherkin
Scenario: Reserve a row that does not exist in the hall layout
  Given a session is selected whose hall layout has rows "A,B,C"
  When the administrator types "Z" into the row input and clicks "Reserve row"
  Then POST /seats/reserve-row {"rowLabel":"Z"} returns HTTP 400 with ApiResult.Error.Code = "SeatOutOfBounds"
  And a red toast surfaces the bilingual message
       "Row 'Z' is not in the hall layout." / "الصف 'Z' غير موجود في مخطط القاعة."
  And no new reservation appears in the grid / table
  And the row input retains the typed value (only success clears it)
```

### E2E-SSP-010 — Conflict / idempotent re-reserve

```gherkin
Scenario: Re-reserving an already admin-reserved row does not duplicate seats
  Given row "B" of the selected session is already fully AdminReservedRow
  When the administrator types "B" and clicks "Reserve row" again
  Then POST /seats/reserve-row {"rowLabel":"B"} returns HTTP 200 (the service skips seats already reserved — SeatAlreadyReserved races are swallowed)
  And a green toast reads "Row reserved." / "تم حجز الصف."
  And the reservation count for row B is unchanged (no duplicate seat rows)
```

### E2E-SSP-011 — Release a stale / missing reservation

```gherkin
Scenario: Releasing a reservation id that no longer exists returns 404
  Given the grid was rendered, then the same reservation was released in another tab
  When the administrator clicks the now-stale seat (or the API is fed a non-existent reservationId)
  Then DELETE /seats/{reservationId} returns HTTP 404 with ApiResult.Error.Code = "SeatReservationNotFound"
  And a red toast surfaces "Seat reservation not found." / "لم يتم العثور على حجز المقعد."
  And no green "released" toast appears
```

### E2E-SSP-012 — Server 500 on list

```gherkin
Scenario: API 500 on /seats/list shows the bilingual fallback toast
  Given the API is configured to return 500 on POST /admin/sessions/{sessionId}/seats/list (e.g. DB down)
  When the administrator selects a session
  Then the "Loading…" text shows briefly
  And then a red toast reads "Could not load session seat plan." / "تعذّر تحميل مخطط مقاعد الجلسة."
  And neither the grid nor the table renders any rows
```

### E2E-SSP-013 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page, grid and legend
  Given the administrator is on /admin/sessions/seat-plans in English with a session + grid loaded
  When they switch the language to العربية in the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "مخطط مقاعد الجلسات"
  And the hint reads "يمكن للمسؤول حجز صف كامل كمحجوز إدارياً (يمنع اختيار الزوار) أو إلغاء حجوزات فردية."
  And the "Select a session" label reads "اختر جلسة"
  And the row input label reads "الصف المراد حجزه (يجب أن يكون موجوداً في مخطط القاعة)"
  And the "Reserve row" button reads "حجز الصف"
  And the seat grid rows mirror right-to-left
  And the summary line reads "{N} حجز نشط"
```

### E2E-SSP-014 — Missing legend / tooltip resx keys (gap guard)

```gherkin
Scenario: Legend and seat-tooltip strings are not localized
  Given a session with a seat layout is selected so the grid + legend render
  When the administrator inspects the legend under the grid
  Then the four legend labels render the raw resource keys
       "Admin.SessionSeatPlans.Legend.Free", ".Legend.User", ".Legend.Admin", ".Legend.Random"
       (because no Strings.resx / Strings.ar.resx entry exists for them)
  And the title attribute of a reserved seat resolves the missing
       "Admin.SessionSeatPlans.Seat.ReservedTitle" key as its literal name with the {0}/{1} placeholders
  # This scenario is a tracked-defect guard, NOT a passing assertion of correct copy.
  # Fix = add the five keys to both resx files, then update this scenario to assert the real bilingual labels.
```

### E2E-SSP-015 — Reserve a single seat for a VIP (2026-07-18)

```gherkin
Feature: Session seat plan — reserve one seat for a VIP
  As an Administrator with SeatPlans.Edit
  I want to hold one specific seat for a VIP
  So that a named guest's seat is kept while everyone else self-picks

Background:
  Given an Administrator has signed in and landed on /admin/sessions/seat-plans
  And an active session "S-001 — Opening Keynote" is selected whose hall layout has rows "A,B,C" (3 seats per row)

Scenario: Tap a free seat to reserve it for a VIP
  Given seat A2 is free (no reserved colour class) and its title reads "Reserve seat A2 for a VIP"
  When the administrator clicks the free seat "A2"
  Then a POST /account/api/admin/sessions/{sessionId}/seats/reserve-seat with body {"rowLabel":"A","seatNumber":2} fires and returns 200
  And a green toast reads "Seat reserved for a VIP." / "تم حجز المقعد لأحد كبار الشخصيات."
  And seat A2 now carries the admin-reserved colour class (seatgrid__seat--admin)
  And the summary count increments by 1

Scenario: A visitor can no longer book that seat, but its neighbour is free
  Given seat A2 has been reserved for a VIP by the admin
  When an approved visitor calls POST /api/v1/app/sessions/{sessionId}/seats/reserve with row A seat 2
  Then the API returns HTTP 409 with ErrorCodes.SeatAlreadyReserved
  And the same visitor booking seat A1 returns HTTP 200 (the neighbour stays free)

Scenario: Re-reserving the same seat, or an out-of-bounds seat, is refused
  Given seat A2 is already reserved for a VIP
  When the administrator reserves seat A2 again
  Then reserve-seat returns HTTP 409 (SeatAlreadyReserved)
  When the administrator reserves seat A9 (beyond the 3-seat row width)
  Then reserve-seat returns HTTP 400 with ErrorCodes.SeatOutOfBounds
```

**Evidence captured:**
- API integration tests: `SeatReservationsTests.Admin_can_reserve_a_single_seat_for_a_vip`, `SeatReservationsTests.Admin_reserve_seat_out_of_bounds_is_400`
- The reserved seat is an `AdminReservedRow` block with a null attendee (`Status=Approved`), released like any admin block via the existing `DELETE /seats/{reservationId}`.

---

### E2E-SSP-016 - Ragged seat grid renders each row at its own width (D-767)

```gherkin
Feature: Session seat plan on a variable (ragged) hall layout
  As an Administrator with SeatPlans.Edit
  I want the seat grid to draw each row at its own seat count
  So that a 4-seat VIP row above 10/10 general rows is shown and managed correctly

Background:
  Given an active session whose hall has a RAGGED layout: rows "VIP,A,B" with seat counts 4,10,10
  And an Administrator has signed in and landed on /admin/sessions/seat-plans

Scenario: The visual grid paints each row at its own count
  When the administrator selects that session
  Then GET /account/api/admin/halls/{hallId}/seat-layout returns 200 with SeatCounts=[4,10,10]
  And the visual seat grid renders row VIP with exactly 4 seat buttons (VIP1..VIP4), row A with 10, row B with 10
  # outer @for over _layout.RowLabels.Count, inner bound = SeatsInRow(r) = SeatCounts[r] (falls back to SeatsPerRow)
  And NO phantom seat button appears past a short row's count (VIP stops at VIP4)

Scenario: Reserve-row, reserve-seat and release all work per-row on the ragged grid
  When the administrator types "VIP" and clicks "Reserve row"
  Then POST /account/api/admin/sessions/{sessionId}/seats/reserve-row {"rowLabel":"VIP"} returns 200
  And exactly 4 seats (VIP1..VIP4) turn admin-reserved (seatgrid__seat--admin) and the summary reads "4 active reservation(s)"
  When the administrator clicks the free seat "A7"
  Then POST /account/api/admin/sessions/{sessionId}/seats/reserve-seat {"rowLabel":"A","seatNumber":7} returns 200 and A7 turns admin-reserved
  When the administrator clicks the reserved seat "VIP2"
  Then DELETE /account/api/admin/sessions/{sessionId}/seats/{reservationId} returns 200 and VIP2 returns to free

Scenario: A short-row out-of-bounds seat is still guarded server-side
  When a reserve-seat is forced for rowLabel "VIP", seatNumber 5 (beyond VIP's 4 seats)
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SEAT_OUT_OF_BOUNDS"
  And the message reads "Seat number must be between 1 and 4." / "يجب أن يكون رقم المقعد بين 1 و 4."
```

**Evidence captured:**
- Grounded in `SessionSeatPlan.razor` (outer `@for (var r = 0; r < _layout.RowLabels.Count; r++)`, inner `@for (var seat = 1; seat <= SeatsInRow(r); seat++)`) and `SessionSeatPlan.razor.cs` `SeatsInRow(rowIndex) => _layout.SeatCounts is { Count: > 0 } sc && rowIndex < sc.Count ? sc[rowIndex] : _layout.SeatsPerRow`.
- Server bound: `SeatReservationService.ValidateSeatBounds` uses the per-row `ctx.SeatCounts[i]`, message "Seat number must be between 1 and {n}." (`ErrorCodes.SeatOutOfBounds`).
- The same ragged index loop lands on the **live-hall** grid (`SessionSeatPlan` twin `SessionLiveHall.razor` + `.razor.cs` `SeatsInRow`), read-only against `SessionSeatMap`.
- **API coverage:** the server-side short-row bound is backed by `SeatReservationsTests.Variable_layout_bounds_each_row_by_its_own_seat_count` (within the wider D-767 set of 10 new variable-layout facts, suite 44/44 passing); the Chrome DevTools MCP run remains the browser-level E2E for the CP ragged-grid UI.

---

### E2E-SSP-017 - A held seat asks before it is released (DEF-SEA-001)

```gherkin
Feature: Releasing a seat on the session seat plan is a confirmed, named action
  As an Administrator with SeatPlans.Edit
  I want to be told whose seat I am about to take, and to confirm it
  So that a single stray click cannot destroy a live attendee reservation

Background:
  Given an active session "S-SEAT" whose hall has the uniform layout rows "A" x 3 seats
  And the visitor "Faisal Al-Harbi" / "فيصل الحربي" holds seat A2 (Approved, not checked in)
  And an Administrator has signed in and landed on /admin/sessions/seat-plans
  And the administrator has selected session "S-SEAT"

Scenario: Clicking a held seat opens the confirmation instead of releasing it
  When the administrator clicks the held seat "A2"
  Then NO DELETE /account/api/admin/sessions/{sessionId}/seats/{reservationId} request is issued
  And a must-decide dialog opens titled "Release this seat?" / "إلغاء حجز هذا المقعد؟"
  And its body reads "Seat A2 is held by Faisal Al-Harbi. Releasing it frees the seat for someone else and cannot be undone. The attendee is notified."
  And the dialog lists Seat = "A2", Held by = "Faisal Al-Harbi", State = "Reserved"
  And the confirm button is the danger-styled "Release" and the cancel button reads "Keep the seat"

Scenario: Cancelling keeps the seat
  When the administrator clicks "Keep the seat"
  Then the dialog closes
  And NO DELETE request is issued and seat A2 is still held by Faisal Al-Harbi

Scenario: Confirming releases the seat and notifies the attendee
  When the administrator clicks the held seat "A2" and then "Release"
  Then DELETE /account/api/admin/sessions/{sessionId}/seats/{reservationId} returns 200
  And the toast reads "Reservation released." / "تم إلغاء الحجز."
  And seat A2 returns to free and the roster no longer lists Faisal Al-Harbi
  And the attendee receives a BookingReleased notification

Scenario: Switching session drops an armed release
  When the administrator clicks the held seat "A2" and then selects a different session
  Then the dialog closes and NO DELETE request is issued against either session
```

**Evidence captured:**
- bUnit: `tests/SIMF.ControlPanel.Tests/SessionSeatPlanReleaseConfirmTests.cs` - clicking a held seat issues zero `simfAccount.deleteJson` calls and opens `Admin.SessionSeatPlans.Release.Title`; the dialog `[data-testid='release-holder']` reads the holder name; cancel leaves the count at zero. 4/4 passing.
- Source: `SessionSeatPlan.razor` `OnSeatClickAsync` -> `AskReleaseAsync` (arms only) + the page-level `<SimfConfirm Danger="true">`; `SessionSeatPlan.razor.cs` `ConfirmReleaseAsync` -> `ReleaseAsync`, and `OnSessionChangedAsync` clearing `_releasing`.
- The release notification is the pre-existing `TryNotifyBookingReleasedAsync` path (`SeatReservationsTests.Admin_release_marks_cancelled_and_notifies`).

---

### E2E-SSP-018 / E2E-SSP-019 - The plan names the holder and reads the real state (DEF-SEA-001 / A11)

```gherkin
Feature: The seat plan answers who is in this seat and whether they arrived
  As an Administrator
  I want the plan to name each holder and show the live seat state
  So that protocol seating decisions are made on facts, not colours

Background:
  Given an active session whose hall has the layout rows "A" x 4 seats
  And an Administrator has signed in and selected that session

Scenario: An attendee booking names the holder and reads Confirmed once they scan in
  Given the visitor "Faisal Al-Harbi" holds seat A2 (Approved)
  And that visitor has an OPEN hall-attendance row for the session (scanned in at the gate)
  When POST /account/api/admin/sessions/{sessionId}/seats/list returns its page
  Then the row carries holderUserId = the visitor, holderName = "Faisal Al-Harbi", holderNameArabic = "فيصل الحربي"
  And status = "Approved" and checkedIn = true
  # A11 - these used to be the record defaults Pending / false
  And the roster under the grid shows Seat "A2", Held by "Faisal Al-Harbi", State "Confirmed (checked in)"
  And the seat tooltip reads "Seat A2 - UserBooking - held by Faisal Al-Harbi"

Scenario: A VVIP admin block reads its guest note instead of a name
  Given the administrator has typed the guest note "Reserved for the Minister" / "محجوز لمعالي الوزير"
  And has tapped free seat A1 to hold it
  When the seat list is re-read
  Then that row carries holderUserId = null, holderName = "" and guestHint = "Reserved for the Minister"
  And the roster "Held by" cell for A1 reads the guest note, and State reads "Unavailable"

Scenario: An admin block with no guest note is labelled, not blank
  Given the administrator reserves the whole row "A" with no guest note
  Then each blocked seat "Held by" cell reads "Admin block (no attendee)" / "حجز إداري (بدون زائر)"

Scenario: The attendee name never reaches a visitor read
  When a signed-in visitor calls GET /app/sessions/{sessionId}/seats
  Then the reservedCells carry NO holderName / holderNameArabic / holderUserId key
  # the app-facing map uses SessionSeatCell; the identity lives only on the admin SeatPlanCell
```

**Evidence captured:**
- API: `SeatReservationsTests.Seat_plan_list_names_the_holder_and_ships_the_real_status_and_check_in` and `Seat_plan_list_carries_the_vvip_guest_note_and_no_holder_for_an_admin_block` (both passing in the 57/57 seat + permission run).
- Contract separation: `SIMF.Contracts/Sessions/SeatReservations.cs` - `SeatPlanCell` (admin) vs `SessionSeatCell` (app); only `ListSessionReservationsAsync` builds the former.
- Bilingual holder pick + the admin-block fallback: `SessionSeatPlan.razor.cs` `HolderLabel`; the state read-out: `StateLabel`.

---

## Implementation notes

- **Manual smoke as canonical source-of-truth today.** Until Playwright is
  adopted, the canonical "run" is a Chrome DevTools MCP session: sign in per the
  Background, walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-sessions-seat-plans-{scenario}.png`. The Gherkin is
  runner-agnostic and converts 1:1 into a `.feature` file under
  `tests/SIMF.E2E.Tests/` (project to be created) when Playwright lands.
- **API integration tests at a lower layer:** `tests/SIMF.Api.Tests/SeatReservationsTests.cs`
  exercises the same backend surface — the `POST …/seats/reserve-row` admin
  row-reserve with `RowLabel = "A"`, and (2026-07-18) the per-seat VIP block
  `POST …/seats/reserve-seat` (`Admin_can_reserve_a_single_seat_for_a_vip` +
  `Admin_reserve_seat_out_of_bounds_is_400`). When an E2E scenario fully covers a
  path, the matching `Api.Tests` case can eventually be retired — keep both during
  the transition.
- **Permission gate:** view = `PermissionCatalog.SeatPlans.View`; the write
  endpoints (`reserve-row`, release `DELETE`) require `SeatPlans.Edit` on the
  API. The CP page only checks `SeatPlans.View`, so a viewer-only admin can open
  the page but their reserve/release calls would be rejected by the API — worth a
  future scenario if a `View`-without-`Edit` role is introduced. Both gates are
  enforced by `CpNavigationPermissionTests` + `PermissionEnforcementTests`.
- **No client-side validation.** All rules (row exists, capacity, ownership) are
  server-side; the page only guards empty/whitespace row input and the `_busy`
  re-entrancy flag. Every failure round-trips and surfaces via
  `Error.MessageForCurrentCulture()`.
- **E2E-SSP-014 defect - CLOSED (re-verified 2026-07-27).** The five grid strings
  `Admin.SessionSeatPlans.Legend.{Free,User,Admin,Random}` and
  `Admin.SessionSeatPlans.Seat.ReservedTitle` are present in BOTH resx files. The
  tooltip key now takes a third placeholder `{2}` - the seat holder (DEF-SEA-001).
- **Holder identity is admin-only (DEF-SEA-001).** `SeatPlanCell` exists so that the
  attendee name is returned by `POST /admin/sessions/{id}/seats/list` and nowhere else;
  the app-facing `GET /app/sessions/{id}/seats` keeps the identity-free
  `SessionSeatCell`. Any future scenario that asserts a holder name on a visitor read is
  asserting a defect.

---

_Last reviewed:_ 2026-07-27 by Claude (DEF-SEA-001 / A11 - added E2E-SSP-017..019 for
the named holder, the confirmed release, and the real Status/CheckedIn read-out).
Prior: 2026-07-25 by Claude (D-767 - added E2E-SSP-016 for the ragged seat
grid: each row drawn at its own SeatCounts[i] via the SeatsInRow index loop, reserve /
release still per-row, short-row out-of-bounds still 400 SEAT_OUT_OF_BOUNDS; page
reference doc authored). Prior: 2026-06-02 by Claude (E2E catalogue rebuild).
