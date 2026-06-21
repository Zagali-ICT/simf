import 'package:meta/meta.dart';

import 'api_result.dart';

/// Typed failure returned to the repository / domain layer.
///
/// A repository never throws raw `dio` exceptions and a screen never has to
/// branch on HTTP status. Instead the data package translates everything
/// into an [ApiFailure]; the repository maps the failure to a domain-typed
/// failure (or propagates this one); the screen renders an error state.
@immutable
class ApiFailure implements Exception {
  const ApiFailure({
    required this.code,
    required this.message,
    this.httpStatus,
    this.details = const <ApiResultErrorDetail>[],
  });

  /// Build an [ApiFailure] from an [ApiResultError] returned by the
  /// backend envelope on a failed call.
  factory ApiFailure.fromEnvelope(
    ApiResultError error, {
    int? httpStatus,
    bool isArabic = false,
  }) {
    return ApiFailure(
      code: error.code,
      // The envelope carries both languages; pick by the app's locale so the
      // displayed error is translated (the screens read `message` as-is).
      message: error.localized(isArabic),
      httpStatus: httpStatus,
      details: error.details,
    );
  }

  /// Stable error code — see `ApiErrorCodes` for the published set.
  final String code;

  /// User-safe message in the active language.
  final String message;

  /// HTTP status when the call reached the server; null when the call
  /// failed before a response was returned (network down, timeout, etc).
  final int? httpStatus;

  /// Field-level details, used mainly for VALIDATION_FAILED.
  final List<ApiResultErrorDetail> details;

  @override
  String toString() =>
      'ApiFailure(code: $code, http: $httpStatus, message: $message)';
}
