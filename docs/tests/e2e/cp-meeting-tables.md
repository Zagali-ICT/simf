# E2E test catalogue — `Meeting Tables & Hall Allocation` (`/admin/meeting-tables`)

> **Authority:** SIMF-FDS-013 (D-248). Flexible hall configuration — set a hall's
> purpose, define / generate meeting tables (random-by-count or by row-column,
> stop-at-max), and reserve hall space (whole / random-by-count / row-column) over
> a from–to time-slot.

| | |
|--|--|
| **Page** | [`meeting-tables.md`](../../pages/cp/meeting-tables.md) |
| **Route** | `/admin/meeting-tables` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via `Get-Totp` helper |
| **Permissions** | `MeetingTables.View` (page), `MeetingTables.Edit`, `HallAllocations.View/Edit`, `Halls.Edit` (set purpose) |
| **API** | `PUT /api/v1/admin/halls/{id}/purpose`, `POST /api/v1/admin/halls/{hallId}/meeting-tables[/list|/generate]`, `PUT|DELETE /api/v1/admin/meeting-tables/{id}`, `POST /api/v1/admin/halls/{hallId}/hall-allocations[/list]`, `DELETE /api/v1/admin/hall-allocations/{id}` |
| **Backed by tests** | `tests/SIMF.Api.Tests/BusinessMeetingsTests.cs` |
| **Last reviewed** | 2026-06-03 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MHT-001 | Set a hall's purpose to Meeting | happy | P0 | authored |
| E2E-MHT-002 | Add a single meeting table | happy | P0 | authored |
| E2E-MHT-003 | Generate tables random-by-count (stop at hall capacity) | happy | P0 | authored |
| E2E-MHT-004 | Generate tables by row/column spec | happy | P1 | authored |
| E2E-MHT-005 | Generate with Reset (clear existing first) | happy | P1 | authored |
| E2E-MHT-006 | Edit a table | happy | P1 | authored |
| E2E-MHT-007 | Delete a table | happy | P1 | authored |
| E2E-MHT-008 | Reserve hall — whole / by-count / row-column over a slot | happy | P0 | authored |
| E2E-MHT-009 | Release an allocation | happy | P1 | authored |
| E2E-MHT-010 | Table in a non-Meeting hall is rejected | error | P0 | authored |
| E2E-MHT-011 | Overlapping hall allocation is rejected | error | P0 | authored |
| E2E-MHT-012 | Auth gate (non-admin → /not-permitted) | auth | P0 | authored |

## Scenarios

### E2E-MHT-001 — Set hall purpose

```gherkin
Feature: Configure a hall for meetings
Background:
  Given an Administrator is signed in
  And a hall "Majlis A" exists with Purpose = General

Scenario: Set the purpose to Meeting
  When the admin opens /admin/meeting-tables
  And selects hall "Majlis A"
  And sets Purpose = Meeting and clicks "Set purpose"
  Then a success toast "Hall purpose saved." appears
  And an OperationLog row Event = "Hall.PurposeChanged" is written
```

### E2E-MHT-002 — Add a table

```gherkin
Scenario: Add a single meeting table
  Given hall "Majlis A" has Purpose = Meeting
  When the admin clicks "Add table", enters Code "T-01", Capacity 4 and saves
  Then the table appears in the tables grid
  And an OperationLog row Event = "MeetingTable.Created" is written
```

### E2E-MHT-003 — Generate random-by-count

```gherkin
Scenario: Generate N tables, capped at hall capacity
  Given hall "Majlis A" has Purpose = Meeting and capacity 50
  When the admin clicks "Generate tables", mode "Random by count", Count 6, Capacity 2 and submits
  Then 6 tables "T-001..T-006" are created
  And the result toast "Tables generated." is shown
  And an OperationLog row Event = "MeetingTable.Generated" is written
```

### E2E-MHT-004 — Generate by row/column

```gherkin
Scenario: Generate tables from a CSV row/column spec
  When the admin generates with mode "By row/column" and spec "A1,A2,B3"
  Then 3 tables coded A1, A2, B3 are created with row/column parsed (A,1 etc.)
```

### E2E-MHT-005 — Generate with reset

```gherkin
Scenario: Reset clears existing tables first
  Given the hall already has 4 tables
  When the admin generates with Reset checked, mode "Random by count", Count 3
  Then the 4 existing tables are deactivated and 3 new tables remain
```

### E2E-MHT-006 — Edit table

```gherkin
Scenario: Edit a table's capacity
  Given a table "T-01" with capacity 2
  When the admin edits it to capacity 6 and saves
  Then the grid shows capacity 6
```

### E2E-MHT-007 — Delete table

```gherkin
Scenario: Delete a table with no upcoming meetings
  Given a table "T-09" with no confirmed future meetings
  When the admin clicks Delete and confirms
  Then the table is removed from the grid
  And deleting a table that has upcoming confirmed meetings returns 409 MEETING_TABLE_INVALID
```

### E2E-MHT-008 — Reserve hall

```gherkin
Scenario: Reserve hall space over a time-slot
  When the admin clicks "Reserve hall", picks Purpose Meeting, Mode "Whole hall",
    Start tomorrow 09:00 and End tomorrow 12:00 and saves
  Then the allocation appears in the allocations grid
  And an OperationLog row Event = "HallAllocation.Created" is written
```

### E2E-MHT-009 — Release allocation

```gherkin
Scenario: Release a hall allocation
  Given an active allocation exists
  When the admin clicks Release and confirms
  Then the allocation drops out of the grid (the slot is free again)
  And an OperationLog row Event = "HallAllocation.Released" is written
```

### E2E-MHT-010 — Table in non-Meeting hall

```gherkin
Scenario: A Session-purpose hall cannot hold meeting tables
  Given hall "Auditorium" has Purpose = Session
  When the admin tries to add a meeting table to it
  Then the API returns 409 HALL_NOT_MEETING_PURPOSE
```

### E2E-MHT-011 — Overlapping allocation

```gherkin
Scenario: Two overlapping allocations on one hall are rejected
  Given an active allocation on "Majlis A" from 09:00 to 12:00
  When the admin reserves the same hall from 11:00 to 13:00
  Then the API returns 409 HALL_ALLOCATION_OVERLAP
```

### E2E-MHT-012 — Auth gate

```gherkin
Scenario: Non-administrator user is denied
  Given a signed-in user with no MeetingTables.View permission
  When they navigate to /admin/meeting-tables
  Then they are redirected to /not-permitted with HTTP 200
```

---

_Last reviewed:_ 2026-06-03 by the SIMF engineering team.
