import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The invisible padding that grows each flag's tap target to a
/// comfortable size without moving the painted dot.
const double flagSpotHitPad = 9;

/// One scattered flag glyph as a tap target. The glyph sits inside
/// [flagSpotHitPad] of transparent padding (widening the touch
/// area); when [selected] the padding box is ringed in gold to mark the active
/// filter.
class FlagSpot extends StatelessWidget {
  const FlagSpot({
    required this.flag,
    required this.selected,
    required this.onTap,
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
        child: Text(flag, style: const TextStyle(fontSize: SimfTokens.delegationsStatsStripFontSize)),
      ),
    );
  }
}
