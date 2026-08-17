# E2E test catalogue - Control Panel grid contract (every server-paged list)

> **Authority:** the shared grid seam. Cross-cutting behaviour file (not tied to
> one page), on the same footing as [`cp-timezone-display.md`](cp-timezone-display.md)
> and [`cp-walk-in-mode.md`](cp-walk-in-mode.md). Every Control Panel list page
> that renders through `SimfDataGrid` and is served by a `ToGridPageAsync` call
> must satisfy every scenario below on its own columns.

| | |
|--|--|
| **Feature** | One declared column set per list resource: sort, filter, search, page, count, and a mandatory tiebreak |
| **Route(s)** | representative CP page `/admin/themes` over `POST /admin/themes/list`; secondary `/admin/badge-requests` (enum column) and `/admin/operation-log` |
| **Surface** | Control Panel (Blazor Server) + the admin API that backs it |
| **Test runner** | Chrome DevTools MCP for the page half; direct HTTP calls (PowerShell / `Invoke-RestMethod`) for the envelope half |
| **Auth setup** | Control-Panel admin sign-in; the TOTP step uses the `Get-Totp` helper, never a literal secret |
| **Seam under test** | [`GridColumns.cs`](../../../src/Shared/SIMF.Common/Grids/GridColumns.cs) - [`GridQueryComposition.cs`](../../../src/Shared/SIMF.Common/Grids/GridQueryComposition.cs) - [`GridFilters.cs`](../../../src/Shared/SIMF.Common/Grids/GridFilters.cs) - [`GridSearchPredicate.cs`](../../../src/Shared/SIMF.Common/Grids/GridSearchPredicate.cs) - [`GridQueryExtensions.cs`](../../../src/Backend/SIMF.Infrastructure/Common/Grids/GridQueryExtensions.cs) |
| **Pinned by** | `tests/SIMF.Api.Tests/GridContractTests.cs`, `tests/SIMF.Api.Tests/GridColumnsTests.cs`, `tests/SIMF.Api.Tests/GridDateSortKeyTests.cs` |
| **Last reviewed** | 2026-08-17 |

## What this file is for

Before the seam, each list service hand-wrote its own sort switch, its own
filter parsing and its own search. The failures were all of one shape: a request
the server did not understand came back looking like an answer. An unknown sort
key fell through a `_ =>` catch-all, so the arrow flipped and the rows did not
move. An unparseable filter value skipped its predicate, so the grid returned
*every* row, which on an admin grid over people is a disclosure rather than an
inconvenience. A search box wired to no searchable column returned the unfiltered
set. None of those raised anything, so none of them was ever reported as a bug.

The contract below exists so that a request the server does not understand is a
**loud bilingual 400** and never a quietly widened result set. It is written
against `/admin/themes` because that page is small enough to state exact row
counts, but the assertions are about the seam, so they hold for every list page
and should be re-run against a page's own column keys when that page changes.

**The wire is a JSON body, not a query string.** A list endpoint is
`POST {resource}/list` binding a `GridQuery`, so the request reads
`{"skip":0,"top":25,"sort":"name","sortDescending":false,"search":"","filters":{"isActive":"true"}}`.
Where a scenario below is written as `filters.isActive = ...` that is the body
member, not a `filters[...]` query parameter.

**The declared contract for `/admin/themes`**, quoted so the scenarios are
readable without opening the service: columns `code` (searchable), `name`
(searchable), `nameArabic` (searchable), `displayOrder`, `isActive`; every
declared column is both sortable and filterable; the natural order is
`displayOrder` then `name`; the tiebreak is `Id`; the page size falls back to 25
and is capped at 200.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-GRID-001 | A valid list request returns rows and a database-side total | happy | P0 | driven 2026-08-17 |
| E2E-GRID-002 | An unknown sort key is a 400 that names the key and lists the sortable columns | validation | P0 | driven 2026-08-17 |
| E2E-GRID-003 | An unknown filter key is a 400 that names the key and lists the filterable columns | validation | P0 | driven 2026-08-17 |
| E2E-GRID-004 | An unparseable filter value is a 400, never a silently widened result set | security | P0 | driven 2026-08-17 |
| E2E-GRID-005 | A filter key in the wrong case still binds and still filters | happy | P0 | driven 2026-08-17 |
| E2E-GRID-006 | The Themes Name filter narrows the list; the box used to be dead | happy | P0 | driven 2026-08-17 |
| E2E-GRID-007 | Clicking the Themes Order header sorts ascending, then descending | happy | P0 | driven 2026-08-17 |
| E2E-GRID-008 | Paging a list whose sort column has ties never repeats or drops a row | correctness | P0 | driven 2026-08-17 |
| E2E-GRID-009 | A search term containing `%` matches literally, not as a wildcard | security | P0 | driven 2026-08-17 |
| E2E-GRID-010 | A list page renders in Arabic with `dir=rtl` and no horizontal overflow | i18n | P1 | driven 2026-08-17 |
| E2E-GRID-011 | A filter that matches nothing returns an empty page with a total of 0 | empty | P1 | authored |
| E2E-GRID-012 | The same filter column sent twice in two casings is a 400, not an empty grid | validation | P1 | authored |
| E2E-GRID-013 | An enum filter parses by NAME only; the ordinal and an out-of-range number are 400 | validation | P1 | authored |
| E2E-GRID-014 | A list with no searchable column refuses a search term instead of ignoring it | security | P1 | authored |
| E2E-GRID-015 | The resource's page-size policy is applied, not the caller's request | resilience | P1 | authored |
| E2E-GRID-016 | A blank filter value still has its key validated | validation | P2 | authored |
| E2E-GRID-017 | A request may not carry an unbounded number of filter keys or an unbounded search term | resilience | P2 | authored |
| E2E-GRID-018 | No grid column is declared over an encrypted (value-converted) column | security | P0 | authored (by construction) |
| E2E-GRID-019 | Every list request is still permission-gated; the grid seam changes nothing there | auth-gate | P0 | authored |
| E2E-GRID-020 | A rejected list request answers the standard bilingual `ApiResult` error envelope | resilience | P1 | authored |

## Scenarios

### E2E-GRID-001 - A valid list request returns rows and a database-side total

```gherkin
Feature: One page of a list resource
  As an administrator
  I want a list page to return the rows I asked for and the true size of the set
  So that the pager is honest about how much data there is

Background:
  Given a Control-Panel admin holding "Themes.View" is signed in
    And the Themes table holds 5 active themes

Scenario: E2E-GRID-001 The first page carries rows and the filtered total
   When "POST /admin/themes/list" is called with
        """
        { "skip": 0, "top": 25 }
        """
   Then the response is 200 with "success": true
    And "data.items" holds 5 rows
    And "data.total" is 5, counted on the server BEFORE Skip/Take
    And "data.skip" is 0 and "data.top" is 25
    And the rows come back in the resource's natural order, displayOrder then name
    And the rows are read-only projections: no tracked entity is materialised
```

**Evidence captured:**
- The `total` is compared against `SELECT COUNT(*) FROM Themes` run separately.
  A total equal to `items.Count` on a set larger than one page is the defect this
  asserts against: it means the count was taken after paging.
- Console errors: 0 expected. Network failures: 0 expected.

### E2E-GRID-002 - An unknown sort key is a 400 that names the key and lists the sortable columns

```gherkin
Scenario: E2E-GRID-002 A sort key that is not a declared column is refused
   When "POST /admin/themes/list" is called with
        """
        { "sort": "notAColumn" }
        """
   Then the response is 400
    And "error.code" is "GRID_SORT_KEY_INVALID"
    And "error.message" begins "'notAColumn' is not a sortable column on this list."
    And the same message goes on to list the sortable columns of THIS list,
        so the caller can correct the key without reading the source
    And the Arabic message names the same key
    And "error.details[0].field" is "sort"
    And NO rows are returned

  # The point is the shape, not the punctuation. Assert that the offending key
  # appears and that the sortable keys of the resource under test appear. Do not
  # pin their order: the list is dictionary enumeration order, not contract.
```

### E2E-GRID-003 - An unknown filter key is a 400 that names the key and lists the filterable columns

```gherkin
Scenario: E2E-GRID-003 A filter key that is not a declared column is refused
   When "POST /admin/themes/list" is called with
        """
        { "filters": { "themeGroup": "maritime" } }
        """
   Then the response is 400
    And "error.code" is "GRID_FILTER_KEY_INVALID"
    And "error.message" begins "'themeGroup' is not a filterable column on this list."
    And the same message goes on to list the filterable columns of THIS list
    And "error.details[0].field" is "themeGroup"
    And NO rows are returned

Scenario: E2E-GRID-003 A key a page injects in code is declared like any other
  Given a page whose code-behind or endpoint injects a filter key the markup
        never renders, such as an owning parent id
   When that page loads normally
   Then the request succeeds
    And the injected key resolves to a declared column
  # This is the regression this scenario exists for. An injected key that was
  # never declared is now a 400 on a live page, and it is invisible in the .razor
  # markup, so it has to be found in the .razor.cs and in the endpoint.
```

### E2E-GRID-004 - An unparseable filter value is a 400, never a silently widened result set

```gherkin
Scenario: E2E-GRID-004 A boolean filter given prose is refused
  Given the Themes table holds 5 themes, 4 active and 1 inactive
   When "POST /admin/themes/list" is called with
        """
        { "filters": { "isActive": "yes-please" } }
        """
   Then the response is 400
    And "error.code" is "GRID_FILTER_VALUE_INVALID"
    And "error.message" is
        "'yes-please' is not a valid value for the 'isActive' filter. Expected Boolean."
    And the Arabic message is
        "القيمة 'yes-please' غير صالحة لتصفية العمود 'isActive'."
    And "error.details[0].field" is "isActive"
    And the response does NOT contain 5 rows

  # The final line is the whole scenario. The old hand-written services dropped a
  # predicate they could not parse, so this request returned every row including
  # the inactive one, and looked like a successful unfiltered list.

Scenario: E2E-GRID-004 A very long filter value is truncated before it is echoed
   When a filter value longer than 64 characters is sent to a column that cannot
        parse it
   Then the 400 echoes only its first 64 characters followed by "..."
    And the error body stays small enough to render in a Control-Panel toast
```

### E2E-GRID-005 - A filter key in the wrong case still binds and still filters

```gherkin
Scenario: E2E-GRID-005 Column keys match case-insensitively
  Given the Themes table holds 5 themes, 4 active and 1 inactive
   When "POST /admin/themes/list" is called with
        """
        { "filters": { "ISACTIVE": "true" } }
        """
   Then the response is 200
    And "data.total" is 4
    And the inactive theme is absent

   When the same call is made with "isActive" instead
   Then the response is 200 with the same 4 rows

  # GridQuery.Filters is a case-SENSITIVE dictionary while SimfDataGrid sends the
  # column key verbatim in camelCase, so the reconciliation has to happen in the
  # column table. A 400 here would mean a live page breaks on a casing difference
  # nobody can see.
```

### E2E-GRID-006 - The Themes Name filter narrows the list

```gherkin
Scenario: E2E-GRID-006 Typing in the Name filter box actually filters
  Given an admin holding "Themes.View" is on "/admin/themes"
    And the grid shows 5 themes, of which exactly 2 have "Maritime" in their
        English name
   When the admin types "Maritime" into the Name column's filter box
    And the grid's filter debounce elapses
   Then the grid reloads and shows 2 rows
    And both visible rows contain "Maritime" in the Name column
    And the row-count summary reports 2, not 5
    And clearing the box restores all 5 rows

  # Driven in the browser, not against the API: the defect being regressed was
  # that this box was wired to a filter key the service ignored, so typing in it
  # sent a request and changed nothing on screen.

Scenario: E2E-GRID-006 The filter is a case-insensitive substring, not a prefix
   When the admin types "maritime" in lower case
   Then the same 2 rows are shown
   When the admin types a fragment from the middle of a name
   Then the rows containing that fragment are shown
```

**Evidence captured:**
- Screenshot before the filter (5 rows) and after (2 rows).
- The `POST /admin/themes/list` request in the network list carries
  `filters.name = "Maritime"` and answers 200.
- Console errors: 0 expected.

### E2E-GRID-007 - Clicking the Themes Order header sorts ascending, then descending

```gherkin
Scenario: E2E-GRID-007 The Order column sorts numerically in both directions
  Given an admin is on "/admin/themes"
    And the 5 themes carry display orders 1, 2, 3, 4 and 5
   When the admin clicks the "Order" column header
   Then the rows are ordered 1, 2, 3, 4, 5
    And the header shows the ascending indicator
   When the admin clicks the same header again
   Then the rows are ordered 5, 4, 3, 2, 1
    And the header shows the descending indicator

Scenario: E2E-GRID-007 Ten sorts before two, not after it
  Given a theme whose display order is 10 exists alongside one whose order is 2
   When the admin sorts ascending on Order
   Then 2 appears before 10

  # The ordering runs over the property's real CLR type, so an int column sorts
  # numerically. An object-typed column table would force a boxing conversion into
  # the expression tree and sort "10" before "2" as text.
```

### E2E-GRID-008 - Paging a list whose sort column has ties never repeats or drops a row

```gherkin
Scenario: E2E-GRID-008 A non-unique sort column still pages stably
  Given a list of at least 60 rows
    And a sortable column on which many rows share the SAME value, for example a
        status column where 40 rows read "Pending"
   When the admin sorts on that column and reads page 1 with { "skip": 0, "top": 25 }
    And then page 2 with { "skip": 25, "top": 25 }
    And then page 3 with { "skip": 50, "top": 25 }
   Then the union of the three pages contains each row exactly once
    And no row id appears on two pages
    And no row id is missing from the union that the unpaged, identically filtered
        query returns
    And re-reading page 2 returns the identical rows in the identical order

  # SQL Server may return tied rows in any order when the ORDER BY does not
  # distinguish them, so without a unique tiebreak appended to every sort, the
  # same row can appear on two pages while another appears on none. The tiebreak
  # is a required parameter of the seam precisely so this cannot be forgotten.
```

**Evidence captured:**
- The three page responses collected and compared by primary key. The assertion
  is on the set, not on the visual order: a duplicate or a gap is the failure.

### E2E-GRID-009 - A search term containing `%` matches literally

```gherkin
Scenario: E2E-GRID-009 The search box has no pattern language
  Given a searchable list holding one row whose searchable text contains a
        literal per-cent sign, for example "100% attendance"
    And several other rows that contain no per-cent sign
   When the admin searches for "%"
   Then only the rows whose text actually contains "%" are returned
    And the full set is NOT returned

   When the admin searches for "_"
   Then only the rows whose text actually contains an underscore are returned
    And a row whose text merely has SOME character in that position is not matched

  # The services this replaced built LIKE '%' + term + '%' with no escaping, so a
  # "%" matched every row and a "_" matched any single character. The seam uses
  # string.Contains, which has no pattern language, so the defect cannot recur.

Scenario: E2E-GRID-009 The search is one OR chain across the searchable columns
   When the admin searches "/admin/themes" for a term present only in nameArabic
   Then the matching row is returned
    And the query runs entirely on the server: no page of rows is fetched and
        filtered in memory
```

### E2E-GRID-010 - A list page renders in Arabic with `dir=rtl` and no horizontal overflow

```gherkin
Scenario: E2E-GRID-010 The grid, its filter row and its pager render right to left
  Given the interface language is Arabic
   When the admin opens "/admin/themes"
   Then the document carries dir="rtl"
    And the column headers, the per-column filter boxes and the pager are laid out
        right to left
    And every header and every control shows its Arabic string, with no resource
        key left visible
    And there is no horizontal overflow: scrollWidth == clientWidth on the page
        body, and any wider grid scrolls inside its own container
    And no image is broken and no same-origin asset returns 400 or worse
    And the console reports zero errors

Scenario: E2E-GRID-010 A grid error surfaces in Arabic too
   When a request from the Arabic interface is rejected by the grid contract
   Then the message shown is the Arabic side of the bilingual error, not the
        English one
```

**Evidence captured:**
- Full-page screenshot in Arabic and in English.
- DOM check: `document.scrollingElement.scrollWidth == clientWidth`.
- Console list: 0 errors. Network list: 0 failed requests.

### E2E-GRID-011 - A filter that matches nothing returns an empty page with a total of 0

```gherkin
Scenario: E2E-GRID-011 An empty result is an empty result, not an error
   When "POST /admin/themes/list" is called with
        """
        { "filters": { "name": "no-theme-has-this-text" } }
        """
   Then the response is 200
    And "data.items" is empty
    And "data.total" is 0
   When the same filter is typed into the page
   Then the grid renders its SimfEmptyState
    And no error toast appears
    And clearing the filter restores the rows
```

### E2E-GRID-012 - The same filter column sent twice in two casings is a 400

```gherkin
Scenario: E2E-GRID-012 A case-variant duplicate is refused rather than ANDed away
   When "POST /admin/themes/list" is called with
        """
        { "filters": { "IsActive": "true", "isActive": "false" } }
        """
   Then the response is 400
    And "error.code" is "GRID_FILTER_KEY_INVALID"
    And "error.message" is "The filter column 'isActive' was sent more than once."
    And the Arabic message names the same column

  # Both keys bind to one column, so applying both would AND "active" with
  # "inactive" and return the empty set. An empty grid with no explanation is the
  # worst of the three possible outcomes, so it is a 400 instead.
  # The keys are walked in ordinal order, so 'IsActive' is seen first and the
  # message names the second spelling, 'isActive'.
```

### E2E-GRID-013 - An enum filter parses by NAME only

```gherkin
Scenario: E2E-GRID-013 An enum column is a name contract on the wire
  Given "/admin/badge-requests", whose "status" column is a MeetingRequestStatus
   When its list endpoint is called with filters.status = "Pending"
   Then the response is 200 and only pending requests are returned
   When it is called with filters.status = "pending"
   Then the response is 200 with the same rows, because names match
        case-insensitively

   When it is called with filters.status = "2"
   Then the response is 400 with "GRID_FILTER_VALUE_INVALID"
    And the message ends "Expected one of:" followed by the enum's member names
   When it is called with filters.status = "99"
   Then the response is 400 with "GRID_FILTER_VALUE_INVALID"

  # "2" must not quietly become the third member and "99" must not become a
  # phantom value that matches no persisted row and returns an empty grid.

Scenario: E2E-GRID-013 An enum sorts by its STORED form
   When the admin sorts on an enum column stored as a string
   Then the rows are ordered alphabetically by the member name
   When the admin sorts on an enum column stored as an int
   Then the rows are ordered by the member's ordinal
  # Neither is a defect; both match the hand-written behaviour that was replaced.
  # Read the entity's mapping before reporting a "wrong" enum sort.
```

### E2E-GRID-014 - A list with no searchable column refuses a search term

```gherkin
Scenario: E2E-GRID-014 A search that cannot be honoured is a 400, not a no-op
  Given a list resource that declares no searchable column
   When its list endpoint is called with a non-empty "search"
   Then the response is 400
    And "error.code" is "GRID_SEARCH_NOT_SUPPORTED"
    And "error.message" is
        "This list has no searchable columns, so it cannot be searched."
    And the Arabic message is
        "لا تحتوي هذه القائمة على أعمدة قابلة للبحث، لذا لا يمكن البحث فيها."

  # An ignored search term returns the unfiltered set, which on a people grid
  # reads as a result rather than a fault. The page fix is either to declare a
  # searchable column or to stop rendering a search box.
```

### E2E-GRID-015 - The resource's page-size policy is applied

```gherkin
Scenario: E2E-GRID-015 An unset page size falls back, an excessive one is capped
  Given "/admin/themes", whose policy is fallback 25 and maximum 200
   When the list is called with "top": 0
   Then 25 rows at most are returned and "data.top" echoes 25
   When the list is called with "top": 5000
   Then 200 rows at most are returned and "data.top" echoes 200
   When the list is called with "skip": -10
   Then the page starts at row 0 and "data.skip" echoes 0
    And no request errors: a page window is clamped, not rejected
```

### E2E-GRID-016 - A blank filter value still has its key validated

```gherkin
Scenario: E2E-GRID-016 An empty value does not excuse an unknown key
   When "POST /admin/themes/list" is called with
        """
        { "filters": { "themeGroup": "" } }
        """
   Then the response is 400 with "GRID_FILTER_KEY_INVALID"

Scenario: E2E-GRID-016 A blank value on a KNOWN key applies no predicate
   When the call carries filters.name = "" instead
   Then the response is 200
    And every row is returned, exactly as if the filter had been omitted

  # A stale key is a client bug whether or not it carries a value today, so the
  # key is checked first and the value is only then allowed to be empty.
```

### E2E-GRID-017 - A request may not carry unbounded filters or an unbounded search term

```gherkin
Scenario: E2E-GRID-017 The filter-key count is bounded
   When a list request carries more than 20 filter columns
   Then the response is 400 with "VALIDATION_FAILED"
    And the message reads "A list request may carry at most 20 filter columns."

Scenario: E2E-GRID-017 The search term length is bounded
   When a list request carries a search term longer than 128 characters
   Then the response is 400 with "VALIDATION_FAILED"
    And the message reads "A search term may be at most 128 characters."

  # Both bounds are about cost, not correctness: 500 filter keys would become 500
  # WHERE clauses and 500 compiled-query cache entries, and an 8 KB search term an
  # 8 KB substring test per searchable column per row.
```

### E2E-GRID-018 - No grid column is declared over an encrypted column

```gherkin
Scenario: E2E-GRID-018 An encrypted column is never sortable or filterable
  Given UserProfile.MobileNumber, UserProfile.SaudiMobile,
        UserProfile.InternationalMobile and ProfileIdentityDocument.Number are
        stored under an AES-GCM value converter
   When any admin list over those entities is built
   Then no GridColumns declaration names any of those properties
    And no Control-Panel grid renders a sortable or filterable column over them

Scenario: E2E-GRID-018 The guard is a test, not a review note
   When "GridContractTests.No_grid_column_is_declared_over_an_encrypted_column"
        runs
   Then it walks every declared column's backing member against the EF model
    And fails the build if one of them carries a value converter

  # Filtering or sorting an encrypted column compiles, translates, runs and
  # matches NOTHING, forever, because the predicate is compared against the
  # ciphertext. It is silent, so only a model-aware test can catch it. A profile
  # search by phone number has to be a decrypt-side or hashed-column design, not
  # a grid column.
```

### E2E-GRID-019 - Every list request is still permission-gated

```gherkin
Scenario: E2E-GRID-019 The grid seam is not an authorisation path
  Given an account WITHOUT "Themes.View"
   When it navigates to "/admin/themes"
   Then it is redirected to /not-permitted
   When it calls "POST /admin/themes/list" directly
   Then the response is 403, before any column is resolved

Scenario: E2E-GRID-019 A scope predicate is applied outside the grid
  Given a list whose rows are restricted to the caller's own scope, for example an
        exhibitor's own booth
   When that list is requested with no filters at all
   Then only the in-scope rows are returned
    And "data.total" counts only the in-scope rows
    And no filter key can widen the set beyond that scope

  # Scope predicates compose onto the source BEFORE the grid runs, so they are not
  # expressible as a client-supplied filter and cannot be turned off by one.
```

### E2E-GRID-020 - A rejected list request answers the standard bilingual envelope

```gherkin
Scenario: E2E-GRID-020 Every grid 400 is an ordinary ApiResult failure
   When any of the rejections above occurs
   Then the body is the standard envelope: "success": false with an "error"
        object carrying "code", "message", "messageArabic" and "details"
    And the HTTP status is 400, not 200 with an error inside
    And the Control-Panel client parses it with the same reader it uses for every
        other endpoint

  # How a given page surfaces that failure - toast, inline banner, retry button -
  # is the page's own concern and belongs in the page's own catalogue file. What
  # is asserted here is only that the grid never answers a rejection with rows.
```

## Run on 2026-08-17

Driven at `localhost:5158` (Control Panel) against a real database built by this
branch's migrations, in the session that landed the seam.

- **001 to 010 were driven.** The Themes Name filter narrowed 5 rows to 2 (that
  box did nothing before the change); the Order header sorted 1,2,3,4,5 and then
  5,4,3,2,1; `sort=notAColumn` answered 400 `GRID_SORT_KEY_INVALID` naming the
  key and listing the sortable columns; an unknown filter key answered 400
  `GRID_FILTER_KEY_INVALID`; `isActive = "yes-please"` answered 400
  `GRID_FILTER_VALUE_INVALID` with "Expected Boolean" rather than returning every
  row; `ISACTIVE = "true"` answered 200 and filtered correctly; paging a tied
  sort column neither repeated nor dropped a row; a `%` search matched literally;
  and the Arabic render carried `dir=rtl` with no horizontal overflow.
- **011 to 020 are authored, not driven.** They are written from the seam's own
  code contract and from the tests that pin it, and they are the scenarios a
  regression round should add next. Do not read them as executed coverage.

## How to apply this file to one page

1. Open the page's `.razor` and list every `<SimfDataGridColumn Key="...">` that
   is `Sortable` or `Filterable`.
2. Open its `.razor.cs` and its endpoint and list every filter key set in code,
   including any the page injects and never renders.
3. Run E2E-GRID-002 and E2E-GRID-003 against **each** of those keys. Every one of
   them must answer 200. A key that answers 400 is a live page that is now broken,
   which is the single most likely defect this change can introduce.
4. Run E2E-GRID-006, E2E-GRID-007 and E2E-GRID-011 on the page's own columns and
   its own row counts.
5. Run E2E-GRID-010 in Arabic.

## Not covered here

- How an individual page renders a failed load. That is per-page and belongs in
  the page's own catalogue file.
- Excel export parity. An export reuses the composition half of the seam without
  paging, so it shares these filter and search semantics, but the export files
  and their column sets are catalogued with their own pages.
- The date-filter calendar-day semantics, which sit on the local-time doctrine
  covered by [`cp-timezone-display.md`](cp-timezone-display.md).

---

_Last reviewed:_ 2026-08-17 by SIMF Team.
