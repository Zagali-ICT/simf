import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/account/data/profile_repository.dart' show referenceNumberProvider;
import 'package:simf_app/features/badge/data/badge_identity_provider.dart';
import 'package:simf_app/features/badge/widgets/badge_actions.dart';
import 'package:simf_app/features/badge/widgets/badge_qr_card.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Entry badge — بطاقة الدخول · route: `RouteNames.badge` · Figma 758:1469
/// Contract: auth-gated; the identity comes from the shipped My-Area layer
/// (`GET /app/account/dashboard`, `RequireApprovedAccount`) and the QR encodes
/// the opaque `qrId` only. A pending account (null `qrId`) keeps the pending
/// state; load failures keep the retry surface (Page_014 L-1).
class BadgeScreen extends ConsumerStatefulWidget {
  const BadgeScreen({super.key});

  @override
  ConsumerState<BadgeScreen> createState() => _BadgeScreenState();
}

class _BadgeScreenState extends ConsumerState<BadgeScreen> {
  CurrentUser? get _currentUser {
    final auth = ref.read(authControllerProvider);
    return auth is AuthStateSignedIn ? auth.session.user : null;
  }

  /// Owner rule: every data page pulls to refresh (CLAUDE.md section 13.6).
  /// The badge was the one data screen with NO refresh hook anywhere, and it
  /// was missing where it mattered most: a pending account pulls here to find
  /// out it has been approved, and until then had to restart the app to see it.
  /// Re-running [badgeIdentityProvider] re-checks approval by construction —
  /// the provider reads the auth state and the dashboard 403s while unapproved.
  ///
  /// A signed-OUT visitor has nothing to re-fetch, so the gesture completes
  /// without a call. That guard is about the PULL only — `build` still watches
  /// [referenceNumberProvider], which has no guest short-circuit, so a guest
  /// already fired one profile read on mount. Fixing that belongs to the
  /// provider, not here.
  ///
  /// The reference number is re-read unconditionally, and deliberately: it is
  /// rendered only on the issued-badge branch, so re-reading it while the
  /// pending state shows looks like waste — but that is exactly the transition
  /// this method exists for. The provider is `autoDispose` and swallows a
  /// failure to `null`, so a pending account holds a null it will never shed on
  /// its own; without this the newly-approved badge would show the `qrId` tail
  /// fallback instead of its real `SIMF-2026-…` reference.
  Future<void> _refresh() async {
    if (_currentUser == null) {
      return;
    }
    // Two independent endpoints (dashboard + profile), so they run together.
    await Future.wait<void>(<Future<void>>[
      ref.read(badgeIdentityProvider.notifier).recheck(),
      refreshAsync(ref, referenceNumberProvider.future),
    ]);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // The displayed ID is the reference number (SIMF-2026-…), not the qrId the
    // QR encodes. Best-effort; falls back to the qrId tail while it loads.
    final referenceNumber = ref.watch(referenceNumberProvider).asData?.value;
    return SimfPageShell(
      title: l10n.badgeTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.badge,
      showSweep: true,
      body: _buildBody(l10n, referenceNumber),
    );
  }

  Widget _buildBody(AppL10n l10n, String? referenceNumber) {
    if (_currentUser == null) {
      // BUG-013 — a TRUE guest (no account at all). Answered BEFORE the
      // provider: the bottom nav switches tabs inside the shell, so the
      // router's auth redirect never runs and a signed-out visitor lands here.
      // Not wrapped in a refresh host either — there is nothing to re-fetch,
      // and the CTA is the way forward here, not a pull.
      return SimfGuestPrompt(
        icon: Icons.qr_code_2_outlined,
        message: l10n.badgeGuestBody,
        signInLabel: l10n.guestSignInCta,
        createAccountLabel: l10n.signUpButton,
      );
    }
    return ref.watch(badgeIdentityProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => SimfRefreshableMessage(
            onRefresh: _refresh,
            child: SimfErrorState(
              message: l10n.badgeError,
              retryLabel: l10n.retryLabel,
              // The same refresh the pull runs, so Retry and pull on the very
              // same surface do the same thing.
              onRetry: () => unawaited(_refresh()),
            ),
          ),
          data: (identity) {
            // Null is "not Approved" (see [badgeIdentityProvider]). Refreshable
            // because this is the state a user waits in, and the pull is how
            // they discover approval has landed.
            if (identity == null) {
              return SimfRefreshableMessage(
                onRefresh: _refresh,
                child: SimfEmptyState(
                  icon: Icons.lock_outline,
                  message: l10n.badgeNotApprovedBody,
                ),
              );
            }
            final qrId = identity.qrId?.trim() ?? '';
            if (qrId.isEmpty) {
              // Approved but the badge is not issued yet (Page_014 L-1).
              return SimfRefreshableMessage(
                onRefresh: _refresh,
                child: SimfEmptyState(
                  icon: Icons.qr_code_2_outlined,
                  message: l10n.badgePendingBody,
                ),
              );
            }
            return _buildBadge(l10n, identity, qrId, referenceNumber);
          },
        );
  }

  Widget _buildBadge(
    AppL10n l10n,
    MyAreaIdentity identity,
    String qrId,
    String? referenceNumber,
  ) {
    return SimfPullToRefresh(
      onRefresh: _refresh,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          BadgeQrCard(
            l10n: l10n,
            identity: identity,
            qrId: qrId,
            maskedId: maskedBadgeId(referenceNumber ?? qrId),
          ),
          const SizedBox(height: SimfTokens.space4),
          // DEF-EXH-005 — the actions gate on the signed-in app ROLE, not on
          // the dashboard's isVisitor flag (which is false for every partner
          // type, so Staff / Moderator / Media / Sponsor were all shown the
          // exhibitor-only scan button and then bounced by the router).
          BadgeActions(
            l10n: l10n,
            role: _currentUser?.effectiveAppRole ?? AppRole.guest,
          ),
        ],
      ),
    );
  }
}

/// The opaque badge id with all but the last 4 characters masked — the strip
/// shows a recognisable tail without exposing the full scan value on screen.
String maskedBadgeId(String qrId) {
  if (qrId.length <= 4) {
    return qrId;
  }
  return '•••• ${qrId.substring(qrId.length - 4)}';
}
