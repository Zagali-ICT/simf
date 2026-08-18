import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/live/data/live_models.dart';
import 'package:simf_app/features/live/data/live_presentation.dart';
import 'package:simf_app/features/live/widgets/ask_question_button.dart';
import 'package:simf_app/features/live/widgets/feed_toggle.dart';
import 'package:simf_app/features/live/widgets/gold_bullet.dart';
import 'package:simf_app/features/live/widgets/sign_language_note.dart';
import 'package:simf_app/features/live/widgets/upcoming_card.dart';

/// The info column under the player band: the feed toggle, the "يُبث الآن"
/// now-broadcasting block (frames 934:3615 / 934:3616 / 934:3617), the Q&A
/// entry and the "الجلسات القادمة" cards.
class LiveBroadcastDetails extends StatelessWidget {
  const LiveBroadcastDetails({
    required this.l10n,
    required this.session,
    required this.upcoming,
    required this.showSignLanguage,
    required this.hasBothFeeds,
    required this.signLanguageOnly,
    required this.isBroadcasting,
    required this.hasId,
    required this.onSignLanguageChanged,
    required this.onAskQuestion,
    super.key,
  });

  final AppL10n l10n;
  final LiveSession session;
  final List<UpcomingSession> upcoming;

  final bool showSignLanguage;

  /// Both a main and a sign-language feed exist, so the toggle has something
  /// to swap between.
  final bool hasBothFeeds;

  /// A sign feed announced with no main feed — nothing to toggle, just the
  /// note.
  final bool signLanguageOnly;

  /// S-3 honesty — the "يُبث الآن" header and the Ask affordance must never
  /// render over a not-live / recording surface, so they require the session to
  /// be in-window AND the feed to actually be up.
  final bool isBroadcasting;

  /// False for the forum-wide synthetic session, which has no id and therefore
  /// no Q&A.
  final bool hasId;

  final ValueChanged<bool> onSignLanguageChanged;
  final VoidCallback onAskQuestion;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;

    return Padding(
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
              onChanged: onSignLanguageChanged,
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
              hall: session.localizedHall(isArabic: isArabic),
            ),
            textAlign: TextAlign.start,
            style: SimfTokens.labelWhiteMediumLg,
          ),
          const SizedBox(height: SimfTokens.space4),
          GoldBullet(
            text: session.localizedTitle(isArabic: isArabic),
            color: SimfTokens.accent,
            fontWeight: FontWeight.w600,
            // Frame 934:3616 — the session title bullet is 16px.
            fontSize: SimfTokens.textLg,
          ),
          // D-433 — the speakers / participants line (frame 934:3617).
          if (session.localizedSpeakers(isArabic: isArabic) !=
              null) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            GoldBullet(
              text: session.localizedSpeakers(isArabic: isArabic)!,
              color: SimfTokens.beigeBorder,
            ),
          ],

          if (signLanguageOnly) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            SignLanguageNote(label: l10n.liveSignLanguageAvailable),
          ],

          // A20 (2026-07-26) — the gold "available only inside the Riyadh
          // region per regulations" card (frame 934:3619) is gone. Nothing
          // anywhere checked the viewer's location, so every viewer was
          // told about a restriction that does not exist. FR-702 was
          // settled by the owner (2026-07-31) as "no restriction, this is
          // only notification": the CP-authored notice now renders as the
          // informational banner above the player, and no viewer is ever
          // geo-checked or blocked.

          // Ask-a-question entry → Page 026 (the frame's L-3 Q&A
          // affordance). Session-specific — only for a real session, not
          // the global main-live. S-3 (owner) — only while the session is
          // actually broadcasting (now within its [start, end] window AND a
          // feed is up): before start the ask lives on the detail screen,
          // and after end the backend closes questions (the view is a
          // YouTube archive, not a live broadcast).
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
    );
  }
}
