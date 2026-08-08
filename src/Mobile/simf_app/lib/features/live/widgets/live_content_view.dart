import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/live/data/live_models.dart';
import 'package:simf_app/features/live/data/live_presentation.dart';
import 'package:simf_app/features/live/widgets/live_content.dart';
import 'package:simf_app/features/live/widgets/live_message_surfaces.dart';
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

  /// Whether the sign-language feed is the one being played.
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
    final hasBothFeeds = mainUrl != null && signUrl != null;
    // S-3 — "live" is the session being INSIDE its scheduled window, not merely
    // "a feed URL is present" (an admin may set the URL before start or leave it
    // after end). This drives the LIVE badge so it never lies before start /
    // after end (the backend closes questions at End). When the window is
    // unknown — the global main-live synthetic (no id) has no start/end — fall
    // back to "a feed is present" so the always-on forum stream still reads live.
    final start = session.start;
    final end = session.end;
    final nowUtc = saudiNow();
    final isLive = (start != null && end != null)
        ? !nowUtc.isBefore(start) && nowUtc.isBefore(end)
        : mainUrl != null;
    // S-3 honesty — the "يُبث الآن" header and the Ask affordance must never
    // render over a not-live / recording surface, so they also require the feed
    // to actually be up. The LIVE badge already only shows when a URL exists.
    final isBroadcasting = isLive && mainUrl != null;
    // When the main feed is present, the active feed is the sign-language one
    // only while the toggle is on AND a sign feed exists; otherwise the main feed.
    final activeUrl = (showSignLanguage && signUrl != null) ? signUrl : mainUrl;
    // FR-702 (owner 2026-07-31) — the organiser's informational notice for this
    // broadcast. Null when the CP left both languages blank, and then nothing is
    // rendered (no empty banner, no reserved space).
    final notice = session.localizedNotice(isArabic);

    return ListView(
      padding: EdgeInsets.zero,
      physics: const AlwaysScrollableScrollPhysics(),
      children: <Widget>[
        // FR-702 — the notice sits ABOVE the player and is purely informational:
        // it never gates, delays or replaces the feed (owner: "no restriction,
        // this is only notification").
        //
        // Shown only when there IS a feed. The branches below are the recording
        // and not-live surfaces, and a notice about the broadcast printed above
        // "this session is not being streamed" contradicts it — which is exactly
        // what a notice left behind on a session whose feed was later cleared
        // would do.
        if (notice != null && mainUrl != null) LiveNoticeBanner(text: notice),

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
                  showSignLanguage: showSignLanguage,
                  mainLabel: l10n.liveFeedMain,
                  signLabel: l10n.liveFeedSignLanguage,
                  onChanged: (value) =>
                      onSignLanguageChanged(value),
                ),
                const SizedBox(height: SimfTokens.space5),
              ],

              // "يُبث الآن" now-broadcasting block (frame 934:3615 / 934:3616):
              // the section label over the session title as a gold bullet.
              Text(
                // D-433 — the hall name (already on the wire) completes the
                // frame's "يُبث الآن · القاعة الرئيسية" header line.
                broadcastLabel(
                  l10n,
                  isLive: isBroadcasting,
                  hall: session.localizedHall(isArabic),
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

              // A20 (2026-07-26) — the gold "available only inside the Riyadh
              // region per regulations" card (frame 934:3619) is gone. Nothing
              // anywhere checked the viewer's location, so every viewer was told
              // about a restriction that does not exist. FR-702 was settled by
              // the owner (2026-07-31) as "no restriction, this is only
              // notification": the CP-authored notice now renders as the
              // informational banner above the player, and no viewer is ever
              // geo-checked or blocked.

              // Ask-a-question entry → Page 026 (the frame's L-3 Q&A affordance).
              // Session-specific — only for a real session, not the global main-live.
              // S-3 (owner) — only while the session is actually broadcasting (now
              // within its [start, end] window AND a feed is up): before start the
              // ask lives on the detail screen, and after end the backend closes
              // questions (the view is a YouTube archive, not a live broadcast).
              if (hasId && isBroadcasting) ...<Widget>[
                const SizedBox(height: SimfTokens.space6),
                AskQuestionButton(
                  label: l10n.liveAskQuestion,
                  onTap: onAskQuestion,
                ),
              ],

              // D-433 — "الجلسات القادمة" upcoming-sessions cards (frame
              // 934:3621/3630), from the shipped agenda list (non-blocking read).
              if (upcoming.isNotEmpty) ...<Widget>[
                const SizedBox(height: SimfTokens.space6),
                Text(
                  l10n.liveUpcomingSessions,
                  textAlign: TextAlign.start,
                  style: SimfTokens.labelWhiteMediumLg,
                ),
                const SizedBox(height: SimfTokens.space4),
                for (final upcoming in upcoming) ...<Widget>[
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
