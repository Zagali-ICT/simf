import 'package:flutter/material.dart';

import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_language_toggle.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// The auth screens' top controls (Figma 627:2361): an optional back chevron at
/// the inline start + the language toggle at the end, forced LTR so the sides
/// and the chevron glyph match the frame under RTL. When [onBack] is null the
/// back button is omitted entirely (e.g. sign-in, which has no back target).
/// The language control is the shared EN/عر pill (Figma 1967:3661, D-670).
class AccountTopControls extends StatelessWidget {
  const AccountTopControls({
    required this.onToggleLanguage,
    required this.busy,
    this.onBack,
    super.key,
  });

  final VoidCallback onToggleLanguage;

  /// Null hides the back button (sign-in has nowhere to go back to).
  final VoidCallback? onBack;

  /// While true both controls are disabled (a request is in flight).
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final onBack = this.onBack;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space3, vertical: SimfTokens.space2),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          if (onBack != null)
            IconButton(
              key: const ValueKey<String>('accountBack'),
              onPressed: busy ? null : onBack,
              tooltip: MaterialLocalizations.of(context).backButtonTooltip,
              icon: const SimfSvgIcon(
                AppAssets.authBack,
                size: SimfTokens.accountTopControlsSize,
                color: SimfTokens.surface,
              ),
            ),
          const Spacer(),
          SimfLanguageToggle(
            onPressed: onToggleLanguage,
            busy: busy,
          ),
        ],
      ),
    );
  }
}
