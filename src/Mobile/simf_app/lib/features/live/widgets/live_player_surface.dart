import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/theme/tokens.dart';
import '../../accessibility/data/accessibility_controller.dart';
import 'live_badges.dart';
import 'live_video_player.dart';

/// The black live player surface (frame 934:3614): the player fills a 16:9 box
/// over a black backdrop, with the LIVE badge + language chip in the top row and
/// the gold-bordered organiser caption strip below the feed.
class LivePlayerSurface extends ConsumerStatefulWidget {
  const LivePlayerSurface({
    required this.url,
    required this.captionHint,
    this.liveLabel,
    this.caption,
    super.key,
  });

  final String url;

  /// S-3 — the "LIVE" badge label, or null to HIDE the badge when the session is
  /// not inside its scheduled window (a feed URL may exist before start / after
  /// end without the session being live).
  final String? liveLabel;

  /// P5 — D-439: the admin-typed caption text for this session, or null.
  final String? caption;
  final String captionHint;

  @override
  ConsumerState<LivePlayerSurface> createState() => _LivePlayerSurfaceState();
}

class _LivePlayerSurfaceState extends ConsumerState<LivePlayerSurface> {
  // D-726 (#27) — the watch keep-alive moved INTO the wrapped
  // [LiveVideoPlayer] so it also covers the summary recording / summary-video
  // surfaces that use that player directly (bypassing this surface); this
  // surface no longer owns a timer.

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
                if (widget.liveLabel != null)
                  LiveBadge(label: widget.liveLabel!),
                const Spacer(),
                const LanguageChip(),
              ],
            ),
            const SizedBox(height: SimfTokens.space4),
            LiveVideoPlayer(url: widget.url),
            const SizedBox(height: SimfTokens.space4),
            _CaptionStrip(caption: widget.caption, hint: widget.captionHint),
          ],
        ),
      ),
    );
  }
}

/// The gold-bordered organiser caption strip under the player (frame 934:3613).
/// P5 — D-439: when the session carries admin-typed [caption] text it is shown
/// in readable white; otherwise the muted placeholder [hint] is shown (and for a
/// YouTube feed the player's own CC supplies captions meanwhile).
///
/// A15 (2026-07-26) — the strip used to carry a gold "AI" badge next to copy
/// promising live translation of the spoken audio. It renders a STATIC
/// admin-typed string (`Session.LiveCaptions`) that never changes during the
/// broadcast, so the AI branding and the live-translation promise were a false
/// capability claim and are gone. Real speech-to-text + streaming translation
/// is a feature, not a defect fix (see the dead
/// `/app/ai/live-translation/chunk` endpoint) — it is an owner decision, not
/// something this surface fakes.
class _CaptionStrip extends ConsumerWidget {
  const _CaptionStrip({required this.hint, this.caption});

  /// The organiser's caption text, or null to show the placeholder [hint].
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
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Text(
        caption ?? hint,
        textAlign: TextAlign.start,
        style: TextStyle(
          // A real caption reads in white; the placeholder is the frame's soft
          // caption colour (#DDE4F0, 934:3613).
          color: hasCaption ? SimfTokens.surface : SimfTokens.captionText,
          fontSize: SimfTokens.textSm,
        ),
      ),
    );
  }
}
