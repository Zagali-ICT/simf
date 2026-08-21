import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// Frozen fixtures for the cached signed-in user, which lives in device secure
// storage. The decoder falls back rather than throwing, so a renamed key does
// not fail — it silently demotes the user to Guest/Pending.

/// Every field carrying a value the decoder cannot produce by accident.
const String _sentinelJson = '''
{
  "id": "WIRE:id",
  "email": "WIRE:email",
  "displayName": "WIRE:displayName",
  "appRole": "Exhibitor",
  "preferredLanguage": "en",
  "registrationStatus": "Approved",
  "avatarUrl": "WIRE:avatarUrl",
  "profileComplete": true
}
''';

/// Every key present, every value null. Pins the fallbacks themselves.
const String _allNullsJson = '''
{
  "id": null,
  "email": null,
  "displayName": null,
  "appRole": null,
  "preferredLanguage": null,
  "registrationStatus": null,
  "avatarUrl": null,
  "profileComplete": null
}
''';

/// Built independently of the fixture so the encode assertion compares against
/// the frozen literal rather than against itself.
const CurrentUser _sentinelUser = CurrentUser(
  id: 'WIRE:id',
  email: 'WIRE:email',
  displayName: 'WIRE:displayName',
  appRole: AppRole.exhibitor,
  preferredLanguage: PreferredLanguage.english,
  registrationStatus: RegistrationStatus.approved,
  avatarUrl: 'WIRE:avatarUrl',
  profileComplete: true,
);

class _MockAuthRepository extends Mock implements AuthRepository {}

class _FakeSecureStorage implements SimfSecureStorage {
  _FakeSecureStorage([Map<String, String>? seed]) {
    if (seed != null) {
      store.addAll(seed);
    }
  }

  final Map<String, String> store = <String, String>{};

  @override
  Future<String?> read(String key) async => store[key];

  @override
  Future<void> write(String key, String value) async {
    store[key] = value;
  }

  @override
  Future<void> delete(String key) async {
    store.remove(key);
  }

  @override
  Future<void> clearAuthValues() async {
    store
      ..remove(StorageKeys.accessToken)
      ..remove(StorageKeys.refreshToken)
      ..remove(StorageKeys.accessTokenExpiresAtIso)
      ..remove(StorageKeys.currentUserJson);
  }
}

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

const NetworkUnavailable _offline = NetworkUnavailable(
  ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'offline'),
);

/// Cold-starts the controller on [userJson]. `getCurrentUser` fails offline so
/// the session keeps the CACHED user rather than a server copy.
Future<CurrentUser> _restoreCachedUser(String userJson) async {
  final repo = _MockAuthRepository();
  final secure = _FakeSecureStorage(<String, String>{
    StorageKeys.accessToken: 'access',
    StorageKeys.refreshToken: 'refresh',
    StorageKeys.accessTokenExpiresAtIso:
        DateTime.now().add(const Duration(hours: 1)).toIso8601String(),
    StorageKeys.currentUserJson: userJson,
  });
  when(repo.getCurrentUser).thenThrow(_offline);

  final container = _container(repo, secure);
  addTearDown(container.dispose);

  final state = await _waitFor(container, (s) => s is AuthStateSignedIn);
  return (state as AuthStateSignedIn).session.user;
}

void main() {
  group('cached CurrentUser — frozen sentinel fixture', () {
    test('every key decodes to its sentinel, not to a fallback', () async {
      final user = await _restoreCachedUser(_sentinelJson);

      expect(user.id, 'WIRE:id');
      expect(user.email, 'WIRE:email');
      expect(user.displayName, 'WIRE:displayName');
      expect(user.appRole, AppRole.exhibitor);
      expect(user.preferredLanguage, PreferredLanguage.english);
      expect(user.registrationStatus, RegistrationStatus.approved);
      expect(user.avatarUrl, 'WIRE:avatarUrl');
      expect(user.profileComplete, isTrue);
    });

    test('the decoded privilege is the one the app actually gates on',
        () async {
      final user = await _restoreCachedUser(_sentinelJson);

      // A dropped `registrationStatus` defaults to Pending, which folds
      // effectiveAppRole back to Guest.
      expect(user.isApproved, isTrue);
      expect(user.effectiveAppRole, AppRole.exhibitor);
    });
  });

  group('cached CurrentUser — fallbacks', () {
    test('an all-nulls object defaults every field', () async {
      final user = await _restoreCachedUser(_allNullsJson);

      expect(user.id, '');
      expect(user.email, '');
      expect(user.displayName, '');
      expect(user.appRole, AppRole.guest);
      expect(user.preferredLanguage, PreferredLanguage.arabic);
      expect(user.registrationStatus, RegistrationStatus.pending);
      expect(user.avatarUrl, isNull);
      expect(user.profileComplete, isFalse);
    });

    test('an empty object defaults every field identically', () async {
      final user = await _restoreCachedUser('{}');

      expect(user.id, '');
      expect(user.email, '');
      expect(user.displayName, '');
      expect(user.appRole, AppRole.guest);
      expect(user.preferredLanguage, PreferredLanguage.arabic);
      expect(user.registrationStatus, RegistrationStatus.pending);
      expect(user.avatarUrl, isNull);
      expect(user.profileComplete, isFalse);
    });

    test('an unrecognised enum case falls back, it does not throw', () async {
      final user = await _restoreCachedUser(
        '{"appRole": "Admiral", "preferredLanguage": "fr",'
        ' "registrationStatus": "Escalated"}',
      );

      expect(user.appRole, AppRole.guest);
      expect(user.preferredLanguage, PreferredLanguage.arabic);
      expect(user.registrationStatus, RegistrationStatus.pending);
    });
  });

  group('cached CurrentUser — the persisted blob equals the fixture', () {
    test('sign-in writes exactly the sentinel fixture back', () async {
      final repo = _MockAuthRepository();
      final secure = _FakeSecureStorage();
      when(
        () => repo.signIn(
          email: any(named: 'email'),
          password: any(named: 'password'),
        ),
      ).thenAnswer(
        (_) async => SignInSession(
          Session(
            accessToken: 'access',
            refreshToken: 'refresh',
            accessTokenExpiresAt:
                DateTime.now().add(const Duration(minutes: 30)),
            user: _sentinelUser,
          ),
        ),
      );
      // Fail hydration so the only currentUserJson write is signIn's.
      when(repo.getCurrentUser).thenThrow(_offline);

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      // Settle the cold-start restore first, or it races signIn for the store.
      await _waitFor(container, (s) => s is AuthStateSignedOut);
      await container
          .read(authControllerProvider.notifier)
          .signIn(email: 'WIRE:email', password: 'pw');

      final written = secure.store[StorageKeys.currentUserJson];
      expect(written, isNotNull);

      final emitted = jsonDecode(written!) as Map<String, dynamic>;
      final fixture = jsonDecode(_sentinelJson) as Map<String, dynamic>;

      // The key SET first: a value-by-value comparison misses a dropped key.
      expect(emitted.keys.toSet(), equals(fixture.keys.toSet()));
      expect(emitted, equals(fixture));
    });

    test('a null avatar is written as a PRESENT null, never omitted',
        () async {
      final repo = _MockAuthRepository();
      final secure = _FakeSecureStorage();
      when(
        () => repo.signIn(
          email: any(named: 'email'),
          password: any(named: 'password'),
        ),
      ).thenAnswer(
        (_) async => SignInSession(
          Session(
            accessToken: 'access',
            refreshToken: 'refresh',
            accessTokenExpiresAt:
                DateTime.now().add(const Duration(minutes: 30)),
            user: const CurrentUser(
              id: 'WIRE:id',
              email: 'WIRE:email',
              displayName: 'WIRE:displayName',
              appRole: AppRole.guest,
              preferredLanguage: PreferredLanguage.arabic,
              registrationStatus: RegistrationStatus.pending,
            ),
          ),
        ),
      );
      when(repo.getCurrentUser).thenThrow(_offline);

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      await _waitFor(container, (s) => s is AuthStateSignedOut);
      await container
          .read(authControllerProvider.notifier)
          .signIn(email: 'WIRE:email', password: 'pw');

      final emitted = jsonDecode(secure.store[StorageKeys.currentUserJson]!)
          as Map<String, dynamic>;
      final fixtureKeys =
          (jsonDecode(_sentinelJson) as Map<String, dynamic>).keys.toSet();

      // The writer is unconditional: a future `if (avatarUrl != null)` would
      // change the on-device artefact.
      expect(emitted.keys.toSet(), equals(fixtureKeys));
      expect(emitted['avatarUrl'], isNull);
      expect(emitted['appRole'], 'Guest');
      expect(emitted['preferredLanguage'], 'ar');
      expect(emitted['registrationStatus'], 'Pending');
      expect(emitted['profileComplete'], false);
    });
  });
}
