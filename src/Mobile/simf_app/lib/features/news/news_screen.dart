import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import 'data/news_models.dart';
import 'news_article_screen.dart';

/// `GET /app/news` → the latest news (public, D-199).
final newsListProvider =
    FutureProvider.autoDispose<List<NewsListItem>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<NewsListItem>>(
    '/app/news',
    decodeData: NewsListItem.listFromData,
  );
});

/// Page 029 — الأخبار · News (#29, `/news`, Guest+).
///
/// **Public.** Lists the news (category chip · title · excerpt); tapping an item
/// pushes the article screen (`GET /app/news/{id}`).
class NewsScreen extends ConsumerWidget {
  const NewsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final news = ref.watch(newsListProvider);
    return Scaffold(
      appBar: AppBar(leading: const SimfBackButton(), title: Text(l10n.newsTitle)),
      // News left the bottom nav in the KSA Wave-2 shell (the Profile tab
      // took its slot) — the bar stays, with no destination highlighted.
      bottomNavigationBar: const SimfBottomNav(current: null),
      body: SafeArea(
        top: false,
        child: news.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => _Error(
            message: l10n.newsError,
            onRetry: () => ref.invalidate(newsListProvider),
          ),
          data: (items) {
            if (items.isEmpty) {
              return _Empty(message: l10n.newsEmpty);
            }
            final isArabic = l10n.isArabic;
            return ListView.separated(
              padding: const EdgeInsets.all(SimfTokens.space4),
              itemCount: items.length,
              separatorBuilder: (_, __) =>
                  const SizedBox(height: SimfTokens.space3),
              itemBuilder: (context, index) {
                final item = items[index];
                final excerpt = item.localizedExcerpt(isArabic);
                return Card(
                  margin: EdgeInsets.zero,
                  clipBehavior: Clip.antiAlias,
                  child: InkWell(
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => NewsArticleScreen(newsId: item.id),
                      ),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(SimfTokens.space4),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          _CategoryChip(label: item.localizedCategory(isArabic)),
                          const SizedBox(height: SimfTokens.space2),
                          Text(
                            item.localizedTitle(isArabic),
                            style: const TextStyle(
                              fontWeight: FontWeight.w700,
                              fontSize: SimfTokens.textMd,
                            ),
                          ),
                          if (excerpt != null) ...<Widget>[
                            const SizedBox(height: SimfTokens.space1),
                            Text(
                              excerpt,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(color: SimfTokens.inkMuted),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
                );
              },
            );
          },
        ),
      ),
    );
  }
}

class _CategoryChip extends StatelessWidget {
  const _CategoryChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    if (label.trim().isEmpty) {
      return const SizedBox.shrink();
    }
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.field,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        style: const TextStyle(
          fontSize: SimfTokens.textXs,
          fontWeight: FontWeight.w700,
          color: SimfTokens.accent,
        ),
      ),
    );
  }
}

class _Empty extends StatelessWidget {
  const _Empty({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(Icons.article_outlined, size: 56, color: SimfTokens.inkMuted),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.inkMuted)),
        ],
      ),
    );
  }
}

class _Error extends StatelessWidget {
  const _Error({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
