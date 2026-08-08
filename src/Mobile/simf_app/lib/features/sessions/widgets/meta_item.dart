import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// One icon + label pair in the meta line (frame 889:2687/889:2686).
class MetaItem extends StatelessWidget {
  const MetaItem({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Icon(icon, size: SimfTokens.metaItemSize, color: SimfTokens.beigeBorder),
        const SizedBox(width: SimfTokens.space2),
        Text(
          label,
          // SemiBold (owner 2026-06-30): the time/date numbers read bolder than
          // the frame's Regular. The ambient Directionality is LTR so the time
          // digits read start→end and the icon leads on the left.
          style: SimfTokens.labelBeigeSemiboldSm,
        ),
      ],
    );
  }
}
