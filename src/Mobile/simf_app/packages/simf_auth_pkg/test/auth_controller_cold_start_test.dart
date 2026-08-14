import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _MockSecureStorage extends Mock implements SimfSecureStorage {}

class _MockAuthRepository extends Mock implements AuthRepository {}

ProviderContainer _container(SimfSecureStorage secure, AuthRepository repo) {
  return ProviderContainer(
    overrides: <Override>[
      simfSecureStorageProvider.overrideWithValue(secure),
      authRepositoryProvider.overrideWithValue(repo),
    ],
  );
}

Future<AuthState> _waitForResolved(ProviderContainer container) {
  final completer = Completer<AuthState>();
  final sub = container.listen<AuthState>(
    authControllerProvider,
    (_, next) {
      if (next is! AuthStateInitial && !completer.isCompleted) {
        completer.complete(next);
      }
    },
    fireImmediately: true,
  );
  return completer.future
      .timeout(const Duration(seconds: 8))
      .whenComplete(sub.close);
}

void main() {
  group('AuthController cold-start restore (Page_001 L-4/L-6, D-295)', () {
    test(
      'a hung keystore read resolves to signed-out instead of stranding at '
      'Initial',
      () async {
        // The platform keystore can hang on a cold start (observed on a real
        // device): _secureStorage.read() never completes. Without the timeout
        // guard the restore stalls, auth stays AuthStateInitial forever, and
        // the router holds the user on the splash with no escape (router.dart).
        // The read is time-boxed, so the catch-all must resolve to signed-out.
        final secure = _MockSecureStorage();
        final repo = _MockAuthRepository();
        when(() => secure.read(any()))
            .thenAnswer((_) => Completer<String?>().future); // never completes

        final container = _container(secure, repo);
        addTearDown(container.dispose);

        // Reading the provider builds it (kicks off the restore).
        expect(container.read(authControllerProvider), isA<AuthStateInitial>());

        final resolved = await _waitForResolved(container);
        expect(
          resolved,
          isA<AuthStateSignedOut>(),
          reason:
              'the cold-start read timeout must never strand auth at Initial',
        );
      },
      timeout: const Timeout(Duration(seconds: 15)),
    );

    test('no stored refresh token resolves to signed-out', () async {
      final secure = _MockSecureStorage();
      final repo = _MockAuthRepository();
      when(() => secure.read(any())).thenAnswer((_) async => null);

      final container = _container(secure, repo);
      addTearDown(container.dispose);

      final resolved = await _waitForResolved(container);
      expect(resolved, isA<AuthStateSignedOut>());
    });
  });
}
