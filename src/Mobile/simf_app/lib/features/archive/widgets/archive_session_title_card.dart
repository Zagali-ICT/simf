import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// One session-title card (frame node 927:3308): a 48-high navy box on the
/// beige 0.2px hairline, the title right-aligned in beige 14px SemiBold
/// (frame's bordered card, not a bare bullet).
class ArchiveSessionTitleCard extends StatelessWidget {
  const ArchiveSessionTitleCard({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: SimfTokens.controlHeight,
      width: double.infinity,
      alignment: Alignment.centerRight,
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Text(
        text,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        textAlign: TextAlign.start,
        style: SimfTokens.labelBeigeSemibold,
      ),
    );
  }
}
