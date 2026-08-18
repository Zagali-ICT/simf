# E2E test catalogue — Gates operations dashboard (`/admin/gates/dashboard`)

| | |
|--|--|
| **Page** | [`cp/admin-gates-dashboard.md`](../../pages/cp/admin-gates-dashboard.md) |
| **Route** | `/admin/gates/dashboard` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-08-18 (both gate reports moved onto the shared grid seam) |

> **Page nature.** This is a **read-only** operations dashboard (D-199). It has
> NO create / edit / delete — gate CRUD lives at `/admin/gates` and check-in /
> check-out happen at the operator console `/admin/gates/operator`. The whole
> surface is: one **Refresh** button, two **stat cards** (Currently inside,
> Gates), and two read-only tables (the *Currently inside* roster and the
> *Gates* roster). It consumes exactly two BFF passthroughs (D-148), both
> fired once on first interactive render:
> - `POST /account/api/admin/gates/reports/currently-inside/list` with `{ "Top": 200 }`
>   → `ApiResult<GridPage<AdminCurrentlyInsideRow>>`
> - `POST /account/api/admin/gates/list` with `{ "Top": 200 }`
>   → `ApiResult<GridPage<AdminGateSummary>>`
>
> **Both reports are server-paged on the shared grid seam.** The occupancy report
> used to be an unpaged `GET` that returned every visitor inside the venue in one
> array. It is now `POST {resource}/list` binding a `GridQuery`, so the page asks
> for a window and reads the occupancy figure from `data.total`, never from the
> length of `data.items`. Its declared column set is deliberately narrow:
> `gateId` and `scannedAt`, natural order `scannedAt` descending, page size
> falling back to 25 and capped at 200, and no searchable column at all.
> Everything else the table renders (the display name, the Arabic name, the
> profile type) is resolved after the page is chosen, some of it out of the other
> database, so naming one of those as a sort or filter key is a 400 rather than a
> sort that quietly does nothing.
>
> **The sibling scan report is covered here too, at the API layer.**
> `POST /admin/gates/reports/scans/list` and its export
> `POST /admin/gates/reports/scans.xlsx` share this page's `Gates.Manage` gate and
> read the same `GateScans` table, but no Control Panel page renders them yet, so
> they have no catalogue file of their own. The BFF passthrough
> (`AccountEndpoints.Gates.cs`) and the `SimfAdminClient` call both exist, so the
> scenarios below can be driven through `/account/api/...` exactly as this page's
> own reads are; what does not exist is a page to drive them from. Their declared columns are `gateId`,
> `userProfileId`, `direction`, `outcome`, `denialReasonCode`, `source`,
> `scannedAt`, plus the searchable `qrIdAtScan` and `scannedDisplayName`, with the
> hand-written range filters `scannedFrom` and `scannedTo`. Natural order is
> `scannedAt` descending, page size falls back to 50 and is capped at 200, and the
> export walks the same composed query up to a 10,000-row cap.
>
> Both API endpoints are gated by `PermissionCatalog.Gates.Manage` +
> `RequireApprovedAccount`; the page itself carries
> `@attribute [RequirePermission(PermissionCatalog.Gates.Manage)]`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-GDS-001 | Golden path — page loads, both tables + both stat cards render, counts agree | happy | P0 | _to author_ |
| E2E-GDS-002 | Refresh button re-fires both calls and updates the *Currently inside* count | happy | P0 | _to author_ |
| E2E-GDS-003 | Stat cards read the server's `Total`, so on a set larger than one page they exceed the rendered row count | happy | P1 | _to author_ |
| E2E-GDS-004 | Empty *Currently inside* roster renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-GDS-005 | Empty *Gates* roster renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-GDS-006 | Gate `Active` / `Inactive` pill renders the correct `SimfPill` variant | happy | P1 | _to author_ |
| E2E-GDS-007 | Auth gate — signed-in admin lacking `Gates.Manage` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-GDS-008 | Server 500 on `/currently-inside` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-GDS-009 | Server 500 on `/gates/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-GDS-010 | Loading state — "Loading…" placeholder shows while both calls are in flight | happy | P2 | _to author_ |
| E2E-GDS-011 | RTL / Arabic render mirrors the page, headings, columns and pills | i18n | P1 | _to author_ |
| E2E-GDS-012 | Regression (D-794) — `/currently-inside` returns 200 against the REAL database, not a stubbed one | regression | P0 | 2026-07-29 PASS |
| E2E-GDS-013 | Occupancy report returns one page and a database-side total: 60 people inside, `top` 25 gives 25 rows and `total` 60 | happy | P0 | authored |
| E2E-GDS-014 | Occupancy report refuses an undeclared sort key with 400 `GRID_SORT_KEY_INVALID`, and a search term with 400 `GRID_SEARCH_NOT_SUPPORTED` | validation | P0 | authored |
| E2E-GDS-015 | Occupancy report pages forward: the second window returns the next 25 people, repeating none and dropping none across a `scannedAt` tie | correctness | P0 | authored |
| E2E-GDS-016 | Occupancy report `gateId` filter narrows the set, and `total` reports the filtered count rather than the page or the whole venue | happy | P1 | authored |
| E2E-GDS-017 | Scan report returns one page over the `scannedFrom` / `scannedTo` window with the true count of that window | happy | P0 | authored |
| E2E-GDS-018 | Scan report `scannedTo` is half-open on the following midnight, so a 23:58 scan on the named day is included | correctness | P0 | authored |
| E2E-GDS-019 | Scan report refuses an undeclared sort key (400 `GRID_SORT_KEY_INVALID`) and an undeclared filter key (400 `GRID_FILTER_KEY_INVALID`) | validation | P0 | authored |
| E2E-GDS-020 | Scan report pages forward: page two carries the next 50 scans and repeats no scan from page one | correctness | P1 | authored |
| E2E-GDS-021 | Scan XLSX export carries the same filters, search and sort as the grid it came from, and refuses the same bad keys | happy | P0 | authored |
| E2E-GDS-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-GDS-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-GDS-012 — Regression: the report is translatable (D-794)

> **Why this scenario exists.** E2E-GDS-008 already covered "the server returns
> 500 on `/currently-inside`" — as a *simulated* fault, with a stubbed response.
> Meanwhile the real endpoint returned 500 on **every** request, because its EF
> query could not be translated to SQL, and this dashboard had never worked.
> A resilience scenario that fakes a failure will never notice that the failure
> is permanent. This one calls the live endpoint and asserts success.

```gherkin
Feature: The currently-inside report can actually be produced
  As an Administrator with the Gates.Manage permission
  I want the dashboard's roster call to succeed against the real database
  So that the page shows occupancy instead of an error toast

Background:
  Given the API is reachable and backed by a REAL SQL Server database
  And an Administrator with the Gates.Manage permission has signed in

Scenario: The report succeeds on an empty scan log
  Given the GateScans table contains no rows at all
  When I POST /api/v1/admin/gates/reports/currently-inside/list with { "top": 200 }
  Then the response status is 200
  And the ApiResult envelope reports Success = true
  And "data.items" is empty and "data.total" is 0
  # Not a data assertion by accident: the defect was at query-TRANSLATION time,
  # so it reproduced on an empty table. Seeding could never have masked it.

Scenario: A visitor whose latest allowed scan is a check-in appears
  Given a gate "GCI-1" and an approved visitor with a QR
  And the visitor has one allowed CheckIn scan 10 minutes ago
  When I POST /api/v1/admin/gates/reports/currently-inside/list with { "top": 200 }
  Then the response status is 200
  And the visitor appears exactly once in "data.items"
  And their LastCheckInGateCode is "GCI-1"

Scenario: A later check-out removes the visitor
  Given the visitor has an allowed CheckIn 10 minutes ago
  And the visitor has an allowed CheckOut 5 minutes ago
  When I POST /api/v1/admin/gates/reports/currently-inside/list with { "top": 200 }
  Then the visitor does not appear
  # Seed these rows directly. Posting two scans through the scan endpoint does
  # NOT work: GateOperatorService absorbs a repeat allowed scan inside a
  # 5-second DuplicateWindow (G-5), so the second call writes no row.

Scenario: A check-in older than the presence window is treated as departed
  Given the visitor has one allowed CheckIn 20 hours ago and no later scan
  When I POST /api/v1/admin/gates/reports/currently-inside/list with { "top": 200 }
  Then the visitor does not appear
  # StalePresenceWindow is 16 hours: an in-only gate never emits a CheckOut.

Scenario: Two scans at the same instant still yield one row
  Given the visitor has two allowed CheckIn scans with an identical ScannedAt
  When I POST /api/v1/admin/gates/reports/currently-inside/list with { "top": 200 }
  Then the visitor appears exactly once
```

**Automated by** `tests/SIMF.Api.Tests/AdminGateCurrentlyInsideTests.cs` (5 cases,
all passing 2026-07-29).

### E2E-GDS-001 — Golden path

```gherkin
Feature: Gates operations dashboard golden path
  As an Administrator with the Gates.Manage permission
  I want a live read-only overview of who is inside the venue and the gate roster
  So that I can monitor gate operations without leaving the Control Panel

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Gates.Manage permission has signed in via /login + /login/totp
  And they have landed on /admin/gates/dashboard

Scenario: Dashboard loads, both rosters and both stat cards render
  Given at least one gate "GATE-A" / "بوابة عامة" exists and is active
  And at least one visitor "Sara Al-Otaibi" is currently checked in at "GATE-A"
  When the page completes its first interactive render
  Then a POST /account/api/admin/gates/reports/currently-inside/list with body {"Top":200} fires and returns 200
  And a POST /account/api/admin/gates/list with body {"Top":200} fires and returns 200
  And the SimfBanner title reads "Gates operations dashboard"
  And a "Currently inside" SimfStatCard shows data.total, the count of everyone inside
  And a "Gates" SimfStatCard shows the gates page's data.total
  And the "Currently inside" table shows a row with
      Name="Sara Al-Otaibi", Profile type (the profile-type name or "—"),
      Gate="GATE-A", Entered at formatted as "yyyy-MM-dd HH:mm:ss UTC"
  And the table summary reads "{N} currently inside"
  And the "Gates" table shows a row with Code="GATE-A", Name="GATE-A" and a green "Active" pill
  And the gates table summary reads "{M} gates"
  And no error SimfAlert is shown
```

**Evidence captured:**
- Screenshot before (loading): `docs/screenshots/cp-admin-gates-dashboard-loading.png`
- Screenshot after (both tables populated): `docs/screenshots/cp-admin-gates-dashboard-golden.png`
- Console errors: 0 expected
- Network: `POST /account/api/admin/gates/reports/currently-inside/list` returns 200 and `POST /account/api/admin/gates/list` returns 200
- Audit: none — this is a read-only dashboard (no `RowAudit` / `OperationLog` write).

### E2E-GDS-002 — Refresh re-fires both calls

```gherkin
Scenario: Refresh button reloads both rosters
  Given the dashboard has finished its initial load
  And the "Currently inside" stat card shows {N}
  When a new visitor checks in at the operator console in a separate session
  And the administrator clicks the "Refresh" button
  Then the button shows its loading label "Refreshing…" while the calls are in flight
  And a fresh POST /account/api/admin/gates/reports/currently-inside/list fires and returns 200
  And a fresh POST /account/api/admin/gates/list fires and returns 200
  And the "Currently inside" stat card now shows {N + 1}
  And the new visitor appears as a new row in the "Currently inside" table
  And no error SimfAlert is shown
```

**Evidence captured:**
- Screenshot after refresh: `docs/screenshots/cp-admin-gates-dashboard-refresh.png`
- Network: two new 200s (the second `/currently-inside/list` + `/gates/list` pair)
- Console errors: 0 expected

### E2E-GDS-003 - Stat cards read the server total

```gherkin
Scenario: Stat card values are the server's totals, not the rendered row counts
  Given the dashboard has finished loading
  And the venue holds 40 people inside and 6 gates, both inside the 200-row window
  When the administrator reads the two SimfStatCard values
  Then the "Currently inside" stat card value is 40, taken from data.total
  And the "Gates" stat card value is 6, taken from the gates page's data.total
  And each value equals the number of <tr> rows in its table only because the set
      happens to fit inside one page
  And both counts are rendered with the invariant culture (no thousands separator drift)

Scenario: A venue holding more than one page still reports true occupancy
  Given 260 people are currently inside, above the page's 200-row window
  When the administrator opens /admin/gates/dashboard
  Then the "Currently inside" stat card reads 260, not 200
  And the inside table renders 200 rows
  And the table summary line reads the same 260
  # A stat card reading items.Count is the defect this asserts against. Before the
  # report was paged the two figures could not differ, so nothing on screen
  # distinguished "everyone inside" from "everyone we happened to fetch".
```

### E2E-GDS-004 — Empty *Currently inside* roster

```gherkin
Scenario: No one inside renders the inside empty state
  Given the database has no visitor currently checked in (every check-in has a matching check-out)
  And at least one gate exists
  When the administrator opens /admin/gates/dashboard
  Then the "Currently inside" stat card shows "0"
  And under the "Currently inside" heading a SimfEmptyState renders
  And it shows the bilingual copy "No one is currently inside the venue." / "لا يوجد أحد داخل المعرض حاليًا."
  And the "Gates" table still renders its rows normally
  And no error SimfAlert is shown
```

### E2E-GDS-005 — Empty *Gates* roster

```gherkin
Scenario: No gates configured renders the gates empty state
  Given the database has no gate rows
  When the administrator opens /admin/gates/dashboard
  Then the "Gates" stat card shows "0"
  And under the "Gates" heading a SimfEmptyState renders
  And it shows the bilingual copy "No gates have been configured." / "لم يتم إعداد أي بوابات."
  And no error SimfAlert is shown
```

### E2E-GDS-006 — Active / Inactive pill rendering

```gherkin
Scenario: Gate active state maps to the correct SimfPill variant
  Given a gate "GATE-ON" exists with IsActive = true
  And a gate "GATE-OFF" exists with IsActive = false
  When the administrator opens /admin/gates/dashboard
  Then the "GATE-ON" row shows the SimfPill variant="on" reading "Active"
  And the "GATE-OFF" row shows the SimfPill variant="off" reading "Inactive"
```

### E2E-GDS-007 — Auth gate

```gherkin
Scenario: Admin without the Gates.Manage permission is denied
  Given a signed-in Control Panel user whose role does NOT include PermissionCatalog.Gates.Manage
  And who is NOT the Administrator wildcard ("*")
  When they navigate to /admin/gates/dashboard
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/gates/reports/currently-inside/list request fires
  And no /account/api/admin/gates/list request fires
```

### E2E-GDS-008 — Server 500 on `/currently-inside`

```gherkin
Scenario: API 500 on the inside report shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/gates/reports/currently-inside/list (e.g. DB down)
  And /admin/gates/list still returns 200
  When the administrator opens /admin/gates/dashboard
  Then the inside call envelope is not {Success:true, Data:not null}
  And a red SimfAlert appears reading "Could not load the gates dashboard." / "تعذّر تحميل لوحة البوابات."
  And (the unfilled inside roster shows its empty state)
```

### E2E-GDS-009 — Server 500 on `/gates/list`

```gherkin
Scenario: API 500 on the gates list shows the fallback bilingual toast
  Given /admin/gates/reports/currently-inside/list returns 200
  And the API is configured to return 500 on /admin/gates/list
  When the administrator opens /admin/gates/dashboard
  Then the gates-list call envelope is not {Success:true, Data:not null}
  And a red SimfAlert appears reading "Could not load the gates dashboard." / "تعذّر تحميل لوحة البوابات."
  And the inside roster still renders its rows from the successful call
```

### E2E-GDS-010 — Loading state

```gherkin
Scenario: Loading placeholder shows while both calls are in flight
  Given the two BFF calls are artificially delayed (throttled network)
  When the administrator opens /admin/gates/dashboard
  Then while _loading is true the page shows the "Loading…" / "جارٍ التحميل…" paragraph
  And neither stat card nor either table is rendered yet
  When both calls resolve
  Then the loading paragraph disappears and the stat cards + tables render
```

### E2E-GDS-011 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the dashboard
  Given the administrator is on /admin/gates/dashboard in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "لوحة عمليات البوابات"
  And the Refresh button reads "تحديث"
  And the stat cards read "داخل المعرض حاليًا" and "البوابات"
  And the "Currently inside" heading reads "الموجودون بالداخل حاليًا"
  And the inside table columns read "الاسم" / "نوع الملف" / "البوابة" / "وقت الدخول"
  And the gates table columns read "الرمز" / "الاسم" / "نشطة"
  And the active pill reads "نشطة" and the inactive pill reads "غير نشطة"
  And the Arabic display name (DisplayNameArabic / NameArabic) is used where present, else the English name
  And the layout mirrors right-to-left
```

## Scenarios - the two reports on the grid seam

> These nine are API-layer scenarios, driven with direct HTTP calls rather than a
> browser, because they assert the wire contract of the two reports. The occupancy
> report is the one this page renders; the scan report and its export have no page
> of their own yet and are hosted here beside their sibling. Both carry the same
> `Gates.Manage` gate, so one signed-in administrator drives all nine.

### E2E-GDS-013 - The occupancy report returns one page and a real total

```gherkin
Feature: One page of the occupancy report
  As an Administrator with the Gates.Manage permission
  I want the report to return the window I asked for and the true size of the set
  So that the dashboard can state occupancy without fetching the whole venue

Background:
  Given the API is reachable and backed by a REAL SQL Server database
  And an Administrator with the Gates.Manage permission has signed in
  And 60 approved visitors each have one allowed CheckIn scan inside the
      16-hour presence window, and no later CheckOut

Scenario: The first page carries 25 rows and a total of 60
   When "POST /admin/gates/reports/currently-inside/list" is called with
        """
        { "skip": 0, "top": 25 }
        """
   Then the response is 200 with "success": true
    And "data.items" holds 25 rows
    And "data.total" is 60, counted on the server BEFORE Skip and Take
    And "data.skip" is 0 and "data.top" is 25
    And the rows come back newest check-in first, the natural order scannedAt descending
    And every row carries DisplayName, LastCheckInGateCode and LastCheckInAt

Scenario: Omitting top gives the wire default, and a top of 0 gives the fallback
   When "POST /admin/gates/reports/currently-inside/list" is called with
        """
        { "skip": 0 }
        """
   Then "data.items" holds 20 rows and "data.top" is 20
    And "data.total" is still 60
   When the same call instead sends
        """
        { "skip": 0, "top": 0 }
        """
   Then "data.items" holds 25 rows, this resource's declared fallback
    And "data.total" is still 60
    # The two numbers differ on purpose, and the endpoint shape decides which one a
    # caller can reach. This endpoint binds a GridQuery straight off the body, and
    # GridQuery carries its own default of 20, so a body with no top never reaches
    # the declared fallback: only an explicit top of 0 does. The endpoints that bind
    # a route id alongside the window declare their own Top with no default, and
    # there an omitted top DOES land on the declared fallback. Assert the fallback
    # through top 0, which holds whichever shape the endpoint takes.

Scenario: A top above the cap is clamped, not honoured
   When "POST /admin/gates/reports/currently-inside/list" is called with
        """
        { "skip": 0, "top": 5000 }
        """
   Then "data.items" holds at most 200 rows, the declared maximum
    And "data.total" is still 60
```

**Evidence captured:**
- `data.total` is compared against a separate count of visitors whose latest
  allowed scan is a CheckIn. A total equal to `data.items` length on a 60-row set
  is the defect this asserts against: it means the count was taken after paging.
- Console errors: 0 expected. Network failures: 0 expected.

### E2E-GDS-014 - The occupancy report refuses keys it does not declare

```gherkin
Scenario: A sort on a resolved field is a 400, not a quiet no-op
   When "POST /admin/gates/reports/currently-inside/list" is called with
        """
        { "sort": "displayName" }
        """
   Then the response is 400
    And "error.code" is "GRID_SORT_KEY_INVALID"
    And "error.message" begins "'displayName' is not a sortable column on this list."
    And the message goes on to name the two columns that ARE sortable,
        gateId and scannedAt
    # displayName is exactly the trap: the CP renders that column, so it is the
    # first key a caller reaches for, and it is resolved AFTER the page is chosen
    # (some of it from the Identity database). Sorting on it can never work, so it
    # has to fail loudly rather than return the same 25 rows in the same order.

Scenario: A filter on a resolved field is a 400 as well
   When "POST /admin/gates/reports/currently-inside/list" is called with
        """
        { "filters": { "profileTypeName": "VIP" } }
        """
   Then the response is 400
    And "error.code" is "GRID_FILTER_KEY_INVALID"
    And no rows are returned
    # A filter that is ignored returns EVERY visitor inside the venue to a caller
    # who asked for one profile type. On an admin report over people that is a
    # disclosure, which is why an unknown filter key can never be skipped.

Scenario: A search term is refused because the report declares no searchable column
   When "POST /admin/gates/reports/currently-inside/list" is called with
        """
        { "search": "Sara" }
        """
   Then the response is 400
    And "error.code" is "GRID_SEARCH_NOT_SUPPORTED"
    And the caller is told this list has no searchable column,
        rather than being handed the unfiltered set
```

### E2E-GDS-015 - The occupancy report pages forward without repeating a row

```gherkin
Scenario: The second window carries the next 25 people
  Given the 60 visitors inside include four whose CheckIn scans share one
        identical ScannedAt, so the sort column ties
   When "POST /admin/gates/reports/currently-inside/list" is called with
        """
        { "skip": 0, "top": 25 }
        """
    And the same call is repeated with
        """
        { "skip": 25, "top": 25 }
        """
    And once more with
        """
        { "skip": 50, "top": 25 }
        """
   Then the three pages hold 25, 25 and 10 rows
    And "data.total" is 60 on all three
    And the union of the three pages is exactly the 60 visitors inside,
        each appearing exactly once
    And no visitor appears on two pages and none is skipped between them
    # The Id tiebreak is what makes this true. Without it, two rows sharing a
    # ScannedAt have no defined order, and SQL Server is free to return one of
    # them on both page one and page two while the other is never returned at all.
```

### E2E-GDS-016 - A gateId filter narrows the report and the total follows

```gherkin
Scenario: Filtering by gate reports the filtered count, not the venue count
  Given gate "GATE-A" holds 45 of the 60 people inside
    And gate "GATE-B" holds the other 15
   When "POST /admin/gates/reports/currently-inside/list" is called with
        """
        { "skip": 0, "top": 25, "filters": { "gateId": "{the GATE-B id}" } }
        """
   Then the response is 200
    And "data.items" holds 15 rows, every one of them last seen at "GATE-B"
    And "data.total" is 15, the size of the FILTERED set
    And "data.total" is not 60 (the unfiltered venue) and not 25 (the page window)

Scenario: A gate with nobody inside returns an empty page, not an error
   When the same call names a gate at which nobody is currently inside
   Then the response is 200 with "success": true
    And "data.items" is empty and "data.total" is 0

Scenario: A gateId that is not a Guid is refused
   When "POST /admin/gates/reports/currently-inside/list" is called with
        """
        { "filters": { "gateId": "not-a-guid" } }
        """
   Then the response is 400
    And "error.code" is "GRID_FILTER_VALUE_INVALID"
    And the message names the key gateId and the value it could not parse
```

### E2E-GDS-017 - The scan report returns one page of a date window

```gherkin
Feature: One page of the gate scan report
  As an Administrator with the Gates.Manage permission
  I want the scan log paged and its window counted on the server
  So that reading a busy day does not materialise the whole scan table

Background:
  Given an Administrator with the Gates.Manage permission has signed in
  And the GateScans table holds 120 scans dated 2026-08-16 (Riyadh)
  And 40 further scans dated 2026-08-17 (Riyadh)

Scenario: The window is filtered, ordered and counted server-side
   When "POST /admin/gates/reports/scans/list" is called with
        """
        {
          "skip": 0,
          "top": 50,
          "filters": { "scannedFrom": "2026-08-16", "scannedTo": "2026-08-16" }
        }
        """
   Then the response is 200 with "success": true
    And "data.items" holds 50 rows
    And "data.total" is 120, the size of the day's window, not the page
    And the rows come back newest scan first, the natural order scannedAt descending
    And every row carries GateCode, Direction, Outcome and ScannedAt
    And no row dated 2026-08-17 appears

Scenario: The outcome filter parses by enum NAME
   When the same call adds "outcome": "Denied" to its filters
   Then only denied scans come back
    And "data.total" is the count of denied scans in the window
   When the call instead sends "outcome": "1"
   Then the response is 400 with "error.code" "GRID_FILTER_VALUE_INVALID"
    # An enum filter parsed by ordinal silently re-points at a different member
    # the day someone appends a value, so only the name is accepted.

Scenario: The searchable columns are the scan's own snapshot fields
   When "POST /admin/gates/reports/scans/list" is called with
        """
        { "search": "Sara" }
        """
   Then the response is 200
    And only scans whose QrIdAtScan or ScannedDisplayName contain "Sara" come back
    And "data.total" is the count of those scans
    # Those two are snapshots written onto the scan row itself, so the search is a
    # server-side WHERE rather than a second-database round trip for every page.
```

### E2E-GDS-018 - scannedTo includes the whole day it names

```gherkin
Scenario: A late-evening scan on the last day of the window is returned
  Given one allowed scan at 23:58 Riyadh on 2026-08-16
    And one allowed scan at 00:04 Riyadh on 2026-08-17
   When "POST /admin/gates/reports/scans/list" is called with
        """
        { "filters": { "scannedFrom": "2026-08-16", "scannedTo": "2026-08-16" } }
        """
   Then the 23:58 scan IS returned
    And the 00:04 scan is NOT returned
    And "data.total" counts the 23:58 scan
    # scannedTo is half-open on the FOLLOWING midnight, so the day it names is
    # included whole. The bespoke filter this replaced compared "<= ToUtc" against
    # a bare date, which resolved to midnight and silently dropped every scan
    # after 00:00 on the last day of the window: a report that lost most of the
    # day an operator asked about, without saying so.

Scenario: Both ends are Saudi wall-clock days, not UTC instants
  Given one allowed scan at 01:30 Riyadh on 2026-08-16, which is 22:30 UTC on 2026-08-15
   When the window names scannedFrom 2026-08-16 and scannedTo 2026-08-16
   Then that scan IS returned, because the day boundaries are Riyadh days

Scenario: An unparseable day is a 400
   When "POST /admin/gates/reports/scans/list" is called with
        """
        { "filters": { "scannedFrom": "yesterday" } }
        """
   Then the response is 400
    And "error.code" is "GRID_FILTER_VALUE_INVALID"
    And the message names scannedFrom and the value it could not read as a day
```

### E2E-GDS-019 - The scan report refuses keys it does not declare

```gherkin
Scenario: An undeclared sort key is a 400 that lists the real ones
   When "POST /admin/gates/reports/scans/list" is called with
        """
        { "sort": "gateCode" }
        """
   Then the response is 400
    And "error.code" is "GRID_SORT_KEY_INVALID"
    And the message names gateCode and then lists the sortable columns:
        gateId, userProfileId, direction, outcome, denialReasonCode, source,
        scannedAt, qrIdAtScan, scannedDisplayName
    # gateCode is the column the report RENDERS; it is joined from Gates after the
    # page is chosen, so it is not a key. The old bespoke filter had no sort input
    # at all: the order was hard-coded, and anything a caller sent was discarded.

Scenario: An undeclared filter key is a 400
   When "POST /admin/gates/reports/scans/list" is called with
        """
        { "filters": { "hallId": "{a hall id}" } }
        """
   Then the response is 400
    And "error.code" is "GRID_FILTER_KEY_INVALID"
    And no rows are returned

Scenario: A filter key in the wrong case still binds and still filters
   When the call sends "SCANNEDFROM" instead of "scannedFrom"
   Then the response is 200 and the window is applied
    # Case-insensitive on purpose. A key that binds in one casing and is ignored in
    # another is the widened-result-set failure wearing a different hat.
```

### E2E-GDS-020 - The scan report pages forward through the window

```gherkin
Scenario: Page two carries the next 50 scans of the same window
  Given the 120 scans of 2026-08-16 include a burst of six recorded within the
        same second, so the sort column ties
   When "POST /admin/gates/reports/scans/list" is called with skip 0, top 50 and
        the same scannedFrom / scannedTo window of 2026-08-16 as E2E-GDS-017
    And the call is repeated with skip 50, and once more with skip 100, each
        carrying that same window
   Then the three pages hold 50, 50 and 20 rows
    And "data.total" is 120 on all three
    And the union of the three pages is exactly the 120 scans, each appearing once
    And no scan appears on two pages and none is skipped between them

Scenario: The same window walked oldest first
   When the three calls carry that same 2026-08-16 window plus "sort": "scannedAt"
        and "sortDescending": false
   Then the three pages hold 50, 50 and 20 rows, oldest scan first
    And their union is again exactly the 120 scans, each appearing once
    # The sort key has to be named. sortDescending on its own is ignored, because
    # composition reads the direction off the named column and falls through to the
    # declared natural order when no column is named. A scenario that sent only
    # "sortDescending": false would assert nothing: it would get the same
    # newest-first page as the scenario above and still pass.
```

### E2E-GDS-021 - The XLSX export carries the grid's own filters

```gherkin
Feature: An export that cannot disagree with the grid it came from
  As an Administrator with the Gates.Manage permission
  I want the workbook to hold exactly the rows the report showed me
  So that the file I send on is the report I read

Background:
  Given the GateScans table holds 120 scans dated 2026-08-16 (Riyadh)
    And 40 further scans dated 2026-08-17 (Riyadh)
    And 30 of the 2026-08-16 scans were denied

Scenario: The same body produces the same set of rows
   When "POST /admin/gates/reports/scans/list" is called with
        """
        {
          "skip": 0,
          "top": 50,
          "filters": { "scannedFrom": "2026-08-16", "scannedTo": "2026-08-16",
                       "outcome": "Denied" }
        }
        """
    And "POST /admin/gates/reports/scans.xlsx" is called with THAT SAME body
   Then the grid answers "data.total" 30
    And the workbook's "Scans" sheet holds 30 data rows under its header row
    And that header row reads
        Scan id | Scanned at | Gate | Visitor | QR | Direction | Outcome |
        Denial reason | Source
    And the workbook's rows are the same 30 scans, in the same scannedAt-descending
        order the grid returned
    And no scan dated 2026-08-17 and no allowed scan appears in the workbook
    # Parity is by construction, not by a second implementation: the export composes
    # the SAME declared columns onto the SAME query and only chooses its own bound.
    # The bespoke filter it replaced accepted top up to 100,000 and built the whole
    # workbook in one request.

Scenario: The export honours the search term too
   When both calls carry "search": "Sara"
   Then the workbook holds exactly the scans the grid counted for that search

Scenario: The export is bounded by its own cap, not by the caller
  Given the window matches 25,000 scans
   When "POST /admin/gates/reports/scans.xlsx" is called for that window
   Then the workbook holds at most 10,000 data rows
    And the response is still a single well-formed .xlsx download
    And the Content-Disposition header names "gate-scans-{yyyyMMddHHmmss}.xlsx",
        stamped on the Saudi clock like every other SIMF export filename
    # 10,000 was the old bespoke filter's own default, so the export callers actually
    # asked for is unchanged; what is gone is the caller's ability to ask for 100,000.

Scenario: A bad key fails the export exactly as it fails the grid
   When "POST /admin/gates/reports/scans.xlsx" is called with
        """
        { "sort": "gateCode" }
        """
   Then the response is 400
    And "error.code" is "GRID_SORT_KEY_INVALID"
    And no workbook is produced
    # The export cannot be a back door around the validation the grid enforces:
    # it runs the same column declaration, so it rejects the same keys.

Scenario: The export is gated by the same permission as the report
  Given a signed-in admin whose role does NOT hold Gates.Manage
   When they call "POST /admin/gates/reports/scans.xlsx"
   Then the response is 403 and no workbook is produced
```

**Evidence captured:**
- The workbook is opened and its data rows counted, then compared with the grid
  call's `data.total` for the identical body. A workbook that is larger than the
  grid's total means the export dropped a filter; smaller means it applied one the
  grid did not, or hit its cap (assert the cap case separately, as above).
- Console errors: 0 expected. Network failures: 0 expected.

---

## Implementation notes

- **API integration tests** at `tests/SIMF.Api.Tests/AdminGatesTests.cs` cover the
  two backing endpoints at a lower layer (no browser): the gates `POST /api/v1/admin/gates/list`
  round-trip (creates a gate, lists it, asserts it is present) and the standard
  CRUD/conflict cases (`GATE_CODE_DUPLICATE` 409, direction-mode update,
  deactivate). The `currently-inside` report is exercised indirectly via the
  scan-flow tests under `tests/SIMF.Api.Tests/GateScanTests.cs` and
  `GateVisitorsListTests.cs`. There is no dedicated browser-level test yet — this
  catalogue is the source of truth for the CP dashboard surface.
- **Permission gate.** The page and both endpoints share one permission,
  `PermissionCatalog.Gates.Manage`. `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  asserts the `Module.GatesDashboard` nav item carries `RequiredPermission =
  PermissionCatalog.Gates.Manage`, and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs`
  asserts the admin gate endpoints reject a caller lacking it — those two cover
  E2E-GDS-007 at the unit/integration layer.
- **No mutation surface.** Because the page is read-only, there are no
  validation, conflict/duplicate, or write-audit scenarios to author here (the
  template's "validation failure" and "conflict / duplicate" rows do not apply);
  those live in the gate-CRUD catalogue for `/admin/gates`. The resilience
  scenarios (E2E-GDS-008 / -009) replace them.
- **Why the scan report lives in this file.** `POST /admin/gates/reports/scans/list`
  and `POST /admin/gates/reports/scans.xlsx` have no Control Panel page: the BFF
  passthrough and the `SimfAdminClient` method exist, but nothing renders them, so
  there is no route to open a per-page catalogue file against. They read the same
  `GateScans` table as the occupancy report, carry the same `Gates.Manage` gate,
  and moved onto the grid seam in the same wave, so E2E-GDS-017 to -021 host them
  here beside their sibling rather than inventing a page that does not exist. If a
  scan-report page ships later, lift those five scenarios into its own file and
  retire the ids here (ids are never reused).
- **The seam itself** is covered cross-cuttingly by
  [`cp-grid-contract.md`](cp-grid-contract.md) (E2E-GRID-001 to -020). The nine
  scenarios above are the per-report proof: the contract file says an unknown sort
  key must be a 400, and E2E-GDS-014 and -019 say which keys THESE two reports
  actually declare, which is the half a generic contract cannot pin.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be
  created) + step-definition class. The Gherkin shape is already runner-agnostic.
  E2E-GDS-013 to -021 are HTTP-level and stay outside the browser runner.

---

_Last reviewed:_ 2026-08-18 by Claude (both gate reports moved onto the shared
grid seam: routes, envelopes and the stat-card contract amended, E2E-GDS-013 to
-021 added, the scan report and its XLSX export adopted into this file). Prior:
2026-06-02 by Claude (E2E catalogue rebuild).
