import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_app/features/accessibility/data/accessibility_preferences_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';
import '_fake_prefs.dart';

// `accessibility-server-sync` — the five accessibility flags used to live in
// device prefs ONLY, so they did not follow the user to a second device and
// did not survive a reinstall. They are now written through to the account on
// every change and replayed at sign-in, with the device prefs kept as the
// offline cache. Every case below fails against the pre-fix tree (there was no
// repository, no write-through and no hydration).

/// Records what the controller pushes and answers with a canned server copy.
class _FakePreferencesApi implements AccessibilityPreferencesRepository {
  _FakePreferencesApi({this.remote, this.fail = false});

  final AccessibilitySettings? remote;
  final bool fail;

  final List<AccessibilitySettings> saved = <AccessibilitySettings>[];

  @override
  Future<AccessibilitySettings> fetch() async {
    if (fail || remote == null) {
      throw const ApiFailure(code: 'BOOM', message: 'unreachable');
    }
    return remote!;
  }

  @override
  Future<AccessibilitySettings> save(AccessibilitySettings settings) async {
    if (fail) {
      throw const ApiFailure(code: 'BOOM', message: 'unreachable');
    }
    saved.add(settings);
    return settings;
  }
}

ProviderContainer _containerWith(
  FakePrefs prefs,
  _FakePreferencesApi api,
) {
  final container = ProviderContainer(
    retry: simfTestNoRetry,
    overrides: <Override>[
      accessibilityControllerProvider
          .overrideWith(() => AccessibilityController(prefs: prefs)),
      accessibilityPreferencesRepositoryProvider.overrideWithValue(api),
    ],
  );
  addTearDown(container.dispose);
  return container;
}

/// Lets the fire-and-forget write-through run to completion.
Future<void> _settle() => Future<void>.delayed(Duration.zero);

void main() {
  group('AccessibilityController write-through', () {
    test('each change is pushed to the account', () async {
      final api = _FakePreferencesApi();
      final container = _containerWith(FakePrefs(), api);
      final controller =
          container.read(accessibilityControllerProvider.notifier);

      await controller.setHighContrast(true);
      await _settle();
      await controller.setTextSize(AppTextSize.large);
      await _settle();
      await controller.setReduceMotion(true);
      await _settle();
      await controller.setScreenReaderAssist(true);
      await _settle();
      await controller.setCaptions(false);
      await _settle();

      expect(api.saved, hasLength(5));
      // The push always carries the WHOLE settings object, so the last one is
      // the full picture rather than a single flag.
      final last = api.saved.last;
      expect(last.highContrast, isTrue);
      expect(last.textSize, AppTextSize.large);
      expect(last.reduceMotion, isTrue);
      expect(last.screenReaderAssist, isTrue);
      expect(last.captions, isFalse);
    });

    test('a failed push never disturbs the local choice', () async {
      final prefs = FakePrefs();
      final container = _containerWith(prefs, _FakePreferencesApi(fail: true));

      await container
          .read(accessibilityControllerProvider.notifier)
          .setHighContrast(true);
      await _settle();

      expect(
        container.read(accessibilityControllerProvider).highContrast,
        isTrue,
      );
      expect(prefs.getBool(StorageKeys.accessibilityHighContrast), isTrue);
    });
  });

  group('AccessibilitySync.hydrate (at sign-in)', () {
    test('the account copy replaces the local one, prefs included', () async {
      final prefs = FakePrefs(<String, Object>{
        StorageKeys.accessibilityTextSize: AppTextSize.small.name,
      });
      final container = _containerWith(
        prefs,
        _FakePreferencesApi(
          remote: const AccessibilitySettings(
            textSize: AppTextSize.extraLarge,
            highContrast: true,
            reduceMotion: true,
            screenReaderAssist: true,
            captions: false,
          ),
        ),
      );
      // Build the controller before hydrating so the local cache is the
      // starting point (this is the real sign-in ordering).
      expect(
        container.read(accessibilityControllerProvider).textSize,
        AppTextSize.small,
      );

      await container.read(accessibilitySyncProvider).hydrate();

      final state = container.read(accessibilityControllerProvider);
      expect(state.textSize, AppTextSize.extraLarge);
      expect(state.highContrast, isTrue);
      expect(state.reduceMotion, isTrue);
      expect(state.screenReaderAssist, isTrue);
      expect(state.captions, isFalse);
      // Written to the offline cache too, so the next cold start reads it
      // instantly and offline.
      expect(
        prefs.getString(StorageKeys.accessibilityTextSize),
        AppTextSize.extraLarge.name,
      );
      expect(prefs.getBool(StorageKeys.accessibilityCaptions), isFalse);
    });

    test('an unreachable server leaves the local cache untouched', () async {
      final prefs = FakePrefs(<String, Object>{
        StorageKeys.accessibilityTextSize: AppTextSize.large.name,
        StorageKeys.accessibilityHighContrast: true,
      });
      final container = _containerWith(prefs, _FakePreferencesApi(fail: true));

      await container.read(accessibilitySyncProvider).hydrate();

      final state = container.read(accessibilityControllerProvider);
      expect(state.textSize, AppTextSize.large);
      expect(state.highContrast, isTrue);
    });
  });

  group('AccessibilityPreferencesRepository.decode', () {
    test('reads the stable text-size NAME token, never an index', () {
      final decoded =
          AccessibilityPreferencesRepository.decode(<String, dynamic>{
        'textSize': 'extraLarge',
        'highContrast': true,
        'reduceMotion': true,
        'screenReaderAssist': true,
        'captions': false,
      });

      expect(decoded.textSize, AppTextSize.extraLarge);
      expect(decoded.highContrast, isTrue);
      expect(decoded.reduceMotion, isTrue);
      expect(decoded.screenReaderAssist, isTrue);
      expect(decoded.captions, isFalse);
    });

    test('an absent / unknown payload falls back to the shipped defaults', () {
      final empty =
          AccessibilityPreferencesRepository.decode(const <String, dynamic>{});
      expect(empty.textSize, AppTextSize.normal);
      expect(empty.highContrast, isFalse);
      expect(empty.captions, isTrue); // captions default ON

      final unknown = AccessibilityPreferencesRepository.decode(
        <String, dynamic>{'textSize': 'gigantic'},
      );
      expect(unknown.textSize, AppTextSize.normal);
    });
  });
}
