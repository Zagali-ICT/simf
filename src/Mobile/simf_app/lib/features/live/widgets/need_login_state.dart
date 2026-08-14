import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// Login-gate state (owner, D-577): the live stream is login-only, so a
/// signed-out guest sees this prompt — an icon, a message, and a gold Sign-in
/// button that routes to sign-in — instead of the player, from any entry point.
class NeedLoginState extends StatelessWidget {
  const NeedLoginState({
    required this.message,
    required this.signInLabel,
    required this.onSignIn,
    super.key,
  });

  final String message;
  final String signInLabel;
  final VoidCallback onSignIn;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.lock_outline,
              size: SimfTokens.liveContentSizeMd,
              color: SimfTokens.beigeBorder,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: SimfTokens.hintBeige,
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(
              onPressed: onSignIn,
              style: FilledButton.styleFrom(
                backgroundColor: SimfTokens.accent,
                foregroundColor: SimfTokens.surface,
              ),
              child: Text(signInLabel),
            ),
          ],
        ),
      ),
    );
  }
}
