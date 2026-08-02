# E2E test catalogue — "Session started, you have not arrived" reminder (FR-903)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md).

| | |
|--|--|
| **Page** | [`mobile-notifications.md`](mobile-notifications.md) (where the nudge lands) |
| **Route** | No HTTP route — `SessionNotAttendedReminderWorker`, a hosted background worker |
| **Surface** | Backend worker → in-app notification |
| **Test runner** | xUnit + `SimfApiFactory` (`tests/SIMF.Api.Tests/SessionNotAttendedReminderWorkerTests.cs`) |
| **Auth setup** | None — the worker runs under the host, not a caller |
| **Last reviewed** | 2026-07-31 |

## What changed and why

`FR-903-not-attended-reminder`. The booking half of FR-903 shipped long ago
(`NotificationKind.BookingConfirmed = 40`, `SessionReminder = 41`,
`BookingReleased = 51`). The not-attended half never did: `NotificationKind` had no
such value across 0-58, and `ReservationNoShowReleaseWorker` — the only worker that
reasons about no-shows at all — merely calls `ReleaseNoShowsAsync` to free the seat.
It notifies nobody.

`NotificationKind.SessionNotAttended = 59` (additive, persisted by name, so no
schema or wire change under the D-110 frozen-enum rule) plus a sibling worker that
fires at Start + grace.

## Dedup — why there is no new column

D-217 gave `SessionReminderWorker` a `Session.ReminderSent` claim stamp because that
reminder is once **per session**. This one is once per **(attendee, session)**: two
holders of the same session must both be nudged. That is exactly the D-713
dispatcher guard — `NotificationRequest.DeduplicateByRelatedEntity` with the session
as the related entity — so the scan is idempotent by construction and needs **no
additive column and no migration**. A restart mid-sweep re-runs harmlessly, and the
`ReminderWindow` bounds how long the sweep keeps re-scanning a session.

## Timing

| Constant | Value | Meaning |
|---|---|---|
| `ArrivalGrace` | 10 min | How long after `Start` a booked attendee is given to arrive |
| `ReminderWindow` | 20 min | How long past the grace the sweep keeps looking |
| `PollInterval` | 1 min | Tick |
| `StartupDelay` | 1 min | Lets migrations + seeding finish first |

A session is also skipped once `End` has passed: nudging someone to attend a
finished session is noise, not a reminder.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SNA-001 | Booked-but-absent attendee is nudged exactly once | happy | P0 | automated |
| E2E-SNA-002 | Attendee who arrived is not nudged | happy | P0 | automated |
| E2E-SNA-003 | Attendee who arrived and left is not nudged | happy | P0 | automated |
| E2E-SNA-004 | Session still inside the grace is not swept | happy | P0 | automated |
| E2E-SNA-005 | Session that has already ended is not swept | happy | P0 | automated |
| E2E-SNA-006 | Released reservation is not nudged | happy | P0 | automated |
| E2E-SNA-007 | One attendee's dispatch failure does not abort the batch | resilience | P1 | code-reviewed |
| E2E-SNA-008 | The nudge renders bilingually in the app notification list | i18n | P1 | manual |

## Scenarios

### E2E-SNA-001 — Booked-but-absent attendee is nudged exactly once

```gherkin
Feature: Not-attended reminder
  As an attendee who booked a seat and lost track of time
  I want a nudge once the session has started without me
  So that I can still get there before my seat is released

Background:
  Given a session "Keynote" that started 15 minutes ago and runs for an hour
  And visitor Khalid holds an active seat reservation for it
  And Khalid has NO HallAttendance row for that session

Scenario: The nudge fires, once
  When the not-attended scan runs
  Then Khalid has exactly 1 notification of kind SessionNotAttended
    with relatedEntityType "Session" and relatedEntityId = the session
  And its severity is Warning
  When the scan runs again inside the same window
  Then Khalid still has exactly 1 such notification
```

**Evidence captured:** `SessionNotAttendedReminderWorkerTests.Booked_attendee_who_has_not_arrived_is_nudged_exactly_once`.

### E2E-SNA-002 / E2E-SNA-003 — Arrival suppresses the nudge

```gherkin
Scenario: Someone who arrived is not told they have not
  Given Khalid has a HallAttendance row for the session
  When the not-attended scan runs
  Then Khalid has no SessionNotAttended notification

Scenario: Someone who arrived and already left is not told they have not
  Given Khalid's HallAttendance row has both Enter and Leave set
  When the not-attended scan runs
  Then Khalid has no SessionNotAttended notification
```

The anti-join tests for **any** `HallAttendance` row, open or closed. Someone who
came and went did attend; only the absence of a row is absence.

**Evidence captured:** `...Attendee_who_has_arrived_is_not_nudged`,
`...Attendee_who_arrived_and_already_left_is_not_nudged`.

### E2E-SNA-004 — Inside the grace

```gherkin
Scenario: A session that started 2 minutes ago is left alone
  Given the session started 2 minutes ago
  When the not-attended scan runs
  Then no notification is written
```

**Evidence captured:** `...Session_still_inside_the_arrival_grace_is_not_swept`.

### E2E-SNA-005 — Already ended

```gherkin
Scenario: A finished session is not swept
  Given a session that started 15 minutes ago and ran for only 5 minutes
  When the not-attended scan runs
  Then no notification is written
```

**Evidence captured:** `...Session_that_has_already_ended_is_not_swept`.

### E2E-SNA-006 — Released reservation

```gherkin
Scenario: A released seat is no longer a booking
  Given Khalid's reservation has ReleasedAt set
  When the not-attended scan runs
  Then Khalid has no SessionNotAttended notification
```

This is what keeps the nudge from arriving *after*
`ReservationNoShowReleaseWorker` has already freed the seat.

**Evidence captured:** `...Released_reservation_is_not_nudged`.

### E2E-SNA-007 — One failure does not abort the batch

```gherkin
Scenario: A single attendee's dispatch failure is contained
  Given 3 booked-but-absent attendees, one of whose dispatches throws
  When the not-attended scan runs
  Then the other 2 are still nudged
  And the failure is logged with the user id and session id
```

Same containment the shipped `SessionReminderWorker` uses.

### E2E-SNA-008 — Bilingual render

```gherkin
Scenario: The nudge reads correctly in both locales
  Given Khalid has a SessionNotAttended notification
  When he opens the app notification list in Arabic
  Then the title reads "بدأت الجلسة"
  And the body names the session in Arabic and reads right-to-left
  When he switches to English
  Then the title reads "The session has started"
```
