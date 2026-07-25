import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../requests/data/request_models.dart';
import '../../requests/data/requests_repository.dart';

/// D-745 — the اللقاءات الثنائية (bilateral meetings) feed: the signed-in user's
/// **approved + upcoming** bilateral meetings (speaker + delegation kinds only),
/// newest first. This is a filtered view of the shared [myRequestsProvider] — the
/// same `GET /app/my-requests` feed the requests-history page reads, so no extra
/// network call. The full, unfiltered log stays on the requests-history page.
///
/// "Upcoming" = a meeting whose slot date has not passed; an approved meeting with
/// no fixed slot yet (`eventDate == null`) still counts as upcoming (not
/// "done"). Past-dated meetings drop off automatically.
final upcomingMeetingsProvider =
    FutureProvider.autoDispose<List<AppRequestItem>>((ref) async {
  final all = await ref.watch(myRequestsProvider.future);
  // "Upcoming" = the slot **date** has not passed. A meeting earlier today or in
  // progress still counts (it is not "done" — only a past *date* is); an
  // approved meeting with no slot yet also counts. Compare against the start of
  // the LOCAL day so the cutoff matches the card's "today" label, which also
  // reads local time (MeetingCard._dateLine) — an instant compare would drop a
  // still-today meeting the moment its start time passed.
  final now = DateTime.now();
  final startOfToday = DateTime(now.year, now.month, now.day);
  return all
      .where(
        (item) =>
            item.isMeetingKind &&
            item.status == AppRequestStatus.accepted &&
            (item.eventDate == null ||
                !item.eventDate!.toLocal().isBefore(startOfToday)),
      )
      .toList(growable: false);
});
