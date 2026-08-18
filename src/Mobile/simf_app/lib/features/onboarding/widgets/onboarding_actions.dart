import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The carousel footer: the gold التالي primary action, then تخطي.
///
/// تخطي sits under the primary action, centered, on every step but the last —
/// Figma 758:1077 (node 758:1091). A matching spacer holds the last step
/// steady when the link is gone.
class OnboardingActions extends StatelessWidget {
  const OnboardingActions({
    required this.isLast,
    required this.onNext,
    required this.onSkip,
    super.key,
  });

  final bool isLast;
  final VoidCallback onNext;
  final VoidCallback onSkip;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space4,
          ),
          child: FilledButton(
            onPressed: onNext,
            child: Text(
              l10n.onboardingNext,
              style: SimfTokens.titleBold,
            ),
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        if (isLast)
          const SizedBox(height: SimfTokens.controlHeight)
        else
          TextButton(
            onPressed: onSkip,
            style: TextButton.styleFrom(
              foregroundColor: SimfTokens.accent,
            ),
            child: Text(
              l10n.onboardingSkip,
              style: SimfTokens.titleSemibold,
            ),
          ),
        const SizedBox(height: SimfTokens.space4),
      ],
    );
  }
}
