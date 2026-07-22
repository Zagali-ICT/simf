import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The full-width gold primary button (Figma 1701:3826): h48, radius-4, white
/// bold label.
class RegistrationPrimaryButton extends StatelessWidget {
  const RegistrationPrimaryButton({
    required this.label,
    required this.onTap,
    super.key,
  });

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      width: double.infinity,
      child: Material(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          child: Center(
            child: Text(
              label,
              style: const TextStyle(
                color: SimfTokens.surface,
                fontSize: SimfTokens.textLg, // 16
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
