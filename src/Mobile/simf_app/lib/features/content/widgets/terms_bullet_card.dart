import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// One gold-hairline bullet card (Figma 505:1639): the gold • at the inline
/// start, the term text in `beigeBorder`.
class TermsBulletCard extends StatelessWidget {
  const TermsBulletCard({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(8),
      decoration: BoxDecoration(
        // The frame's hairline (505:1639 — 0.2px); kept ≥0.2 so it still
        // rasterises on every phone density.
        border: Border.all(color: SimfTokens.accent, width: 0.2),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const Padding(
            padding: EdgeInsetsDirectional.only(start: 4, end: 12),
            child: Text(
              '•',
              style: TextStyle(color: SimfTokens.accent, fontSize: 16),
            ),
          ),
          Expanded(
            child: SelectableText(
              text,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: 14,
                height: 1.5,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
