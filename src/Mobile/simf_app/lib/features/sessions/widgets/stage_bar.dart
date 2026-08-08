import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The gold-bordered "المسرح · STAGE" band at the top of the hall card
/// (frame 905:1584): a full-width navyDeep pill, gold hairline, gold label.
class StageBar extends StatelessWidget {
  const StageBar({required this.label, super.key});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      height: SimfTokens.controlHeight,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.accent,
          width: SimfTokens.hairlineBold,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        textAlign: TextAlign.center,
        style: SimfTokens.bodyGold,
      ),
    );
  }
}
