import 'package:meta/meta.dart';

/// Configuration the host app injects into the data package.
///
/// Nothing here is hardcoded inside `simf_data_pkg`; the main app reads
/// the values from its build-flavour config (dev / test / prod, see
/// SIMF-MAA-001 §13) and supplies them via the
/// `simfDataConfigProvider` override at app startup.
///
/// This keeps the data package environment-agnostic and stops the production
/// `X-App-Key` from ever sitting next to test fixtures in the same file.
@immutable
class SimfDataConfig {
  const SimfDataConfig({
    required this.baseUrl,
    required this.appKey,
    required this.deviceType,
    this.connectTimeout = const Duration(seconds: 15),
    this.receiveTimeout = const Duration(seconds: 30),
    this.sendTimeout = const Duration(seconds: 30),
    this.enableRequestLogging = false,
  });

  /// The base URL of the SIMF backend, including `/api/v1` (or whatever
  /// the current major version is). Example:
  /// `https://api.dev.simf.example/api/v1`.
  final String baseUrl;

  /// The `X-App-Key` value the backend uses to identify the calling
  /// application (SIMF-API-001 §5). One key per environment, per app
  /// shell. Never committed.
  final String appKey;

  /// The `X-Device-Type` header value: `Android`, `iOS`, or (dev only) `Web`.
  /// The main app computes this from the platform at startup.
  final SimfDeviceType deviceType;

  final Duration connectTimeout;
  final Duration receiveTimeout;
  final Duration sendTimeout;

  /// When true, the logging interceptor prints request / response
  /// summaries. Always off in production builds.
  final bool enableRequestLogging;
}

/// The legal values for the `X-Device-Type` header that come from the
/// mobile app (SIMF-API-001 §5). `web` is sent only by a dev-diagnostics
/// `flutter run -d chrome` session (SIMF-MAA-001 §2 ships Android + iOS);
/// `ControlPanel` exists on the header contract but is not produced here.
enum SimfDeviceType {
  android('Android'),
  ios('iOS'),
  web('Web');

  const SimfDeviceType(this.headerValue);
  final String headerValue;
}
