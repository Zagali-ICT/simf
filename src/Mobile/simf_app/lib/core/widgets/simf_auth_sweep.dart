import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';

/// The decorative diagonal sweep behind the auth screens' header (Figma node
/// 168:2850, rotated 28.28°) — approximated as a tinted rounded rectangle.
///
/// Shared so the auth entry screens (sign-in / sign-up / forgot / reset …) all
/// paint the exact same shape from one place instead of each copying the
/// `Positioned` + `Transform.rotate` block.
class SimfAuthSweep extends StatelessWidget {
  const SimfAuthSweep({super.key});

  @override
  Widget build(BuildContext context) {
    return Positioned(
      top: -156,
      left: 60,
      child: Transform.rotate(
        angle: 0.4936, // 28.28°
        child: Container(
          width: 313,
          height: 323,
          decoration: BoxDecoration(
            color: SimfTokens.surfaceTint,
            borderRadius: BorderRadius.circular(40),
          ),
        ),
      ),
    );
  }
}
