import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// Sentinel wire fixtures for the cached signed-in user.
//
// WHY THIS FILE EXISTS. `_restoreCurrentUserFromMap` is tolerant by design —
// `json['displayName'] as String? ?? ''`, and three enum decoders that return
// a default for anything they do not recognise. A renamed or dropped key
// therefore does NOT throw: `appRole` silently becomes Guest,
// `registrationStatus` silently becomes Pending, and the user is quietly
// demoted out of every screen they are entitled to. `auth_controller_restore_
// test.dart` cannot see that — it builds its own JSON from the same fields it
// then asserts, so a rename on both sides stays green.
//
// This blob is written to secure storage ON THE DEVICE, so a drift breaks a
// shipped install at upgrade with no server involved. The frozen literal below
// is the artefact as it sits in the keystore; the map functions that read and
// write it are private, so both directions are exercised through the
// controller, which is also the only path that runs in production.

/// Every field carrying a value nothing in the decoder can produce by
/// accident: no empty strings, a non-default case for all three enums, a
/// non-null avatar, and `profileComplete` true against a false default.
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

/// The user the sentinel fixture describes, built independently of it so the
/// encode assertion compares the emitted map against the frozen literal rather
/// than against itself.
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

/// In-memory secure storage. The controller's persist path is what emits the
/// blob, so reading it back out of a real map is more direct than capturing
/// mock arguments — and it keeps read and write on the same store, the way the
/// device does.
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

/// Cold-starts the controller on [userJson] with a still-valid access token,
/// so the restore takes the cached-session fast path. `getCurrentUser` fails
/// offline, which `reloadCurrentUser` swallows — that is what leaves the
/// CACHED user in the session for the assertions to inspect, rather than a
/// server copy.
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

      // A dropped `registrationStatus` key defaults to Pending, which folds
      // effectiveAppRole back to Guest — a silent demotion out of every
      // Visitor screen. Assert the consequence, not only the field.
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
      // Fail the hydration call so the only currentUserJson write on the
      // store is the one signIn made from the session above.
      when(repo.getCurrentUser).thenThrow(_offline);

      final container = _container(repo, secure);
      addTearDown(container.dispose);

      // Let the cold-start restore settle to signed-out before signing in;
      // otherwise the two paths race for the same store.
      await _waitFor(container, (s) => s is AuthStateSignedOut);
      await container
          .read(authControllerProvider.notifier)
          .signIn(email: 'WIRE:email', password: 'pw');

      final written = secure.store[StorageKeys.currentUserJson];
      expect(written, isNotNull);

      final emitted = jsonDecode(written!) as Map<String, dynamic>;
      final fixture = jsonDecode(_sentinelJson) as Map<String, dynamic>;

      // The key SET first — that is what catches a key silently dropped on
      // write, which a value-by-value comparison would not notice.
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

      // Nothing in this writer is conditional: all eight keys are emitted
      // whatever the values are. Pinning that stops a future
      // `if (avatarUrl != null)` from quietly changing the artefact.
      expect(emitted.keys.toSet(), equals(fixtureKeys));
      expect(emitted['avatarUrl'], isNull);
      expect(emitted['appRole'], 'Guest');
      expect(emitted['preferredLanguage'], 'ar');
      expect(emitted['registrationStatus'], 'Pending');
      expect(emitted['profileComplete'], false);
    });
  });
}
