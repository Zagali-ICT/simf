import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

class RolePill extends StatelessWidget {
  const RolePill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      alignment: Alignment.center,
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        style: SimfTokens.labelWhiteBoldTitle,
      ),
    );
  }
}
