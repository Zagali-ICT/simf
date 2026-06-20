# E2E test catalogue — `My meetings` (`mobile-my-meetings`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> D-479 (#11 follow-up). The screen is **read-only**: delegation-meeting
> creation/management lives on the Control Panel (`/admin/delegation-meetings`,
> D-478); speaker meetings are requested from a speaker's profile (D-269/D-477).
> API implementation lives in `tests/SIMF.Api.Tests/MyMeetingsTests.cs`; the
> Flutter screen in `src/Mobile/simf_app/test/features/meetings/my_meetings_screen_test.dart`.

| | |
|--|--|
| **Route** | `GET /api/v1/app/my-meetings` (**approved-only**) · app screen `RouteNames.myMeetings` → `/my-meetings` (reached from My Area) |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget test (screen) |
| **Auth setup** | An **approved Visitor** token (the user only ever sees their own meetings). No literal secrets. |
| **Last reviewed** | 2026-06-20 (D-479) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MMM-001 | The feed returns the user's speaker + delegation requests, newest first | happy | P0 | authored ✓ (`MyMeetingsTests`, API) |
| E2E-MMM-002 | The feed never returns another user's meetings | auth | P0 | authored ✓ (`MyMeetingsTests`, API) |
| E2E-MMM-003 | The screen renders each row with its kind label + a status pill | happy | P0 | authored ✓ (screen) |
| E2E-MMM-004 | A row with a confirmed slot shows the slot time (else the submitted time) | happy | P1 | authored ✓ (screen) |
| E2E-MMM-005 | Empty feed → the empty state; a wire error → retry that re-fetches | edge | P0 | authored ✓ (screen) |
| E2E-MMM-006 | RTL / Arabic render — counterparty name + status pill mirror | i18n | P1 | _to author_ |

## Scenarios

### E2E-MMM-001/002 — The feed is the user's own meetings, newest first

```gherkin
Feature: My meetings (read-only)
  As an approved attendee
  I want to see the meetings I requested and their status
  So that I can track speaker and delegation meetings in one place

Scenario: The feed unifies speaker + delegation requests, newest first
  Given an approved Visitor who requested a speaker meeting (older)
  And the same user is the requester of a delegation meeting (newer)
  When the app calls GET /api/v1/app/my-meetings with the user's token
  Then the response is 200
  And it returns both items, the delegation meeting first (newest)
  And each item has kind, the counterparty title, subject, status, createdAt

Scenario: The feed is scoped to the caller
  Given a meeting requested by another user
  When a different approved Visitor calls GET /api/v1/app/my-meetings
  Then that other user's meeting is not in the response
```

**Evidence:** `MyMeetingsTests` (green).

### E2E-MMM-003/004/005 — The screen

```gherkin
Scenario: The list renders kind labels and status pills
  Given the feed returns a Pending speaker meeting and an Accepted delegation meeting
  When the My meetings screen loads
  Then it shows the speaker name and "Speaker meeting" with a Pending pill
  And it shows the delegation country and "Delegation meeting" with an Accepted pill

Scenario: A confirmed slot shows the slot time
  Given a meeting row carries a slotStartUtc
  Then the row shows the slot time (otherwise it shows the submitted time)

Scenario: Empty and error states
  Given the feed is empty
  Then the screen shows the "You have no meetings yet" empty state
  Given the read fails
  Then the screen shows the error message and a Retry that re-fetches
```

**Evidence:** `my_meetings_screen_test` (4/4, green).

---

_Last reviewed:_ 2026-06-20 by SIMF Team — D-479 (#11 follow-up) read-only My meetings screen.
