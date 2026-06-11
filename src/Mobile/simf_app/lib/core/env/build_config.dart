import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Build-flavour configuration.
///
/// Values come from `--dart-define` at compile time so the same binary can
/// point at different environments without code changes (SIMF-MAA-001 §13).
/// Defaults are conservative — the app simply will not boot if the values
/// are missing in production builds, because `SimfDataConfig` requires
/// non-empty fields.
class BuildConfig {
  BuildConfig._();

  /// `dev`, `test`, `prod`. Defaults to `dev` for local development.
  static const String build =
      String.fromEnvironment('SIMF_BUILD', defaultValue: 'dev');

  static const String apiBaseUrl = String.fromEnvironment(
    'SIMF_API_BASE',
    defaultValue: 'https://api.dev.simf.local/api/v1',
  );

  /// Base URL used only for a dev-diagnostics web run (`flutter run -d chrome`).
  /// The Android-emulator alias `10.0.2.2` does not resolve in a browser, so web
  /// defaults to `localhost`; override with `--dart-define=SIMF_API_BASE_WEB=`.
  static const String apiBaseUrlWeb = String.fromEnvironment(
    'SIMF_API_BASE_WEB',
    defaultValue: 'http://localhost:5175/api/v1',
  );

  static const String appKey = String.fromEnvironment(
    'SIMF_APP_KEY',
    defaultValue: 'simf-dev-app-key',
  );

  /// Whether to print request / response summaries in the dio logging
  /// interceptor. Forced off when `build == 'prod'`.
  static bool get enableRequestLogging => build != 'prod';

  /// Produces the [SimfDataConfig] the data package needs.
  ///
  /// [deviceType] is computed by the caller at startup (Android / iOS, or
  /// `web` for a dev `flutter run -d chrome`); this file does not import
  /// `dart:io` so it can be referenced in widget tests. A web run targets the
  /// browser-reachable [apiBaseUrlWeb] instead of [apiBaseUrl].
  static SimfDataConfig dataConfig({required SimfDeviceType deviceType}) {
    return SimfDataConfig(
      baseUrl: kIsWeb ? apiBaseUrlWeb : apiBaseUrl,
      appKey: appKey,
      deviceType: deviceType,
      enableRequestLogging: enableRequestLogging,
    );
  }
}
