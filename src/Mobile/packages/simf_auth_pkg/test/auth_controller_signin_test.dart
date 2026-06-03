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

Session _guestPayloadSession() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      // The wire AuthUser carries no role, so the decoded user defaults to Guest.
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
  setUpAll(() {
    registerFallbackValue(const RefreshRequest(refreshToken: ''));
  });

  group('AuthController sign-in privilege hydration (D-254 issue B)', () {
    test('sign-in hydrates the real app-role from /app/users/me', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(
        () => repo.signIn(
          email: any(named: 'email'),
          password: any(named: 'password'),
        ),
      ).thenAnswer((_) async => SignInSession(_guestPayloadSession()));
      when(() => repo.getCurrentUser()).thenAnswer(
        (_) async => _user(AppRole.visitor, RegistrationStatus.approved),
      );

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      // Let the cold-start restore settle to signed-out first.
      await _waitFor(container, (s) => s is AuthStateSignedOut);
      await container
          .read(authControllerProvider.notifier)
          .signIn(email: 'visitor@example.sa', password: 'pw');

      final state = container.read(authControllerProvider);
      expect(state, isA<AuthStateSignedIn>());
      expect(
        (state as AuthStateSignedIn).session.user.appRole,
        equals(AppRole.visitor),
      );
    });

    test('email-OTP (2FA) hydrates the real app-role', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(
        () => repo.signIn(
          email: any(named: 'email'),
          password: any(named: 'password'),
        ),
      ).thenAnswer((_) async => const SignInOtpChallenge('otp-token'));
      when(
        () => repo.verifyOtp(
          otpToken: any(named: 'otpToken'),
          code: any(named: 'code'),
        ),
      ).thenAnswer((_) async => _guestPayloadSession());
      when(() => repo.getCurrentUser()).thenAnswer(
        (_) async => _user(AppRole.moderator, RegistrationStatus.approved),
      );

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      await _waitFor(container, (s) => s is AuthStateSignedOut);
      final notifier = container.read(authControllerProvider.notifier);
      await notifier.signIn(email: 'visitor@example.sa', password: 'pw');
      expect(
        container.read(authControllerProvider),
        isA<AuthStateAwaitingOtp>(),
      );

      await notifier.verifyOtp(code: '123456');
      final state = container.read(authControllerProvider);
      expect(
        (state as AuthStateSignedIn).session.user.appRole,
        equals(AppRole.moderator),
      );
    });

    test('an interceptor refresh preserves the resolved privilege', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(
        () => repo.signIn(
          email: any(named: 'email'),
          password: any(named: 'password'),
        ),
      ).thenAnswer((_) async => SignInSession(_guestPayloadSession()));
      when(() => repo.getCurrentUser()).thenAnswer(
        (_) async => _user(AppRole.staff, RegistrationStatus.approved),
      );
      // The refresh payload also carries the role-less Guest user.
      when(() => repo.refresh(refreshToken: any(named: 'refreshToken')))
          .thenAnswer((_) async => _guestPayloadSession());

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      await _waitFor(container, (s) => s is AuthStateSignedOut);
      final notifier = container.read(authControllerProvider.notifier);
      await notifier.signIn(email: 'staff@simf', password: 'pw');
      expect(
        (container.read(authControllerProvider) as AuthStateSignedIn)
            .session
            .user
            .appRole,
        equals(AppRole.staff),
      );

      // A mid-session 401 triggers AuthTokenSource.refresh — privilege must survive.
      final ok = await notifier.refresh();
      expect(ok, isTrue);
      expect(
        (container.read(authControllerProvider) as AuthStateSignedIn)
            .session
            .user
            .appRole,
        equals(AppRole.staff),
      );
    });
  });
}
