import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The text-size choices offered on the accessibility screen (Page 038; Figma
/// 1116:16630 صغير / متوسط / كبير / أكبر). Persisted by stable `.name`, so the
/// cases can be reordered freely and an unknown stored value falls back to
/// [normal].
enum AppTextSize { small, normal, large, extraLarge }

extension AppTextSizeScale on AppTextSize {
  /// The MediaQuery text-scale factor applied app-wide for this choice.
  double get scaleFactor {
    switch (this) {
      case AppTextSize.small:
        return 0.85;
      case AppTextSize.normal:
        return 1.0;
      case AppTextSize.large:
        return 1.15;
      case AppTextSize.extraLarge:
        return 1.3;
    }
  }
}

/// The user's accessibility preferences. Immutable; the controller swaps the
/// whole value on each change so widgets rebuild from a single source.
@immutable
class AccessibilitySettings {
  const AccessibilitySettings({
    this.textSize = AppTextSize.normal,
    this.highContrast = false,
    this.reduceMotion = false,
    this.screenReaderAssist = false,
    this.captions = true,
  });

  final AppTextSize textSize;
  final bool highContrast;
  final bool reduceMotion;

  /// When on, the app announces each screen on navigation via the platform
  /// accessibility channel (Figma "قارئ الشاشة"). Off by default.
  final bool screenReaderAssist;

  /// When on, the live/session AI-caption strip is shown (Figma "الترجمة
  /// النصية (للجلسات)"). On by default — turning it off hides the strip.
  final bool captions;

  AccessibilitySettings copyWith({
    AppTextSize? textSize,
    bool? highContrast,
    bool? reduceMotion,
    bool? screenReaderAssist,
    bool? captions,
  }) {
    return AccessibilitySettings(
      textSize: textSize ?? this.textSize,
      highContrast: highContrast ?? this.highContrast,
      reduceMotion: reduceMotion ?? this.reduceMotion,
      screenReaderAssist: screenReaderAssist ?? this.screenReaderAssist,
      captions: captions ?? this.captions,
    );
  }
}

/// Holds the accessibility preferences and persists each change to prefs, so
/// the choices survive a restart and are applied app-wide (text scale, theme,
/// reduced motion). Mirrors the read-on-boot / write-on-change shape of
/// `LocaleController`.
class AccessibilityController extends Notifier<AccessibilitySettings> {
  AccessibilityController({required this.prefs});

  final SimfPrefsStorage prefs;

  @override
  AccessibilitySettings build() {
    return AccessibilitySettings(
      textSize: _readTextSize(),
      highContrast:
          prefs.getBool(StorageKeys.accessibilityHighContrast) ?? false,
      reduceMotion:
          prefs.getBool(StorageKeys.accessibilityReduceMotion) ?? false,
      screenReaderAssist:
          prefs.getBool(StorageKeys.accessibilityScreenReader) ?? false,
      // Defaults on so the existing live-caption strip stays visible until the
      // user opts out.
      captions: prefs.getBool(StorageKeys.accessibilityCaptions) ?? true,
    );
  }

  AppTextSize _readTextSize() {
    final stored = prefs.getString(StorageKeys.accessibilityTextSize);
    for (final value in AppTextSize.values) {
      if (value.name == stored) {
        return value;
      }
    }
    return AppTextSize.normal;
  }

  Future<void> setTextSize(AppTextSize value) async {
    await prefs.setString(StorageKeys.accessibilityTextSize, value.name);
    state = state.copyWith(textSize: value);
  }

  Future<void> setHighContrast(bool value) async {
    await prefs.setBool(StorageKeys.accessibilityHighContrast, value);
    state = state.copyWith(highContrast: value);
  }

  Future<void> setReduceMotion(bool value) async {
    await prefs.setBool(StorageKeys.accessibilityReduceMotion, value);
    state = state.copyWith(reduceMotion: value);
  }

  Future<void> setScreenReaderAssist(bool value) async {
    await prefs.setBool(StorageKeys.accessibilityScreenReader, value);
    state = state.copyWith(screenReaderAssist: value);
  }

  Future<void> setCaptions(bool value) async {
    await prefs.setBool(StorageKeys.accessibilityCaptions, value);
    state = state.copyWith(captions: value);
  }
}

final accessibilityControllerProvider =
    NotifierProvider<AccessibilityController, AccessibilitySettings>(() {
  throw UnimplementedError(
    'accessibilityControllerProvider must be overridden at app startup with '
    'an AccessibilityController whose prefs comes from simfPrefsStorageProvider.',
  );
});
