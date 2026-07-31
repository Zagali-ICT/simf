# Hosted background workers

| | |
|--|--|
| **Surface** | Backend — `IHostedService`s registered in `SIMF.Infrastructure/DependencyInjection.cs` |
| **Source** | `src/Backend/SIMF.Infrastructure/Operations/` |
| **Monitoring** | Every worker registers with `IWorkerHeartbeatRegistry`, surfaced at `/admin/ops/services` and `/health` |
| **Last reviewed** | 2026-07-31 |

## Purpose

Time-based behaviour nobody triggers: reminders, releases, prompts, pushes. Each
worker owns one guarantee and states its dedup strategy explicitly, because "fires
exactly once" is the property that is easiest to lose and hardest to notice.

## Roster

| Worker | Poll | Guarantee | Dedup |
|---|---|---|---|
| `RegistrationGateAutoCloseWorker` | 1 min | Closes the registration gate on schedule | state transition |
| `SessionReminderWorker` | 1 min | "Session starting soon" to booked attendees, 30 min ahead | `Session.ReminderSent` claim stamp (D-217), committed **before** dispatch |
| **`SessionNotAttendedReminderWorker`** | 1 min | **FR-903** — "the session started and you have not arrived" | D-713 dispatcher guard, per (attendee, session) |
| `MeetingReminderWorker` | 1 min | 15-min meeting reminder (email + app) | `ReminderSent` on the request row |
| `MeetingAwaitingSpeakerExpiryWorker` | — | Reverts a stuck `AwaitingSpeaker` request to Pending, freeing the slot | status transition |
| `ReservationNoShowReleaseWorker` | — | Releases no-show seats 3 min before start | `ReleasedAt` on the reservation |
| **`MatchRecommendationPushWorker`** | 15 min | **FR-803** — pushes >=80% matches to opted-in attendees | D-713 dispatcher guard, per (caller, candidate) |
| `SessionRatingPromptWorker` | — | End-of-session rating prompt | D-713 dispatcher guard |
| `ProgrammeRatingPromptWorker` | — | End-of-day / end-of-programme rating prompt | D-713 dispatcher guard |
| `HallAttendanceCloseoutWorker` | — | Closes open attendance rows whose session ended | `Leave` stamp |
| `NotificationBroadcastWorker` | — | Fans out an admin broadcast | broadcast state |

## `SessionNotAttendedReminderWorker` (FR-903)

Fires between **Start + 10 min** (`ArrivalGrace`) and **Start + 30 min**
(`ArrivalGrace + ReminderWindow`), to every holder of an active reservation with **no
`HallAttendance` row** for the session. Skips a session whose `End` has passed —
nudging someone to attend a finished session is noise, not a reminder.

**Why no stamp column.** D-217's reminder is once per *session*, so a per-session
stamp fits. This one is once per *(attendee, session)*: two holders of one session must
both be nudged, which a per-session stamp cannot express and a per-attendee stamp would
need a new table for. `NotificationRequest.DeduplicateByRelatedEntity` (D-713) already
gives exactly one-per-(user, kind, entity), which makes the scan idempotent by
construction — no claim/commit dance, no crash window, no migration. The
`ReminderWindow` is what bounds the repeated scanning.

## `MatchRecommendationPushWorker` (FR-803)

Takes **25 callers per tick**, ordered by user id, resuming after the previous tick's
last id and wrapping at the end; over a few hours the opted-in roster is covered. Only
profiles with `ShowInMeetLikeYou = true` **and** at least one interest enter a batch —
the opt-out is enforced at batch selection, before the ranker sees the caller, as well
as inside the ranker's own candidate query.

**Why batched.** The ranker does a full candidate scan per caller, so scoring everyone
in one tick would be O(n²) in a burst. **Why an in-memory cursor.** Losing it on
restart costs nothing, because the D-713 guard makes re-running a batch a no-op.

Only candidates whose **normalised** score reaches
`RecommendationService.StrongMatchThreshold` (0.80) are pushed; the ordinary
`meet-like-you` browse read still returns the best N regardless of strength, which is
right for a surface the user chose to open.

## Shared conventions

- **Startup delay** of 1-2 minutes so migrations and seeding finish before the first
  DB hit.
- **A single item's failure is logged and skipped**, never aborting the batch.
- **Tick failures are caught**, recorded on the heartbeat registry and retried next
  tick — a worker never dies on one bad poll.
- The scan body of each worker is an `internal static` method taking its dependencies
  explicitly, so tests drive it directly instead of the `BackgroundService` loop.

## Tests

`SessionReminderWorkerTests`, `SessionNotAttendedReminderWorkerTests`,
`MatchRecommendationPushWorkerTests`, `MeetingReminderWorkerTests`. E2E:
[`api-session-not-attended-reminder.md`](../../tests/e2e/api-session-not-attended-reminder.md),
[`api-match-recommendation-push.md`](../../tests/e2e/api-match-recommendation-push.md).
