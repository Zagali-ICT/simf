import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import 'package:video_player/video_player.dart';
import 'package:youtube_player_iframe/youtube_player_iframe.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/localization/locale_controller.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../core/organization_profile/organization_profile.dart';
import '../accessibility/data/accessibility_controller.dart';
import 'data/live_repository.dart';
import 'youtube_url.dart';

/// Page 025 — البث المباشر · Live broadcast (#25, `/live?sessionId=`), rebuilt
/// to the KSA-Project Figma frame **934:3450** on the shared navy shell.
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
/// **Frame mapping (934:3450):** the navy header (circled back chevron + centred
/// title), a **black player surface** carrying the LIVE badge + the gold-bordered
/// "AI live-caption" strip, then the **"يُبث الآن" now-broadcasting** block (the
/// session title as a gold bullet), the gold **region-restriction notice card**,
/// and the **ask-a-question** entry to Page 026 (`/live/question`). The hall name
/// and the speakers/participants line ride the wire (D-433); the **AI live-caption
/// text** rides the wire too (P5 — D-439: an admin-set field on the session, the
/// stubbed-provider surface), and the strip falls back to a placeholder hint when
/// it is blank. The language chip toggles the app locale. This screen renders
/// only what the contract carries and never fabricates the missing rows.
///
/// **Provider (D-349):** the live-video provider is **YouTube** (POC). Each feed
/// URL is sniffed by [YoutubeUrl]: a YouTube link plays via the IFrame player,
/// anything else (HLS/MP4) via `video_player`. The player widget owns its own
/// controller lifecycle, so swapping the active URL just rebuilds it.
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
  // D-433 — the "الجلسات القادمة" strip (a non-blocking second read).
  List<UpcomingSession> _upcoming = const <UpcomingSession>[];

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
      // The upcoming-sessions strip is optional chrome — load it after the main
      // read, non-blocking (a list failure must not break the live screen).
      unawaited(_loadUpcoming());
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

  Future<void> _loadUpcoming() async {
    try {
      final upcoming = await ref
          .read(liveRepositoryProvider)
          .getUpcomingSessions(excludeSessionId: widget.sessionId?.trim());
      if (!mounted) {
        return;
      }
      setState(() => _upcoming = upcoming);
    } on ApiFailure {
      // Optional strip — ignore a list failure, the live screen still works.
    }
  }

  void _askQuestion() {
    context.pushNamed(
      RouteNames.sendQuestion,
      queryParameters: <String, String>{'sessionId': widget.sessionId!.trim()},
    );
  }

  /// D-495 — a synthetic live session for the forum's main (global) live stream,
  /// used when the screen opens without a specific session id. The title is the
  /// forum name; the feed is the Organization profile's liveStreamUrl.
  LiveSession _globalLiveSession(OrgProfile profile, String url) => LiveSession(
        title: profile.name,
        titleArabic: profile.nameArabic,
        status: 1,
        hasRecording: false,
        liveStreamUrl: url,
      );

  /// The frame's header line — "يُبث الآن · {hall}" (or just the label when the
  /// broadcasting hall is not known).
  static String _broadcastLabel(AppL10n l10n, bool isLive, String? hall) {
    final base = isLive ? l10n.liveNowBroadcasting : l10n.liveSessionLabel;
    return hall == null ? base : '$base · $hall';
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return KsaPage(
      title: l10n.liveBroadcastTitle,
      onBack: () => ksaBackOrHome(context),
      tab: SimfTab.sessions,
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (!_hasId) {
      // D-495 — no session id → play the forum's main (global) live-stream link
      // from the Organization profile when the admin has configured one; else the
      // "pick a session" empty state.
      final profile = ref.watch(orgProfileProvider);
      final globalUrl = profile?.liveStreamUrl;
      if (profile != null && globalUrl != null && globalUrl.isNotEmpty) {
        return _content(l10n, _globalLiveSession(profile, globalUrl));
      }
      return KsaEmptyState(
        icon: Icons.live_tv_outlined,
        message: l10n.liveNoSessionSelected,
      );
    }
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_notFound) {
      return KsaEmptyState(
        icon: Icons.live_tv_outlined,
        message: l10n.sessionNotFound,
      );
    }
    if (_error || _session == null) {
      return KsaErrorState(
        message: l10n.liveBroadcastError,
        retryLabel: l10n.retryLabel,
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
    final isLive = mainUrl != null;
    // When the main feed is present, the active feed is the sign-language one
    // only while the toggle is on AND a sign feed exists; otherwise the main feed.
    final activeUrl = (_showSignLanguage && signUrl != null) ? signUrl : mainUrl;

    return ListView(
      padding: EdgeInsets.zero,
      children: <Widget>[
        // The black player surface (frame 934:3614) — full-bleed, edge to edge.
        if (mainUrl != null)
          _PlayerSurface(
            // Keyed by the active URL so swapping the feed disposes the old
            // controller and builds a fresh player for the new one.
            key: ValueKey<String>(activeUrl!),
            url: activeUrl,
            liveLabel: l10n.liveNowLabel,
            // P5 — D-439: the admin-set AI caption when present, else the
            // placeholder hint (YouTube CC supplies captions meanwhile).
            caption: session.localizedCaption(isArabic),
            captionHint: l10n.liveCaptionHint,
          )
        else if (session.hasRecording)
          _RecordingSurface(message: l10n.liveRecordingAvailable)
        else
          _NotLiveSurface(message: l10n.liveNotLiveYet),

        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space5,
            SimfTokens.space4,
            SimfTokens.space6,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              if (hasBothFeeds) ...<Widget>[
                _FeedToggle(
                  showSignLanguage: _showSignLanguage,
                  mainLabel: l10n.liveFeedMain,
                  signLabel: l10n.liveFeedSignLanguage,
                  onChanged: (value) =>
                      setState(() => _showSignLanguage = value),
                ),
                const SizedBox(height: SimfTokens.space5),
              ],

              // "يُبث الآن" now-broadcasting block (frame 934:3615 / 934:3616):
              // the section label over the session title as a gold bullet.
              Text(
                // D-433 — the hall name (already on the wire) completes the
                // frame's "يُبث الآن · القاعة الرئيسية" header line.
                _broadcastLabel(l10n, isLive, session.localizedHall(isArabic)),
                textAlign: TextAlign.right,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: SimfTokens.textLg,
                  fontWeight: FontWeight.w500,
                ),
              ),
              const SizedBox(height: SimfTokens.space4),
              _GoldBullet(
                text: session.localizedTitle(isArabic),
                color: SimfTokens.accent,
                fontWeight: FontWeight.w600,
                // Frame 934:3616 — the session title bullet is 16px.
                fontSize: SimfTokens.textLg,
              ),
              // D-433 — the speakers / participants line (frame 934:3617).
              if (session.localizedSpeakers(isArabic) != null) ...<Widget>[
                const SizedBox(height: SimfTokens.space2),
                _GoldBullet(
                  text: session.localizedSpeakers(isArabic)!,
                  color: SimfTokens.beigeBorder,
                ),
              ],

              // Sign-language-only note (a sign feed announced with no main
              // feed → nothing to toggle, just the note).
              if (signUrl != null && mainUrl == null) ...<Widget>[
                const SizedBox(height: SimfTokens.space4),
                _SignLanguageNote(label: l10n.liveSignLanguageAvailable),
              ],

              const SizedBox(height: SimfTokens.space5),
              // The gold region-restriction notice card (frame 934:3619).
              _RegionNoticeCard(
                noticeLabel: l10n.liveRegionNoticeLabel,
                noticeBody: l10n.liveRegionNoticeBody,
              ),

              // Ask-a-question entry → Page 026 (the frame's L-3 Q&A affordance).
              // Session-specific — only for a real session, not the global main-live.
              if (_hasId) ...<Widget>[
                const SizedBox(height: SimfTokens.space6),
                _AskQuestionButton(
                  label: l10n.liveAskQuestion,
                  onTap: _askQuestion,
                ),
              ],

              // D-433 — "الجلسات القادمة" upcoming-sessions cards (frame
              // 934:3621/3630), from the shipped agenda list (non-blocking read).
              if (_upcoming.isNotEmpty) ...<Widget>[
                const SizedBox(height: SimfTokens.space6),
                Text(
                  l10n.liveUpcomingSessions,
                  textAlign: TextAlign.right,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textLg,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: SimfTokens.space4),
                for (final upcoming in _upcoming) ...<Widget>[
                  _UpcomingCard(session: upcoming, isArabic: isArabic),
                  const SizedBox(height: SimfTokens.space3),
                ],
              ],
            ],
          ),
        ),
      ],
    );
  }
}

/// One "الجلسات القادمة" card (frame 934:3621/3630): the session title with a
/// gold HH:mm time chip at the inline-end.
class _UpcomingCard extends StatelessWidget {
  const _UpcomingCard({required this.session, required this.isArabic});

  final UpcomingSession session;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      child: Padding(
        // Frame 934:3621 — px8 / py16 on the radius-4 navy card.
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space2,
          vertical: SimfTokens.space4,
        ),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Text(
                session.localizedTitle(isArabic),
                textAlign: TextAlign.right,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                // Frame 934:3626 — the upcoming-session title is 14px Bold.
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: SimfTokens.textMd,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space3),
            _TimeChip(time: session.startUtc),
          ],
        ),
      ),
    );
  }
}

/// A gold time chip showing the local HH:mm (frame 934:3628).
class _TimeChip extends StatelessWidget {
  const _TimeChip({required this.time});

  final DateTime? time;

  @override
  Widget build(BuildContext context) {
    final t = time;
    final label = t == null
        ? '—'
        : '${t.hour.toString().padLeft(2, '0')}:'
            '${t.minute.toString().padLeft(2, '0')}';
    return Container(
      // Frame 934:3628 — a fixed 53-wide gold chip, p-4, radius-4.
      width: 53,
      padding: const EdgeInsets.all(SimfTokens.space1),
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        textDirection: TextDirection.ltr,
        textAlign: TextAlign.center,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textMd,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

/// The black live player surface (frame 934:3614): the player fills a 16:9 box
/// over a black backdrop, with the LIVE badge pinned top-start and the
/// gold-bordered AI live-caption strip below it.
class _PlayerSurface extends StatelessWidget {
  const _PlayerSurface({
    required this.url,
    required this.liveLabel,
    required this.captionHint,
    this.caption,
    super.key,
  });

  final String url;
  final String liveLabel;

  /// P5 — D-439: the admin-set AI caption text for this session, or null.
  final String? caption;
  final String captionHint;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Colors.black,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            // Frame 934:3612 — the badges sit in a flex row at the top of the
            // black band (LIVE at the inline-start / right, the language chip at
            // the inline-end / left), not overlaid on the video.
            Row(
              children: <Widget>[
                _LiveBadge(label: liveLabel),
                const Spacer(),
                const _LanguageChip(),
              ],
            ),
            const SizedBox(height: SimfTokens.space4),
            _LivePlayer(url: url),
            const SizedBox(height: SimfTokens.space4),
            _CaptionStrip(caption: caption, hint: captionHint),
          ],
        ),
      ),
    );
  }
}

/// The gold-bordered AI live-caption strip under the player (frame 934:3613):
/// the caption text with a small gold "AI" badge. P5 — D-439: when the session
/// carries admin-set [caption] text (the stubbed-provider surface) it is shown
/// in readable white; otherwise the muted placeholder [hint] is shown (and for a
/// YouTube feed the player's own CC supplies captions meanwhile).
class _CaptionStrip extends ConsumerWidget {
  const _CaptionStrip({required this.hint, this.caption});

  /// The real AI caption text, or null to show the placeholder [hint].
  final String? caption;
  final String hint;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Page-038 captions toggle: when the user turns captions off, hide the
    // strip entirely (the YouTube player's own CC stays user-controlled).
    // Defaults to shown when the accessibility DI isn't wired (widget tests).
    bool captionsEnabled;
    try {
      captionsEnabled = ref.watch(accessibilityControllerProvider).captions;
    } catch (_) {
      captionsEnabled = true;
    }
    if (!captionsEnabled) {
      return const SizedBox.shrink();
    }
    final hasCaption = caption != null;
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space3,
      ),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(color: SimfTokens.beigeBorder, width: 0.2),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              caption ?? hint,
              textAlign: TextAlign.right,
              style: TextStyle(
                // Real caption text reads in white; the placeholder is the
                // frame's soft caption colour (#DDE4F0, 934:3613).
                color: hasCaption ? SimfTokens.surface : SimfTokens.captionText,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Container(
            width: 20,
            height: 20,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: SimfTokens.accent,
              borderRadius: BorderRadius.circular(5),
            ),
            child: const Text(
              'AI',
              style: TextStyle(
                color: Colors.white,
                // Frame 934:3602 — 12px SemiBold.
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The live video surface. Owns its own controller and picks the player by the
/// URL (D-349): a YouTube link → the IFrame player; anything else (HLS/MP4) →
/// `video_player`. The parent rebuilds this with a new `ValueKey(url)` to switch
/// feeds, so this widget only ever binds one URL for its lifetime.
class _LivePlayer extends StatefulWidget {
  const _LivePlayer({required this.url});

  final String url;

  @override
  State<_LivePlayer> createState() => _LivePlayerState();
}

class _LivePlayerState extends State<_LivePlayer> {
  YoutubePlayerController? _youtube;
  VideoPlayerController? _video;
  bool _videoReady = false;
  bool _error = false;

  @override
  void initState() {
    super.initState();
    _bind();
  }

  void _bind() {
    final videoId = YoutubeUrl.tryParseId(widget.url);
    if (videoId != null) {
      try {
        _youtube = YoutubePlayerController.fromVideoId(
          videoId: videoId,
          autoPlay: true,
        );
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

  @override
  void dispose() {
    _youtube?.close();
    _video?.dispose();
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
      child: YoutubePlayer(controller: controller, aspectRatio: 16 / 9),
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
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: _TogglePill(
              label: mainLabel,
              active: !showSignLanguage,
              onTap: () => onChanged(false),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: _TogglePill(
              label: signLabel,
              active: showSignLanguage,
              icon: Icons.sign_language_outlined,
              onTap: () => onChanged(true),
            ),
          ),
        ],
      ),
    );
  }
}

/// One feed-toggle pill — the gold/navy view-pill language shared with the
/// sessions screen: active = solid gold, inactive = bordered navy card.
class _TogglePill extends StatelessWidget {
  const _TogglePill({
    required this.label,
    required this.active,
    required this.onTap,
    this.icon,
  });

  final String label;
  final bool active;
  final VoidCallback onTap;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      onTap: active ? null : onTap,
      color: active ? SimfTokens.accent : SimfTokens.navyDeep,
      borderColor: active ? SimfTokens.accent : SimfTokens.beigeBorder,
      child: SizedBox(
        height: 44,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            if (icon != null) ...<Widget>[
              Icon(icon, size: 16, color: Colors.white),
              const SizedBox(width: SimfTokens.space1),
            ],
            Flexible(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A gold (or beige) right-aligned bulleted line — the frame's "·"-led list
/// items (the session title 934:3616, the speakers line 934:3617).
class _GoldBullet extends StatelessWidget {
  const _GoldBullet({
    required this.text,
    required this.color,
    this.fontWeight = FontWeight.w500,
    this.fontSize = SimfTokens.textMd,
  });

  final String text;
  final Color color;
  final FontWeight fontWeight;
  final double fontSize;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Expanded(
          child: Text(
            text,
            textAlign: TextAlign.right,
            style: TextStyle(
              color: color,
              fontSize: fontSize,
              fontWeight: fontWeight,
              height: 1.5,
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        Padding(
          padding: const EdgeInsets.only(top: 6),
          child: Container(
            width: 5,
            height: 5,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
        ),
      ],
    );
  }
}

/// The gold region-restriction notice card (frame 934:3619): a bold "إشعار:"
/// label followed by the static notice body, on a solid gold card.
class _RegionNoticeCard extends StatelessWidget {
  const _RegionNoticeCard({required this.noticeLabel, required this.noticeBody});

  final String noticeLabel;
  final String noticeBody;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space3,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Text.rich(
        TextSpan(
          children: <InlineSpan>[
            TextSpan(
              text: '$noticeLabel ',
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
            TextSpan(text: noticeBody),
          ],
        ),
        textAlign: TextAlign.right,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textMd,
          fontWeight: FontWeight.w500,
          height: 1.5,
        ),
      ),
    );
  }
}

/// The ask-a-question entry (frame's L-3 Q&A affordance) → Page 026
/// (`/live/question?sessionId=`). A full-width gold action button.
class _AskQuestionButton extends StatelessWidget {
  const _AskQuestionButton({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      child: FilledButton.icon(
        onPressed: onTap,
        icon: const Icon(Icons.help_outline, size: 18),
        label: Text(label),
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.accent,
          foregroundColor: Colors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
          textStyle: const TextStyle(
            fontSize: SimfTokens.textLg,
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
    );
  }
}

/// The `video_player` surface: a 16:9-aware [VideoPlayer] with a LIVE badge
/// overlay and a play/pause FAB (the HLS/MP4 fallback path).
class _Player extends StatelessWidget {
  const _Player({required this.controller, required this.onToggle});

  final VideoPlayerController controller;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    final ratio = controller.value.aspectRatio == 0
        ? 16 / 9
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
      aspectRatio: 16 / 9,
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
            child: const Icon(Icons.play_arrow, size: 22, color: Colors.white),
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
      aspectRatio: 16 / 9,
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
                  style: const TextStyle(color: SimfTokens.beigeBorder),
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

/// The red LIVE badge (frame 934:3609): a white pulse dot before the label.
class _LiveBadge extends StatelessWidget {
  const _LiveBadge({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      // Frame 934:3609 — px10 py4 on a brick-red (#C0392B) pill, radius 6.
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.liveRed,
        borderRadius: BorderRadius.circular(SimfTokens.radius6),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        // Latin pill: the "LIVE" label leads and the ~7px white pulse dot trails
        // it (frame 934:3609/3610), regardless of the surrounding RTL direction.
        textDirection: TextDirection.ltr,
        children: <Widget>[
          Text(
            label,
            style: const TextStyle(
              color: SimfTokens.surface,
              fontWeight: FontWeight.w700,
              // Frame 934:3611 — 12px Bold.
              fontSize: SimfTokens.textSm,
            ),
          ),
          const SizedBox(width: 5),
          Container(
            width: 7,
            height: 7,
            decoration: const BoxDecoration(
              color: SimfTokens.surface,
              shape: BoxShape.circle,
            ),
          ),
        ],
      ),
    );
  }
}

/// The frame's caption-language chip (934:3450) on the player band, opposite the
/// LIVE badge. Shows the current UI language's native name + a globe glyph and
/// toggles the app language on tap (owner decision: wire to the app locale).
class _LanguageChip extends ConsumerWidget {
  const _LanguageChip();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isArabic = AppL10n.of(context).isArabic;
    return Material(
      // Frame 934:3604 — a translucent dark glassy pill with a gold hairline and
      // white text/icon (was a solid white chip with navy glyphs).
      color: SimfTokens.scrimBlack55,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(
          color: SimfTokens.accent,
          width: SimfTokens.hairline,
        ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        onTap: () =>
            unawaited(ref.read(localeControllerProvider.notifier).toggle()),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: 10,
            vertical: 5,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                isArabic ? 'العربية' : 'English',
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontWeight: FontWeight.w400,
                  fontSize: SimfTokens.textSm,
                ),
              ),
              const SizedBox(width: 5),
              const Icon(Icons.language, size: 14, color: SimfTokens.surface),
            ],
          ),
        ),
      ),
    );
  }
}

/// The black surface shown when there is no live stream but a recording exists —
/// keeps the frame's black player band, with a recording note instead of a feed.
class _RecordingSurface extends StatelessWidget {
  const _RecordingSurface({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return _MessageSurface(
      icon: Icons.video_library_outlined,
      message: message,
    );
  }
}

/// The black surface shown when the session is neither live nor recorded.
class _NotLiveSurface extends StatelessWidget {
  const _NotLiveSurface({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return _MessageSurface(
      icon: Icons.live_tv_outlined,
      message: message,
    );
  }
}

/// The black player-band placeholder for the non-live states (recording /
/// not-live) — keeps the frame's full-bleed black band, centring an icon +
/// message where the feed would play.
class _MessageSurface extends StatelessWidget {
  const _MessageSurface({required this.icon, required this.message});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Colors.black,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: AspectRatio(
          aspectRatio: 16 / 9,
          child: DecoratedBox(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(SimfTokens.radius),
              border: Border.all(color: SimfTokens.beigeBorder, width: 0.2),
            ),
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space4),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Icon(icon, size: 40, color: SimfTokens.beigeBorder),
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      message,
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: SimfTokens.beigeBorder),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// A sign-language-available note — a gold icon + muted label (shown when a sign
/// feed is announced with no main feed to toggle into).
class _SignLanguageNote extends StatelessWidget {
  const _SignLanguageNote({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        const Icon(
          Icons.sign_language_outlined,
          size: 18,
          color: SimfTokens.accent,
        ),
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: Text(
            label,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ),
      ],
    );
  }
}
