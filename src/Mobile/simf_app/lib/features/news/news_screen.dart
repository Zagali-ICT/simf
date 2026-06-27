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

/// Page 029 — الأخبار · News (#29, `/news`, Guest+), rebuilt to the KSA-Project
/// frame **1049:12629 "Media coverage"** on the shared navy shell.
///
/// **Public.** The frame is the two-tab "المركز الاعلامي" (Media center)
/// container — احدث المستجدات (this screen, active gold) · الشركاء الإعلاميون.
/// The inactive pill navigates to the media-partners (#31) route. (Figma
/// 947/1049 dropped the معرض الصور tab; the gallery screen #30 stays in the
/// app, reached elsewhere.) The body is the news list — each row the horizontal
/// frame-1049:12736 card (thumbnail + gold date + title; no excerpt) — and
/// tapping a row pushes the article screen (`GET /app/news/{id}`). The data
/// contract (`newsListProvider` + tap → [NewsArticleScreen]) is unchanged.
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
              // Pull-to-refresh also works in the error/empty states so the
              // user can pull to retry; both centred states are hosted in a
              // viewport-filling scroll view so the gesture fires on short
              // content. onRefresh invalidates [newsListProvider] and awaits
              // the re-fetch.
              error: (_, __) => KsaRefresh(
                onRefresh: () => _refresh(ref),
                child: _RefreshableCentered(
                  child: KsaErrorState(
                    message: l10n.newsError,
                    retryLabel: l10n.retryLabel,
                    onRetry: () => ref.invalidate(newsListProvider),
                  ),
                ),
              ),
              data: (items) {
                if (items.isEmpty) {
                  return KsaRefresh(
                    onRefresh: () => _refresh(ref),
                    child: _RefreshableCentered(
                      child: KsaEmptyState(
                        icon: Icons.article_outlined,
                        message: l10n.newsEmpty,
                      ),
                    ),
                  );
                }
                final isArabic = l10n.isArabic;
                return KsaRefresh(
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
                      return _NewsCard(
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

/// Hosts a centred state (error / empty) inside a viewport-filling scroll view
/// so [KsaRefresh] can drive a pull-to-refresh even when the content is short.
/// The [AlwaysScrollableScrollPhysics] makes the pull gesture fire regardless
/// of content height; the min-height constraint keeps the child vertically
/// centred in the available space.
class _RefreshableCentered extends StatelessWidget {
  const _RefreshableCentered({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        return SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: constraints.maxHeight),
            child: child,
          ),
        );
      },
    );
  }
}

/// The two media-center tabs (Figma 1049:12629): الشركاء الإعلاميون (inactive,
/// routes to the partners screen) · احدث المستجدات (active gold, this screen).
/// Shared, identical to `media_partners_screen.dart:_MediaTabs` (the Gallery tab
/// was dropped from the strip — Figma 947/1049 show two tabs only).
class _MediaTabs extends StatelessWidget {
  const _MediaTabs({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    // Figma 1049:12629 (Arabic/RTL): right→left الشركاء الإعلاميون · احدث المستجدات.
    // A Row lays children start→end, so under RTL the first child is the
    // right-most: partners (inactive) then latest-updates (active here).
    return Row(
      children: <Widget>[
        Expanded(
          child: _MediaTab(
            label: l10n.mediaPartnersTitle,
            active: false,
            onTap: () => context.pushReplacementNamed(RouteNames.mediaPartners),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _MediaTab(label: l10n.latestUpdatesTitle, active: true),
        ),
      ],
    );
  }
}

/// One media-center tab pill (frame node 1049:12640 / 1049:12642): a 48-high,
/// 8px-radius pill — active is solid gold with **white** text; inactive is a
/// transparent pill with a beige 0.2 hairline and beige text.
class _MediaTab extends StatelessWidget {
  const _MediaTab({required this.label, required this.active, this.onTap});

  final String label;
  final bool active;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: active ? SimfTokens.accent : Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: active
            ? BorderSide.none
            : const BorderSide(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
      ),
      child: InkWell(
        onTap: active ? null : onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: SizedBox(
          height: 48,
          child: Center(
            child: Padding(
              padding:
                  const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
              child: Text(
                label,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                  // Figma 1049 — the active gold pill carries white text.
                  color: active ? Colors.white : SimfTokens.beigeBorder,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// One news row — frame node 957:2197: a borderless navy (radius-8) card laid
/// out horizontally. In RTL the text block sits at the inline-start (right) —
/// the muted category label, the gold date, then the bold white title — and the
/// thumbnail at the inline-end (left); both mirror under LTR. The thumbnail
/// carries the article's `NewsImage` asset with a gold category chip overlaid
/// and a navy bottom-gradient. Tapping opens the article screen.
class _NewsCard extends StatelessWidget {
  const _NewsCard({
    required this.item,
    required this.isArabic,
    required this.baseUrl,
    required this.onTap,
  });

  final NewsListItem item;
  final bool isArabic;
  final String baseUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final category = item.localizedCategory(isArabic);
    return Material(
      color: SimfTokens.navyDeep,
      borderRadius: const BorderRadius.all(Radius.circular(SimfTokens.radius)),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        // The fixed-height thumbnail pins the card to the frame's proportions,
        // so a short title can't collapse it.
        child: Row(
          children: <Widget>[
            // Inline-start (right in RTL): the text block.
            Expanded(
              child: Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: SimfTokens.space4,
                  vertical: SimfTokens.space2,
                ),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    if (category.isNotEmpty) ...<Widget>[
                      Text(
                        category,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: SimfTokens.beigeBorder,
                          fontSize: SimfTokens.textSm,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space1),
                    ],
                    Text(
                      _formatDate(item.publishedAt),
                      // Keep DD-MM-YYYY left-to-right so the Arabic/RTL paragraph
                      // direction does not reorder the date segments.
                      textDirection: TextDirection.ltr,
                      style: const TextStyle(
                        color: SimfTokens.accent,
                        fontSize: SimfTokens.textMd,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      item.localizedTitle(isArabic),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: SimfTokens.textLg,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            // Inline-end (left in RTL): the thumbnail + overlaid category chip.
            _NewsThumbnail(
              imageUrl: '$baseUrl/app/assets/NewsImage/${item.id}/image',
              category: category,
            ),
          ],
        ),
      ),
    );
  }

  /// Frame date format `DD-MM-YYYY` with Western digits (the frame shows Western
  /// digits even in the Arabic UI). Formatted from the UTC value so it is
  /// timezone-stable.
  static String _formatDate(DateTime publishedAt) {
    final dd = publishedAt.day.toString().padLeft(2, '0');
    final mm = publishedAt.month.toString().padLeft(2, '0');
    return '$dd-$mm-${publishedAt.year}';
  }
}

/// The news thumbnail (frame node 958:2202): the article's `NewsImage` asset
/// (fetched from the public anonymous D-357 route) under a navy bottom-gradient,
/// with the gold category chip overlaid at the inline-start top corner. A
/// spinner shows while it loads; a navy article-icon box is the no-image / fetch
/// -failure fall-back. 155 wide, stretched to the card height.
class _NewsThumbnail extends StatelessWidget {
  const _NewsThumbnail({required this.imageUrl, required this.category});

  final String imageUrl;
  final String category;

  // Frame 958:2202 — the thumbnail is a fixed 155×85 tile.
  static const double _width = 155;
  static const double _height = 85;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: _width,
      height: _height,
      child: ClipRRect(
        borderRadius:
            const BorderRadius.all(Radius.circular(SimfTokens.radius)),
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            Image.network(
              imageUrl,
              fit: BoxFit.cover,
              gaplessPlayback: true,
              loadingBuilder: (context, child, progress) {
                if (progress == null) {
                  return child;
                }
                return const ColoredBox(
                  color: SimfTokens.navy,
                  child: Center(
                    child: SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  ),
                );
              },
              errorBuilder: (context, error, stackTrace) =>
                  const _NewsImageFallback(),
            ),
            // The frame's bottom gradient (transparent → navy `#001030` @ 80%).
            const DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.center,
                  end: Alignment.bottomCenter,
                  colors: <Color>[Colors.transparent, _gradientNavy],
                ),
              ),
            ),
            if (category.isNotEmpty)
              PositionedDirectional(
                top: SimfTokens.space2,
                start: SimfTokens.space2,
                child: _CategoryChip(label: category),
              ),
          ],
        ),
      ),
    );
  }
}

/// Frame gradient end colour: navy `#001030` at 80% (rgba(0,16,48,0.8)).
const Color _gradientNavy = Color(0xCC001030);

/// The no-image / failed-fetch fall-back: a navy box with the article glyph.
class _NewsImageFallback extends StatelessWidget {
  const _NewsImageFallback();

  @override
  Widget build(BuildContext context) => const ColoredBox(
        color: SimfTokens.navy,
        child: Center(
          child: Icon(
            Icons.article_outlined,
            size: 28,
            color: SimfTokens.accent,
          ),
        ),
      );
}

/// The gold category chip overlaid on the thumbnail (frame node 958:2203): a
/// solid-gold rounded pill with white bold text.
class _CategoryChip extends StatelessWidget {
  const _CategoryChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      // Frame 958:2203 — 10px horizontal padding (no matching spacing token).
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: SimfTokens.space1,
      ),
      decoration: const BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textXs,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}
