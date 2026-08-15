import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

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
///
/// Route: `RouteNames.guestMode`.
/// Data: none — renders what it is given.
/// Perf: no list — a single-screen layout.
class GuestModeScreen extends StatelessWidget {
  const GuestModeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.guestModeTitle,
      onBack: () => backOrHome(context),
      showSweep: true,
      showBottomNav: false,
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Center(
                child: Container(
                  width: SimfTokens.guestModeScreenWidthMd,
                  height: SimfTokens.guestModeScreenHeightMd,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: SimfTokens.accent.withValues(alpha: 0.08),
                    border: Border.all(
                        color: SimfTokens.accent,
                        width: SimfTokens.guestModeScreenWidthSm,),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.explore_outlined,
                    size: SimfTokens.guestModeScreenSize,
                    color: SimfTokens.accent,
                  ),
                ),
              ),
              const SizedBox(height: SimfTokens.space5),
              Text(
                l10n.guestModeHeadline,
                textAlign: TextAlign.center,
                style: SimfTokens.labelWhiteBoldXl,
              ),
              const SizedBox(height: SimfTokens.space5),
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
                        height: SimfTokens.guestModeScreenHeightSm,
                      ),
                    ),
                    const SizedBox(height: SimfTokens.space3),
                    Text(
                      l10n.guestModeSignInBody,
                      style: const TextStyle(
                        color: SimfTokens.txtSecondary,
                        fontSize: SimfTokens.textSm,
                        height: SimfTokens.guestModeScreenHeightSm,
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
    );
  }
}
