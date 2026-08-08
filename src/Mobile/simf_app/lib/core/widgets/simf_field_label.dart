import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// A caption above a form field, aligned to the inline start (right under RTL).
///
/// Defaults to the design's 12px grey label; pass [color] (e.g.
/// [SimfTokens.navy]) or [fontSize] for the documented per-screen variants.
/// One shared widget replaces the five near-identical field-label copies that
/// used to live in profile / auth / sign-in / sign-up-form.
class SimfFieldLabel extends StatelessWidget {
  const SimfFieldLabel(
    this.text, {
    this.color = SimfTokens.greyText,
    this.fontSize = 12,
    super.key,
  });

  final String text;
  final Color color;
  final double fontSize;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Text(
        text,
        style: TextStyle(
          color: color,
          fontSize: fontSize,
          fontWeight: FontWeight.w500,
        ),
      ),
    );
  }
}
