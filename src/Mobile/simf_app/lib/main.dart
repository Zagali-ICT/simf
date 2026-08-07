import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'app/app.dart';
import 'app/localization/locale_controller.dart';
import 'core/env/build_config.dart';
import 'core/startup/app_version_policy.dart';
import 'features/accessibility/data/accessibility_controller.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Portrait-only: the SIMF app is designed for vertical use, so lock out
  // landscape / auto-rotate app-wide. The native android/ios folders are
  // generated (simf-run), so this Flutter-side lock is the source of truth.
  await SystemChrome.setPreferredOrientations(
    const <DeviceOrientation>[DeviceOrientation.portraitUp],
  );

  // Eagerly resolve the values the providers need at construction time. Prefs
  // and the installed version are independent native round-trips on the
  // cold-boot path, so resolve them concurrently rather than in series.
  final (prefs, installedVersion) =
      await (SimfPrefsStorage.open(), _installedVersion()).wait;
  final deviceType = _deviceType();
  final dataConfig = BuildConfig.dataConfig(deviceType: deviceType);

  runApp(
    ProviderScope(
      overrides: <Override>[
        // The data package's mandatory overrides.
        simfDataConfigProvider.overrideWithValue(dataConfig),
        simfPrefsStorageProvider.overrideWithValue(prefs),

        // D-736 — the real installed version (About/More display + the launch
        // update-policy comparison).
        installedAppVersionProvider.overrideWithValue(installedVersion),

        // The locale controller — wires the prefs-backed implementation.
        localeControllerProvider.overrideWith(
          () => LocaleController(prefs: prefs),
        ),

        // The accessibility controller — persists + applies the Page 038
        // text-size / high-contrast / reduce-motion choices app-wide.
        accessibilityControllerProvider.overrideWith(
          () => AccessibilityController(prefs: prefs),
        ),

        // The current-language provider used by the headers interceptor.
        // It reads `localeControllerProvider` on every call so a language
        // change is picked up immediately by subsequent requests.
        currentLanguageCodeProvider.overrideWith((ref) {
          return () => ref.read(localeControllerProvider).languageCode;
        }),

        // The auth-token-source override — the passive bridge the auth
        // controller registers itself into at build time (D-372). The old
        // eager `ref.read(authControllerProvider.notifier)` pattern formed
        // a circular provider dependency that left the interceptors with a
        // never-initialised controller, so authenticated requests went out
        // without their bearer token (caught by the Wave-1 live E2E).
        authTokenSourceProvider.overrideWith((ref) => AuthTokenBridge()),
      ],
      child: const SimfApp(),
    ),
  );
}

/// The pubspec `version` of this build (D-736). Best-effort: a platform where
/// the plugin cannot resolve it (e.g. a bare web dev run) yields '' — the
/// screens render a dash and the update check fails open.
Future<String> _installedVersion() async {
  try {
    return (await PackageInfo.fromPlatform()).version;
  } on Object {
    return '';
  }
}

SimfDeviceType _deviceType() {
  // Web is a dev-diagnostics target only (SIMF-MAA-001 §2 ships Android + iOS);
  // a `flutter run -d chrome` session reports the truthful Web device type.
  if (kIsWeb) {
    return SimfDeviceType.web;
  }
  // `defaultTargetPlatform` (foundation) is web-safe and avoids a `dart:io`
  // import; with the kIsWeb guard above it only runs on a real device.
  if (defaultTargetPlatform == TargetPlatform.iOS) {
    return SimfDeviceType.ios;
  }
  return SimfDeviceType.android;
}
