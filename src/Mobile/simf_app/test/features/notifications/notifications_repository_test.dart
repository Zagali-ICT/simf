import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import '../../support/simf_test_scope.dart';

CurrentUser _visitor() => CurrentUser(
      id: 'u1',
      email: 'visitor@example.sa',
      displayName: 'Visitor',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: RegistrationStatus.approved,
    );

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _visitor(),
    );

class _SignedInController extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_session());
}

class _GuestController extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();
}

/// A fake repository (via `implements`, so it needs no real [SimfApiClient]):
/// returns a fixed count, or throws when [fail] is set.
class _FakeNotificationsRepository implements NotificationsRepository {
  _FakeNotificationsRepository({this.count = 0, this.fail = false});

  final int count;
  final bool fail;

  @override
  Future<int> getUnreadCount() async {
    if (fail) {
      throw const ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'offline',
      );
    }
    return count;
  }

  @override
  Future<List<NotificationItem>> getNotifications(
          {int skip = 0, int top = 50,}) async =>
      const <NotificationItem>[];

  @override
  Future<bool> markRead(String id) async => true;

  @override
  Future<bool> markAllRead() async => true;
}

void main() {
  group('unreadNotificationCountProvider (Page 013 L-5)', () {
    test('a guest resolves to 0 without calling the repository', () async {
      final container = ProviderContainer(
        retry: simfTestNoRetry,
        overrides: <Override>[
          authControllerProvider.overrideWith(_GuestController.new),
        ],
      );
      addTearDown(container.dispose);

      expect(await container.read(unreadNotificationCountProvider.future), 0);
    });

    test('a signed-in user resolves to the repository count', () async {
      final container = ProviderContainer(
        retry: simfTestNoRetry,
        overrides: <Override>[
          authControllerProvider.overrideWith(_SignedInController.new),
          notificationsRepositoryProvider.overrideWithValue(
            _FakeNotificationsRepository(count: 5),
          ),
        ],
      );
      addTearDown(container.dispose);

      expect(await container.read(unreadNotificationCountProvider.future), 5);
    });

    test('a wire error resolves to 0 (best-effort, silent)', () async {
      final container = ProviderContainer(
        retry: simfTestNoRetry,
        overrides: <Override>[
          authControllerProvider.overrideWith(_SignedInController.new),
          notificationsRepositoryProvider.overrideWithValue(
            _FakeNotificationsRepository(fail: true),
          ),
        ],
      );
      addTearDown(container.dispose);

      expect(await container.read(unreadNotificationCountProvider.future), 0);
    });
  });
}
