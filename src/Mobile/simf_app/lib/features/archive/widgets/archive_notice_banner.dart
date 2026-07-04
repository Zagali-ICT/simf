import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The beige-bordered notice banner (node 925:3222): centred beige text in a
/// navy-deep box with the 0.5px beige hairline.
class ArchiveNoticeBanner extends StatelessWidget {
  const ArchiveNoticeBanner({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 48,
      alignment: Alignment.center,
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairlineBold,
        ),
      ),
      child: Text(
        text,
        textAlign: TextAlign.center,
        style: const TextStyle(
          color: SimfTokens.beigeBorder,
          fontSize: SimfTokens.textMd,
          fontWeight: FontWeight.w500,
        ),
      ),
    );
  }
}
