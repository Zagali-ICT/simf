import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'content_models.dart';

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
      '/app/content/$key',
      decodeData: (data) => ContentBlock.fromJson(_asMap(data)),
    );
  }

  static Map<String, dynamic> _asMap(Object? data) =>
      (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

final contentRepositoryProvider = Provider<ContentRepository>((ref) {
  return ContentRepository(ref.watch(simfApiClientProvider));
});
