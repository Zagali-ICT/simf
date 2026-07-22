import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import 'auth_chrome.dart';

/// A centred "question + action link" row shared by the auth cards — sign-in's
/// «no account? create account» and sign-up's «have an account? sign in». The
/// link renders in navy semibold on the compact shared link style.
class AccountAuthPrompt extends StatelessWidget {
  const AccountAuthPrompt({
    required this.question,
    required this.linkLabel,
    required this.onTap,
    required this.enabled,
    super.key,
  });

  final String question;
  final String linkLabel;
  final VoidCallback onTap;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Flexible(
          child: Text(
            question,
            style: const TextStyle(fontSize: SimfTokens.textSm, color: SimfTokens.greyText),
          ),
        ),
        const SizedBox(width: 6),
        Flexible(
          child: TextButton(
            onPressed: enabled ? onTap : null,
            style: authLinkButtonStyle(SimfTokens.linkNavy),
            child: Text(
              linkLabel,
              style: const TextStyle(
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ),
      ],
    );
  }
}
