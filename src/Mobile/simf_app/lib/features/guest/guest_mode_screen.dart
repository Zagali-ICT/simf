import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/guest/widgets/guest_explore_mark.dart';
import 'package:simf_app/features/guest/widgets/guest_mode_callout.dart';

/// Guest mode — وضع الضيف · route: `RouteNames.guestMode` · public
/// Contract: no API — renders entirely client-side, so it is offline-safe.
/// Deliberate deviation from the Page 012 mockup frame: the tile grid, the
/// "open to everyone" rows and the bottom nav are home-dashboard navigation
/// this entry screen does not carry.
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
              const GuestExploreMark(),
              const SizedBox(height: SimfTokens.space5),
              Text(
                l10n.guestModeHeadline,
                textAlign: TextAlign.center,
                style: SimfTokens.labelWhiteBoldXl,
              ),
              const SizedBox(height: SimfTokens.space5),
              const GuestModeCallout(),
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
