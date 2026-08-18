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
  group('AuthController email-verification (Page 006)', () {
    test('verifyEmail delegates to the repository and never changes state',
        () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(
        () => repo.verifyEmail(
          email: any(named: 'email'),
          code: any(named: 'code'),
        ),
      ).thenAnswer((_) async {});

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      await _waitForSignedOut(container);

      await container.read(authControllerProvider.notifier).verifyEmail(
            email: 'visitor@example.sa',
            code: '123456',
          );

      verify(
        () => repo.verifyEmail(email: 'visitor@example.sa', code: '123456'),
      ).called(1);
      expect(container.read(authControllerProvider), isA<AuthStateSignedOut>());
    });

    test('resendCode returns the code lifetime from the repository', () async {
      final repo = _MockAuthRepository();
      final secure = _MockSecureStorage();
      when(() => secure.read(any())).thenAnswer((_) async => null);
      when(() => secure.write(any(), any())).thenAnswer((_) async {});
      when(() => repo.resendCode(email: any(named: 'email')))
          .thenAnswer((_) async => 600);

      final container = _container(repo, secure);
      addTearDown(container.dispose);
      await _waitForSignedOut(container);

      final seconds = await container
          .read(authControllerProvider.notifier)
          .resendCode(email: 'visitor@example.sa');

      expect(seconds, equals(600));
      verify(() => repo.resendCode(email: 'visitor@example.sa')).called(1);
    });
  });
}
