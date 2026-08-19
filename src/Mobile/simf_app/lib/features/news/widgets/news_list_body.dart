import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/news/data/news_repository.dart';
import 'package:simf_app/features/news/widgets/news_card.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The news list under the media-coverage tabs — each row the horizontal
/// frame-1049:12736 card (thumbnail + gold date + title; no excerpt), tapping
/// one pushes the article screen (`GET /app/news/{id}`).
class NewsListBody extends ConsumerWidget {
  const NewsListBody({super.key});

  Future<void> _refresh(WidgetRef ref) =>
      refreshAsync(ref, newsListProvider.future);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final news = ref.watch(newsListProvider);
    // The card builds `{base}/app/assets/NewsImage/{id}/image` for the
    // thumbnail; the base already includes `/api/v1`.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    return news.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      // Pull-to-refresh also works in the error/empty states so the user can
      // pull to retry; both centred states are hosted in a viewport-filling
      // scroll view so the gesture fires on short content.
      error: (_, __) => SimfRefreshableMessage(
        onRefresh: () => _refresh(ref),
        child: SimfErrorState(
          message: l10n.newsError,
          retryLabel: l10n.retryLabel,
          onRetry: () => ref.invalidate(newsListProvider),
        ),
      ),
      data: (items) {
        if (items.isEmpty) {
          return SimfRefreshableMessage(
            onRefresh: () => _refresh(ref),
            child: SimfEmptyState(
              icon: Icons.article_outlined,
              message: l10n.newsEmpty,
            ),
          );
        }
        final isArabic = l10n.isArabic;
        return SimfPullToRefresh(
          onRefresh: () => _refresh(ref),
          child: ListView.separated(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space2,
              SimfTokens.space4,
              SimfTokens.space6,
            ),
            itemCount: items.length,
            separatorBuilder: (_, __) =>
                const SizedBox(height: SimfTokens.space4),
            itemBuilder: (context, index) {
              final item = items[index];
              return NewsCard(
                item: item,
                isArabic: isArabic,
                baseUrl: baseUrl,
                onTap: () => context.pushNamed(
                  RouteNames.newsArticle,
                  pathParameters: <String, String>{
                    RouteParams.newsId: item.id,
                  },
                ),
              );
            },
          ),
        );
      },
    );
  }
}
