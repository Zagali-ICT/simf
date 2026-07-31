# E2E test catalogue — Meeting hall check-in reporting + delegation export

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md).

| | |
|--|--|
| **Pages** | [`cp-admin-speaker-meeting-requests.md`](cp-admin-speaker-meeting-requests.md) · [`cp-admin-delegation-meetings.md`](cp-admin-delegation-meetings.md) |
| **Routes** | `POST /api/v1/admin/speaker-meeting-requests/list` · `POST /api/v1/admin/speaker-meeting-requests/export` · `POST /api/v1/admin/delegation-meeting-requests/list` · **new** `POST /api/v1/admin/delegation-meeting-requests/export` |
| **Surface** | Admin API + Control Panel grids |
| **Test runner** | xUnit + `SimfApiFactory` (`tests/SIMF.Api.Tests/MeetingCheckInExportTests.cs`) |
| **Auth setup** | Administrator account; `DelegationMeetings.Export` / `SpeakerMeetingRequests.Export` |
| **Last reviewed** | 2026-07-31 |

## What changed and why

`OA-D5`. The check-in ACTIONS existed — `POST /admin/speaker-meeting-requests/{id}/check-in`
and `POST /admin/delegation-meeting-requests/{id}/check-in` — and both stamped
`CheckedInAt` + `CheckedInByUserId`. The reporting surface exposed neither:
`AdminSpeakerMeetingRequestRow` had no `CheckedIn*` member at all, the speaker XLSX
shipped exactly six columns (Speaker, Requester, Subject, Status, CreatedAt,
RespondedAt), and the delegation desk had **no `/export` route whatsoever**. "Who
actually turned up" was unanswerable anywhere off-screen.

Now:

- `AdminSpeakerMeetingRequestRow` and `AdminDelegationMeetingRequestRow` each gained
  `CheckedInAt` + `CheckedInByName`, **appended with defaults** so the shipped wire
  contract stays append-only (D-219).
- Both list services project the stamps and resolve the operator's display name
  through `IIdentityUserDirectory.GetDisplayNamesAsync` — ONE Identity-DB query per
  page, merged in memory. `CheckedInByUserId` is a bare logical FK (D-157), so this
  is never a cross-database JOIN.
- The speaker export gained `CheckedInAt` + `CheckedInBy` columns.
- A new `ExportDelegationMeetingRequestsEndpoint` mirrors the speaker one, gated on
  the new `PermissionCatalog.DelegationMeetings.Export`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MCX-001 | Speaker grid row carries the check-in stamps | happy | P0 | automated |
| E2E-MCX-002 | A row with no check-in reports null stamps | happy | P0 | automated |
| E2E-MCX-003 | Delegation grid row carries the check-in stamps | happy | P0 | automated |
| E2E-MCX-004 | Delegation export returns an XLSX workbook | happy | P0 | automated |
| E2E-MCX-005 | Delegation export is forbidden without `DelegationMeetings.Export` | auth | P0 | automated |
| E2E-MCX-006 | Speaker export carries the two new columns | happy | P0 | automated |
| E2E-MCX-007 | Operator name resolves without a cross-DB join | perf | P1 | automated |
| E2E-MCX-008 | A deleted operator account leaves the name blank, not an error | resilience | P1 | manual |

## Scenarios

### E2E-MCX-001 — Speaker grid row carries the check-in stamps

```gherkin
Feature: Meeting check-in reporting
  As the meetings desk
  I want the check-in stamps on the grid and in the export
  So that "who actually turned up" is answerable off-screen

Background:
  Given an Administrator "Gate Operator Nora" is signed in
  And a speaker meeting request that Nora checked in at the hall

Scenario: The grid shows when it was checked in and by whom
  When the administrator POSTs /api/v1/admin/speaker-meeting-requests/list with { top: 200 }
  Then the response is 200
  And the row for that request has a non-null checkedInAt
  And its checkedInByName is "Gate Operator Nora"
```

**Evidence captured:** `MeetingCheckInExportTests.Speaker_grid_row_carries_the_check_in_stamps`.

### E2E-MCX-002 — A row with no check-in reports null stamps

```gherkin
Scenario: An accepted-but-not-yet-arrived meeting reports nothing
  Given an Accepted speaker meeting request that nobody has checked in
  When the administrator lists the grid
  Then that row's checkedInAt is null
  And its checkedInByName is null
```

Both members are nullable with `null` defaults, so an older client that does not know
them is unaffected (D-219 append-only).

**Evidence captured:** `MeetingCheckInExportTests.Speaker_row_without_a_check_in_reports_null_stamps`.

### E2E-MCX-003 — Delegation grid row carries the check-in stamps

```gherkin
Scenario: The delegation desk mirrors the speaker desk
  Given a delegation (G2G) meeting request checked in by "Gate Operator Nora"
  When the administrator POSTs /api/v1/admin/delegation-meeting-requests/list
  Then the row has a non-null checkedInAt
  And its checkedInByName is "Gate Operator Nora"
```

**Evidence captured:** `MeetingCheckInExportTests.Delegation_grid_row_carries_the_check_in_stamps`.

### E2E-MCX-004 — Delegation export returns an XLSX workbook

```gherkin
Scenario: The desk that had no export now has one
  Given an Administrator holding DelegationMeetings.Export is signed in
  When they POST /api/v1/admin/delegation-meeting-requests/export
    with { query: { top: 100 } }
  Then the response is 200
  And the Content-Disposition filename starts with "simf-delegation-meeting-requests-"
  And the body begins with the ZIP local-file header 50 4B 03 04
  And the worksheet is named "DelegationMeetingRequests"
  And its columns are
    RequestingCountry, TargetCountry, Attendees, Subject, Status, SlotStart,
    CreatedAt, RespondedAt, CheckedInAt, CheckedInBy
```

The requester email is deliberately **not** a column: it is per-record PII surfaced
only through the audited detail endpoint (the D-185 pattern the speaker export
already follows).

**Evidence captured:** `MeetingCheckInExportTests.Delegation_export_returns_an_xlsx_workbook`.

### E2E-MCX-005 — Export permission gate

```gherkin
Scenario: Reading the desk is not the same act as taking a spreadsheet off it
  Given an approved visitor (no admin permissions) is signed in
  When they POST /api/v1/admin/delegation-meeting-requests/export
  Then the response is 403
```

`Export` is split from `View` for exactly the reason every other export gate is:
downloading the whole meeting roster is a bigger act than reading a page of it.

**Evidence captured:** `MeetingCheckInExportTests.Delegation_export_is_forbidden_without_the_export_permission`.

### E2E-MCX-006 — Speaker export carries the two new columns

```gherkin
Scenario: The six-column speaker export becomes eight
  Given a checked-in speaker meeting request
  When the administrator POSTs /api/v1/admin/speaker-meeting-requests/export
  Then the workbook's columns are
    Speaker, Requester, Subject, Status, CreatedAt, RespondedAt, CheckedInAt, CheckedInBy
  And the CheckedInAt cell renders as "yyyy-MM-dd HH:mm UTC"
  And the CheckedInBy cell holds the operator's display name
```

**Evidence captured:** `SpeakerMeetingRequestsExcelTests.Export_returns_an_xlsx_workbook`
(round-trip) plus `MeetingCheckInExportTests` for the values.

### E2E-MCX-007 — Operator name resolves without a cross-DB join

```gherkin
Scenario: One Identity query per page, not one per row
  Given a page of 25 meeting requests, 20 of them checked in by 3 different operators
  When the administrator lists the grid
  Then every checkedInByName is populated
  And the Identity database was queried once for the page
```

`CheckedInByUserId` is a bare `Guid` (D-157) — App and Identity are physically
separate databases, so the name comes from a second query merged in memory. Rows
with no check-in contribute no id to that query.

### E2E-MCX-008 — A deleted operator leaves the name blank

```gherkin
Scenario: A dangling logical FK degrades, it does not throw
  Given a checked-in meeting whose operator account no longer exists
  When the administrator lists the grid
  Then checkedInAt is still populated
  And checkedInByName is null
  And no error is raised
```

`GetDisplayNamesAsync` omits ids with no matching user, and `ResolveOperatorName`
returns null on a miss — the timestamp is the audit fact and survives.

## Follow-up outside this change

The CP grids do not yet render the two new columns and the delegation page has no
Export button. Adding them needs `SimfAdminClient` + `AccountEndpoints` changes (the
CP is a BFF with no catch-all proxy), which are outside this track's file set. See
`docs/_pending/C2.md`.
