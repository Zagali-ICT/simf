import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../data/live_models.dart';
import 'toggle_pill.dart';
import 'time_chip.dart';

/// Login-gate state (owner, D-577): the live stream is login-only, so a
/// signed-out guest sees this prompt — an icon, a message, and a gold Sign-in
/// button that routes to sign-in — instead of the player, from any entry point.
class NeedLoginState extends StatelessWidget {
  const NeedLoginState({
    required this.message,
    required this.signInLabel,
    required this.onSignIn,
    super.key,
  });

  final String message;
  final String signInLabel;
  final VoidCallback onSignIn;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.lock_outline,
              size: 56,
              color: SimfTokens.beigeBorder,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: SimfTokens.hintBeige,
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(
              onPressed: onSignIn,
              style: FilledButton.styleFrom(
                backgroundColor: SimfTokens.accent,
                foregroundColor: SimfTokens.surface,
              ),
              child: Text(signInLabel),
            ),
          ],
        ),
      ),
    );
  }
}

/// Swaps the player between the main feed and the sign-language feed (Page_025
/// L-3). Shown only when the session carries both.
class FeedToggle extends StatelessWidget {
  const FeedToggle({
    required this.showSignLanguage,
    required this.mainLabel,
    required this.signLabel,
    required this.onChanged,
    super.key,
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
            child: TogglePill(
              label: mainLabel,
              active: !showSignLanguage,
              onTap: () => onChanged(false),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: TogglePill(
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

/// A gold (or beige) right-aligned bulleted line — the frame's "·"-led list
/// items (the session title 934:3616, the speakers line 934:3617).
class GoldBullet extends StatelessWidget {
  const GoldBullet({
    required this.text,
    required this.color,
    this.fontWeight = FontWeight.w500,
    this.fontSize = SimfTokens.textMd,
    super.key,
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
            textAlign: TextAlign.start,
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
          padding: const EdgeInsets.only(top: SimfTokens.gap6),
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

/// A sign-language-available note — a gold icon + muted label (shown when a sign
/// feed is announced with no main feed to toggle into).
class SignLanguageNote extends StatelessWidget {
  const SignLanguageNote({required this.label, super.key});

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
            style: SimfTokens.labelBeigeSm,
          ),
        ),
      ],
    );
  }
}

// A20 (2026-07-26) — `RegionNoticeCard` (frame 934:3619, the gold "available
// only inside the Riyadh region per regulations" card) is deleted with its only
// caller: nothing in the app, API or CP ever checked the viewer's location, so
// the notice was an unconditional false claim. Geo-fencing the stream for real
// is a product/legal decision, not a defect fix.
//
// FR-702 (owner 2026-07-31) — that product decision was taken, and it is "no
// restriction, this is only notification": [LiveNoticeBanner] below replaces the
// hard-coded region claim with the free-text notice the admin authors per
// session in the Control Panel.

/// FR-702 — the session's informational live notice, shown above the player: an
/// info glyph plus the organiser's text on the shared navy card. Calm by
/// construction — it reuses [SimfPageNote] (the muted "what is this" line) and
/// the plain [SimfCard] chrome, so it never reads as an error, and it neither
/// gates nor delays the feed. The caller renders it only when the notice is set,
/// so there is no empty-banner state.
class LiveNoticeBanner extends StatelessWidget {
  const LiveNoticeBanner({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      // The banner sits ABOVE the full-bleed player band, and the list around it
      // has no padding of its own, so it carries all four insets itself. The
      // BOTTOM one is load-bearing: without it the card's beige hairline sits
      // flush against the black player with no separation.
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: SimfCard(
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space3),
          child: SimfPageNote(text: text),
        ),
      ),
    );
  }
}

/// The ask-a-question entry (frame's L-3 Q&A affordance) → Page 026
/// (`/live/question?sessionId=`). A full-width gold action button.
class AskQuestionButton extends StatelessWidget {
  const AskQuestionButton({required this.label, required this.onTap, super.key});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: SimfTokens.controlHeight,
      child: FilledButton.icon(
        onPressed: onTap,
        icon: const Icon(Icons.help_outline, size: 18),
        // The size/weight ride the label Text (not styleFrom.textStyle) so the
        // Arabic label keeps the theme's brand font — an inline
        // `styleFrom.textStyle` drops fontFamily and tofus the Arabic (the
        // recurring button-font bug, D-546/D-549).
        label: Text(
          label,
          style: SimfTokens.titleBold,
        ),
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.accent,
          foregroundColor: SimfTokens.surface,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
        ),
      ),
    );
  }
}

/// One "الجلسات القادمة" card (frame 934:3621/3630): the session title with a
/// gold HH:mm time chip at the inline-end.
class UpcomingCard extends StatelessWidget {
  const UpcomingCard({required this.session, required this.isArabic, super.key});

  final UpcomingSession session;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
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
                textAlign: TextAlign.start,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                // Frame 934:3626 — the upcoming-session title is 14px Bold.
                style: SimfTokens.labelWhiteBoldMd,
              ),
            ),
            const SizedBox(width: SimfTokens.space3),
            TimeChip(time: session.start),
          ],
        ),
      ),
    );
  }
}

