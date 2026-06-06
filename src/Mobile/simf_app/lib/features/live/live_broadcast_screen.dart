import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import 'package:video_player/video_player.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/live_repository.dart';

/// Page 025 — البث المباشر · Live broadcast (#25, `/live?sessionId=`).
///
/// **Public** (anonymous). Takes an optional [sessionId] from the query string.
/// With no id it shows a "pick a session" empty state and never fetches. With
/// an id it reads the broadcast slice (`GET /app/programme/sessions/{id}`,
/// `AllowAnonymous`) and branches three ways (Page_025 L-3):
/// * `liveStreamUrl` non-empty → initialise a [VideoPlayerController] and show
///   the player + a LIVE badge;
/// * `liveStreamUrl` null but `hasRecording` → a "recording available" note;
/// * neither → a "not live / scheduled" state.
/// 404 → not-found; any other failure → retry. The controller is disposed in
/// [dispose]. UI is interim — final visuals land with SIMF-VID-001.
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
  VideoPlayerController? _controller;
  bool _videoReady = false;

  bool get _hasId =>
      widget.sessionId != null && widget.sessionId!.trim().isNotEmpty;

  @override
  void initState() {
    super.initState();
    if (_hasId) {
      unawaited(_load());
    }
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
      _notFound = false;
    });
    try {
      final session =
          await ref.read(liveRepositoryProvider).getLiveSession(widget.sessionId!.trim());
      if (!mounted) {
        return;
      }
      setState(() {
        _session = session;
        _loading = false;
      });
      final url = session.liveStreamUrl;
      if (url != null) {
        await _initPlayer(url);
      }
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

  Future<void> _initPlayer(String url) async {
    final controller = VideoPlayerController.networkUrl(Uri.parse(url));
    _controller = controller;
    try {
      await controller.initialize();
    } catch (_) {
      // A bad/unreachable stream falls back to the recording/not-live copy
      // rather than crashing the screen (Page_025 L-4).
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
  }

  void _togglePlay() {
    final controller = _controller;
    if (controller == null) {
      return;
    }
    setState(() {
      controller.value.isPlaying ? controller.pause() : controller.play();
    });
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
    final controller = _controller;
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
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textXl,
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        if (session.liveStreamUrl != null && _videoReady && controller != null)
          _Player(controller: controller, onToggle: _togglePlay, liveLabel: l10n.liveNowLabel)
        else if (session.liveStreamUrl != null)
          const _PlayerLoading()
        else if (session.hasRecording)
          _RecordingNote(l10n: l10n)
        else
          _NotLiveNote(l10n: l10n),
        if (session.liveSignLanguageUrl != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          _SignLanguageNote(label: l10n.liveSignLanguageAvailable),
        ],
      ],
    );
  }
}

/// The video surface: a 16:9-aware [VideoPlayer] with a LIVE badge overlay and a
/// play/pause FAB.
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
            alignment: Alignment.bottomRight,
            children: <Widget>[
              AspectRatio(
                aspectRatio: ratio,
                child: VideoPlayer(controller),
              ),
              Positioned(
                top: SimfTokens.space2,
                left: SimfTokens.space2,
                child: _LiveBadge(label: liveLabel),
              ),
              Padding(
                padding: const EdgeInsets.all(SimfTokens.space3),
                child: FloatingActionButton.small(
                  heroTag: 'live-play',
                  onPressed: onToggle,
                  child: Icon(
                    controller.value.isPlaying
                        ? Icons.pause
                        : Icons.play_arrow,
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
          color: SimfTokens.field,
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
          const Icon(Icons.fiber_manual_record, size: 10, color: Colors.white),
          const SizedBox(width: SimfTokens.space1),
          Text(
            label,
            style: const TextStyle(
              color: Colors.white,
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
                style: const TextStyle(fontSize: SimfTokens.textMd),
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
              color: SimfTokens.inkMuted,
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
        color: SimfTokens.field,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        children: <Widget>[
          const Icon(
            Icons.live_tv_outlined,
            size: 40,
            color: SimfTokens.inkMuted,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.liveNotLiveYet,
            textAlign: TextAlign.center,
            style: const TextStyle(color: SimfTokens.inkMuted),
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
            Icon(icon, size: 56, color: SimfTokens.inkMuted),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.inkMuted),
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
