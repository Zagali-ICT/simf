# Session seat plan - `/admin/sessions/seat-plans`

| | |
|--|--|
| **Route** | `/admin/sessions/seat-plans` |
| **Audience** | Administrator (and any role granted `SeatPlans.View`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.SeatPlans.View)]` (CP page) + API `SeatPlans.View` (session list + seat list + layout GET) / `SeatPlans.Edit` (reserve-row / reserve-seat / release) policies + `RequireApprovedAccount` |
| **Pattern** | Session picker + seat tool (D-182 / D-215) - NOT a CRUD grid. A session `<select>` -> a "Reserve row" input -> one of (visual seat grid / fallback table / `SimfEmptyState`). **D-767:** the visual grid renders **ragged** (each row at its own seat count). |
| **Status** | ✅ Real (D-182 / D-215; 2026-07-18 per-seat VIP reserve; **D-767** ragged grid) |
| **Required permissions** | `SeatPlans.View` (page + nav item `Module.SessionSeatPlans` + the session/seat/layout reads), `SeatPlans.Edit` (reserve-row, reserve-seat, release) - `PermissionCatalog.SeatPlans.*`, baseline `AdminOnly`. |
| **Backend endpoints** | BFF `/account/api/admin/sessions/*` + `/account/api/admin/halls/*` -> API: `POST /admin/sessions/list`, `GET /admin/halls/{hallId}/seat-layout`, `POST /admin/sessions/{id}/seats/list`, `POST .../seats/reserve-row`, `POST .../seats/reserve-seat`, `DELETE .../seats/{reservationId}`. |
| **Backed by** | `dbo.SeatReservations` (one row per reserved seat) + `dbo.HallSeatLayouts` (the grid; **D-767** `SeatCounts`). No new table. |
| **Source** | [`SessionSeatPlan.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionSeatPlan.razor) + [`.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionSeatPlan.razor.cs), the read-only twin [`SessionLiveHall.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionLiveHall.razor) + [`.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionLiveHall.razor.cs), [`SeatReservationEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Sessions/SeatReservationEndpoints.cs), [`SeatReservationService.cs`](../../../src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs), [`SeatReservations.cs`](../../../src/Shared/SIMF.Contracts/Sessions/SeatReservations.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-sessions-seat-plans.md`](../../tests/e2e/cp-admin-sessions-seat-plans.md) (E2E-SSP-001..016) |
| **Last reviewed** | 2026-07-25 |

## 1. Purpose

A Control Panel tool for **protocol seating** on a chosen session: reserve a whole row
as an admin block, hold one specific seat for a VIP, or release a reservation - before
visitors self-pick. The admin selects a session; if its hall has a saved seat layout,
the page draws a visual grid (one button per seat, coloured per `SeatReservationKind`);
otherwise it falls back to a reservation table or an empty state. It is not a CRUD grid
and has no Add / Edit / Details modal - every rule is enforced server-side and surfaced
as a toast.

**D-767 - ragged grid.** The grid loop now honours per-row seat counts: the outer loop
runs over `RowLabels.Count` and each row's inner loop is bounded by `SeatsInRow(r)`
(`SeatCounts[r]` when the layout is variable, else the uniform `SeatsPerRow`). A 4-seat
VIP row above 10/10 general rows renders exactly VIP1..VIP4 with no phantom seats; the
existing per-row `.seatgrid__row` flexbox already narrows a short row and holds RTL
alignment, so no CSS change was needed. The same index loop lands on the read-only
live-hall twin (`SessionLiveHall`). See DECISIONS_LOG **D-767**.

## 4. UI

- `SimfBanner` (`Admin.SessionSeatPlans.Title`, "Session seat plans" / "مخطط مقاعد
  الجلسات") + a hint, inside `simf-page-wide` / `simf-surface`.
- **Session picker** - a `<select>` (`Admin.SessionSeatPlans` "Select a session")
  populated from active sessions, each option `{Code} {Title}`, `Top = 200`, ordered by
  `Code`. When there is no active session the page shows `SimfEmptyState` ("No sessions
  available.").
- **Reserve-row** - a text input ("Row to reserve (must exist in the hall layout)") + a
  **Reserve row** button (`ReserveRowAsync`). Visible once a session is picked.
- **One of three seat views:**
  - **Visual seat grid** (the hall has a layout) - one `<button>` per seat. A **free**
    seat is clickable to reserve it for a VIP; a **reserved** seat is clickable to
    release it. A **legend** (Free / User / Admin / Random) + an "{N} active
    reservation(s)" summary follow. **D-767:** each row draws `SeatsInRow(r)` seats.
  - **Fallback table** (Row / Seat / Kind / Actions, per-row "Release") - when the hall
    has no layout but reservations exist.
  - **`SimfEmptyState`** ("No active reservations on this session.") - no layout and no
    reservations.
- A top `SimfAlert` toast carries every outcome and is cleared on session change.

## 5. Data flow + endpoints

CP page -> JS `simfAccount.*` -> CP BFF passthroughs -> API endpoints in
`SeatReservationEndpoints.cs` -> `SeatReservationService`.

| BFF route | API route | Body | Returns | Policy |
|-----------|-----------|------|---------|--------|
| `POST /account/api/admin/sessions/list` | `POST /admin/sessions/list` | `GridQuery { Top = 200 }` | `ApiResult<GridPage<AdminSessionSummary>>` | `SeatPlans.View` |
| `GET /account/api/admin/halls/{hallId}/seat-layout` | `GET /admin/halls/{hallId}/seat-layout` | - | `ApiResult<HallSeatLayoutSnapshot>` (a 404 / missing layout -> table / empty fallback) | `SeatLayouts.View` |
| `POST /account/api/admin/sessions/{id}/seats/list` | `POST /admin/sessions/{id}/seats/list` | `GridQuery { Top = 500 }` | `ApiResult<GridPage<SessionSeatCell>>` | `SeatPlans.View` |
| `POST /account/api/admin/sessions/{id}/seats/reserve-row` | `POST /admin/sessions/{id}/seats/reserve-row` | `{ rowLabel }` | `ApiResult<bool>` | `SeatPlans.Edit`; `auth` limiter |
| `POST /account/api/admin/sessions/{id}/seats/reserve-seat` | `POST /admin/sessions/{id}/seats/reserve-seat` | `{ rowLabel, seatNumber }` | `ApiResult<bool>` | `SeatPlans.Edit`; `auth` limiter |
| `DELETE /account/api/admin/sessions/{id}/seats/{reservationId}` | `DELETE /admin/sessions/{id}/seats/{reservationId}` | - | `ApiResult<bool>` | `SeatPlans.Edit`; `auth` limiter |

The `HallSeatLayoutSnapshot` the grid loads carries `RowLabels`, `SeatsPerRow`,
`LayoutCapacity`, `HallCapacity` and (**D-767**) the nullable `SeatCounts`; the grid
resolves each row's width through `SeatsInRow(rowIndex)`. Reserve / release use
`rowLabel` + `seatNumber` and are **unchanged** by D-767 - they already operate per-seat.

## 6. Validation + error handling

All rules are server-side; the page only guards an empty/whitespace row input and a
`_busy` re-entrancy flag. Every failure round-trips via
`Error.MessageForCurrentCulture()`.

| Rule | Code / HTTP | Message (EN / AR) |
|------|-------------|-------------------|
| Reserve a row / seat not in the hall layout (row absent) | `SEAT_OUT_OF_BOUNDS` 400 | "Row '{row}' is not in the hall layout." / "الصف '{row}' غير موجود في مخطط القاعة." |
| **D-767** reserve a seat number past that row's count (e.g. VIP seat 5 in a 4-seat row) | `SEAT_OUT_OF_BOUNDS` 400 | "Seat number must be between 1 and {n}." / "يجب أن يكون رقم المقعد بين 1 و {n}." |
| Reserve an already-held seat | `SEAT_ALREADY_RESERVED` 409 | (a visitor cannot then book it) |
| Release a reservation id that does not exist / wrong session | `SEAT_RESERVATION_NOT_FOUND` 404 | "Seat reservation not found." / "لم يتم العثور على حجز المقعد." |
| Seat-list load failure | fallback | "Could not load session seat plan." toast |

**D-767 note:** the per-row seat bound now reads `ctx.SeatCounts[i]` in
`ValidateSeatBounds`, so on a ragged layout the "between 1 and {n}" message reports that
row's real width (4 for a 4-seat VIP row).

## 7. Edge cases + known limitations

- **View-without-Edit.** The page only checks `SeatPlans.View`; a viewer-only admin can
  open it but their reserve / release calls are rejected 403 by the API
  (`SeatPlans.Edit`).
- **Idempotent row reserve.** Re-reserving an already-reserved row returns 200 (the
  service skips seats already held); no duplicate rows.
- **Ragged short-row phantom (old mobile client).** The server emits `SeatsPerRow =
  max(counts)` on the wire, so an un-upgraded app renders max-width rows; a tap on a
  phantom short-row seat fails safe with 400 `SEAT_OUT_OF_BOUNDS` (no corruption). The CP
  grid itself is ragged (uses `SeatsInRow`).
- **Known localization gap (E2E-SSP-014).** The four legend strings
  (`Admin.SessionSeatPlans.Legend.{Free,User,Admin,Random}`) and the reserved-seat
  tooltip (`Admin.SessionSeatPlans.Seat.ReservedTitle`) are referenced in the `.razor`
  but missing from both resx files, so they render the raw key text. Tracked, not fixed
  here.

## 8. i18n + RTL

`Admin.SessionSeatPlans.*` keys carry EN <-> AR parity except the five known-missing
grid strings above. Under Arabic the page + grid + summary mirror to RTL
(`<html dir="rtl" lang="ar">`); the seat rows mirror right-to-left, and a ragged short
row narrows on its own (per-row flexbox). D-767 adds no new resx key on this page (the
ragged behaviour is a loop change only).

## 10. Use cases

- UC-SSP-ROW-001 (reserve a whole row), UC-SSP-VIP-001 (hold one seat for a VIP, 2026-07-18),
  UC-SSP-RELEASE-001 (release a reservation), UC-SSP-RAGGED-001 (**D-767** - the grid
  renders and manages a ragged layout per-row). Supports FR-503/903 (protocol seating
  before visitor self-pick).

## 11. E2E

See [`docs/tests/e2e/cp-admin-sessions-seat-plans.md`](../../tests/e2e/cp-admin-sessions-seat-plans.md):
E2E-SSP-001 golden (pick -> reserve row -> release), 002 load, 003 reserve row, 004
release from grid, 005 fallback table, 006 empty, 007 no sessions, 008 auth gate, 009
row-not-in-layout 400, 010 idempotent re-reserve, 011 stale-release 404, 012 server-500,
013 RTL, 014 missing-legend gap guard, 015 VIP single-seat reserve, and the **D-767**
addition **016** ragged grid renders per-row + reserve/release per-row + short-row
out-of-bounds 400. API integration coverage: `tests/SIMF.Api.Tests/SeatReservationsTests.cs`
(reserve-row, reserve-seat, out-of-bounds **plus 10 new D-767 variable-layout facts**,
incl. the per-row short-row bound `Variable_layout_bounds_each_row_by_its_own_seat_count`;
suite 44/44 passing). E2E-SSP-016 adds the browser-level ragged-grid run on top of that
xUnit coverage.

## 12. Related docs

- Related pages: [`cp/admin-halls-seat-layouts.md`](admin-halls-seat-layouts.md) (defines
  the layout this page renders), [`cp/admin-sessions.md`](admin-sessions.md),
  [`e2e/cp-admin-session-live-hall.md`](../../tests/e2e/cp-admin-session-live-hall.md)
  (the read-only live-hall twin that shares the `SeatsInRow` loop).
- Decisions: **D-767** (per-row variable seat counts), D-215 (visual seat grid), D-182
  (CP editor for D-175), 2026-07-18 (per-seat VIP reserve), D-219 (append-only mobile
  wire), D-157 (App / Identity DB separation).
- Wire contracts: `SeatReservations.cs` (`SessionSeatCell`, `HallSeatLayoutSnapshot`,
  `AdminReserveRowRequest`, `AdminReserveSeatRequest`).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-25 | D-767 | **First authored page reference doc** (was `-` in `PAGE-INDEX.md`). Documents the D-767 ragged grid: `SessionSeatPlan` (and the `SessionLiveHall` twin) render each row at its own `SeatsInRow(r) = SeatCounts[r] ?? SeatsPerRow` width; reserve-row / reserve-seat / release are unchanged (per-seat); the per-row seat bound now reports the real short-row width in the `SEAT_OUT_OF_BOUNDS` message. |
| 2026-07-26 | D-771 | **VVIP guest note + tier bands.** A VVIP seat has no registration, so the page gained two free-text fields above the grid — "Guest note (Arabic)" / "Guest note (English)", ≤256 chars — that travel with the per-seat block (`AdminReserveSeatRequest.GuestHint` / `GuestHintArabic`, persisted on `SeatReservation`, migration `App/D771_AddSeatTiersAndVvipGuestHint`). The workflow is: type the note, then tap the seat to hold it; the fields clear on success. Each grid row now shows its `SeatTier` as a start-edge colour band (`--color-seat-tier-vvip` / `-vip` / `-normal`) with the tier name on the row-label tooltip, and a blocked seat's tooltip appends its guest note. `SessionSeatCell` appends `GuestHint` / `GuestHintArabic` (append-only wire). The reserve-row action is now wrapped in `<AuthorizedAction Permission="SeatPlans.Edit">`. The same note is what the mobile app and the new staff seating desk (`mobile-staff-seating.md`) display for that seat. |

_Last reviewed:_ 2026-07-25 by Claude (D-767 - authored from live source:
`SessionSeatPlan.razor(.cs)`, `SessionLiveHall.razor(.cs)`, `SeatReservationService`,
`SeatReservationEndpoints`, contracts).
