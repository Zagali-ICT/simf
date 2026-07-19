# Booking approvals — `/admin/bookings`

| | |
|--|--|
| **Route** | `/admin/bookings` |
| **Audience** | Administrator (and any role granted `Bookings.View`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Bookings.View)]` (CP page) + per-action API policies + `RequireApprovedAccount`; mutations also `RequireRateLimiting("auth")` |
| **Pattern** | P2.2 / D-227 (FDS-005 §5.2) — **review/approval queue, not CRUD**. Migrated to `SimfDataGrid` (D-255). **Retained but dormant / always empty** — attendee reserves auto-confirm (`Status = Approved`); the real seat confirmation is the gate check-in (staff QR scan), not this queue. |
| **Status** | ✅ Real (D-227) |
| **Required permissions** | `Bookings.View` (page + list), `Bookings.Approve` (row Approve + bulk Approve), `Bookings.Reject` (row Reject), `Bookings.Export` (Excel export) — `PermissionCatalog.Bookings.*` |
| **Backend endpoints** | BFF `/account/api/admin/bookings/*` → API: `POST /admin/bookings/list` (`GridQuery`), `POST /admin/bookings/{id}/approve` (empty body), `POST /admin/bookings/{id}/reject` (`RejectBookingRequest`), `POST /admin/bookings/bulk-approve` (`AdminBulkApprovalRequest`), `POST /admin/bookings/export` (`AdminGridExportRequest`) |
| **Source** | [`BookingsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BookingsList.razor), [`SeatReservationEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Sessions/SeatReservationEndpoints.cs), [`ExportBookingsEndpoint`](../../../src/Backend/SIMF.Api/Endpoints/Admin/BookingsExcelEndpoints.cs), [`SeatReservationService.cs`](../../../src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs), [`SeatReservations.cs`](../../../src/Shared/SIMF.Contracts/Sessions/SeatReservations.cs) |
| **Backed by** | Existing `dbo.SeatReservations` (no new table — the booking workflow columns `Status` / `ReviewedByUserId` / `ReviewedAt` / `RejectionReason` shipped with D-227's migration `App/D227`). |
| **Tests** | [`docs/tests/e2e/cp-admin-bookings.md`](../../tests/e2e/cp-admin-bookings.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

The Control Panel **booking approval queue** per SIMF-FDS-005 §5.2. **This queue is
retained but dormant (always empty).** In the shipped code, `SeatReservationService`
(`ReserveAsync` / `ReserveRandomAsync` / `JoinOpenSeatingAsync`) writes every attendee
booking `Status = Approved` on create — the reservation is confirmed immediately with
**no** Control Panel approval step, and **no** notification fires on reserve (the app
shows an inline success message). The reservation is a **provisional hold** until the
attendee **checks in at the hall gate** (staff QR scan), which is what confirms the
seat; a pre-start sweep releases any hold not checked in shortly before the session
starts. Because nothing ever writes `Status = Pending`, this page lists no rows in
practice.

The approve / reject / bulk-approve surface below is kept intact but never exercised
on the live attendee path. Were a Pending booking to exist, a reviewer could:

- **Approve** a booking (seat confirmed; a `BookingConfirmed` in-app notification
  fires to the attendee),
- **Reject** a booking with a required **reason** (the held seat is released so it
  can be re-booked; a `BookingRejected` notification carrying the reason fires), or
- **Approve selected** in bulk over the checked rows.

This is a **review queue, not a CRUD grid** — there is no Add / Edit / Details /
Deactivate, and there is **no import** (bookings are created by visitors in the app,
never uploaded here). Admin row-blocks never appear: `AdminReserveRowAsync` writes
those rows `Status = Approved` with a null `ReservedForUserId`, and the queue filters
to `Status == Pending && ReleasedAt == null && ReservedForUserId != null` — a filter
that matches nothing because attendee reserves are auto-`Approved`.

## 4. UI

- `SimfBanner` titled `Admin.Bookings.Title`, then a `SimfDataGrid` of
  `BookingQueueRow` inside `simf-page-wide` / `simf-surface`.
- **Grid columns** (keys in parentheses): Session (`session`), Starts UTC
  (`start`, rendered `yyyy-MM-dd HH:mm`), Seat (`seat`, rendered `{RowLabel}{SeatNumber}`,
  e.g. `A1`, or **General admission** for an open-seating join with no seat — D-485),
  Attendee (`attendee`), Booked UTC (`bookedAt`, rendered
  `yyyy-MM-dd HH:mm`).
- **Sortable:** Session, Starts, Seat, Booked UTC. **Filterable** (per-column quiet
  input): Session and Seat only. The **Attendee** column is neither sortable nor
  filterable — its display name is resolved cross-DB from Identity after paging
  (D-157), so it is not a server-queryable column.
- **Row actions** (quiet icons, permission-gated): Approve (`check-circle`, gated by
  `Bookings.Approve`) and Reject (`close`, gated by `Bookings.Reject`).
- **Bulk action:** `Multiselect="true"` with row checkboxes + select-all; the grid's
  `OnApproveSelected` drives an "Approve selected" toolbar action labelled
  `Admin.Bookings.Approve`. With zero rows checked it is disabled.
- **Reject modal:** clicking Reject opens a `SimfModal` titled
  `Admin.Bookings.Reject.Title` with a single `<textarea maxlength="512">` labelled
  `Admin.Bookings.Reject.Reason`; footer = Cancel + Reject confirm.
- **Empty state:** when the queue is empty the grid's `EmptyTemplate` renders
  `SimfEmptyState` titled `Admin.Bookings.None`.
- **Excel export (D-356):** the grid toolbar `OnExport` action (labelled `Grid.Export`)
  calls `simfAccount.downloadXlsx` against `/account/api/admin/bookings/export` with an
  `AdminGridExportRequest` — `Ids` = the checked rows' `ReservationId`s and `Query =
  null` when a selection exists, else empty `Ids` + the current `_query` to export the
  on-screen filtered/sorted set. **Export only — there is no Import action and no
  `/import` endpoint for this page** (bookings originate in the app).
- Toasts use `SimfAlert`; success/error variants per action.

> **No Page ↔ Popup presentation toggle here.** Unlike the canonical CRUD pages
> (e.g. Themes, D-353), this review queue has no `CrudShell` / `CrudPresentationToggle`
> — its only modal is the bespoke Reject dialog. Do not document a toggle the source
> does not have.

## 4.5 Queue columns + Approve / Reject actions

This page has **no create/edit form**. The reviewer works against rows, not fields.

**`BookingQueueRow` columns shown in the grid:**

| Column | Source field | Notes |
|--------|--------------|-------|
| Session | `SessionTitle` | English title; filter matches `Title` **or** `TitleArabic` server-side |
| Starts (UTC) | `SessionStartUtc` | session start, `yyyy-MM-dd HH:mm` |
| Seat | `RowLabel` + `SeatNumber` | concatenated, e.g. `A1`; the Seat filter matches `RowLabel` |
| Attendee | `AttendeeName` | resolved from Identity `Users.DisplayName` (D-157); blank if unresolved |
| Booked (UTC) | `CreatedAt` | when the visitor reserved, `yyyy-MM-dd HH:mm` |

(`BookingQueueRow` also carries `ReservationId`, `SessionId`, `Kind`
(`SeatReservationKind`: `UserBooking` / `AdminReservedRow` / `RandomAssignment`) and
`AttendeeUserId`, used by the export and row actions.)

**Actions:**

| Action | API call | Effect |
|--------|----------|--------|
| Approve (row) | `POST /{id}/approve` (empty body) | `Status → Approved`, stamps `ReviewedByUserId` / `ReviewedAt`; audit `Booking.Approved`; fires `BookingConfirmed` notification to the attendee |
| Reject (row) | `POST /{id}/reject` `{ Reason }` | `Status → Rejected`, sets `RejectionReason`, stamps reviewer, **sets `ReleasedAt`** (frees the seat); audit `Booking.Rejected`; fires `BookingRejected` (Warning) notification carrying the reason |
| Approve selected (bulk) | `POST /bulk-approve` `{ Ids }` | approves each Pending/held id, skips missing/already-decided; returns the count approved; one `Booking.Approved` audit row per booking (`Detail` ends `bulk=true`) + one `BookingConfirmed` each |

Reject reason is required (1–512 chars): the CP blocks a blank reason client-side; the
API enforces the same as defence in depth (`BOOKING_REJECTION_REASON_REQUIRED`, 400).

## 5. Data flow + endpoints

CP page → JS `simfAccount.postJson` / `downloadXlsx` → CP BFF passthroughs in
[`AccountEndpoints.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs)
(`/account/api/admin/bookings/*`, each forwarding with the access token) → API
endpoints in `SeatReservationEndpoints.cs` → `SeatReservationService`.

| BFF route | API route | Body | Returns |
|-----------|-----------|------|---------|
| `POST /account/api/admin/bookings/list` | `POST /api/v1/admin/bookings/list` | `GridQuery` | `ApiResult<GridPage<BookingQueueRow>>` |
| `POST /account/api/admin/bookings/{id}/approve` | `POST /api/v1/admin/bookings/{id}/approve` | empty | `ApiResult<bool>` |
| `POST /account/api/admin/bookings/{id}/reject` | `POST /api/v1/admin/bookings/{id}/reject` | `RejectBookingRequest { Reason }` | `ApiResult<bool>` |
| `POST /account/api/admin/bookings/bulk-approve` | `POST /api/v1/admin/bookings/bulk-approve` | `AdminBulkApprovalRequest { Ids }` | `ApiResult<int>` (count approved) |
| `POST /account/api/admin/bookings/export` | `POST /api/v1/admin/bookings/export` | `AdminGridExportRequest { Ids, Query }` | XLSX bytes |

**Contract note — bulk-approve field name.** The CP and BFF speak
`AdminBulkApprovalRequest { Ids }`; the BFF maps that to the API endpoint's
`BulkApproveBookingsRequest { ReservationIds }` (`body.Ids?.ToList() ?? []`). Both
sides resolve to the same list of `ReservationId`s.

**List query.** `ListPendingBookingsAsync` joins `SeatReservations` to `Sessions`
up-front so Session/Seat are server-filterable/sortable, applies the per-column
filters (`session` → Title/TitleArabic Contains; `seat` → RowLabel Contains; unknown
keys ignored), sorts (`session` / `start` / `seat` / `bookedAt`, default newest-first
by `CreatedAt` desc), pages, then resolves attendee names in **one** Identity
round-trip (no cross-DB JOIN, D-157). `Top` is clamped to 1–500.

## 6. Validation + error handling

- **Approve / Reject of a non-Pending or released booking** → `LoadPendingBookingAsync`
  throws `BOOKING_NOT_PENDING` (409, "This booking has already been decided." /
  "تم البت في هذا الحجز بالفعل.").
- **Unknown booking id** → `BOOKING_NOT_FOUND` (404).
- **Blank / out-of-range reject reason** → `BOOKING_REJECTION_REASON_REQUIRED` (400);
  the CP also guards it with a bilingual toast `Admin.Bookings.ReasonRequired` before
  any POST.
- **Bulk approve** is lenient: missing or already-decided ids are silently skipped
  (counted out of the result), not an error.
- **List / action failure** → the CP surfaces `env.Error.MessageForCurrentCulture()`,
  falling back to `Admin.Bookings.LoadFailed`.
- All bilingual error text comes from the service's `ApiException` (EN + AR).

## 7. Edge cases + known limitations

- **Notifications are best-effort.** `TryNotifyBookingConfirmedAsync` /
  `TryNotifyBookingRejectedAsync` swallow-and-log on failure (they write to the
  Identity DbContext after the booking is already committed) — a notification failure
  never rolls back or fails the approve/reject.
- **Reject releases the seat; approve does not change `ReleasedAt`.** A rejected seat
  becomes immediately re-bookable by any visitor.
- **Attendee name may be blank** if the Identity lookup finds no `DisplayName`; the
  column renders empty rather than erroring.
- **Selection is per page.** Reloading the queue (after any action) clears checkboxes.
- **Admin row-blocks and random/admin-confirmed rows never appear** — only
  `Status == Pending`, `ReleasedAt == null`, `ReservedForUserId != null` rows are listed.
- **Export ceiling.** The export endpoint sets `Top = 5000`, but the booking list
  service clamps `Top` to **500**, so the effective export size is the first 500
  Pending rows of the filtered/sorted set; a selected-ids export filters that capped
  page. (No client-side 400 is raised for an oversized selection.)

## 8. i18n + RTL

`Admin.Bookings.*` keys (Title, Loading, None, Approve, Reject, Reject.Title,
Reject.Reason, Reject.Confirm, Cancel, Approved, Rejected, BulkApproved, ReasonRequired,
LoadFailed, Col.Session, Col.Start, Col.Seat, Col.Attendee, Col.BookedAt) plus shared
`Grid.*` keys, all with EN ↔ AR parity. Under Arabic the page, grid, and Reject modal
mirror to RTL (`<html dir="rtl" lang="ar">`).

## 10. Use cases

- UC-BKG-APPROVE-001 (approve a held booking), UC-BKG-REJECT-001 (reject with reason),
  UC-BKG-BULK-001 (bulk approve), UC-BKG-EXPORT-001 (export the queue). Authority spec:
  SIMF-FDS-005 §5.2 / §8; FR-502 (overlap guard) and FR-504 (cancel-before-start) are
  exercised on the **visitor** reserve/cancel path, not this CP page.

## 11. E2E

See [`docs/tests/e2e/cp-admin-bookings.md`](../../tests/e2e/cp-admin-bookings.md):
E2E-BKG-001 approve golden, 002 reject with reason, 003 bulk approve, 004 checkbox
drives the bulk button, 005 empty queue, 006 auth gate, 007 reject validation,
008 already-decided 409, 009 server-500, 010 RTL, 011 per-column filter, 012 column
sort, 013 Excel export (export-only — no import). API integration coverage:
`tests/SIMF.Api.Tests/BookingApprovalTests.cs` and `BookingsExcelTests.cs`.

## 12. Related docs

- Admin Manual: booking approvals section (FDS-005 §5.2).
- Decisions: D-227 (booking approval workflow + `App/D227` migration), D-217
  (`BookingConfirmed`/`SessionReminder` notification kinds), D-356 (grid Excel export,
  export-only here), D-255 (`SimfDataGrid` migration).
- Authority spec: SIMF-FDS-005 §5.2, §8.
- Wire contracts: `SeatReservations.cs` (`BookingQueueRow`, `RejectBookingRequest`),
  `AdminAccount.cs` (`AdminBulkApprovalRequest`), `GridExcel.cs` (`AdminGridExportRequest`).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-11 | D-356 | First authored page reference doc for the booking approval queue (P2.2 / D-227), grounded in live source. Documents the queue columns + Approve/Reject/bulk-approve actions and the D-356 Excel **export only** (no import — bookings are created by visitors in the app). |
| 2026-07-19 | Apexium | Corrected the doc to match `SeatReservationService`: attendee reserves auto-confirm (`Status = Approved`) with no Control Panel approval step and no reserve-time notification; the seat is a provisional hold confirmed by the **gate check-in** (staff QR scan), with a pre-start sweep releasing un-checked-in holds. Relabelled this approval queue as **retained but dormant / always empty** without deleting the approve/reject/bulk documentation. |

_Last reviewed:_ 2026-07-19 by Apexium (corrected to reservation-only auto-confirm + gate check-in; queue retained but dormant). Prior: 2026-06-11 by Claude (D-356 — authored from live source; export-only per `ExportBookingsEndpoint`).
