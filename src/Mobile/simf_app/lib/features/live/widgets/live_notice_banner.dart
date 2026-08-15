import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

/// FR-702 — the session's informational live notice, shown above the player: an
/// info glyph plus the organiser's text on the shared navy card. Calm by
/// construction — it reuses [SimfPageNote] (the muted "what is this" line) and
/// the plain [SimfCard] chrome, so it never reads as an error, and it neither
/// gates nor delays the feed. The caller renders it only when the notice is
/// set, so there is no empty-banner state.
class LiveNoticeBanner extends StatelessWidget {
  const LiveNoticeBanner({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      // The banner sits ABOVE the full-bleed player band, and the list around
      // it has no padding of its own, so it carries all four insets itself. The
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
