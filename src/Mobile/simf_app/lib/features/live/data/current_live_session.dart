import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../sessions/data/session_lifecycle.dart';
import '../../sessions/data/sessions_repository.dart';

/// The id of the session that is in its live time-window right now (the first,
/// in programme order), or null when nothing is live. Backs the Home LIVE
/// banner deep-link: tapping "watch the live stream" opens that session on the
/// live screen (`/live?sessionId=…`) instead of the empty "no live session
/// selected" state; a null id falls back to the screen's event-wide stream.
///
/// Read on tap (not watched), so Home does not fetch the programme on every
/// render — it reuses the shared [programmeSessionsProvider] cache. The list
/// has no per-session `hasLiveStream` flag (that lives on the detail), so the
/// live screen resolves the chosen session's feed/empty state itself.
final currentLiveSessionIdProvider = FutureProvider.autoDispose<String?>((
  ref,
) async {
  final sessions = await ref.watch(programmeSessionsProvider.future);
  final nowUtc = DateTime.now().toUtc();
  for (final session in sessions) {
    if (session.phase(nowUtc) == SessionPhase.live) {
      return session.id;
    }
  }
  return null;
});
