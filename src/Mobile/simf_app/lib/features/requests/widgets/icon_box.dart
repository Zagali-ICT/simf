import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The gold rounded type-icon box at the inline start of a card (Figma
/// 1408:9783 — 32px, radius-4, a 16px navy glyph).
class IconBox extends StatelessWidget {
  const IconBox({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.requestIconBox,
      height: SimfTokens.requestIconBox,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Icon(icon, size: 16, color: SimfTokens.navy),
    );
  }
}
