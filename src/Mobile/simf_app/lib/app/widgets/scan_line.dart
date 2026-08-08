import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The horizontal glowing gold scan line (Figma 758:4735).
class ScanLine extends StatelessWidget {
  const ScanLine({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      height: SimfTokens.scanLineHeight,
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            SimfTokens.accentFade,
            SimfTokens.accent,
            SimfTokens.accentFade,
          ],
        ),
        boxShadow: <BoxShadow>[
          BoxShadow(color: SimfTokens.accent, blurRadius: SimfTokens.scanLineBlurRadius),
        ],
      ),
    );
  }
}
