# E2E test catalogue — System-wide local-time display (Saudi AST, UTC+3)

> **Authority:** owner directive 2026-07-18 — the whole system (Control Panel
> and app) must display Saudi local time; storage stays UTC. Cross-cutting
> behaviour test (not tied to one page). Backed by `SIMF.Common.SaudiTime`
> (fixed +03:00, no DST) and its unit tests `SaudiTimeTests`.

| | |
|--|--|
| **Feature** | UTC storage → Saudi (AST, UTC+3, no DST) display, everywhere |
| **Route(s)** | representative: `/admin/sessions`, `/account/notifications`, `/admin/sessions` add/edit; app session detail + notifications |
| **Surface** | Control Panel + Mobile |
| **Test runner** | Chrome DevTools MCP + PowerShell driver (CP); device/emulator (app) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via `Get-Totp` helper |
| **Helper under test** | [`SaudiTime.cs`](../../../src/Shared/SIMF.Common/SaudiTime.cs) · unit tests [`SaudiTimeTests.cs`](../../../tests/SIMF.Application.Tests/SaudiTimeTests.cs) |
| **Last reviewed** | 2026-07-21 (residue sweep: ops/services, sessions/live-hall, session-summaries AI-draft label) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-TZ-001 | A known UTC instant renders +3h on a CP list | happy | P0 | authored |
| E2E-TZ-002 | Near-midnight UTC lands on the correct local calendar day | edge | P0 | authored |
| E2E-TZ-003 | No render still labels a Saudi-local value as `UTC` | regression | P1 | authored |
| E2E-TZ-004 | datetime-local edit round-trips (Saudi in → UTC stored → Saudi out) | happy | P0 | to author |
| E2E-TZ-005 | App shows the same Saudi-local time as the CP for one instant | consistency | P0 | to author |
| E2E-TZ-006 | Missing timezone id never throws (fixed-offset guarantee) | resilience | P2 | authored |

## Scenarios

### E2E-TZ-001 — A known UTC instant renders +3h on a CP list

```gherkin
Feature: Saudi-local display of stored UTC times
  As an administrator
  I want every date/time shown in Saudi local time
  So that the schedule I read matches the event's wall clock

Background:
  Given an Administrator is signed in
  And a Session exists with Start = 2026-11-20T09:00:00Z and End = 2026-11-20T10:00:00Z

Scenario: Sessions list shows the Saudi wall clock, not raw UTC
  When the administrator opens /admin/sessions
  Then the row's start time reads "2026-11-20 12:00"
  And the end time reads "2026-11-20 13:00"
  And no cell shows "09:00" (the raw UTC value) or a "UTC" suffix
```

**Evidence captured:**
- Screenshot: `docs/screenshots/tz-sessions-list.png`
- Console errors: 0 expected; Network failures: 0 expected
- Cross-check: the persisted `Session.Start` is still `09:00:00Z` (display-only change).

### E2E-TZ-002 — Near-midnight UTC lands on the correct local calendar day

```gherkin
Scenario: A 22:30Z instant shows as 01:30 the NEXT local day
  Given a Notification created at 2026-11-20T22:30:00Z
  When the administrator opens /account/notifications
  Then its timestamp reads "2026-11-21 01:30"
  And the calendar day shown is the 21st, not the 20th
```

### E2E-TZ-003 — No render still labels a Saudi-local value as `UTC`

```gherkin
Scenario: The stale "UTC" literal is gone from converted pages
  Given the local-time sweep has been applied
  When the administrator views any converted list (e.g. /admin/business-meetings)
  Then no visible timestamp is suffixed with the literal "UTC"
  And the displayed time equals the stored UTC + 3 hours

Scenario: The 2026-07-21 residue sweep converts the pages added after the first wave
  Given the residue sweep routed the newer CP render sites through SaudiTime
  When the administrator views /admin/ops/services (worker heartbeats + "refreshed at")
  And the /admin/sessions/live-hall arrival times
  And the AI-draft capture label on /admin/session-summaries
  Then every timestamp reads on the Saudi wall clock (UTC + 3 hours)
  And none is rendered via server .ToLocalTime() or suffixed "UTC"
```

### E2E-TZ-004 — datetime-local edit round-trips (Saudi in → UTC stored → Saudi out)

```gherkin
Scenario: Editing a session start in Saudi local persists the correct UTC
  Given a Session with Start = 2026-11-20T09:00:00Z
  When the administrator opens the Sessions edit form
  Then the "start" datetime-local input shows "2026-11-20T12:00" (Saudi wall clock)
  When the administrator changes it to "2026-11-20T14:30" and saves
  Then Session.Start is persisted as 2026-11-20T11:30:00Z
  And re-opening the list shows the start as "2026-11-20 14:30"
```

**Note:** covers the `FromSaudiWallClock` inverse on the fill + save path of every
`<input type="datetime-local">` (Sessions/Banners/BusinessMeetings/Hall/MeetingTables/
Operations/SpeakerAvailability add-edit).

### E2E-TZ-005 — App shows the same Saudi-local time as the CP for one instant

```gherkin
Scenario: One session, identical wall-clock time on CP and app
  Given a Session with Start = 2026-11-20T09:00:00Z
  When the administrator reads it on /admin/sessions (CP)
  And a signed-in visitor opens the same session detail in the app
  Then both show the start as 12:00 (Saudi), regardless of the app device's own timezone
```

### E2E-TZ-006 — Missing timezone id never throws (fixed-offset guarantee)

```gherkin
Scenario: Conversion works on a host without the "Arab Standard Time" id
  Given the server has no Windows "Arab Standard Time" nor IANA "Asia/Riyadh" entry
  When any timestamp is formatted via SaudiTime.FormatSaudi
  Then it still renders the +03:00 wall clock and never throws
  # Guaranteed by construction: SaudiTime uses a fixed TimeSpan(3h), not a TimeZoneInfo lookup.
```

**Evidence captured:** `SaudiTimeTests` (8 unit tests) prove the offset, the
near-midnight day boundary, the nullable handling, and the save→render round-trip.

---

_Last reviewed:_ 2026-07-21 by SIMF Team.
