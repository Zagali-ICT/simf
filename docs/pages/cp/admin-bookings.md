# Booking monitor — `/admin/bookings`

| | |
|--|--|
| **Route** | `/admin/bookings` |
| **Audience** | Administrator (and any role granted `Bookings.View`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Bookings.View)]` (CP page) + API `Bookings.View` / `Bookings.Export` policies + `RequireApprovedAccount` |
| **Pattern** | #6/#17 (owner 2026-07-20) — **read-only monitor, not CRUD and not a review queue**. `SimfDataGrid` (D-255). Bookings auto-confirm (no approval step); no-shows are released by a background worker. Replaced the D-227 approval queue. |
| **Status** | ✅ Real (#6/#17) |
| **Required permissions** | `Bookings.View` (page + list), `Bookings.Export` (Excel export) — `PermissionCatalog.Bookings.*`. **`Bookings.Approve` / `Bookings.Reject` were retired** with the approval step. |
| **Backend endpoints** | BFF `/account/api/admin/bookings/*` → API: `POST /admin/bookings/list` (`GridQuery`), `POST /admin/bookings/export` (`AdminGridExportRequest`). The approve/reject/bulk-approve endpoints were removed. |
| **Source** | [`BookingsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BookingsList.razor), [`SeatReservationEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Sessions/SeatReservationEndpoints.cs), [`ExportBookingsEndpoint`](../../../src/Backend/SIMF.Api/Endpoints/Admin/BookingsExcelEndpoints.cs), [`SeatReservationService.cs`](../../../src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs), [`ReservationNoShowReleaseWorker.cs`](../../../src/Backend/SIMF.Infrastructure/Operations/ReservationNoShowReleaseWorker.cs), [`SeatReservations.cs`](../../../src/Shared/SIMF.Contracts/Sessions/SeatReservations.cs) |
| **Backed by** | Existing `dbo.SeatReservations` (no new table). `ExpiresUtc` now carries the no-show deadline (`StartUtc − 3min`). |
| **Tests** | [`docs/tests/e2e/cp-admin-bookings.md`](../../tests/e2e/cp-admin-bookings.md) |
| **Last reviewed** | 2026-07-21 |

## 1. Purpose

A Control Panel **read-only monitor** of the **active** (confirmed, still-held) visitor
seat reservations across all sessions. Per the owner's 2026-07-20 directive there is
**no approval step**: `SeatReservationService` (`ReserveAsync` / `ReserveRandomAsync` /
`JoinOpenSeatingAsync`) writes every attendee booking `Status = Approved` on create, so
the seat is confirmed immediately. This page lets an admin **see** and **export** those
bookings — nothing more. It replaced the retired D-227 approval queue.

**The no-show release (the real lifecycle).** A reserved seat is a **provisional hold**
stamped with `ExpiresUtc = StartUtc − 3min`. The background
`ReservationNoShowReleaseWorker` calls `ISeatReservationService.ReleaseNoShowsAsync`
once a minute: any active (`Status = Approved`, `ReleasedAt == null`,
`ReservedForUserId != null`) hold past its deadline whose holder has **no**
`HallAttendance` (never checked in) **and** that was **booked ahead** of the deadline
(`CreatedAt < ExpiresUtc`) is released (`Status = Cancelled`, `ReleasedAt` stamped) so
the seat can go to someone else, and the holder gets a `BookingReleased` notification.
A walk-in who booked at/after the deadline is exempt; an admin row-block (null attendee,
null `ExpiresUtc`) is never touched.

This is **not** a CRUD grid and **not** a review queue — there is no Add / Edit /
Details / Deactivate, no Approve / Reject / bulk-approve, and no import.

## 4. UI

- `SimfBanner` titled `Admin.Bookings.Title` ("Booking monitor" / "مراقبة الحجوزات"),
  then an info `SimfAlert` (variant `info`) with `Admin.Bookings.MonitorHint`
  explaining the no-approval + 3-min no-show release model, then a `SimfDataGrid` of
  `ActiveBookingRow` inside `simf-page-wide` / `simf-surface`.
- **Grid columns** (keys in parentheses): Session (`session`), Starts UTC (`start`,
  `yyyy-MM-dd HH:mm`), Seat (`seat`, `{RowLabel}{SeatNumber}` e.g. `A1`, or **General
  admission** for an open-seating join with no seat — D-485), Attendee (`attendee`),
  Booked UTC (`bookedAt`, `yyyy-MM-dd HH:mm`).
- **Sortable:** Session, Starts, Seat, Booked UTC. **Filterable** (per-column quiet
  input): Session and Seat only. The **Attendee** column is neither — its display name
  is resolved cross-DB from Identity after paging (D-157).
- **No row actions, no bulk action, no modal.** The grid keeps `Multiselect="true"`
  only so the checkboxes can drive a **selected-rows Excel export**.
- **Empty state:** the grid's `EmptyTemplate` renders `SimfEmptyState` titled
  `Admin.Bookings.None` ("No active bookings." / "لا توجد حجوزات نشطة.").
- **Excel export (D-356):** the grid toolbar `OnExport` action (labelled `Grid.Export`)
  calls `simfAccount.downloadXlsx` against `/account/api/admin/bookings/export` with an
  `AdminGridExportRequest` — `Ids` = the checked rows' `ReservationId`s and `Query =
  null` when a selection exists, else empty `Ids` + the current `_query`. **Export only
  — there is no Import action** (bookings originate in the app).
- Toasts use `SimfAlert`; the only toast path is a load-failure error.

## 5. Data flow + endpoints

CP page → JS `simfAccount.postJson` / `downloadXlsx` → CP BFF passthroughs in
[`AccountEndpoints.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs)
→ API endpoints in `SeatReservationEndpoints.cs` → `SeatReservationService`.

| BFF route | API route | Body | Returns |
|-----------|-----------|------|---------|
| `POST /account/api/admin/bookings/list` | `POST /api/v1/admin/bookings/list` | `GridQuery` | `ApiResult<GridPage<ActiveBookingRow>>` |
| `POST /account/api/admin/bookings/export` | `POST /api/v1/admin/bookings/export` | `AdminGridExportRequest { Ids, Query }` | XLSX bytes |

**List query.** `ListActiveBookingsAsync` filters
`Status == Approved && ReleasedAt == null && ReservedForUserId != null`, joins
`SeatReservations` to `Sessions` up-front so Session/Seat are server-filterable/sortable,
applies the per-column filters (`session` → Title/TitleArabic Contains; `seat` →
RowLabel Contains; unknown keys ignored), sorts (`session` / `start` / `seat` /
`bookedAt`, default newest-first by `CreatedAt` desc), pages, then resolves attendee
names in **one** Identity round-trip (no cross-DB JOIN, D-157). `Top` clamped 1–500.

**No-show release.** `ReleaseNoShowsAsync(now)` is called by
`ReservationNoShowReleaseWorker` each minute (heartbeat-registered, visible on
`/admin/ops/services`); it is not reachable from this page or any endpoint.

## 6. Validation + error handling

- **List failure** → the CP surfaces `env.Error.MessageForCurrentCulture()`, falling
  back to `Admin.Bookings.LoadFailed`.
- The page has no mutating actions, so there are no action-level validation paths here
  (the reserve/cancel guards — `BOOKING_OVERLAP`, `BOOKING_SESSION_STARTED`,
  `BOOKING_SESSION_ENDED` — live on the visitor reserve/cancel path).

## 7. Edge cases + known limitations

- **No-show release is best-effort per row.** The release notification
  (`TryNotifyBookingReleasedAsync`, `noShow: true`) swallow-and-logs on failure (it
  writes to the Identity DbContext after the release is committed) — a notification
  failure never rolls back the release.
- **Walk-ins are exempt** from the no-show release (`CreatedAt >= ExpiresUtc`), so a
  seat booked during/just before the session is not yanked from a present attendee.
- **Attendee name may be blank** if the Identity lookup finds no `DisplayName`.
- **Export ceiling.** The export endpoint sets `Top = 5000`, but the list service
  clamps `Top` to **500**, so the effective export size is the first 500 active rows of
  the filtered/sorted set.

## 8. i18n + RTL

`Admin.Bookings.*` keys (Title, Loading, None, MonitorHint, LoadFailed, Col.Session,
Col.Start, Col.Seat, Col.Attendee, Col.BookedAt) plus shared `Grid.*` keys, with EN ↔
AR parity. (The retired Approve/Reject/Reject.* keys remain in the resx as dead entries
and are no longer referenced by the page.) Under Arabic the page + grid mirror to RTL
(`<html dir="rtl" lang="ar">`).

## 10. Use cases

- UC-BKG-MONITOR-001 (view the active bookings), UC-BKG-EXPORT-001 (export the monitor),
  UC-BKG-NOSHOW-001 (a no-show hold is auto-released 3 min before start). FR-503/903
  (reservation, no approval) + FR-502/504 (overlap / cancel-before-start) are exercised
  on the **visitor** reserve/cancel path.

## 11. E2E

See [`docs/tests/e2e/cp-admin-bookings.md`](../../tests/e2e/cp-admin-bookings.md):
E2E-BKG-001 monitor golden, 002 read-only (no approve/reject), 003 no-show release,
004 empty, 005 auth gate, 006 server-500, 007 RTL, 008 per-column filter, 009 column
sort, 010 Excel export (export-only), 011 admin release + notify, 012 monitor hint. API
integration coverage: `tests/SIMF.Api.Tests/ReservationNoShowReleaseWorkerTests.cs`,
`BookingLifecycleTests.cs`, `SeatReservationsTests.cs`, `BookingsExcelTests.cs`.

## 12. Related docs

- Admin Manual: booking monitor section.
- Decisions: #6/#17 (approval retired, read-only monitor + no-show release), D-227
  (original booking workflow + `App/D227` migration — superseded), D-217
  (`BookingReleased` notification kind), D-356 (grid Excel export), D-255
  (`SimfDataGrid` migration).
- Authority spec: SIMF-FDS-005 §5.2/§5.3 (updated to reservation-only + no-show release).
- Wire contracts: `SeatReservations.cs` (`ActiveBookingRow`),
  `GridExcel.cs` (`AdminGridExportRequest`).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-11 | D-356 | First authored page reference doc for the booking approval queue (P2.2 / D-227). |
| 2026-07-19 | Apexium | Corrected to reservation-only auto-confirm + gate check-in; queue relabelled retained-but-dormant. |
| 2026-07-21 | #6/#17 | **Approval retired.** Approve / Reject / bulk-approve endpoints, the `Bookings.Approve` / `Bookings.Reject` permissions, the CP BFF passthroughs and the Reject modal were removed. The page became a **read-only monitor** of active bookings, and the pre-start no-show release moved into `ReservationNoShowReleaseWorker` / `ReleaseNoShowsAsync` (release un-checked-in holds 3 min before start + `BookingReleased` notification). Contract `BookingQueueRow` → `ActiveBookingRow`; `ListPendingBookingsAsync` → `ListActiveBookingsAsync`. |

_Last reviewed:_ 2026-07-21 by Claude (#6/#17 — read-only monitor + background no-show release). Prior: 2026-07-19 by Apexium (reservation-only auto-confirm). Prior: 2026-06-11 by Claude (D-356 authored from live source).
