# E2E test catalogue — `Session presentations` (`sessionPresentations`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> data-driven from `GET /app/presentations` (`RequireApprovedAccount`), a
> read-only projection of the D-228 `SpeakerPresentation` files (active rows on
> active sessions). The file bytes stream from `GET /app/presentations/{id}/file`
> through the authenticated client and are handed to the OS share/save sheet.
> Built to KSA Figma frame **`1388:7621`** (عروض الجلسات). Previously a ComingSoon
> placeholder (D-464). Tested in
> `src/Mobile/simf_app/test/features/sessions/session_presentations_screen_test.dart`
> + `presentation_models_test.dart`; backend in
> `tests/SIMF.Api.Tests/PublicPresentationsTests.cs`
> (`List_returns_the_presentation_and_the_file_downloads`,
> `Download_an_unknown_presentation_returns_404`, `List_without_a_token_returns_401`).

| | |
|--|--|
| **Page** | app screen #202 `sessionPresentations` |
| **Route** | `/session-presentations` (`GET /app/presentations` + `/{id}/file`) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1388:7621` |
| **Auth setup** | **Approved account** — `RequireApprovedAccount`. Sign in as an approved visitor (`Get-Totp` for the OTP step, never a literal secret). |
| **Last reviewed** | 2026-06-26 |

## Layout

- **Header**: back chevron + centred title **عروض الجلسات**.
- **Day tabs** (scrollable pills, RTL): الكل (all) + one per distinct event day
  (اليوم الأول, اليوم الثاني, …), grouped from each deck's session start.
- **Cards** (navy-deep, beige hairline): a file icon; the session title; the
  presenting speaker; a gold **تحميل** (Download) button. The button shows a
  spinner + جارٍ التحميل while fetching, then opens the OS share/save sheet with
  the file bytes; a wire error shows a toast (تعذر تحميل الملف).
- **States**: spinner while loading; retry surface on a wire error; an empty
  message (`No presentations available yet.`).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB202-001 | Decks render with the speaker + a Download button under the day tabs | happy | P0 | authored ✓ (screen `lists the decks with the speaker and a Download button`) |
| E2E-MOB202-002 | Empty catalogue → empty message | empty | P1 | authored ✓ (screen `shows the empty state when there are no presentations`) |
| E2E-MOB202-003 | `GET /app/presentations` lists the deck; `/file` downloads the bytes | happy | P0 | authored ✓ (API `List_returns_the_presentation_and_the_file_downloads`) |
| E2E-MOB202-004 | Unknown / soft-deleted presentation download → 404 | error | P1 | authored ✓ (API `Download_an_unknown_presentation_returns_404`) |
| E2E-MOB202-005 | Anonymous list → 401 | auth | P0 | authored ✓ (API `List_without_a_token_returns_401`) |
| E2E-MOB202-006 | RTL — Arabic session title / speaker from the same item | rtl | P2 | covered (models `localized*` getters) |

## Scenarios

```gherkin
Feature: Session presentations (approved account, Figma 1388:7621, GET /app/presentations)

Scenario: List and download a deck
  Given an admin uploaded the deck "deck.pdf" for a session by "Speaker One"
  When an approved visitor GETs /api/v1/app/presentations
  Then it returns 200 with the deck, its session title and "Speaker One"
  When the visitor GETs /api/v1/app/presentations/{id}/file
  Then it returns 200 with the file bytes

Scenario: Empty catalogue
  Given no active presentations exist
  When the visitor opens /session-presentations
  Then the screen shows "No presentations available yet."

Scenario: Download a missing file
  Given a presentation id that does not exist
  When the visitor GETs /api/v1/app/presentations/{id}/file
  Then it returns 404

Scenario: The read requires an approved account
  Given no bearer token
  When a client GETs /api/v1/app/presentations
  Then it returns 401
```

**Evidence:** screen tests (2 — list+download button, empty); models test (2 —
decode + bilingual, defaults); API tests (3 — list+download, 404, 401).

---

_Last reviewed:_ `2026-06-26` by `SIMF Team`.
