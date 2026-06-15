import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
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

/// Page 029 — الأخبار · News (#29, `/news`, Guest+), rebuilt to the KSA
/// Wave-2 frame **958:2246 "Media coverage"** on the shared navy shell.
///
/// **Public.** The frame is the three-tab "التغطية الإعلامية" container —
/// الأخبار (this screen) · الشركاء الإعلاميون · معرض الصور والفيديوهات. The
/// News tab is active (gold pill); the two inactive pills navigate to the
/// existing media-partners (#31) and gallery (#30) routes. The body is the
/// news list — category chip · title · excerpt on the navy KSA card — and
/// tapping a row pushes the article screen (`GET /app/news/{id}`). The data
/// contract (`newsListProvider` + tap → [NewsArticleScreen]) is unchanged.
class NewsScreen extends ConsumerWidget {
  const NewsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final news = ref.watch(newsListProvider);
    return KsaPage(
      // Frame header — the container is "التغطية الإعلامية" (Media coverage),
      // not the bare "الأخبار" tab label.
      title: l10n.mediaCoverageTitle,
      onBack: () => ksaBackOrHome(context),
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
            child: _MediaTabs(l10n: l10n),
          ),
          Expanded(
            child: news.when(
              loading: () =>
                  const Center(child: CircularProgressIndicator()),
              error: (_, __) => KsaErrorState(
                message: l10n.newsError,
                retryLabel: l10n.retryLabel,
                onRetry: () => ref.invalidate(newsListProvider),
              ),
              data: (items) {
                if (items.isEmpty) {
                  return KsaEmptyState(
                    icon: Icons.article_outlined,
                    message: l10n.newsEmpty,
                  );
                }
                final isArabic = l10n.isArabic;
                return ListView.separated(
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
                    return _NewsCard(
                      item: item,
                      isArabic: isArabic,
                      onTap: () => Navigator.of(context).push(
                        MaterialPageRoute<void>(
                          builder: (_) => NewsArticleScreen(newsId: item.id),
                        ),
                      ),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

/// The three media-coverage tabs (frame node 958:2256): الأخبار (active gold) ·
/// الشركاء الإعلاميون · معرض الصور والفيديوهات. The active tab is solid gold;
/// the inactive pills are bordered navy cards that route to their own screens.
class _MediaTabs extends StatelessWidget {
  const _MediaTabs({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: _MediaTab(label: l10n.newsTitle, active: true),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _MediaTab(
            label: l10n.mediaPartnersTitle,
            active: false,
            onTap: () => context.pushReplacementNamed(RouteNames.mediaPartners),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _MediaTab(
            label: l10n.galleryTitle,
            active: false,
            onTap: () => context.pushReplacementNamed(RouteNames.gallery),
          ),
        ),
      ],
    );
  }
}

/// One media-coverage tab pill (frame nodes 958:2257 / 958:2259 / 958:2261):
/// the active pill is solid gold with white text; an inactive pill is a navy
/// card with a beige hairline and beige text.
class _MediaTab extends StatelessWidget {
  const _MediaTab({required this.label, required this.active, this.onTap});

  final String label;
  final bool active;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      onTap: active ? null : onTap,
      color: active ? SimfTokens.accent : SimfTokens.navySurface,
      borderColor: active ? SimfTokens.accent : SimfTokens.beigeBorder,
      child: SizedBox(
        height: 48,
        child: Center(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
            child: Text(
              label,
              textAlign: TextAlign.center,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
                color: active ? Colors.white : SimfTokens.beigeBorder,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// One news row on the KSA navy card: a gold category chip, the bold white
/// title, then the muted beige excerpt. Tapping opens the article screen.
class _NewsCard extends StatelessWidget {
  const _NewsCard({
    required this.item,
    required this.isArabic,
    required this.onTap,
  });

  final NewsListItem item;
  final bool isArabic;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final excerpt = item.localizedExcerpt(isArabic);
    return KsaCard(
      onTap: onTap,
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
                color: Colors.white,
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
                style: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  height: 1.5,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// The gold category chip — a navy box with gold text, matching the frame's
/// gold-on-navy accents.
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
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.accent,
          width: SimfTokens.hairlineBold,
        ),
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
