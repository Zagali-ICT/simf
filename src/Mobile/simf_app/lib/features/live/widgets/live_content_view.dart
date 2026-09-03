import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/live/data/live_models.dart';
import 'package:simf_app/features/live/widgets/live_broadcast_details.dart';
import 'package:simf_app/features/live/widgets/live_message_surfaces.dart';
import 'package:simf_app/features/live/widgets/live_notice_banner.dart';
import 'package:simf_app/features/live/widgets/live_player_surface.dart';

/// The live screen's body once a session is resolved: the player band, the
/// broadcast header, the Q&A entry and the upcoming-sessions list.
///
/// Extracted from the screen's State, where it was a 165-line `_content`
/// method. The four things it actually needed are now constructor parameters,
/// so the screen keeps the state and this keeps the layout.
class LiveContentView extends StatelessWidget {
  const LiveContentView({
    required this.l10n,
    required this.session,
    required this.upcoming,
    required this.showSignLanguage,
    required this.hasId,
    required this.onSignLanguageChanged,
    required this.onAskQuestion,
    super.key,
  });

  final AppL10n l10n;
  final LiveSession session;
  final List<UpcomingSession> upcoming;

  final bool showSignLanguage;

  /// False for the forum-wide synthetic session, which has no id and therefore
  /// no Q&A.
  final bool hasId;

  final ValueChanged<bool> onSignLanguageChanged;
  final VoidCallback onAskQuestion;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final mainUrl = session.liveStreamUrl;
    final signUrl = session.liveSignLanguageUrl;
    // S-3 — "live" is the session being INSIDE its scheduled window, not merely
    // "a feed URL is present" (an admin may set the URL before start or leave
    // it after end). This drives the LIVE badge so it never lies before start /
    // after end (the backend closes questions at End). When the window is
    // unknown — the global main-live synthetic (no id) has no start/end — fall
    // back to "a feed is present" so the always-on forum stream still reads
    // live.
    final isLive = session.isLiveAt(saudiNow(), hasFeed: mainUrl != null);
    // When the main feed is present, the active feed is the sign-language one
    // only while the toggle is on AND a sign feed exists; otherwise the main
    // feed.
    final activeUrl = (showSignLanguage && signUrl != null) ? signUrl : mainUrl;
    // FR-702 (owner 2026-07-31) — the organiser's informational notice for this
    // broadcast. Null when the CP left both languages blank, and then nothing
    // is rendered (no empty banner, no reserved space).
    final notice = session.localizedNotice(isArabic: isArabic);

    return ListView(
      padding: EdgeInsets.zero,
      physics: const AlwaysScrollableScrollPhysics(),
      children: <Widget>[
        // FR-702 — the notice sits ABOVE the player and is purely
        // informational: it never gates, delays or replaces the feed (owner:
        // "no restriction, this is only notification").
        //
        // Shown only when there IS a feed. The branches below are the recording
        // and not-live surfaces, and a notice about the broadcast printed above
        // "this session is not being streamed" contradicts it — which is
        // exactly what a notice left behind on a session whose feed was later
        // cleared would do.
        if (notice != null && mainUrl != null) LiveNoticeBanner(text: notice),

        // The black player surface (frame 934:3614) — full-bleed, edge to edge.
        if (mainUrl != null)
          LivePlayerSurface(
            // Keyed by the active URL so swapping the feed disposes the old
            // controller and builds a fresh player for the new one.
            key: ValueKey<String>(activeUrl!),
            url: activeUrl,
            // S-3 — the LIVE badge only when the session is genuinely
            // in-window; null (badge hidden) for a pre-start premiere /
            // post-end archive URL.
            liveLabel: isLive ? l10n.liveNowLabel : null,
            // P5 — D-439: the admin-set AI caption when present, else the
            // placeholder hint (YouTube CC supplies captions meanwhile).
            caption: session.localizedCaption(isArabic: isArabic),
          )
        else if (session.hasRecording)
          RecordingSurface(message: l10n.liveRecordingAvailable)
        else
          NotLiveSurface(message: l10n.liveNotLiveYet),

        LiveBroadcastDetails(
          l10n: l10n,
          session: session,
          upcoming: upcoming,
          showSignLanguage: showSignLanguage,
          hasBothFeeds: mainUrl != null && signUrl != null,
          signLanguageOnly: signUrl != null && mainUrl == null,
          isBroadcasting: isLive && mainUrl != null,
          hasId: hasId,
          onSignLanguageChanged: onSignLanguageChanged,
          onAskQuestion: onAskQuestion,
        ),
      ],
    );
  }
}
