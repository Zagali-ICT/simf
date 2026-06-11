import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'app/app.dart';
import 'app/localization/locale_controller.dart';
import 'core/env/build_config.dart';
import 'features/accessibility/data/accessibility_controller.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Eagerly resolve the two values the providers need at construction time
  // (Prefs and the device type), then build the override list.
  final prefs = await SimfPrefsStorage.open();
  final deviceType = _deviceType();
  final dataConfig = BuildConfig.dataConfig(deviceType: deviceType);

  runApp(
    ProviderScope(
      overrides: <Override>[
        // The data package's mandatory overrides.
        simfDataConfigProvider.overrideWithValue(dataConfig),
        simfPrefsStorageProvider.overrideWithValue(prefs),

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

        // The auth-token-source override — points the data package's
        // interceptors at the auth controller (which implements
        // AuthTokenSource). Reading the notifier is safe because the
        // controller's `build()` reads its own dependencies from the
        // overridden providers above.
        authTokenSourceProvider.overrideWith((ref) {
          return ref.read(authControllerProvider.notifier);
        }),
      ],
      child: const SimfApp(),
    ),
  );
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
