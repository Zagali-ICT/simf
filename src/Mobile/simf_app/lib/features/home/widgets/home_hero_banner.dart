import 'dart:async';

import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';

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
/// The home hero playing the onboard_01 video (#43): the forum edition — name
/// (gold), theme, date range and location — overlaid on a looping muted video
/// background. Replaced the rotating CP-managed banner strip (D-373).
///
/// Falls back to the bundled discover photo when the decoder is unavailable
/// (tests / unsupported runtime). Tapping the hero runs [onTap] (the home opens
/// News).
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

  VideoPlayerController? _video;
  bool _videoReady = false;

  @override
  void initState() {
    super.initState();
    unawaited(_initVideo());
  }

  /// Best-effort background video — a missing decoder silently falls back to
  /// the bundled discover photo.
  Future<void> _initVideo() async {
    final controller = VideoPlayerController.asset(AppAssets.onboardVideo1);
    try {
      await controller.initialize();
      await controller.setLooping(true);
      await controller.setVolume(0);
      controller.addListener(() {
        if (controller.value.isCompleted) {
          unawaited(controller.seekTo(Duration.zero));
          unawaited(controller.play());
        }
      });
      await controller.play();
      if (!mounted) {
        await controller.dispose();
        return;
      }
      setState(() {
        _video = controller;
        _videoReady = true;
      });
    } catch (_) {
      await controller.dispose();
    }
  }

  @override
  void dispose() {
    unawaited(_video?.dispose());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final banners = widget.banners;
    // A CP-configured background video (D-756 / D-761) is the base layer when a
    // playable (direct MP4/HLS) URL is set, taking precedence over the banner
    // image strip; the edition text overlay + scrim stay on top. A YouTube URL is
    // not played in-app (an Android WebView can't be clipped into the band — see
    // D-761) and falls through to the image carousel, which also shows when no
    // video is set.
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
            if (_videoReady && _video != null)
              FittedBox(
                fit: BoxFit.cover,
                clipBehavior: Clip.hardEdge,
                child: SizedBox(
                  width: _video!.value.size.width,
                  height: _video!.value.size.height,
                  child: VideoPlayer(_video!),
                ),
              )
            else
              Image.asset(AppAssets.discoverHero, fit: BoxFit.fill),
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
