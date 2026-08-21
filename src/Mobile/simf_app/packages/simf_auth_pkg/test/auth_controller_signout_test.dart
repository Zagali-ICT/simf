import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

/// A real in-memory store, not a mock: these tests assert what is LEFT BEHIND
/// after teardown. `clearAuthValues` is deliberately NOT overridden, so the
/// production clear path is the code under test.
class _FakeSecureStorage extends SimfSecureStorage {
  final Map<String, String> values = <String, String>{};

  @override
  Future<String?> read(String key) async => values[key];

  @override
  Future<void> write(String key, String value) async {
    values[key] = value;
  }

  @override
  Future<void> delete(String key) async {
    values.remove(key);
  }
}

CurrentUser _user() => CurrentUser(
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
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 5)),
      user: _user(),
    );

ProviderContainer _container(AuthRepository repo, SimfSecureStorage secure) {
  return ProviderContainer(
    overrides: <Override>[
      authRepositoryProvider.overrideWithValue(repo),
      simfSecureStorageProvider.overrideWithValue(secure),
    ],
  );
}

Future<void> _waitFor(
  ProviderContainer container,
  bool Function(AuthState) test,
) async {
  final completer = Completer<void>();
  final sub = container.listen<AuthState>(
    authControllerProvider,
    (_, next) {
      if (test(next) && !completer.isCompleted) {
        completer.complete();
      }
    },
    fireImmediately: true,
  );
  try {
    await completer.future.timeout(const Duration(seconds: 5));
  } finally {
    sub.close();
  }
}

/// Drives the controller to a persisted signed-in session.
Future<AuthController> _signedInController(
  ProviderContainer container,
  AuthRepository repo,
) async {
  when(
    () => repo.signIn(
      email: any(named: 'email'),
      password: any(named: 'password'),
    ),
  ).thenAnswer((_) async => SignInSession(_session()));
  when(repo.getCurrentUser).thenAnswer((_) async => _user());

  final controller = container.read(authControllerProvider.notifier);
  // Nothing is stored yet, so the cold-start restore settles to signed-out.
  await _waitFor(container, (s) => s is AuthStateSignedOut);
  await controller.signIn(email: 'visitor@example.sa', password: 'x');
  return controller;
}

void main() {
  group('AuthController session teardown', () {
    test('signOut leaves NOTHING in secure storage', () async {
      final repo = _MockAuthRepository();
      final secure = _FakeSecureStorage();
      when(() => repo.signOut(refreshToken: any(named: 'refreshToken')))
          .thenAnswer((_) async {});

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      final controller = await _signedInController(container, repo);

      // Without this the post-signOut emptiness below is vacuously true.
      expect(secure.values.keys, contains(StorageKeys.accessToken));
      expect(secure.values.keys, contains(StorageKeys.refreshToken));
      expect(secure.values.keys, contains(StorageKeys.accessTokenExpiresAtIso));
      expect(secure.values.keys, contains(StorageKeys.currentUserJson));

      await controller.signOut();

      // A leftover token signs the previous user back in on a shared device.
      expect(secure.values, isEmpty);
      expect(container.read(authControllerProvider), isA<AuthStateSignedOut>());
      expect(controller.currentAccessToken(), isNull);
    });

    test('signOut still clears local state when the wire sign-out fails',
        () async {
      final repo = _MockAuthRepository();
      final secure = _FakeSecureStorage();
      when(() => repo.signOut(refreshToken: any(named: 'refreshToken')))
          .thenThrow(
        const RefreshTokenExpired(
          ApiFailure(
            code: ApiErrorCodes.authRefreshTokenExpired,
            message: 'expired',
          ),
        ),
      );

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      final controller = await _signedInController(container, repo);

      await controller.signOut();

      expect(secure.values, isEmpty);
      expect(container.read(authControllerProvider), isA<AuthStateSignedOut>());
    });

    // The 401-interceptor path: a dead refresh token must tear the session
    // down, or the app sits SignedIn on it forever.
    test('refresh() tears the session down when the refresh token is dead',
        () async {
      final repo = _MockAuthRepository();
      final secure = _FakeSecureStorage();

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      final controller = await _signedInController(container, repo);

      when(() => repo.refresh(refreshToken: any(named: 'refreshToken')))
          .thenThrow(
        const RefreshTokenExpired(
          ApiFailure(
            code: ApiErrorCodes.authRefreshTokenExpired,
            message: 'expired',
          ),
        ),
      );

      final ok = await controller.refresh();

      expect(ok, isFalse);
      expect(container.read(authControllerProvider), isA<AuthStateSignedOut>());
      expect(secure.values, isEmpty);
      expect(controller.currentAccessToken(), isNull);
    });

    // D-737 — the deliberate contrast with refresh() above: the proactive
    // guard must NOT sign the user out, so the 30s warning can precede it.
    test('tryRefresh() leaves the session in place when refresh fails',
        () async {
      final repo = _MockAuthRepository();
      final secure = _FakeSecureStorage();

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      final controller = await _signedInController(container, repo);

      when(() => repo.refresh(refreshToken: any(named: 'refreshToken')))
          .thenThrow(
        const RefreshTokenExpired(
          ApiFailure(
            code: ApiErrorCodes.authRefreshTokenExpired,
            message: 'expired',
          ),
        ),
      );

      final ok = await controller.tryRefresh();

      expect(ok, isFalse);
      expect(container.read(authControllerProvider), isA<AuthStateSignedIn>());
      expect(secure.values[StorageKeys.refreshToken], equals('R'));
      expect(controller.currentAccessToken(), equals('A'));
    });
  });
}
