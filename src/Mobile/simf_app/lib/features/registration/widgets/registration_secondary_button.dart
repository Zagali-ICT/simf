import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The full-width **outlined** secondary button beneath the gold primary action
/// (h48, radius-4, beige hairline border, beige bold label). Distinct from the
/// gold [RegistrationPrimaryButton] and the muted [RegistrationSignOutLink] —
/// used for the "الانتقال للرئيسية" (go to home) action a non-approved account
/// gets under Re-check so it is never stuck on the registration gate.
class RegistrationSecondaryButton extends StatelessWidget {
  const RegistrationSecondaryButton({
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
        color: Colors.transparent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          child: DecoratedBox(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              border: Border.all(color: SimfTokens.beigeBorder),
            ),
            child: Center(
              child: Text(
                label,
                style: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  fontSize: SimfTokens.textLg, // 16
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
