import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/net/asset_urls.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/widgets/news_thumbnail.dart';

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
    final category = item.localizedCategory(isArabic: isArabic);
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
                        style: SimfTokens.labelBeigeMediumSm,
                      ),
                      const SizedBox(height: SimfTokens.space1),
                    ],
                    Text(
                      _formatDate(item.publishedAt),
                      // Keep DD-MM-YYYY left-to-right so the Arabic/RTL
                      // paragraph
                      // direction does not reorder the date segments.
                      textDirection: TextDirection.ltr,
                      style: SimfTokens.labelGoldSemibold,
                    ),
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      item.localizedTitle(isArabic: isArabic),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: SimfTokens.labelWhiteBoldLg,
                    ),
                  ],
                ),
              ),
            ),
            // Inline-end (left in RTL): the thumbnail + overlaid category chip.
            NewsThumbnail(
              imageUrl: AssetUrls.image(baseUrl, AssetKind.newsImage, item.id),
              category: category,
            ),
          ],
        ),
      ),
    );
  }

  /// Frame date format `DD-MM-YYYY` with Western digits (the frame shows
  /// Western
  /// digits even in the Arabic UI). Formatted from the stored value so it is
  /// timezone-stable.
  static String _formatDate(DateTime publishedAt) {
    final dd = publishedAt.day.toString().padLeft(2, '0');
    final mm = publishedAt.month.toString().padLeft(2, '0');
    return '$dd-$mm-${publishedAt.year}';
  }
}
