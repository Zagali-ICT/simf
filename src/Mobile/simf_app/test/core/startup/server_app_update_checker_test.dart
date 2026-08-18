import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/startup/app_update_checker.dart';
import 'package:simf_app/core/startup/app_version_policy.dart';
import 'package:simf_app/core/startup/server_app_update_checker.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import '../../support/simf_test_scope.dart';

class _FakePolicyRepository implements AppVersionPolicyRepository {
  _FakePolicyRepository({this.policy});

  /// Null = the fetch throws (server unreachable / malformed).
  final AppVersionPolicy? policy;

  @override
  Future<AppVersionPolicy> fetch() async {
    final value = policy;
    if (value == null) {
      throw Exception('unreachable');
    }
    return value;
  }
}

/// In-memory [SimfPrefsStorage] so the tests need no platform channel.
class _FakePrefs implements SimfPrefsStorage {
  _FakePrefs([Map<String, Object>? seed]) {
    if (seed != null) {
      _store.addAll(seed);
    }
  }

  final Map<String, Object> _store = <String, Object>{};

  @override
  String? getString(String key) {
    final value = _store[key];
    return value is String ? value : null;
  }

  @override
  Future<bool> setString(String key, String value) async {
    _store[key] = value;
    return true;
  }

  @override
  bool? getBool(String key) {
    final value = _store[key];
    return value is bool ? value : null;
  }

  @override
  Future<bool> setBool(String key, bool value) async {
    _store[key] = value;
    return true;
  }

  @override
  double? getDouble(String key) {
    final value = _store[key];
    return value is double ? value : null;
  }

  @override
  Future<bool> setDouble(String key, double value) async {
    _store[key] = value;
    return true;
  }

  @override
  int? getInt(String key) {
    final value = _store[key];
    return value is int ? value : null;
  }

  @override
  Future<bool> setInt(String key, int value) async {
    _store[key] = value;
    return true;
  }

  @override
  Future<bool> remove(String key) async {
    _store.remove(key);
    return true;
  }
}

const _storeUrl = 'https://play.google.com/store/apps/details?id=sa.simf.app';

AppVersionPolicy _policy({
  String? min,
  String? latest,
  String? storeUrl = _storeUrl,
  PlatformVersionPolicy ios = const PlatformVersionPolicy(),
}) =>
    AppVersionPolicy(
      android: PlatformVersionPolicy(
        minVersion: min,
        latestVersion: latest,
        storeUrl: storeUrl,
      ),
      ios: ios,
    );

/// A container wiring the checker against fakes. [now] pins the clock for the
/// snooze-window cases.
({
  ProviderContainer container,
  ServerAppUpdateChecker checker,
  _FakePrefs prefs
}) _harness({
  AppVersionPolicy? policy,
  String installed = '1.0.0',
  Map<String, Object>? prefsSeed,
  DateTime Function()? now,
}) {
  final prefs = _FakePrefs(prefsSeed);
  final checkerProvider = Provider<ServerAppUpdateChecker>(
    (ref) => ServerAppUpdateChecker(ref, now: now),
  );
  final container = ProviderContainer(
    retry: simfTestNoRetry,
    overrides: <Override>[
      appVersionPolicyRepositoryProvider
          .overrideWithValue(_FakePolicyRepository(policy: policy)),
      simfPrefsStorageProvider.overrideWithValue(prefs),
      installedAppVersionProvider.overrideWithValue(installed),
    ],
  );
  return (
    container: container,
    checker: container.read(checkerProvider),
    prefs: prefs,
  );
}

void main() {
  tearDown(() => debugDefaultTargetPlatformOverride = null);

  group('ServerAppUpdateChecker.check (D-736)', () {
    test('an unreachable server fails open to upToDate', () async {
      final h = _harness(); // no policy = the fetch throws
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.upToDate);
    });

    test('an unoverridden provider graph fails open too', () async {
      // The integration harness boots the real app with no data-config
      // override — the repository provider throws at construction. The
      // checker must swallow even that.
      final container = ProviderContainer(
        retry: simfTestNoRetry,
        overrides: <Override>[
          installedAppVersionProvider.overrideWithValue('1.0.0'),
        ],
      );
      addTearDown(container.dispose);
      final checkerProvider =
          Provider<ServerAppUpdateChecker>(ServerAppUpdateChecker.new);

      expect(
        await container.read(checkerProvider).check(),
        AppUpdateStatus.upToDate,
      );
    });

    test('installed below the minimum → forced', () async {
      final h = _harness(policy: _policy(min: '1.2.0'));
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.forced);
    });

    test('below the minimum without a store URL → upToDate (anti-brick)',
        () async {
      final h = _harness(policy: _policy(min: '1.2.0', storeUrl: null));
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.upToDate);
    });

    test('below the latest → optional', () async {
      final h = _harness(policy: _policy(latest: '1.1.0'));
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.optional);
    });

    test('at the latest → upToDate', () async {
      final h = _harness(policy: _policy(min: '0.9.0', latest: '1.0.0'));
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.upToDate);
    });

    test('an iOS device reads the ios policy branch', () async {
      debugDefaultTargetPlatformOverride = TargetPlatform.iOS;
      final h = _harness(
        // The android side would force; the (default-empty) ios side is silent.
        policy: _policy(min: '9.9.9'),
      );
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.upToDate);
    });
  });

  group('ServerAppUpdateChecker snooze (D-736)', () {
    test('a snoozed version stays quiet inside the window', () async {
      final h = _harness(
        policy: _policy(latest: '1.1.0'),
        prefsSeed: <String, Object>{
          StorageKeys.appUpdateSnoozedVersion: '1.1.0',
          StorageKeys.appUpdateSnoozedAtIso:
              DateTime(2026, 7, 10).toIso8601String(),
        },
        now: () => DateTime(2026, 7, 11), // 1 day later, window = 3 days
      );
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.upToDate);
    });

    test('an expired snooze prompts again', () async {
      final h = _harness(
        policy: _policy(latest: '1.1.0'),
        prefsSeed: <String, Object>{
          StorageKeys.appUpdateSnoozedVersion: '1.1.0',
          StorageKeys.appUpdateSnoozedAtIso:
              DateTime(2026, 7).toIso8601String(),
        },
        now: () => DateTime(2026, 7, 10),
      );
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.optional);
    });

    test('a NEWER version than the snoozed one prompts immediately', () async {
      final h = _harness(
        policy: _policy(latest: '1.2.0'),
        prefsSeed: <String, Object>{
          StorageKeys.appUpdateSnoozedVersion: '1.1.0',
          StorageKeys.appUpdateSnoozedAtIso:
              DateTime(2026, 7, 10).toIso8601String(),
        },
        now: () => DateTime(2026, 7, 10),
      );
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.optional);
    });

    test('a snooze never suppresses a FORCED update', () async {
      final h = _harness(
        policy: _policy(min: '1.1.0', latest: '1.1.0'),
        prefsSeed: <String, Object>{
          StorageKeys.appUpdateSnoozedVersion: '1.1.0',
          StorageKeys.appUpdateSnoozedAtIso:
              DateTime(2026, 7, 10).toIso8601String(),
        },
        now: () => DateTime(2026, 7, 10),
      );
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.forced);
    });

    test('snoozeOptionalUpdate persists the offered version + timestamp',
        () async {
      final h = _harness(
        policy: _policy(latest: '1.1.0'),
        now: () => DateTime(2026, 7, 10, 12),
      );
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.optional);
      await h.checker.snoozeOptionalUpdate();

      expect(h.prefs.getString(StorageKeys.appUpdateSnoozedVersion), '1.1.0');
      expect(
        h.prefs.getString(StorageKeys.appUpdateSnoozedAtIso),
        DateTime(2026, 7, 10, 12).toIso8601String(),
      );
      // The next launch inside the window stays quiet.
      expect(await h.checker.check(), AppUpdateStatus.upToDate);
    });

    test('snoozeOptionalUpdate is a no-op when nothing was offered', () async {
      final h = _harness(policy: _policy());
      addTearDown(h.container.dispose);

      expect(await h.checker.check(), AppUpdateStatus.upToDate);
      await h.checker.snoozeOptionalUpdate();

      expect(h.prefs.getString(StorageKeys.appUpdateSnoozedVersion), isNull);
    });
  });
}
