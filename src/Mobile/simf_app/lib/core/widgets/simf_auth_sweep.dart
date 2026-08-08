import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The decorative diagonal sweep behind the auth screens' header (Figma node
/// 168:2850, rotated 28.28°) — approximated as a tinted rounded rectangle.
///
/// Shared so the auth screens (sign-in / sign-up / OTP verify / interests …)
/// all paint the exact same shape from one place instead of each copying the
/// `Positioned` + `Transform.rotate` block. Only the placement varies per
/// frame, so [top] / [left] / [right] pass straight through to the [Positioned];
/// the defaults are the sign-in frame's top-left position, and the OTP frames
/// pass `left: null` with a `right` offset for the mirrored top-right sweep.
class SimfAuthSweep extends StatelessWidget {
  const SimfAuthSweep({
    this.top = -156,
    this.left = 60,
    this.right,
    super.key,
  });

  final double? top;
  final double? left;
  final double? right;

  @override
  Widget build(BuildContext context) {
    return Positioned(
      top: top,
      left: left,
      right: right,
      child: Transform.rotate(
        angle: 0.4936, // 28.28°
        child: Container(
          width: SimfTokens.simfAuthSweepWidth,
          height: SimfTokens.simfAuthSweepHeight,
          decoration: BoxDecoration(
            color: SimfTokens.surfaceTint,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSheet),
          ),
        ),
      ),
    );
  }
}
