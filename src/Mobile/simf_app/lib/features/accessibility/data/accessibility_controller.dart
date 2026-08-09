import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/accessibility/data/accessibility_preferences_repository.dart';
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
///
/// `accessibility-server-sync` — the five flags are also **account** settings:
/// every change is written through to `PUT /app/account/preferences`, and
/// [applyRemote] replays the server's copy at sign-in. Device prefs remain the
/// offline cache and the only READ path, so the app still renders the right
/// scale on the first frame, offline, before any network call. A failed sync
/// never disturbs the local choice.
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
    unawaited(_pushToServer());
  }

  Future<void> setHighContrast(bool value) async {
    await prefs.setBool(StorageKeys.accessibilityHighContrast, value);
    state = state.copyWith(highContrast: value);
    unawaited(_pushToServer());
  }

  Future<void> setReduceMotion(bool value) async {
    await prefs.setBool(StorageKeys.accessibilityReduceMotion, value);
    state = state.copyWith(reduceMotion: value);
    unawaited(_pushToServer());
  }

  Future<void> setScreenReaderAssist(bool value) async {
    await prefs.setBool(StorageKeys.accessibilityScreenReader, value);
    state = state.copyWith(screenReaderAssist: value);
    unawaited(_pushToServer());
  }

  Future<void> setCaptions(bool value) async {
    await prefs.setBool(StorageKeys.accessibilityCaptions, value);
    state = state.copyWith(captions: value);
    unawaited(_pushToServer());
  }

  /// Replays the account's server-held choices over the local cache — called
  /// once at sign-in so the settings follow the user to a new device or a
  /// reinstall. Writes the prefs too, so the next cold start reads them
  /// instantly and offline.
  Future<void> applyRemote(AccessibilitySettings remote) async {
    await prefs.setString(
      StorageKeys.accessibilityTextSize,
      remote.textSize.name,
    );
    await prefs.setBool(
      StorageKeys.accessibilityHighContrast,
      remote.highContrast,
    );
    await prefs.setBool(
      StorageKeys.accessibilityReduceMotion,
      remote.reduceMotion,
    );
    await prefs.setBool(
      StorageKeys.accessibilityScreenReader,
      remote.screenReaderAssist,
    );
    await prefs.setBool(StorageKeys.accessibilityCaptions, remote.captions);
    state = remote;
  }

  /// Best-effort write-through. A failed sync must never disturb the choice the
  /// user just made: the local prefs are already written and stay authoritative
  /// until the next successful sync (same contract as `OrgProfileController.warm`).
  Future<void> _pushToServer() async {
    try {
      await ref.read(accessibilityPreferencesRepositoryProvider).save(state);
    } on Object {
      // Offline / signed-out / transient error — keep the local choice.
    }
  }
}

/// Pulls the account's accessibility preferences down at sign-in. Kept behind
/// its own dependency-free provider so the post-auth seam can fire it without
/// taking a hard dependency on the controller (or the API client) being wired —
/// every risky read happens inside [hydrate]'s guard, and a sync failure can
/// never fail a sign-in.
class AccessibilitySync {
  AccessibilitySync(this._ref);

  final Ref _ref;

  Future<void> hydrate() async {
    try {
      final repository =
          _ref.read(accessibilityPreferencesRepositoryProvider);
      final remote = await repository.fetch();
      if (remote == null) {
        // The account has never saved a choice. Do NOT apply the server's
        // defaults over the device — that would reset a user who had already
        // picked extraLarge + high contrast here (chosen before this endpoint
        // existed, or chosen offline so the write-through failed silently).
        // Seed the account from the device instead, so the first sign-in
        // migrates the local choices up rather than destroying them.
        await repository.save(
          _ref.read(accessibilityControllerProvider),
        );
        return;
      }
      await _ref
          .read(accessibilityControllerProvider.notifier)
          .applyRemote(remote);
    } on Object {
      // Offline / not-yet-provisioned / transient error — the local prefs cache
      // stays authoritative until the next successful sync.
    }
  }
}

final accessibilitySyncProvider =
    Provider<AccessibilitySync>((ref) => AccessibilitySync(ref));

final accessibilityControllerProvider =
    NotifierProvider<AccessibilityController, AccessibilitySettings>(() {
  throw UnimplementedError(
    'accessibilityControllerProvider must be overridden at app startup with '
    'an AccessibilityController whose prefs comes from simfPrefsStorageProvider.',
  );
});
