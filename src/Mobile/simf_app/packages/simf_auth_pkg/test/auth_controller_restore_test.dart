import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockSecureStorage extends Mock implements SimfSecureStorage {}

CurrentUser _user(AppRole role, RegistrationStatus status) => CurrentUser(
      id: 'u1',
      email: 'visitor@example.sa',
      displayName: 'Visitor',
      appRole: role,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: status,
    );

String _userJson(CurrentUser u) => jsonEncode(<String, dynamic>{
      'id': u.id,
      'email': u.email,
      'displayName': u.displayName,
      'appRole': u.appRole.wireName,
      'preferredLanguage': u.preferredLanguage.code,
      'registrationStatus': u.registrationStatus.wireName,
      'avatarUrl': u.avatarUrl,
    });

ProviderContainer _container(AuthRepository repo, SimfSecureStorage secure) {
  return ProviderContainer(
    overrides: <Override>[
      authRepositoryProvider.overrideWithValue(repo),
      simfSecureStorageProvider.overrideWithValue(secure),
    ],
  );
}

/// Waits until the auth state satisfies [test]. The cold-start restore passes
/// through an interim signed-in state before privilege hydration settles, so
/// callers wait for the *final* condition, not merely the first non-Initial.
Future<AuthState> _waitFor(
  ProviderContainer container,
  bool Function(AuthState) test,
) async {
  final completer = Completer<AuthState>();
  final sub = container.listen<AuthState>(
    authControllerProvider,
    (_, next) {
      if (test(next) && !completer.isCompleted) {
        completer.complete(next);
      }
    },
    fireImmediately: true,
  );
  try {
    return await completer.future.timeout(const Duration(seconds: 5));
  } finally {
    sub.close();
  }
}

void main() {
  group('AuthController cold-start restore (Page_001 Logic L-4/L-6)', () {
    test('signs out when no refresh token is stored', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      final state = await _waitFor(container, (s) => s is AuthStateSignedOut);
      expect(state, isA<AuthStateSignedOut>());
    });

    test('a valid cached token restores, then hydrates the real privilege',
        () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      final future = DateTime.now().add(const Duration(hours: 1));
      // Cached user is (wrongly) Guest/Pending — proves hydration corrects it.
      when(() => secure.read(StorageKeys.accessToken))
          .thenAnswer((_) async => 'A');
      when(() => secure.read(StorageKeys.refreshToken))
          .thenAnswer((_) async => 'R');
      when(() => secure.read(StorageKeys.accessTokenExpiresAtIso))
          .thenAnswer((_) async => future.toIso8601String());
      when(() => secure.read(StorageKeys.currentUserJson)).thenAnswer(
        (_) async =>
            _userJson(_user(AppRole.guest, RegistrationStatus.pending)),
      );
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(repo.getCurrentUser).thenAnswer(
        (_) async => _user(AppRole.visitor, RegistrationStatus.approved),
      );

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      final state = await _waitFor(
        container,
        (s) =>
            s is AuthStateSignedIn && s.session.user.appRole == AppRole.visitor,
      );
      final user = (state as AuthStateSignedIn).session.user;
      expect(user.appRole, equals(AppRole.visitor));
      expect(user.registrationStatus, equals(RegistrationStatus.approved));
    });

    test('an expired access token refreshes, then hydrates the real privilege',
        () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(StorageKeys.accessToken))
          .thenAnswer((_) async => null);
      when(() => secure.read(StorageKeys.refreshToken))
          .thenAnswer((_) async => 'R');
      when(() => secure.read(StorageKeys.accessTokenExpiresAtIso))
          .thenAnswer((_) async => null);
      when(() => secure.read(StorageKeys.currentUserJson))
          .thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(() => repo.refresh(refreshToken: any(named: 'refreshToken')))
          .thenAnswer(
        (_) async => Session(
          accessToken: 'A2',
          refreshToken: 'R2',
          accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
          user: _user(AppRole.guest, RegistrationStatus.pending),
        ),
      );
      when(repo.getCurrentUser).thenAnswer(
        (_) async => _user(AppRole.staff, RegistrationStatus.approved),
      );

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      final state = await _waitFor(
        container,
        (s) =>
            s is AuthStateSignedIn && s.session.user.appRole == AppRole.staff,
      );
      expect(
        (state as AuthStateSignedIn).session.user.appRole,
        equals(AppRole.staff),
      );
    });

    test('an offline refresh resumes on the cached identity (degraded)',
        () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      final past = DateTime.now().subtract(const Duration(minutes: 1));
      when(() => secure.read(StorageKeys.accessToken))
          .thenAnswer((_) async => 'A');
      when(() => secure.read(StorageKeys.refreshToken))
          .thenAnswer((_) async => 'R');
      when(() => secure.read(StorageKeys.accessTokenExpiresAtIso))
          .thenAnswer((_) async => past.toIso8601String());
      when(() => secure.read(StorageKeys.currentUserJson)).thenAnswer(
        (_) async =>
            _userJson(_user(AppRole.visitor, RegistrationStatus.approved)),
      );
      when(() => repo.refresh(refreshToken: any(named: 'refreshToken')))
          .thenThrow(
        const NetworkUnavailable(
          ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'offline'),
        ),
      );

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      final state = await _waitFor(container, (s) => s is AuthStateSignedIn);
      expect(
        (state as AuthStateSignedIn).session.user.appRole,
        equals(AppRole.visitor),
      );
    });

    test('an expired refresh token signs out', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(StorageKeys.accessToken))
          .thenAnswer((_) async => null);
      when(() => secure.read(StorageKeys.refreshToken))
          .thenAnswer((_) async => 'R');
      when(() => secure.read(StorageKeys.accessTokenExpiresAtIso))
          .thenAnswer((_) async => null);
      when(() => secure.read(StorageKeys.currentUserJson))
          .thenAnswer((_) async => null);
      when(secure.clearAuthValues).thenAnswer((_) async {});
      when(() => repo.refresh(refreshToken: any(named: 'refreshToken')))
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

      final state = await _waitFor(container, (s) => s is AuthStateSignedOut);
      expect(state, isA<AuthStateSignedOut>());
    });

    test('a throwing secure-storage read does not strand the splash (L-6)',
        () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      // Async throw, like real flutter_secure_storage on a corrupt keystore
      // (a synchronous throw would land inside build() before it returns).
      when(() => secure.read(any()))
          .thenAnswer((_) async => throw Exception('corrupt keystore'));

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      final state = await _waitFor(container, (s) => s is AuthStateSignedOut);
      expect(state, isA<AuthStateSignedOut>());
    });

    test('an offline refresh with no cached identity signs out', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(StorageKeys.accessToken))
          .thenAnswer((_) async => null);
      when(() => secure.read(StorageKeys.refreshToken))
          .thenAnswer((_) async => 'R');
      when(() => secure.read(StorageKeys.accessTokenExpiresAtIso))
          .thenAnswer((_) async => null);
      when(() => secure.read(StorageKeys.currentUserJson))
          .thenAnswer((_) async => null);
      when(() => repo.refresh(refreshToken: any(named: 'refreshToken')))
          .thenThrow(
        const NetworkUnavailable(
          ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'offline'),
        ),
      );

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      final state = await _waitFor(container, (s) => s is AuthStateSignedOut);
      expect(state, isA<AuthStateSignedOut>());
    });
  });
}
