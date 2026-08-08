import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

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
        minimumSize: const Size.fromHeight(SimfTokens.buttonHeight),
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
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w600,
                color: SimfTokens.goldSoft,
              ),
            ),
          ),
          const SizedBox(width: SimfTokens.authChromeWidthSm),
          icon,
        ],
      ),
    );
  }
}

final ButtonStyle authSubmitButtonStyle = FilledButton.styleFrom(
  backgroundColor: SimfTokens.accent,
  disabledBackgroundColor: SimfTokens.accent.withValues(alpha: 0.5),
  minimumSize: const Size.fromHeight(SimfTokens.buttonHeight),
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
              width: SimfTokens.space5,
              height: SimfTokens.space5,
              child: CircularProgressIndicator(
                strokeWidth: SimfTokens.authChromeStrokeWidth,
                color: SimfTokens.surface,
              ),
            )
          : Text(
              label,
              style: SimfTokens.labelWhiteBoldLg,
            ),
    );
  }
}
