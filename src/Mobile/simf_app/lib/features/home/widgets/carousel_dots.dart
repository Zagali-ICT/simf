import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/motion/motion_durations.dart';

/// The carousel position dots — the active one is a wider gold pill, the rest
/// are faint beige. Shared by the home highlights carousel and the home hero
/// banner (both auto-advance a PageView).
class CarouselDots extends StatelessWidget {
  const CarouselDots({required this.count, required this.index, super.key});

  final int count;
  final int index;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: List<Widget>.generate(count, (i) {
        final active = i == index;
        return AnimatedContainer(
          duration: MotionDurations.dotFade,
          margin: const EdgeInsets.symmetric(horizontal: 3),
          width: active ? 16 : 6,
          height: SimfTokens.carouselDotsHeight,
          decoration: BoxDecoration(
            color: active
                ? SimfTokens.accent
                : SimfTokens.beigeBorder.withValues(alpha: 0.4),
            borderRadius: BorderRadius.circular(SimfTokens.radiusDot),
          ),
        );
      }),
    );
  }
}
