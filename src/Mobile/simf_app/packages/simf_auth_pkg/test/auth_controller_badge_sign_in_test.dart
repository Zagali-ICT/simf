import 'dart:async';

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

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _user(AppRole.guest, RegistrationStatus.pending),
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
  group('AuthController.signInWithBadge (D-738)', () {
    test('a token response signs the user in with the hydrated role', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(secure.clearAuthValues).thenAnswer((_) async {});
      when(
        () => repo.signInWithBadge(
          qrId: any(named: 'qrId'),
          password: any(named: 'password'),
        ),
      ).thenAnswer((_) async => SignInSession(_session()));
      when(repo.getCurrentUser).thenAnswer(
        (_) async => _user(AppRole.visitor, RegistrationStatus.approved),
      );

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      await _waitFor(container, (s) => s is AuthStateSignedOut);

      await container
          .read(authControllerProvider.notifier)
          .signInWithBadge(qrId: 'ABCDEFGH2345', password: 'pw');

      final state = container.read(authControllerProvider);
      expect(state, isA<AuthStateSignedIn>());
      expect(
        (state as AuthStateSignedIn).session.user.appRole,
        equals(AppRole.visitor),
      );
      verify(() => repo.signInWithBadge(qrId: 'ABCDEFGH2345', password: 'pw'))
          .called(1);
    });

    test('a 2FA challenge awaits OTP carrying the MASKED display email',
        () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(
        () => repo.signInWithBadge(
          qrId: any(named: 'qrId'),
          password: any(named: 'password'),
        ),
      ).thenAnswer((_) async => const SignInOtpChallenge('otp-token'));

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      await _waitFor(container, (s) => s is AuthStateSignedOut);

      await container.read(authControllerProvider.notifier).signInWithBadge(
            qrId: 'ABCDEFGH2345',
            password: 'pw',
            displayEmail: 'k***@example.com',
          );

      final state = container.read(authControllerProvider);
      expect(state, isA<AuthStateAwaitingOtp>());
      // The verify-otp screen shows the masked email, never the real address.
      expect((state as AuthStateAwaitingOtp).email, 'k***@example.com');
    });
  });
}
