import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// A field caption above its input — the design's 12-grey label
/// (Figma "Title" rows).
class FieldLabel extends StatelessWidget {
  const FieldLabel(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Text(
        text,
        style: const TextStyle(
          color: SimfTokens.greyText,
          fontSize: 12,
          fontWeight: FontWeight.w500,
        ),
      ),
    );
  }
}
