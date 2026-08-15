import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The small gold-tinted marker on the device the app is currently running on.
///
/// Its own file rather than a private class beside the device row, because a
/// private widget class is a SIMF-C3 finding wherever it sits: the checker
/// reads it as a widget that never became reusable. Kept deliberately narrow —
/// it takes a label and nothing else, so it stays a marker rather than
/// drifting into a general-purpose chip every screen styles differently.
class DeviceChip extends StatelessWidget {
  const DeviceChip({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(text, style: SimfTokens.labelGoldMedium),
    );
  }
}
