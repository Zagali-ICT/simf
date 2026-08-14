import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/myarea/data/my_sessions_models.dart';
import 'package:simf_app/features/myarea/data/myarea_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for the "my sessions" list — App "تفاصيل الجلسات" (Figma
/// 1388:9067). One read of the caller's booked / joined sessions (`GET
/// /app/account/sessions`, `RequireApprovedAccount`), each enriched with the
/// per-user heart + attended flag. Throws [ApiFailure] on a wire error — the
/// screen maps it to error + retry.
class MySessionsRepository {
  MySessionsRepository(this._client);

  final SimfApiClient _client;

  Future<MyAreaSessions> getMySessions() {
    return _client.get<MyAreaSessions>(
      MyAreaEndpoints.sessions,
      decodeData: MyAreaSessions.fromData,
    );
  }
}

final mySessionsRepositoryProvider = Provider<MySessionsRepository>((ref) {
  return MySessionsRepository(ref.watch(simfApiClientProvider));
});

/// The caller's "my sessions" list (auto-disposed so it re-reads each visit).
final mySessionsProvider =
    FutureProvider.autoDispose<MyAreaSessions>((ref) async {
  return ref.watch(mySessionsRepositoryProvider).getMySessions();
});
