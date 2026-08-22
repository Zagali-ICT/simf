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
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/live/data/live_models.dart';
import 'package:simf_app/features/live/data/live_presentation.dart';
import 'package:simf_app/features/live/data/live_repository.dart';
import 'package:simf_app/features/live/widgets/live_broadcast_body.dart';
import 'package:simf_app/features/sessions/data/rate_prompt_tracker.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Live broadcast — route: `RouteNames.liveBroadcast` · Figma 934:3450
/// Contract: login-only (owner, D-577) — a signed-out guest gets the
/// in-screen need-login prompt instead of the player, from every entry point.
/// With no session id the forum-wide feed comes from the Organization profile
/// (D-495); the video provider is YouTube (D-349); and FR-702's notice is
/// informational only — nothing here geo-checks or withholds the stream.
class LiveBroadcastScreen extends ConsumerStatefulWidget {
  const LiveBroadcastScreen({this.sessionId, super.key});

  final String? sessionId;

  @override
  ConsumerState<LiveBroadcastScreen> createState() =>
      _LiveBroadcastScreenState();
}

class _LiveBroadcastScreenState extends ConsumerState<LiveBroadcastScreen> {
  /// When true the player shows the sign-language feed instead of the main one.
  /// Only meaningful when the session carries both feeds (the toggle is hidden
  /// otherwise).
  bool _showSignLanguage = false;

  /// The router captured in [didChangeDependencies] so [dispose] can push the
  /// after-watch rate prompt (D-712) after this element is gone.
  GoRouter? _router;

  /// Non-null only when this view is eligible to prompt on leave — a signed-in
  /// approved attendee who actually had a live feed to watch. Captured in
  /// `_captureRateEligibility` so [dispose] never reads a provider from a dead
  /// element. Shares the D-690 tracker + the `Session` rating code, so watching
  /// online then leaving the (ended) session detail can't double-prompt.
  SessionRatePromptTracker? _rateTracker;

  /// The trimmed session id, or null when this view is the id-less global feed.
  String? get _id {
    final id = widget.sessionId?.trim();
    return (id == null || id.isEmpty) ? null : id;
  }

  bool get _hasId => _id != null;

  @override
  void initState() {
    super.initState();
    // Login-gate (owner): a signed-out guest sees the need-login prompt instead
    // of the stream, so don't fetch the session for them.
    final isSignedIn = ref.read(authControllerProvider) is AuthStateSignedIn;
    if (isSignedIn && _hasId) {
      unawaited(_captureRateEligibility());
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

  /// D-712 — capture the after-watch rate eligibility once the session has
  /// loaded: an approved attendee (a pending account presents as guest via
  /// `effectiveAppRole` and is excluded) who actually had a live feed to watch.
  ///
  /// Awaits the provider's FIRST future rather than listening, for the reason
  /// the tracker exists: the reference must be captured while this element is
  /// alive so [dispose] reuses it instead of reading a provider from a dead
  /// element. Listening would also re-run this on every pull-to-refresh.
  Future<void> _captureRateEligibility() async {
    final LiveSession? session;
    try {
      session = await ref.read(liveSessionProvider(_id!).future);
    } on Object {
      return; // The error branch renders; there was nothing to watch.
    }
    if (!mounted || session == null) {
      return;
    }
    final auth = ref.read(authControllerProvider);
    final isApprovedAttendee = auth is AuthStateSignedIn &&
        isAttendeeRole(auth.session.user.effectiveAppRole);
    // 2026-07-22 — respect the CP: no after-watch prompt when the "Session"
    // rating type is deactivated in RatingConfig
    // (siteSettings.sessionRatingEnabled). Fail-open (true) while the cached
    // settings load / on error, matching the server, which also suppresses
    // the notification when the type is off.
    final sessionRatingEnabled =
        ref.read(siteSettingsProvider).value?.sessionRatingEnabled ??
            true;
    _rateTracker = isApprovedAttendee &&
            session.liveStreamUrl != null &&
            sessionRatingEnabled
        ? ref.read(sessionRatePromptTrackerProvider)
        : null;
  }

  /// Owner rule: every data page pulls to refresh. Re-reads the session and the
  /// upcoming strip together; the org profile backs the id-less global feed, so
  /// it is warmed too.
  Future<void> _refresh() async {
    final id = _id;
    await Future.wait<void>(<Future<void>>[
      if (id != null) refreshAsync(ref, liveSessionProvider(id).future),
      refreshAsync(ref, upcomingSessionsProvider(id).future),
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.liveBroadcastTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: SimfPullToRefresh(
        onRefresh: _refresh,
        child: LiveBroadcastBody(
          sessionId: _id,
          showSignLanguage: _showSignLanguage,
          onSignLanguageChanged: (value) =>
              setState(() => _showSignLanguage = value),
          onAskQuestion: _askQuestion,
        ),
      ),
    );
  }
}
