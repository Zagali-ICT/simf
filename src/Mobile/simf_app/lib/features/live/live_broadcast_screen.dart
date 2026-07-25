import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/organization_profile/organization_profile.dart';
import '../../core/site_settings/site_settings.dart';
import '../sessions/data/rate_prompt_tracker.dart';
import 'data/live_repository.dart';
import 'widgets/live_content.dart';
import 'widgets/live_message_surfaces.dart';
import 'widgets/live_player_surface.dart';

/// Page 025 — البث المباشر · Live broadcast (#25, `/live?sessionId=`), rebuilt
/// to the KSA-Project Figma frame **934:3450** on the shared navy shell.
///
/// **Login-only** (owner, D-577): a signed-out guest sees an in-screen "need
/// login" prompt with a Sign-in button instead of the player, from any entry
/// point (supersedes the D-199 "public, anonymous" design). This route is NOT
/// router-redirect-gated (unlike sessions/detail under D-576) — the gate is
/// in-screen, so the guest still lands here. A signed-in user takes an optional
/// [sessionId] from the query string.
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
/// and the **ask-a-question** entry to Page 026 (`/live/question`). The player
/// surface + its media engine live in `widgets/live_player_surface.dart` +
/// `live_video_player.dart` + `live_badges.dart`; the non-live black bands in
/// `live_message_surfaces.dart`; the info column widgets in `live_content.dart`.
///
/// **Provider (D-349):** the live-video provider is **YouTube** (POC). Each feed
/// URL is sniffed by `YoutubeUrl`: a YouTube link plays via the IFrame player,
/// anything else (HLS/MP4) via `video_player`. The player widget owns its own
/// controller lifecycle, so swapping the active URL just rebuilds it.
class LiveBroadcastScreen extends ConsumerStatefulWidget {
  const LiveBroadcastScreen({this.sessionId, this.liveUrl, super.key});

  final String? sessionId;
  final String? liveUrl;

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

  /// The router captured in [didChangeDependencies] so [dispose] can push the
  /// after-watch rate prompt (D-712) after this element is gone.
  GoRouter? _router;

  /// Non-null only when this view is eligible to prompt on leave — a signed-in
  /// approved attendee who actually had a live feed to watch. Captured in [_load]
  /// so [dispose] never reads a provider from a dead element. Shares the D-690
  /// tracker + the `Session` rating code, so watching online then leaving the
  /// (ended) session detail can't double-prompt.
  SessionRatePromptTracker? _rateTracker;

  bool get _hasId =>
      widget.sessionId != null && widget.sessionId!.trim().isNotEmpty;

  @override
  void initState() {
    super.initState();
    // Login-gate (owner): a signed-out guest sees the need-login prompt instead
    // of the stream, so don't fetch the session for them.
    final isSignedIn = ref.read(authControllerProvider) is AuthStateSignedIn;
    if (isSignedIn && _hasId) {
      unawaited(_load());
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // maybeOf (not of): a bare-MaterialApp test host with no GoRouter leaves this
    // null, and the leave-prompt simply does not fire.
    _router = GoRouter.maybeOf(context);
  }

  @override
  void dispose() {
    _maybePromptRateAfterWatch();
    super.dispose();
  }

  bool _isAttendeeRole(AppRole role) =>
      role == AppRole.visitor || role == AppRole.exhibitor;

  /// D-712 (FDS-007 §C.4 GAP-B, owner item 8) — "online session, live-stream
  /// close → rate the online session". When an approved attendee leaves the live
  /// screen for a session that carried a live feed, open the dynamic rate screen
  /// for it once. Runs from [dispose] (the reliable "left the screen" signal for
  /// every exit path) and pushes through the captured [GoRouter] on the next
  /// frame. Forward navigations (ask-a-question, sign-in) keep this screen alive,
  /// so they do not fire it; the shared tracker dedups it with the D-690
  /// after-view prompt so a session is rated at most once.
  void _maybePromptRateAfterWatch() {
    final router = _router;
    final tracker = _rateTracker;
    final sessionId = widget.sessionId?.trim();
    if (router == null ||
        tracker == null ||
        sessionId == null ||
        sessionId.isEmpty ||
        tracker.hasShown(sessionId)) {
      return;
    }
    unawaited(tracker.markShown(sessionId));
    WidgetsBinding.instance.addPostFrameCallback((_) {
      unawaited(
        router.pushNamed(
          RouteNames.rate,
          queryParameters: <String, String>{
            'code': 'Session',
            'targetId': sessionId,
          },
        ),
      );
    });
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
      // D-712 — capture the after-watch rate eligibility: an approved attendee
      // (a pending account presents as guest via effectiveAppRole and is excluded)
      // who actually had a live feed to watch. Captured here so [dispose] reuses
      // the reference instead of reading a provider from a dead element.
      final auth = ref.read(authControllerProvider);
      final isApprovedAttendee = auth is AuthStateSignedIn &&
          _isAttendeeRole(auth.session.user.effectiveAppRole);
      // 2026-07-22 — respect the CP: no after-watch prompt when the "Session"
      // rating type is deactivated in RatingConfig (siteSettings.sessionRatingEnabled).
      // Fail-open (true) while the cached settings load / on error, matching the
      // server, which also suppresses the notification when the type is off.
      final sessionRatingEnabled =
          ref.read(siteSettingsProvider).valueOrNull?.sessionRatingEnabled ?? true;
      _rateTracker = isApprovedAttendee &&
              session.liveStreamUrl != null &&
              sessionRatingEnabled
          ? ref.read(sessionRatePromptTrackerProvider)
          : null;
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
      queryParameters: <String, String>{RouteParams.sessionId: widget.sessionId!.trim()},
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
    return SimfPageShell(
      title: l10n.liveBroadcastTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    // Login-gate (owner, D-577): the live stream is login-only — a signed-out
    // guest sees a "need login" prompt with a Sign-in button instead of the
    // player, from any entry point (session link, deep link, global main-live).
    final isSignedIn = ref.watch(authControllerProvider) is AuthStateSignedIn;
    if (!isSignedIn) {
      return NeedLoginState(
        message: l10n.liveNeedLogin,
        signInLabel: l10n.signInButton,
        onSignIn: () => context.pushNamed(RouteNames.signIn),
      );
    }
    if (!_hasId) {
      // D-495 — no session id → play the forum's main (global) live-stream link
      // from the Organization profile when the admin has configured one; else the
      // "pick a session" empty state.
      // When a liveUrl param is provided (e.g. from the home LiveBanner tap), use
      // it directly without hitting the API or the org profile.
      final explicitUrl = widget.liveUrl?.trim();
      if (explicitUrl != null && explicitUrl.isNotEmpty) {
        return _content(
          l10n,
          LiveSession(
            title: '',
            titleArabic: '',
            status: 1,
            hasRecording: false,
            liveStreamUrl: explicitUrl,
          ),
        );
      }
      final profile = ref.watch(orgProfileProvider);
      final globalUrl = profile?.liveStreamUrl;
      if (profile != null && globalUrl != null && globalUrl.isNotEmpty) {
        return _content(l10n, _globalLiveSession(profile, globalUrl));
      }
      return SimfEmptyState(
        icon: Icons.live_tv_outlined,
        message: l10n.liveNoSessionSelected,
      );
    }
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_notFound) {
      return SimfEmptyState(
        icon: Icons.live_tv_outlined,
        message: l10n.sessionNotFound,
      );
    }
    if (_error || _session == null) {
      return SimfErrorState(
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
    // S-3 — "live" is the session being INSIDE its scheduled window, not merely
    // "a feed URL is present" (an admin may set the URL before start or leave it
    // after end). This drives the LIVE badge so it never lies before start /
    // after end (the backend closes questions at End). When the window is
    // unknown — the global main-live synthetic (no id) has no start/end — fall
    // back to "a feed is present" so the always-on forum stream still reads live.
    final start = session.start;
    final end = session.end;
    final nowUtc = DateTime.now().toUtc();
    final isLive = (start != null && end != null)
        ? !nowUtc.isBefore(start) && nowUtc.isBefore(end)
        : mainUrl != null;
    // S-3 honesty — the "يُبث الآن" header and the Ask affordance must never
    // render over a not-live / recording surface, so they also require the feed
    // to actually be up. The LIVE badge already only shows when a URL exists.
    final isBroadcasting = isLive && mainUrl != null;
    // When the main feed is present, the active feed is the sign-language one
    // only while the toggle is on AND a sign feed exists; otherwise the main feed.
    final activeUrl = (_showSignLanguage && signUrl != null) ? signUrl : mainUrl;

    return ListView(
      padding: EdgeInsets.zero,
      children: <Widget>[
        // The black player surface (frame 934:3614) — full-bleed, edge to edge.
        if (mainUrl != null)
          LivePlayerSurface(
            // Keyed by the active URL so swapping the feed disposes the old
            // controller and builds a fresh player for the new one.
            key: ValueKey<String>(activeUrl!),
            url: activeUrl,
            // S-3 — the LIVE badge only when the session is genuinely in-window;
            // null (badge hidden) for a pre-start premiere / post-end archive URL.
            liveLabel: isLive ? l10n.liveNowLabel : null,
            // P5 — D-439: the admin-set AI caption when present, else the
            // placeholder hint (YouTube CC supplies captions meanwhile).
            caption: session.localizedCaption(isArabic),
            captionHint: l10n.liveCaptionHint,
          )
        else if (session.hasRecording)
          RecordingSurface(message: l10n.liveRecordingAvailable)
        else
          NotLiveSurface(message: l10n.liveNotLiveYet),

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
                FeedToggle(
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
                _broadcastLabel(
                  l10n,
                  isBroadcasting,
                  session.localizedHall(isArabic),
                ),
                textAlign: TextAlign.start,
                style: SimfTokens.labelWhiteMediumLg,
              ),
              const SizedBox(height: SimfTokens.space4),
              GoldBullet(
                text: session.localizedTitle(isArabic),
                color: SimfTokens.accent,
                fontWeight: FontWeight.w600,
                // Frame 934:3616 — the session title bullet is 16px.
                fontSize: SimfTokens.textLg,
              ),
              // D-433 — the speakers / participants line (frame 934:3617).
              if (session.localizedSpeakers(isArabic) != null) ...<Widget>[
                const SizedBox(height: SimfTokens.space2),
                GoldBullet(
                  text: session.localizedSpeakers(isArabic)!,
                  color: SimfTokens.beigeBorder,
                ),
              ],

              // Sign-language-only note (a sign feed announced with no main
              // feed → nothing to toggle, just the note).
              if (signUrl != null && mainUrl == null) ...<Widget>[
                const SizedBox(height: SimfTokens.space4),
                SignLanguageNote(label: l10n.liveSignLanguageAvailable),
              ],

              const SizedBox(height: SimfTokens.space5),
              // The gold region-restriction notice card (frame 934:3619).
              RegionNoticeCard(
                noticeLabel: l10n.liveRegionNoticeLabel,
                noticeBody: l10n.liveRegionNoticeBody,
              ),

              // Ask-a-question entry → Page 026 (the frame's L-3 Q&A affordance).
              // Session-specific — only for a real session, not the global main-live.
              // S-3 (owner) — only while the session is actually broadcasting (now
              // within its [start, end] window AND a feed is up): before start the
              // ask lives on the detail screen, and after end the backend closes
              // questions (the view is a YouTube archive, not a live broadcast).
              if (_hasId && isBroadcasting) ...<Widget>[
                const SizedBox(height: SimfTokens.space6),
                AskQuestionButton(
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
                  textAlign: TextAlign.start,
                  style: SimfTokens.labelWhiteMediumLg,
                ),
                const SizedBox(height: SimfTokens.space4),
                for (final upcoming in _upcoming) ...<Widget>[
                  UpcomingCard(session: upcoming, isArabic: isArabic),
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
