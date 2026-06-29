import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// One of the design's gender radio pills (Figma 522:2151): a white pill with
/// the label and an 18 px gold-ringed radio that fills when selected.
class RadioPill extends StatelessWidget {
  const RadioPill({
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
    return InkWell(
      onTap: onTap,
      borderRadius: SimfTokens.borderRadiusSmall,
      child: Container(
        height: 48,
        decoration: const BoxDecoration(
          color: Color(0xE6FFFFFF), // white at 90% over the beige card
          borderRadius: SimfTokens.borderRadiusSmall,
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Text(
              label,
              style: const TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w500,
                color: SimfTokens.navy,
              ),
            ),
            const SizedBox(width: 12),
            Container(
              width: 18,
              height: 18,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(color: SimfTokens.accent, width: 1.2),
              ),
              alignment: Alignment.center,
              child: selected
                  ? Container(
                      width: 10,
                      height: 10,
                      decoration: const BoxDecoration(
                        shape: BoxShape.circle,
                        color: SimfTokens.accent,
                      ),
                    )
                  : null,
            ),
          ],
        ),
      ),
    );
  }
}
