import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// One gold-hairline bullet card (Figma 505:1639): the gold • at the inline
/// start, the term text in `beigeBorder`.
class TermsBulletCard extends StatelessWidget {
  const TermsBulletCard({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        // The frame's hairline (505:1639 — 0.2px); kept ≥0.2 so it still
        // rasterises on every phone density.
        border: Border.all(
            color: SimfTokens.accent, width: SimfTokens.termsBulletCardWidth,),
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const Padding(
            padding: EdgeInsetsDirectional.only(
              start: SimfTokens.space1,
              end: SimfTokens.space3,
            ),
            child: Text(
              '•',
              style: TextStyle(
                color: SimfTokens.accent,
                fontSize: SimfTokens.textLg,
              ),
            ),
          ),
          Expanded(
            child: SelectableText(
              text,
              style: SimfTokens.bodyBeige,
            ),
          ),
        ],
      ),
    );
  }
}
