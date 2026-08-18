import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';

/// The "remembered your password? sign in" foot under the forgot-password CTA
/// (918:2371). Both halves are [Flexible] so a long Arabic question wraps
/// instead of overflowing the row.
class RememberedSignInRow extends StatelessWidget {
  const RememberedSignInRow({required this.busy, super.key});

  final bool busy;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Flexible(
          child: Text(
            l10n.rememberedPasswordQuestion,
            style: const TextStyle(
              color: SimfTokens.surface,
              fontSize: SimfTokens.textMd,
              fontWeight: FontWeight.w500,
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.forgotPasswordScreenWidth),
        Flexible(
          child: TextButton(
            onPressed: busy ? null : () => context.goNamed(RouteNames.signIn),
            style: authLinkButtonStyle(SimfTokens.accent),
            child: Text(
              l10n.signInTitle,
              style: const TextStyle(
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ),
      ],
    );
  }
}
