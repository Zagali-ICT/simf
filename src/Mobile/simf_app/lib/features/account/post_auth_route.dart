import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// D-374 — the single post-auth routing rule (owner: "if user not add their
/// profile, must show them add profile as first stage"). The completeness
/// flag is server-computed and rides on the sign-in hydration
/// (`/app/users/me` → `profileComplete`), so no extra probe is needed.
/// Used by BOTH the password sign-in and the 2FA OTP completion.
void routeAfterAuth(BuildContext context, WidgetRef ref) {
  // `accessibility-server-sync` — this is the one seam every sign-in path runs
  // through (password, 2FA completion, badge password), so it is where the
  // account's accessibility preferences are pulled down onto this device.
  // Fire-and-forget and failure-swallowing by contract: routing must never wait
  // on, or fail because of, a preferences sync (the local prefs cache stays
  // authoritative until the next successful one).
  unawaited(ref.read(accessibilitySyncProvider).hydrate());

  final state = ref.read(authControllerProvider);
  if (state is AuthStateSignedIn && !state.session.user.profileComplete) {
    context.goNamed(RouteNames.signUpVisitor);
    return;
  }
  context.go('/');
}
