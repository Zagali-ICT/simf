# E2E test catalogue — `Sessions` (`sessionPresentations`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> data-driven from `GET /app/presentations` (`RequireApprovedAccount`). **D-704
> (owner 2026-07-08):** the list is now **every active session** (with its primary
> speaker), time-ordered by start — NOT only the sessions that have an uploaded
> deck. This is the fix for the empty "الجلسات" tile (no decks were uploaded on
> prod). When a session DOES carry an active D-228 `SpeakerPresentation`, its id +
> file metadata ride along so the `GET /app/presentations/{id}/file` download still
> resolves; a session with no deck appears with the session id and empty file
> fields (the app decodes them but the card ignores them). Built to KSA Figma frame
> **`1388:7621`** (الجلسات), reached from the Home "الجلسات" tile. **Owner
> 2026-07-03:** tapping a card opens the **session detail** (17); the gold **تحميل**
> button opens that session's **summary** (ملخص الجلسة, 34) — the screen no longer
> downloads the deck bytes. Tested in
> `src/Mobile/simf_app/test/features/sessions/session_presentations_screen_test.dart`
> + `presentation_models_test.dart`; backend list in
> `tests/SIMF.Api.Tests/PublicPresentationsTests.cs`
> (`List_returns_the_presentation_and_the_file_downloads`,
> `List_includes_a_session_that_has_no_presentation`,
> `List_without_a_token_returns_401`).

| | |
|--|--|
| **Page** | app screen #202 `sessionPresentations` |
| **Route** | `/session-presentations` (`GET /app/presentations`) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1388:7621` |
| **Auth setup** | **Approved account** — `RequireApprovedAccount`. Sign in as an approved visitor (`Get-Totp` for the OTP step, never a literal secret). |
| **Last reviewed** | 2026-07-08 |

## Layout

- **Header**: back chevron + centred title **الجلسات** (matches the Home tile).
- **Day tabs** (scrollable pills, RTL): الكل (all) + one per distinct event day
  (اليوم الأول, اليوم الثاني, …), grouped from each session's start.
- **Cards** (navy-deep, beige hairline): a file icon; the session title; the
  presenting speaker; the event-day label; a gold **تحميل** button. **Tapping the
  card** opens that session's **detail** (17). **Tapping تحميل** opens that
  session's **summary** (ملخص الجلسة, 34) — which 404s gracefully until the
  Committee publishes it.
- **States**: spinner while loading; retry surface on a wire error; an empty
  message (`No presentations available yet.`) — shown only when there are **no
  active sessions at all** (D-704), not merely no uploaded decks.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB202-001 | Sessions render with the speaker + a Download button under the day tabs | happy | P0 | authored ✓ (screen `lists the sessions with the speaker and a Download button`) |
| E2E-MOB202-002 | Empty catalogue → empty message | empty | P1 | authored ✓ (screen `shows the empty state when there are no presentations`) |
| E2E-MOB202-003 | Tapping a card → that session's detail (17) | happy | P0 | authored ✓ (screen `tapping the card opens that session detail (17)`) |
| E2E-MOB202-004 | Tapping تحميل → that session's summary (34) | happy | P0 | authored ✓ (screen `tapping تحميل opens that session summary (34)`) |
| E2E-MOB202-005 | `GET /app/presentations` lists a session that has a deck (id + file ride along) | happy | P0 | authored ✓ (API `List_returns_the_presentation_and_the_file_downloads`) |
| E2E-MOB202-006 | Anonymous list → 401 | auth | P0 | authored ✓ (API `List_without_a_token_returns_401`) |
| E2E-MOB202-007 | RTL — Arabic session title / speaker from the same item | rtl | P2 | covered (models `localized*` getters) |
| E2E-MOB202-008 | `GET /app/presentations` lists a session that has **no** deck (id = session id, empty file) — D-704 | happy | P0 | authored ✓ (API `List_includes_a_session_that_has_no_presentation`) |

## Scenarios

```gherkin
Feature: Sessions (approved account, Figma 1388:7621, GET /app/presentations)

Scenario: List renders and the card opens the session detail
  Given an admin uploaded a deck for a session by "Speaker One"
  When an approved visitor opens /session-presentations
  Then the screen lists the session title and "Speaker One" under the day tabs
  When the visitor taps the session card
  Then the app navigates to that session's detail (17)

Scenario: The تحميل button opens the session summary
  Given the session card is visible
  When the visitor taps the gold تحميل button
  Then the app navigates to that session's summary (34, sessionId in the query)

Scenario: A session with no uploaded deck still appears (D-704)
  Given an active session by "Speaker Two" has no presentation deck
  When an approved visitor GETs /api/v1/app/presentations
  Then the list includes that session (id = the session id, empty file fields)
  And the card opens that session's detail (17) and summary (34) as usual

Scenario: Empty catalogue
  Given no active sessions exist at all
  When the visitor opens /session-presentations
  Then the screen shows "No presentations available yet."

Scenario: The read requires an approved account
  Given no bearer token
  When a client GETs /api/v1/app/presentations
  Then it returns 401
```

**Evidence:** screen tests (4 — list+download button, empty, card→detail,
تحميل→summary); models test (2 — decode + bilingual, defaults); API tests
(3 — list-with-deck, list-includes-deckless-session (D-704), 401).

---

_Last reviewed:_ `2026-07-08` by `SIMF Team`.
