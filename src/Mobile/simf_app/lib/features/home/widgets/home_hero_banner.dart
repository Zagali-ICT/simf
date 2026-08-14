import 'dart:async';

import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/motion/motion_durations.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/banners/data/banner_models.dart';
import 'package:simf_app/features/home/widgets/carousel_dots.dart';
import 'package:simf_app/features/home/widgets/hero_background_video.dart';
import 'package:simf_app/features/home/widgets/hero_image.dart';
import 'package:simf_app/features/home/widgets/hero_overlay.dart';

/// The home hero (replaces the static discover banner, #43): the forum edition
/// — name (gold), theme, date range and location — overlaid on a rotating strip
/// of CP-managed banner images (`GET /app/banners`, each served by row id at
/// `/app/assets/Banner/{id}/image`). Auto-advances every 4 s when there is more
/// than one banner, with position dots.
///
/// Falls back to the bundled discover photo + the "اكتشف السعودية" copy when no
/// banners are configured AND no edition is loaded, so a fresh install / empty
/// config renders exactly as the old static hero (zero regression). Tapping the
/// hero runs [onTap] (the home opens News).
class HomeHeroBanner extends StatefulWidget {
  const HomeHeroBanner({
    required this.l10n,
    required this.profile,
    required this.banners,
    required this.baseUrl,
    required this.onTap,
    super.key,
  });

  final AppL10n l10n;
  final OrgProfile? profile;
  final List<PublicBannerItem> banners;
  final String baseUrl;
  final VoidCallback onTap;

  @override
  State<HomeHeroBanner> createState() => _HomeHeroBannerState();
}

class _HomeHeroBannerState extends State<HomeHeroBanner> {
  static const double _height = SimfTokens.heroBannerHeight;
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

  @override
  void didUpdateWidget(HomeHeroBanner oldWidget) {
    super.didUpdateWidget(oldWidget);
    // The banner list is delivered asynchronously — the first frame is empty
    // and the list arrives on a later rebuild (the State is reused, so
    // initState's timer set-up already ran against an empty list). Re-evaluate
    // the auto-advance whenever the count changes: clamp the index into the new
    // range and (re)start or stop the timer as the count crosses the > 1 line.
    if (widget.banners.length != oldWidget.banners.length) {
      _timer?.cancel();
      _timer = null;
      if (_index >= widget.banners.length) {
        _index = 0;
      }
      _startAutoAdvance();
    }
  }

  // Auto-advance to the next banner every [_interval], wrapping at the end.
  // Only runs when there is more than one banner (mirrors HighlightsCarousel).
  void _startAutoAdvance() {
    if (widget.banners.length <= 1) {
      return;
    }
    _timer = Timer.periodic(_interval, (_) {
      if (!mounted || !_controller.hasClients) {
        return;
      }
      final next = (_index + 1) % widget.banners.length;
      unawaited(_controller.animateToPage(
          next,
          duration: MotionDurations.carouselSlide,
          curve: Curves.easeInOut,
        ));
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
    final banners = widget.banners;
    // A CP-configured background video (D-756 / D-761) is the base layer when a
    // playable (direct MP4/HLS) URL is set, taking precedence over the banner
    // image strip; the edition text overlay + scrim stay on top. A YouTube URL
    // is not played in-app (an Android WebView can't be clipped into the band —
    // see D-761) and falls through to the image carousel, which also shows when
    // no video is set.
    final videoUrl = widget.profile?.backgroundVideoUrl;
    final hasVideo = HeroBackgroundVideo.isSupported(videoUrl);
    return SizedBox(
      height: _height,
      width: double.infinity,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            if (hasVideo)
              HeroBackgroundVideo(url: videoUrl!)
            else if (banners.isEmpty)
              Image.asset(AppAssets.discoverHero, fit: BoxFit.fill)
            else
              PageView.builder(
                controller: _controller,
                onPageChanged: (i) => setState(() => _index = i),
                itemCount: banners.length,
                itemBuilder: (context, i) => HeroImage(
                  url: banners[i].assetImageUrl(widget.baseUrl),
                  fallbackUrl: banners[i].imageUrl,
                ),
              ),
            const ColoredBox(color: SimfTokens.scrimBlack50),
            Material(
              color: SimfTokens.transparent,
              child: InkWell(
                onTap: widget.onTap,
                child: Padding(
                  padding: const EdgeInsets.all(SimfTokens.space2),
                  child: HeroOverlay(
                    l10n: widget.l10n,
                    profile: widget.profile,
                  ),
                ),
              ),
            ),
            if (!hasVideo && banners.length > 1)
              Positioned(
                bottom: SimfTokens.space2,
                left: 0,
                right: 0,
                child: CarouselDots(count: banners.length, index: _index),
              ),
          ],
        ),
      ),
    );
  }
}
