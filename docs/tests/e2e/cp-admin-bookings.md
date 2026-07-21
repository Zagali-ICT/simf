# E2E test catalogue — Booking monitor (`/admin/bookings`)

| | |
|--|--|
| **Page** | [`cp/admin-bookings.md`](../../pages/cp/admin-bookings.md) |
| **Route** | `/admin/bookings` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` / `Aa@123456789` + TOTP via the `Get-Totp` helper |
| **Required permission** | `Bookings.View` (page + list); `Bookings.Export` (Excel export) — `PermissionCatalog.Bookings.*`. There is **no** `Bookings.Approve` / `Bookings.Reject` (retired with the approval step, #6/#17). |
| **Last reviewed** | 2026-07-21 (#6/#17 — approval retired; read-only monitor + no-show release) |

> **What this page is (#6/#17, owner 2026-07-20).** A **read-only monitor** of the
> **active** (confirmed, still-held) visitor seat reservations across all sessions,
> newest-first. There is **no approval step** — a reserve / random / join creates the
> seat already `Approved` (`SeatReservationService`) — so this page has **no Approve
> / Reject / bulk-approve actions**; it only **lists** and **exports** (`Bookings.View`
> / `Bookings.Export`). It replaced the old P2.2 / D-227 approval queue. Admin
> row-blocks never appear here (they carry a null attendee).
>
> **No-show release (the real lifecycle).** A reserved seat is a **provisional hold**
> stamped with `ExpiresUtc = StartUtc − 3min`. The background
> `ReservationNoShowReleaseWorker` runs once a minute and calls
> `ISeatReservationService.ReleaseNoShowsAsync`: any active hold past that deadline
> whose holder **never checked in** (no `HallAttendance` for the session) and that was
> **booked ahead** of the deadline is released (`Status = Cancelled`, seat freed) and
> the holder gets a `BookingReleased` notification. A walk-in who booked at/after the
> deadline is exempt. Nothing on this page performs the release; it is automatic.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-BKG-001 | Golden — monitor lists the active (confirmed, still-held) bookings from `/list` | happy | P0 | _to author_ |
| E2E-BKG-002 | Read-only — the page exposes NO Approve / Reject / bulk-approve action (only Export) | happy | P0 | _to author_ |
| E2E-BKG-003 | No-show release (background) — an un-checked-in hold is freed 3 min before start + holder notified | happy | P0 | authored ✓ |
| E2E-BKG-004 | Empty monitor renders `SimfEmptyState` ("No active bookings.") | happy | P1 | _to author_ |
| E2E-BKG-005 | Auth gate — admin lacking `Bookings.View` → `/not-permitted`; direct API call → 403 | auth | P0 | authored ✓ |
| E2E-BKG-006 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-BKG-007 | RTL / Arabic render — banner, hint, headers mirror | i18n | P1 | _to author_ |
| E2E-BKG-008 | Per-column grid filter (Session / Seat) narrows the monitor | happy | P1 | _to author_ |
| E2E-BKG-009 | Column sort toggles (Session / Starts / Seat / Booked) ascending ⇄ descending | happy | P2 | _to author_ |
| E2E-BKG-010 | Excel export (D-356) — toolbar Export downloads an .xlsx (whole filtered set vs selected rows) | happy | P1 | _to author_ |
| E2E-BKG-011 | Admin release closes the lifecycle + notifies (M-4) — releasing a held/confirmed seat is terminal-Cancelled + BookingReleased | happy | P1 | authored ✓ |
| E2E-BKG-012 | Monitor hint — the info banner explains bookings auto-confirm + the 3-min no-show release | happy | P2 | _to author_ |

## Scenarios

### E2E-BKG-001 — Golden path (the monitor lists active bookings)

```gherkin
Feature: Booking monitor — list the active reservations
  As an Administrator with the Bookings.View permission
  I want to see the confirmed, still-held seat reservations across all sessions
  So that I can monitor demand without approving anything

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an approved visitor "Layla Al-Harbi" has reserved seat A1 for the session
    "Naval Logistics Forum" (the reservation is Status=Approved, ReleasedAt=null)
  And an Administrator has signed in via /login + /login/totp with Bookings.View
  And they have landed on /admin/bookings

Scenario: The monitor renders the active bookings
  Given the BFF POSTs /account/api/admin/bookings/list with a GridQuery
  And the API returns HTTP 200 with ApiResult.Success=true
  Then the grid shows a row with Session="Naval Logistics Forum", Seat="A1",
    Attendee="Layla Al-Harbi", and the "Booked (UTC)" timestamp
  And the summary line reads "Showing 1–{N} of {N}"
  And an info banner reads the monitor hint (bookings auto-confirm + 3-min no-show release)
  And no console error is logged
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-bookings-monitor.png` (grid with the active row + info banner)
- Network: `/account/api/admin/bookings/list` returns 200 with the active rows
- Console errors: 0 expected

### E2E-BKG-002 — Read-only (no mutating actions)

```gherkin
Scenario: The monitor exposes no approve / reject / bulk-approve affordance
  Given an Administrator with Bookings.View is on /admin/bookings with ≥1 active row
  Then there is NO per-row Approve (check-circle) or Reject (close/X) quiet icon action
  And there is NO "Approve selected ({n})" bulk toolbar button
  And there is NO Reject modal
  And the only toolbar action is "Export" / "تصدير" (gated by Bookings.Export)
  And the /account/api/admin/bookings/{id}/approve, /{id}/reject and /bulk-approve
    routes do not exist (the CP BFF no longer maps them; the API endpoints were removed)
```

### E2E-BKG-003 — No-show release (background worker)

```gherkin
Feature: No-show seat release (#6/#17)
  As the organiser
  I want a reserved seat freed if the holder does not check in before the session
  So that the seat can go to someone who shows up

Background:
  Given an approved visitor holds a confirmed seat A1 for a session, booked well ahead
  And the reservation's ExpiresUtc = the session's StartUtc − 3 minutes

Scenario: An un-checked-in hold past its deadline is released and the holder notified
  Given the current time is at or after the reservation's ExpiresUtc
  And the holder has NO HallAttendance (check-in) for that session
  When ReservationNoShowReleaseWorker runs its minute tick
    (ISeatReservationService.ReleaseNoShowsAsync)
  Then the reservation becomes ReleasedAt != null, Status = Cancelled (the seat is freed)
  And the holder receives a BookingReleased notification explaining they were not checked in
  And the freed seat A1 can be reserved by someone else

Scenario: A checked-in holder, a future deadline, a walk-in and an admin block are all kept
  Given a second holder past the deadline HAS checked in (a HallAttendance row exists)
  And a third hold's deadline is still in the future
  And a fourth hold was booked AT/AFTER the deadline (a walk-in — CreatedAt >= ExpiresUtc)
  And an AdminReservedRow block (no attendee, no ExpiresUtc) exists
  When the worker runs
  Then none of those four are released — only the un-checked-in, booked-ahead no-show is
```

**Evidence captured:**
- API integration tests: `ReservationNoShowReleaseWorkerTests.Releases_only_past_deadline_no_show_holds_booked_ahead`,
  `ReservationNoShowReleaseWorkerTests.Notifies_the_freed_no_show_holder`
- Worker heartbeat: `/admin/ops/services` shows `ReservationNoShowReleaseWorker` registered + last-success updating
- Notification kind `BookingReleased` (value 51), bilingual body "…released because you did not check in…" / "…لعدم تسجيل دخولك قبل بدء الجلسة."

### E2E-BKG-004 — Empty monitor

```gherkin
Scenario: Empty monitor renders SimfEmptyState
  Given no SeatReservation is Status=Approved with ReleasedAt=null and a non-null attendee
  When the administrator opens /admin/bookings
  Then the page renders the SimfEmptyState component
  And the empty state title reads "No active bookings." / "لا توجد حجوزات نشطة."
  And the table and the summary line are NOT rendered
  And the info hint banner still renders
  And no error toast appears
```

### E2E-BKG-005 — Auth gate

```gherkin
Scenario: Admin lacking the Bookings.View permission is denied
  Given a signed-in admin whose role does NOT include Bookings.View
    (and is not the Administrator wildcard "*")
  When they navigate to /admin/bookings
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/bookings/list request fires
  And the "Bookings" nav item is hidden for that user (RequiredPermission=Bookings.View)
  And separately, a visitor JWT calling POST /api/v1/admin/bookings/list directly
    receives HTTP 403 Forbidden (per BookingLifecycleTests.Non_admin_cannot_view_the_booking_monitor)
```

### E2E-BKG-006 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/bookings/list (e.g. DB down)
  When the administrator opens /admin/bookings
  Then the page first shows "Loading bookings…" / "جارٍ تحميل الحجوزات…"
  And then a red toast appears reading "The action could not be completed. Please try again." /
    "تعذّر إكمال العملية. يُرجى المحاولة مرة أخرى."
  And no grid rows render
  And no empty-state component renders (the load failed rather than returning zero rows)
```

### E2E-BKG-007 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the monitor
  Given the administrator is on /admin/bookings in English with at least one active row
  When they switch the UI to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "مراقبة الحجوزات"
  And the info hint reads the Arabic monitor hint
  And the column headers read "الجلسة", "تبدأ (UTC)", "المقعد", "الحاضر", "تاريخ الحجز (UTC)"
  And the grid + nav rail mirror to RTL
```

### E2E-BKG-008 — Per-column grid filter narrows the monitor

```gherkin
Feature: Booking monitor — per-column grid filter (D-256 SimfDataGrid)
  As an Administrator with Bookings.View
  I want to filter the monitor by a single column
  So that I can find one session's or one seat's active bookings fast

Background:
  Given an Administrator with Bookings.View is on /admin/bookings
  And the monitor holds active rows across several sessions, including
    "Naval Logistics Forum" (seat A1) and "Maritime Cyber Defence" (seat B3)

Scenario: Typing into the Session column filter narrows the grid
  Given the "Session" and "Seat" columns each expose a quiet filter input
    (they are the only Filterable columns)
  When the administrator types "Naval" into the Session column filter
  Then the BFF POSTs /account/api/admin/bookings/list with a GridQuery whose
    Filters["session"] = "Naval" and Skip = 0
  And the grid re-renders showing only the "Naval Logistics Forum" rows
```

### E2E-BKG-009 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles ascending then descending
  Given the "Session", "Starts (UTC)", "Seat" and "Booked (UTC)" headers are
    sortable (the "Attendee" column is NOT sortable)
  When the administrator clicks the "Session" column header
  Then the BFF POSTs /account/api/admin/bookings/list with Sort="session",
    SortDescending=false and Skip=0, and the rows reorder A→Z by session
  When they click the "Session" header again
  Then the next /list POST carries Sort="session", SortDescending=true, Skip=0 (Z→A)
```

### E2E-BKG-010 — Excel export (D-356)

```gherkin
Feature: Booking monitor — Excel export (D-356 Uniform CRUD)
  As an Administrator with the Bookings.Export permission
  I want to export the active bookings to an .xlsx workbook

Background:
  Given an Administrator with Bookings.View + Bookings.Export is on /admin/bookings
  And the monitor holds at least two active rows

Scenario: Export the whole filtered set when no rows are selected
  When the administrator clicks the toolbar "Export" / "تصدير" action
  Then the browser invokes simfAccount.downloadXlsx against
    POST /account/api/admin/bookings/export
  And the request body is an AdminGridExportRequest with an EMPTY Ids list
    and the current GridQuery
  And the API (gated by Bookings.Export) returns HTTP 200 with an
    application/vnd.openxmlformats-officedocument.spreadsheetml.sheet body
  And the workbook's "Bookings" sheet header row is
    SessionTitle | SessionTitleArabic | SessionStart | Row | Seat | Kind | Attendee | BookedAt

Scenario: Export only the selected rows
  Given the administrator ticks two rows
  When they click "Export"
  Then the AdminGridExportRequest carries those two ReservationIds and a null Query
  And the workbook contains exactly those two rows

Scenario: Export is export-only — there is no Import affordance
  Then the toolbar exposes "Export" but NOT an "Import" action
```

**Evidence captured:**
- Network: `POST /account/api/admin/bookings/export` returns 200
- API integration test: `tests/SIMF.Api.Tests/BookingsExcelTests.cs`

### E2E-BKG-011 — Admin release closes the lifecycle + notifies (M-4)

```gherkin
Feature: Admin seat release (M-4)
  As an Administrator releasing a held or confirmed seat (from the seat-plans page)
  The booking must reach a terminal state and the attendee must be told

Scenario: Releasing a visitor booking cancels it and notifies the attendee
  Given a visitor holds a confirmed (Approved) seat in a session
  When the administrator releases the reservation
  Then the reservation is ReleasedAt != null, Status = Cancelled, reviewer id stamped
  And a BookingReleased in-app notification is dispatched to the attendee

Scenario: Releasing an admin-reserved-row block does not notify
  Given an AdminReservedRow block (ReservedForUserId is null)
  When the administrator releases one of its seats
  Then the row is Cancelled and NO BookingReleased notification is dispatched
```

**Evidence captured:**
- API integration tests: `SeatReservationsTests.Admin_release_marks_cancelled_and_notifies`,
  `SeatReservationsTests.Admin_release_of_admin_reserved_row_does_not_notify`
- Notification kind `BookingReleased` (value 51) groups under "Bookings"

### E2E-BKG-012 — Monitor hint banner

```gherkin
Scenario: The info banner explains the no-approval + no-show-release model
  Given the administrator is on /admin/bookings
  Then a SimfAlert (variant "info") reads
    "Bookings confirm instantly — there is no approval step. A reserved seat is
    released automatically if the visitor has not checked in 3 minutes before the
    session starts." (Arabic: the Admin.Bookings.MonitorHint value)
```

---

## Implementation notes

- **Read-only monitor — no CRUD verbs.** This page has **no** Add / Edit / Details /
  Deactivate and **no** Approve / Reject / bulk-approve. The only actions are viewing
  and Excel export. Do not author scenarios for actions the page does not expose.
- **Approval retired (#6/#17).** The old `/admin/bookings/{id}/approve`, `/{id}/reject`
  and `/bulk-approve` endpoints, the `Bookings.Approve` / `Bookings.Reject` permissions,
  the CP BFF passthroughs and the Reject modal were **removed**. The seeder
  (`IdentitySeeder.RetireRemovedPermissionsAsync`) deletes any lingering
  `Bookings.Approve` / `Bookings.Reject` permission rows + grants on boot.
- **API integration tests** at the lower layer (no browser):
  - `tests/SIMF.Api.Tests/ReservationNoShowReleaseWorkerTests.cs` — the no-show release rule (→ E2E-BKG-003)
  - `tests/SIMF.Api.Tests/BookingLifecycleTests.cs` — reserve/cancel guards + the monitor auth gate (→ E2E-BKG-005)
  - `tests/SIMF.Api.Tests/SeatReservationsTests.cs` — seat-map, capacity, no-show deadline stamp, admin release (→ E2E-BKG-011)
  - `tests/SIMF.Api.Tests/BookingsExcelTests.cs` — the export endpoint (→ E2E-BKG-010)
- **Wire contract.** BFF passthrough (`AccountEndpoints.cs`): only
  `/account/api/admin/bookings/list` (body `GridQuery`) + the generic
  `/account/api/admin/bookings/export`. API endpoints live in
  `SeatReservationEndpoints.cs` (`ListActiveBookingsEndpoint`,
  `ExportBookingsEndpoint`); the orchestration + the no-show release rule are in
  `SeatReservationService.cs` (`ListActiveBookingsAsync`, `ReleaseNoShowsAsync`).
- **Convert to Playwright** when the runner is adopted: each Gherkin scenario maps to
  a `.feature` file + step class. The shape is already runner-agnostic.

---

_Last reviewed:_ 2026-07-21 by Claude (#6/#17 — approval step retired; the page became a read-only booking monitor, and the pre-start no-show release moved into `ReservationNoShowReleaseWorker` / `ReleaseNoShowsAsync`). Earlier: 2026-07-19 (reservation-only correction — auto-confirm on create). Earlier: 2026-06-10 (D-356 Excel export). Earlier: 2026-06-03 (E2E catalogue rebuild).
