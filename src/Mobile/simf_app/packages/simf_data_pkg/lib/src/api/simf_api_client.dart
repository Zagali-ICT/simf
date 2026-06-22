import 'dart:async';
import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../config/simf_data_config.dart';
import 'api_error_codes.dart';
import 'api_failure.dart';
import 'api_result.dart';
import 'auth_token_source.dart';
import 'interceptors/headers_interceptor.dart';
import 'interceptors/logging_interceptor.dart';

/// The one SIMF API client (SIMF-MAA-001 v1.2 §9.1).
///
/// Construct **once** at app start (via the provider in `providers.dart`)
/// and inject the instance everywhere through Riverpod. No other class in
/// the codebase instantiates `dio.Dio` or holds a `dio.Dio` reference.
///
/// Lifecycle of a request:
///
///   1. The headers interceptor attaches the standard headers.
///   2. dio sends the request; the response (success or failure) comes back
///      as an [ApiResult] envelope (`validateStatus` is open so 4xx and 5xx
///      come through with their body intact).
///   3. If the status is 401 on the first attempt, [_send] asks
///      [AuthTokenSource] to refresh; on success the request is replayed
///      exactly once.
///   4. The envelope is parsed. A `success: true` response returns the
///      data; a `success: false` envelope is thrown as [ApiFailure].
class SimfApiClient {
  SimfApiClient._(this._dio, this._tokenSource, this._currentLanguageCode);

  factory SimfApiClient.build({
    required SimfDataConfig config,
    required AuthTokenSource tokenSource,
    required String Function() currentLanguageCode,
    Dio? dioOverride,
  }) {
    final dio = dioOverride ??
        Dio(
          BaseOptions(
            baseUrl: config.baseUrl,
            connectTimeout: config.connectTimeout,
            receiveTimeout: config.receiveTimeout,
            sendTimeout: config.sendTimeout,
            responseType: ResponseType.json,
            // Accept any HTTP status — the envelope (which the backend
            // always returns) is the authoritative result. SIMF-API-001 §8
            // says the HTTP status and the envelope agree.
            validateStatus: (_) => true,
          ),
        );

    dio.interceptors.add(
      HeadersInterceptor(
        config: config,
        tokenSource: tokenSource,
        currentLanguageCode: currentLanguageCode,
      ),
    );
    dio.interceptors.add(
      LoggingInterceptor(enabled: config.enableRequestLogging),
    );

    return SimfApiClient._(dio, tokenSource, currentLanguageCode);
  }

  final Dio _dio;
  final AuthTokenSource _tokenSource;
  // The app's current language code (e.g. 'ar'/'en'), used to pick the
  // locale-appropriate error message from the bilingual envelope.
  final String Function() _currentLanguageCode;

  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    required T Function(Object? data) decodeData,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      (options) => _dio.get<dynamic>(
        path,
        queryParameters: queryParameters,
        cancelToken: cancelToken,
        options: options,
      ),
      decodeData,
    );
  }

  /// [skipAuthRefresh] marks the request so a 401 on it does NOT trigger the
  /// token-refresh-and-retry path. Set it for the refresh call itself — otherwise
  /// a 401 on `/app/auth/refresh` re-enters the single-flight [AuthTokenSource.refresh]
  /// (the same in-flight future the refresh is running inside) and deadlocks.
  Future<T> post<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? queryParameters,
    required T Function(Object? data) decodeData,
    CancelToken? cancelToken,
    bool skipAuthRefresh = false,
  }) {
    return _send<T>(
      (options) => _dio.post<dynamic>(
        path,
        data: body,
        queryParameters: queryParameters,
        cancelToken: cancelToken,
        options: _maybeSkipRefresh(options, skipAuthRefresh),
      ),
      decodeData,
    );
  }

  Future<T> put<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? queryParameters,
    required T Function(Object? data) decodeData,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      (options) => _dio.put<dynamic>(
        path,
        data: body,
        queryParameters: queryParameters,
        cancelToken: cancelToken,
        options: options,
      ),
      decodeData,
    );
  }

  Future<T> patch<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? queryParameters,
    required T Function(Object? data) decodeData,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      (options) => _dio.patch<dynamic>(
        path,
        data: body,
        queryParameters: queryParameters,
        cancelToken: cancelToken,
        options: options,
      ),
      decodeData,
    );
  }

  Future<T> delete<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? queryParameters,
    required T Function(Object? data) decodeData,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      (options) => _dio.delete<dynamic>(
        path,
        data: body,
        queryParameters: queryParameters,
        cancelToken: cancelToken,
        options: options,
      ),
      decodeData,
    );
  }

  /// Multipart upload — posts [bytes] as a single file field plus optional
  /// [fields]. Keeps all dio/multipart knowledge inside this package
  /// (SIMF-MAA-001 §9.1). Used for the self-service ID-image upload (Page 007).
  ///
  /// [contentType] is the MIME of the file part (e.g. `image/jpeg`). It MUST be
  /// set for endpoints that gate on the part's content-type — the SIMF ID-image
  /// endpoint checks `image/jpeg|png|webp` (+ magic bytes), so without it dio
  /// would default to `application/octet-stream` and the upload would be rejected.
  Future<T> upload<T>(
    String path, {
    required List<int> bytes,
    required String filename,
    required T Function(Object? data) decodeData,
    String fileField = 'File',
    String? contentType,
    Map<String, dynamic>? fields,
    CancelToken? cancelToken,
  }) {
    final form = FormData.fromMap(<String, dynamic>{
      ...?fields,
      fileField: MultipartFile.fromBytes(
        bytes,
        filename: filename,
        contentType: contentType == null
            ? null
            : DioMediaType.parse(contentType),
      ),
    });
    return _send<T>(
      (options) => _dio.post<dynamic>(
        path,
        data: form,
        cancelToken: cancelToken,
        options: options,
      ),
      decodeData,
    );
  }

  /// Fetches a **raw text** body (not the `ApiResult` envelope) — for the file
  /// export endpoints that return `text/calendar` / `text/vcard` (Page_014
  /// E2/E3). Refreshes + replays once on a 401 like the envelope path. Throws
  /// [ApiFailure] on a transport error or a non-2xx status.
  Future<String> getText(
    String path, {
    Map<String, dynamic>? queryParameters,
    CancelToken? cancelToken,
  }) async {
    final response = await _execute(
      (options) => _dio.get<dynamic>(
        path,
        queryParameters: queryParameters,
        cancelToken: cancelToken,
        options: (options ?? Options())..responseType = ResponseType.plain,
      ),
    );

    final status = response.statusCode ?? 0;
    if (status < 200 || status >= 300) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'Request failed with status $status.',
        httpStatus: status,
      );
    }

    final body = response.data;
    return body is String ? body : (body?.toString() ?? '');
  }

  /// Fetches a **raw binary** body (not the `ApiResult` envelope) — for the
  /// streamed image endpoints that return image bytes (e.g. the account avatar
  /// `GET /app/account/avatar/{id}`). Goes through this client so it inherits
  /// the bearer + `X-App-Key` headers and the self-signed-TLS handling that a
  /// bare `Image.network` cannot. Refreshes + replays once on a 401 like the
  /// other paths. Returns the bytes on a 2xx; throws [ApiFailure] on a transport
  /// error or a non-2xx status (a 404 = "no avatar", surfaced to the caller).
  Future<Uint8List> getBytes(
    String path, {
    Map<String, dynamic>? queryParameters,
    CancelToken? cancelToken,
  }) async {
    final response = await _execute(
      (options) => _dio.get<dynamic>(
        path,
        queryParameters: queryParameters,
        cancelToken: cancelToken,
        options: (options ?? Options())..responseType = ResponseType.bytes,
      ),
    );

    final status = response.statusCode ?? 0;
    if (status < 200 || status >= 300) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'Request failed with status $status.',
        httpStatus: status,
      );
    }

    final body = response.data;
    if (body is List<int>) {
      return Uint8List.fromList(body);
    }
    throw ApiFailure(
      code: ApiErrorCodes.clientMalformedResponse,
      message: 'Expected a binary response body.',
      httpStatus: status,
    );
  }

  Future<T> _send<T>(
    Future<Response<dynamic>> Function(Options? options) call,
    T Function(Object? data) decodeData,
  ) async {
    final response = await _execute(call);
    return _parseEnvelope<T>(response, decodeData);
  }

  /// Sends [call], refreshing + replaying once on a single 401, and returns the
  /// raw [Response]. Shared by the envelope path ([_send]) and the raw-text path
  /// ([getText]).
  Future<Response<dynamic>> _execute(
    Future<Response<dynamic>> Function(Options? options) call,
  ) async {
    Response<dynamic> response;
    try {
      response = await call(null);
    } on DioException catch (e) {
      throw _mapDioException(e);
    } catch (e) {
      throw ApiFailure(
        code: ApiErrorCodes.unknown,
        message: e.toString(),
      );
    }

    // Refresh + retry on a single 401. The token source serialises
    // concurrent refresh attempts on its end. Requests marked skip-refresh
    // (the refresh call itself, and the post-refresh retry) do NOT re-enter the
    // refresh path — that re-entry would deadlock the single-flight refresh
    // against itself / loop the retry.
    if (response.statusCode == 401 &&
        response.requestOptions.extra[_extraSkipRefresh] != true) {
      final refreshed = await _refreshBounded();
      if (refreshed) {
        try {
          response = await call(_skipRefreshOptions());
        } on DioException catch (e) {
          throw _mapDioException(e);
        }
      } else {
        await _tokenSource.onSessionExpired();
      }
    }

    return response;
  }

  /// A refresh-attempt timeout. The Dio HTTP timeouts cover the network call,
  /// but a stalled single-flight refresh future (e.g. one wedged awaiting a
  /// never-completed Completer) would otherwise hang the request forever and
  /// spin the UI indefinitely. Bound it so a stuck refresh is treated as
  /// "not refreshed" — the original 401 then surfaces as a normal ApiFailure
  /// (the screen shows its error toast) instead of an endless spinner.
  static const Duration _refreshTimeout = Duration(seconds: 20);

  Future<bool> _refreshBounded() async {
    try {
      return await _tokenSource.refresh().timeout(_refreshTimeout);
    } on TimeoutException {
      return false;
    }
  }

  T _parseEnvelope<T>(
    Response<dynamic> response,
    T Function(Object? data) decodeData,
  ) {
    final body = response.data;
    if (body is! Map<String, dynamic>) {
      throw ApiFailure(
        code: ApiErrorCodes.clientMalformedResponse,
        message: 'Response body was not a JSON object.',
        httpStatus: response.statusCode,
      );
    }

    final ApiResult<T> result;
    try {
      result = ApiResult<T>.fromJson(body, decodeData);
    } catch (e) {
      throw ApiFailure(
        code: ApiErrorCodes.clientMalformedResponse,
        message: 'Could not parse response: $e',
        httpStatus: response.statusCode,
      );
    }

    if (result.success && result.data is T) {
      return result.data as T;
    }

    if (result.error != null) {
      throw ApiFailure.fromEnvelope(
        result.error!,
        httpStatus: response.statusCode,
        isArabic: _currentLanguageCode() == 'ar',
      );
    }

    throw ApiFailure(
      code: ApiErrorCodes.clientMalformedResponse,
      message: 'Envelope marked success but data was missing.',
      httpStatus: response.statusCode,
    );
  }

  /// Used on the single retry after a refresh. Marks the request so a
  /// second 401 doesn't re-enter the refresh path and loop.
  Options _skipRefreshOptions() {
    return Options(
      extra: <String, dynamic>{_extraSkipRefresh: true},
    );
  }

  /// Returns [options] with the skip-refresh marker merged in when [skip] is set
  /// (else unchanged). Lets [post] tag the refresh call so its own 401 surfaces
  /// as a normal failure instead of re-entering the single-flight refresh.
  Options? _maybeSkipRefresh(Options? options, bool skip) {
    if (!skip) {
      return options;
    }
    final base = options ?? Options();
    return base.copyWith(
      extra: <String, dynamic>{...?base.extra, _extraSkipRefresh: true},
    );
  }

  ApiFailure _mapDioException(DioException e) {
    switch (e.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        return ApiFailure(
          code: ApiErrorCodes.clientTimeout,
          message: e.message ?? 'Request timed out.',
        );
      case DioExceptionType.cancel:
        return ApiFailure(
          code: ApiErrorCodes.clientCancelled,
          message: e.message ?? 'Request was cancelled.',
        );
      case DioExceptionType.connectionError:
        return ApiFailure(
          code: ApiErrorCodes.clientNetwork,
          message: e.message ?? 'Network is unreachable.',
        );
      case DioExceptionType.badCertificate:
      case DioExceptionType.badResponse:
      case DioExceptionType.unknown:
        return ApiFailure(
          code: ApiErrorCodes.clientNetwork,
          message: e.message ?? 'Network error.',
          httpStatus: e.response?.statusCode,
        );
    }
  }

  /// Marker on the request `extra` map. Reserved for the [_send] retry-after-
  /// refresh path. Currently the marker is set but no interceptor reads it;
  /// the single-retry contract is enforced by [_send] itself (it does not
  /// recurse). Kept as a stable key for future extension.
  static const String _extraSkipRefresh = 'simf.auth.skipRefresh';
}
