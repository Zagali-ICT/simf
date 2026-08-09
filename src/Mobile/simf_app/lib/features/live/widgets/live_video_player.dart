import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/session/session_activity.dart';
import 'package:simf_app/features/live/widgets/player.dart';
import 'package:simf_app/features/live/widgets/player_error.dart';
import 'package:simf_app/features/live/widgets/player_loading.dart';
import 'package:simf_app/features/live/widgets/youtube_view.dart';
import 'package:simf_app/features/live/youtube_url.dart';
import 'package:video_player/video_player.dart';
import 'package:youtube_player_iframe/youtube_player_iframe.dart';

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
    } else if (widget.url.startsWith('asset:')) {
      unawaited(_initAssetVideo(widget.url.substring(6)));
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

  Future<void> _initAssetVideo(String assetPath) async {
    try {
      final controller = VideoPlayerController.asset(assetPath);
      _video = controller;
      await controller.initialize();
      await controller.setLooping(true);
      await controller.setVolume(0);
      await controller.play();
      if (!mounted) {
        return;
      }
      setState(() => _videoReady = true);
    } catch (_) {
      if (!mounted) {
        return;
      }
      setState(() => _error = true);
    }
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
    // The glyph is driven by the controller's ValueListenable in [Player], so
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
      return PlayerError(
        message: l10n.liveFeedError,
        retryLabel: l10n.retryLabel,
        onRetry: _retry,
      );
    }
    final youtube = _youtube;
    if (youtube != null) {
      return YoutubeView(controller: youtube);
    }
    final video = _video;
    if (video != null && _videoReady) {
      return Player(controller: video, onToggle: _toggleVideoPlay);
    }
    return const PlayerLoading();
  }
}

