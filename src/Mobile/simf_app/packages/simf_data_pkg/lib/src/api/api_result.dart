import 'package:meta/meta.dart';

/// Dart side of the SIMF backend's `ApiResult<T>` envelope (SIMF-API-001 §6).
///
/// Every response — success or failure, every endpoint — deserialises into
/// exactly this shape. The repository layer pattern-matches on it and
/// produces either a domain value or an `ApiFailure`.
@immutable
class ApiResult<T> {
  const ApiResult({
    required this.success,
    this.data,
    this.error,
    this.meta,
  });

  /// Decodes an `ApiResult<T>` from a parsed JSON map. [decodeData] is the
  /// function that knows how to turn the `data` field into a `T`.
  ///
  /// When `success` is false, `data` is null by contract — [decodeData] is
  /// not called.
  factory ApiResult.fromJson(
    Map<String, dynamic> json,
    T Function(Object? data) decodeData,
  ) {
    final success = json['success'] as bool? ?? false;
    final rawError = json['error'];
    final rawMeta = json['meta'];
    return ApiResult<T>(
      success: success,
      data: success ? decodeData(json['data']) : null,
      error: rawError is Map<String, dynamic>
          ? ApiResultError.fromJson(rawError)
          : null,
      meta: rawMeta is Map<String, dynamic>
          ? Map<String, dynamic>.from(rawMeta)
          : null,
    );
  }

  final bool success;
  final T? data;
  final ApiResultError? error;
  final Map<String, dynamic>? meta;

  /// True when the envelope reports a successful response AND a non-null
  /// payload is present. Use in repositories to gate the happy path.
  bool get hasData => success && data != null;
}

/// The `error` object inside an [ApiResult]. Fields mirror SIMF-API-001 §7.1.
@immutable
class ApiResultError {
  const ApiResultError({
    required this.code,
    required this.message,
    this.messageArabic = '',
    this.details = const <ApiResultErrorDetail>[],
  });

  factory ApiResultError.fromJson(Map<String, dynamic> json) {
    final detailsRaw = json['details'];
    return ApiResultError(
      code: json['code'] as String? ?? 'UNKNOWN_ERROR',
      message: json['message'] as String? ?? '',
      messageArabic: json['messageArabic'] as String? ?? '',
      details: detailsRaw is List
          ? detailsRaw
              .whereType<Map<String, dynamic>>()
              .map(ApiResultErrorDetail.fromJson)
              .toList(growable: false)
          : const <ApiResultErrorDetail>[],
    );
  }

  /// Stable machine-readable code. Repositories branch on this, not on
  /// [message].
  final String code;

  /// Human-readable message — the English text from the envelope. The server
  /// sends BOTH languages; use [localized] to pick by the app's locale (the
  /// envelope's `message` is not pre-localized by Accept-Language).
  final String message;

  /// The Arabic message from the envelope; empty when the server didn't send
  /// one.
  final String messageArabic;

  /// Field-level errors used mainly for validation responses.
  final List<ApiResultErrorDetail> details;

  /// The locale-appropriate message: the Arabic text when [isArabic] and a
  /// non-empty Arabic message is present, otherwise the English [message].
  String localized({required bool isArabic}) =>
      isArabic && messageArabic.isNotEmpty ? messageArabic : message;
}

/// A single field-level error entry from [ApiResultError.details].
@immutable
class ApiResultErrorDetail {
  const ApiResultErrorDetail({
    required this.field,
    required this.message,
    this.messageArabic = '',
  });

  factory ApiResultErrorDetail.fromJson(Map<String, dynamic> json) {
    return ApiResultErrorDetail(
      field: json['field'] as String? ?? '',
      message: json['message'] as String? ?? '',
      messageArabic: json['messageArabic'] as String? ?? '',
    );
  }

  final String field;
  final String message;
  final String messageArabic;

  /// The locale-appropriate message (Arabic when [isArabic] + present).
  String localized({required bool isArabic}) =>
      isArabic && messageArabic.isNotEmpty ? messageArabic : message;
}
