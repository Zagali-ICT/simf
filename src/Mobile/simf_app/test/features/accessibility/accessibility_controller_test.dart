import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';
import '_fake_prefs.dart';

ProviderContainer _containerWith(FakePrefs prefs) {
  final container = ProviderContainer(
    retry: simfTestNoRetry,
    overrides: <Override>[
      accessibilityControllerProvider
          .overrideWith(() => AccessibilityController(prefs: prefs)),
    ],
  );
  addTearDown(container.dispose);
  return container;
}

void main() {
  group('AccessibilityController', () {
    test('defaults to normal / off when prefs are empty', () {
      final container = _containerWith(FakePrefs());
      final state = container.read(accessibilityControllerProvider);
      expect(state.textSize, AppTextSize.normal);
      expect(state.highContrast, isFalse);
      expect(state.reduceMotion, isFalse);
    });

    test('reads persisted values on build', () {
      final prefs = FakePrefs(<String, Object>{
        StorageKeys.accessibilityTextSize: AppTextSize.large.name,
        StorageKeys.accessibilityHighContrast: true,
        StorageKeys.accessibilityReduceMotion: true,
      });
      final state = _containerWith(prefs).read(accessibilityControllerProvider);
      expect(state.textSize, AppTextSize.large);
      expect(state.highContrast, isTrue);
      expect(state.reduceMotion, isTrue);
    });

    test('falls back to normal when the stored text-size token is unknown', () {
      final prefs = FakePrefs(<String, Object>{
        StorageKeys.accessibilityTextSize: 'gigantic',
      });
      expect(
        _containerWith(prefs).read(accessibilityControllerProvider).textSize,
        AppTextSize.normal,
      );
    });

    test('setTextSize persists the name token and updates state', () async {
      final prefs = FakePrefs();
      final container = _containerWith(prefs);
      await container
          .read(accessibilityControllerProvider.notifier)
          .setTextSize(AppTextSize.small);
      expect(
        container.read(accessibilityControllerProvider).textSize,
        AppTextSize.small,
      );
      expect(
        prefs.getString(StorageKeys.accessibilityTextSize),
        AppTextSize.small.name,
      );
    });

    test('setHighContrast and setReduceMotion persist + update state',
        () async {
      final prefs = FakePrefs();
      final container = _containerWith(prefs);
      final controller =
          container.read(accessibilityControllerProvider.notifier);
      await controller.setHighContrast(true);
      await controller.setReduceMotion(true);
      final state = container.read(accessibilityControllerProvider);
      expect(state.highContrast, isTrue);
      expect(state.reduceMotion, isTrue);
      expect(prefs.getBool(StorageKeys.accessibilityHighContrast), isTrue);
      expect(prefs.getBool(StorageKeys.accessibilityReduceMotion), isTrue);
    });

    test('scaleFactor maps each text size', () {
      expect(AppTextSize.small.scaleFactor, 0.85);
      expect(AppTextSize.normal.scaleFactor, 1.0);
      expect(AppTextSize.large.scaleFactor, 1.15);
    });
  });
}
