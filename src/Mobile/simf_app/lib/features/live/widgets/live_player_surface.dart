import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/theme/tokens.dart';
import '../../accessibility/data/accessibility_controller.dart';
import 'live_badges.dart';
import 'live_video_player.dart';

/// The black live player surface (frame 934:3614): the player fills a 16:9 box
/// over a black backdrop, with the LIVE badge + language chip in the top row and
/// the gold-bordered AI live-caption strip below the feed.
class LivePlayerSurface extends StatelessWidget {
  const LivePlayerSurface({
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
                LiveBadge(label: liveLabel),
                const Spacer(),
                const LanguageChip(),
              ],
            ),
            const SizedBox(height: SimfTokens.space4),
            LiveVideoPlayer(url: url),
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
              textAlign: TextAlign.start,
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
