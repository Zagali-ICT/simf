import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The gold category chip overlaid on the thumbnail (frame node 958:2203): a
/// solid-gold rounded pill with white bold text.
class CategoryChip extends StatelessWidget {
  const CategoryChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      // Frame 958:2203 — 10px horizontal padding (no matching spacing token).
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: SimfTokens.space1,
      ),
      decoration: const BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: SimfTokens.labelWhiteBoldXs,
      ),
    );
  }
}
