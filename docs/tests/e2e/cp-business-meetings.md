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
| **API** | `POST /api/v1/admin/business-meetings/list`, `GET /api/v1/admin/business-meetings/{id}`, `POST /api/v1/admin/business-meetings`, `POST /api/v1/admin/business-meetings/{id}/cancel` |
| **Backed by tests** | `tests/SIMF.Api.Tests/BusinessMeetingsTests.cs` |
| **Last reviewed** | 2026-06-03 |

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
  When the admin clicks "View" on its row
  Then the detail modal lists each participant's display name and kind
  And shows the hall, table, type, start and end
```

### E2E-BMT-005 — Cancel

```gherkin
Scenario: Cancel a confirmed meeting with a reason
  Given a confirmed meeting exists
  When the admin clicks "Cancel", enters reason "Rescheduled by organiser." and confirms
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
  When the admin selects "Cancelled" in the status filter
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

---

_Last reviewed:_ 2026-06-03 by the SIMF engineering team.
