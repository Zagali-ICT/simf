import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/data/news_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The full news article (Page_029 detail, `GET /app/news/{id}`). Pushed from the
/// news list via an imperative route (the screen has no go_router entry).
///
/// Route: `RouteNames.newsArticle`.
/// Data: [newsArticleProvider] over [newsRepositoryProvider].
/// Perf: ListView builds every child up front — correct for a short static
///       page, a defect on a data feed.

/// One article, or **null when the server has no such id** (a 404).
///
/// A 404 here is "this article is gone", which the screen answers with its own
/// not-found copy rather than the error surface — so folding it to null puts it
/// on `when`'s DATA branch and leaves `error` for genuine failures. Same shape
/// as `termsBlockProvider`.
final newsArticleProvider =
    FutureProvider.autoDispose.family<NewsArticle?, String>((ref, id) async {
  try {
    return await ref.watch(newsRepositoryProvider).getArticle(id);
  } on ApiFailure catch (failure) {
    if (failure.httpStatus == 404) {
      return null;
    }
    rethrow;
  }
});

class NewsArticleScreen extends ConsumerWidget {
  const NewsArticleScreen({required this.newsId, super.key});

  final String newsId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.newsTitle,
      onBack: () => backOrHome(context),
      body: SimfPullToRefresh(
        onRefresh: () => refreshAsync(ref, newsArticleProvider(newsId).future),
        child: _buildBody(ref, l10n),
      ),
    );
  }

  Widget _buildBody(WidgetRef ref, AppL10n l10n) {
    return ref.watch(newsArticleProvider(newsId)).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => SimfPullableHost(
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space6),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(l10n.newsError, textAlign: TextAlign.center),
                    const SizedBox(height: SimfTokens.space4),
                    FilledButton(
                      onPressed: () =>
                          ref.invalidate(newsArticleProvider(newsId)),
                      child: Text(l10n.retryLabel),
                    ),
                  ],
                ),
              ),
            ),
          ),
          // Null is the not-found state (see [newsArticleProvider]).
          data: (article) => article == null
              ? SimfPullableHost(
                  child: Center(
                    child: Text(
                      l10n.newsNotFound,
                      style: SimfTokens.bodyInkMuted,
                    ),
                  ),
                )
              : _buildArticle(l10n, article),
        );
  }

  Widget _buildArticle(AppL10n l10n, NewsArticle article) {
    final isArabic = l10n.isArabic;
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        Text(
          article.localizedCategory(isArabic: isArabic),
          style: SimfTokens.labelGoldBoldXs,
        ),
        const SizedBox(height: SimfTokens.space2),
        Text(
          article.localizedTitle(isArabic: isArabic),
          style: SimfTokens.titleBoldXl,
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(article.localizedBody(isArabic: isArabic)),
      ],
    );
  }
}
