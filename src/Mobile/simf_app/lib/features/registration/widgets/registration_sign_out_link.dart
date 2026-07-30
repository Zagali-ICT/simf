import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The "تسجيل الخروج" link beneath the primary button (Figma 1701:3827) —
/// centred, muted, 12px.
class RegistrationSignOutLink extends StatelessWidget {
  const RegistrationSignOutLink({
    required this.label,
    required this.onTap,
    super.key,
  });

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return TextButton(
      onPressed: onTap,
      style: TextButton.styleFrom(
        minimumSize: const Size.fromHeight(36),
        foregroundColor: SimfTokens.beigeBorder,
      ),
      child: Text(
        label,
        textAlign: TextAlign.center,
        style: SimfTokens.labelBeigeSm,
      ),
    );
  }
}
