import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The decorative diagonal sweep behind the registration-success page (Figma
/// 505:1453, top-right area). A [Positioned], so it belongs directly under the
/// page's [Stack].
class RegistrationSuccessSweep extends StatelessWidget {
  const RegistrationSuccessSweep({super.key});

  @override
  Widget build(BuildContext context) {
    return Positioned(
      top: -180,
      right: -40,
      child: Transform.rotate(
        angle: 0.4936, // 28.28°
        child: Container(
          width: SimfTokens.sweepBlockWidth,
          height: SimfTokens.sweepBlockHeight,
          decoration: BoxDecoration(
            color: SimfTokens.surfaceTint,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSheet),
          ),
        ),
      ),
    );
  }
}
