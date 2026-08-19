import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockSecureStorage extends Mock implements SimfSecureStorage {}

ProviderContainer _container(AuthRepository repo, SimfSecureStorage secure) {
  return ProviderContainer(
    overrides: <Override>[
      authRepositoryProvider.overrideWithValue(repo),
      simfSecureStorageProvider.overrideWithValue(secure),
    ],
  );
}

/// Lets the cold-start restore settle to signed-out before the test acts.
Future<void> _waitForSignedOut(ProviderContainer container) async {
  final completer = Completer<void>();
  final sub = container.listen<AuthState>(
    authControllerProvider,
    (_, next) {
      if (next is AuthStateSignedOut && !completer.isCompleted) {
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
  group('AuthController.signUp (Page 005)', () {
    test('delegates to the repository and never signs the user in (L-5)',
        () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(
        () => repo.signUp(
          email: any(named: 'email'),
          password: any(named: 'password'),
          confirmPassword: any(named: 'confirmPassword'),
        ),
      ).thenAnswer((_) async {});

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      await _waitForSignedOut(container);

      await container.read(authControllerProvider.notifier).signUp(
            email: 'visitor@example.sa',
            password: 'Password1',
            confirmPassword: 'Password1',
          );

      verify(
        () => repo.signUp(
          email: 'visitor@example.sa',
          password: 'Password1',
          confirmPassword: 'Password1',
        ),
      ).called(1);
      // Sign-up creates an under-review account but issues no session (L-5).
      expect(container.read(authControllerProvider), isA<AuthStateSignedOut>());
    });

    test('propagates an AuthFailure from the repository', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(
        () => repo.signUp(
          email: any(named: 'email'),
          password: any(named: 'password'),
          confirmPassword: any(named: 'confirmPassword'),
        ),
      ).thenThrow(
        const ValidationFailed(
          ApiFailure(
            code: 'VALIDATION_FAILED',
            message: 'bad request',
            httpStatus: 400,
          ),
        ),
      );

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      await _waitForSignedOut(container);

      await expectLater(
        container.read(authControllerProvider.notifier).signUp(
              email: 'visitor@example.sa',
              password: 'Password1',
              confirmPassword: 'Password1',
            ),
        throwsA(isA<ValidationFailed>()),
      );
      expect(container.read(authControllerProvider), isA<AuthStateSignedOut>());
    });
  });
}
