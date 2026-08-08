import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_language_toggle.dart';

/// The onboarding top bar: a back chevron on steps 2–3 at the physical left and
/// the gold language globe at the physical right (matching the sign-in top).
/// Forced LTR so both sit at the same edges in AR + EN and the chevron is not
/// auto-mirrored. The fixed height keeps the layout stable on step 1 where the
/// chevron is absent. Skip lives at the bottom, under the primary action, per
/// Figma 758:1077.
class OnboardingTopBar extends StatelessWidget {
  const OnboardingTopBar({
    required this.showBack,
    required this.onBack,
    required this.onToggleLanguage,
    super.key,
  });

  final bool showBack;
  final VoidCallback onBack;
  final VoidCallback onToggleLanguage;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: SimfTokens.controlHeight,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space2,
          vertical: SimfTokens.space1,
        ),
        child: Row(
          textDirection: TextDirection.ltr,
          children: <Widget>[
            if (showBack)
              IconButton(
                onPressed: onBack,
                tooltip: MaterialLocalizations.of(context).backButtonTooltip,
                icon: const Icon(
                  Icons.arrow_back_ios_new,
                  color: SimfTokens.surface,
                  size: SimfTokens.onboardingTopBarSize,
                  textDirection: TextDirection.ltr,
                ),
              ),
            const Spacer(),
            SimfLanguageToggle(onPressed: onToggleLanguage),
          ],
        ),
      ),
    );
  }
}
