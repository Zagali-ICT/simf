import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The generate-summary card (frame 1433:11389): the gold "توليد ملخص للجلسة"
/// button (sparkle + label on the right, the collapse chevron on the left) over
/// the published summary paragraph, shown when expanded.
class SummaryGenerateCard extends StatelessWidget {
  const SummaryGenerateCard({
    required this.label,
    required this.expanded,
    required this.onToggle,
    required this.paragraph,
    super.key,
  });

  final String label;
  final bool expanded;
  final VoidCallback onToggle;
  final String paragraph;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space4,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Material(
            color: SimfTokens.accent,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            child: InkWell(
              onTap: onToggle,
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              child: Container(
                height: 48,
                padding: const EdgeInsets.symmetric(
                  horizontal: SimfTokens.space4,
                  vertical: SimfTokens.space3,
                ),
                child: Row(
                  children: <Widget>[
                    Expanded(
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          const Icon(
                            Icons.auto_awesome,
                            size: 18,
                            color: Colors.white,
                          ),
                          const SizedBox(width: SimfTokens.space2),
                          Flexible(
                            child: Text(
                              label,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                color: Colors.white,
                                fontSize: SimfTokens.textLg,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    Icon(
                      expanded
                          ? Icons.keyboard_arrow_up
                          : Icons.keyboard_arrow_down,
                      size: 20,
                      color: Colors.white,
                    ),
                  ],
                ),
              ),
            ),
          ),
          if (expanded) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            Text(
              paragraph,
              textAlign: TextAlign.start,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w500,
                height: 1.5,
              ),
            ),
          ],
        ],
      ),
    );
  }
}
