# Hall seat-layout editor - `/admin/halls/seat-layouts`

| | |
|--|--|
| **Route** | `/admin/halls/seat-layouts` |
| **Audience** | Administrator (and any role granted `SeatLayouts.View`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.SeatLayouts.View)]` (CP page) + API `SeatLayouts.View` (GET) / `SeatLayouts.Edit` (PUT) / **`SeatLayouts.Delete` (DELETE, B15)** policies + `RequireApprovedAccount` |
| **Pattern** | Single-hall editor (D-182) - NOT a CRUD grid and NOT `SimfDataGrid`. A hall `<select>` (pre-selected from `?hallId=`, **A40**) -> the row-labels text input -> **one seat-count input per row** + a per-row tier select + a client-side validation list (**A40**) + a live **Total seats** readout + a **Save layout** button + a **Remove layout** button behind a `SimfConfirm` (**B15**). |
| **Status** | ✅ Real (D-182; **D-767** per-row variable seat counts; **D-771** per-row tiers; **B15** remove-layout; **A40** grid row action + client-side validation) |
| **Required permissions** | `SeatLayouts.View` (page + nav item `Module.HallSeatLayouts` + the Halls-grid row action + GET), `SeatLayouts.Edit` (Save / PUT), **`SeatLayouts.Delete` (Remove layout / DELETE - B15)** - `PermissionCatalog.SeatLayouts.*`, baseline `AdminOnly`. |
| **Backend endpoints** | BFF `/account/api/admin/halls/*` -> API: `POST /admin/halls/list` (`GridQuery`, fill the dropdown), `GET /admin/halls/{hallId}/seat-layout`, `PUT /admin/halls/{hallId}/seat-layout` (`SetHallSeatLayoutRequest`), **`DELETE /admin/halls/{hallId}/seat-layout` (B15)**. |
| **Backed by** | `dbo.HallSeatLayouts` - one row per hall (unique `HallId`). **D-767** adds `SeatCounts nvarchar(256) NULL` (migration `App/D767_AddHallSeatLayoutSeatCounts`); `RowLabels` (CSV) and `SeatsPerRow` are unchanged. |
| **Source** | [`HallSeatLayoutEditor.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallSeatLayoutEditor.razor) + [`.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallSeatLayoutEditor.razor.cs), [`SeatReservationEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Sessions/SeatReservationEndpoints.cs), [`SeatReservationService.cs`](../../../src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs), [`SeatReservations.cs`](../../../src/Shared/SIMF.Contracts/Sessions/SeatReservations.cs), [`HallSeatLayout.cs`](../../../src/Backend/SIMF.Domain/SeatReservations/HallSeatLayout.cs), [`HallSeatLayoutConfiguration.cs`](../../../src/Backend/SIMF.Infrastructure/Persistence/Configurations/App/HallSeatLayoutConfiguration.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-halls-seat-layouts.md`](../../tests/e2e/cp-admin-halls-seat-layouts.md) (E2E-HSL-001..030); xUnit `tests/SIMF.Api.Tests/SeatReservationsTests.cs`, bUnit `tests/SIMF.ControlPanel.Tests/HallSeatLayoutEditorTests.cs` + `HallsListSeatLayoutActionTests.cs` |
| **Last reviewed** | 2026-07-27 |

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

**A40 - reachability + client-side validation.** The editor used to be reachable only
from the side-menu item, which opens on a blank hall picker; the Halls grid offered no
way in at all. The grid now carries a quiet-icon **Seat layout** row action
(`SeatLayouts.View`) that navigates to `/admin/halls/seat-layouts?hallId={id}`, and the
editor reads that query parameter and pre-selects the hall. The page also validates
**client-side against exactly the server's rules** (1-26 rows, each label 1-8 chars,
labels unique case-insensitively, each per-row count 1-80, `sum(counts) <=
Hall.Capacity`) and **disables Save** while any rule fails, listing one message per
violation. The row-labels input's `maxlength="256"` equals the EF
`HallSeatLayout.RowLabels` `HasMaxLength(256)`. The server still re-validates
everything - the client mirror only removes the wasted round-trip.

**B15 - the layout can be removed.** Only GET and PUT existed, and an empty layout was
rejected 400 `SEAT_LAYOUT_INVALID`, so a laid-out hall could never go back to general
admission. A **Remove layout** button (behind `SeatLayouts.Delete` and a `SimfConfirm`)
now calls `DELETE .../seat-layout`. It applies the same orphan rule `SetLayoutAsync`
uses for a shrinking change - removing the grid strands EVERY active seat-specific
reservation, so a single one refuses the delete with 409
`SEAT_LAYOUT_HAS_RESERVATIONS`, naming **how many** the operator must release first.
Open-seating holds (null row/seat) never block: general admission needs no grid. After
a successful removal the hall's sessions report `Mode = OpenSeating` (D-706) and are
joined with one tap.

## 4. UI

- `SimfBanner` titled `Admin.HallSeatLayouts.Title` ("Hall seat layouts" / "مخططات
  مقاعد القاعات"), an `Admin.HallSeatLayouts.Hint` line, then the editor inside
  `simf-page-wide` / `simf-surface`.
- **Hall picker** - a native `<select>` (`Admin.HallSeatLayouts.Pick`, "Select a hall")
  with a blank first option; options read `{Code} {Name} (cap {Capacity})` and list only
  active halls, ordered by `Code`. When no active hall exists the page renders
  `SimfEmptyState` (`Admin.HallSeatLayouts.None`). **A40:** `[SupplyParameterFromQuery]
  public Guid? HallId` pre-selects the hall the Halls-grid row action deep-linked to and
  loads its layout on init; the picker stays blank when the page is opened from the nav.
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
- **Validation list (A40)** - a `<SimfAlert Variant="error">` listing one
  `Admin.HallSeatLayouts.Validation.*` message per broken rule (RowCount /
  RowLabelLength / RowLabelDuplicate / RowLabelsTooLong / SeatCount / Capacity),
  recomputed by `Revalidate()` after every edit. **Save layout**
  (`Admin.HallSeatLayouts.Save`) is disabled while the list is non-empty
  (`_canSave => _errors.Count == 0 && !_busy`), and `SaveAsync` re-runs the rules before
  posting. The capacity panel additionally turns amber (`hsl-capacity--over`) with its
  `Admin.HallSeatLayouts.CapacityExceeded` line on the two numeric rules - kept as the
  INLINE signal beside the numbers it is about, while the list carries the exact values.
  A hall with no layout and no typed rows is NOT an error state (`_notStarted`): the
  list stays hidden and the preview placeholder does the guiding, though Save is still
  disabled because an empty layout is a server 400 - **Remove layout** is the supported
  way to end up with no layout.
- **Remove layout (B15)** - a `Variant="danger"` **Remove layout**
  (`Admin.HallSeatLayouts.Delete`) button, rendered only when a layout exists
  (`_hasStoredLayout => _snapshot is { RowLabels.Count: > 0 }`) and wrapped in
  `<AuthorizedAction Permission="SeatLayouts.Delete">`. It opens a `SimfConfirm`
  (`Admin.HallSeatLayouts.Delete.Title` / `.Delete.Confirm`, `Danger="true"`,
  `RequireExplicitClose`) so the destructive call cannot be triggered by a stray
  backdrop click.
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
| **`DELETE /account/api/admin/halls/{hallId}/seat-layout`** (B15) | `DELETE /api/v1/admin/halls/{hallId}/seat-layout` | - | `ApiResult<HallSeatLayoutSnapshot>` - the now-EMPTY snapshot (`SeatLayouts.Delete`; `auth` limiter) |

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

**`DeleteLayoutAsync` (B15).** Resolves the hall (404 `HALL_NOT_FOUND`), then the layout
(404 `SEAT_LAYOUT_MISSING` when the hall was never laid out), counts the ACTIVE
(`ReleasedAt IS NULL`) seat-SPECIFIC (`RowLabel != null`) reservations across every
session in the hall, refuses with 409 `SEAT_LAYOUT_HAS_RESERVATIONS` naming that count
when it is non-zero, removes the row, and writes
`AuditEvents.HallSeatLayoutDeleted` with `Detail = "hallId=<id>; rows=<csv>;
seatsPerRow=<n>; seatCounts=<csv or (uniform)>"`. It returns an empty
`HallSeatLayoutSnapshot` (no rows, `SeatsPerRow = 0`, `LayoutCapacity = 0`, the hall's
own capacity preserved) so the editor can clear in one render.

## 6. Validation + error handling

All rules are enforced **server-side** in `SetLayoutAsync` / `DeleteLayoutAsync`; **A40**
adds a client-side mirror of every one of them so an invalid form disables Save instead
of round-tripping. A rule that still reaches the API (a direct call, or a state the
client cannot see - the orphan guard) surfaces via `Error.MessageForCurrentCulture()`.

| Rule | Code / HTTP | Message (EN / AR) |
|------|-------------|-------------------|
| Row labels not 1 to 26 unique entries of 1 to 8 chars | `SEAT_LAYOUT_INVALID` 400 | "Row labels must be 1-26 unique entries of 1-8 chars each." / "يجب أن تكون رموز الصفوف بين 1 و 26 إدخالاً فريداً بطول 1 إلى 8 محارف." |
| Uniform path: `SeatsPerRow` outside 1 to 80 | `SEAT_LAYOUT_INVALID` 400 | "Seats per row must be between 1 and 80." / "يجب أن يكون عدد المقاعد في كل صف بين 1 و 80." |
| **D-767** `SeatCounts` length not equal to the row count | `SEAT_LAYOUT_INVALID` 400 | "Seat counts (N) must match the number of rows (M)." / "يجب أن يساوي عدد قيم المقاعد (N) عدد الصفوف (M)." |
| **D-767** a per-row count outside 1 to 80 | `SEAT_LAYOUT_INVALID` 400 | "Each row's seat count must be between 1 and 80." / "يجب أن يكون عدد مقاعد كل صف بين 1 و 80." |
| `sum(counts)` exceeds `Hall.Capacity` | `SEAT_CAPACITY_EXCEEDED` 400 | "Layout capacity (X) exceeds hall capacity (Y)." / "السعة المقترحة (X) تتجاوز سعة القاعة (Y)." |
| A change would strand active reservations (dropped row or a row shrunk below a booked seat) | `SEAT_LAYOUT_HAS_RESERVATIONS` 409 | (release those seats first) |
| **B15** delete would strand N active seat reservations | `SEAT_LAYOUT_HAS_RESERVATIONS` 409 | "Removing this layout would strand N active seat reservation(s). Release them before removing the layout." / "ستؤدي إزالة هذا المخطط إلى إلغاء N حجز مقعد نشط. يرجى إلغاء هذه الحجوزات قبل إزالة المخطط." |
| **B15** delete on a hall with no layout | `SEAT_LAYOUT_MISSING` 404 | "This hall does not have a seat layout to remove." / "لا يوجد مخطط مقاعد لهذه القاعة لإزالته." |
| Hall id not found | `HALL_NOT_FOUND` 404 | "Hall not found." / "لم يتم العثور على القاعة." |
| Halls list load failure | fallback | `Admin.HallSeatLayouts.LoadFailed` toast |

**Client-side mirror (A40).** `Revalidate()` runs on every edit and on submit. Its
constants (`MinRows = 1`, `MaxRows = 26`, `MaxRowLabelLength = 8`,
`MinSeatsPerRow = 1`, `MaxSeatsPerRow = 80`, `MaxRowLabelsCsvLength = 256`) are the
service's rules and the EF `HasMaxLength`, and the same 256 is the input's
`maxlength` - the UI = FluentValidation-equivalent = EF triple-lock. The definitive
rejection is still the API 400/409.

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
- **B15 - the delete is the ONLY way to an empty layout.** `SetLayoutAsync` still
  rejects an empty `RowLabels` with 400 `SEAT_LAYOUT_INVALID`; A40 makes the editor
  refuse it client-side too. Removing the grid is a DELETE, not a "save nothing".
- **B15 - open-seating holds survive.** The blocking count only looks at seat-SPECIFIC
  reservations (`RowLabel != null`). A general-admission join has no seat to strand, so
  it neither blocks the removal nor is invalidated by it.
- **B15 - the hall row is untouched.** Only the `HallSeatLayouts` row is removed;
  `Hall.Capacity` and the hall's `SeatSelectionMode` are unchanged. The switch to
  one-tap join comes from D-706's `EffectiveMode` rule (no layout => `OpenSeating`),
  not from editing the hall.
- **A40 - an unknown `?hallId=`** (a hall that is inactive or does not exist) is ignored:
  the picker stays on its blank entry rather than showing a half-loaded state.

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
  UC-HSL-ORPHAN-001 (a layout change that would strand booked seats is blocked 409),
  **UC-HSL-REACH-001** (A40 - reach the editor from the Halls grid), **UC-HSL-VALID-001**
  (A40 - a broken rule is refused before the round-trip), **UC-HSL-REMOVE-001** (B15 -
  convert a laid-out hall back to general admission), **UC-HSL-REMOVE-002** (B15 - the
  removal is refused while reservations are live).
  Feeds FR-503/903 (seat reservation) via the seat picker and the session seat-plan.

## 11. E2E

See [`docs/tests/e2e/cp-admin-halls-seat-layouts.md`](../../tests/e2e/cp-admin-halls-seat-layouts.md):
E2E-HSL-001 golden round-trip, 002 dropdown, 003 select/prefill, 004-005 live capacity,
006 stale-toast clear, 007 empty, 008 view auth gate, 009-011 row/seat validation, 012
uniform capacity conflict, 013 Save-permission gate, 014 server-500, 015 RTL, 016 orphan
guard, and the **D-767** additions **017** ragged round-trip, **018** Total-seats preview
+ disabled-Save guard, **019** count-mismatch 400, **020** out-of-range 400, **021**
sum-over-capacity 400, **022** uniform back-compat, and the **A40 / B15** additions
**024** grid row action + `?hallId=` deep link, **025** row-action permission gate,
**026** the client-side validation mirror, **027** remove-layout happy path, **028**
remove refused 409 with the blocking count, **029** remove permission gate, **030**
remove on a hall with no layout 404. API integration coverage:
`tests/SIMF.Api.Tests/SeatReservationsTests.cs` (uniform paths + orphan guard **plus 10
new D-767 variable-layout facts** - per-row bounds, sum-capacity, random-scan,
shrink-guard both ways, uniform-null back-compat, round-trip, count-mismatch,
out-of-range + over-capacity, the wire seat-counts, **plus 5 B15 delete facts** - revert
to general admission, blocked-by-active-reservation with the count in the message,
released-reservations-ignored, 404 on a hall with no layout, and delete-then-redefine;
suite 49/49 passing) and bUnit `tests/SIMF.ControlPanel.Tests/HallSeatLayoutEditorTests.cs`
(13 facts: the `?hallId=` deep link, the six client-side rules, the quiet
not-yet-started state, the `maxlength` alignment, and the four remove-layout states) +
`HallsListSeatLayoutActionTests.cs` (3 facts: the row action renders, deep-links, and is
permission-gated);
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
| 2026-07-26 | D-771 | **Seat TIERS per row.** Each row now carries a `SeatTier` select (`Normal` / `VIP` / `VVIP (reserved)`) beside its seat count, plus a tier hint and a coloured tier band on the preview row. `SetHallSeatLayoutRequest` / `HallSeatLayoutSnapshot` append `SeatTiers` (parallel to `RowLabels`); persisted as a CSV in `HallSeatLayout.SeatTiers` (migration `App/D771_AddSeatTiersAndVvipGuestHint`). **Authoring default: a NEWLY added row starts `VVIP` (reserved)** per the owner — the admin downgrades rows deliberately. A layout stored before D-771 (null CSV) reads as all-`Normal`, so no shipped session loses a bookable seat. The editor always sends explicit tiers; an omitted `SeatTiers` on a hall that already has a layout keeps the stored tiers, and on a first-time layout defaults every row to `VVIP`. The Save button is now wrapped in `<AuthorizedAction Permission="SeatLayouts.Edit">`. |

| 2026-07-27 | B15 / A40 | **Remove layout + reachability + client-side validation.** B15: new `DELETE /admin/halls/{hallId}/seat-layout` (`SeatLayouts.Delete`, a NEW catalogue permission), `ISeatReservationService.DeleteLayoutAsync`, a `Variant="danger"` **Remove layout** button behind a `SimfConfirm`, and `AuditEvents.HallSeatLayoutDeleted`. The removal is refused 409 `SEAT_LAYOUT_HAS_RESERVATIONS` while any active seat-specific reservation would be stranded, and the message names how many. A40: a quiet-icon **Seat layout** row action on `/admin/halls` deep-links to `?hallId=`, which the editor pre-selects; `Revalidate()` mirrors every server rule client-side and disables Save, and the row-labels input's `maxlength="256"` equals the EF `HasMaxLength(256)`. **No migration** - the delete is a row removal on the existing `HallSeatLayouts` table. |

_Last reviewed:_ 2026-07-27 by Claude (B15 + A40). Prior: 2026-07-25 by Claude (D-767 - authored from live source: contracts,
`SeatReservationService.SetLayoutAsync`, `HallSeatLayoutEditor.razor(.cs)`, the resx pair,
and the migration).
