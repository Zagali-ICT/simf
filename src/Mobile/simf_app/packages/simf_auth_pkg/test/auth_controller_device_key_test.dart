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


/// Writes the owner binding a real enrolment would have written.
Future<void> _bind(
  _InMemSecureStorage secure,
  String deviceKeyId,
  String email,
) =>
    secure.write(
      StorageKeys.deviceKeyAccountJson,
      jsonEncode(
        DeviceKeyBinding.create(
          userId: 'u1',
          deviceKeyId: deviceKeyId,
          email: email,
        ).toJson(),
      ),
    );

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
      await _bind(secure, 'dk-1', 'visitor@example.sa');

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

  group('AuthController device-key OWNER BINDING', () {
    // The defect: the device key was stored with no record of whose it was,
    // survived sign-out, and signed the holder in ignoring the typed address.
    // Account A enrolled, signed out, and someone typing B's email and tapping
    // Face ID landed in A - silently, because the server resolves the account
    // from the key and never sees an address at all.
    test('enrol records WHO the key belongs to', () async {
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

      final enrolled = await notifier.enrolledDeviceKey();
      expect(enrolled, isNotNull);
      expect(enrolled!.binding.userId, 'u1');
      expect(enrolled.binding.maskedEmail, 'v***@example.sa');
      expect(
        enrolled.binding.matchesEmail(
          deviceKeyId: 'dk-1',
          email: 'visitor@example.sa',
        ),
        isTrue,
      );
    });

    test('THE REGRESSION: another account is refused before any network call',
        () async {
      final repo = _MockAuthRepository();
      final secure = _InMemSecureStorage();
      const client = DeviceKeyClient();
      final pair = client.generateKeyPair();
      await secure.write(StorageKeys.deviceKeyId, 'dk-1');
      await secure.write(StorageKeys.deviceKeyPrivate, pair.privateKeyBase64);
      await _bind(secure, 'dk-1', 'visitor@example.sa');

      final container = ProviderContainer(
        overrides: <Override>[
          authRepositoryProvider.overrideWithValue(repo),
          simfSecureStorageProvider.overrideWithValue(secure),
        ],
      );
      addTearDown(container.dispose);
      await _waitFor(container, (s) => s is AuthStateSignedOut);

      final outcome = await container
          .read(authControllerProvider.notifier)
          .signInWithDeviceKey(expectedEmail: 'someone.else@example.sa');

      expect(outcome, DeviceKeySignInOutcome.accountMismatch);
      expect(container.read(authControllerProvider), isA<AuthStateSignedOut>());
      // Refused locally: no challenge issued, so no round trip and no OS
      // prompt spent on a sign-in that could never have succeeded.
      verifyNever(() => repo.issueDeviceKeyChallenge(any()));
    });

    test(
        'an upgraded install with no binding reads as not enrolled, and the '
        'stale key is cleared', () async {
      final repo = _MockAuthRepository();
      final secure = _InMemSecureStorage();
      await secure.write(StorageKeys.deviceKeyId, 'dk-1');
      await secure.write(StorageKeys.deviceKeyPrivate, 'priv');

      final container = ProviderContainer(
        overrides: <Override>[
          authRepositoryProvider.overrideWithValue(repo),
          simfSecureStorageProvider.overrideWithValue(secure),
        ],
      );
      addTearDown(container.dispose);
      await _waitFor(container, (s) => s is AuthStateSignedOut);
      final notifier = container.read(authControllerProvider.notifier);

      // Keeping it would preserve the exact defect on precisely the devices
      // that already have it: nothing can say which account it opens.
      expect(await notifier.hasEnrolledDeviceKey(), isFalse);
      expect(await secure.read(StorageKeys.deviceKeyId), isNull);
      expect(await secure.read(StorageKeys.deviceKeyPrivate), isNull);
      expect(
        await notifier.signInWithDeviceKey(),
        DeviceKeySignInOutcome.notEnrolled,
      );
    });

    test('an empty email field still signs in as the enrolled account',
        () async {
      final repo = _MockAuthRepository();
      final secure = _InMemSecureStorage();
      const client = DeviceKeyClient();
      final pair = client.generateKeyPair();
      await secure.write(StorageKeys.deviceKeyId, 'dk-1');
      await secure.write(StorageKeys.deviceKeyPrivate, pair.privateKeyBase64);
      await _bind(secure, 'dk-1', 'visitor@example.sa');

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

      // Blank means "whoever this device is set up for", which is the normal
      // case: the field is empty until the user types.
      expect(
        await container
            .read(authControllerProvider.notifier)
            .signInWithDeviceKey(expectedEmail: '  '),
        DeviceKeySignInOutcome.signedIn,
      );
    });

    test('a DIFFERENT account signing in here drops the previous key',
        () async {
      final repo = _MockAuthRepository();
      final secure = _InMemSecureStorage();
      await secure.write(StorageKeys.deviceKeyId, 'dk-1');
      await secure.write(StorageKeys.deviceKeyPrivate, 'priv');
      await _bind(secure, 'dk-1', 'visitor@example.sa');

      final otherUser = CurrentUser(
        id: 'u2',
        email: 'second@example.sa',
        displayName: 'Second',
        appRole: AppRole.visitor,
        preferredLanguage: PreferredLanguage.fromJson('ar'),
        registrationStatus: RegistrationStatus.approved,
      );
      when(repo.getCurrentUser).thenAnswer((_) async => otherUser);
      when(
        () => repo.signIn(
          email: any(named: 'email'),
          password: any(named: 'password'),
        ),
      ).thenAnswer(
        (_) async => SignInSession(
          Session(
            accessToken: 'A',
            refreshToken: 'R',
            accessTokenExpiresAt:
                DateTime.now().add(const Duration(minutes: 30)),
            user: otherUser,
          ),
        ),
      );

      final container = ProviderContainer(
        overrides: <Override>[
          authRepositoryProvider.overrideWithValue(repo),
          simfSecureStorageProvider.overrideWithValue(secure),
        ],
      );
      addTearDown(container.dispose);
      await _waitFor(container, (s) => s is AuthStateSignedOut);
      final notifier = container.read(authControllerProvider.notifier);
      await notifier.signIn(email: 'second@example.sa', password: 'pw');

      // Otherwise B signs in with a password, signs out, and the sign-in screen
      // offers "Continue as v***@example.sa" - showing B a masked form of A's
      // address and a way into A's account.
      expect(await secure.read(StorageKeys.deviceKeyId), isNull);
      expect(await secure.read(StorageKeys.deviceKeyAccountJson), isNull);
      expect(await notifier.hasEnrolledDeviceKey(), isFalse);
    });
  });
}
