import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// One filter chip: gold-filled when selected, gold-outlined otherwise.
class NotificationFilterChip extends StatelessWidget {
  const NotificationFilterChip({
    required this.label,
    required this.selected,
    required this.onTap,
    super.key,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 758:2491 — gold fill when selected, beige 0.2 hairline otherwise;
    // radius 4, 14px SemiBold, label always white.
    return Material(
      color: selected ? SimfTokens.accent : SimfTokens.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Text(
            label,
            style: SimfTokens.labelWhiteSemibold,
          ),
        ),
      ),
    );
  }
}
