# E2E test catalogue — `Session summaries` (`sessionSummaryList`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> the searchable, day-grouped list of every programme session over the cached
> `aiSummarySessionsProvider` (no new read for the list itself), with three tabs:
> الجميع (all), جلساتي (the caller's booked sessions, from `GET /app/account/sessions`),
> المفضلة (favourited, from `GET /app/sessions/favourites`). Each card carries the
> المفضلة heart; tapping a card opens that session's AI summary (#34). Built to KSA
> Figma frame **`1388:8392`** (ملخص الجلسات) — Wave 2 pixel pass over the earlier
> #1/#6 list. Tested in
> `src/Mobile/simf_app/test/features/ai_summary/session_summary_list_screen_test.dart`;
> favourites backend in `tests/SIMF.Api.Tests/SessionFavouriteTests.cs`.

| | |
|--|--|
| **Page** | app screen #111 `sessionSummaryList` |
| **Route** | `/session-summaries` (cached programme; favourites/booked overlays) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1388:8392` |
| **Auth setup** | **Guest+** for the list. جلساتي + المفضلة tabs + the heart toggle need an **Approved account** (empty / no-op for a guest). |
| **Last reviewed** | 2026-06-26 |

## Layout

- **Header**: back chevron + centred title **ملخص الجلسات**.
- **Search field**: navy with a magnifier, hint `ابحث عن جلسة أو متحدث...`;
  filters by session title + speaker name (both languages), live.
- **Tabs** (pills, RTL): الجميع (all) · جلساتي (booked) · المفضلة (favourited).
- **Day groups**: a header per distinct event day (اليوم الأول, …) over the cards.
- **Cards** (navy-deep, beige hairline): the المفضلة heart on the trailing edge;
  title; clock line `time · {duration}`; primary speaker `name · rank` + hall; a
  bottom row with a **state chip** (`مباشر الآن` live / `مسجّل` recorded — the
  summary chip is suppressed here since the whole list is summarised, owner
  2026-07-14) + the bordered category chip. Tapping a card opens the AI summary
  details (#34); tapping the heart toggles the favourite (optimistic; reverts +
  toasts on a server error).
- **List filter** (owner 2026-07-14): the list shows **only sessions with a
  published summary** (`hasPublishedSummary` on the cached programme) — a future
  / not-yet-summarised session does not appear.
- **States**: spinner while loading; retry on a wire error; tab-specific empty
  messages (no booked / no favourites / no search match); a
  **`لا توجد ملخصات منشورة بعد`** empty state when the programme has sessions but
  none are summarised yet.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB111-001 | All tab lists sessions; tap opens the AI summary | happy | P0 | authored ✓ (screen `the All tab lists sessions; tapping one opens its summary`) |
| E2E-MOB111-002 | Search filters by title / speaker | data | P0 | authored ✓ (screen `the search field filters by title`) |
| E2E-MOB111-003 | المفضلة tab shows only favourited sessions | data | P1 | authored ✓ (screen `the Favourites tab shows only favourited sessions`) |
| E2E-MOB111-004 | جلساتي tab shows only booked sessions | data | P1 | authored ✓ (screen `the My sessions tab shows only booked sessions`) |
| E2E-MOB111-005 | Empty programme → empty message | empty | P1 | authored ✓ (screen `shows the empty state when there are no sessions`) |
| E2E-MOB111-006 | Favourite POST/DELETE round-trips + is per-user | happy | P0 | authored ✓ (API `Favourite_then_list_then_unfavourite_round_trips`, `Favourites_are_per_user`) |
| E2E-MOB111-007 | Favourite an unknown session → 404 | error | P1 | authored ✓ (API `Favourite_an_unknown_session_returns_404`) |
| E2E-MOB111-008 | Favourites read is approved-only | auth | P0 | authored ✓ (API `Favourites_list_without_a_token_returns_401`) |
| E2E-MOB111-009 | A published summary stays hidden until the session has STARTED (clock-based, S-6 owner) — a future session's summary is not viewable; once it starts it shows | data | P1 | authored ✓ (API `SessionSummaryTests.GetSessionSummaryAsync_BeforeSessionStarts_ReturnsNull` + `.GetSessionSummaryAsync_AfterStart_ReturnsSummary`) |
| E2E-MOB111-010 | The list shows ONLY sessions with a published summary — a not-yet-summarised session is excluded (owner 2026-07-14) | data | P0 | authored ✓ (screen `excludes a session with no published summary`) |
| E2E-MOB111-011 | Programme has sessions but none summarised → the "no published summaries yet" empty state | empty | P1 | authored ✓ (screen `shows the "no summaries yet" empty state when the programme has sessions but none are summarised`) |

## Scenarios

```gherkin
Feature: Session summaries (Figma 1388:8392, cached programme + favourites overlay)

Scenario: List and open a summary
  Given the programme has the sessions "Opening" and "Closing"
  When the user opens /session-summaries
  Then both sessions are listed under their day group
  When the user taps "Opening"
  Then the AI summary for that session opens

Scenario: Search
  Given the sessions "Opening" and "Closing" are listed
  When the user types "Clos" in the search field
  Then only "Closing" is shown

Scenario: The Favourites tab
  Given the user has favourited "Closing"
  When the user selects the المفضلة tab
  Then only "Closing" is shown

Scenario: Toggling a favourite round-trips per user
  Given an approved visitor and a seeded session
  When the visitor POSTs /api/v1/app/sessions/{id}/favourite
  Then GET /api/v1/app/sessions/favourites contains the session
  And a second visitor's favourites do not contain it
  When the visitor DELETEs the favourite
  Then it is no longer in their favourites

Scenario: A published summary is viewable only once the session has started (S-6 owner)
  Given a FUTURE session (Start ahead of now) with a published summary
  Then GET /app/programme/sessions/{id}/summary returns 404 — you cannot view a
    summary before the session begins
  Given a STARTED session (Start in the past) with a published summary
  Then the same read returns the summary (200) — gated on the clock, not Session.Status

Scenario: The list is only summarised sessions (owner 2026-07-14)
  Given the programme has "Summarised" (a published summary) and "NotSummarised" (none)
  When the user opens /session-summaries
  Then "Summarised" is listed
  And "NotSummarised" is not listed

Scenario: No summaries yet
  Given the programme has sessions but none has a published summary
  When the user opens /session-summaries
  Then the "لا توجد ملخصات منشورة بعد" empty state is shown
```

**Evidence:** screen tests (5 — list+nav, search, favourites tab, mine tab,
empty); favourites API tests (4 — round-trip, unknown→404, per-user, anon→401).

---

_Last reviewed:_ `2026-06-26` by `SIMF Team`.
