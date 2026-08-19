import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/content/data/content_endpoints.dart';
import 'package:simf_app/features/content/data/content_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// App-local data layer for the public CMS read (`GET /app/content/{key}`,
/// anonymous — Page_009). Throws [ApiFailure] on a wire error; a 404 means the
/// key is unstored / inactive (the screen renders the empty state, not an error).
class ContentRepository {
  ContentRepository(this._client);

  final SimfApiClient _client;

  /// Well-known content key for the Terms & Conditions body (Page_009 L-1).
  static const String termsKey = 'terms';

  Future<ContentBlock> getContentBlock(String key) {
    return _client.get<ContentBlock>(
      ContentEndpoints.byKey(key),
      decodeData: (data) => ContentBlock.fromJson(_asMap(data)),
    );
  }

  static Map<String, dynamic> _asMap(Object? data) =>
      (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

final contentRepositoryProvider = Provider<ContentRepository>((ref) {
  return ContentRepository(ref.watch(simfApiClientProvider));
});

/// The terms block, or **null for the empty state**.
///
/// Two different things mean "there is nothing to show", and both are empty
/// rather than broken (Page_009 L-6): the key is missing or inactive (a 404),
/// or it exists with no body. Folding them here is what lets the screen read
/// `AsyncValue` directly — three server outcomes collapse into the three
/// branches `when` already has, instead of a fourth `_empty` flag beside them.
///
/// Any other failure propagates, so the error branch shows the server's own
/// message rather than a generic one.
final termsBlockProvider =
    FutureProvider.autoDispose<ContentBlock?>((ref) async {
  try {
    final block = await ref
        .watch(contentRepositoryProvider)
        .getContentBlock(ContentRepository.termsKey);
    return block.hasBody ? block : null;
  } on ApiFailure catch (failure) {
    if (failure.httpStatus == 404) {
      return null;
    }
    rethrow;
  }
});
