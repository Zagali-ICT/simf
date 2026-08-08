import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// PAR-D3 — the session's category tag pill, sitting under the title inside the
/// header card: a small gold-hairline pill on the 4px radius carrying the
/// localized category name in gold 12px SemiBold. Rendered only when the session
/// carries a category, so an uncategorised session keeps the pre-PAR-D3 layout.
class CategoryPill extends StatelessWidget {
  const CategoryPill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          // The gold hairline is the BOLD one, as on the accented ملخص الجلسة
          // chip below it (the 0.2 hairline is the beige variant).
          color: SimfTokens.accent,
          width: SimfTokens.hairlineBold,
        ),
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: SimfTokens.labelGoldSemiboldSm,
      ),
    );
  }
}
