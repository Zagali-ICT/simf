import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';

/// The "no code? resend" foot under the sign-up email-verify CTA (505:1003).
/// [onResend] is null while the cooldown runs or a request is in flight, which
/// is what greys the link out.
class EmailVerifyResendRow extends StatelessWidget {
  const EmailVerifyResendRow({required this.onResend, super.key});

  final VoidCallback? onResend;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Text(
          l10n.noCodeQuestion,
          style: const TextStyle(
            color: SimfTokens.surface,
            fontSize: SimfTokens.textMd,
            fontWeight: FontWeight.w500,
          ),
        ),
        const SizedBox(width: SimfTokens.gap6),
        TextButton(
          onPressed: onResend,
          style: authLinkButtonStyle(SimfTokens.accent),
          child: Text(
            l10n.resendAction,
            style: const TextStyle(
              fontSize: SimfTokens.textMd,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ],
    );
  }
}
