# E2E test catalogue — `Agenda` (`agenda`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Mobile
> catalogue — the public programme API is built (D-199) + enriched (D-252); the
> API implementation lives in `tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs`.

| | |
|--|--|
| **Page** | [`Page_016`](../../App/Page_016/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/programme/sessions` (+`?day=`) · `GET /api/v1/app/programme/sessions/{id}` · app screen #16 `/agenda` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **Anonymous** for the public reads (no token). Admin token only to seed sessions/speakers/themes. **No literal secrets.** |
| **Last reviewed** | 2026-06-03 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB016-001 | Anonymous list returns the active programme (hall, title, theme) | happy | P0 | authored ✓ (`Public_list_returns_active_sessions_to_an_anonymous_caller`) |
| E2E-MOB016-002 | Each list item carries body + ordered speaker cards (cached payload drives detail) | happy | P0 | authored ✓ (`Public_list_item_carries_the_body_and_speaker_cards`) |
| E2E-MOB016-003 | List is ordered by start time | happy | P1 | authored ✓ (`Public_list_is_ordered_by_start_time`) |
| E2E-MOB016-004 | `?day=` restricts to one UTC calendar day (thin-client filter) | happy | P1 | authored ✓ (`Day_filter_restricts_to_that_utc_calendar_day`) |
| E2E-MOB016-005 | Malformed `?day=` → 400 | error | P1 | authored ✓ (`Malformed_day_filter_is_rejected_with_400`) |
| E2E-MOB016-006 | Soft-deleted session drops from the list | edge | P1 | authored ✓ (covered by the delete test) |
| E2E-MOB016-007 | Tap a row → Session detail (17) renders from cache; "main session" = category tag | happy | P1 | authored (screen) |
| E2E-MOB016-008 | Client filters (Upcoming/Forum pills, day strip, search) slice the cache — no refetch | happy | P0 | authored (screen) |
| E2E-MOB016-009 | RTL render + day strip scroll + active-session highlight | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB016-001 — Anonymous full programme

```gherkin
Feature: Agenda (public programme)
  As a guest (not logged in)
  I want the whole programme in one cacheable call
  So that I can browse and filter it offline

Scenario: The programme is readable without a token
  Given an active session "Opening Keynote" in "Main Hall" exists
  When an anonymous client calls GET /api/v1/app/programme/sessions
  Then the response is 200
  And the item carries title, hallName (EN+AR) and the primary theme
```

**Evidence:** `ProgrammeSessionsTests.Public_list_returns_active_sessions_to_an_anonymous_caller` (green).

### E2E-MOB016-002 — Cached payload carries body + speakers (D-252)

```gherkin
Scenario: Each list row carries the body and the ordered speaker cards
  Given an active session with a description and one speaker
  When an anonymous client calls GET /api/v1/app/programme/sessions
  Then the item.description and item.descriptionArabic are present
  And item.speakers has one card with name + rank (title)
  And the app can render the session preview from this cached item without a second fetch
```

**Evidence:** `ProgrammeSessionsTests.Public_list_item_carries_the_body_and_speaker_cards` (green).

### E2E-MOB016-003 — Ordering

```gherkin
Scenario: Sessions are ordered ascending by start time
  Given two sessions on the same day at 09:00 and 14:00
  When the programme is fetched
  Then the 09:00 session appears before the 14:00 session
```

**Evidence:** `ProgrammeSessionsTests.Public_list_is_ordered_by_start_time` (green).

### E2E-MOB016-004 — Day filter (thin client)

```gherkin
Scenario: ?day= restricts to one UTC calendar day
  Given sessions on day D and day D+1
  When the client calls GET /api/v1/app/programme/sessions?day={D}
  Then only the day-D session is returned
```

> The **app** does not use this — it caches the whole programme and filters the
> day strip client-side (Page_016_Logic L-1). The server filter serves thin clients.

**Evidence:** `ProgrammeSessionsTests.Day_filter_restricts_to_that_utc_calendar_day` (green).

### E2E-MOB016-005 — Malformed day

```gherkin
Scenario: A bad day filter is rejected
  When the client calls GET /api/v1/app/programme/sessions?day=not-a-date
  Then the response is 400 (SessionInvalid)
```

**Evidence:** `ProgrammeSessionsTests.Malformed_day_filter_is_rejected_with_400` (green).

### E2E-MOB016-006 — Soft-delete drops from list

```gherkin
Scenario: A soft-deleted session disappears from the programme
  Given an active session that is then deleted by an admin
  When the programme is fetched
  Then the deleted session is not in the list
  And its detail returns 404
```

### E2E-MOB016-007 — Tap-through + type tag

```gherkin
Scenario: Tapping a row opens the detail from cache with the category tag
  Given the cached programme contains a session whose category is "Main Session" (جلسة رئيسية)
  When the user taps the row
  Then Session detail (17) renders immediately from the cached item
  And it shows the hall tag + the "جلسة رئيسية" / "Main Session" category tag
  And the live seat count refreshes in the background
```

### E2E-MOB016-008 — Client-side filters, no refetch

```gherkin
Scenario: The pills, day strip and search slice the cache
  Given the whole programme is cached from one fetch
  When the user switches Upcoming/Forum, picks a day, or types in search
  Then the visible list updates instantly from the cache
  And no new GET /app/programme/sessions request is made
```

### E2E-MOB016-009 — RTL render

```gherkin
Scenario: The agenda renders right-to-left in Arabic
  Given the device locale is Arabic
  When the agenda renders
  Then the layout and day strip are right-to-left
  And the active/next session row is highlighted in brass
  And times render in the device timezone
```

---

_Last reviewed:_ `2026-06-03` by `SIMF Team`.
