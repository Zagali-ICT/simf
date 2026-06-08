import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import 'package:video_player/video_player.dart';
import 'package:youtube_player_iframe/youtube_player_iframe.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/live_repository.dart';
import 'youtube_url.dart';

/// Page 025 — البث المباشر · Live broadcast (#25, `/live?sessionId=`).
///
/// **Public** (anonymous). Takes an optional [sessionId] from the query string.
/// With no id it shows a "pick a session" empty state and never fetches. With
/// an id it reads the broadcast slice (`GET /app/programme/sessions/{id}`,
/// `AllowAnonymous`) and branches three ways (Page_025 L-3):
/// * `liveStreamUrl` non-empty → play the feed and show a LIVE badge; when a
///   `liveSignLanguageUrl` also exists a toggle swaps the player between the
///   main feed and the sign-language feed (Page_025 L-3);
/// * `liveStreamUrl` null but `hasRecording` → a "recording available" note;
/// * neither → a "not live / scheduled" state.
/// 404 → not-found; any other failure → retry.
///
/// **Provider (D-349):** the live-video provider is **YouTube** (POC). Each feed
/// URL is sniffed by [YoutubeUrl]: a YouTube link plays via the IFrame player,
/// anything else (HLS/MP4) via `video_player`. The player widget owns its own
/// controller lifecycle, so swapping the active URL just rebuilds it.
/// UI is interim — final visuals land with SIMF-VID-001.
class LiveBroadcastScreen extends ConsumerStatefulWidget {
  const LiveBroadcastScreen({this.sessionId, super.key});

  final String? sessionId;

  @override
  ConsumerState<LiveBroadcastScreen> createState() =>
      _LiveBroadcastScreenState();
}

class _LiveBroadcastScreenState extends ConsumerState<LiveBroadcastScreen> {
  bool _loading = false;
  bool _error = false;
  bool _notFound = false;
  LiveSession? _session;

  /// When true the player shows the sign-language feed instead of the main one.
  /// Only meaningful when the session carries both feeds (the toggle is hidden
  /// otherwise).
  bool _showSignLanguage = false;

  bool get _hasId =>
      widget.sessionId != null && widget.sessionId!.trim().isNotEmpty;

  @override
  void initState() {
    super.initState();
    if (_hasId) {
      unawaited(_load());
    }
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
      _notFound = false;
    });
    try {
      final session = await ref
          .read(liveRepositoryProvider)
          .getLiveSession(widget.sessionId!.trim());
      if (!mounted) {
        return;
      }
      setState(() {
        _session = session;
        _showSignLanguage = false;
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _loading = false;
        _notFound = failure.httpStatus == 404;
        _error = failure.httpStatus != 404;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.liveBroadcastTitle)),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (!_hasId) {
      return _EmptyState(
        icon: Icons.live_tv_outlined,
        message: l10n.liveNoSessionSelected,
      );
    }
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_notFound) {
      return _EmptyState(
        icon: Icons.live_tv_outlined,
        message: l10n.sessionNotFound,
      );
    }
    if (_error || _session == null) {
      return _ErrorState(
        message: l10n.liveBroadcastError,
        onRetry: () => unawaited(_load()),
      );
    }
    return _content(l10n, _session!);
  }

  Widget _content(AppL10n l10n, LiveSession session) {
    final isArabic = l10n.isArabic;
    final mainUrl = session.liveStreamUrl;
    final signUrl = session.liveSignLanguageUrl;
    final hasBothFeeds = mainUrl != null && signUrl != null;
    // When the main feed is present, the active feed is the sign-language one
    // only while the toggle is on AND a sign feed exists; otherwise the main feed.
    final activeUrl = (_showSignLanguage && signUrl != null) ? signUrl : mainUrl;

    return ListView(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space4,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        Text(
          session.localizedTitle(isArabic),
          style: const TextStyle(
            color: SimfTokens.surface,
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textXl,
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        if (mainUrl != null) ...<Widget>[
          // Keyed by the active URL so swapping the feed disposes the old
          // controller and builds a fresh player for the new one.
          _LivePlayer(
            key: ValueKey<String>(activeUrl!),
            url: activeUrl,
            liveLabel: l10n.liveNowLabel,
          ),
          if (hasBothFeeds) ...<Widget>[
            const SizedBox(height: SimfTokens.space3),
            _FeedToggle(
              showSignLanguage: _showSignLanguage,
              mainLabel: l10n.liveFeedMain,
              signLabel: l10n.liveFeedSignLanguage,
              onChanged: (value) =>
                  setState(() => _showSignLanguage = value),
            ),
          ],
        ] else if (session.hasRecording)
          _RecordingNote(l10n: l10n)
        else
          _NotLiveNote(l10n: l10n),
        // No main feed but a sign-language feed is announced → keep the note
        // (there is nothing to toggle between).
        if (signUrl != null && mainUrl == null) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          _SignLanguageNote(label: l10n.liveSignLanguageAvailable),
        ],
      ],
    );
  }
}

/// The live video surface. Owns its own controller and picks the player by the
/// URL (D-349): a YouTube link → the IFrame player; anything else (HLS/MP4) →
/// `video_player`. The parent rebuilds this with a new `ValueKey(url)` to switch
/// feeds, so this widget only ever binds one URL for its lifetime.
class _LivePlayer extends StatefulWidget {
  const _LivePlayer({required this.url, required this.liveLabel, super.key});

  final String url;
  final String liveLabel;

  @override
  State<_LivePlayer> createState() => _LivePlayerState();
}

class _LivePlayerState extends State<_LivePlayer> {
  YoutubePlayerController? _youtube;
  VideoPlayerController? _video;
  bool _videoReady = false;

  @override
  void initState() {
    super.initState();
    final videoId = YoutubeUrl.tryParseId(widget.url);
    if (videoId != null) {
      _youtube = YoutubePlayerController.fromVideoId(
        videoId: videoId,
        autoPlay: true,
      );
    } else {
      unawaited(_initVideo(widget.url));
    }
  }

  Future<void> _initVideo(String url) async {
    final controller = VideoPlayerController.networkUrl(Uri.parse(url));
    _video = controller;
    try {
      await controller.initialize();
    } catch (_) {
      // A bad/unreachable stream falls back to the loading surface rather than
      // crashing the screen (Page_025 L-7).
      if (!mounted) {
        return;
      }
      setState(() => _videoReady = false);
      return;
    }
    if (!mounted) {
      return;
    }
    setState(() => _videoReady = true);
    unawaited(controller.play());
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

  @override
  void dispose() {
    _youtube?.close();
    _video?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final youtube = _youtube;
    if (youtube != null) {
      return _YoutubeView(controller: youtube, liveLabel: widget.liveLabel);
    }
    final video = _video;
    if (video != null && _videoReady) {
      return _Player(
        controller: video,
        onToggle: _toggleVideoPlay,
        liveLabel: widget.liveLabel,
      );
    }
    return const _PlayerLoading();
  }
}

/// The YouTube IFrame player surface (D-349) with a LIVE badge overlay. YouTube
/// supplies its own play/pause + CC controls (the latter covers الترجمة الفورية
/// for YouTube feeds), so no extra play FAB is added here.
class _YoutubeView extends StatelessWidget {
  const _YoutubeView({required this.controller, required this.liveLabel});

  final YoutubePlayerController controller;
  final String liveLabel;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Stack(
        alignment: AlignmentDirectional.topStart,
        children: <Widget>[
          YoutubePlayer(controller: controller, aspectRatio: 16 / 9),
          PositionedDirectional(
            top: SimfTokens.space2,
            start: SimfTokens.space2,
            child: _LiveBadge(label: liveLabel),
          ),
        ],
      ),
    );
  }
}

/// Swaps the player between the main feed and the sign-language feed (Page_025
/// L-3). Shown only when the session carries both.
class _FeedToggle extends StatelessWidget {
  const _FeedToggle({
    required this.showSignLanguage,
    required this.mainLabel,
    required this.signLabel,
    required this.onChanged,
  });

  final bool showSignLanguage;
  final String mainLabel;
  final String signLabel;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return SegmentedButton<bool>(
      showSelectedIcon: false,
      segments: <ButtonSegment<bool>>[
        ButtonSegment<bool>(value: false, label: Text(mainLabel)),
        ButtonSegment<bool>(
          value: true,
          label: Text(signLabel),
          icon: const Icon(Icons.sign_language_outlined, size: 16),
        ),
      ],
      selected: <bool>{showSignLanguage},
      onSelectionChanged: (selection) => onChanged(selection.first),
    );
  }
}

/// The `video_player` surface: a 16:9-aware [VideoPlayer] with a LIVE badge
/// overlay and a play/pause FAB (the HLS/MP4 fallback path).
class _Player extends StatelessWidget {
  const _Player({
    required this.controller,
    required this.onToggle,
    required this.liveLabel,
  });

  final VideoPlayerController controller;
  final VoidCallback onToggle;
  final String liveLabel;

  @override
  Widget build(BuildContext context) {
    final ratio = controller.value.aspectRatio == 0
        ? 16 / 9
        : controller.value.aspectRatio;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        ClipRRect(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          child: Stack(
            alignment: AlignmentDirectional.bottomEnd,
            children: <Widget>[
              AspectRatio(
                aspectRatio: ratio,
                child: VideoPlayer(controller),
              ),
              PositionedDirectional(
                top: SimfTokens.space2,
                start: SimfTokens.space2,
                child: _LiveBadge(label: liveLabel),
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
        ),
      ],
    );
  }
}

class _PlayerLoading extends StatelessWidget {
  const _PlayerLoading();

  @override
  Widget build(BuildContext context) {
    return AspectRatio(
      aspectRatio: 16 / 9,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: Colors.black,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
        ),
        child: const Center(child: CircularProgressIndicator()),
      ),
    );
  }
}

class _LiveBadge extends StatelessWidget {
  const _LiveBadge({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.danger,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(
            Icons.fiber_manual_record,
            size: 10,
            color: SimfTokens.surface,
          ),
          const SizedBox(width: SimfTokens.space1),
          Text(
            label,
            style: const TextStyle(
              color: SimfTokens.surface,
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textXs,
            ),
          ),
        ],
      ),
    );
  }
}

/// Shown when there is no live stream but a recording exists — an interim note +
/// link affordance (no inline playback; the recorded-Q&A read lands later).
class _RecordingNote extends StatelessWidget {
  const _RecordingNote({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Row(
          children: <Widget>[
            const Icon(
              Icons.video_library_outlined,
              color: SimfTokens.accent,
            ),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Text(
                l10n.liveRecordingAvailable,
                style: const TextStyle(
                  color: SimfTokens.txtSecondary,
                  fontSize: SimfTokens.textMd,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SignLanguageNote extends StatelessWidget {
  const _SignLanguageNote({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        const Icon(Icons.sign_language_outlined, size: 18, color: SimfTokens.accent),
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: Text(
            label,
            style: const TextStyle(
              color: SimfTokens.txtSecondary,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ),
      ],
    );
  }
}

/// Shown when the session is neither live nor recorded — scheduled / off-air.
class _NotLiveNote extends StatelessWidget {
  const _NotLiveNote({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space5),
      decoration: BoxDecoration(
        color: SimfTokens.surfaceTint,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(color: SimfTokens.line),
      ),
      child: Column(
        children: <Widget>[
          const Icon(
            Icons.live_tv_outlined,
            size: 40,
            color: SimfTokens.txtTertiary,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.liveNotLiveYet,
            textAlign: TextAlign.center,
            style: const TextStyle(color: SimfTokens.txtTertiary),
          ),
        ],
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.icon, required this.message});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, size: 56, color: SimfTokens.txtTertiary),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.txtTertiary),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
