import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockSecureStorage extends Mock implements SimfSecureStorage {}

CurrentUser _user(RegistrationStatus status) => CurrentUser(
      id: 'u1',
      email: 'visitor@example.sa',
      displayName: 'Visitor',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: status,
    );

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _user(RegistrationStatus.pending),
    );

ProviderContainer _container(AuthRepository repo, SimfSecureStorage secure) {
  return ProviderContainer(
    overrides: <Override>[
      authRepositoryProvider.overrideWithValue(repo),
      simfSecureStorageProvider.overrideWithValue(secure),
    ],
  );
}

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
  group('AuthController.refreshCurrentUser (Page_011)', () {
    test('returns the fresh user and updates the session on success', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(
        () => repo.signIn(
          email: any(named: 'email'),
          password: any(named: 'password'),
        ),
      ).thenAnswer((_) async => SignInSession(_session()));
      var current = _user(RegistrationStatus.pending);
      when(repo.getCurrentUser).thenAnswer((_) async => current);

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      await _waitFor(container, (s) => s is AuthStateSignedOut);
      final notifier = container.read(authControllerProvider.notifier);
      await notifier.signIn(email: 'visitor@example.sa', password: 'pw');
      expect(
        (container.read(authControllerProvider) as AuthStateSignedIn)
            .session
            .user
            .registrationStatus,
        RegistrationStatus.pending,
      );

      // The admin approves; the next refresh reflects it.
      current = _user(RegistrationStatus.approved);
      final refreshed = await notifier.refreshCurrentUser();

      expect(refreshed.registrationStatus, RegistrationStatus.approved);
      expect(
        (container.read(authControllerProvider) as AuthStateSignedIn)
            .session
            .user
            .registrationStatus,
        RegistrationStatus.approved,
      );
    });

    test('propagates the failure and leaves the session unchanged', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(
        () => repo.signIn(
          email: any(named: 'email'),
          password: any(named: 'password'),
        ),
      ).thenAnswer((_) async => SignInSession(_session()));
      var fail = false;
      when(repo.getCurrentUser).thenAnswer((_) async {
        if (fail) {
          // Reproduces exactly what the real repository does on a wire error:
          // it throws an `AuthFailure`, the package's sealed error channel.
          // ignore: only_throw_errors
          throw const NetworkUnavailable(
            ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'offline'),
          );
        }
        return _user(RegistrationStatus.pending);
      });

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      await _waitFor(container, (s) => s is AuthStateSignedOut);
      final notifier = container.read(authControllerProvider.notifier);
      await notifier.signIn(email: 'visitor@example.sa', password: 'pw');

      fail = true;
      await expectLater(
        notifier.refreshCurrentUser(),
        throwsA(isA<AuthFailure>()),
      );
      final state = container.read(authControllerProvider);
      expect(state, isA<AuthStateSignedIn>());
      expect(
        (state as AuthStateSignedIn).session.user.registrationStatus,
        RegistrationStatus.pending,
      );
    });
  });
}
