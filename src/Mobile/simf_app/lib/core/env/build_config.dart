import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Build-flavour configuration.
///
/// Values come from `--dart-define` at compile time so the same binary can
/// point at different environments without code changes (SIMF-MAA-001 §13).
/// The API base URL defaults to the **production** host
/// (`https://api.simrsnf.com`), so a build with no overrides always
/// runs against prod — a device build can never silently fall back to a
/// dev/LAN host (the stale-`--dart-define` trap). Override only for local dev.
class BuildConfig {
  BuildConfig._();

  /// `dev`, `test`, `prod`. Defaults to `dev` for local development.
  static const String build =
      String.fromEnvironment('SIMF_BUILD', defaultValue: 'dev');

  // The app targets the PRODUCTION API by default, so any build — including a
  // plain `flutter build apk` with no `--dart-define` — runs against prod and
  // can never fall back to a non-prod host. Override only for local dev.
  static const String apiBaseUrl = String.fromEnvironment(
    'SIMF_API_BASE',
    defaultValue: 'https://api.simrsnf.com/api/v1',
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

  /// Official support contacts for the registration-success screen's
  /// تواصل معانا tiles (D-369). Empty (the default) keeps a tile inert —
  /// never a dead dialer/mail intent — until the real values are supplied
  /// at build time via `--dart-define`.
  static const String supportPhone =
      String.fromEnvironment('SIMF_SUPPORT_PHONE');
  static const String supportEmail =
      String.fromEnvironment('SIMF_SUPPORT_EMAIL');

  /// Official social-profile links for the home "تابعنا" row (W2 home,
  /// frame 203:1236). Same contract as the contact tiles (D-369): an empty
  /// value keeps that button inert until the real URL is supplied at build
  /// time via `--dart-define`.
  static const String socialXUrl = String.fromEnvironment('SIMF_SOCIAL_X');
  static const String socialInstagramUrl =
      String.fromEnvironment('SIMF_SOCIAL_INSTAGRAM');
  static const String socialLinkedInUrl =
      String.fromEnvironment('SIMF_SOCIAL_LINKEDIN');
  static const String socialYouTubeUrl =
      String.fromEnvironment('SIMF_SOCIAL_YOUTUBE');
  static const String socialTikTokUrl =
      String.fromEnvironment('SIMF_SOCIAL_TIKTOK');

  /// The روح السعودية discover link (guest + signed-in home). The default is
  /// the public Visit Saudi site; overridable per build.
  static const String visitSaudiUrl = String.fromEnvironment(
    'SIMF_VISIT_SAUDI_URL',
    defaultValue: 'https://www.visitsaudi.com',
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
