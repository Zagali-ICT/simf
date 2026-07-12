# E2E test catalogue — Hall seat layouts (`/admin/halls/seat-layouts`)

| | |
|--|--|
| **Page** | `cp/admin-halls-seat-layouts.md` _(reference doc not yet authored — see `docs/pages/cp/admin-halls.md` for the related Halls CRUD page)_ |
| **Route** | `/admin/halls/seat-layouts` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page shape (read from `HallSeatLayoutEditor.razor`, D-182).** This is a
> **single-hall editor**, not a CRUD grid. There is no Add / Edit / Details /
> Delete modal. The whole page is: a hall **`<select>`** dropdown, and once a
> hall is chosen, two inputs — **Row labels** (comma-separated text) and
> **Seats per row** (number 1–80) — plus a read-only **Hall capacity** /
> **Layout capacity** description list, and one **Save layout** button. All
> validation is **server-side**; there is no client-side guard, so an invalid
> input round-trips to the API and returns as an error toast.
>
> **Permission gate:** view = `PermissionCatalog.SeatLayouts.View`
> (`SeatLayouts.View`); save = `SeatLayouts.Edit` enforced on the API PUT.
> **`RequiredPermission` on the `Module.HallSeatLayouts` nav item =
> `SeatLayouts.View`.**
>
> **Backend routes (via the CP BFF `/account/api/...`):**
> - `POST /account/api/admin/halls/list` → `ApiResult<GridPage<AdminHallSummary>>` (load the dropdown; `Top = 200`, filtered client-side to `IsActive`).
> - `GET  /account/api/admin/halls/{hallId}/seat-layout` → `ApiResult<HallSeatLayoutSnapshot>` (load on select).
> - `PUT  /account/api/admin/halls/{hallId}/seat-layout` → `ApiResult<HallSeatLayoutSnapshot>` (Save; rate-limited under the `auth` limiter).
>
> **`HallSeatLayoutSnapshot`** = `(HallId, RowLabels[], SeatsPerRow, LayoutCapacity, HallCapacity)`.
> **`SetHallSeatLayoutRequest`** = `{ RowLabels: string[], SeatsPerRow: int }`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-HSL-001 | Golden path — pick hall → set rows + seats → Save → read-back round-trip | happy | P0 | _to author_ |
| E2E-HSL-002 | Load halls dropdown — only active halls, sorted by Code, capacity shown | happy | P1 | _to author_ |
| E2E-HSL-003 | Select a hall — layout loads, fields prefill, Layout capacity recomputes | happy | P1 | _to author_ |
| E2E-HSL-004 | Edit Row labels — typing recomputes Layout capacity live (rows × seats) | happy | P2 | _to author_ |
| E2E-HSL-005 | Edit Seats per row — number input recomputes Layout capacity live | happy | P2 | _to author_ |
| E2E-HSL-006 | Switch hall clears stale toast — "Saved" from hall A doesn't follow to hall B | happy | P2 | _to author_ |
| E2E-HSL-007 | Empty state — no active halls renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-HSL-008 | Auth gate — user lacking `SeatLayouts.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-HSL-009 | Validation: row labels out of bounds (0 / >26 / >8 chars) → 400 `SEAT_LAYOUT_INVALID` | error | P1 | _to author_ |
| E2E-HSL-010 | Validation: duplicate row labels (case-insensitive) → 400 `SEAT_LAYOUT_INVALID` | error | P1 | _to author_ |
| E2E-HSL-011 | Validation: seats per row out of bounds (<1 / >80) → 400 `SEAT_LAYOUT_INVALID` | error | P1 | _to author_ |
| E2E-HSL-012 | Capacity conflict: rows × seats > Hall.Capacity → 400 `SEAT_CAPACITY_EXCEEDED` | error | P1 | _to author_ |
| E2E-HSL-013 | Save-permission gate — viewer with `View` but not `Edit` → PUT 403 on Save | auth | P1 | _to author_ |
| E2E-HSL-014 | Server 500 on halls `/list` → bilingual fallback toast, no dropdown | resilience | P2 | _to author_ |
| E2E-HSL-015 | RTL render — Arabic toggle mirrors banner, hint, fields and Save button | i18n | P1 | _to author_ |
| E2E-HSL-016 | Orphan guard (H-2) — a layout change that would strand active reservations (dropped row or shrunk seats-per-row) is blocked with 409 SEAT_LAYOUT_HAS_RESERVATIONS; a change with no orphans (and released reservations) still saves | conflict | P1 | authored ✓ |

## Scenarios

### E2E-HSL-001 — Golden path (round-trip)

```gherkin
Feature: Hall seat-layout editor round-trip
  As an Administrator with SeatLayouts.Edit
  I want to define the row labels and seats-per-row for a hall
  So that the seat picker visitors see during sign-up and seat reservation is correct

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the SeatLayouts.View + SeatLayouts.Edit permissions has signed in via /login + /login/totp (TOTP from Get-Totp)
  And they have navigated to /admin/halls/seat-layouts
  And at least one active Hall exists, e.g. Code="H-01", Name="Main Auditorium", Capacity=120

Scenario: Pick a hall, set a valid layout, Save, and confirm the read-back
  Given the page header (SimfBanner) reads "Hall seat layouts"
  And the hint reads "The layout drives the seat picker visitors see. Rows × seats-per-row must not exceed the hall capacity."
  And the "Select a hall" dropdown is shown with a blank first option
  When the administrator selects the option "H-01 — Main Auditorium (cap 120)"
  Then a GET /account/api/admin/halls/<H-01 id>/seat-layout fires and returns 200
  And the "Row labels (comma-separated, 1–26 entries, e.g. A,B,C,VIP)" text input appears
  And the "Seats per row (1–80)" number input appears
  And the description list shows "Hall capacity" = 120
  When the administrator types Row labels = "A,B,C,D"
  And sets Seats per row = "20"
  Then the "Layout capacity" value reads "80"  # 4 rows × 20 seats
  When they click "Save layout"
  Then a PUT /account/api/admin/halls/<H-01 id>/seat-layout fires with body { "rowLabels": ["A","B","C","D"], "seatsPerRow": 20 }
  And the API returns HTTP 200 with ApiResult.Success = true
  And the returned HallSeatLayoutSnapshot has RowLabels=["A","B","C","D"], SeatsPerRow=20, LayoutCapacity=80, HallCapacity=120
  And a green SimfAlert (success) appears reading "Layout saved." / "تم حفظ المخطط."
  When the administrator reselects the blank option then re-selects "H-01 — Main Auditorium (cap 120)"
  Then the GET re-loads and the Row labels input prefills "A,B,C,D" and Seats per row prefills "20"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-halls-seat-layouts-golden-before.png` (hall selected, fields empty/default)
- Screenshot after: `docs/screenshots/cp-admin-halls-seat-layouts-golden-after.png` (Save success toast + recomputed Layout capacity)
- Console errors: 0 expected
- Network: `POST /account/api/admin/halls/list`, the `GET .../seat-layout`, and the `PUT .../seat-layout` all return 200
- Audit row: an `AuditEntry` with `EventType = 'HallSeatLayout.Updated'` (`AuditEvents.HallSeatLayoutUpdated`), `Outcome = Success`, `ActorUserId` = the signed-in admin, and `Detail = "hallId=<H-01 id>; rows=A,B,C,D; seatsPerRow=20"`

### E2E-HSL-002 — Load halls dropdown

```gherkin
Scenario: Dropdown lists only active halls, ordered by Code, with capacity
  Given active halls H-01 (cap 120) and H-03 (cap 50) and a deactivated hall H-02 exist
  When the administrator opens /admin/halls/seat-layouts
  Then a POST /account/api/admin/halls/list fires with body { "top": 200 } and returns 200
  And the "Select a hall" dropdown shows a blank first option
  And then "H-01 — Main Auditorium (cap 120)" then "H-03 — <name> (cap 50)" in ascending Code order
  And the inactive hall "H-02" does NOT appear (client filters Where(IsActive))
```

### E2E-HSL-003 — Select a hall loads the layout

```gherkin
Scenario: Selecting a hall loads its stored layout and prefills the fields
  Given hall "H-01" already has a saved layout RowLabels="A,B" SeatsPerRow=5
  When the administrator selects "H-01 — Main Auditorium (cap 120)"
  Then a GET /account/api/admin/halls/<H-01 id>/seat-layout returns 200
  And the Row labels input prefills "A,B"
  And the Seats per row input prefills "5"
  And the description list reads Hall capacity = 120 and Layout capacity = 10

Scenario: Selecting a hall with no stored layout shows safe defaults
  Given hall "H-03" has NEVER been given a layout
  When the administrator selects "H-03 — <name> (cap 50)"
  Then the GET returns 200 with an empty RowLabels and SeatsPerRow=0
  And the Row labels input is blank
  And the Seats per row input shows "1"  # _seatsPerRow falls back to 1 when snapshot value is 0
```

### E2E-HSL-004 — Editing row labels recomputes layout capacity

```gherkin
Scenario: Layout capacity recomputes as row labels change
  Given hall "H-01" (cap 120) is selected with Seats per row = "10"
  When the administrator types Row labels = "A,B,C"
  Then the "Layout capacity" value reads "30"  # 3 rows × 10
  When they change Row labels = "A,B,C, ,D,"   # blank + trailing entries ignored
  Then the row count counts only non-empty trimmed entries (A,B,C,D = 4)
  And the "Layout capacity" value reads "40"
```

### E2E-HSL-005 — Editing seats per row recomputes layout capacity

```gherkin
Scenario: Layout capacity recomputes as seats-per-row changes
  Given hall "H-01" (cap 120) is selected with Row labels = "A,B,C,D"
  When the administrator sets Seats per row = "25"
  Then the "Layout capacity" value reads "100"  # 4 rows × 25
  And the number input enforces min="1" max="80" attributes
```

### E2E-HSL-006 — Switching hall clears stale toast

```gherkin
Scenario: A success toast from one hall does not follow to the next
  Given the administrator has just saved a layout for "H-01" and the "Layout saved." toast is visible
  When they select "H-03 — <name> (cap 50)" from the dropdown
  Then the success toast is cleared (OnHallChangedAsync sets _toast = null before loading)
  And the GET for H-03's layout fires and the fields reflect H-03
```

### E2E-HSL-007 — Empty state

```gherkin
Scenario: No active halls renders SimfEmptyState
  Given the database has no active Hall rows
  When the administrator opens /admin/halls/seat-layouts
  Then the POST /account/api/admin/halls/list returns 200 with an empty Items array
  And the page renders the SimfEmptyState component
  And the empty state title reads "No halls available." / "لا توجد قاعات متاحة."
  And the hall "Select a hall" dropdown is NOT rendered
  And no error toast appears
```

### E2E-HSL-008 — Auth gate (view permission)

```gherkin
Scenario: A signed-in admin lacking SeatLayouts.View is denied
  Given a signed-in CP user in a role WITHOUT the SeatLayouts.View permission
  When they navigate to /admin/halls/seat-layouts
  Then the [RequirePermission(PermissionCatalog.SeatLayouts.View)] attribute denies them
  And they land on /not-permitted with HTTP 200
  And no /account/api/admin/halls/list request fires
  And the Module.HallSeatLayouts nav item is hidden for them (RequiredPermission = SeatLayouts.View)
```

### E2E-HSL-009 — Validation: row-label bounds

```gherkin
Scenario Outline: Out-of-bounds row labels return 400 SEAT_LAYOUT_INVALID
  Given hall "H-01" (cap 120) is selected
  When the administrator sets Row labels = "<rowLabels>" and Seats per row = "5"
  And clicks "Save layout"
  Then the PUT /account/api/admin/halls/<H-01 id>/seat-layout fires
  And the API returns HTTP 400 with ApiResult.Error.Code = "SEAT_LAYOUT_INVALID"
  And a red SimfAlert (error) surfaces "Row labels must be 1–26 unique entries of 1–8 chars each." / "يجب أن تكون رموز الصفوف بين 1 و 26 إدخالاً فريداً بطول 1 إلى 8 محارف."
  And no "Layout saved." toast appears

  Examples:
    | rowLabels                                                                                            | reason            |
    |                                                                                                      | zero rows (<1)    |
    | A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z,AA                                                | 27 rows (>26)     |
    | A,TOOLONGLABEL,C                                                                                      | a label >8 chars  |
```

### E2E-HSL-010 — Validation: duplicate row labels

```gherkin
Scenario: Case-insensitive duplicate row labels return 400 SEAT_LAYOUT_INVALID
  Given hall "H-01" (cap 120) is selected
  When the administrator sets Row labels = "A,a,B"   # "A" and "a" collide under OrdinalIgnoreCase
  And sets Seats per row = "5"
  And clicks "Save layout"
  Then the PUT returns HTTP 400 with ApiResult.Error.Code = "SEAT_LAYOUT_INVALID"
  And the error toast reads "Row labels must be 1–26 unique entries of 1–8 chars each." / "يجب أن تكون رموز الصفوف بين 1 و 26 إدخالاً فريداً بطول 1 إلى 8 محارف."
```

### E2E-HSL-011 — Validation: seats-per-row bounds

```gherkin
Scenario Outline: Out-of-bounds seats-per-row returns 400 SEAT_LAYOUT_INVALID
  Given hall "H-01" (cap 120) is selected with Row labels = "A,B"
  When the administrator sets Seats per row = "<seats>"
  And clicks "Save layout"
  Then the PUT returns HTTP 400 with ApiResult.Error.Code = "SEAT_LAYOUT_INVALID"
  And the error toast reads "Seats per row must be between 1 and 80." / "يجب أن يكون عدد المقاعد في كل صف بين 1 و 80."

  Examples:
    | seats |
    | 0     |
    | 81    |
```

### E2E-HSL-012 — Capacity conflict

```gherkin
Scenario: Layout capacity exceeding hall capacity returns 400 SEAT_CAPACITY_EXCEEDED
  Given hall "H-03" (cap 50) is selected
  When the administrator sets Row labels = "A,B,C,D,E,F" (6 rows) and Seats per row = "10"
  Then the "Layout capacity" client value reads "60" (exceeds 50)
  When they click "Save layout"
  Then the PUT returns HTTP 400 with ApiResult.Error.Code = "SEAT_CAPACITY_EXCEEDED"
  And the error toast surfaces the bilingual message "Layout capacity (60) exceeds hall capacity (50)." / "السعة المقترحة (60) تتجاوز سعة القاعة (50)."
  And no layout is persisted
```

### E2E-HSL-013 — Save-permission gate

```gherkin
Scenario: An admin with SeatLayouts.View but not SeatLayouts.Edit cannot Save
  Given a signed-in CP user whose role has SeatLayouts.View but NOT SeatLayouts.Edit
  When they open /admin/halls/seat-layouts (allowed — View gate passes)
  And they select "H-01", set Row labels = "A,B", Seats per row = "5"
  And click "Save layout"
  Then the PUT /account/api/admin/halls/<H-01 id>/seat-layout is rejected by the API
  And the API returns HTTP 403 (Policies(PolicyFor(SeatLayouts.Edit), RequireApprovedAccount))
  And a red error toast appears (the BFF Forward surfaces the failure)
  And no "Layout saved." success toast appears
```

### E2E-HSL-014 — Server 500 on halls list

```gherkin
Scenario: API 500 on /admin/halls/list shows the fallback toast and no dropdown
  Given the API is configured to return HTTP 500 on /admin/halls/list (e.g. DB down)
  When the administrator opens /admin/halls/seat-layouts
  Then the POST /account/api/admin/halls/list returns a non-success envelope
  And a red SimfAlert (error) appears reading "Could not load hall seat layouts." / "تعذّر تحميل مخططات مقاعد القاعات."
  And the "Select a hall" dropdown renders with no hall options
  And no unhandled console exception is thrown
```

### E2E-HSL-015 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the editor
  Given the administrator is on /admin/halls/seat-layouts in English with a hall selected
  When they switch the UI to العربية (Arabic) via the header language link
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "مخططات مقاعد القاعات"
  And the hint reads "المخطط يحدّد منتقي المقاعد الذي يراه الزائر. عدد الصفوف × المقاعد لكل صف يجب ألا يتجاوز سعة القاعة."
  And the dropdown label reads "اختر قاعة"
  And the field labels read "رموز الصفوف (مفصولة بفواصل، من 1 إلى 26 إدخالاً، مثال: A,B,C,VIP)" and "عدد المقاعد في كل صف (1–80)"
  And the description-list terms read "سعة القاعة" and "سعة المخطط"
  And the Save button reads "حفظ المخطط"
  And the whole surface is mirrored right-to-left
```

---

### E2E-HSL-016 — Layout change may not orphan active reservations (H-2)

```gherkin
Feature: Seat-layout orphan guard (H-2)
  As an Administrator changing a hall's seat layout
  I must not strand seats that are actively reserved across the hall's sessions

Background:
  Given a hall with rows A,B × 5 seats and a session on that hall

Scenario: Dropping a booked row is blocked
  Given a visitor actively holds seat B4
  When the administrator saves a layout of rows [A] × 5 (row B dropped)
  Then the API returns HTTP 409 with ErrorCodes.SeatLayoutHasReservations
  And the stored layout is unchanged (still A,B)

Scenario: Shrinking seats-per-row below a booked seat is blocked
  Given a visitor actively holds seat A5
  When the administrator saves a layout of rows [A] × 3
  Then the API returns HTTP 409 with ErrorCodes.SeatLayoutHasReservations

Scenario: A change with no orphans succeeds (released seats do not block)
  Given a visitor holds A1 (inside the new grid) and another visitor's B4 was
    already released
  When the administrator saves a layout of rows [A] × 5 (row B dropped)
  Then the API returns HTTP 200 and the layout becomes A only
```

**Evidence captured:**
- API integration tests: `SeatReservationsTests.Shrinking_a_layout_that_orphans_a_reservation_is_blocked`, `SeatReservationsTests.Shrinking_seats_per_row_below_a_booked_seat_is_blocked`, `SeatReservationsTests.Layout_change_with_no_orphans_succeeds`
- CP toast/error map must include SEAT_LAYOUT_HAS_RESERVATIONS so the operator sees the bilingual message

---

## Implementation notes

- **API integration tests at a lower layer.** `tests/SIMF.Api.Tests/SeatReservationsTests.cs`
  exercises the same `GET`/`PUT .../seat-layout` surface without a browser —
  it round-trips a valid layout and asserts the `SEAT_LAYOUT_INVALID` /
  `SEAT_CAPACITY_EXCEEDED` rejections (see its `RowLabels`/`SeatsPerRow`
  cases around lines 197 and 225). `tests/SIMF.Api.Tests/BookingApprovalTests.cs`
  also seeds a layout via `SetHallSeatLayoutRequest { RowLabels = "A,B", SeatsPerRow = 5 }`.
  When an E2E scenario above is covered by Playwright, the matching
  `Api.Tests` case can usually be retired — but keep both during the transition.
- **No client-side validation on this page.** `HallSeatLayoutEditor.razor`
  performs no in-browser bounds check; every invalid input round-trips to the
  API and returns via `MessageForCurrentCulture()`. The bounds live only in
  `SeatReservationService.SetLayoutAsync` (1–26 unique rows, ≤8 chars each,
  1–80 seats, rows × seats ≤ `Hall.Capacity`).
- **Permission seeding.** `SeatLayouts.View` and `SeatLayouts.Edit` are
  seeded with `BaselineRoles = AdminOnly` in `PermissionCatalog.All`. The
  `CpNavigationPermissionTests` / `PermissionEnforcementTests` guards fail the
  build if either gate is dropped.
- **Manual smoke is the canonical run today.** Until a Playwright project
  exists, drive these scenarios with a Chrome DevTools MCP session per the
  Auth setup row and capture screenshots into
  `docs/screenshots/cp-admin-halls-seat-layouts-*.png`. The Gherkin shape is
  already runner-agnostic for the eventual `.feature` port.
- **Reference doc gap.** `docs/pages/cp/admin-halls-seat-layouts.md` does not
  exist yet; the closest authored page doc is `docs/pages/cp/admin-halls.md`
  (the Halls CRUD page that feeds this editor's dropdown).

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
