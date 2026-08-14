import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// A gold (or beige) right-aligned bulleted line — the frame's "·"-led list
/// items (the session title 934:3616, the speakers line 934:3617).
class GoldBullet extends StatelessWidget {
  const GoldBullet({
    required this.text,
    required this.color,
    this.fontWeight = FontWeight.w500,
    this.fontSize = SimfTokens.textMd,
    super.key,
  });

  final String text;
  final Color color;
  final FontWeight fontWeight;
  final double fontSize;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Expanded(
          child: Text(
            text,
            textAlign: TextAlign.start,
            style: TextStyle(
              color: color,
              fontSize: fontSize,
              fontWeight: fontWeight,
              height: SimfTokens.liveContentHeightSm,
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        Padding(
          padding: const EdgeInsets.only(top: SimfTokens.gap6),
          child: Container(
            width: SimfTokens.liveContentWidth,
            height: SimfTokens.liveContentHeightMd,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
        ),
      ],
    );
  }
}
