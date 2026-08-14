/// Shared session-state model (owner 2026-07-14) — the single place that
/// classifies a session's **time-phase** so the four session surfaces (session
/// detail, session summaries, my sessions, agenda) gate their icons/buttons off
/// the same rule instead of each re-deriving it inline.
///
/// The phase is orthogonal to the two other state axes already on the wire:
/// - [SessionStatus] — the editorial broadcast lifecycle (Scheduled/Held/
///   Recorded/Published), and
/// - the capability flags — `hasLiveStream` (a live online feed),
///   `hasPublishedSummary` (a published محضر), `hasRecording` (a replay).
///
/// A session can be [ended] yet carry no summary, or [live] with no online
/// stream (in-hall only) — so callers combine the phase with the flag they
/// need (e.g. the summary button = `hasPublishedSummary`; the live button =
/// `hasLiveStream && phase == live`).
library;

import 'package:simf_app/features/sessions/data/session_models.dart' show SessionStatus;

/// The broadcast time-phase of a session relative to "now", from its
/// `start`/`end` window.
enum SessionPhase {
  /// Before `Start` — still to come. No summary, no live feed yet.
  upcoming,

  /// Now within `[Start, End)` — happening (in the hall / streaming).
  live,

  /// At or after `End` — finished (may have a recording / summary).
  ended,
}

/// Classifies a session's [SessionPhase] from its `[start, end)` window
/// against [nowUtc] (all three on the same clock). Start-inclusive,
/// end-exclusive: exactly at
/// the start it is [SessionPhase.live], exactly at the end it is
/// [SessionPhase.ended]. A zero-length or inverted window still resolves
/// deterministically (before start → upcoming, else → ended).
SessionPhase sessionPhase(DateTime start, DateTime end, DateTime nowUtc) {
  if (nowUtc.isBefore(start)) {
    return SessionPhase.upcoming;
  }
  if (nowUtc.isBefore(end)) {
    return SessionPhase.live;
  }
  return SessionPhase.ended;
}
