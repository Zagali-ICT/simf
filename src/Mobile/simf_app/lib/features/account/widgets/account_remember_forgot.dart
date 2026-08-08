import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import 'auth_chrome.dart';

/// The sign-in card's "remember me" checkbox at the inline start and the
/// "forgot password?" link at the end (Figma 168:2800). Extracted so the row's
/// metrics and the link style stay in one place.
class AccountRememberForgot extends StatelessWidget {
  const AccountRememberForgot({
    required this.rememberMe,
    required this.onRememberChanged,
    required this.rememberLabel,
    required this.forgotLabel,
    required this.onForgot,
    required this.enabled,
    super.key,
  });

  final bool rememberMe;
  final ValueChanged<bool> onRememberChanged;
  final String rememberLabel;
  final String forgotLabel;
  final VoidCallback onForgot;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        Flexible(
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              SizedBox(
                width: SimfTokens.accountRememberForgotWidthLg,
                height: SimfTokens.accountRememberForgotHeight,
                child: Checkbox(
                  value: rememberMe,
                  // The caption is a sibling Text, so the box itself announced
                  // as an unnamed checkbox (BUG-012).
                  semanticLabel: rememberLabel,
                  onChanged: enabled
                      ? (v) => onRememberChanged(v ?? true)
                      : null,
                  activeColor: SimfTokens.accent,
                  side: const BorderSide(
                    color: SimfTokens.greyText,
                    width: SimfTokens.accountRememberForgotWidthSm,
                  ),
                  shape: const RoundedRectangleBorder(
                    borderRadius: SimfTokens.borderRadiusSmall,
                  ),
                  materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                ),
              ),
              const SizedBox(width: SimfTokens.accountRememberForgotWidthMd),
              Flexible(
                child: Text(
                  rememberLabel,
                  style: SimfTokens.bodyGreySm,
                ),
              ),
            ],
          ),
        ),
        Flexible(
          child: TextButton(
            onPressed: enabled ? onForgot : null,
            style: authLinkButtonStyle(SimfTokens.greyText),
            child: Text(
              forgotLabel,
              style: const TextStyle(fontSize: SimfTokens.textSm),
            ),
          ),
        ),
      ],
    );
  }
}
