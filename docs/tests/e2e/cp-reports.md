# E2E test catalogue — `Reports` (`/admin/reports*`)

| | |
|--|--|
| **Page** | [`reports.md`](../../pages/cp/reports.md) |
| **Routes** | `/admin/reports`, and `/admin/reports/{attendance, registrations, gates, sessions, ratings, partners, meetings, engagement}` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via `Get-Totp` helper |
| **Last reviewed** | 2026-08-10 |

> **Execution record — 2026-08-10, local dev (API 5175 + CP 5158), Chrome
> DevTools MCP, signed in as `superadmin@simrsnf.com`.**
>
> | | |
> |--|--|
> | **Passed against live data** | 001, 003, 006, 007, 012, 014, 015, 018 (mechanism), 019, 021, 022, 027 (empty-average case), 031, 033 (partners), 034 |
> | **Structure passed, data blocked** | 024, 025, 026, 028, 029, 030 — columns, sortable columns, totals labels, empty state and RTL all verified; the row and figure assertions need seeded meetings / ratings / questions, of which the dev DB has **none** |
> | **Not run** | 032 (needs the restricted-role fixture — the seeded super-admin holds `*`, which satisfies every gate, so running it as super-admin would prove nothing), 013 (needs a forced 500), 002/004/005/008/009/010/011/016/017/020 (pre-existing Wave B scenarios, not re-run in this pass) |
>
> Zero console errors or warnings across every page visited. Zero error banners
> on every healthy load. Zero horizontal overflow at 1280 and 1920, in RTL, with
> a full 20-row grid.
>
> Three corrections were folded back into this file **because** the run found
> them: the hub count needed scoping to `<main>`, partners ignores the date
> range, and engagement's workbook renames its first column. An unexecuted
> catalogue would have kept all three wrong.

> **Scope note (2026-08-10).** This file covered only the hub plus attendance,
> registrations and gate activity — the Wave B reports. Wave C shipped
> sessions, ratings, partners, meetings and engagement **without** extending the
> catalogue, so partners and meetings had no scenario of any kind. E2E-RPT-021
> to 034 close that. The header row in `README.md` also advertised
> `E2E-RPT-001..025` when only 001..020 existed; five of those ids were never
> written.

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
| E2E-RPT-018 | Sorting a column re-queries the server. **The date column must sort in the direction its arrow shows** on all four of registrations (`registered`), gate activity (`scanned`), ratings (`submitted`) and engagement (`asked`) — each fell through to a default that reads the direction inverted, so `aria-sort="ascending"` sat over newest-first rows | happy | P1 | authored ✓ (`ReportingTests.Registrations_sort_on_registered_follows_the_arrow`, `Gate_activity_sorts_on_scanned_following_the_arrow`, `Ratings_sort_on_submitted_following_the_arrow`, `Engagement_sorts_on_asked_following_the_arrow`) |
| E2E-RPT-019 | No horizontal overflow at 1280 and 1920 | layout | P1 | authored |
| E2E-RPT-020 | Reports nav group renders without crashing the shell | regression | P0 | authored |
| E2E-RPT-021 | Partners report flattens exhibitors, sponsors and booths into one directory | happy | P0 | authored |
| E2E-RPT-022 | Partners totals split by kind and reconcile to the partner total | happy | P0 | authored |
| E2E-RPT-023 | A partner with no tier, email, phone or website still renders | boundary | P1 | authored |
| E2E-RPT-024 | Meetings report flattens speaker and delegation requests | happy | P0 | authored |
| E2E-RPT-025 | Meetings totals count pending and checked-in | happy | P0 | authored |
| E2E-RPT-026 | Ratings report renders stars, scope and comment | happy | P0 | authored |
| E2E-RPT-027 | Average rating is one decimal, and blank when there is nothing to average | boundary | P0 | authored |
| E2E-RPT-028 | A rating with no stars and no comment does not break the row | boundary | P1 | authored |
| E2E-RPT-029 | Engagement report renders questions with recipient, status and phase | happy | P0 | authored |
| E2E-RPT-030 | A hidden question is counted and still listed | boundary | P0 | authored |
| E2E-RPT-031 | The export is a deliberate superset of the grid | happy | P0 | authored |
| E2E-RPT-032 | Each Wave C report refuses an operator lacking its own permission | auth | P0 | authored |
| E2E-RPT-033 | Every Wave C export is Saudi-stamped and formula-safe | i18n | P0 | authored |
| E2E-RPT-034 | Every Wave C report renders in Arabic RTL without overflow | i18n | P1 | authored |

## Scenarios

### E2E-RPT-001 — Hub lists the reports the operator may open

```gherkin
Feature: Reports hub
  As an organiser holding Reports.View
  I want a single place to reach every report
  So that I do not have to remember each route

Scenario: The hub shows a card per permitted report
  Given I am signed in as "superadmin@simrsnf.com"
  When I open "/admin/reports"
  Then the page title is "Reports"
  And I see exactly 8 report cards WITHIN <main>
  # Scope to <main>. The sidebar renders the same eight routes, so an unscoped
  # a[href^="/admin/reports/"] returns 16 and the count assertion fails against
  # a correct page. Confirmed on 2026-08-10.
  And I see a card linking to "/admin/reports/attendance"
  And I see a card linking to "/admin/reports/registrations"
  And I see a card linking to "/admin/reports/gates"
  And I see a card linking to "/admin/reports/sessions"
  And I see a card linking to "/admin/reports/ratings"
  And I see a card linking to "/admin/reports/partners"
  And I see a card linking to "/admin/reports/meetings"
  And I see a card linking to "/admin/reports/engagement"
  And each card shows a one-line description of that report
  # The count is asserted, not just the presence of each link. This scenario
  # previously named only the first three, so the five Wave C reports could have
  # vanished from the hub with every step still passing.
```

### E2E-RPT-002 — Attendance report renders rows and totals

```gherkin
Scenario: The attendance report loads
  Given I am signed in as "superadmin@simrsnf.com"
  When I open "/admin/reports/attendance"
  Then the grid shows columns "Code", "Session", "Hall", "Start", "Attendees", "Inside now"
  And the totals row shows "Sessions", "Distinct attendees" and "Inside now"
  And the "Sessions" total equals the grid's reported total row count
```

### E2E-RPT-003 — Registrations report renders rows and totals

```gherkin
Scenario: The registrations report loads
  Given I am signed in as "superadmin@simrsnf.com"
  When I open "/admin/reports/registrations"
  Then the grid shows columns "Name", "Email", "Profile type",
       "Account state", "Registered"
  And the totals row shows "Registrations", "Approved" and "Pending"
  And Approved + Pending is not greater than Registrations
  # Not equal: an account can be in a state that is neither, so asserting
  # equality would fail on the first rejected or suspended account.
  And every "Registered" cell reads dd-MM-yyyy Saudi local
```

### E2E-RPT-004 — Gate report renders allowed and denied scans

```gherkin
Scenario: Both outcomes appear with the denial reason
  Given a visitor was admitted at gate "North" at 08:12 Riyadh
  And a visitor was denied at gate "North" for "Badge not active"
  And I am signed in as "superadmin@simrsnf.com"
  When I open "/admin/reports/gates"
  Then the grid shows columns "Gate", "Scanned", "Visitor", "Profile type",
       "Direction", "Outcome", "Denial reason"
  And a row reads Outcome "Allowed" with an empty "Denial reason"
  And a row reads Outcome "Denied" with "Denial reason" "Badge not active"
  And the totals show "Scans", "Allowed", "Denied" and "Distinct admitted"
  And Allowed + Denied equals Scans

Scenario: Distinct admitted counts a repeat visitor once
  Given one visitor was admitted three times at different gates
  Then "Scans" counts 3
  And "Distinct admitted" counts 1
  # The two figures answer different questions - how busy were the gates, and
  # how many people got in. Conflating them overstates attendance threefold.
```

### E2E-RPT-007 — Clearing the range restores the unbounded report

```gherkin
Scenario: Clear removes both bounds, it does not reset to today
  Given I am on "/admin/reports/registrations"
  And I have applied From "23-11-2026" and To "23-11-2026"
  And the grid shows fewer rows than the unfiltered report
  When I press "Clear"
  Then the From and To fields are empty
  And the grid shows every registration again
  And the totals match the unfiltered totals
  # A Clear that silently reapplied "today" would look identical on a busy day
  # and hide most of the data on a quiet one.
```

### E2E-RPT-005 — The last day of the range is included

```gherkin
Feature: Inclusive Saudi date range
  The To date is the last day the operator wants INCLUDED. Instants are stored
  as UTC, so the exclusive bound must be the start of the day AFTER To.

Scenario: A session on the To day appears
  Given a session exists starting at 12:00 Riyadh on "23-11-2026"
  And I am signed in as "superadmin@simrsnf.com"
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

### E2E-RPT-018 — Sorting re-queries, and the arrow tells the truth

```gherkin
Feature: The sort arrow must match the rows beneath it
  Regression (D-817): four reports declared a sortable date column and then fell
  through to a default ordering that ignored the requested direction. The header
  rendered aria-sort="ascending" while the grid showed newest-first. The rows
  were plausible, so nobody noticed - the arrow was the only thing that said
  otherwise, and it was wrong.

Scenario Outline: The date column sorts in the direction it advertises
  Given I am on "<route>"
  When I click the "<column>" header until aria-sort is "ascending"
  Then the first row holds the OLDEST value in that column
  And the server was re-queried - the ordering is not done in the browser
  When I click it once more so aria-sort is "descending"
  Then the first row holds the NEWEST value

  Examples:
    | route                       | column     |
    | /admin/reports/registrations | Registered |
    | /admin/reports/gates         | Scanned    |
    | /admin/reports/ratings       | Submitted  |
    | /admin/reports/engagement    | Asked      |

# Covered by ReportingTests.Registrations_sort_on_registered_follows_the_arrow,
# Gate_activity_sorts_on_scanned_following_the_arrow,
# Ratings_sort_on_submitted_following_the_arrow and
# Engagement_sorts_on_asked_following_the_arrow.
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

---

## Wave C scenarios — partners, meetings, ratings, engagement

These five reports shipped after the scenarios above were written. Each of the
four below is driven at its own route with its own permission; the sessions
report is already covered by E2E-RPT-018's sort assertions and the shared
range / export / empty / error scenarios, which apply to every report.

> **The partners report ignores the date range — by design.** It is the only one
> of the eight that does; the other seven all resolve the Saudi window through
> `ResolveWindow`. `ReportingService.Partners` documents why: a partner directory
> is a snapshot of who is participating, not a record of events in a period.
>
> **So never demonstrate a range-dependent scenario on partners** — E2E-RPT-005,
> 006's re-query half, 007 and 012 must be driven on a report that actually
> filters (registrations is the cheapest). Setting a range on partners changes
> nothing, which reads as a broken filter rather than an inapplicable one.
> Verified live 2026-08-10: filtering partners to 01-01-2020..02-01-2020 still
> returned all 22. **Reported as a UX defect** — the page renders From / To /
> Apply controls that are inert, with nothing on screen saying so.

### E2E-RPT-021 — Partners report flattens exhibitors, sponsors and booths

```gherkin
Feature: One partner directory
  The Control Panel manages exhibitors, sponsors and booths on three separate
  pages. An organiser chasing a contact should not have to visit three pages and
  merge the results by hand, so this report flattens all three into one list
  with a Kind column.

Scenario: All three kinds appear in one grid
  Given an exhibitor "Red Sea Marine" exists
  And a sponsor "Gulf Defence Systems" at tier "Gold" exists
  And a booth "B-14" exists
  And I am signed in as "superadmin@simrsnf.com"
  When I open "/admin/reports/partners"
  Then the grid shows columns "Kind", "Name", "Tier", "Email", "Phone", "Website"
  And a row reads Kind "Exhibitor" and Name "Red Sea Marine"
  And a row reads Kind "Sponsor" and Name "Gulf Defence Systems" and Tier "Gold"
  And a row reads Kind "Booth" and Name "B-14"
  And the "Kind" and "Name" columns are sortable
  # Tier, Email, Phone and Website are NOT sortable - only Kind and Name carry
  # Sortable="true". A test that clicks a Tier header is asserting a control
  # that does not exist.
```

### E2E-RPT-022 — Partners totals split by kind and reconcile

```gherkin
Scenario: The four totals are internally consistent
  Given 3 exhibitors, 2 sponsors and 5 booths exist
  When I open "/admin/reports/partners"
  Then the totals read "Partners" 10, "Exhibitors" 3, "Sponsors" 2, "Booths" 5
  And Exhibitors + Sponsors + Booths equals Partners
  # The reconciliation is the point. Four independently-computed figures that
  # do not add up mean one of the three kind queries has drifted from the
  # combined query, and no single figure on its own would reveal it.

Scenario: The totals describe the filtered set, not the page
  Given 30 partners exist and the page size is 20
  When I move to page 2
  Then all four totals are unchanged
```

### E2E-RPT-023 — A partner with nothing but a name still renders

```gherkin
Feature: Optional contact details
  Tier, ContactEmail, ContactPhone and Website are all nullable on
  PartnersReportRow. A booth typically has none of them.

Scenario: Empty optional fields render as empty cells, not "null"
  Given a booth "B-99" exists with no tier, email, phone or website
  When I open "/admin/reports/partners"
  Then the row for "B-99" renders
  And its "Tier", "Email", "Phone" and "Website" cells are empty
  And no cell anywhere reads "null"
```

### E2E-RPT-024 — Meetings report flattens speaker and delegation requests

```gherkin
Feature: One meetings list
  Speaker meeting requests and delegation meeting requests are separate tables
  with the same operational shape - who asked, of whom, for when, and how it was
  answered - so the report presents them as one list keyed by Kind.

Scenario: Both request kinds appear
  Given a speaker meeting request from "Ahmed Al-Otaibi" to "Dr. Sarah Nasser"
        with subject "Naval logistics briefing" exists
  And a delegation meeting request from "Kuwait Delegation" exists
  When I open "/admin/reports/meetings"
  Then the grid shows columns "Kind", "Requester", "Target", "Subject", "Slot",
       "Status", "Requested"
  And a row reads Requester "Ahmed Al-Otaibi" and Target "Dr. Sarah Nasser"
  And a row reads Requester "Kuwait Delegation"
  And the "Kind" and "Status" columns are sortable
  And the "Slot" and "Requested" cells read dd-MM-yyyy Saudi local
```

### E2E-RPT-025 — Meetings totals count pending and checked-in

```gherkin
Scenario: Pending and checked-in are counted across both kinds
  Given 8 meeting requests exist, of which 3 are Pending and 4 are checked in
  When I open "/admin/reports/meetings"
  Then the totals read "Meeting requests" 8, "Pending" 3, "Checked in" 4
  # "Checked in" counts MeetingsReportRow.CheckedIn, which has NO grid column -
  # it is visible only in this total and in the export. Assert it here, because
  # nothing on the page would show the figure was wrong.
```

### E2E-RPT-026 — Ratings report renders stars, scope and comment

```gherkin
Scenario: A submitted rating appears with its scope and text
  Given a rating of type "Session feedback" scoped to "Session"
        with 4 stars and the comment "Well paced, good Q&A" exists
  And I am signed in as "superadmin@simrsnf.com"
  When I open "/admin/reports/ratings"
  Then the grid shows columns "Rating type", "Scope", "Stars", "Comment",
       "Submitted"
  And a row reads Rating type "Session feedback", Scope "Session", Stars "4"
  And its Comment cell reads "Well paced, good Q&A"
  And the "Rating type", "Stars" and "Submitted" columns are sortable
```

### E2E-RPT-027 — Average rating formatting and the empty case

```gherkin
Feature: The average is formatted, and absent when undefined
  ReportingService.Ratings formats the average as "0.0" with the invariant
  culture, and emits an EMPTY STRING when there is nothing to average.

Scenario: The average shows one decimal place
  Given ratings of 5, 4 and 4 stars exist in the period
  When I open "/admin/reports/ratings"
  Then the "Average rating" total reads "4.3"
  And it uses a dot as the decimal separator even when the culture is Arabic
  # Invariant formatting is deliberate. An Arabic culture would otherwise render
  # a decimal comma and the figure would not match the exported workbook.

Scenario: No ratings in the period leaves the average blank
  Given no ratings were submitted in the selected period
  When I apply that period
  Then the "Ratings" total reads "0"
  And the "Average rating" total is empty
  And it does NOT read "0.0"
  # An average of zero would assert that everyone rated the event 0 stars.
  # Blank is the honest rendering of "undefined".
```

### E2E-RPT-028 — A rating with no stars and no comment

```gherkin
Scenario: Null stars and null comment render as empty cells
  Given a rating exists with no overall star value and no comment
  When I open "/admin/reports/ratings"
  Then that row renders
  And its "Stars" and "Comment" cells are empty
  And the "With a comment" total does NOT count it
```

### E2E-RPT-029 — Engagement report renders questions

```gherkin
Scenario: An audience question appears with its routing state
  Given a question "How is the fleet maintained in winter?" was asked in
        session "S-204" addressed to "Speaker"
  And I am signed in as "superadmin@simrsnf.com"
  When I open "/admin/reports/engagement"
  Then the grid shows columns "Code", "Session", "Question", "Recipient",
       "Status", "Phase", "Asked"
  And a row reads Code "S-204" and Question
      "How is the fleet maintained in winter?"
  And its "Asked" cell reads dd-MM-yyyy Saudi local
  And the "Session", "Status" and "Asked" columns are sortable
```

### E2E-RPT-030 — A hidden question is counted and still listed

```gherkin
Feature: Moderation is visible to the organiser
  A question hidden by a moderator is still a question that was asked. The
  report is the organiser's record of engagement, not the audience's view, so
  hiding must not remove it from the list.

Scenario: Hiding does not remove the row
  Given 12 questions were asked, of which 2 are hidden and 5 were pushed
        to the speaker
  When I open "/admin/reports/engagement"
  Then the totals read "Questions" 12, "Hidden" 2, "Pushed to speaker" 5
  And both hidden questions still appear as rows
  # EngagementReportRow.IsHidden has NO grid column, exactly like the meetings
  # CheckedIn field: the total and the export are the only places it surfaces.
```

### E2E-RPT-031 — The export is a deliberate superset of the grid

```gherkin
Feature: The workbook carries more than the screen
  Each Wave C export adds columns the grid omits. This is deliberate - the grid
  is for reading, the workbook is for analysis - so the difference is asserted
  rather than left to look like a bug.

Scenario Outline: Every export adds its extra columns
  When I export the <report> report
  Then the workbook header row contains <extra>
  And those columns are absent from the on-screen grid

  Examples:
    | report     | extra                          |
    | partners   | "Name (Arabic)" and "Active"   |
    | meetings   | "Checked in"                   |
    | engagement | "Hidden"                       |
    | ratings    | "Target"                       |

# All four verified live on 2026-08-10 by unzipping the downloaded workbook and
# reading xl/sharedStrings.xml. Note when writing a parser: ClosedXML emits
# NAMESPACED tags (<x:t>, not <t>), so an un-namespaced regex silently matches
# nothing and looks like an empty workbook.

Scenario: Engagement's first column is renamed in the workbook
  When I export the engagement report
  Then the workbook's first header reads "Session code"
  And the on-screen grid's first header reads "Code"
  # Not a missing column - the same field under a longer label, which has room
  # in a spreadsheet and not in a grid. Asserted so the difference is not
  # "fixed" into a mismatch later.

Scenario: The workbook row count matches the filtered total
  Given the registrations report shows a "Registrations" total of 8
  When I export it with the same range applied
  Then the workbook has 8 data rows beneath the header
  # Driven on registrations, NOT partners - partners ignores the range by
  # design (see the note above), so it cannot demonstrate that the export
  # honours the same filter as the grid.
```

### E2E-RPT-032 — Each Wave C report enforces its own permission

```gherkin
Feature: One permission per report
  Reports.View admits an operator to the hub only. Each report carries its own
  code, so a partner-liaison role can be given partners without also seeing
  ratings or audience questions.

Scenario Outline: A missing per-report permission is refused
  Given I am signed in as a role holding Reports.View but NOT <permission>
  When I open "<route>"
  Then I am redirected to "/not-permitted"
  And the hub does not show a card linking to "<route>"
  And POSTing to "/account/api<route>/list" returns 403

  Examples:
    | route                       | permission           |
    | /admin/reports/partners     | Reports.Partners     |
    | /admin/reports/meetings     | Reports.Meetings     |
    | /admin/reports/ratings      | Reports.Ratings      |
    | /admin/reports/engagement   | Reports.Engagement   |
    | /admin/reports/sessions     | Reports.Sessions     |

Scenario: Viewing a report does not imply exporting it
  Given I am signed in as a role holding Reports.Partners but NOT Reports.Export
  When I open "/admin/reports/partners"
  Then the grid renders
  And there is no "Export to Excel" button
  And POSTing to "/account/api/admin/reports/partners/export" returns 403
  # Every export endpoint gates on the single Reports.Export code, NOT on the
  # per-report code - verified in ReportingEndpoints. Granting a report does
  # not grant its download.
```

### E2E-RPT-033 — Wave C exports are Saudi-stamped and formula-safe

```gherkin
Scenario Outline: The download name carries the Riyadh wall clock
  Given the Riyadh wall clock reads 2026-08-10 09:15:22
  When I export the <report> report
  Then the file name is "simf-<slug>-20260810-091522.xlsx"
  And the content type is
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
  And the first two bytes are 0x50 0x4B

  Examples:
    | report     | slug       |
    | partners   | partners   |
    | meetings   | meetings   |
    | ratings    | ratings    |
    | engagement | engagement |

Scenario: A question beginning with = is neutralised in the workbook
  Given an audience question was asked reading "=cmd|'/c calc'!A1"
  When I export the engagement report
  And I open the workbook
  Then that cell is stored as text, not as a formula
  And opening the file executes nothing
  # CWE-1236. The guard lives in the shared ClosedXmlGridExcelExporter, so this
  # holds for every report - but engagement is the one whose cells are free
  # text typed by an attendee, so it is the realistic attack path.
```

### E2E-RPT-034 — Wave C reports in Arabic RTL

```gherkin
Scenario Outline: Each report mirrors correctly
  Given I switch the culture to "ar"
  When I open "<route>"
  Then the document direction is "rtl"
  And the banner reads "<title>"
  And the range labels read "من" and "إلى"
  And the export button reads "تصدير إلى إكسل"
  And the totals read <totals>
  And every date still reads dd-MM-yyyy with Latin digits
  And document.scrollWidth equals document.clientWidth

  Examples:
    | route                     | title            | totals                                             |
    | /admin/reports/partners   | تقرير الشركاء    | "الشركاء", "العارضون", "الرعاة", "الأجنحة"          |
    | /admin/reports/meetings   | تقرير الاجتماعات | "طلبات الاجتماعات", "قيد الانتظار", "تم تسجيل حضورهم" |
    | /admin/reports/ratings    | تقرير التقييمات  | "التقييمات", "متوسط التقييم", "مع تعليق"            |
    | /admin/reports/engagement | تقرير التفاعل    | "الأسئلة", "المخفية", "المرسلة للمتحدث"             |
```
