import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/live/data/live_models.dart';
import 'package:simf_app/features/live/data/live_presentation.dart';
import 'package:simf_app/features/live/data/live_repository.dart';
import 'package:simf_app/features/live/widgets/live_content_view.dart';
import 'package:simf_app/features/live/widgets/need_login_state.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// The live screen's body: the login gate, the id-less global-feed branch and
/// the per-session read, each resolving to a [LiveContentView] or to one of the
/// empty / error surfaces.
///
/// D-495 — a synthetic live session stands in for the forum's main (global)
/// live stream when the screen opens without a specific session id. The title
/// is the forum name; the feed is the Organization profile's liveStreamUrl.
class LiveBroadcastBody extends ConsumerWidget {
  const LiveBroadcastBody({
    required this.sessionId,
    required this.liveUrl,
    required this.showSignLanguage,
    required this.onSignLanguageChanged,
    required this.onAskQuestion,
    super.key,
  });

  /// The trimmed session id, or null when this view is the id-less global feed.
  final String? sessionId;

  /// A feed URL handed straight in (the home LiveBanner tap), played without
  /// hitting the API or the org profile.
  final String? liveUrl;

  final bool showSignLanguage;
  final ValueChanged<bool> onSignLanguageChanged;
  final VoidCallback onAskQuestion;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
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
    if (sessionId == null) {
      // D-495 — no session id → play the forum's main (global) live-stream link
      // from the Organization profile when the admin has configured one; else
      // the "pick a session" empty state. When a liveUrl param is provided
      // (e.g. from the home LiveBanner tap), use it directly without hitting
      // the API or the org profile.
      final explicitUrl = liveUrl?.trim();
      if (explicitUrl != null && explicitUrl.isNotEmpty) {
        return _content(
          ref,
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
        return _content(ref, l10n, globalLiveSession(profile, globalUrl));
      }
      return SimfPullableHost(
        child: SimfEmptyState(
          icon: Icons.live_tv_outlined,
          message: l10n.liveNoSessionSelected,
        ),
      );
    }
    // Reached only past the login gate and the id-less global-feed branch
    // above, which is what keeps a guest and the global feed from ever
    // starting this request.
    final id = sessionId!;
    return ref.watch(liveSessionProvider(id)).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => SimfPullableHost(
            child: SimfErrorState(
              message: l10n.liveBroadcastError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(liveSessionProvider(id)),
            ),
          ),
          // Null is the not-found state (see [liveSessionProvider]).
          data: (session) => session == null
              ? SimfPullableHost(
                  child: SimfEmptyState(
                    icon: Icons.live_tv_outlined,
                    message: l10n.sessionNotFound,
                  ),
                )
              : _content(ref, l10n, session),
        );
  }

  Widget _content(WidgetRef ref, AppL10n l10n, LiveSession session) =>
      LiveContentView(
        l10n: l10n,
        session: session,
        upcoming: ref.watch(upcomingSessionsProvider(sessionId)).valueOrNull ??
            const <UpcomingSession>[],
        showSignLanguage: showSignLanguage,
        hasId: sessionId != null,
        onSignLanguageChanged: onSignLanguageChanged,
        onAskQuestion: onAskQuestion,
      );
}
