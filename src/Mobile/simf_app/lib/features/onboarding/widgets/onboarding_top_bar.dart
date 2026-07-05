import 'package:flutter/material.dart';

/// The onboarding top bar: a back chevron on steps 2–3 at the physical left.
/// Forced LTR so the chevron sits at the same edge in AR + EN and is not
/// auto-mirrored. The fixed height keeps the layout stable on step 1 where the
/// chevron is absent. Skip lives at the bottom, under the primary action, per
/// Figma 758:1077.
class OnboardingTopBar extends StatelessWidget {
  const OnboardingTopBar({
    required this.showBack,
    required this.onBack,
    super.key,
  });

  final bool showBack;
  final VoidCallback onBack;

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
              ),
          ],
        ),
      ),
    );
  }
}
