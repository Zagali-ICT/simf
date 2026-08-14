import 'dart:async';

import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/live/youtube_url.dart';
import 'package:video_player/video_player.dart';

/// The hero-section background video (D-756 / D-761): plays the CP-configured
/// `OrganizationProfile.backgroundVideoUrl` behind the home hero — muted,
/// looping, no controls, non-interactive (taps fall through to the hero's own
/// InkWell). Only a **direct MP4/HLS** link is played, through `video_player`
/// (a Flutter Texture that clips into the hero band correctly).
///
/// A YouTube link is deliberately NOT played here (D-761): a YouTube embed is
/// an Android platform WebView, and the WebView does not honour the Flutter
/// clip around the cover-crop — it paints its full, over-sized surface over the
/// whole screen instead of the 160px band. So a YouTube URL falls back to the
/// hero's banner-image band (the website can still play the same link, because
/// it crops the iframe in CSS, not on the Flutter side). To show a moving video
/// in the app hero, set `backgroundVideoUrl` to a direct MP4/HLS link.
///
/// The frame is cover-fitted (cropped, never letterboxed) to fill the hero band
/// over the navy fill, so nothing shows through while it warms up. On any
/// failure it renders the navy fill only, and the hero's own image /
/// discover-photo fallback stays behind it.
class HeroBackgroundVideo extends StatefulWidget {
  const HeroBackgroundVideo({required this.url, super.key});

  final String url;

  /// Whether [url] is a background-video source this widget can play — a direct
  /// https MP4/HLS stream. A YouTube link is deliberately excluded (see the
  /// class doc / D-761): it would need an Android WebView, which can't be
  /// clipped into the hero band. The hero only mounts the widget when this is
  /// true (otherwise it keeps the banner-image fallback).
  static bool isSupported(String? url) =>
      YoutubeUrl.isAllowedLiveUrl(url) && YoutubeUrl.tryParseId(url) == null;

  @override
  State<HeroBackgroundVideo> createState() => _HeroBackgroundVideoState();
}

class _HeroBackgroundVideoState extends State<HeroBackgroundVideo> {
  VideoPlayerController? _video;
  bool _videoReady = false;

  @override
  void initState() {
    super.initState();
    unawaited(_bindVideo(widget.url));
  }

  Future<void> _bindVideo(String url) async {
    final controller = VideoPlayerController.networkUrl(Uri.parse(url));
    try {
      await controller.initialize();
      await controller.setVolume(0);
      await controller.setLooping(true);
      if (!mounted) {
        // Disposed mid-initialise — release the native player we just built
        // ([_video] was never assigned, so widget dispose would not catch it).
        await controller.dispose();
        return;
      }
      setState(() {
        _video = controller;
        _videoReady = true;
      });
      unawaited(controller.play());
    } catch (_) {
      // A malformed URL, an unreachable stream, or a codec error degrades to
      // the navy fill rather than spinning forever or crashing home. Dispose
      // the controller we created so a failed mount never leaks a native
      // player.
      await controller.dispose();
      if (mounted) {
        setState(() => _videoReady = false);
      }
    }
  }

  @override
  void dispose() {
    unawaited(_video?.dispose());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final video = _video;
    if (video != null && _videoReady) {
      final ratio = video.value.aspectRatio == 0
          ? SimfTokens.videoAspectRatio
          : video.value.aspectRatio;
      return _cover(aspectRatio: ratio, child: VideoPlayer(video));
    }
    // Warming up / unsupported — the navy fill covers the band until the player
    // renders (or the parent's image fallback shows through on failure).
    return const ColoredBox(color: SimfTokens.navy);
  }

  /// Cover-fit [child] (a fixed-[aspectRatio] video surface) into the hero
  /// band: scale to fill both axes and crop the overflow, centred — never
  /// letterboxed. Uses an [OverflowBox] (not a [FittedBox] transform) so the
  /// video Texture is sized natively rather than scaled under a transform.
  Widget _cover({required double aspectRatio, required Widget child}) {
    return ColoredBox(
      color: SimfTokens.navy,
      child: LayoutBuilder(
        builder: (context, constraints) {
          final w = constraints.maxWidth;
          final h = constraints.maxHeight;
          final boxRatio = h == 0 ? aspectRatio : w / h;
          final coverW = boxRatio > aspectRatio ? w : h * aspectRatio;
          final coverH = boxRatio > aspectRatio ? w / aspectRatio : h;
          return ClipRect(
            child: OverflowBox(
              minWidth: coverW,
              maxWidth: coverW,
              minHeight: coverH,
              maxHeight: coverH,
              child: SizedBox(width: coverW, height: coverH, child: child),
            ),
          );
        },
      ),
    );
  }
}
