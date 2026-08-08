import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import 'star_row.dart';

/// One per-element row: the beige-hairline 48-high box with the element name at
/// the inline start and the small star bar at the inline end.
class RateCategoryRow extends StatelessWidget {
  const RateCategoryRow({
    required this.label,
    required this.value,
    required this.onChanged,
    super.key,
  });

  final String label;
  final int value;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: SimfTokens.controlHeight,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      decoration: BoxDecoration(
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: <Widget>[
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: SimfTokens.labelBeigeSemibold,
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          StarRow(
            value: value,
            size: SimfTokens.rateCategoryRowSize,
            gap: SimfTokens.space1,
            onChanged: onChanged,
          ),
        ],
      ),
    );
  }
}
