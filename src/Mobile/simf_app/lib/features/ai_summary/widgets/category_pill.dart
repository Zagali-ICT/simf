import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The bordered category pill on the card's bottom row.
class CategoryPill extends StatelessWidget {
  const CategoryPill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4, // 16
        vertical: SimfTokens.space2, // 8
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius), // 8
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        textAlign: TextAlign.center,
        style: SimfTokens.labelWhiteMediumSm,
      ),
    );
  }
}
