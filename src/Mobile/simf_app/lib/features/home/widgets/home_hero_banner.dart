import 'dart:async';

import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/organization_profile/organization_profile.dart';
import '../../banners/data/banner_models.dart';
import 'carousel_dots.dart';
import 'hero_background_video.dart';

/// The home hero (replaces the static discover banner, #43): the forum edition —
/// name (gold), theme, date range and location — overlaid on a rotating strip of
/// CP-managed banner images (`GET /app/banners`, each served by row id at
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
    // The banner list is delivered asynchronously — the first frame is empty and
    // the list arrives on a later rebuild (the State is reused, so initState's
    // timer set-up already ran against an empty list). Re-evaluate the
    // auto-advance whenever the count changes: clamp the index into the new
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

  // Auto-advance to the next banner every [_interval], wrapping at the end. Only
  // runs when there is more than one banner (mirrors HighlightsCarousel).
  void _startAutoAdvance() {
    if (widget.banners.length <= 1) {
      return;
    }
    _timer = Timer.periodic(_interval, (_) {
      if (!mounted || !_controller.hasClients) {
        return;
      }
      final next = (_index + 1) % widget.banners.length;
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
    final banners = widget.banners;
    // A CP-configured background video (D-756) is the base layer when set,
    // taking precedence over the banner image strip; the edition text overlay +
    // scrim stay on top. The image carousel (and its dots) show only when there
    // is no video.
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
                itemBuilder: (context, i) => _HeroImage(
                  url: banners[i].assetImageUrl(widget.baseUrl),
                  fallbackUrl: banners[i].imageUrl,
                ),
              ),
            const ColoredBox(color: Color(0x80000000)),
            Material(
              color: SimfTokens.transparent,
              child: InkWell(
                onTap: widget.onTap,
                child: Padding(
                  padding: const EdgeInsets.all(SimfTokens.space2),
                  child: _HeroOverlay(
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

/// One hero background image: the uploaded banner asset, falling back to the
/// banner's pasted [fallbackUrl], then the bundled discover photo — so the hero
/// always shows something even before an image is uploaded.
class _HeroImage extends StatelessWidget {
  const _HeroImage({required this.url, this.fallbackUrl});

  final String url;
  final String? fallbackUrl;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.navy,
      child: Image.network(
        url,
        fit: BoxFit.fill,
        gaplessPlayback: true,
        errorBuilder: (context, error, stackTrace) {
          final fallback = fallbackUrl;
          if (fallback != null && fallback.isNotEmpty) {
            return Image.network(
              fallback,
              fit: BoxFit.fill,
              errorBuilder: (_, __, ___) => _placeholder,
            );
          }
          return _placeholder;
        },
      ),
    );
  }

  Widget get _placeholder =>
      Image.asset(AppAssets.discoverHero, fit: BoxFit.fill);
}

/// The hero text overlay: the forum edition (name + theme + date range +
/// location) when the profile is loaded, otherwise the original discover copy.
class _HeroOverlay extends StatelessWidget {
  const _HeroOverlay({required this.l10n, required this.profile});

  final AppL10n l10n;
  final OrgProfile? profile;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final p = profile;
    final name = p?.nameFor(isArabic) ?? '';

    // No edition config yet → the original discover copy (zero regression).
    if (p == null || name.isEmpty) {
      return Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.discoverSection,
            style: SimfTokens.labelGoldBoldLg,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.discoverBannerSubtitle,
            style: SimfTokens.labelWhiteMediumSm,
          ),
        ],
      );
    }

    final theme = p.titleFor(isArabic);
    final dates = p.eventDateRange(isArabic);
    final location = p.locationFor(isArabic);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          name,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: SimfTokens.labelGoldBold,
        ),
        if (theme.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space1),
          Text(
            theme,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: SimfTokens.labelWhiteMediumSm,
          ),
        ],
        if (dates != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space1),
          _MetaLine(icon: Icons.event_outlined, text: dates),
        ],
        if (location != null && location.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space1),
          _MetaLine(icon: Icons.place_outlined, text: location),
        ],
      ],
    );
  }
}

/// A small icon + text meta row (date / location) under the hero title.
class _MetaLine extends StatelessWidget {
  const _MetaLine({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Icon(icon, size: 14, color: SimfTokens.surface),
        const SizedBox(width: SimfTokens.space1),
        Flexible(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: SimfTokens.bodyWhiteSm,
          ),
        ),
      ],
    );
  }
}
