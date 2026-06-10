# E2E test catalogue — Booking approvals (`/admin/bookings`)

| | |
|--|--|
| **Page** | [`cp/admin-bookings.md`](../../pages/cp/admin-bookings.md) _(page doc not yet authored — see Implementation notes)_ |
| **Route** | `/admin/bookings` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` / `Aa@123456789` + TOTP via the `Get-Totp` helper |
| **Required permission** | `Bookings.View` (page); `Bookings.Approve` (Approve + bulk Approve); `Bookings.Reject` (Reject) — `PermissionCatalog.Bookings.*` |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **What this page is.** P2.2 / D-227 (SIMF-FDS-005 §5.2): the booking approval
> queue. It lists **Pending**, still-held visitor seat bookings across all
> sessions, ordered newest-first. A reviewer **Approves** a row (seat confirmed +
> a `BookingConfirmed` in-app notification fires to the attendee), **Rejects** a
> row with a required reason (seat released + `BookingRejected` notification with
> the reason), or **Approve selected** for the checked rows in bulk. Admin
> row-blocks never appear here (they are created `Approved` with a null
> attendee). The queue is the **only** surface on the page — there is no Add /
> Edit / Details / Deactivate (this is a review queue, not a CRUD grid).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-BKG-001 | Golden path — Pending booking → Approve → leaves queue + attendee notified | happy | P0 | _to author_ |
| E2E-BKG-002 | Reject a booking with a reason → seat released + attendee notified | happy | P0 | _to author_ |
| E2E-BKG-003 | Bulk "Approve selected ({n})" over checked rows | happy | P1 | _to author_ |
| E2E-BKG-004 | Row checkbox toggle drives the "Approve selected" count + disabled state | happy | P2 | _to author_ |
| E2E-BKG-005 | Empty queue renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-BKG-006 | Auth gate — admin lacking `Bookings.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-BKG-007 | Reject validation — blank reason → client-side bilingual error, no POST | error | P1 | _to author_ |
| E2E-BKG-008 | Conflict — approve/reject an already-decided booking → `BOOKING_NOT_PENDING` (409) | error | P1 | _to author_ |
| E2E-BKG-009 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-BKG-010 | RTL / Arabic render — page + Reject modal mirror | i18n | P1 | _to author_ |
| E2E-BKG-011 | Per-column grid filter (Session / Seat) narrows the queue | happy | P1 | _to author_ |
| E2E-BKG-012 | Column sort toggles (Session / Booked) ascending ⇄ descending | happy | P2 | _to author_ |
| E2E-BKG-013 | Excel export (D-356) — toolbar Export downloads an .xlsx of the queue (whole filtered set vs selected rows) | happy | P1 | _to author_ |

## Scenarios

### E2E-BKG-001 — Golden path (Approve a pending booking)

```gherkin
Feature: Booking approvals — approve flow
  As an Administrator with the Bookings.Approve permission
  I want to approve a visitor's held seat booking
  So that the seat is confirmed and the attendee is notified

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an approved visitor "Layla Al-Harbi" has reserved seat A1 for the session
    "Naval Logistics Forum" (the reservation is Status=Pending, ReleasedAt=null)
  And an Administrator has signed in via /login + /login/totp with the Bookings.View
    and Bookings.Approve permissions
  And they have landed on /admin/bookings

Scenario: Approve one pending booking
  Given the queue shows a row with Session="Naval Logistics Forum", Seat="A1",
    Attendee="Layla Al-Harbi", and the "Booked (UTC)" timestamp
  And the summary line reads "Showing 1–{N} of {N}"
  When the administrator clicks the row's Approve (check-circle) quiet icon action,
    titled "Approve" / "اعتماد"
  Then the BFF POSTs /account/api/admin/bookings/{reservationId}/approve with an empty body
  And the API returns HTTP 200 with ApiResult.Success=true
  And a green toast reads "Booking approved." / "تم اعتماد الحجز."
  And the page reloads the queue and the approved row is gone
  And the summary count drops by one
  And the attendee receives a BookingConfirmed in-app notification titled
    "Seat reservation confirmed" / "تم تأكيد حجز المقعد"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-bookings-golden-before.png` (queue with the Pending row)
- Screenshot after: `docs/screenshots/cp-admin-bookings-golden-after.png` (row gone + green toast)
- Console errors: 0 expected
- Network: `/account/api/admin/bookings/list` returns 200, `/account/api/admin/bookings/{id}/approve` returns 200; the reload `list` returns 200
- Audit row: `OperationLog` / audit row with `Event = 'Booking.Approved'` and the actor's id, `Detail` containing `reservationId=…; sessionId=…; row=A; seat=1`
- Notification row: Identity-DB `Notification` with `Kind = BookingConfirmed` and `RelatedEntityId = {sessionId}` for the attendee

### E2E-BKG-002 — Reject a booking with a reason

```gherkin
Feature: Booking approvals — reject flow
  As an Administrator with the Bookings.Reject permission
  I want to reject a held booking with a reason
  So that the seat is released and the attendee learns why

Background:
  Given an Administrator with Bookings.View + Bookings.Reject is on /admin/bookings
  And the queue shows a Pending row for Attendee="Layla Al-Harbi", Seat="A1",
    Session="Naval Logistics Forum"

Scenario: Reject with a reason releases the seat and notifies the attendee
  When the administrator clicks the row's Reject (close/X) quiet icon action,
    titled "Reject" / "رفض"
  Then a SimfModal opens titled "Reject booking" / "رفض الحجز"
  And it shows one textarea labelled "Reason (sent to the attendee)" /
    "السبب (يُرسل إلى الحاضر)" with maxlength 512
  And the footer shows "Cancel" / "إلغاء" and "Reject booking" / "رفض الحجز"
  When they type Reason="Seat reserved for the VIP delegation."
  And they click the footer "Reject booking" button
  Then the BFF POSTs /account/api/admin/bookings/{reservationId}/reject
    with { "Reason": "Seat reserved for the VIP delegation." }
  And the API returns HTTP 200 with ApiResult.Success=true
  And the modal closes
  And a green toast reads "Booking rejected." / "تم رفض الحجز."
  And the rejected row is gone from the reloaded queue
  And the held seat A1 is released (the same seat can be re-booked by a visitor)
  And the attendee receives a BookingRejected notification whose body contains the reason
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-bookings-reject-modal.png` (Reject modal open with reason typed)
- Screenshot after: `docs/screenshots/cp-admin-bookings-reject-after.png` (row gone + green toast)
- Console errors: 0 expected
- Network: `/account/api/admin/bookings/{id}/reject` returns 200; reload `list` returns 200
- Audit row: audit row with `Event = 'Booking.Rejected'` and the actor's id
- Notification row: Identity-DB `Notification` with `Kind = BookingRejected`, severity Warning, body containing "VIP"

### E2E-BKG-003 — Bulk "Approve selected ({n})"

```gherkin
Feature: Booking approvals — bulk approve
  As an Administrator with Bookings.Approve
  I want to approve several pending bookings at once
  So that I clear the queue efficiently

Background:
  Given two approved visitors have reserved seats A1 and A2 for "Naval Logistics Forum"
    (both Status=Pending)
  And an Administrator with Bookings.View + Bookings.Approve is on /admin/bookings
  And the queue shows both Pending rows

Scenario: Approve selected over two checked rows
  Given the "Approve selected (0)" button is disabled
  When the administrator ticks the checkbox on the A1 row
  And ticks the checkbox on the A2 row
  Then the button label reads "Approve selected (2)" / "اعتماد المحدد (2)" and is enabled
  When they click "Approve selected (2)"
  Then the BFF POSTs /account/api/admin/bookings/bulk-approve
    with { "Ids": ["{reservationId-A1}", "{reservationId-A2}"] }
  And the API returns HTTP 200 with ApiResult.Data = 2
  And a green toast reads "2 booking(s) approved." / "تم اعتماد 2 حجز."
  And both rows are gone from the reloaded queue
  And two BookingConfirmed notifications were dispatched (one per attendee)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-bookings-bulk-selected.png` (two rows ticked, button reads "Approve selected (2)")
- Screenshot after: `docs/screenshots/cp-admin-bookings-bulk-after.png` (queue cleared + toast)
- Console errors: 0 expected
- Network: `/account/api/admin/bookings/bulk-approve` returns 200 with Data=2; reload `list` returns 200
- Audit rows: two `Booking.Approved` rows, each `Detail` ending `bulk=true`

### E2E-BKG-004 — Checkbox toggle drives the bulk button

```gherkin
Scenario: Selecting and clearing checkboxes updates the bulk button label + disabled state
  Given the administrator is on /admin/bookings with at least one Pending row
  And the "Approve selected (0)" button is disabled
  When they tick one row's checkbox
  Then the button reads "Approve selected (1)" and is enabled
  When they untick that checkbox
  Then the button reads "Approve selected (0)" and is disabled again
  And no /account/api/admin/bookings/bulk-approve request has fired
  And the selection is per-page only — reloading the queue clears all checkboxes
```

### E2E-BKG-005 — Empty queue

```gherkin
Scenario: Empty queue renders SimfEmptyState
  Given no SeatReservation has Status=Pending with ReleasedAt=null and a non-null attendee
  When the administrator opens /admin/bookings
  Then the page renders the SimfEmptyState component
  And the empty state title reads "No bookings are awaiting approval." /
    "لا توجد حجوزات بانتظار الاعتماد."
  And the table, the "Approve selected" button, and the summary line are NOT rendered
  And no error toast appears
```

### E2E-BKG-006 — Auth gate

```gherkin
Scenario: Admin lacking the Bookings.View permission is denied
  Given a signed-in admin whose role does NOT include Bookings.View
    (and is not the Administrator wildcard "*")
  When they navigate to /admin/bookings
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/bookings/list request fires
  And the "Bookings" nav item is hidden for that user (RequiredPermission=Bookings.View)
  And separately, a visitor JWT calling POST /api/v1/admin/bookings/list directly
    receives HTTP 403 Forbidden (per BookingApprovalTests.Non_admin_cannot_view_the_booking_queue)
```

### E2E-BKG-007 — Reject validation (blank reason)

```gherkin
Scenario: Blank reason is rejected client-side before any POST
  Given the Reject modal is open for a Pending row
  When the administrator leaves the reason textarea blank (or only whitespace)
  And clicks the footer "Reject booking" button
  Then an error toast reads "A reason is required to reject a booking." /
    "يلزم إدخال سبب لرفض الحجز."
  And the modal stays open
  And NO /account/api/admin/bookings/{id}/reject request fires
  And, were a blank reason to reach the API, it would return HTTP 400 with
    ApiResult.Error.Code = "BOOKING_REJECTION_REASON_REQUIRED"
    (the API enforces 1–512 chars — defence in depth)
```

### E2E-BKG-008 — Conflict (already-decided booking)

```gherkin
Scenario: Approving or rejecting an already-decided booking returns 409
  Given a booking that has already been approved (Status != Pending) — e.g. it was
    approved in another browser tab a moment ago
  And the stale row is still visible in this tab's queue
  When the administrator clicks the row's Approve (check-circle) icon action
    (or rejects that row)
  Then the BFF forwards the call to the API
  And the API returns HTTP 409 with ApiResult.Error.Code = "BOOKING_NOT_PENDING"
  And a red toast surfaces the bilingual MessageForCurrentCulture()
    "This booking has already been decided." / "تم البت في هذا الحجز بالفعل."
  And the queue is reloaded so the stale row disappears
  And a booking id that does not exist would instead return HTTP 404 with
    ApiResult.Error.Code = "BOOKING_NOT_FOUND"
```

### E2E-BKG-009 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/bookings/list (e.g. DB down)
  When the administrator opens /admin/bookings
  Then the page first shows "Loading bookings…" / "جارٍ تحميل الحجوزات…"
  And then a red toast appears reading "The action could not be completed. Please try again." /
    "تعذّر إكمال العملية. يُرجى المحاولة مرة أخرى."
  And no queue rows render
  And no empty-state component renders (the load failed rather than returning zero rows)
```

### E2E-BKG-010 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Reject modal
  Given the administrator is on /admin/bookings in English with at least one Pending row
  When they switch the UI to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "اعتماد الحجوزات"
  And the column headers read "الجلسة", "تبدأ (UTC)", "المقعد", "الحاضر", "تاريخ الحجز (UTC)"
  And each row's quiet icon actions carry the titles "اعتماد" (Approve, check-circle)
    and "رفض" (Reject, close/X)
  And the bulk button reads "اعتماد المحدد ({0})"
  And the table + nav rail mirror to RTL

  When they click the row's Reject (close/X) icon action titled "رفض"
  Then the Reject modal opens in RTL titled "رفض الحجز"
  And the reason label reads "السبب (يُرسل إلى الحاضر)"
  And the footer buttons read "إلغاء" and "رفض الحجز" in reverse order
```

### E2E-BKG-011 — Per-column grid filter narrows the queue

```gherkin
Feature: Booking approvals — per-column grid filter (D-256 SimfDataGrid)
  As an Administrator with Bookings.View
  I want to filter the queue by a single column
  So that I can find one session's or one seat's pending bookings fast

Background:
  Given an Administrator with Bookings.View is on /admin/bookings
  And the queue holds Pending rows across several sessions, including
    "Naval Logistics Forum" (seat A1) and "Maritime Cyber Defence" (seat B3)

Scenario: Typing into the Session column filter narrows the grid
  Given the grid shows the per-column filter row (the "Session" and "Seat"
    columns each expose a quiet filter input — they are the only Filterable
    columns; "Starts", "Attendee" and "Booked (UTC)" have no filter input)
  And each filter input is titled "Filter column Session" / "تصفية العمود الجلسة"
    (resp. "Filter column Seat") with placeholder "Search" / "بحث"
  When the administrator types "Naval" into the Session column filter
  Then the BFF POSTs /account/api/admin/bookings/list with a GridQuery whose
    Filters["session"] = "Naval" and Skip = 0 (the offset resets to the first page)
  And the API returns HTTP 200 and the grid re-renders showing only the
    "Naval Logistics Forum" rows
  And the summary line reads "Showing 1–{M} of {M}" for the narrowed total

Scenario: A second column filter combines with the first
  Given the Session filter already holds "Naval"
  When the administrator also types "A1" into the Seat column filter
  Then the next /list POST carries Filters["session"]="Naval" AND Filters["seat"]="A1"
    with Skip = 0
  And the grid narrows to the single A1 / Naval Logistics Forum row
  When the administrator clears both filter inputs
  Then a /list POST fires with empty Filters and the full Pending queue returns
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-bookings-filter-session.png` (Session filter = "Naval", grid narrowed)
- Console errors: 0 expected
- Network: each keystroke-committed filter POSTs `/account/api/admin/bookings/list`; request body shows `filters.session` / `filters.seat` and `skip: 0`
- Note: the grid filter only sends keys the API honours; an unrecognised key is ignored server-side (the endpoint validates filter keys per `GridQuery` D-045)

### E2E-BKG-012 — Column sort toggles

```gherkin
Feature: Booking approvals — column sort (D-256 SimfDataGrid)
  As an Administrator with Bookings.View
  I want to sort the queue by a column
  So that I can triage by session, start time, seat, or booking time

Background:
  Given an Administrator with Bookings.View is on /admin/bookings
  And the queue holds at least three Pending rows across different sessions

Scenario: Clicking a sortable header toggles ascending then descending
  Given the "Session", "Starts (UTC)", "Seat" and "Booked (UTC)" headers are
    sortable (the "Attendee" column is NOT sortable — no sort affordance)
  And the queue is in its natural newest-first order (aria-sort="none" on every header)
  When the administrator clicks the "Session" column header
  Then the BFF POSTs /account/api/admin/bookings/list with a GridQuery whose
    Sort = "session", SortDescending = false and Skip = 0
  And the header renders aria-sort="ascending" and the rows reorder A→Z by session
  When the administrator clicks the "Session" header again
  Then the next /list POST carries Sort = "session", SortDescending = true, Skip = 0
  And the header renders aria-sort="descending" and the rows reorder Z→A
  When the administrator clicks the "Booked (UTC)" header instead
  Then a /list POST carries Sort = "bookedAt", SortDescending = false, Skip = 0
  And the "Session" header returns to aria-sort="none"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-bookings-sort-session.png` (Session header aria-sort="descending")
- Console errors: 0 expected
- Network: each header click POSTs `/account/api/admin/bookings/list` with the new `sort` / `sortDescending` and `skip: 0`

### E2E-BKG-013 — Excel export (D-356)

```gherkin
Feature: Booking approvals — Excel export (D-356 Uniform CRUD)
  As an Administrator with the Bookings.Export permission
  I want to export the booking approval queue to an .xlsx workbook
  So that I can review or share the pending bookings offline

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with Bookings.View + Bookings.Export has signed in via
    /login + /login/totp and landed on /admin/bookings
  And the queue holds at least two Pending rows, including Session="Naval Logistics Forum"
    Seat="A1" for Attendee="Layla Al-Harbi", and Session="Maritime Cyber Defence"
    Seat="B3" for Attendee="Omar Said"

Scenario: Export the whole filtered queue when no rows are selected
  Given the grid toolbar shows the "Export" action labelled "Export" / "تصدير"
    (wired on the SimfDataGrid via OnExport; the underlying
    /api/v1/admin/bookings/export endpoint is gated by Bookings.Export, so an
    admin lacking that permission gets HTTP 403 on click)
  And no rows are selected
  When the administrator clicks "Export"
  Then the browser invokes simfAccount.downloadXlsx against
    POST /account/api/admin/bookings/export
  And the request body is an AdminGridExportRequest whose Ids list is EMPTY
    and whose Query carries the current GridQuery (so the export mirrors the
    on-screen filtered/sorted set)
  And the API returns HTTP 200 with an
    application/vnd.openxmlformats-officedocument.spreadsheetml.sheet body
  And the browser saves a file named simf-bookings-{timestamp}.xlsx
  And the workbook's "Bookings" sheet has the header row
    SessionTitle | SessionTitleArabic | SessionStart | Row | Seat | Kind | Attendee | BookedAt
  And the body has one row per Pending booking in the queue (seat split into the
    Row label + Seat number, and the seat Kind enum rendered as text)

Scenario: Export only the selected rows
  Given the administrator ticks the checkbox on the "Naval Logistics Forum" / A1 row
  And ticks the checkbox on the "Maritime Cyber Defence" / B3 row
  When they click "Export"
  Then the AdminGridExportRequest body carries Ids = ["{reservationId-A1}", "{reservationId-B3}"]
    and a null Query (the selection wins over the filtered set)
  And the workbook contains exactly those two booking rows
  And a selection larger than the 5000-row export cap is rejected by the API
    with HTTP 400 (the endpoint caps export at 5000 rows)

Scenario: Export is export-only — there is no Import affordance
  Given the administrator is on /admin/bookings
  Then the toolbar exposes "Export" but NOT an "Import" action
  And no /account/api/admin/bookings/import endpoint exists for this page
    (bookings are created by visitors in the app and only approved/rejected here)
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-bookings-export.png` (toolbar Export action, no Import action present)
- Console errors: 0 expected
- Network: `POST /account/api/admin/bookings/export` returns 200; request body shows `ids: []` + a `query` object (whole-grid export) or a populated `ids` array + `query: null` (selected-rows export)
- Downloaded workbook: `simf-bookings-{timestamp}.xlsx`, sheet "Bookings", header row `SessionTitle | SessionTitleArabic | SessionStart | Row | Seat | Kind | Attendee | BookedAt`
- API integration test: `tests/SIMF.Api.Tests/BookingsExcelTests.cs` covers the same surface at the endpoint layer (export-only — no import test, as `ExportBookingsEndpoint` is the only Excel endpoint for this page)

---

## Implementation notes

- **Page doc gap.** There is no `docs/pages/cp/admin-bookings.md` reference doc at
  the time of this rebuild — the page contract lives in the page header comment
  (`BookingsList.razor`, P2.2 / D-227, FDS-005 §5.2) and the resx keys
  (`Admin.Bookings.*` in `Strings.resx` / `Strings.ar.resx`). Author the page doc
  when convenient; the `Page` link above is forward-declared.
- **No CRUD verbs here.** Unlike `cp-admin-interests`, this page has **no** Add /
  Edit / Details / Deactivate. The only mutating actions are Approve (row + bulk)
  and Reject (row, with a required reason). Do not author scenarios for actions the
  page does not expose.
- **API integration tests** at `tests/SIMF.Api.Tests/BookingApprovalTests.cs`
  cover the same surface at a lower layer (no browser):
  - `Approve_confirms_the_seat_and_writes_booking_confirmed` (→ E2E-BKG-001)
  - `Reject_with_a_reason_releases_the_seat_and_notifies` (→ E2E-BKG-002)
  - `Bulk_approve_approves_the_selected_bookings` (→ E2E-BKG-003)
  - `Reject_without_a_reason_is_400` → `BOOKING_REJECTION_REASON_REQUIRED` (→ E2E-BKG-007)
  - `Non_admin_cannot_view_the_booking_queue` → HTTP 403 (→ E2E-BKG-006)
  - `Overlapping_booking_in_another_session_is_blocked` → `BOOKING_OVERLAP`
    and `Cancel_after_the_session_has_started_is_refused` → `BOOKING_SESSION_STARTED`
    exercise the visitor-facing reserve/cancel guards (lower layer, not this CP page).
- **Wire contract.** BFF passthroughs (`AccountEndpoints.cs`): `/account/api/admin/bookings/list`
  (body `GridQuery`), `/{id}/approve` (empty body), `/{id}/reject` (body
  `RejectBookingRequest { Reason }`), `/bulk-approve` (body
  `AdminBulkApprovalRequest { Ids }`). API endpoints live in
  `SeatReservationEndpoints.cs`; the orchestration + error codes + notifications
  are in `SeatReservationService.cs`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus
  a step-definition class. The Gherkin shape is already runner-agnostic.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle; added E2E-BKG-013 Excel export, export-only per `ExportBookingsEndpoint`). Earlier: 2026-06-03 (E2E catalogue rebuild, D-256/D-257 grid affordances reconciled).
