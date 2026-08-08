import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The confirmation chip above the CTAs: shows the seat the visitor has tapped
/// so they can verify it before committing. A bordered navy pill (frame parity
/// with the my-seat chips) — kept forced-LTR-agnostic (it reads inside the RTL
/// picker body).
class SelectedSeatChip extends StatelessWidget {
  const SelectedSeatChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(color: SimfTokens.accent),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        textAlign: TextAlign.center,
        style: SimfTokens.labelWhiteSemibold,
      ),
    );
  }
}
