import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The no-logo / failed-fetch fall-back: the partner's initials on the frame's
/// gold tile (navy text for contrast on gold).
class InitialsTile extends StatelessWidget {
  const InitialsTile({required this.initials});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.accent,
      child: Center(
        child: Text(
          initials,
          style: SimfTokens.labelNavyBoldTracked,
        ),
      ),
    );
  }
}
