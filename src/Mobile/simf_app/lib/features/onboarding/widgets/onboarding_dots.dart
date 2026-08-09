import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/motion/motion_durations.dart';

/// The design's pill page dots — active 32×8 beige, inactive 16×8 soft gold.
/// Forced LTR so the active dot travels left → right exactly as in the
/// frames (which keep that progression even in the RTL design).
class OnboardingDots extends StatelessWidget {
  const OnboardingDots({
    required this.count,
    required this.activeIndex,
    super.key,
  });

  final int count;
  final int activeIndex;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      textDirection: TextDirection.ltr,
      children: <Widget>[
        for (int i = 0; i < count; i++)
          AnimatedContainer(
            duration: MotionDurations.onboardingDotFade,
            margin: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
            width: i == activeIndex ? 32 : 16,
            height: SimfTokens.space2,
            decoration: BoxDecoration(
              color: i == activeIndex
                  ? SimfTokens.beigeBorder
                  : SimfTokens.goldSoftFill50,
              borderRadius: BorderRadius.circular(SimfTokens.radiusPill),
            ),
          ),
      ],
    );
  }
}
