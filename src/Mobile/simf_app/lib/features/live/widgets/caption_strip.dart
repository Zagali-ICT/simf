import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The gold-bordered organiser caption strip under the player (frame 934:3613).
/// P5 — D-439: shown only when the session carries admin-typed [caption] text.
///
/// It used to fall back to a placeholder reading "caption text written by the
/// organiser appears here", which drew an empty bordered box on every session
/// without a note — a feature that looks broken rather than unused, and no
/// toggle removed it. A strip with nothing to say now says nothing. For a
/// YouTube feed the player's own CC stays available and user-controlled.
class CaptionStrip extends StatelessWidget {
  const CaptionStrip({super.key, this.caption});

  /// The organiser's caption text. Null means the strip does not render.
  final String? caption;

  @override
  Widget build(BuildContext context) {
    // The Page-038 captions flag is no longer read. Its switch is withdrawn,
    // so a stored `false` would hide every organiser caption with nothing left
    // to turn it back on. The YouTube player's own CC stays user-controlled.
    final text = caption;
    if (text == null) {
      return const SizedBox.shrink();
    }
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
        text,
        textAlign: TextAlign.start,
        style: const TextStyle(
          color: SimfTokens.surface,
          fontSize: SimfTokens.textSm,
        ),
      ),
    );
  }
}
