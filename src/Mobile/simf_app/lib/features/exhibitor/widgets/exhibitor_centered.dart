import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// A centred, padded message — the shared empty / forbidden surface for the
/// exhibitor "My Visitors" screen (D-426). Extracted from `my_visitors_screen`
/// per the #16 sweep so the screen body stays a thin composition.
class ExhibitorCentered extends StatelessWidget {
  const ExhibitorCentered({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Text(
          text,
          textAlign: TextAlign.center,
          style: SimfTokens.hintBeige,
        ),
      ),
    );
  }
}
