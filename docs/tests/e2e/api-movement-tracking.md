# E2E test catalogue — Movement / dwell / route tracking (FR-1103)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md).

| | |
|--|--|
| **Pages** | [`cp-admin-attendance.md`](cp-admin-attendance.md) (the reporting neighbourhood) · app capture screen is Track D's |
| **Routes** | `POST /api/v1/app/movement/pings` · `GET /api/v1/admin/movement/dwell` · `GET /api/v1/admin/movement/route/{userId}` |
| **Surface** | App API (self-service capture) + Admin API (reporting) |
| **Test runner** | xUnit + `SimfApiFactory` (`tests/SIMF.Api.Tests/MovementTrackingTests.cs`) |
| **Auth setup** | Capture: any approved account. Reports: `Attendance.View`. |
| **Last reviewed** | 2026-07-31 |

## What changed and why

`FR-1103-movement-dwell`. There was no capture path and no data source. The only
positional record was `HallAttendance`, which is deliberately an arrival/departure
PAIR — its own XML doc says so: "never the raw coordinates or a continuous track
(that is the deferred movement/dwell feature, FR-1103)". A repo-wide search for
`dwell|MovementTrack` returned only that comment and its echo in
`HallAttendanceService`.

Owner decision **Q6** unblocked it: build it CP-configurable, inert until halls are
given boundaries. This change adds:

- `DevicePositionPings` — an additive table (migration
  `20260731043000_FR1103_AddDevicePositionPings`): userId, hallId, sessionId,
  capturedAt, lat/lon, accuracy.
- `IMovementTrackingService` — capture, dwell-per-hall aggregation, route projection.
- Three endpoints (one capture, two reports).

## Inert by construction

A ping binds to a hall by testing it against that hall's configured geofence
(`Hall.GeofenceCenterLat` / `Lon` / `RadiusMeters` — columns that already existed for
the D-240 arrival check). While no hall has one — the shipped state — every ping
lands with a null `HallId`, both reports return nothing, and `matchedToHall` in the
capture response is 0. The caller can therefore *tell* the feature is dormant rather
than guess. Nothing else in the system reads these rows.

## Privacy posture

Unlike `HallAttendance`, these rows DO hold raw coordinates — a route projection
cannot be derived without them — and GPS is sensitive personal data (FDS-003 §10).
Three consequences are load-bearing and each has a scenario below:

1. The capture endpoint is **self-only**: the attendee id comes from the `sub` claim
   and never from the body, so a caller cannot post anybody else's position.
2. Both reports are gated on `Attendance.View`.
3. Every report requires an explicit, bounded window (max 7 days) — there is no
   unbounded read over raw GPS.

## No FK, on purpose

`HallId` / `SessionId` are bare `Guid`s with plain indexes and no foreign key, even
though both tables live in the same database. These are telemetry rows written at
device cadence; one must never be the reason a hall cannot be edited or a session
removed. The aggregation resolves names by lookup and renders a leg without a name
when the id no longer resolves.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOV-001 | Pings inside a configured boundary bind to that hall | happy | P0 | automated |
| E2E-MOV-002 | A hall with no boundary matches nothing (inert) | happy | P0 | automated |
| E2E-MOV-003 | Dwell sums the time between consecutive in-hall pings | happy | P0 | automated |
| E2E-MOV-004 | A long silence is not counted as presence | happy | P0 | automated |
| E2E-MOV-005 | Route collapses the track into ordered legs | happy | P0 | automated |
| E2E-MOV-006 | Reports require `Attendance.View` | auth | P0 | automated |
| E2E-MOV-007 | The reporting window is required and bounded | error | P0 | automated |
| E2E-MOV-008 | Capture requires an approved account | auth | P0 | automated |
| E2E-MOV-009 | An out-of-range coordinate is rejected | error | P1 | code-reviewed |
| E2E-MOV-010 | An oversized batch is rejected | error | P1 | code-reviewed |
| E2E-MOV-011 | A dangling hall id renders a nameless leg, not an error | resilience | P1 | code-reviewed |
| E2E-MOV-012 | An out-of-range accuracy radius (negative / absurd) is rejected | error | P1 | _to author_ |
| E2E-MOV-013 | A sample omitting accuracy is still accepted (the field is optional) | happy | P1 | _to author_ |

## Scenarios

### E2E-MOV-001 — Pings bind to the containing hall

```gherkin
Feature: Movement capture
  As the event operations team
  I want a device-position trail
  So that dwell per hall and an attendee's route can be reported

Background:
  Given hall "Movement Test Hall" has a geofence centred on 24.7136, 46.6753 with radius 150 m
  And an approved visitor is signed in

Scenario: Only the samples inside the boundary bind to the hall
  When the device POSTs /api/v1/app/movement/pings with 3 samples
    | capturedAt | lat     | lon     |
    | T+0        | 24.7136 | 46.6753 |
    | T+5m       | 24.7136 | 46.6753 |
    | T+10m      | 24.7236 | 46.6753 |
  Then the response is 200
  And data.accepted is 3
  And data.matchedToHall is 2
  And exactly 2 DevicePositionPings rows carry that hall's id
```

The third sample is ~1.1 km away, well outside a 150 m radius. When two geofences
overlap, the **nearest containing** boundary wins, so the resolution is deterministic
rather than row-order dependent.

**Evidence captured:** `MovementTrackingTests.Pings_inside_a_configured_boundary_bind_to_that_hall`.

### E2E-MOV-002 — Inert until a hall has a boundary

```gherkin
Scenario: Standing inside an un-bounded hall matches nothing
  Given a hall with no GeofenceCenterLat / Lon / RadiusMeters configured
  And an approved visitor is signed in
  When the device POSTs one sample taken at that hall's location
  Then the response is 200
  And data.accepted is 1
  And data.matchedToHall is 0
```

This is Q6's whole premise: the capture path ships now and stays dormant. No error,
no partial feature — just silence until the boundaries are configured from the CP.

**Evidence captured:** `MovementTrackingTests.A_hall_with_no_boundary_matches_nothing`.

### E2E-MOV-003 — Dwell aggregation

```gherkin
Scenario: Dwell is the time BETWEEN consecutive in-hall pings
  Given one attendee pinged inside the hall at T+0, T+5m and T+10m
  When the dwell report is requested for a window containing them
  Then that hall reports 1 distinct attendee
  And 10 total dwell minutes
  And 10 average dwell minutes
```

A single isolated ping contributes zero: it evidences presence, not duration.

**Evidence captured:** `MovementTrackingTests.Dwell_aggregation_sums_time_between_consecutive_pings_in_a_hall`.

### E2E-MOV-004 — Silence is not presence

```gherkin
Scenario: A device that went dark for an hour did not spend that hour in the hall
  Given one attendee pinged inside the hall at T+0 and again at T+1h
  When the dwell report is requested
  Then that hall reports 1 distinct attendee
  And 0 total dwell minutes
```

A gap longer than `MaxLegGap` (10 minutes) ends the leg rather than being counted.
Without this rule, a phone that lost signal at the door would read as an all-day
attendee.

**Evidence captured:** `MovementTrackingTests.Dwell_aggregation_does_not_count_a_long_silence_as_presence`.

### E2E-MOV-005 — Route projection

```gherkin
Scenario: The track collapses into ordered legs, including the walk between halls
  Given one attendee pinged
    | T+0, T+4m   | inside the hall  |
    | T+8m        | outside any hall |
    | T+12m, T+16m| inside the hall  |
  When the route is requested for that attendee
  Then 3 legs are returned, ordered by entry time
  And leg 1 is in the hall with 4 dwell minutes
  And leg 2 has a null hallId (the walk)
  And leg 3 is back in the hall
```

Unmatched legs are kept rather than dropped: "where was the attendee between two
halls" is a legitimate question, and dropping them would silently join two separate
visits into one.

**Evidence captured:** `MovementTrackingTests.Route_projection_collapses_the_track_into_ordered_legs`.

### E2E-MOV-006 — Permission gate

```gherkin
Scenario: An ordinary attendee cannot read movement reports
  Given an approved visitor with no admin permissions is signed in
  When they GET /api/v1/admin/movement/dwell?from=...&to=...
  Then the response is 403
  When they GET /api/v1/admin/movement/route/{someone-elses-id}?from=...&to=...
  Then the response is 403
```

**Evidence captured:** `MovementTrackingTests.Movement_reports_require_the_attendance_view_permission`.

### E2E-MOV-007 — Bounded window

```gherkin
Scenario: There is no unbounded read over raw GPS
  Given an Administrator holding Attendance.View is signed in
  When they GET /api/v1/admin/movement/dwell with no from/to
  Then the response is 400 with error.code "VALIDATION_FAILED"
  When they GET it with a 30-day window
  Then the response is 400 with error.code "VALIDATION_FAILED"
```

`MovementWindow.MaxSpan` is 7 days. It bounds the raw-ping scan and keeps a
personal-data read narrow.

**Evidence captured:** `MovementTrackingTests.Reporting_window_is_required_and_bounded`.

### E2E-MOV-008 — Capture auth gate

```gherkin
Scenario: Anonymous capture is refused
  When a client with no Authorization header POSTs /api/v1/app/movement/pings
  Then the response is 401
```

The route is also rate-limited under the `auth` policy.

**Evidence captured:** `MovementTrackingTests.Capture_endpoint_requires_an_approved_account`.

### E2E-MOV-009 / E2E-MOV-010 / E2E-MOV-012 / E2E-MOV-013 — Input guards

```gherkin
Scenario: An out-of-range coordinate is refused
  When the device POSTs a sample with lat = 95
  Then the response is 400 with error.code "VALIDATION_FAILED"

Scenario: An oversized batch is refused
  When the device POSTs 201 samples
  Then the response is 400 with error.code "VALIDATION_FAILED"

Scenario: An out-of-range accuracy radius is refused
  When the device POSTs a sample with accuracyMeters = -5
  Then the response is 400 with error.code "VALIDATION_FAILED"

  When the device POSTs a sample with accuracyMeters = 250000
  Then the response is 400 with error.code "VALIDATION_FAILED"

Scenario: A sample that reports no accuracy is still accepted
  When the device POSTs a sample with accuracyMeters omitted
  Then the response is 200 and the sample is stored
```

A device batching a long offline stretch is normal; an unbounded batch is not
(`MaxSamplesPerUpload` = 200).

The accuracy radius is bounded at `MaxAccuracyMeters` = 10,000 m. The coordinates
beside it were already range-checked while the radius was stored verbatim, so any
number at all reached the column. A consumer GPS fix is good to single-digit
metres and even a coarse cell-tower fix to a few thousand; past that the sample
locates nothing a venue-scale geofence can use. The field stays **optional** on
the wire — `accuracyMeters` is nullable and a device that reports none must keep
working, so the bound must never turn an absent value into a 400.

### E2E-MOV-011 — Dangling hall id

```gherkin
Scenario: A hall removed after the pings were taken does not break the report
  Given pings bound to a hall that no longer exists
  When the route is requested
  Then the leg still reports its hallId, enter, leave and dwell
  And hallName and hallNameArabic are null
  And no error is raised
```

The rows carry no FK by design, so this is a designed-for state rather than a
corruption.

## Follow-up outside this change

- The app-side capture (a periodic position uploader) is Track D's.
- The CP page for per-hall lat/lon/radius lives at `/admin/halls/geofence`
  (`HallGeofence.View` / `.Manage`, added by the Prep agent to `CpNavigation`) and is
  a Control-Panel deliverable — see `docs/_pending/C8.md`.
