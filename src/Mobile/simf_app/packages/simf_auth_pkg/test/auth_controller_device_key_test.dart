import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_auth_pkg/src/data/device_key_client.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

/// In-memory secure storage that actually remembers writes (so enrol → read
/// round-trips). `implements` ignores the concrete constructor.
class _InMemSecureStorage implements SimfSecureStorage {
  final Map<String, String> _store = <String, String>{};

  @override
  Future<String?> read(String key) async => _store[key];

  @override
  Future<void> write(String key, String value) async {
    _store[key] = value;
  }

  @override
  Future<void> delete(String key) async {
    _store.remove(key);
  }

  @override
  Future<void> clearAuthValues() async {
    _store
      ..remove(StorageKeys.accessToken)
      ..remove(StorageKeys.refreshToken)
      ..remove(StorageKeys.accessTokenExpiresAtIso)
      ..remove(StorageKeys.currentUserJson);
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
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _user(),
    );

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
  group('AuthController device-key (biometric, D-172)', () {
    test('enrol registers the public key and stores the id + private key',
        () async {
      final repo = _MockAuthRepository();
      final secure = _InMemSecureStorage();
      when(repo.getCurrentUser).thenAnswer((_) async => _user());
      when(
        () => repo.signIn(
          email: any(named: 'email'),
          password: any(named: 'password'),
        ),
      ).thenAnswer((_) async => SignInSession(_session()));
      when(
        () => repo.registerDeviceKey(
          publicKeySpki: any(named: 'publicKeySpki'),
          label: any(named: 'label'),
        ),
      ).thenAnswer((_) async => 'dk-1');

      final container = ProviderContainer(
        overrides: <Override>[
          authRepositoryProvider.overrideWithValue(repo),
          simfSecureStorageProvider.overrideWithValue(secure),
        ],
      );
      addTearDown(container.dispose);

      await _waitFor(container, (s) => s is AuthStateSignedOut);
      final notifier = container.read(authControllerProvider.notifier);
      await notifier.signIn(email: 'visitor@example.sa', password: 'pw');
      await notifier.enrolDeviceKey();

      expect(await notifier.hasEnrolledDeviceKey(), isTrue);
      expect(await secure.read(StorageKeys.deviceKeyId), equals('dk-1'));
      expect(await secure.read(StorageKeys.deviceKeyPrivate), isNotNull);
      final captured = verify(
        () => repo.registerDeviceKey(
          publicKeySpki: captureAny(named: 'publicKeySpki'),
          label: any(named: 'label'),
        ),
      ).captured.single as String;
      // A base64 P-256 SubjectPublicKeyInfo is 91 bytes.
      expect(base64.decode(captured).length, equals(91));
    });

    test('biometric sign-in signs the challenge and mints a session', () async {
      final repo = _MockAuthRepository();
      final secure = _InMemSecureStorage();
      // Pre-enrol a real key pair on the device.
      const client = DeviceKeyClient();
      final pair = client.generateKeyPair();
      await secure.write(StorageKeys.deviceKeyId, 'dk-1');
      await secure.write(StorageKeys.deviceKeyPrivate, pair.privateKeyBase64);

      final challenge = base64.encode(
        Uint8List.fromList(List<int>.generate(32, (i) => i)),
      );
      when(() => repo.issueDeviceKeyChallenge('dk-1'))
          .thenAnswer((_) async => challenge);
      when(
        () => repo.signInWithDeviceKey(
          deviceKeyId: any(named: 'deviceKeyId'),
          challenge: any(named: 'challenge'),
          signature: any(named: 'signature'),
        ),
      ).thenAnswer((_) async => _session());
      when(repo.getCurrentUser).thenAnswer((_) async => _user());

      final container = ProviderContainer(
        overrides: <Override>[
          authRepositoryProvider.overrideWithValue(repo),
          simfSecureStorageProvider.overrideWithValue(secure),
        ],
      );
      addTearDown(container.dispose);

      await _waitFor(container, (s) => s is AuthStateSignedOut);
      await container
          .read(authControllerProvider.notifier)
          .signInWithDeviceKey();

      expect(container.read(authControllerProvider), isA<AuthStateSignedIn>());
      final captured = verify(
        () => repo.signInWithDeviceKey(
          deviceKeyId: 'dk-1',
          challenge: challenge,
          signature: captureAny(named: 'signature'),
        ),
      ).captured.single as String;
      // A base64 IEEE-P1363 P-256 signature is 64 bytes.
      expect(base64.decode(captured).length, equals(64));
    });

    test('disable revokes the server key and clears the local id + private key',
        () async {
      final repo = _MockAuthRepository();
      final secure = _InMemSecureStorage();
      await secure.write(StorageKeys.deviceKeyId, 'dk-1');
      await secure.write(StorageKeys.deviceKeyPrivate, 'priv');
      when(() => repo.revokeDeviceKey('dk-1')).thenAnswer((_) async {});

      final container = ProviderContainer(
        overrides: <Override>[
          authRepositoryProvider.overrideWithValue(repo),
          simfSecureStorageProvider.overrideWithValue(secure),
        ],
      );
      addTearDown(container.dispose);
      await _waitFor(container, (s) => s is AuthStateSignedOut);

      await container.read(authControllerProvider.notifier).disableDeviceKey();

      verify(() => repo.revokeDeviceKey('dk-1')).called(1);
      expect(await secure.read(StorageKeys.deviceKeyId), isNull);
      expect(await secure.read(StorageKeys.deviceKeyPrivate), isNull);
    });

    test('disable still clears the local key when the server revoke fails',
        () async {
      final repo = _MockAuthRepository();
      final secure = _InMemSecureStorage();
      await secure.write(StorageKeys.deviceKeyId, 'dk-1');
      await secure.write(StorageKeys.deviceKeyPrivate, 'priv');
      when(() => repo.revokeDeviceKey('dk-1')).thenThrow(Exception('offline'));

      final container = ProviderContainer(
        overrides: <Override>[
          authRepositoryProvider.overrideWithValue(repo),
          simfSecureStorageProvider.overrideWithValue(secure),
        ],
      );
      addTearDown(container.dispose);
      await _waitFor(container, (s) => s is AuthStateSignedOut);

      final notifier = container.read(authControllerProvider.notifier);
      await notifier.disableDeviceKey();

      // Best-effort: the local key is gone, so the biometric path is off even
      // though the server revoke threw.
      expect(await notifier.hasEnrolledDeviceKey(), isFalse);
    });
  });
}
