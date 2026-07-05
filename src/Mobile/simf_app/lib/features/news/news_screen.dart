import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/media_coverage_tabs.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/news_repository.dart';
import 'news_article_screen.dart';
import 'widgets/news_card.dart';

// `newsListProvider` lives in `data/news_repository.dart`; re-exported so the
// existing `show newsListProvider` imports (the Home highlights carousel + the
// news tests) keep resolving off this screen.
export 'data/news_repository.dart' show newsListProvider;

/// Page 029 — الأخبار · News (#29, `/news`, Guest+), rebuilt to the KSA-Project
/// frame **1049:12629 "Media coverage"** on the shared navy shell.
///
/// **Public.** The frame is the two-tab "المركز الاعلامي" (Media center)
/// container — احدث المستجدات (this screen, active gold) · الشركاء الإعلاميون.
/// The inactive pill navigates to the media-partners (#31) route. The body is
/// the news list — each row the horizontal frame-1049:12736 card (thumbnail +
/// gold date + title; no excerpt) — and tapping a row pushes the article screen
/// (`GET /app/news/{id}`).
class NewsScreen extends ConsumerWidget {
  const NewsScreen({super.key});

  /// Pull-to-refresh handler — re-fetch the news list by invalidating
  /// [newsListProvider] and awaiting its next value so the gold spinner stays
  /// until the new data has loaded.
  Future<void> _refresh(WidgetRef ref) async {
    ref.invalidate(newsListProvider);
    await ref.read(newsListProvider.future);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final news = ref.watch(newsListProvider);
    // The card builds `{base}/app/assets/NewsImage/{id}/image` for the
    // thumbnail; the base already includes `/api/v1`.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    return SimfPageShell(
      // Frame header — the container is "التغطية الإعلامية" (Media coverage),
      // not the bare "الأخبار" tab label.
      title: l10n.mediaCoverageTitle,
      onBack: () => backOrHome(context),
      // News left the bottom nav in the KSA Wave-2 shell (the Profile tab took
      // its slot) — the bar stays, with no destination highlighted.
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space2,
              SimfTokens.space4,
              SimfTokens.space2,
            ),
            child: const MediaCoverageTabs(active: MediaCoverageTab.latestUpdates),
          ),
          Expanded(
            child: news.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              // Pull-to-refresh also works in the error/empty states so the
              // user can pull to retry; both centred states are hosted in a
              // viewport-filling scroll view so the gesture fires on short
              // content. onRefresh invalidates [newsListProvider] and awaits
              // the re-fetch.
              error: (_, __) => SimfPullToRefresh(
                onRefresh: () => _refresh(ref),
                child: SimfPullableHost(
                  child: SimfErrorState(
                    message: l10n.newsError,
                    retryLabel: l10n.retryLabel,
                    onRetry: () => ref.invalidate(newsListProvider),
                  ),
                ),
              ),
              data: (items) {
                if (items.isEmpty) {
                  return SimfPullToRefresh(
                    onRefresh: () => _refresh(ref),
                    child: SimfPullableHost(
                      child: SimfEmptyState(
                        icon: Icons.article_outlined,
                        message: l10n.newsEmpty,
                      ),
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
                        onTap: () => Navigator.of(context).push(
                          MaterialPageRoute<void>(
                            builder: (_) => NewsArticleScreen(newsId: item.id),
                          ),
                        ),
                      );
                    },
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
