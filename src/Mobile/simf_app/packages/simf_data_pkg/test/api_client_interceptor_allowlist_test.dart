import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import 'package:simf_data_pkg/src/api/interceptors/headers_interceptor.dart';
import 'package:simf_data_pkg/src/api/interceptors/logging_interceptor.dart';

/// Pins the interceptor stack of the one API client, so a body logger cannot
/// be added back without the build going red.
///
/// A `PrettyDioLogger` sat on this stack until 2026-08-20 with
/// `requestBody: true` and `responseBody` left at its default of `true`. It
/// printed the sign-in POST body — the real email and the real password — and
/// the sign-in response body — the access token and the refresh token — to the
/// device log, where `adb logcat`, a device-log export or `READ_LOGS` on a
/// managed handset could read them.
///
/// The reason it survived review is worth keeping: it WAS gated, on
/// `config.enableRequestLogging`, which reads as a flavour switch and looks
/// safe. It is not. That flag is `SIMF_BUILD != 'prod'`, `SIMF_BUILD` defaults
/// to `dev`, and `BuildConfig.apiBaseUrl` defaults to the PRODUCTION edge —
/// the two defaults disagree, deliberately. So the same change that made the
/// base URL safe against a forgotten `--dart-define` made the logger unsafe
/// against one: `flutter build apk --release` with no defines shipped a
/// binary that talked to production with full body logging on, and nothing at
/// compile time or run time said so.
///
/// This is an allowlist, not a ban on one class name, because the defect is
/// "an interceptor that logs a body", not "PrettyDioLogger". Adding an
/// interceptor here is a deliberate act: add it to [_allowed] and state what
/// it logs.
void main() {
  // Type name -> what it is allowed to put in the log.
  const allowed = <String, String>{
    // dio installs this one itself, ahead of ours, from the Dio() constructor.
    // It only derives Content-Type from the payload shape and writes nothing
    // anywhere. It is on the list because this test found it on its first run,
    // which is the allowlist doing its job.
    'ImplyContentTypeInterceptor': 'no logging; dio built-in',
    'HeadersInterceptor': 'no logging at all; attaches the standard headers',
    'LoggingInterceptor':
        'method, path (no query) and status only — never a header, never a '
            'body (see its own doc comment)',
  };

  test('the API client carries no interceptor that can log a body', () {
    final dio = Dio();

    SimfApiClient.build(
      config: const SimfDataConfig(
        baseUrl: 'https://api.test/api/v1',
        appKey: 'test-app-key',
        deviceType: SimfDeviceType.android,
        // The dangerous setting, on purpose: if a body logger is ever added
        // back behind this flag, this test must be the thing that catches it.
        enableRequestLogging: true,
      ),
      tokenSource: const NoAuthTokenSource(),
      currentLanguageCode: () => 'ar',
      dioOverride: dio,
    );

    final unexpected = dio.interceptors
        .map((interceptor) => interceptor.runtimeType.toString())
        .where((name) => !allowed.containsKey(name))
        .toList();

    expect(
      unexpected,
      isEmpty,
      reason: 'Unreviewed interceptor(s) on the SIMF API client: $unexpected.\n'
          'Every interceptor here sees the sign-in request body (email + '
          'password) and the sign-in response body (access + refresh token). '
          'If the new one logs either, it leaks credentials to the device log '
          'in any build that forgets --dart-define=SIMF_BUILD=prod, which is '
          'every build by default. If it genuinely logs nothing, add it to '
          'the allowlist in this test with a note saying so.',
    );
  });

  test('the two allowed interceptors are actually installed', () {
    final dio = Dio();

    SimfApiClient.build(
      config: const SimfDataConfig(
        baseUrl: 'https://api.test/api/v1',
        appKey: 'test-app-key',
        deviceType: SimfDeviceType.android,
      ),
      tokenSource: const NoAuthTokenSource(),
      currentLanguageCode: () => 'ar',
      dioOverride: dio,
    );

    expect(dio.interceptors.whereType<HeadersInterceptor>(), hasLength(1));
    expect(dio.interceptors.whereType<LoggingInterceptor>(), hasLength(1));
  });
}
