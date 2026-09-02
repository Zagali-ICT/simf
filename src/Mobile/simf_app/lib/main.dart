import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:simf_app/app/app.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/theme/system_ui.dart';
import 'package:simf_app/core/env/build_config.dart';
import 'package:simf_app/core/startup/app_version_policy.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // MainActivity enables edge-to-edge before Flutter creates its content.
  // Flutter owns only icon brightness; AndroidX owns the system-bar layout and
  // backward-compatible transparent/translucent protection.
  SystemChrome.setSystemUIOverlayStyle(SimfSystemUi.edgeToEdge);

  // Portrait-only: the SIMF app is designed for vertical use, so lock out
  // landscape / auto-rotate app-wide. Both native projects are TRACKED, not
  // generated (BUG-010), and the iOS Info.plist deliberately still PERMITS
  // landscape - the live player rotates into it for fullscreen video, and a
  // plist that forbade it would block that rotation. So this Flutter-side
  // lock stays the source of truth for what the app actually does.
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
      // Automatic provider retry stays OFF. Riverpod 3 would re-run a failed
      // fetch up to ten times behind an exponential backoff, but this app's
      // failures are largely deterministic — the visitor, moderation and seat
      // screens branch on 403/404, a state a second call will not change — so
      // a backoff loop is a client-side retry storm against the live API.
      // Every data screen renders an explicit error state whose button is the
      // retry, and pull-to-refresh is the other manual path; returning null
      // keeps a failure settling into AsyncError where both expect it.
      // Nested scopes inherit this from the root container.
      retry: (retryCount, error) => null,
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
