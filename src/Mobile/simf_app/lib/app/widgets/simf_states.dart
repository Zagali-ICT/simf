import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../route_names.dart';
import '../theme/tokens.dart';

/// The shared loading / error / empty triad every data screen composes.
/// Split out of `simf_page_shell.dart` (one widget group per file); that file
/// re-exports these, so every existing import keeps working.

/// The standard error surface: the message over a gold retry button — one
/// home for the retry chrome instead of a per-screen copy.
class SimfErrorState extends StatelessWidget {
  const SimfErrorState({
    required this.message,
    required this.retryLabel,
    required this.onRetry,
    super.key,
  });

  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.surface),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(retryLabel)),
          ],
        ),
      ),
    );
  }
}

/// The standard empty / pending surface: a muted icon over the message.
class SimfEmptyState extends StatelessWidget {
  const SimfEmptyState({
    required this.icon,
    required this.message,
    this.action,
    super.key,
  });

  final IconData icon;
  final String message;

  /// An optional action rendered under the message — e.g. the true-guest
  /// sign-in / create-account pair on the badge and profile tabs
  /// ([SimfGuestPrompt]). Null keeps the plain empty surface.
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, size: SimfTokens.simfStatesSize, color: SimfTokens.beigeBorder),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: SimfTokens.hintBeige,
            ),
            if (action != null) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              action!,
            ],
          ],
        ),
      ),
    );
  }
}

/// The **true-guest** surface for a signed-in-only tab: the empty-state icon and
/// message over a working sign-in button and a create-account link.
///
/// The bottom nav switches tabs INSIDE the shell (no go_router navigation), so
/// the router's auth redirect never runs and a visitor with no account at all
/// reaches the badge and profile tabs. Both used to render the PENDING-account
/// copy ("your account is not approved yet…" / "under review…"), which describes
/// a submitted registration that does not exist and offered no way out — a dead
/// end (BUG-013). The pending copy stays for genuinely pending accounts.
class SimfGuestPrompt extends StatelessWidget {
  const SimfGuestPrompt({
    required this.icon,
    required this.message,
    required this.signInLabel,
    required this.createAccountLabel,
    super.key,
  });

  final IconData icon;

  /// The true-guest copy — "sign in or create an account to …", never the
  /// pending-approval wording.
  final String message;
  final String signInLabel;
  final String createAccountLabel;

  @override
  Widget build(BuildContext context) {
    return SimfEmptyState(
      icon: icon,
      message: message,
      action: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          FilledButton(
            onPressed: () => context.pushNamed(RouteNames.signIn),
            child: Text(signInLabel),
          ),
          TextButton(
            onPressed: () => context.pushNamed(RouteNames.signUpForm),
            child: Text(createAccountLabel),
          ),
        ],
      ),
    );
  }
}

/// The standard loading surface: the accent spinner, centered — one home for the
/// loader chrome so screens don't re-emit the raw indicator. Completes the
/// loading / error / empty triad with [SimfErrorState] and [SimfEmptyState].
class SimfLoadingState extends StatelessWidget {
  const SimfLoadingState({super.key});

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: CircularProgressIndicator(color: SimfTokens.accent),
    );
  }
}
