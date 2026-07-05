import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_logo.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// The beige-bordered, 48-high outlined "alternative action" button used below
/// the sign-in card's "or" divider (Face-ID, printed-badge QR). Label sits at
/// the inline start with the glyph trailing it, gold-soft text — shared so the
/// shape/typography stays in one place.
class AuthAltButton extends StatelessWidget {
  const AuthAltButton({
    required this.label,
    required this.icon,
    required this.onPressed,
    super.key,
  });

  final String label;

  /// The trailing glyph (an `Icon` or `SimfSvgIcon`); sized by the caller.
  final Widget icon;

  /// Null disables the button.
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton(
      onPressed: onPressed,
      style: OutlinedButton.styleFrom(
        side: const BorderSide(color: SimfTokens.beigeBorder),
        minimumSize: const Size.fromHeight(48),
        shape: const RoundedRectangleBorder(
          borderRadius: SimfTokens.borderRadiusSmall,
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Flexible(
            child: Text(
              label,
              style: const TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w600,
                color: SimfTokens.goldSoft,
              ),
            ),
          ),
          const SizedBox(width: 10),
          icon,
        ],
      ),
    );
  }
}

final ButtonStyle authSubmitButtonStyle = FilledButton.styleFrom(
  backgroundColor: SimfTokens.accent,
  disabledBackgroundColor: SimfTokens.accent.withValues(alpha: 0.5),
  minimumSize: const Size.fromHeight(48),
  shape: const RoundedRectangleBorder(
    borderRadius: SimfTokens.borderRadiusSmall,
  ),
);

/// The compact, padding-free [TextButton] style used by the auth cards' inline
/// links (forgot-password, create-account, guest) — shared so the tap target
/// and metrics stay in one place. [color] is the link's foreground colour.
ButtonStyle authLinkButtonStyle(Color color) => TextButton.styleFrom(
      padding: EdgeInsets.zero,
      minimumSize: Size.zero,
      tapTargetSize: MaterialTapTargetSize.shrinkWrap,
      foregroundColor: color,
    );

/// The auth card's gold submit button with the busy spinner — shared by the
/// sign-in / forgot / reset screens so the spinner/typography stays in one place.
class AuthSubmitButton extends StatelessWidget {
  const AuthSubmitButton({
    required this.label,
    required this.busy,
    required this.onPressed,
    super.key,
  });

  final String label;
  final bool busy;

  /// Null disables the button (the busy spinner still shows when [busy]).
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      onPressed: onPressed,
      style: authSubmitButtonStyle,
      child: busy
          ? const SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: Colors.white,
              ),
            )
          : Text(
              label,
              style: const TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w700,
                color: Colors.white,
              ),
            ),
    );
  }
}

/// Forum logo + name header shared by the auth entry screens (sign-up / verify):
/// the [SimfLogo] at the inline start (the right under RTL) + the forum name,
/// matching the Figma frames' centred logo+title row.
class AuthBrandHeader extends StatelessWidget {
  const AuthBrandHeader({required this.title, super.key});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        const SimfLogo(size: 44),
        const SizedBox(width: 16),
        Flexible(
          child: Text(
            title,
            style: const TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w500,
              color: Colors.white,
            ),
          ),
        ),
      ],
    );
  }
}

/// The auth entry screens' top controls (Figma 627:2361/2407): a back chevron at
/// the start + the gold language globe at the end, forced LTR so the sides and
/// the chevron glyph match the frame even under RTL. Carries stable keys
/// (`authBack` / `authLanguage`) so it can sit on top of a centred scroll body.
class AuthTopControls extends StatelessWidget {
  const AuthTopControls({
    required this.onBack,
    required this.onToggleLanguage,
    required this.busy,
    super.key,
  });

  final VoidCallback onBack;
  final VoidCallback onToggleLanguage;

  /// While true both controls are disabled (a request is in flight).
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          IconButton(
            key: const ValueKey<String>('authBack'),
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
              key: const ValueKey<String>('authLanguage'),
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
