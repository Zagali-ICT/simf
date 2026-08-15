import 'package:dio/dio.dart';
import 'package:simf_data_pkg/src/api/auth_token_source.dart';
import 'package:simf_data_pkg/src/config/simf_data_config.dart';

/// Attaches the SIMF standard request headers to every request
/// (SIMF-API-001 §5).
///
/// `X-App-Key` and `X-Device-Type` come from [SimfDataConfig].
/// `Accept-Language` is read from a getter passed in at construction (the
/// main app exposes the current locale via a Riverpod provider; the wiring
/// of the dio instance reads it once per request).
/// `Authorization` is read on every request from [AuthTokenSource] — null
/// means no header, which is correct for guest endpoints.
class HeadersInterceptor extends Interceptor {
  HeadersInterceptor({
    required this.config,
    required this.tokenSource,
    required this.currentLanguageCode,
  });

  final SimfDataConfig config;
  final AuthTokenSource tokenSource;

  /// Returns `ar` or `en`. The interceptor calls it on every request so a
  /// language change picks up immediately.
  final String Function() currentLanguageCode;

  @override
  void onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) {
    options.headers[_xAppKey] = config.appKey;
    options.headers[_xDeviceType] = config.deviceType.headerValue;
    options.headers[_acceptLanguage] = currentLanguageCode();

    final token = tokenSource.currentAccessToken();
    if (token != null && token.isNotEmpty) {
      options.headers[_authorization] = 'Bearer $token';
    } else {
      options.headers.remove(_authorization);
    }

    if (_isStateChangingMethod(options.method)) {
      // X-Anti-Forgery token is supplied by the backend the first time the
      // app fetches a CSRF-protected resource. Until that flow lands the
      // header is omitted; protected POSTs from native mobile against a
      // bearer token are not the brute-force surface CSRF protects.
    }

    handler.next(options);
  }

  static bool _isStateChangingMethod(String method) {
    final upper = method.toUpperCase();
    return upper == 'POST' ||
        upper == 'PUT' ||
        upper == 'PATCH' ||
        upper == 'DELETE';
  }

  static const String _xAppKey = 'X-App-Key';
  static const String _xDeviceType = 'X-Device-Type';
  static const String _acceptLanguage = 'Accept-Language';
  static const String _authorization = 'Authorization';
}
