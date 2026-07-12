import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockSecureStorage extends Mock implements SimfSecureStorage {}

CurrentUser _user() => CurrentUser(
      id: 'u1',
      email: 'visitor@example.sa',
      displayName: 'Visitor',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: RegistrationStatus.pending,
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

void main() {
  // D-443 — concurrent 401s (the home screen's parallel loads after the 5-min
  // access token lapses) must share ONE refresh. Without the single-flight
  // guard each would rotate the same refresh token and the server's
  // reuse-detection would revoke the whole session.
  test('concurrent refresh() calls trigger only one repository refresh',
      () async {
    final repo = _MockAuthRepository();
    final secure = _MockSecureStorage();
    when(() => secure.read(any())).thenAnswer((_) async => null);
    when(() => secure.write(any(), any())).thenAnswer((_) async {});
    when(secure.clearAuthValues).thenAnswer((_) async {});
    when(
      () => repo.signIn(
        email: any(named: 'email'),
        password: any(named: 'password'),
      ),
    ).thenAnswer((_) async => SignInSession(_session()));
    when(repo.getCurrentUser).thenAnswer((_) async => _user());

    var refreshCalls = 0;
    when(() => repo.refresh(refreshToken: any(named: 'refreshToken')))
        .thenAnswer((_) async {
      refreshCalls++;
      // Stay in flight long enough that the other callers overlap.
      await Future<void>.delayed(const Duration(milliseconds: 50));
      return _session();
    });

    final container = _container(repo, secure);
    addTearDown(container.dispose);
    final controller = container.read(authControllerProvider.notifier);

    // Let the cold-start restore settle (no stored token → signed out), then
    // sign in so the controller holds a refresh token.
    await _waitFor(container, (s) => s is AuthStateSignedOut);
    await controller.signIn(email: 'visitor@example.sa', password: 'x');

    // Fire four refreshes at once — they must collapse to a single rotation.
    final results = await Future.wait<bool>(<Future<bool>>[
      controller.refresh(),
      controller.refresh(),
      controller.refresh(),
      controller.refresh(),
    ]);

    expect(results, everyElement(isTrue));
    expect(refreshCalls, 1);

    // A later refresh is a fresh single-flight (the guard cleared on settle).
    await controller.refresh();
    expect(refreshCalls, 2);
  });

  // D-737 — refresh() (the 401-interceptor path) signs the user out when the
  // refresh token is dead; tryRefresh() (the proactive session-guard path) does
  // NOT — it returns false and leaves the session in place so the guard can show
  // its 30s warning instead of an abrupt, unannounced sign-out.
  Future<AuthController> signedIn(
    _MockAuthRepository repo,
    _MockSecureStorage secure,
    ProviderContainer container,
  ) async {
    when(() => secure.read(any())).thenAnswer((_) async => null);
    when(() => secure.write(any(), any())).thenAnswer((_) async {});
    when(secure.clearAuthValues).thenAnswer((_) async {});
    when(
      () => repo.signIn(
        email: any(named: 'email'),
        password: any(named: 'password'),
      ),
    ).thenAnswer((_) async => SignInSession(_session()));
    when(repo.getCurrentUser).thenAnswer((_) async => _user());
    final controller = container.read(authControllerProvider.notifier);
    await _waitFor(container, (s) => s is AuthStateSignedOut);
    await controller.signIn(email: 'visitor@example.sa', password: 'x');
    return controller;
  }

  test('refresh() signs the user out when the refresh token is dead', () async {
    final repo = _MockAuthRepository();
    final secure = _MockSecureStorage();
    final container = _container(repo, secure);
    addTearDown(container.dispose);
    final controller = await signedIn(repo, secure, container);

    when(() => repo.refresh(refreshToken: any(named: 'refreshToken'))).thenThrow(
      const RefreshTokenExpired(
        ApiFailure(code: ApiErrorCodes.authRefreshTokenExpired, message: 'x'),
      ),
    );

    final ok = await controller.refresh();
    expect(ok, isFalse);
    expect(container.read(authControllerProvider), isA<AuthStateSignedOut>());
  });

  test('tryRefresh() failure returns false but keeps the user signed in',
      () async {
    final repo = _MockAuthRepository();
    final secure = _MockSecureStorage();
    final container = _container(repo, secure);
    addTearDown(container.dispose);
    final controller = await signedIn(repo, secure, container);

    when(() => repo.refresh(refreshToken: any(named: 'refreshToken'))).thenThrow(
      const RefreshTokenExpired(
        ApiFailure(code: ApiErrorCodes.authRefreshTokenExpired, message: 'x'),
      ),
    );

    final ok = await controller.tryRefresh();
    expect(ok, isFalse);
    // The whole point of D-737: no silent sign-out — the guard decides.
    expect(container.read(authControllerProvider), isA<AuthStateSignedIn>());
  });

  test('tryRefresh() success rotates the token and stays signed in', () async {
    final repo = _MockAuthRepository();
    final secure = _MockSecureStorage();
    final container = _container(repo, secure);
    addTearDown(container.dispose);
    final controller = await signedIn(repo, secure, container);

    when(() => repo.refresh(refreshToken: any(named: 'refreshToken')))
        .thenAnswer((_) async => _session());

    final ok = await controller.tryRefresh();
    expect(ok, isTrue);
    expect(container.read(authControllerProvider), isA<AuthStateSignedIn>());
  });
}
