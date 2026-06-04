import 'dart:io' show Platform;

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'app/app.dart';
import 'app/localization/locale_controller.dart';
import 'core/env/build_config.dart';

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
  if (kIsWeb) {
    // Flutter web is not in scope (SIMF-MAA-001 §2), but we don't want
    // `Platform` accesses to crash if someone runs `flutter run -d chrome`
    // for diagnostics; default to Android in that case.
    return SimfDeviceType.android;
  }
  if (Platform.isIOS) {
    return SimfDeviceType.ios;
  }
  return SimfDeviceType.android;
}
