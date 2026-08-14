import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/core/site_settings/site_settings.dart';
import 'package:simf_app/features/live/data/live_models.dart';
import 'package:simf_app/features/live/data/live_presentation.dart';
import 'package:simf_app/features/live/data/live_repository.dart';
import 'package:simf_app/features/live/widgets/live_content_view.dart';
import 'package:simf_app/features/live/widgets/need_login_state.dart';
import 'package:simf_app/features/sessions/data/rate_prompt_tracker.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Page 025 — البث المباشر · Live broadcast (#25, `/live?sessionId=`), rebuilt
/// to the KSA-Project Figma frame **934:3450** on the shared navy shell.
///
/// **Login-only** (owner, D-577): a signed-out guest sees an in-screen "need
/// login" prompt with a Sign-in button instead of the player, from any entry
/// point (supersedes the D-199 "public, anonymous" design). This route is NOT
/// router-redirect-gated (unlike sessions/detail under D-576) — the gate is
/// in-screen, so the guest still lands here. A signed-in user takes an optional
/// [sessionId] from the query string. With no id it shows a "pick a session"
/// empty state and never fetches. With an id it reads the broadcast slice (`GET
/// /app/programme/sessions/{id}`, `AllowAnonymous`) and branches three ways
/// (Page_025 L-3):
/// * `liveStreamUrl` non-empty → play the feed and show a LIVE badge; when a
///   `liveSignLanguageUrl` also exists a toggle swaps the player between the
///   main feed and the sign-language feed (Page_025 L-3);
/// * `liveStreamUrl` null but `hasRecording` → a "recording available" note;
/// * neither → a "not live / scheduled" state. 404 → not-found; any other
///   failure → retry.
///
/// **Frame mapping (934:3450):** the navy header (circled back chevron +
/// centred title), a **black player surface** carrying the LIVE badge + the
/// gold-bordered organiser caption strip, then the **"يُبث الآن"
/// now-broadcasting** block (the session title as a gold bullet) and the
/// **ask-a-question** entry to Page 026 (`/live/question`). The player surface
/// + its media engine live in `widgets/live_player_surface.dart` +
///   `live_video_player.dart` + `live_badges.dart`; the non-live black bands in
///   `live_message_surfaces.dart`; the info column widgets in
///   `live_content.dart`.
///
/// **FR-702 (owner 2026-07-31):** when the session carries a live notice it is
/// rendered as a calm informational banner ABOVE the player. It is a
/// notification only — nothing here checks where the viewer is and nothing
/// withholds the stream.
///
/// **Provider (D-349):** the live-video provider is **YouTube** (POC). Each
/// feed URL is sniffed by `YoutubeUrl`: a YouTube link plays via the IFrame
/// player, anything else (HLS/MP4) via `video_player`. The player widget owns
/// its own controller lifecycle, so swapping the active URL just rebuilds it.
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
  /// approved attendee who actually had a live feed to watch. Captured in
  /// [_load] so [dispose] never reads a provider from a dead element. Shares
  /// the D-690 tracker + the `Session` rating code, so watching online then
  /// leaving the (ended) session detail can't double-prompt.
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
    // maybeOf (not of): a bare-MaterialApp test host with no GoRouter leaves
    // this null, and the leave-prompt simply does not fire.
    _router = GoRouter.maybeOf(context);
  }

  @override
  void dispose() {
    _maybePromptRateAfterWatch();
    super.dispose();
  }

  /// D-712 (FDS-007 §C.4 GAP-B, owner item 8) — "online session, live-stream
  /// close → rate the online session". When an approved attendee leaves the
  /// live screen for a session that carried a live feed, open the dynamic rate
  /// screen for it once. Runs from [dispose] (the reliable "left the screen"
  /// signal for every exit path) and pushes through the captured [GoRouter] on
  /// the next frame. Forward navigations (ask-a-question, sign-in) keep this
  /// screen alive, so they do not fire it; the shared tracker dedups it with
  /// the D-690 after-view prompt so a session is rated at most once.
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
      // (a pending account presents as guest via effectiveAppRole and is
      // excluded) who actually had a live feed to watch. Captured here so
      // [dispose] reuses the reference instead of reading a provider from a
      // dead element.
      final auth = ref.read(authControllerProvider);
      final isApprovedAttendee = auth is AuthStateSignedIn &&
          isAttendeeRole(auth.session.user.effectiveAppRole);
      // 2026-07-22 — respect the CP: no after-watch prompt when the "Session"
      // rating type is deactivated in RatingConfig
      // (siteSettings.sessionRatingEnabled). Fail-open (true) while the cached
      // settings load / on error, matching the server, which also suppresses
      // the notification when the type is off.
      final sessionRatingEnabled =
          ref.read(siteSettingsProvider).valueOrNull?.sessionRatingEnabled ??
              true;
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

  /// Owner rule: every data page pulls to refresh. Re-reads the session and the
  /// upcoming strip together; the org profile backs the id-less global feed, so
  /// it is warmed too.
  Future<void> _refresh() async {
    await Future.wait<void>(<Future<void>>[
      if (_hasId) _load(),
      _loadUpcoming(),
      ref.read(orgProfileProvider.notifier).warm(),
    ]);
  }

  void _askQuestion() {
    unawaited(
      context.pushNamed(
        RouteNames.sendQuestion,
        queryParameters: <String, String>{
          RouteParams.sessionId: widget.sessionId!.trim(),
        },
      ),
    );
  }

  /// D-495 — a synthetic live session for the forum's main (global) live
  /// stream, used when the screen opens without a specific session id. The
  /// title is the forum name; the feed is the Organization profile's
  /// liveStreamUrl.
  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.liveBroadcastTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: SimfPullToRefresh(
        onRefresh: _refresh,
        child: _buildBody(l10n),
      ),
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
      // from the Organization profile when the admin has configured one; else
      // the "pick a session" empty state. When a liveUrl param is provided
      // (e.g. from the home LiveBanner tap), use it directly without hitting
      // the API or the org profile.
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
        return _content(l10n, globalLiveSession(profile, globalUrl));
      }
      return SimfPullableHost(
        child: SimfEmptyState(
          icon: Icons.live_tv_outlined,
          message: l10n.liveNoSessionSelected,
        ),
      );
    }
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_notFound) {
      return SimfPullableHost(
        child: SimfEmptyState(
          icon: Icons.live_tv_outlined,
          message: l10n.sessionNotFound,
        ),
      );
    }
    if (_error || _session == null) {
      return SimfPullableHost(
        child: SimfErrorState(
          message: l10n.liveBroadcastError,
          retryLabel: l10n.retryLabel,
          onRetry: () => unawaited(_load()),
        ),
      );
    }
    return _content(l10n, _session!);
  }

  Widget _content(AppL10n l10n, LiveSession session) => LiveContentView(
        l10n: l10n,
        session: session,
        upcoming: _upcoming,
        showSignLanguage: _showSignLanguage,
        hasId: _hasId,
        onSignLanguageChanged: (value) =>
            setState(() => _showSignLanguage = value),
        onAskQuestion: _askQuestion,
      );
}
