# E2E test catalogue — `Business Meetings` (`/admin/business-meetings`)

> **Authority:** SIMF-FDS-013 (D-248). Admin-arranged B2B/B2C business meetings —
> the admin schedules a meeting between two or more parties (companies + visitors)
> at a meeting table for a from–to time-slot; confirmed on save, cancellable.

| | |
|--|--|
| **Page** | [`business-meetings.md`](../../pages/cp/business-meetings.md) |
| **Route** | `/admin/business-meetings` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via `Get-Totp` helper |
| **Permissions** | `BusinessMeetings.View` (page), `BusinessMeetings.Schedule`, `BusinessMeetings.Cancel` |
| **API** | `POST /api/v1/admin/business-meetings/list`, `GET /api/v1/admin/business-meetings/{id}`, `POST /api/v1/admin/business-meetings`, `POST /api/v1/admin/business-meetings/{id}/cancel`, `POST /api/v1/admin/business-meetings/export` |
| **Backed by tests** | `tests/SIMF.Api.Tests/BusinessMeetingsTests.cs`, `tests/SIMF.Api.Tests/BusinessMeetingsExcelTests.cs` |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel export added; export-only — no import, no toggle) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-BMT-001 | Golden path — schedule a B2B company↔company meeting | happy | P0 | authored |
| E2E-BMT-002 | Schedule a B2C company↔visitor meeting | happy | P0 | authored |
| E2E-BMT-003 | Schedule a group meeting (3+ participants) | happy | P1 | authored |
| E2E-BMT-004 | View meeting detail (participants resolved) | happy | P1 | authored |
| E2E-BMT-005 | Cancel a confirmed meeting (with reason) | happy | P0 | authored |
| E2E-BMT-006 | Empty state | happy | P1 | authored |
| E2E-BMT-007 | Status filter (Confirmed / Cancelled) | happy | P2 | authored |
| E2E-BMT-008 | Validation — fewer than two participants | error | P1 | authored |
| E2E-BMT-009 | Conflict — same table, overlapping slot | error | P0 | authored |
| E2E-BMT-010 | Conflict — same party, overlapping meetings | error | P0 | authored |
| E2E-BMT-011 | Capacity — participants over table capacity | error | P1 | authored |
| E2E-BMT-012 | Auth gate (non-admin → /not-permitted) | auth | P0 | authored |
| E2E-BMT-013 | RTL render (Arabic) | i18n | P1 | authored |
| E2E-BMT-014 | Per-column filter narrows the grid | happy | P1 | _to author_ |
| E2E-BMT-015 | Column sort toggles | happy | P2 | _to author_ |
| E2E-BMT-016 | Excel export — toolbar Export downloads an .xlsx of the meetings grid (whole filtered set vs selected rows) (D-356) | happy | P1 | _to author_ |

## Scenarios

### E2E-BMT-001 — Golden path (B2B)

```gherkin
Feature: Schedule a B2B business meeting
  As an organiser with BusinessMeetings.Schedule
  I want to arrange a meeting between two companies at a table
  So that exhibitors can meet at the forum

Background:
  Given an Administrator is signed in
  And a hall "Majlis A" exists with Purpose = Meeting
  And the hall has a meeting table "T-001" with capacity 4
  And two active exhibitor companies "Alpha Marine" and "Beta Yards" exist

Scenario: Schedule a confirmed B2B meeting
  When the admin opens /admin/business-meetings
  And clicks "Schedule meeting"
  And selects hall "Majlis A" and table "T-001"
  And selects type "B2B"
  And sets Start = tomorrow 10:00 UTC and End = tomorrow 11:00 UTC
  And adds participant Company "Alpha Marine"
  And adds participant Company "Beta Yards"
  And submits
  Then a success toast "Meeting scheduled." appears
  And the grid shows a Confirmed row for "Majlis A / T-001" with 2 participants
  And an OperationLog row Event = "BusinessMeeting.Scheduled" is written
```

**Evidence captured:**
- Console errors: 0 expected · Network failures: 0 expected
- Audit row: `OperationLog` `Event = 'BusinessMeeting.Scheduled'` with the actor id.

### E2E-BMT-002 — B2C with a visitor

```gherkin
Scenario: Schedule a B2C company↔visitor meeting
  Given an approved visitor "Sara Q" exists in the attendee roster
  When the admin schedules a meeting at "T-001" with type "B2C"
  And adds participant Company "Alpha Marine"
  And adds participant Visitor "Sara Q"
  And submits
  Then the meeting is Confirmed
  And both the company account(s) and the visitor receive a MeetingScheduled notification
```

### E2E-BMT-003 — Group meeting

```gherkin
Scenario: Schedule a group meeting within table capacity
  Given table "T-001" has capacity 4
  When the admin adds 3 company participants and submits
  Then the meeting is Confirmed with 3 participants
```

### E2E-BMT-004 — View detail

```gherkin
Scenario: View a meeting's detail
  Given a confirmed meeting exists
  When the admin clicks the row's Details (view) action in the grid
  Then the detail modal lists each participant's display name and kind
  And shows the hall, table, type, start and end
```

### E2E-BMT-005 — Cancel

```gherkin
Scenario: Cancel a confirmed meeting with a reason
  Given a confirmed meeting exists
  When the admin clicks the row's Cancel (quiet close-icon) action, enters reason "Rescheduled by organiser." and confirms
  Then a success toast "Meeting cancelled." appears
  And the row shows status Cancelled
  And the table/slot is free again (a new meeting can be scheduled on it)
  And each participant receives a MeetingCancelled notification
  And an OperationLog row Event = "BusinessMeeting.Cancelled" is written
```

### E2E-BMT-006 — Empty state

```gherkin
Scenario: Empty state renders SimfEmptyState
  Given no business meetings exist
  When the admin opens the page
  Then the SimfEmptyState "No business meetings yet." is shown
```

### E2E-BMT-007 — Status filter

```gherkin
Scenario: Filter by status
  Given confirmed and cancelled meetings exist
  When the admin opens the Status column's grid filter and enters "Cancelled"
  Then only Cancelled rows are listed
```

### E2E-BMT-008 — Too few participants

```gherkin
Scenario: Fewer than two participants is rejected
  When the admin tries to schedule with a single participant
  Then the API returns 400 MEETING_PARTICIPANT_INVALID
  And an error toast is shown
```

### E2E-BMT-009 — Table conflict

```gherkin
Scenario: Overlapping meeting on the same table is rejected
  Given a confirmed meeting on "T-001" from 10:00 to 11:00
  When the admin schedules another meeting on "T-001" from 10:30 to 12:00
  Then the API returns 409 BUSINESS_MEETING_TABLE_CONFLICT
```

### E2E-BMT-010 — Participant conflict

```gherkin
Scenario: A party already booked at the time is rejected
  Given company "Alpha Marine" is in a confirmed meeting from 10:00 to 11:00
  When the admin schedules a meeting including "Alpha Marine" from 10:30 to 11:30
  Then the API returns 409 BUSINESS_MEETING_PARTICIPANT_CONFLICT
```

### E2E-BMT-011 — Capacity exceeded

```gherkin
Scenario: More participants than the table seats is rejected
  Given table "T-002" has capacity 2
  When the admin schedules a meeting with 3 participants on "T-002"
  Then the API returns 409 MEETING_CAPACITY_EXCEEDED
```

### E2E-BMT-012 — Auth gate

```gherkin
Scenario: Non-administrator user is denied
  Given a signed-in user with no BusinessMeetings.View permission
  When they navigate to /admin/business-meetings
  Then they are redirected to /not-permitted with HTTP 200
  And POST /api/v1/admin/business-meetings returns 403 for that caller
```

### E2E-BMT-013 — RTL render

```gherkin
Scenario: Arabic UI renders right-to-left
  Given the UI language is Arabic
  When the admin opens the page
  Then the banner reads "اجتماعات الأعمال" and the layout is RTL
  And the schedule modal labels are Arabic
```

### E2E-BMT-014 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a per-column grid filter narrows the meetings list (D-255/D-256)
  Given confirmed meetings exist in halls "Majlis A" and "Majlis B"
  When the admin opens the Hall column's "Filter column Hall" input and types "Majlis A"
  Then a POST /api/v1/admin/business-meetings/list fires
    with GridQuery.Filters["hall"] = "Majlis A" and Skip reset to 0
  And the grid narrows to rows whose hall contains "Majlis A"
  When the admin also opens the Status column's "Filter column Status" input and types "Cancelled"
  Then the request carries GridQuery.Filters["status"] = "Cancelled" (parsed to the BusinessMeetingStatus enum)
  And only Cancelled rows in "Majlis A" remain
  And clearing both filter inputs re-issues the list with no Filters and restores the full grid
```

**Evidence captured:**
- Filterable columns are Hall (`hall`), Table (`table`) and Status (`status`); the
  per-column filter inputs read "Filter column {Header}" / Arabic "تصفية العمود".
- Network: each keystroke-commit posts `/business-meetings/list` with `Skip = 0`.

### E2E-BMT-015 — Column sort toggles

```gherkin
Scenario: Clicking a sortable column header toggles ascending/descending (D-256)
  Given several confirmed meetings exist across different start times
  When the admin clicks the "Start" column header
  Then a POST /api/v1/admin/business-meetings/list fires
    with GridQuery.Sort = "start" and SortDescending = false
  And the rows are ordered by start time ascending
  When the admin clicks the "Start" header again
  Then the request carries Sort = "start" and SortDescending = true
  And the rows reverse to descending order
```

**Evidence captured:**
- Sortable columns are Hall, Table, Type, Start, End, Status (the Parties count
  column is not sortable). The default (no Sort) order is StartUtc descending.

### E2E-BMT-016 — Excel export (D-356)

```gherkin
Scenario: Export the business-meetings grid to an XLSX workbook
  Given an Administrator with BusinessMeetings.Export is on /admin/business-meetings
  And the grid shows at least two confirmed meetings across halls "Majlis A" and "Majlis B"

  When the admin clicks the toolbar "Export" action with no rows selected
  Then a POST /account/api/admin/business-meetings/export fires (BFF -> API)
    carrying AdminGridExportRequest with an empty Ids list and the current Query
    (the page sends Query only when no rows are selected)
  And the API caps the export at 5000 rows and resets Skip to 0
  And the browser saves a file named simf-business-meetings-{yyyyMMddHHmmss}.xlsx
  And the workbook's "BusinessMeetings" sheet header row reads
    Hall | Table | Type | Start | End | Parties | Status
  And the Type cells render "B2B"/"B2C"/"G2B" and the Status cells render "Confirmed"/"Cancelled" (display text, not the wire enum)

  When the admin instead ticks exactly two meeting rows and clicks "Export"
  Then the request carries those two row Ids and a null Query
  And the workbook contains exactly those two meetings' rows (plus the header)

Scenario: Export is permission-gated (export-only — no import)
  Given a signed-in admin WITHOUT BusinessMeetings.Export
  When they POST /account/api/admin/business-meetings/export
  Then the API returns 403 (the endpoint is gated by PermissionCatalog.BusinessMeetings.Export)
  And the page exposes no Import action — scheduling and cancelling stay on the page's bespoke modals
```

**Evidence captured:**
- The grid wires `OnExport` only (no `OnImport`); `OnExportAsync` calls
  `simfAccount.downloadXlsx("/account/api/admin/business-meetings/export", …)`.
- This page has **no D-353 Page<->Popup presentation toggle** and **no CrudShell
  delete/confirm flow** — meetings are scheduled/cancelled through the existing
  `SimfModal` dialogs (see E2E-BMT-001 / E2E-BMT-005). Only the D-356 Excel
  **export** was added; there is intentionally **no import** path.

### E2E-BMT-017 — Schedule a G2B meeting (D-730, owner item 15B)

```gherkin
Scenario: The type dropdown offers G2B and it round-trips
  Given an Administrator on /admin/business-meetings scheduling a meeting
  Then the "Type" dropdown offers B2B, B2C, and G2B (government-to-business)
  When the admin picks G2B, fills a Meeting-purpose hall / table / slot + two
    participants and schedules
  Then POST /account/api/admin/business-meetings succeeds
  And the detail + grid show the type G2B, and the Excel export renders "G2B"
  # G2B is an additive BusinessMeetingType value (no schema change); the
  # delegation (g2g) desk is unchanged.
```

### E2E-BMT-018 — Schedule a meeting whose start is in the past is blocked (M-5)

```gherkin
Scenario: A past start is rejected by the shared ValidateSlot lower bound
  Given an Administrator scheduling a meeting on a Meeting-purpose table
  When StartUtc is in the past (EndUtc a valid hour later) with two valid companies
  Then POST /account/api/admin/business-meetings returns 400 HALL_ALLOCATION_INVALID
    (bilingual toast: "The start time cannot be in the past." /
    "لا يمكن أن يكون وقت البداية في الماضي.")
```

### E2E-BMT-019 — Create a hall allocation whose start is in the past is blocked (M-5)

```gherkin
Scenario: The same not-in-past bound guards the allocation path
  Given an Administrator creating a Whole hall allocation
  When StartUtc is in the past and EndUtc is a valid hour after it
  Then POST /account/api/admin/halls/{id}/hall-allocations returns 400 HALL_ALLOCATION_INVALID
```

> **Concurrency note (M-5).** The table / hall / participant overlap checks
> (E2E-BMT-004 table conflict, E2E-BMT-005 participant conflict, and the
> whole-hall-session block) now run together with the insert inside ONE
> Serializable transaction (via the EF execution strategy), so two concurrent
> overlapping schedules can no longer both slip through — the loser retries and
> re-checks, raising the clean 409. No behaviour change for the sequential paths.

**Evidence:** `BusinessMeetingsTests.Schedule_a_meeting_in_the_past_is_400`, `Create_hall_allocation_in_the_past_is_400`, plus the regression guards `Overlapping_meeting_on_the_same_table_is_409_table_conflict`, `Same_party_in_two_overlapping_meetings_is_409_participant_conflict`, `A_whole_hall_session_allocation_blocks_a_meeting_in_that_hall`, `Schedule_b2c_with_a_visitor_then_cancel` (all green under the Serializable transaction).

---

_Last reviewed:_ 2026-07-11 by Claude (on-site W2b — M-5 not-in-past ValidateSlot lower bound + Serializable-transaction double-book closure; added E2E-BMT-018/019). Prior: 2026-07-10 by SIMF Team (D-730, item 15B — added the G2B business-meeting type + E2E-BMT-017); 2026-06-10 (D-356 Phase 5 — Excel export added; export-only, no import/toggle); 2026-06-03 (D-256/D-257 grid affordances reconciled).
