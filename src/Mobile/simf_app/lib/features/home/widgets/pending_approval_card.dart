import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/registration/widgets/registration_sign_out_link.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// The home block for a signed-in but **not-yet-approved** account (D-666): the
/// "awaiting approval" note, a single **Registration status** button that opens
/// the status gate and **re-checks on return** (so an approval granted there
/// flips Home into the full experience), and a small **sign-out** link in the
/// registration-gate style. It replaces the guest "sign in" CTA, which is wrong
/// for an already-logged-in user. (D-668 collapsed the earlier duplicate
/// Re-check + Registration-status buttons into this one navigate-and-refresh
/// action and added the sign-out link.)
class PendingApprovalCard extends ConsumerWidget {
  const PendingApprovalCard({required this.l10n, super.key});

  final AppL10n l10n;

  /// Opens the registration-status gate, then re-fetches the account on return:
  /// if it was approved there, the auth-state change rebuilds Home into the
  /// full experience. `pushNamed` uses [context] synchronously before the
  /// await, so there is no use-after-await on the context.
  Future<void> _openStatus(BuildContext context, WidgetRef ref) async {
    await context.pushNamed(RouteNames.registrationStatus);
    try {
      await ref.read(authControllerProvider.notifier).refreshCurrentUser();
    } on AuthFailure {
      // A stale session flips auth to signed-out and the router gate handles
      // it.
    }
  }

  /// Plain sign-out (no confirm) → sign-in, matching the registration gate. The
  /// router is captured before the await so [context] is not used after it.
  Future<void> _signOut(BuildContext context, WidgetRef ref) async {
    final router = GoRouter.of(context);
    await ref.read(authControllerProvider.notifier).signOut();
    router.goNamed(RouteNames.signIn);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space3),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.accent,
          width: SimfTokens.hairlineBold,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              const Icon(
                Icons.hourglass_top,
                color: SimfTokens.accent,
                size: SimfTokens.pendingApprovalCardSize,
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: Text(
                  l10n.homePendingApprovalNote,
                  textAlign: TextAlign.start,
                  style: SimfTokens.bodyBeige,
                ),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space3),
          FilledButton(
            onPressed: () => unawaited(_openStatus(context, ref)),
            child: Text(l10n.registrationStatusButton),
          ),
          RegistrationSignOutLink(
            label: l10n.signOutLink,
            onTap: () => unawaited(_signOut(context, ref)),
          ),
        ],
      ),
    );
  }
}
