import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:video_player/video_player.dart';
import 'package:youtube_player_iframe/youtube_player_iframe.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/session/session_activity.dart';
import '../youtube_url.dart';

/// The device orientations to lock while the live player is (not) fullscreen
/// (D-721): landscape in fullscreen, back to the app-wide portrait lock out of
/// it. A pure function so the portrait-lock exception is unit-testable without
/// the platform channel.
List<DeviceOrientation> liveFullScreenOrientations(bool isFullScreen) {
  return isFullScreen
      ? const <DeviceOrientation>[
          DeviceOrientation.landscapeLeft,
          DeviceOrientation.landscapeRight,
        ]
      : const <DeviceOrientation>[DeviceOrientation.portraitUp];
}

/// The live video surface. Owns its own controller and picks the player by the
/// URL (D-349): a YouTube link → the IFrame player; anything else (HLS/MP4) →
/// `video_player`. The parent rebuilds this with a new `ValueKey(url)` to switch
/// feeds, so this widget only ever binds one URL for its lifetime.
///
/// The YouTube path shows a fullscreen button (D-721): entering fullscreen
/// rotates to landscape and exiting restores portrait — a deliberate,
/// owner-approved exception to the app-wide portrait lock in `main.dart`, for
/// the video surface only (YouTube is the POC provider; the HLS/MP4 fallback
/// keeps its play-only controls).
class LiveVideoPlayer extends ConsumerStatefulWidget {
  const LiveVideoPlayer({required this.url, super.key});

  final String url;

  @override
  ConsumerState<LiveVideoPlayer> createState() => _LiveVideoPlayerState();
}

class _LiveVideoPlayerState extends ConsumerState<LiveVideoPlayer> {
  YoutubePlayerController? _youtube;
  VideoPlayerController? _video;
  bool _videoReady = false;
  bool _error = false;

  /// D-726 (#13/#27) — the watch keep-alive lives on the shared player so
  /// EVERY surface that shows it is covered: the live screen AND the summary
  /// recording / summary-video cards, which use this player directly
  /// (bypassing `LivePlayerSurface`). While mounted it pings the session
  /// activity clock every minute so watching a long video (which produces no
  /// touch input) does not trip the SessionGuard idle sign-out. It cannot
  /// outlive playback — leaving the screen unmounts the player and `dispose`
  /// cancels the timer — and it can never exceed the server 24h cap (a refresh
  /// past it fails, D-443).
  Timer? _keepAlive;

  @override
  void initState() {
    super.initState();
    ref.read(sessionActivityProvider).markActive();
    _keepAlive = Timer.periodic(
      const Duration(minutes: 1),
      (_) => ref.read(sessionActivityProvider).markActive(),
    );
    _bind();
  }

  void _bind() {
    final videoId = YoutubeUrl.tryParseId(widget.url);
    if (videoId != null) {
      try {
        _youtube = YoutubePlayerController.fromVideoId(
          videoId: videoId,
          autoPlay: true,
          params: const YoutubePlayerParams(showFullscreenButton: true),
        )..setFullScreenListener(_onFullScreenChanged);
      } catch (_) {
        // A failure building the IFrame controller degrades to the error
        // surface rather than crashing the screen (Page_025 L-7).
        _error = true;
      }
    } else {
      unawaited(_initVideo(widget.url));
    }
  }

  void _retry() {
    _youtube?.close();
    _youtube = null;
    _video?.dispose();
    _video = null;
    setState(() {
      _error = false;
      _videoReady = false;
    });
    _bind();
  }

  Future<void> _initVideo(String url) async {
    try {
      final controller = VideoPlayerController.networkUrl(Uri.parse(url));
      _video = controller;
      await controller.initialize();
      if (!mounted) {
        return;
      }
      setState(() => _videoReady = true);
      unawaited(controller.play());
    } catch (_) {
      // ANY failure — a malformed URL (Uri.parse), an unreachable stream, or a
      // codec error — surfaces the error/retry state rather than spinning
      // forever or crashing the screen (Page_025 L-7).
      if (!mounted) {
        return;
      }
      setState(() => _error = true);
    }
  }

  void _toggleVideoPlay() {
    final controller = _video;
    if (controller == null) {
      return;
    }
    // The glyph is driven by the controller's ValueListenable in [_Player], so
    // no setState is needed here — just fire the toggle.
    unawaited(
      controller.value.isPlaying ? controller.pause() : controller.play(),
    );
  }

  /// Fired by the IFrame's fullscreen button (D-721). Video is the one surface
  /// allowed to break the app-wide portrait lock: landscape while fullscreen,
  /// portrait again on exit.
  void _onFullScreenChanged(bool isFullScreen) {
    unawaited(
      SystemChrome.setPreferredOrientations(liveFullScreenOrientations(isFullScreen)),
    );
  }

  @override
  void dispose() {
    _keepAlive?.cancel();
    _youtube?.close();
    _video?.dispose();
    // Re-assert the portrait lock in case we're torn down mid-fullscreen.
    unawaited(
      SystemChrome.setPreferredOrientations(liveFullScreenOrientations(false)),
    );
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_error) {
      final l10n = AppL10n.of(context);
      return _PlayerError(
        message: l10n.liveFeedError,
        retryLabel: l10n.retryLabel,
        onRetry: _retry,
      );
    }
    final youtube = _youtube;
    if (youtube != null) {
      return _YoutubeView(controller: youtube);
    }
    final video = _video;
    if (video != null && _videoReady) {
      return _Player(controller: video, onToggle: _toggleVideoPlay);
    }
    return const _PlayerLoading();
  }
}

/// The YouTube IFrame player surface (D-349) with a LIVE badge overlay. YouTube
/// supplies its own play/pause + CC controls (the latter covers الترجمة الفورية
/// for YouTube feeds), so no extra play FAB is added here.
class _YoutubeView extends StatelessWidget {
  const _YoutubeView({required this.controller});

  final YoutubePlayerController controller;

  @override
  Widget build(BuildContext context) {
    // The LIVE badge + language chip live in the surface's top row (934:3612),
    // not overlaid on the video, so the player is just the rounded feed.
    return ClipRRect(
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: YoutubePlayer(
        controller: controller,
        aspectRatio: SimfTokens.videoAspectRatio,
      ),
    );
  }
}

/// The `video_player` surface: a 16:9-aware [VideoPlayer] with a play/pause FAB
/// (the HLS/MP4 fallback path).
class _Player extends StatelessWidget {
  const _Player({required this.controller, required this.onToggle});

  final VideoPlayerController controller;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    final ratio = controller.value.aspectRatio == 0
        ? SimfTokens.videoAspectRatio
        : controller.value.aspectRatio;
    // The LIVE badge + language chip live in the surface's top row (934:3612).
    return ClipRRect(
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Stack(
        alignment: AlignmentDirectional.bottomEnd,
        children: <Widget>[
          AspectRatio(
            aspectRatio: ratio,
            child: VideoPlayer(controller),
          ),
          Padding(
            padding: const EdgeInsets.all(SimfTokens.space3),
            child: FloatingActionButton.small(
              heroTag: 'live-play',
              onPressed: onToggle,
              child: ValueListenableBuilder<VideoPlayerValue>(
                valueListenable: controller,
                builder: (_, value, __) => Icon(
                  value.isPlaying ? Icons.pause : Icons.play_arrow,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _PlayerLoading extends StatelessWidget {
  const _PlayerLoading();

  @override
  Widget build(BuildContext context) {
    // Frame 934:3595 — the resting/poster affordance: a 52px translucent-white
    // circle holding a 22px white play triangle (shown until the feed renders).
    return AspectRatio(
      aspectRatio: SimfTokens.videoAspectRatio,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: Colors.black,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
        ),
        child: Center(
          child: Container(
            width: 52,
            height: 52,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              color: SimfTokens.playScrim,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.play_arrow,
              size: 22,
              color: SimfTokens.surface,
            ),
          ),
        ),
      ),
    );
  }
}

/// Shown when a live feed fails to load — a terminal error surface with a Retry
/// that re-binds the player (Page_025 L-7), instead of an endless spinner.
class _PlayerError extends StatelessWidget {
  const _PlayerError({
    required this.message,
    required this.retryLabel,
    required this.onRetry,
  });

  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return AspectRatio(
      aspectRatio: SimfTokens.videoAspectRatio,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: Colors.black,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
        ),
        // Centred + scrollable so the icon + message + button never overflow
        // the fixed 16:9 box on a short / landscape viewport (RenderFlex).
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(SimfTokens.space3),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                const Icon(
                  Icons.error_outline,
                  size: 36,
                  color: SimfTokens.beigeBorder,
                ),
                const SizedBox(height: SimfTokens.space2),
                Text(
                  message,
                  textAlign: TextAlign.center,
                  style: SimfTokens.hintBeige,
                ),
                const SizedBox(height: SimfTokens.space3),
                FilledButton(onPressed: onRetry, child: Text(retryLabel)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
