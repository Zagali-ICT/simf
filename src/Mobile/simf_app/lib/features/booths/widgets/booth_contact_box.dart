import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// One booth-officer contact box (frame 922:2810): a bordered navy box carrying
/// the contact text (email / phone) with a trailing glyph.
class BoothContactBox extends StatelessWidget {
  const BoothContactBox({required this.text, required this.icon, super.key});

  final String text;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: SimfTokens.contactRowHeight,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              textDirection: TextDirection.ltr,
              style: SimfTokens.bodyWhiteXs,
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Icon(icon, size: 16, color: SimfTokens.beigeBorder),
        ],
      ),
    );
  }
}
