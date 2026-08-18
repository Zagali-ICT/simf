import 'package:simf_app/features/sessions/data/programme_day.dart';
import 'package:simf_app/features/sessions/data/session_enums.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';

/// The two top filter pills (Page_016): Upcoming (still to come) and Forum (the
/// whole programme). Filtering is **client-side** over the cached list (L-1).
enum SessionsView { upcoming, forum }

/// Pure client-side filter over the cached programme (Page_016 L-1): [view]
/// (Upcoming = `start >= nowUtc`, L-2), an optional [localDay] (matched on
/// the session's device-local calendar day), and a free-text [query] over
/// title/description/code (both languages). Input order is preserved (the
/// server returns the list time-ordered, L-5).
List<SessionListItem> filterSessions(
  List<SessionListItem> items, {
  required SessionsView view,
  required DateTime nowUtc,
  DateTime? localDay,
  String query = '',
}) {
  return items.where((session) {
    if (view == SessionsView.upcoming && session.start.isBefore(nowUtc)) {
      return false;
    }
    if (localDay != null && !sameLocalDay(session.startLocal, localDay)) {
      return false;
    }
    return sessionMatchesQuery(session, query);
  }).toList(growable: false);
}

/// Whether [session] matches a free-text [query] across its title, description
/// and code in BOTH languages, case-insensitively. An empty query matches
/// everything.
///
/// One definition, because there were two. This predicate was also written
/// inline in `sessions_screen.dart`, and only the copy inside [filterSessions]
/// had tests — while only the inline one actually ran, because
/// [filterSessions] had no production caller at all. Five tests were passing
/// over a rule nobody shipped.
bool sessionMatchesQuery(SessionListItem session, String query) {
  final needle = query.trim().toLowerCase();
  if (needle.isEmpty) {
    return true;
  }
  final haystack = <String?>[
    session.title,
    session.titleArabic,
    session.description,
    session.descriptionArabic,
    session.code,
  ].whereType<String>().join(' ').toLowerCase();
  return haystack.contains(needle);
}

/// The sessions of [day] the programme screen shows, given the active type tab
/// and the search box. Input order is preserved (the server returns the day
/// time-ordered).
///
/// Lifted out of the screen's `build()`, which re-derived it on every keystroke
/// and every rebuild (section 2: no data shaping in build).
List<SessionListItem> sessionsForDay(
  ProgrammeDay day, {
  SessionType? type,
  String query = '',
}) => <SessionListItem>[
  for (final session in day.sessions)
    if (type == null || session.type == type)
      if (sessionMatchesQuery(session, query)) session,
];

/// The distinct device-local calendar days present in [items], ascending — the
/// data-driven day strip (Page_016: the event's "remaining days"). Each entry
/// is
/// a midnight-local [DateTime].
List<DateTime> sessionDays(List<SessionListItem> items) =>
    distinctLocalDays(items, (session) => session.startLocal);
