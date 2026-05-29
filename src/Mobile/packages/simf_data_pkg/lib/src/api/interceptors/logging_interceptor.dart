import 'dart:developer' as developer;

import 'package:dio/dio.dart';

/// Lightweight, opt-in logging interceptor. Off in production builds.
///
/// Logs the method, the path (no query), and the status code. Does not log
/// headers (which can carry the access token) or bodies (which can carry
/// credentials or PII).
class LoggingInterceptor extends Interceptor {
  LoggingInterceptor({this.enabled = false});

  final bool enabled;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    if (enabled) {
      developer.log(
        '${options.method} ${options.uri.path}',
        name: 'simf.api',
      );
    }
    handler.next(options);
  }

  @override
  void onResponse(
    Response<dynamic> response,
    ResponseInterceptorHandler handler,
  ) {
    if (enabled) {
      developer.log(
        '${response.statusCode} ${response.requestOptions.method} '
        '${response.requestOptions.uri.path}',
        name: 'simf.api',
      );
    }
    handler.next(response);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    if (enabled) {
      developer.log(
        '${err.type} ${err.response?.statusCode ?? ''} '
        '${err.requestOptions.method} ${err.requestOptions.uri.path}',
        name: 'simf.api',
        error: err.message,
      );
    }
    handler.next(err);
  }
}
