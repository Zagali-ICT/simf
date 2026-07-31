# Movement / dwell / route tracking — `/api/v1/app/movement/*`, `/api/v1/admin/movement/*`

| | |
|--|--|
| **Routes** | `POST /app/movement/pings` · `GET /admin/movement/dwell` · `GET /admin/movement/route/{userId}` |
| **Surface** | App API (capture) + Admin API (reporting) |
| **Auth** | Capture: `RequireApprovedAccount`, **self-only**, rate-limited (`auth` policy). Reports: `Attendance.View` + `RequireApprovedAccount`. |
| **Source** | `src/Backend/SIMF.Api/Endpoints/Sessions/MovementTrackingEndpoints.cs` · `src/Backend/SIMF.Infrastructure/Programme/MovementTrackingService.cs` · `src/Backend/SIMF.Domain/Programme/DevicePositionPing.cs` |
| **Migration** | `App/20260731043000_FR1103_AddDevicePositionPings` |
| **Tests** | `tests/SIMF.Api.Tests/MovementTrackingTests.cs` · E2E [`api-movement-tracking.md`](../../tests/e2e/api-movement-tracking.md) |
| **Last reviewed** | 2026-07-31 |

## Purpose

FR-1103. `HallAttendance` answers "did this person come to that session" — an
arrival/departure pair, and deliberately nothing more. It cannot answer "how long did
people spend in the exhibition hall" or "what path did this delegate take through the
venue". Those need a continuous track, which needs a capture path, which did not
exist: a repo-wide search for `dwell|MovementTrack` found only two comments saying the
feature was deferred.

## Inert until halls have boundaries

A ping binds to a hall by testing it against that hall's configured geofence
(`Hall.GeofenceCenterLat` / `Lon` / `RadiusMeters`, columns that already existed for
the D-240 arrival check). While no hall has one — the state the system ships in —
every ping lands with a null `HallId`, both reports return nothing, and the capture
response reports `matchedToHall: 0` so the caller can tell the feature is dormant.
Nothing else in the system reads these rows.

That is owner decision **Q6**: build it CP-configurable now, and let configuring a
boundary switch it on, rather than block the whole feature on the G-OI-2
venue-boundary answer.

## Privacy

Unlike `HallAttendance` — which keeps only derived enter/leave times — these rows hold
**raw coordinates**, because a route cannot be projected without them. GPS is
sensitive personal data (FDS-003 §10), so three controls are load-bearing:

1. Capture is **self-only**: the attendee id comes from the `sub` claim and never the
   body.
2. Both reports are gated on `Attendance.View`.
3. Every report needs an explicit window, capped at **7 days**
   (`MovementWindow.MaxSpan`). There is no unbounded read over raw GPS.

## Contract

### `POST /app/movement/pings`

```jsonc
{ "samples": [ { "capturedAt": "2026-11-23T09:05:00+03:00",
                 "lat": 24.7136, "lon": 46.6753, "accuracyMeters": 8 } ] }
```

Up to 200 samples per upload (`MaxSamplesPerUpload`) — a device batching an offline
stretch is normal, an unbounded batch is not. `capturedAt` is the **device** clock and
is kept distinct from the server's `CreatedAt` so a late upload still orders correctly.
Response: `{ "accepted": n, "matchedToHall": m }`.

### `GET /admin/movement/dwell?from=&to=`

Per hall: distinct attendees seen inside, total dwell minutes, average per attendee.
Ordered by total dwell descending. Halls with no pings are absent.

### `GET /admin/movement/route/{userId}?from=&to=`

The attendee's pings collapsed into ordered legs, each with `hallId` (null for the walk
between halls), enter, leave and dwell minutes.

## Aggregation rules

These are judgement calls, not mechanics, and each has a test:

- **Dwell is the time between consecutive pings**, so a single isolated ping
  contributes zero — it evidences presence, not duration.
- **A gap longer than 10 minutes (`MaxLegGap`) ends the leg** instead of counting as
  dwell. Otherwise a phone that lost signal at the door reads as an all-day attendee.
- **Unmatched legs are kept**, not dropped: dropping them would silently join two
  separate visits to a hall into one.
- **Nearest containing boundary wins** when geofences overlap, so resolution is
  deterministic rather than row-order dependent.

## Schema note

`DevicePositionPings` carries **no FK** to `Hall` or `Session`, even though both live
in the same database: these are telemetry rows written at device cadence, and one must
never be the reason a hall cannot be edited or a session removed. The aggregation
resolves names by lookup and renders a nameless leg when an id no longer resolves. Two
indexes serve the only two reads: `(UserId, CapturedAt)` for the route,
`(HallId, CapturedAt)` for dwell.

## Follow-up

- The CP page for the per-hall boundary triple (`/admin/halls/geofence`,
  `HallGeofence.View` / `.Manage`) is a Control-Panel deliverable and is not built yet.
- The app-side periodic uploader is a Flutter deliverable.
- **D6** — the wider statistics metric list — is still an open owner item; dwell and
  route are the two aggregates FR-1103 itself names.
