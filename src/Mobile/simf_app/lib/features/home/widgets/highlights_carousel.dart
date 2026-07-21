import 'dart:async';

import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../news/data/news_models.dart';
import 'carousel_dots.dart';

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

class _HighlightsCarouselState extends State<HighlightsCarousel> {
  static const double _slideHeight = 170;
  static const Duration _interval = Duration(seconds: 4);

  late final PageController _controller;
  Timer? _timer;
  int _index = 0;

  @override
  void initState() {
    super.initState();
    _controller = PageController();
    _startAutoAdvance();
  }

  // Auto-advance to the next slide every [_interval], wrapping at the end. Only
  // runs when there is more than one slide.
  void _startAutoAdvance() {
    if (widget.items.length <= 1) {
      return;
    }
    _timer = Timer.periodic(_interval, (_) {
      if (!mounted || !_controller.hasClients) {
        return;
      }
      final next = (_index + 1) % widget.items.length;
      _controller.animateToPage(
        next,
        duration: const Duration(milliseconds: 450),
        curve: Curves.easeInOut,
      );
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        SizedBox(
          height: _slideHeight,
          child: PageView.builder(
            controller: _controller,
            onPageChanged: (i) => setState(() => _index = i),
            itemCount: widget.items.length,
            itemBuilder: (context, i) {
              final post = widget.items[i];
              return _HighlightSlide(
                title: post.localizedTitle(widget.l10n.isArabic),
                imageUrl:
                    '${widget.baseUrl}/app/assets/NewsImage/${post.id}/image',
                onTap: () => widget.onTap(post),
              );
            },
          ),
        ),
        if (widget.items.length > 1) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          CarouselDots(count: widget.items.length, index: _index),
        ],
      ],
    );
  }
}

/// One carousel slide (758:1239): the news image filling a rounded card with a
/// bottom scrim and the title overlaid — image + text only. Tapping opens the
/// article. The image rides the D-357 anonymous `NewsImage` route; a spinner
/// shows while it loads and a navy image-glyph box is the no-image fall-back.
class _HighlightSlide extends StatelessWidget {
  const _HighlightSlide({
    required this.title,
    required this.imageUrl,
    required this.onTap,
  });

  final String title;
  final String imageUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      // A small inset so the neighbouring slides peek at the edges.
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          child: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              ColoredBox(
                color: SimfTokens.navy,
                child: Image.network(
                  imageUrl,
                  fit: BoxFit.cover,
                  gaplessPlayback: true,
                  loadingBuilder: (context, child, progress) =>
                      progress == null
                          ? child
                          : const Center(
                              child: SizedBox(
                                width: 18,
                                height: 18,
                                child:
                                    CircularProgressIndicator(strokeWidth: 2),
                              ),
                            ),
                  errorBuilder: (context, error, stackTrace) => const Center(
                    child: Icon(
                      Icons.image_outlined,
                      size: 28,
                      color: SimfTokens.beigeBorder,
                    ),
                  ),
                ),
              ),
              // Bottom scrim so the white title reads over any image.
              const DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.center,
                    end: Alignment.bottomCenter,
                    colors: <Color>[Colors.transparent, Color(0xCC01132D)],
                  ),
                ),
              ),
              Positioned(
                left: SimfTokens.space4,
                right: SimfTokens.space4,
                bottom: SimfTokens.space3,
                child: Text(
                  title,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                    fontSize: SimfTokens.textLg,
                    height: 1.3,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

