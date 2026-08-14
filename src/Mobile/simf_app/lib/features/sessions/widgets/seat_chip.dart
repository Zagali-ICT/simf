import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// One bordered seat/row chip (frame 905:1577 / 905:1579): a gold label word
/// next to its value, centred on a navyDeep fill with a thin gold/beige border.
class SeatChip extends StatelessWidget {
  const SeatChip({
    required this.goldLabel,
    required this.value,
    required this.borderColor,
    required this.borderWidth,
    super.key,
  });

  final String goldLabel;
  final String value;
  final Color borderColor;
  final double borderWidth;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: SimfTokens.actionChipHeight,
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(color: borderColor, width: borderWidth),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text.rich(
        TextSpan(
          children: <InlineSpan>[
            TextSpan(
              text: goldLabel,
              style: SimfTokens.labelGoldSemiboldSm,
            ),
            const TextSpan(text: ' '),
            TextSpan(
              text: value,
              // Frame 905:1577/1579 — the value (12 / B) is white; only the
              // leading label word (مقعد / الصف) is gold.
              style: SimfTokens.labelWhiteSemibold,
            ),
          ],
        ),
        textAlign: TextAlign.center,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }
}
