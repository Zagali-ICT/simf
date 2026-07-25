# Hall seat layouts — per-row variable seat counts (Option A) + seat-picker UX — approved design plan

Status: **APPROVED, implementation HELD** pending a clean base that includes the D-766
InlineContactDirectory migration (a `SeatCounts` EF migration cannot chain onto an
uncommitted D-766 + dirty model snapshot). Authored 2026-07-25 from a six-surface design
pass. This is a planning artifact, not a controlled document; the binding record is the
DECISIONS_LOG entry to be added when the change lands.

Owner-approved decisions: Option A (per-row variable seat counts via an additive nullable
`HallSeatLayout.SeatCounts` CSV parallel to `RowLabels`, keeping `SeatsPerRow` as the
uniform fallback); seat-picker UX = seat number in each cell + tap→select→Confirm chip +
a11y icon for reserved/your-seat; maxLength 256; variable `seatsPerRow` stored as
`max(counts)`; short rows centred under the stage.

---

### 1. What I understood

You want ONE ordered, owner-approvable implementation plan that consolidates six independent surface analyses into a single build-ordered change set for **Option A — per-row variable seat counts** on hall seat layouts, PLUS three approved Flutter seat-picker UX improvements (seat-number in each cell, tap-to-select→confirm chip, and a non-colour a11y icon for reserved/your-seat states). This is a **D-110 frozen-schema change** requiring an explicit owner freeze-lift before the migration is added.

The core mechanism agreed across all six analyses: add ONE additive, **nullable** column `HallSeatLayouts.SeatCounts nvarchar(N)` — a CSV of ints **parallel** to the existing `RowLabels` CSV (`"4,10,8,8"`). `SeatsPerRow` is **kept, never removed**; when `SeatCounts IS NULL` the layout stays uniform (exactly today's behaviour). No shipped mobile JSON key is renamed or removed — `seatCounts` is a new append-only field. No child table (rejected as over-engineering for a <=26-row aggregate always read/written whole).

This is a **change-spec / plan only**. No production code is written until you approve. I have NOT run a build, `dotnet ef`, or `flutter test` — those are in the verification gate below.

One cross-analysis conflict I must surface: the EF `HasMaxLength` for the CSV column was proposed as **256** (schema surface, to mirror `RowLabels`) and as **128** (service + contracts surfaces). Both fit the worst case (~78 chars). I recommend **256** to mirror the `RowLabels(256)` convention exactly — flagged in Open Questions.

---

### 2. What the change is (ordered, file-by-file)

Build order is strict: contracts/schema must compile before the CP and app can reference `SeatCounts`.

#### (i) Schema + migration — `SimfAppDbContext`

**`src/Backend/SIMF.Domain/SeatReservations/HallSeatLayout.cs`** — Risk: **none**
Add ONE nullable property after `SeatsPerRow`: `public string? SeatCounts { get; set; }`. Amend the class-level XML doc (currently states the grid is uniform) to describe the optional per-row override: `SeatsPerRow` is the uniform fallback when `SeatCounts` is null; when set, `#counts == #RowLabels`, each count 1..80, and the capacity check becomes `sum(SeatCounts) <= Hall.Capacity`. Add a `// Tests: SIMF.Api.Tests/SeatReservationsTests.cs` header. Do NOT touch `RowLabels` or `SeatsPerRow`.

**`src/Backend/SIMF.Infrastructure/Persistence/Configurations/App/HallSeatLayoutConfiguration.cs`** — Risk: **none**
Add ONE line after the `RowLabels` config: `builder.Property(x => x.SeatCounts).HasMaxLength(256);` — NULLABLE (no `.IsRequired()`) → `nvarchar(256) NULL`. Do NOT touch the `RowLabels` config, the Hall FK, or the unique `HallId` index.

**`src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/<timestamp>_D-XXX_AddHallSeatLayoutSeatCounts.cs (+ .Designer.cs)` — NEW** — Risk: **breaking** (schema; freeze-lift gated)
Generate from repo root (NEVER `--no-build`, per the D-611 stale-assembly trap):
```
dotnet ef migrations add D-XXX_AddHallSeatLayoutSeatCounts --context SimfAppDbContext \
  --project src/Backend/SIMF.Infrastructure/SIMF.Infrastructure.csproj \
  --startup-project src/Backend/SIMF.Api/SIMF.Api.csproj \
  --output-dir Persistence/Migrations/App
```
Expected shape:
```
Up():   migrationBuilder.AddColumn<string>(name: "SeatCounts", table: "HallSeatLayouts",
                                            type: "nvarchar(256)", maxLength: 256, nullable: true);
Down(): migrationBuilder.DropColumn(name: "SeatCounts", table: "HallSeatLayouts");
```
**GATE:** after generation, verify `Up()` contains ONLY that `AddColumn` and NO `CreateIndex`/`DropIndex`/`AlterColumn` on `SeatReservations`. The `SeatReservations` filtered unique indexes (e.g. `IX_SeatReservations_SessionId_RowLabel_SeatNumber`) must be untouched; any index churn signals model-snapshot drift → reject, do not ship.

**`src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/SimfAppDbContextModelSnapshot.cs`** — Risk: **none**
EF rewrites this automatically as part of `migrations add`. Do NOT hand-edit; confirm the only diff is the new `SeatCounts` property on the `HallSeatLayout` block (no unrelated drift).

#### (ii) Contracts + wire — `src/Shared/SIMF.Contracts/Sessions/SeatReservations.cs` — Risk: **breaking** (mobile-wire-frozen DTO; changed additively)

Three additive edits, all append-only:

1. **`SessionSeatMap`** (MOBILE-WIRE-FROZEN, D-219): APPEND `IReadOnlyList<int>? SeatCounts = null` at the very END of the positional record, **after `Mode`** (matches the D-432 `SessionTitle` / D-485 `Mode` append-only precedent). KEEP `SeatsPerRow` at its exact name and position. Serializes as camelCase `seatCounts`.
   - Before: `record SessionSeatMap(..., IReadOnlyList<string> RowLabels, int SeatsPerRow, ..., SeatSelectionMode Mode = ...)`
   - After: `record SessionSeatMap(..., int SeatsPerRow, ..., SeatSelectionMode Mode = ..., IReadOnlyList<int>? SeatCounts = null)`

2. **`HallSeatLayoutSnapshot`** (CP-only read-back): APPEND `IReadOnlyList<int>? SeatCounts = null` at the end. KEEP `SeatsPerRow`. NO new capacity field — the existing `LayoutCapacity` is reused; only its computed VALUE changes in the service to `sum(SeatCounts)` when variable.

3. **`SetHallSeatLayoutRequest`** (CP-only write): ADD `public IReadOnlyList<int>? SeatCounts { get; set; }` (nullable: null/empty = uniform via `SeatsPerRow`). KEEP `SeatsPerRow`. Update the XML `<summary>`: when non-empty, `SeatCounts` is authoritative, its length MUST equal `RowLabels` count, each element 1..80, `sum(SeatCounts) <= Hall.Capacity`.

`SessionSeatCell` and `MySeatReservation` are UNCHANGED (per-seat, already carry `rowLabel`+`seatNumber`).

#### (iii) Backend service + endpoints

**`src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs`** — Risk: **none** (behaviour extended; uniform path preserved as null default)
Contain the uniform-width assumption behind one expanded array carried on the private `SessionContext`:

- Add field `IReadOnlyList<int> SeatCounts` to `SessionContext`.
- Add helper `ExpandSeatCounts(HallSeatLayout, IReadOnlyList<string> rowLabels)`: if `SeatCounts` null/blank → `Repeat(SeatsPerRow, rows.Count)`; else split CSV, `int.Parse` each; if parsed length != rowLabels.Count or any parse fails → log + throw `ApiException(SeatLayoutInvalid, 500)` (corrupt persisted state, deterministic, no silent fallback).
- Add helper `RowIndex(rowLabels, label)` (OrdinalIgnoreCase, `-1` if absent) — used by three call sites.
- `BuildContextAsync`: compute `rowLabels` + `ExpandSeatCounts` and pass both into `SessionContext`.
- Mechanical `SeatsPerRow` → per-row substitutions:
  - `ValidateSeatBounds`: `var i = RowIndex(...); if (i<0) throw SeatOutOfBounds; if (seat < 1 || seat > ctx.SeatCounts[i]) throw` (message `between 1 and {ctx.SeatCounts[i]}`, EN+AR).
  - `EffectiveCapacity`: `ctx.RowLabels.Count * SeatsPerRow` → `ctx.SeatCounts.Sum()` (outer `Math.Min(..., override ?? hallCapacity)` unchanged).
  - `PickRandomSeat`: index loop `for i … for seat 1..ctx.SeatCounts[i]`.
  - `AdminReserveRowAsync`: loop bound `SeatsPerRow` → `ctx.SeatCounts[i]`.
  - `EnsureLayoutChangeKeepsActiveReservationsAsync`: param `int newSeatsPerRow` → `IReadOnlyList<int> newSeatCounts`; orphan test = row absent OR `SeatNumber > newSeatCounts[index]`.
- `SetLayoutAsync`: branch on `request.SeatCounts` —
  - null/empty → existing uniform path (validate `SeatsPerRow` 1..80, `layoutCapacity = rows.Count * SeatsPerRow`, store `SeatCounts = null`).
  - non-empty → validate `counts.Count == rows.Count` (else 400 `SEAT_LAYOUT_INVALID`), each 1..80 (else 400 `SEAT_LAYOUT_INVALID`), `layoutCapacity = counts.Sum()`; existing `layoutCapacity > hall.Capacity` → 400 `SEAT_CAPACITY_EXCEEDED`. On persist set `layout.SeatCounts = countsCsv` and `layout.SeatsPerRow = counts.Max()` (legacy fallback). Extend audit Detail with `seatCounts=…`.
- `GetSessionSeatMapAsync` / `GetLayoutAsync`: compute expanded counts; keep emitting `layout?.SeatsPerRow ?? 0` for the FROZEN key AND append `seatCounts`; `LayoutCapacity = seatCounts.Sum()`.

Before/after (capacity):
```
// before
var layoutCapacity = ctx.RowLabels.Count * ctx.Layout!.SeatsPerRow;
// after
var layoutCapacity = ctx.SeatCounts.Sum();   // == Repeat(SeatsPerRow) sum when uniform
```
Before/after (bounds):
```
// before
if (!ctx.RowLabels.Contains(row) || seat < 1 || seat > ctx.Layout!.SeatsPerRow) throw SeatOutOfBounds;
// after
var i = RowIndex(ctx.RowLabels, row);
if (i < 0 || seat < 1 || seat > ctx.SeatCounts[i]) throw SeatOutOfBounds;
```

**`src/Backend/SIMF.Api/Endpoints/Sessions/SeatReservationEndpoints.cs`** — Risk: **none**
In `SetHallSeatLayoutEndpoint.HandleAsync`, the over-post-safe re-projection currently copies only `RowLabels`+`SeatsPerRow`. ADD `SeatCounts = req.SeatCounts,` so the new field flows through. All other endpoints serialize the DTOs and pick up the appended fields for free — no change.

#### (iv) Control Panel (Blazor Server)

**`HallSeatLayoutEditor.razor` + `.razor.cs`** — Risk: **breaking** (persisted write semantics change; must ship with the service)
Replace the single "seats per row" number field with one small **raw** `<input type=number min=1 max=80>` beside EACH parsed row label (raw input, matching the existing page, to sidestep the SimfTextField-without-ValueExpression freeze, D-648). Keep the RowLabels text input as the row-set source. Add a live per-row preview + Total (`sum`) validated against `Hall.Capacity` with a `<SimfAlert Variant="warning">` and disabled Save when `sum > capacity` or any count out of 1..80.
Code-behind: introduce `record RowSeat { Label; Count; }` + `List<RowSeat> _rows`; `LoadLayoutAsync` builds `_rows` from `SeatCounts` (or fills from `SeatsPerRow` when null — back-compat); `OnRowLabelsChanged` reconciles `_rows` POSITIONALLY (rename keeps the position's count); `OnRowCountChanged(idx, e)` clamps/stores; `SaveAsync` posts `SeatCounts = counts` + `SeatsPerRow = counts.Max()`. Remove `_seatsPerRow` / `OnSeatsPerRowChanged`. Client guard mirrors the triple-lock.

**`SessionSeatPlan.razor` + `.razor.cs`** — Risk: **none**
Convert the outer `@foreach (rowLabel in RowLabels)` to an index loop; inner seat loop bound becomes `SeatsInRow(r)`. Add helper `int SeatsInRow(int rowIndex) => _layout is null ? 0 : (_layout.SeatCounts is { Count: >0 } sc && rowIndex < sc.Count ? sc[rowIndex] : _layout.SeatsPerRow);`.

**`SessionLiveHall.razor` + `.razor.cs`** — Risk: **none**
Identical index-loop + `SeatsInRow` change against `SessionSeatMap`.

**`SessionSeatPlan.razor.css` / `SessionLiveHall.razor.css`** — Risk: **none** — **NO CHANGE REQUIRED.** Each `.seatgrid__row` / `.seatmap__row` is already an independent flexbox (never a fixed-column CSS grid), so fewer seats → narrower row automatically, and RTL right-alignment already holds. The uniform-width assumption lived ONLY in the Razor `@for` loops.

**`Resources/Strings.resx` + `Strings.ar.resx`** — Risk: **none**
Add bilingual keys for the redesigned editor: `Admin.HallSeatLayouts.Field.RowSeats`, `.TotalSeats`, `.CapacityExceeded`; reword `Field.RowLabels`/`Hint` to drop the "× seats-per-row" phrasing. The resx pair must not drift. Do NOT delete the now-possibly-unused `Field.SeatsPerRow` key (flag, don't remove — out of scope).

#### (v) Flutter app — variable-width render + the 3 UX improvements

**`lib/features/sessions/data/seat_map_models.dart`** — Risk: **breaking** (wire-touching; changed additively)
Add `final List<int> seatCounts;` with ctor default `const <int>[]` (so every existing `const SessionSeatMap(...)` fixture keeps compiling and renders uniform). In `fromJson`, ADD a new parse right after the retained `seatsPerRow` line: `seatCounts: (json['seatCounts'] as List? ?? const []).whereType<num>().map((n)=>n.toInt()).toList(growable:false)`. Add `int seatsInRow(int i)` (tolerant per-index fallback to `seatsPerRow`) and `int get maxSeatsPerRow` (plain for-loop, no `dart:math` import). Widen `hasLayout` → `rowLabels.isNotEmpty && (seatsPerRow > 0 || seatCounts.any((c)=>c>0))`. Update the stale class doc.

**`lib/features/sessions/widgets/hall_seat_map.dart`** — Risk: **none** (shared card — all three visual changes land here)
- **Variable width, UNIFORM seat size:** compute `columns = map.maxSeatsPerRow` once; pass `seatCount: map.seatsInRow(index)` + `columns` into `_SeatGridRow`. Size math denominator becomes `columns` (all rows same seat size); loop bound becomes `s <= seatCount`. Restructure the row so the label is pinned start and short rows' seats are centred within the shared seats-area (`Expanded(child: Center(child: Row(mainAxisSize.min, …)))`).
- **(2a) seat number:** render a centred numeral in each cell via new token text styles (no raw TextStyle/Color).
- **(2c) a11y icon:** on reserved + mine cells show a token-sized Material icon in place of the number, add `Semantics(label:)` reusing `legendReserved`/`legendMine` + seat id; mirror the two icons into the legend swatches.
- Update the stale `plain seat squares (no number)` comment. Grid stays force-LTR (L-7).

**`lib/features/sessions/seat_picker_screen.dart`** — Risk: **breaking** (converts shipped one-tap-reserve into tap→select→confirm; owner sign-off item)
- **(2b) confirmation chip:** add `String? _selectedRow; int? _selectedSeat;`. Change `onSeatTap` to SELECT (setState) instead of reserving; highlight the tapped cell. Insert a selected-seat chip above the auto-pick button (`l10n.seatPickerSelectedChip(row, seat)`), shown only when selected. Add a "Confirm my seat" CTA (`l10n.seatPickerConfirmCta`), enabled only when selected, that calls the existing `_reserve(...)` unchanged. Pull-to-refresh, RTL, const preserved.

**`lib/features/sessions/my_seat_screen.dart`** — Risk: **none** — NO functional change; inherits every visual change through the shared `HallSeatMapCard`. Only its golden regenerates. Optionally refresh one doc-header clause.

**`lib/app/theme/tokens.dart`** — Risk: **none** — additive tokens only: `seatNumberSize`, `seatStateIconSize`, and two numeral styles `seatNumberOnDark` / `seatNumberOnGold` (w600, height 1). No existing token altered.

**`lib/app/localization/app_l10n.dart`** — Risk: **none** — additive ar+en: `seatPickerSelectedChip(row, seat)`, `seatPickerConfirmCta`. Reuse existing `legendReserved`/`legendMine` for the new Semantics.

#### (vi) Tests

- **`tests/SIMF.Api.Tests/SeatReservationsTests.cs`** — Risk: **none** — new helper `SeedSessionWithVariableLayoutAsync` (existing uniform helper KEPT); ~9 facts: per-row bounds (seat 10 valid in a 10-row, 400 in an 8-row), capacity = `sum(counts)`, random allocator across variable rows never yields a phantom seat, orphan guard on shrink (409), unbooked shrink (200), uniform-null back-compat, admin set variable round-trip, count-mismatch 400, count-range 400, sum-over-capacity 400.
- **`test/features/sessions/seat_map_models_test.dart`** — parses `seatCounts` + `seatsInRow`, uniform fallback when absent, `seatsPerRow:0`+counts still `hasLayout`, length-mismatch falls back.
- **`test/features/sessions/widgets/hall_seat_map_test.dart` — NEW** — each row at its own count, seat-number label on every seat, uniform fallback, tappable seat returns correct row+seat.
- **`seat_picker_screen_test.dart`** — REWRITE the tap-to-reserve test to tap→select (chip appears, no POST) → Confirm CTA fires `_reserve`; add variable-width no-phantom-seat case.
- **`my_seat_screen_test.dart`** — add ragged variable-grid render case; existing cases stay green.
- **Goldens** (`seat_picker_golden_test.dart`, `my_seat_golden_test.dart` + the two `.png`) — switch fixtures to variable layouts and REGENERATE via `--update-goldens` on a FontLoader host. NOTE: the seat-number label repaints every cell, so both goldens go red until regenerated — do NOT conflate with the 6 pre-existing red goldens in project memory.

#### (vii) Docs / E2E (same changeset, per D-246)

- **`docs/decisions/DECISIONS_LOG.md`** — Risk: **none** — prepend the D-entry (text in §3).
- **E2E:** update `cp-admin-halls-seat-layouts.md` (+ E2E-HSL-017..022), `cp-admin-sessions-seat-plans.md` (+ E2E-SSP-016), `mobile-seat-picker.md` (+ E2E-MOBPICK-011), `mobile-my-seat.md` (+ E2E-MOB018-018); correct the stale ranges in `docs/tests/e2e/README.md` and add a dated Option-A block.
- **`docs/pages/PAGE-INDEX.md`** — fill the `—` doc columns for `/admin/halls/seat-layouts` and `/admin/sessions/seat-plans`.
- **NEW reference docs:** `docs/pages/cp/admin-halls-seat-layouts.md` and `docs/pages/cp/admin-sessions-seat-plans.md` (both currently absent) authored from the CP template.

---

### 3. Freeze-lift + DECISIONS_LOG

**Freeze-lift rationale (D-110).** The event has rows of genuinely different widths (a 4-seat VIP row above 10/8/8 general rows); one uniform `SeatsPerRow` cannot express that. Additive `SimfAppDbContext` columns are already permitted under the standing D-199/D-211/D-219 lift (as D-758/D-760/D-762/D-766 did); this records the owner's explicit approval for THIS column. A nullable parallel CSV is the minimal additive change matching the `RowLabels` convention, keeps every existing layout working unchanged (null = uniform, no backfill), and preserves the shipped mobile wire as append-only. The **Identity** schema stays frozen (D-110/D-157); no cross-DB relation. The D-110 freeze must be re-instated before the production publish/handover.

**Exact new DECISIONS_LOG row** (owner assigns the real D-number — likely D-767, since the log's highest authored row is D-765 but a `D766_*` migration already exists with no matching log row; keep the migration prefix and log id equal):

| D-XXX | 2026-07-25 | **Hall seat layouts gain per-row variable seat counts (additive nullable `HallSeatLayout.SeatCounts` CSV).** A `HallSeatLayout` (App DB) carried `RowLabels` (CSV) + a single uniform `SeatsPerRow int`, so every row was the same width. Owner-approved (Option A): add an ADDITIVE, **nullable** column `HallSeatLayouts.SeatCounts nvarchar(256)` = a CSV of ints PARALLEL to `RowLabels` (`"4,10,8,8"`) giving each row its own count; `SeatsPerRow` is KEPT for backward-compat, and when `SeatCounts IS NULL` the layout stays uniform (`SeatsPerRow` per row, unchanged). Migration `App/D-XXX_AddHallSeatLayoutSeatCounts` (single-column `AddColumn`/`DropColumn`, `HasMaxLength(256)` mirroring `RowLabels`; a pure AddColumn that does NOT touch the `SeatReservations` filtered unique indexes). Invariants (service layer, mirroring the existing inline row-label checks in `SeatReservationService.SetLayoutAsync`): each count 1..80, `#counts == #RowLabels`, `sum(counts) <= Hall.Capacity`. When variable, the retained `SeatsPerRow` stores `max(counts)` as the legacy uniform fallback. CSV-parallel chosen over a child table: the layout is always read/rewritten wholesale (`LoadLayoutAsync` = `SingleOrDefaultAsync`), the row set is already a CSV aggregate, and a child table would duplicate `RowLabels` or force reworking a frozen column. **Freeze:** additive `SimfAppDbContext` columns are already permitted under the standing D-199/D-211/D-219 lift; this records explicit owner approval for this column, and the D-110 freeze must be re-instated before the production publish/handover. **Wire contract (D-219):** the shipped app decodes `SessionSeatMap.seatsPerRow` (+ `rowLabels`, `reservedCells`) — NO shipped JSON key is renamed/removed; `seatCounts` is a NEW append-only field (old builds keep working uniform via `seatsPerRow`; new builds prefer `seatCounts`). No Identity change (D-110/D-157); no cross-DB relation. Also delivers three approved seat-picker UX improvements (seat number in each cell, tap→select→confirm chip, a11y icon for reserved/your-seat). Downstream: contracts (`SetHallSeatLayoutRequest`/`HallSeatLayoutSnapshot`/`SessionSeatMap` += `SeatCounts`), `SeatReservationService` per-row `EffectiveCapacity`/`ValidateSeatBounds`/`PickRandomSeat`/`AdminReserveRow`, CP `HallSeatLayoutEditor`/`SessionSeatPlan`/`SessionLiveHall`, app `seat_map_models` + `hall_seat_map`. | The event has rows of genuinely different widths; one `SeatsPerRow` cannot express that. A nullable parallel CSV is the minimal additive change matching the existing `RowLabels` convention, keeps every existing layout working unchanged (null = uniform), and preserves the shipped mobile wire as append-only. A child table was rejected as over-engineering for an aggregate always loaded and saved as one unit. |

---

### 4. D-219 wire-contract guarantee

The deployed app decodes these camelCase `SessionSeatMap` keys, which are NEVER renamed or removed: `sessionId`, `hallId`, `hallCapacity`, `sessionCapacity`, `rowLabels`, `seatsPerRow`, `reservedCells`, `myCell`, `activeReservedCount`, `sessionTitle`, `sessionTitleArabic`, `mode`. The ONLY wire change is a NEW additive key `seatCounts` (JSON int array, parallel to `rowLabels`), appended after `Mode` on the record.

- **Uniform layout:** server emits `SeatCounts = null` → key omitted. Old AND new apps read `seatsPerRow` and render uniformly. Identical to today.
- **Variable layout:** server emits `seatCounts` AND still emits a representative non-zero `seatsPerRow = max(seatCounts)`.
  - **Old app build:** ignores the unknown `seatCounts`, draws every row at `seatsPerRow` (= max). It may show phantom seats on short rows; tapping one fails SAFELY with the existing 400 `SEAT_OUT_OF_BOUNDS` (no data corruption). This is the accepted degradation for un-upgraded clients — a variable layout is inherently un-representable by a uniform renderer, and `max` (vs `min`) never HIDES a real bookable seat.
  - **New app build:** prefers `seatCounts` when present and `length == rowLabels.length`; row `i` draws `seatCounts[i]`; falls back to `seatsPerRow` if absent or length-mismatched.

`HallSeatLayoutSnapshot` and `SetHallSeatLayoutRequest` are CP-only (admin GET/PUT), NOT decoded by the mobile app — changed additively anyway. Guard in review: any reorder/rename of `SessionSeatMap` members breaks the shipped app.

---

### 5. Test + verification plan

Live-verification gate (§17), in order — lead the delivery message with real output, never assertions:

1. **Unit + integration (backend):** `dotnet test tests/SIMF.Api.Tests` — the ~9 new variable-layout facts green PLUS all existing uniform SeatReservations / AdminHallCapacity / BookingLifecycle cases still green (null = uniform default). Paste real counts.
2. **Flutter analyze + test:** `flutter analyze` (0 issues) then `flutter test` — new model/widget/screen tests green; both regenerated goldens green (verify on FontLoader host). Report the pass count and confirm the 2 seat goldens are NOT among the 6 known pre-existing reds.
3. **Clean build:** `dotnet build -c Release` → 0 warnings / 0 errors (note the QA-round DEF-001 AngleSharp restore workaround `-p:NuGetAudit=false` if it recurs, and say so explicitly).
4. **Migration gate:** diff the generated `Up()`/Designer/snapshot — confirm the ONLY delta is the `SeatCounts` `AddColumn` on `HallSeatLayouts`; NO `SeatReservations` index churn, no unrelated `AlterColumn`.
5. **Live DOM check (CP):** open `HallSeatLayoutEditor`, `SessionSeatPlan`, `SessionLiveHall` in the running Control Panel — full-page screenshot, console (zero errors), network (zero 404s/broken assets), and `scrollWidth == clientWidth` (no horizontal overflow). Confirm variable rows render ragged and RTL-right-aligned, and the capacity-exceeded alert + disabled Save fire.
6. **Live render (app):** render the seat picker + my-seat on the device (TXZ W09 tablet per memory) against a variable layout; confirm ragged widths, seat numbers, the reserved/mine icons, the selection chip, and the Confirm CTA.
7. **E2E catalogue:** drive E2E-HSL-017..022, E2E-SSP-016, E2E-MOBPICK-011, E2E-MOB018-018 plus the updated golden-path scenarios.
8. **Review agents + `simplify`** on the changed code, then **commit** (do not push unless asked). Docs/E2E land in the SAME changeset (D-246).

---

### 6. What I will NOT touch

- `RowLabels` (column, config, or CSV shape) and the existing uniform `SeatsPerRow` semantics — both retained.
- The `SeatReservations` filtered unique indexes, the Hall FK, or the unique `HallId` index.
- The **Identity** schema and any cross-DB relation (D-110/D-157) — no change either direction.
- `SessionSeatCell` / `MySeatReservation` contracts and the app's `SeatCell` / `MyReservation` models (per-seat, unaffected).
- The two seat-grid `.razor.css` files (per-row flexbox already handles variable width + RTL).
- The Flutter grid's force-LTR behaviour (L-7), const constructors on existing fixtures (kept compiling via the `const []` default), and `my_seat_screen.dart` logic.
- `.csproj` / project settings, the SDK, and any migration other than the one new additive migration.
- The now-possibly-unused `Field.SeatsPerRow` resx key — flagged, not deleted (out of scope).
- The concurrency backstop (`InsertHoldWithinCapacityAsync` / `EnforceCapacityAfterInsertAsync`) — unchanged; only `EffectiveCapacity` now sums the per-row array.
- No refactors, renames, or "while I'm here" cleanups anywhere.

---

### 7. Open questions for the owner

1. **`HasMaxLength` for the `SeatCounts` column: 256 or 128?** The schema analysis proposed **256** (mirrors `RowLabels(256)`); the service and contracts analyses proposed **128**. Both fit the ~78-char worst case. I recommend **256** for convention consistency. Confirm.
2. **Legacy `SeatsPerRow` / wire `seatsPerRow` value for a variable layout:** recommend **`max(counts)`** (never hides a real seat from old apps; over-shown phantom seats fail safely with 400). Alternatives: `min(counts)` (hides seats, never allows an invalid pick) or a stored last-uniform value. Confirm `max`.
3. **Seat-picker behaviour change (2b):** the confirmation chip requires converting the SHIPPED one-tap-reserve flow (D-485/D-750) into **tap→select→confirm**. Approve the two-step flow, OR keep immediate-reserve and drop the chip.
4. **Figma-unmapped visuals (§13.5 ASK-don't-guess):** the seat-number numeral (recommend ~9px, w600; Western digits; beige/white on dark cells, navy on the gold "mine" cell), the reserved/mine **icon glyphs** (recommend number on available cells, icon replacing the number on reserved/mine), and the chip/CTA shape are in NO mapped node (frame 898:2873 seats are blank; the picker has no frame, D-600). Confirm before build.
5. **Short-row alignment:** recommend CENTRE short rows under the stage (labels pinned in a fixed left column). Alternative: left/right-align to the label column. Confirm the hall-plan look.
6. **New-row default seat count in the CP editor:** recommend seeding from the loaded uniform `SeatsPerRow` (else 1). Confirm.
7. **D-number:** the log ends at D-765 but a `D766_*` migration exists with no matching row, so **D-767** is the first collision-free id for both the migration prefix and the log entry. Confirm the id to cite across the migration, DECISIONS_LOG, and the E2E/reference-doc changelogs.
8. **Exact bilingual copy** for the two new validation errors (count-of-counts != count-of-rows; a per-row count out of 1..80) — needed verbatim for the E2E-HSL-018/019 assertions.

**Waiting for your approval before making any changes.**