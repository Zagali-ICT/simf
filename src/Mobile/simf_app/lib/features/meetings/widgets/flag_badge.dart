import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The 48×48 nationality flag badge (Figma 1408:9726): the flag emoji on a soft
/// green well.
class FlagBadge extends StatelessWidget {
  const FlagBadge({required this.flag});

  final String flag;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.flagBadgeWidth,
      height: SimfTokens.flagBadgeHeight,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.flagBadgeBg,
        borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
        border: Border.all(color: SimfTokens.flagBadgeBorder),
      ),
      child: Text(
        flag,
        textDirection: TextDirection.ltr,
        // The frame's 28px flag glyph (textHero token == 28).
        style: const TextStyle(fontSize: SimfTokens.textHero, height: 1),
      ),
    );
  }
}
