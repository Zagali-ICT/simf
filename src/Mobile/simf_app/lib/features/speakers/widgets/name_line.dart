import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The header name line. Figma 1327:3461 — when the speaker has a nationality
/// the flag sits at the inline-start of the name (physical left in this
/// LTR-forced header), 8px before it; the name ellipsises if it is too long.
class NameLine extends StatelessWidget {
  const NameLine({required this.title, required this.flag, super.key});

  final String title;
  final String flag;

  @override
  Widget build(BuildContext context) {
    final nameText = Text(
      title,
      textAlign: TextAlign.center,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: SimfTokens.labelWhiteSemiboldTitle,
    );
    if (flag.isEmpty) {
      return nameText;
    }
    return Row(
      textDirection: TextDirection.ltr,
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Text(flag, style: const TextStyle(fontSize: SimfTokens.textTitle)),
        const SizedBox(width: SimfTokens.space2),
        Flexible(child: nameText),
      ],
    );
  }
}
