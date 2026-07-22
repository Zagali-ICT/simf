import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// A navy chip with a leading accent glyph + a beige message, shown above the
/// rating form. Two call sites: the D-713 "watched at" line (event icon) and
/// the owner-2026-07-19 "attend to rate" note (info icon) when not eligible.
class RateNavyNoteChip extends StatelessWidget {
  const RateNavyNoteChip({required this.icon, required this.text, super.key});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space3,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, size: 16, color: SimfTokens.accent),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Text(
              text,
              style: SimfTokens.labelBeigeSemiboldSmTall,
            ),
          ),
        ],
      ),
    );
  }
}
