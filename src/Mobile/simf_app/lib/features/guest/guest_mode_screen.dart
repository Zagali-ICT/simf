import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Page 012 — وضع الضيف · Guest mode (#12, `/guest`, **public**).
///
/// An informational entry screen — **no API**. It explains what a guest can do
/// (browse sessions, speakers, the venue map and media) and what needs an
/// account (the smart badge, personal notifications and booking), then offers a
/// primary **Continue as guest** action (→ home) and a secondary **Sign in**
/// action (→ Page_003). Renders entirely client-side, so it is offline-safe.
class GuestModeScreen extends StatelessWidget {
  const GuestModeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.guestModeTitle)),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(SimfTokens.space6),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                const Icon(
                  Icons.explore_outlined,
                  size: 72,
                  color: SimfTokens.accent,
                ),
                const SizedBox(height: SimfTokens.space5),
                Text(
                  l10n.guestModeHeadline,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontSize: SimfTokens.textXl,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: SimfTokens.space3),
                Text(
                  l10n.guestModeBrowseBody,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: SimfTokens.inkMuted),
                ),
                const SizedBox(height: SimfTokens.space3),
                Text(
                  l10n.guestModeSignInBody,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: SimfTokens.inkMuted),
                ),
                const SizedBox(height: SimfTokens.space6),
                FilledButton(
                  onPressed: () => context.go('/'),
                  child: Text(l10n.guestModeContinueButton),
                ),
                const SizedBox(height: SimfTokens.space2),
                OutlinedButton(
                  onPressed: () => context.pushNamed(RouteNames.signIn),
                  child: Text(l10n.guestModeSignInButton),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
