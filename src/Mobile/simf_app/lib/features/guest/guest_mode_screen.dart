import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';

/// Page 012 — وضع الضيف · Guest mode (#12, `/guest`, **public**).
///
/// An informational entry screen — **no API**. It explains what a guest can do
/// (browse sessions, speakers, the venue map and media) and what needs an
/// account (the smart badge, personal notifications and booking), then offers a
/// primary **Continue as guest** action (→ home) and a secondary **Sign in**
/// action (→ Page_003). Renders entirely client-side, so it is offline-safe.
///
/// Styled to the Page 012 mockup frame (الرئيسية · ضيف): a navy surface with an
/// accent explore mark, an accent "you are browsing as a guest" callout
/// carrying the browse + sign-in explainer, then the navy/gold primary action
/// and a bordered sign-in action. The mockup frame's tile grid, "open to
/// everyone" rows and bottom-nav are home-dashboard navigation this entry
/// screen does not carry, so they are not rendered here.
class GuestModeScreen extends StatelessWidget {
  const GuestModeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(leading: const SimfBackButton(), title: Text(l10n.guestModeTitle)),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(SimfTokens.space6),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                // Accent explore mark inside a hairline-bordered accent ring
                // (mockup state-badge idiom on the navy surface).
                Center(
                  child: Container(
                    width: 64,
                    height: 64,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: SimfTokens.accent.withValues(alpha: 0.08),
                      border: Border.all(color: SimfTokens.accent, width: 1.5),
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.explore_outlined,
                      size: 30,
                      color: SimfTokens.accent,
                    ),
                  ),
                ),
                const SizedBox(height: SimfTokens.space5),
                Text(
                  l10n.guestModeHeadline,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: SimfTokens.surface,
                    fontSize: SimfTokens.textXl,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: SimfTokens.space5),
                // Accent guest callout (mockup: accent border + accent-6% fill,
                // on-navy secondary copy — the mockup's dashed-accent banner
                // rendered with the shipped solid-accent callout idiom). Carries
                // the "what a guest can browse" line and the "sign in for more"
                // line.
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: SimfTokens.space4,
                    vertical: SimfTokens.space4,
                  ),
                  decoration: BoxDecoration(
                    color: SimfTokens.accent.withValues(alpha: 0.06),
                    border: Border.all(color: SimfTokens.accent),
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        l10n.guestModeBrowseBody,
                        style: const TextStyle(
                          color: SimfTokens.txtSecondary,
                          fontSize: SimfTokens.textSm,
                          height: 1.7,
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space3),
                      Text(
                        l10n.guestModeSignInBody,
                        style: const TextStyle(
                          color: SimfTokens.txtSecondary,
                          fontSize: SimfTokens.textSm,
                          height: 1.7,
                        ),
                      ),
                    ],
                  ),
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
