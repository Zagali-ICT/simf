import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/news/data/news_endpoints.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// `GET /app/news` → the latest news list (public, D-199). Consumed by the News
/// screen + the Home highlights carousel (both via the `news_screen.dart`
/// re-export, so existing `show newsListProvider` imports keep resolving).
final newsListProvider =
    FutureProvider.autoDispose<List<NewsListItem>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<NewsListItem>>(
    NewsEndpoints.list,
    decodeData: NewsListItem.listFromData,
  );
});

/// Data layer for the news article detail (Page_029). The read is **public**
/// (`AllowAnonymous`). Throws [ApiFailure] on a wire error — the article screen
/// maps a 404 to "not found".
class NewsRepository {
  NewsRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/news/{id}` → the full article. 404 when missing / unpublished.
  Future<NewsArticle> getArticle(String id) {
    return _client.get<NewsArticle>(
      NewsEndpoints.article(id),
      decodeData: (data) => NewsArticle.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }
}

final newsRepositoryProvider = Provider<NewsRepository>((ref) {
  return NewsRepository(ref.watch(simfApiClientProvider));
});
