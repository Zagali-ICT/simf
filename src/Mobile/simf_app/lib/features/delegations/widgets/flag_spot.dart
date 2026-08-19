import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The invisible padding that grows each flag's tap target to a
/// comfortable size without moving the painted dot.
const double flagSpotHitPad = 9;

class FlagSpot extends StatelessWidget {
  const FlagSpot({
    required this.flag,
    required this.selected,
    required this.onTap,
    super.key,
  });

  final String flag;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(flagSpotHitPad),
        decoration: selected
            ? BoxDecoration(
                color: SimfTokens.goldFill6,
                border: Border.all(color: SimfTokens.accent),
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              )
            : null,
        child: Text(flag,
            style: const TextStyle(
                fontSize: SimfTokens.delegationsStatsStripFontSize,),),
      ),
    );
  }
}
