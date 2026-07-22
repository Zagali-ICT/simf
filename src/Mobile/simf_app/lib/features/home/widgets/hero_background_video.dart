import 'dart:async';

import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';
import 'package:youtube_player_iframe/youtube_player_iframe.dart';

import '../../../app/theme/tokens.dart';
import '../../live/youtube_url.dart';

/// The hero-section background video (D-756): plays the CP-configured
/// `OrganizationProfile.backgroundVideoUrl` behind the home hero — muted,
/// looping, no controls, non-interactive (taps fall through to the hero's own
/// InkWell). A YouTube link uses the IFrame player (reusing the D-349 live-feed
/// stack); a direct MP4/HLS link uses `video_player`. The frame is cover-fitted
/// (cropped, never letterboxed) to fill the hero band over the navy fill, so
/// nothing shows through while it warms up. On any failure it renders the navy
/// fill only, and the hero's own image / discover-photo fallback stays behind it.
class HeroBackgroundVideo extends StatefulWidget {
  const HeroBackgroundVideo({required this.url, super.key});

  final String url;

  /// Whether [url] is a background-video source this widget can play — a YouTube
  /// link with a valid id or a direct MP4/HLS stream. The hero only mounts the
  /// widget when this is true (otherwise it keeps the banner-image fallback).
  static bool isSupported(String? url) => YoutubeUrl.isAllowedLiveUrl(url);

  @override
  State<HeroBackgroundVideo> createState() => _HeroBackgroundVideoState();
}

class _HeroBackgroundVideoState extends State<HeroBackgroundVideo> {
  YoutubePlayerController? _youtube;
  StreamSubscription<YoutubePlayerValue>? _youtubeSub;
  VideoPlayerController? _video;
  bool _videoReady = false;

  @override
  void initState() {
    super.initState();
    final id = YoutubeUrl.tryParseId(widget.url);
    if (id != null) {
      _bindYoutube(id);
    } else {
      unawaited(_bindVideo(widget.url));
    }
  }

  void _bindYoutube(String videoId) {
    try {
      final controller = YoutubePlayerController.fromVideoId(
        videoId: videoId,
        autoPlay: true,
        params: const YoutubePlayerParams(
          mute: true,
          loop: true,
          showControls: false,
          showFullscreenButton: false,
          showVideoAnnotations: false,
          enableCaption: false,
          enableKeyboard: false,
          // A passive background — let taps fall through to the hero's own
          // InkWell instead of being captured by the IFrame WebView.
          pointerEvents: PointerEvents.none,
        ),
      );
      // The embed `loop=1` only loops a *playlist*, so a single video is
      // restarted by hand when it ends to keep the background continuous.
      _youtubeSub = controller.listen((value) {
        if (value.playerState == PlayerState.ended) {
          unawaited(controller.seekTo(seconds: 0, allowSeekAhead: true));
          unawaited(controller.playVideo());
        }
      });
      _youtube = controller;
    } catch (_) {
      // A failure building the IFrame controller degrades to the navy fill (the
      // hero keeps its image fallback) rather than crashing the home screen.
      _youtube = null;
    }
  }

  Future<void> _bindVideo(String url) async {
    try {
      final controller = VideoPlayerController.networkUrl(Uri.parse(url));
      _video = controller;
      await controller.initialize();
      await controller.setVolume(0);
      await controller.setLooping(true);
      if (!mounted) {
        return;
      }
      setState(() => _videoReady = true);
      unawaited(controller.play());
    } catch (_) {
      // A malformed URL, an unreachable stream, or a codec error degrades to the
      // navy fill rather than spinning forever or crashing home.
      _video = null;
      if (mounted) {
        setState(() => _videoReady = false);
      }
    }
  }

  @override
  void dispose() {
    unawaited(_youtubeSub?.cancel());
    _youtube?.close();
    _video?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final youtube = _youtube;
    if (youtube != null) {
      return _cover(
        aspectRatio: SimfTokens.videoAspectRatio,
        child: YoutubePlayer(
          controller: youtube,
          aspectRatio: SimfTokens.videoAspectRatio,
        ),
      );
    }
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

  /// Cover-fit [child] (a fixed-[aspectRatio] video surface) into the hero band:
  /// scale to fill both axes and crop the overflow, centred — never letterboxed.
  /// Uses an [OverflowBox] (not a [FittedBox] transform) so the platform WebView
  /// is sized natively rather than scaled under a transform (which mis-renders on
  /// Android hybrid composition).
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
