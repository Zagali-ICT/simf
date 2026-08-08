import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

class FlagBox extends StatelessWidget {
  const FlagBox({required this.emoji});

  final String emoji;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.flagBoxWidth,
      height: SimfTokens.flagBoxHeight,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.surfaceTint,
        border: Border.all(color: SimfTokens.line),
        borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
      ),
      child: Text(emoji, style: const TextStyle(fontSize: SimfTokens.flagBoxFontSize)),
    );
  }
}
