import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The onboarding top bar: a back chevron (steps 2–3) at the physical left and a
/// prominent تخطي skip at the top-trailing corner (every step but the last).
/// Forced LTR so both sit in the same place in AR + EN and the chevron is not
/// auto-mirrored. The fixed height keeps the layout stable when either control
/// is absent.
class OnboardingTopBar extends StatelessWidget {
  const OnboardingTopBar({
    required this.showBack,
    required this.onBack,
    required this.showSkip,
    required this.onSkip,
    required this.skipLabel,
    super.key,
  });

  final bool showBack;
  final VoidCallback onBack;
  final bool showSkip;
  final VoidCallback onSkip;
  final String skipLabel;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        child: Row(
          textDirection: TextDirection.ltr,
          children: <Widget>[
            if (showBack)
              IconButton(
                onPressed: onBack,
                icon: const Icon(
                  Icons.arrow_back_ios_new,
                  color: Colors.white,
                  size: 20,
                  textDirection: TextDirection.ltr,
                ),
              )
            else
              const SizedBox(width: 48),
            const Spacer(),
            if (showSkip)
              TextButton(
                onPressed: onSkip,
                style: TextButton.styleFrom(
                  foregroundColor: SimfTokens.accent,
                ),
                child: Text(
                  skipLabel,
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
