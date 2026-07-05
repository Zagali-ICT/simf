import 'package:flutter/material.dart';

import '../localization/app_l10n.dart';
import '../theme/tokens.dart';
import 'simf_svg_icon.dart';

const String _icGlobe = 'assets/icons/auth_globe.svg'; // exact Figma globe

/// The gold globe language toggle — a navy-deep rounded square with the Figma
/// globe glyph — shown at the top-trailing corner of the auth entry and
/// onboarding tops. [onPressed] flips AR ↔ EN; the control is disabled while
/// [busy]. (The frozen auth screens keep their own copy per the freeze rule;
/// new screens use this shared widget.)
class SimfLanguageToggle extends StatelessWidget {
  const SimfLanguageToggle({
    required this.onPressed,
    this.busy = false,
    super.key,
  });

  final VoidCallback onPressed;

  /// While true the button is disabled (a request is in flight).
  final bool busy;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 40,
      height: 40,
      child: IconButton(
        key: const ValueKey<String>('languageToggle'),
        tooltip: AppL10n.of(context).languageToggleLabel,
        onPressed: busy ? null : onPressed,
        style: IconButton.styleFrom(
          backgroundColor: SimfTokens.navyDeep,
          shape: const RoundedRectangleBorder(
            borderRadius: SimfTokens.borderRadiusSmall,
          ),
        ),
        icon: const SimfSvgIcon(_icGlobe, size: 24, color: SimfTokens.accent),
      ),
    );
  }
}
