import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/net/asset_urls.dart';
import 'package:simf_app/features/home/widgets/carousel_auto_advance.dart';
import 'package:simf_app/features/home/widgets/carousel_dots.dart';
import 'package:simf_app/features/home/widgets/highlight_slide.dart';
import 'package:simf_app/features/news/data/news_models.dart';

/// ابرز الاحداث — the highlights carousel (frame node 758:1239): an
/// auto-advancing, swipeable PageView of image+title slides drawn from the most
/// recent news items (CP-managed via /admin/news). A row of dots tracks the
/// position. Owner spec (2026-06-28): "multiple slides, image and text only,
/// animated, entered via the Control Panel" — the old single image becomes a
/// gallery; news already backs it, so no new table or API is needed.
class HighlightsCarousel extends StatefulWidget {
  const HighlightsCarousel({
    required this.l10n,
    required this.items,
    required this.baseUrl,
    required this.onTap,
    super.key,
  });

  final AppL10n l10n;
  final List<NewsListItem> items;
  final String baseUrl;
  final void Function(NewsListItem) onTap;

  @override
  State<HighlightsCarousel> createState() => _HighlightsCarouselState();
}

class _HighlightsCarouselState extends State<HighlightsCarousel>
    with CarouselAutoAdvance<HighlightsCarousel> {
  static const double _slideHeight = SimfTokens.highlightSlideHeight;

  @override
  int get carouselItemCount => widget.items.length;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        SizedBox(
          height: _slideHeight,
          child: PageView.builder(
            controller: carouselController,
            onPageChanged: onCarouselPageChanged,
            itemCount: widget.items.length,
            itemBuilder: (context, i) {
              final post = widget.items[i];
              return HighlightSlide(
                title: post.localizedTitle(isArabic: widget.l10n.isArabic),
                imageUrl: AssetUrls.image(
                  widget.baseUrl,
                  AssetKind.newsImage,
                  post.id,
                ),
                onTap: () => widget.onTap(post),
              );
            },
          ),
        ),
        if (widget.items.length > 1) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          CarouselDots(count: widget.items.length, index: carouselIndex),
        ],
      ],
    );
  }
}
