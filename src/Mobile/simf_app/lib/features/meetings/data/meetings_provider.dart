import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/data/requests_repository.dart';

/// D-745 / R9 (D-767) — the اللقاءات الثنائية (bilateral meetings) feed:
/// **ALL** of the signed-in user's bilateral meeting requests (speaker +
/// delegation kinds), **any status** (pending / accepted / rejected /
/// cancelled), newest first.
///
/// Owner: the meeting page must "load my requests" — so it shows the full
/// picture (a just-submitted Pending request appears immediately, each row
/// carrying its status), not only the accepted + upcoming subset it showed
/// before. This is a filtered view of the shared [myRequestsProvider] — the
/// same `GET /app/my-requests` feed the requests-history page reads — so it
/// costs no extra network call.
final myMeetingRequestsProvider =
    FutureProvider.autoDispose<List<AppRequestItem>>((ref) async {
  final all = await ref.watch(myRequestsProvider.future);
  final meetings = all.where((item) => item.isMeetingKind).toList()
    ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
  return meetings;
});
