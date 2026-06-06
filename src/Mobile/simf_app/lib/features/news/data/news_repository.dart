import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'news_models.dart';

/// Data layer for the news article detail (Page_029). The read is **public**
/// (`AllowAnonymous`). Throws [ApiFailure] on a wire error — the article screen
/// maps a 404 to "not found". (The list uses `newsListProvider` in
/// `news_screen.dart`.)
class NewsRepository {
  NewsRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/news/{id}` → the full article. 404 when missing / unpublished.
  Future<NewsArticle> getArticle(String id) {
    return _client.get<NewsArticle>(
      '/app/news/$id',
      decodeData: (data) => NewsArticle.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }
}

final newsRepositoryProvider = Provider<NewsRepository>((ref) {
  return NewsRepository(ref.watch(simfApiClientProvider));
});
