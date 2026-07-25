# Hall seat-layout editor - `/admin/halls/seat-layouts`

| | |
|--|--|
| **Route** | `/admin/halls/seat-layouts` |
| **Audience** | Administrator (and any role granted `SeatLayouts.View`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.SeatLayouts.View)]` (CP page) + API `SeatLayouts.View` (GET) / `SeatLayouts.Edit` (PUT) policies + `RequireApprovedAccount` |
| **Pattern** | Single-hall editor (D-182) - NOT a CRUD grid and NOT `SimfDataGrid`. A hall `<select>` -> the row-labels text input -> **one seat-count input per row** + a live **Total seats** readout + one **Save layout** button. |
| **Status** | ✅ Real (D-182; **D-767** per-row variable seat counts) |
| **Required permissions** | `SeatLayouts.View` (page + nav item `Module.HallSeatLayouts` + GET), `SeatLayouts.Edit` (Save / PUT) - `PermissionCatalog.SeatLayouts.*`, baseline `AdminOnly`. |
| **Backend endpoints** | BFF `/account/api/admin/halls/*` -> API: `POST /admin/halls/list` (`GridQuery`, fill the dropdown), `GET /admin/halls/{hallId}/seat-layout`, `PUT /admin/halls/{hallId}/seat-layout` (`SetHallSeatLayoutRequest`). |
| **Backed by** | `dbo.HallSeatLayouts` - one row per hall (unique `HallId`). **D-767** adds `SeatCounts nvarchar(256) NULL` (migration `App/D767_AddHallSeatLayoutSeatCounts`); `RowLabels` (CSV) and `SeatsPerRow` are unchanged. |
| **Source** | [`HallSeatLayoutEditor.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallSeatLayoutEditor.razor) + [`.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallSeatLayoutEditor.razor.cs), [`SeatReservationEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Sessions/SeatReservationEndpoints.cs), [`SeatReservationService.cs`](../../../src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs), [`SeatReservations.cs`](../../../src/Shared/SIMF.Contracts/Sessions/SeatReservations.cs), [`HallSeatLayout.cs`](../../../src/Backend/SIMF.Domain/SeatReservations/HallSeatLayout.cs), [`HallSeatLayoutConfiguration.cs`](../../../src/Backend/SIMF.Infrastructure/Persistence/Configurations/App/HallSeatLayoutConfiguration.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-halls-seat-layouts.md`](../../tests/e2e/cp-admin-halls-seat-layouts.md) (E2E-HSL-001..022) |
| **Last reviewed** | 2026-07-25 |

## 1. Purpose

A Control Panel editor that defines the seat grid for **one hall at a time**. The
saved layout is the source of truth for the seat picker visitors see during seat
reservation and for the admin session seat-plan / live-hall grids. The admin picks a
hall, enters the **row labels** (a comma-separated set, e.g. `VIP,A,B,C`), sets **how
many seats each row has**, and saves. A hall has at most one layout row.

**D-767 - per-row variable seat counts (Option A).** Before D-767 a layout was a set
of row labels plus a single uniform `SeatsPerRow`, so every row was the same width.
D-767 adds an additive nullable `SeatCounts` CSV (parallel to `RowLabels`) so each row
can carry its own count - a 4-seat VIP row can sit above 10/8/8 general rows.
`SeatsPerRow` is kept as the uniform fallback: when `SeatCounts` is null the layout is
uniform exactly as before, and when it is set the stored `SeatsPerRow` holds
`max(SeatCounts)` (never hides a real seat from an old client). See DECISIONS_LOG
**D-767**.

## 4. UI

- `SimfBanner` titled `Admin.HallSeatLayouts.Title` ("Hall seat layouts" / "مخططات
  مقاعد القاعات"), an `Admin.HallSeatLayouts.Hint` line, then the editor inside
  `simf-page-wide` / `simf-surface`.
- **Hall picker** - a native `<select>` (`Admin.HallSeatLayouts.Pick`, "Select a hall")
  with a blank first option; options read `{Code} {Name} (cap {Capacity})` and list only
  active halls, ordered by `Code`. When no active hall exists the page renders
  `SimfEmptyState` (`Admin.HallSeatLayouts.None`).
- **Row labels** - a raw text input (`Admin.HallSeatLayouts.Field.RowLabels`); the
  comma-separated, trimmed, non-empty entries are the row set.
- **Seats in each row (D-767)** - **one raw `<input type=number min=1 max=80>` beside
  each parsed row label** (`Admin.HallSeatLayouts.Field.RowSeats`, "Seats in each row").
  Raw inputs are used deliberately, NOT `SimfTextField`, to avoid the D-648
  ValueExpression page-freeze. Renaming a label keeps that position's count
  (`OnRowLabelsChanged` reconciles the `_rows` list positionally); a newly added row
  seeds from the loaded uniform `SeatsPerRow` (else 1).
- **Capacity readout** - a description list showing `Admin.HallSeatLayouts.HallCapacity`
  ("Hall capacity", the hall's `Capacity`) and `Admin.HallSeatLayouts.TotalSeats`
  ("Total seats", the live `sum(counts)`). Total seats replaced the old single
  "Layout capacity" line.
- **Guard banner + disabled Save** - a `<SimfAlert Variant="warning">`
  (`Admin.HallSeatLayouts.CapacityExceeded`) shows and **Save layout**
  (`Admin.HallSeatLayouts.Save`) is disabled while `sum(counts) > Hall.Capacity` OR any
  row count is outside 1 to 80 (`_saveDisabled = _anyOutOfRange || _capacityExceeded`) -
  a client mirror of the server triple-lock.
- Toasts use `SimfAlert`; a stale toast is cleared when the hall changes
  (`OnHallChangedAsync` sets `_toast = null` before loading).

There is no Add / Edit / Details / Delete modal, no import and no export.

## 5. Data flow + endpoints

CP page -> JS `simfAccount.getJson` / `postJson` -> CP BFF passthroughs in
`AccountEndpoints.cs` -> API endpoints in `SeatReservationEndpoints.cs` ->
`SeatReservationService`.

| BFF route | API route | Body | Returns |
|-----------|-----------|------|---------|
| `POST /account/api/admin/halls/list` | `POST /api/v1/admin/halls/list` | `GridQuery { Top = 200 }` | `ApiResult<GridPage<AdminHallSummary>>` (filtered client-side to `IsActive`) |
| `GET /account/api/admin/halls/{hallId}/seat-layout` | `GET /api/v1/admin/halls/{hallId}/seat-layout` | - | `ApiResult<HallSeatLayoutSnapshot>` (`SeatLayouts.View`) |
| `PUT /account/api/admin/halls/{hallId}/seat-layout` | `PUT /api/v1/admin/halls/{hallId}/seat-layout` | `SetHallSeatLayoutRequest` | `ApiResult<HallSeatLayoutSnapshot>` (`SeatLayouts.Edit`; `auth` limiter) |

**Contracts (D-767 additions in bold).**

- `SetHallSeatLayoutRequest` = `{ RowLabels: string[], SeatsPerRow: int, `**`SeatCounts?: int[]`**` }`. `SeatCounts` null/empty = uniform via `SeatsPerRow`; when non-empty its length must equal the row count and each value is 1 to 80. The `SetHallSeatLayoutEndpoint` re-projects `RowLabels` + `SeatsPerRow` + **`SeatCounts`** into a fresh request (over-post-safe).
- `HallSeatLayoutSnapshot` = `(HallId, RowLabels[], SeatsPerRow, LayoutCapacity, HallCapacity, `**`SeatCounts?`**`)`. `LayoutCapacity` is reused and carries `sum(SeatCounts)` in the variable case; `SeatCounts` is null when uniform.

**`SetLayoutAsync` (persist).** Trims/validates the row labels, resolves the per-row
counts (variable branch when `SeatCounts` is non-empty, else `Repeat(SeatsPerRow)`),
computes `layoutCapacity = counts.Sum()`, runs the orphan guard against active
reservations, then persists `RowLabels` CSV + `SeatsPerRow = variable ? counts.Max() :
SeatsPerRow` + `SeatCounts = variable ? join(counts) : null`. It writes an audit entry
`AuditEvents.HallSeatLayoutUpdated` with `Detail = "hallId=<id>; rows=<csv>;
seatsPerRow=<n>; seatCounts=<csv or (uniform)>"`.

## 6. Validation + error handling

All rules are enforced **server-side** in `SetLayoutAsync`; the page adds a client-side
mirror only for the seat-count range + capacity (to disable Save early). Every failure
round-trips and surfaces via `Error.MessageForCurrentCulture()`.

| Rule | Code / HTTP | Message (EN / AR) |
|------|-------------|-------------------|
| Row labels not 1 to 26 unique entries of 1 to 8 chars | `SEAT_LAYOUT_INVALID` 400 | "Row labels must be 1-26 unique entries of 1-8 chars each." / "يجب أن تكون رموز الصفوف بين 1 و 26 إدخالاً فريداً بطول 1 إلى 8 محارف." |
| Uniform path: `SeatsPerRow` outside 1 to 80 | `SEAT_LAYOUT_INVALID` 400 | "Seats per row must be between 1 and 80." / "يجب أن يكون عدد المقاعد في كل صف بين 1 و 80." |
| **D-767** `SeatCounts` length not equal to the row count | `SEAT_LAYOUT_INVALID` 400 | "Seat counts (N) must match the number of rows (M)." / "يجب أن يساوي عدد قيم المقاعد (N) عدد الصفوف (M)." |
| **D-767** a per-row count outside 1 to 80 | `SEAT_LAYOUT_INVALID` 400 | "Each row's seat count must be between 1 and 80." / "يجب أن يكون عدد مقاعد كل صف بين 1 و 80." |
| `sum(counts)` exceeds `Hall.Capacity` | `SEAT_CAPACITY_EXCEEDED` 400 | "Layout capacity (X) exceeds hall capacity (Y)." / "السعة المقترحة (X) تتجاوز سعة القاعة (Y)." |
| A change would strand active reservations (dropped row or a row shrunk below a booked seat) | `SEAT_LAYOUT_HAS_RESERVATIONS` 409 | (release those seats first) |
| Hall id not found | `HALL_NOT_FOUND` 404 | "Hall not found." / "لم يتم العثور على القاعة." |
| Halls list load failure | fallback | `Admin.HallSeatLayouts.LoadFailed` toast |

The client warning banner (`Admin.HallSeatLayouts.CapacityExceeded`) states that the
total exceeds the hall capacity or a row is outside the 1 to 80 range and blocks Save;
the definitive rejection is the API 400.

## 7. Edge cases + known limitations

- **Save always posts a parallel array.** The editor posts `SeatCounts = counts` even
  when every row is equal, so a CP-saved uniform layout persists a uniform-valued CSV
  (with `SeatsPerRow = max = that value`). The `SeatCounts = null` path is reached only
  by API callers that OMIT `SeatCounts` (e.g. the `BookingApprovalTests` seeder) - the
  pre-D-767 uniform contract, kept working unchanged.
- **`SeatsPerRow` is never removed.** It is the append-only wire fallback; a variable
  layout stores `max(counts)` so an un-upgraded mobile client renders max-width rows
  (phantom seats on short rows fail safe with 400 `SEAT_OUT_OF_BOUNDS`).
- **First layout vs edit.** A first-time layout (no existing row) skips the orphan guard
  (no seat-specific reservation can exist yet).
- **Corrupt persisted CSV** (a stored `SeatCounts` whose length no longer matches
  `RowLabels`, or a non-integer part) throws `SEAT_LAYOUT_INVALID` 500 on read
  (`ExpandSeatCounts`) - a deterministic fail, no silent fallback.
- **Unused resx key.** `Admin.HallSeatLayouts.Field.SeatsPerRow` may now be unreferenced
  by the page; it is flagged, not deleted (out of scope for D-767).

## 8. i18n + RTL

`Admin.HallSeatLayouts.*` keys (Title, Hint, Pick, None, Loading, Field.RowLabels,
**Field.RowSeats**, HallCapacity, **TotalSeats**, **CapacityExceeded**, Save,
LoadFailed) carry EN <-> AR parity; the three D-767 keys and the reworded Hint /
RowLabels landed in **both** `Strings.resx` and `Strings.ar.resx` (verified no drift).
Under Arabic the page + fields + Save button mirror to RTL
(`<html dir="rtl" lang="ar">`); the per-row seat-count number inputs mirror with the
page.

## 10. Use cases

- UC-HSL-EDIT-001 (define a uniform layout), UC-HSL-RAGGED-001 (D-767 - define a ragged
  per-row layout), UC-HSL-GUARD-001 (the Total-seats / capacity guard blocks Save),
  UC-HSL-ORPHAN-001 (a layout change that would strand booked seats is blocked 409).
  Feeds FR-503/903 (seat reservation) via the seat picker and the session seat-plan.

## 11. E2E

See [`docs/tests/e2e/cp-admin-halls-seat-layouts.md`](../../tests/e2e/cp-admin-halls-seat-layouts.md):
E2E-HSL-001 golden round-trip, 002 dropdown, 003 select/prefill, 004-005 live capacity,
006 stale-toast clear, 007 empty, 008 view auth gate, 009-011 row/seat validation, 012
uniform capacity conflict, 013 Save-permission gate, 014 server-500, 015 RTL, 016 orphan
guard, and the **D-767** additions **017** ragged round-trip, **018** Total-seats preview
+ disabled-Save guard, **019** count-mismatch 400, **020** out-of-range 400, **021**
sum-over-capacity 400, **022** uniform back-compat. API integration coverage:
`tests/SIMF.Api.Tests/SeatReservationsTests.cs` (uniform paths + orphan guard **plus 10
new D-767 variable-layout facts** - per-row bounds, sum-capacity, random-scan,
shrink-guard both ways, uniform-null back-compat, round-trip, count-mismatch,
out-of-range + over-capacity, and the wire seat-counts; suite 44/44 passing);
`BookingApprovalTests.cs` seeds a uniform layout. E2E-HSL-017..022 add the browser-level
CP-editor run on top of that xUnit coverage.

## 12. Related docs

- Related page: [`cp/admin-halls.md`](admin-halls.md) (the Halls CRUD page that feeds the
  dropdown and owns `Hall.Capacity`), [`cp/admin-sessions-seat-plans.md`](admin-sessions-seat-plans.md)
  (consumes the layout).
- Decisions: **D-767** (per-row variable seat counts), D-182 (CP editor for D-175 seat
  reservations), D-175 (seat reservation model), D-648 (SimfTextField ValueExpression
  freeze - why the seat inputs are raw), D-219 (append-only mobile wire), D-157 (App /
  Identity DB separation), D-110 (schema freeze + the standing additive-column lift).
- Wire contracts: `SeatReservations.cs` (`SetHallSeatLayoutRequest`,
  `HallSeatLayoutSnapshot`).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-25 | D-767 | **First authored page reference doc** (was `-` in `PAGE-INDEX.md`). Documents the D-767 per-row variable seat counts: the single "Seats per row" field became one seat-count input per row + a live "Total seats" readout + a disabled-Save guard; `SetHallSeatLayoutRequest` / `HallSeatLayoutSnapshot` append `SeatCounts`; migration `App/D767_AddHallSeatLayoutSeatCounts`; the count-mismatch / out-of-range / sum-over-capacity validation, and the uniform-null back-compat. |

_Last reviewed:_ 2026-07-25 by Claude (D-767 - authored from live source: contracts,
`SeatReservationService.SetLayoutAsync`, `HallSeatLayoutEditor.razor(.cs)`, the resx pair,
and the migration).
