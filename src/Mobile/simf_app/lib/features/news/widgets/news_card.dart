import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../data/news_models.dart';

/// One news row — frame node 957:2197: a borderless navy (radius-8) card laid
/// out horizontally. In RTL the text block sits at the inline-start (right) —
/// the muted category label, the gold date, then the bold white title — and the
/// thumbnail at the inline-end (left); both mirror under LTR. The thumbnail
/// carries the article's `NewsImage` asset with a gold category chip overlaid
/// and a navy bottom-gradient. Tapping opens the article screen.
class NewsCard extends StatelessWidget {
  const NewsCard({
    required this.item,
    required this.isArabic,
    required this.baseUrl,
    required this.onTap,
    super.key,
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
                  colors: <Color>[Colors.transparent, SimfTokens.bannerScrim],
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
