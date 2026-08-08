import 'presentation_models.dart';
import 'session_models.dart';

/// Whether a presentation's تحميل (open-summary) button is active (owner
/// 2026-07-14). True only when the matched programme [session] has a published
/// summary — same signal the summaries list filters on. When the programme isn't
/// loaded yet ([session] null) it falls back to the presentation's own start: a
/// not-yet-started session can't have a summary, so its button stays inactive;
/// a started/past one keeps it (a real 404 shows the summary screen's empty note).
bool presentationSummaryReady(
  PresentationItem item,
  SessionListItem? session,
  DateTime nowUtc,
) =>
    session != null
        ? session.hasPublishedSummary
        : !nowUtc.isBefore(item.sessionStart);
