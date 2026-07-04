import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The green-ringed success check (Figma 505:1343): a 104px navy-deep circle
/// with a 2.4px approval-green ring and a centred check.
class RegistrationSuccessMark extends StatelessWidget {
  const RegistrationSuccessMark({super.key});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        width: 104,
        height: 104,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: SimfTokens.navyDeep,
          shape: BoxShape.circle,
          border: Border.all(color: SimfTokens.statusAccepted, width: 2.4),
        ),
        child: const Icon(
          Icons.check_rounded,
          size: 40,
          color: SimfTokens.statusAccepted,
        ),
      ),
    );
  }
}
