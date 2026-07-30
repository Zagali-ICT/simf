# E2E test catalogue — `Reports` (`/admin/reports*`)

| | |
|--|--|
| **Page** | [`reports.md`](../../pages/cp/reports.md) |
| **Routes** | `/admin/reports`, `/admin/reports/attendance`, `/admin/reports/registrations`, `/admin/reports/gates` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via `Get-Totp` helper |
| **Last reviewed** | 2026-07-30 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-RPT-001 | Hub lists the reports the operator may open | happy | P0 | authored |
| E2E-RPT-002 | Attendance report renders rows and totals | happy | P0 | authored |
| E2E-RPT-003 | Registrations report renders rows and totals | happy | P0 | authored |
| E2E-RPT-004 | Gate report renders allowed and denied scans | happy | P0 | authored |
| E2E-RPT-005 | The last day of the range is included | boundary | P0 | authored |
| E2E-RPT-006 | An inverted range is refused before it queries | validation | P1 | authored |
| E2E-RPT-007 | Clearing the range restores the unbounded report | happy | P1 | authored |
| E2E-RPT-008 | Export downloads a valid XLSX | happy | P0 | authored |
| E2E-RPT-009 | Export file name carries a Saudi-local stamp | i18n | P1 | authored |
| E2E-RPT-010 | An operator without the report permission is refused | auth | P0 | authored |
| E2E-RPT-011 | An operator without the export permission sees no export button | auth | P0 | authored |
| E2E-RPT-012 | Empty period shows the empty state, not an error | happy | P1 | authored |
| E2E-RPT-013 | A failed load shows an error banner | resilience | P1 | authored |
| E2E-RPT-014 | A successful load shows NO error banner | regression | P0 | authored |
| E2E-RPT-015 | Every date reads dd-MM-yyyy Saudi local, never UTC | i18n | P0 | authored |
| E2E-RPT-016 | Arabic RTL render | i18n | P1 | authored |
| E2E-RPT-017 | Paging keeps the totals stable | happy | P1 | authored |
| E2E-RPT-018 | Sorting a column re-queries the server | happy | P2 | authored |
| E2E-RPT-019 | No horizontal overflow at 1280 and 1920 | layout | P1 | authored |
| E2E-RPT-020 | Reports nav group renders without crashing the shell | regression | P0 | authored |

## Scenarios

### E2E-RPT-001 — Hub lists the reports the operator may open

```gherkin
Feature: Reports hub
  As an organiser holding Reports.View
  I want a single place to reach every report
  So that I do not have to remember each route

Scenario: The hub shows a card per permitted report
  Given I am signed in as "superadmin@zagali-ict.com"
  When I open "/admin/reports"
  Then the page title is "Reports"
  And I see a card linking to "/admin/reports/attendance"
  And I see a card linking to "/admin/reports/registrations"
  And I see a card linking to "/admin/reports/gates"
  And each card shows a one-line description of that report
```

### E2E-RPT-002 — Attendance report renders rows and totals

```gherkin
Scenario: The attendance report loads
  Given I am signed in as "superadmin@zagali-ict.com"
  When I open "/admin/reports/attendance"
  Then the grid shows columns "Code", "Session", "Hall", "Start", "Attendees", "Inside now"
  And the totals row shows "Sessions", "Distinct attendees" and "Inside now"
  And the "Sessions" total equals the grid's reported total row count
```

### E2E-RPT-005 — The last day of the range is included

```gherkin
Feature: Inclusive Saudi date range
  The To date is the last day the operator wants INCLUDED. Instants are stored
  as UTC, so the exclusive bound must be the start of the day AFTER To.

Scenario: A session on the To day appears
  Given a session exists starting at 12:00 Riyadh on "23-11-2026"
  And I am signed in as "superadmin@zagali-ict.com"
  When I open "/admin/reports/attendance"
  And I set From to "23-11-2026" and To to "23-11-2026"
  And I press "Apply"
  Then the grid contains that session
  And its "Start" cell reads "23-11-2026 12:00 PM"

Scenario: A session late on the To evening still appears
  Given a session exists starting at 23:30 Riyadh on "23-11-2026"
  When I filter From "23-11-2026" To "23-11-2026"
  Then the grid contains that session
  # Stored as 20:30 UTC on the same day. A naive UTC-date filter would keep it,
  # but an exclusive bound of "To 00:00" would drop it.

Scenario: A session just after midnight the next day does not appear
  Given a session exists starting at 00:30 Riyadh on "24-11-2026"
  When I filter From "23-11-2026" To "23-11-2026"
  Then the grid does not contain that session
  # Stored as 21:30 UTC on 23-11: it is UTC-23rd but Saudi-24th.
```

### E2E-RPT-006 — An inverted range is refused before it queries

```gherkin
Scenario: From after To is rejected with a message
  Given I am on "/admin/reports/attendance"
  When I set From to "25-11-2026" and To to "23-11-2026"
  Then I see the message "The start date is after the end date."
  And pressing "Apply" does not clear the grid
  # An inverted range would return zero rows, which reads as "no data" rather
  # than "you picked the dates backwards".
```

### E2E-RPT-008 — Export downloads a valid XLSX

```gherkin
Scenario: The export button downloads a workbook
  Given I am on "/admin/reports/gates"
  When I press "Export to Excel"
  Then the response status is 200
  And the content type is "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
  And the first two bytes are 0x50 0x4B
  And the Content-Disposition is an attachment
```

### E2E-RPT-009 — Export file name carries a Saudi-local stamp

```gherkin
Scenario: The download is dated in Riyadh time
  Given the Riyadh wall clock reads 2026-07-30 16:08
  When I export the gate-activity report
  Then the file name is "simf-gate-activity-20260730-160837.xlsx"
  # Not the UTC 13:08. An operator exporting at 1am Riyadh must not receive a
  # file dated the previous day (D-770).
```

### E2E-RPT-010 — An operator without the report permission is refused

```gherkin
Scenario: A restricted role cannot open a report
  Given I am signed in as a role holding Reports.View but NOT Reports.Gates
  When I open "/admin/reports/gates"
  Then I am redirected to "/not-permitted"
  And the hub at "/admin/reports" does not show a Gate activity card
```

### E2E-RPT-011 — An operator without the export permission sees no export button

```gherkin
Scenario: Viewing does not imply exporting
  Given I am signed in as a role holding Reports.Attendance but NOT Reports.Export
  When I open "/admin/reports/attendance"
  Then the grid renders
  And there is no "Export to Excel" button
  And POSTing to "/account/api/admin/reports/attendance/export" returns 403
```

### E2E-RPT-014 — A successful load shows NO error banner

```gherkin
Feature: The error banner means something
  Regression: `Error="Error"` bound the LITERAL string, so every report carried
  a permanent empty red banner. An always-on banner trains the operator to
  ignore the one place a real failure appears.

Scenario: A healthy report has no banner
  Given I am on "/admin/reports/attendance"
  When the report finishes loading
  Then there are zero elements matching ".simf-alert--error"
  And the browser console has zero errors
```

### E2E-RPT-015 — Every date reads dd-MM-yyyy Saudi local, never UTC

```gherkin
Scenario: Dates are Saudi wall clock
  Given a session starts at 01:00 Riyadh on "24-11-2026"
  When I open "/admin/reports/attendance"
  Then its "Start" cell reads "24-11-2026 01:00 AM"
  And no cell anywhere on the page ends with "Z"
  And no cell renders a "+00:00" offset
```

### E2E-RPT-016 — Arabic RTL render

```gherkin
Scenario: The report mirrors in Arabic
  Given I switch the culture to "ar"
  When I open "/admin/reports/attendance"
  Then the document direction is "rtl"
  And the banner reads "تقرير الحضور"
  And the range labels read "من" and "إلى"
  And the export button reads "تصدير إلى إكسل"
  And the totals read "الجلسات", "الحضور الفعليون", "الموجودون الآن"
  And the dates still read dd-MM-yyyy with Latin digits
  And the page does not scroll horizontally
```

### E2E-RPT-017 — Paging keeps the totals stable

```gherkin
Scenario: The header figures describe the whole filtered set
  Given a filtered report with 60 sessions and a page size of 20
  When I note the "Sessions" total
  And I move to page 2
  Then the "Sessions" total is unchanged
  # Totals that moved with the page would be worse than no totals.
```

### E2E-RPT-020 — Reports nav group renders without crashing the shell

```gherkin
Feature: An unknown icon name blanks the whole Control Panel
  Regression: SimfIcon THROWS on an unknown name, and the sidebar renders it,
  so one bad name took down the entire Blazor circuit and served a blank page.

Scenario: The shell renders with the Reports group present
  Given I am signed in
  When I open any Control Panel page
  Then the sidebar shows a "Reports" group
  And the page body is not empty
  And the console shows no "Unknown SimfIcon name" error
```

### E2E-RPT-012 / 013 / 019 — Empty, failed and layout

```gherkin
Scenario: A period with no records
  When I filter to a period with no records
  Then I see "No records match this period."
  And there is no error banner

Scenario: The API is unavailable
  Given the reporting endpoint returns 500
  When I open "/admin/reports/attendance"
  Then I see "The report could not be loaded. Try again, or narrow the date range."

Scenario: No horizontal overflow
  When I open each report at 1280px and at 1920px
  Then document.scrollWidth equals document.clientWidth
```
