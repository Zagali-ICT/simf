import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// App-local data layer for the notification bell's unread count
/// (`GET /app/account/notifications/unread-count`, signed-in only — Page_013 E1).
///
/// Returns the integer unread count; throws [ApiFailure] on a wire error. The
/// best-effort gating (guest → 0, silent failure → 0) lives in
/// [unreadNotificationCountProvider], not here — the repository stays a thin,
/// honest wrapper over the one shared [SimfApiClient].
class NotificationsRepository {
  NotificationsRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/account/notifications/unread-count` →
  /// `{ unreadCount }` (`SIMF.Contracts.Notifications.UnreadCountResponse`).
  Future<int> getUnreadCount() {
    return _client.get<int>(
      '/app/account/notifications/unread-count',
      decodeData: (data) =>
          (data is Map ? data['unreadCount'] as int? : null) ?? 0,
    );
  }
}

final notificationsRepositoryProvider =
    Provider<NotificationsRepository>((ref) {
  return NotificationsRepository(ref.watch(simfApiClientProvider));
});

/// The notification-bell unread count for Home (Page_013 Logic L-5).
///
/// **Signed-in only** — a guest never calls it and resolves to `0`. **Best
/// effort** — any wire error resolves to `0` so Home never blocks or shows an
/// error for the badge. Recomputes when the auth state changes.
final unreadNotificationCountProvider =
    FutureProvider.autoDispose<int>((ref) async {
  final auth = ref.watch(authControllerProvider);
  if (auth is! AuthStateSignedIn) {
    return 0;
  }
  try {
    return await ref.read(notificationsRepositoryProvider).getUnreadCount();
  } on ApiFailure {
    return 0;
  }
});
