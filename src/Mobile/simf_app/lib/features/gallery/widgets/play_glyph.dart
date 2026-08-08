import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The video play glyph (frame node 949:4059): a navy-70% circle with a centred
/// play triangle.
class PlayGlyph extends StatelessWidget {
  const PlayGlyph();

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        width: 52,
        height: 52,
        decoration: const BoxDecoration(
          color: SimfTokens.navyFill70,
          shape: BoxShape.circle,
        ),
        alignment: Alignment.center,
        child: const Icon(
          Icons.play_arrow_rounded,
          size: 30,
          color: SimfTokens.surface,
        ),
      ),
    );
  }
}
