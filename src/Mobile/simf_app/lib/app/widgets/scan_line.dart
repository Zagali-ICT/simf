import 'package:flutter/material.dart';
import '../theme/tokens.dart';

/// The horizontal glowing gold scan line (Figma 758:4735).
class ScanLine extends StatelessWidget {
  const ScanLine();

  @override
  Widget build(BuildContext context) {
    return Container(
      height: SimfTokens.scanLineHeight,
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: <Color>[
            SimfTokens.accentFade,
            SimfTokens.accent,
            SimfTokens.accentFade,
          ],
        ),
        boxShadow: const <BoxShadow>[
          BoxShadow(color: SimfTokens.accent, blurRadius: SimfTokens.scanLineBlurRadius),
        ],
      ),
    );
  }
}
