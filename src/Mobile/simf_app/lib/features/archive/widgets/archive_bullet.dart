import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// A bulleted list item (the frame's disc bullets, node 925:3258 / 925:3264):
/// a leading inline-start disc and the value text. The disc colour matches the
/// text so gold titles get a gold bullet, beige body gets a beige bullet.
class ArchiveBullet extends StatelessWidget {
  const ArchiveBullet({
    required this.text,
    required this.color,
    this.bold = false,
    super.key,
  });

  final String text;
  final Color color;
  final bool bold;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Padding(
          padding: const EdgeInsetsDirectional.only(
            top: SimfTokens.bulletTopNudge,
            end: SimfTokens.space2,
          ),
          child: Container(
            width: 5,
            height: 5,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
        ),
        Expanded(
          child: Text(
            text,
            style: (bold ? SimfTokens.bulletTitle : SimfTokens.bulletBody)
                .copyWith(color: color),
          ),
        ),
      ],
    );
  }
}
