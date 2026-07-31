# E2E test catalogue — Public programme list, category filter (`GET /api/v1/app/programme/sessions`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md).

| | |
|--|--|
| **Page** | [`web-programme.md`](web-programme.md) (website) · `mobile-sessions` (app agenda) |
| **Route** | `GET /api/v1/app/programme/sessions?day={yyyy-MM-dd}&categoryId={guid}` |
| **Surface** | Public API (anonymous) — consumed by the Website `/programme` page and the app agenda |
| **Test runner** | xUnit + `SimfApiFactory` (`tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs`) |
| **Auth setup** | None — the endpoint is `AllowAnonymous` |
| **Last reviewed** | 2026-07-31 |

## What changed and why

`OA-D6`. The endpoint declared exactly one query property, `Day`, and `HandleAsync`
parsed only that before calling `service.ListAsync(day, ct)`. Any category / track
filtering the app or website did was client-side over the whole programme. The
`Session.CategoryId` FK and the dynamic `SessionCategory` lookup (D-226) both already
existed — only the server-side filter was missing.

`ListProgrammeSessionsRequest` now also carries `Guid? CategoryId`, threaded into
`IProgrammeSessionService.ListAsync` as a second nullable predicate. The endpoint
keeps `CacheOutput("PublicRead")`, which varies by **all** query keys, so every
`?day=` × `?categoryId=` combination keys its own 45-second cache entry with no
cache-policy change.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-PCF-001 | Category filter returns only that category | happy | P0 | automated |
| E2E-PCF-002 | Category filter ANDs with the day filter | happy | P0 | automated |
| E2E-PCF-003 | Unknown category id returns an empty list, HTTP 200 | error | P0 | automated |
| E2E-PCF-004 | Omitted category returns the whole programme (no regression) | happy | P0 | automated |
| E2E-PCF-005 | Each category keys its own output-cache entry | perf | P1 | automated |
| E2E-PCF-006 | Malformed day still 400s alongside a valid category | error | P1 | automated |
| E2E-PCF-007 | Anonymous caller — no token required | auth | P0 | automated |

## Scenarios

### E2E-PCF-001 — Category filter returns only that category

```gherkin
Feature: Server-side programme track filter
  As an anonymous agenda reader
  I want to ask for one track
  So that the client does not download the whole programme to filter it

Background:
  Given a session "Opening Keynote" assigned to SessionCategory "Keynote track"
  And a session "Damage Control Workshop" assigned to SessionCategory "Workshop track"

Scenario: Only the requested track comes back
  When an anonymous client GETs /api/v1/app/programme/sessions?categoryId={keynote-track-id}
  Then the response is 200
  And data.items contains the session "Opening Keynote"
  And data.items does not contain the session "Damage Control Workshop"
```

**Evidence captured:** `ProgrammeSessionsTests.Category_filter_returns_only_that_category`.

### E2E-PCF-002 — Category filter ANDs with the day filter

```gherkin
Scenario: The two filters combine rather than replace
  Given two sessions in the SAME category, one on day 1 and one on day 2
  When an anonymous client GETs
    /api/v1/app/programme/sessions?day={day-1}&categoryId={category-id}
  Then the response is 200
  And data.items contains only the day-1 session
```

**Evidence captured:** `ProgrammeSessionsTests.Category_filter_combines_with_the_day_filter`.

### E2E-PCF-003 — Unknown category id returns an empty list

```gherkin
Scenario: The anonymous agenda is not a category-id oracle
  When an anonymous client GETs /api/v1/app/programme/sessions?categoryId={random-guid}
  Then the response is 200
  And data.items is empty
  And the response is NOT 404
```

An unknown id must not 404: a 404-vs-200 difference would let an anonymous caller
enumerate which category ids exist. It matches nothing and says so.

**Evidence captured:** `ProgrammeSessionsTests.Unknown_category_filter_returns_an_empty_list_not_404`.

### E2E-PCF-004 — Omitted category returns the whole programme

```gherkin
Scenario: The pre-existing contract is untouched
  When an anonymous client GETs /api/v1/app/programme/sessions
  Then the response is 200
  And data.items contains sessions from every category, and uncategorised sessions
```

`CategoryId` is nullable with no default filter, so an old client that never sends it
sees exactly what it saw before.

**Evidence captured:** the pre-existing
`ProgrammeSessionsTests.Public_list_is_anonymous_and_returns_active_session_with_hall_and_theme`
still passes unchanged.

### E2E-PCF-005 — Each category keys its own cache entry

```gherkin
Scenario: Two tracks do not share one cached body
  Given sessions in two different categories
  When an anonymous client GETs the list for category A
  And an anonymous client GETs the list for category B
  Then each response contains only its own category's sessions
```

`CacheOutput("PublicRead")` varies by query key, so the new parameter needed no
cache-policy change. This scenario is the proof, not the assumption.

**Evidence captured:** the second half of
`ProgrammeSessionsTests.Category_filter_returns_only_that_category`.

### E2E-PCF-006 — Malformed day still 400s

```gherkin
Scenario: The day parser is unchanged
  When an anonymous client GETs /api/v1/app/programme/sessions?day=not-a-date
  Then the response is 400
  And error.code is "SESSION_INVALID"
```

**Evidence captured:** the pre-existing
`ProgrammeSessionsTests.Malformed_day_filter_is_rejected_with_400`.

### E2E-PCF-007 — Anonymous caller

```gherkin
Scenario: No token is required
  When a client with no Authorization header GETs the list with a category filter
  Then the response is 200
```

## Notes for the runner

- The admin session API carries no category field, so the fixtures assign
  `Session.CategoryId` directly on `SimfAppDbContext` — the same approach the
  published-summary tests use.
- `SessionCategory` ships EMPTY (D-226, open item OI-2): the client's category list
  is still outstanding, so on a fresh production database every request without
  `?categoryId=` behaves exactly as before and any request WITH one returns nothing
  until the lookup is seeded.
