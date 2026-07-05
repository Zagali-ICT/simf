import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// The auth screens' top controls (Figma 627:2361): an optional back chevron at
/// the inline start + the gold language globe at the end, forced LTR so the
/// sides and the chevron glyph match the frame under RTL. When [onBack] is null
/// the back button is omitted entirely (e.g. sign-in, which has no back target).
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
    final l10n = AppL10n.of(context);
    final onBack = this.onBack;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          if (onBack != null)
            IconButton(
              key: const ValueKey<String>('accountBack'),
              onPressed: busy ? null : onBack,
              icon: const SimfSvgIcon(
                AppAssets.authBack,
                size: 24,
                color: Colors.white,
              ),
            ),
          const Spacer(),
          SizedBox(
            width: 40,
            height: 40,
            child: IconButton(
              key: const ValueKey<String>('accountLanguage'),
              tooltip: l10n.languageToggleLabel,
              onPressed: busy ? null : onToggleLanguage,
              style: IconButton.styleFrom(
                backgroundColor: SimfTokens.navyDeep,
                shape: const RoundedRectangleBorder(
                  borderRadius: SimfTokens.borderRadiusSmall,
                ),
              ),
              icon: const SimfSvgIcon(
                AppAssets.authGlobe,
                size: 24,
                color: SimfTokens.accent,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
