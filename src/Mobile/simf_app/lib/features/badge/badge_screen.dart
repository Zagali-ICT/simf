import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/account/data/profile_repository.dart' show referenceNumberProvider;
import 'package:simf_app/features/badge/widgets/badge_actions.dart';
import 'package:simf_app/features/badge/widgets/badge_qr_card.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Page 032 — بطاقة الدخول · Entry badge (#32, `/badge`), rebuilt to the
/// KSA frame **758:1469 "QR"** on the shared shell.
///
/// **Auth-gated** (route 32 in `_authenticatedRoutes`); data contract
/// unchanged: the shipped My-Area layer (`GET /app/account/dashboard`,
/// `RequireApprovedAccount`) supplies the identity, and the QR encodes the
/// opaque `qrId` only. Frame mapping: the gold-bordered **white card**
/// holding the QR, the "امسح للدخول" hint and the **gold identity strip**
/// (avatar, name, tier line, the masked `ID · …` reference), plus the
/// bordered **امسح لإضافة شخص** action → the existing contact-QR scanner
/// (`/contacts/scan`, FDS-014). A pending account (null `qrId`) keeps the
/// pending state; load failures keep the retry surface (Page_014 L-1).
class BadgeScreen extends ConsumerStatefulWidget {
  const BadgeScreen({super.key});

  @override
  ConsumerState<BadgeScreen> createState() => _BadgeScreenState();
}

class _BadgeScreenState extends ConsumerState<BadgeScreen> {
  bool _loading = true;
  bool _error = false;
  bool _notApproved = false;
  bool _signedOut = false;
  MyAreaIdentity? _identity;

  @override
  void initState() {
    super.initState();
    final user = _currentUser;
    if (user == null) {
      // BUG-013 — a TRUE guest (no account at all). The bottom nav switches
      // tabs inside the shell, so the router's auth redirect never runs and a
      // signed-out visitor lands here. Showing the not-approved copy described
      // a registration they never submitted and offered no way out; the guest
      // state gets its own copy plus a working sign-in CTA.
      _loading = false;
      _signedOut = true;
    } else if (user.isApproved) {
      unawaited(_load());
    } else {
      // Signed in but not approved: the badge is issued only on approval
      // (Page_014 L-1). Show the not-approved state instead of calling the
      // Approved-only dashboard (which would 403).
      _loading = false;
      _notApproved = true;
    }
  }

  CurrentUser? get _currentUser {
    final auth = ref.read(authControllerProvider);
    return auth is AuthStateSignedIn ? auth.session.user : null;
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
      _notApproved = false;
    });
    try {
      final dashboard = await ref.read(myAreaRepositoryProvider).getDashboard();
      if (!mounted) {
        return;
      }
      setState(() {
        _identity = dashboard.identity;
        _loading = false;
      });
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      setState(() {
        // 403 = signed-in but not approved (status drifted since boot) → the
        // not-approved state; any other failure shows the retry surface.
        _notApproved = e.httpStatus == 403;
        _error = e.httpStatus != 403;
        _identity = null;
        _loading = false;
      });
    }
  }

  /// Owner rule: every data page pulls to refresh (CLAUDE.md section 13.6).
  /// The badge was the one data screen with NO refresh hook anywhere, and it
  /// was missing where it mattered most: a pending account pulls here to find
  /// out it has been approved, and until now had to restart the app to see it.
  /// `_load()` re-checks approval by construction — the dashboard 403s while
  /// unapproved and that 403 is what sets the pending state.
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
    if (_signedOut) {
      return;
    }
    // Two independent endpoints (dashboard + profile), so they run together.
    await Future.wait<void>(<Future<void>>[
      _load(),
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
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_signedOut) {
      // No account at all — the guest copy + a way in (BUG-013). Not wrapped
      // in a refresh host: there is nothing to re-fetch, and the CTA is the
      // way forward here, not a pull.
      return SimfGuestPrompt(
        icon: Icons.qr_code_2_outlined,
        message: l10n.badgeGuestBody,
        signInLabel: l10n.guestSignInCta,
        createAccountLabel: l10n.signUpButton,
      );
    }
    if (_notApproved) {
      // Signed-in but not approved — show "account not approved", not the QR.
      // Refreshable: this is the state a user sits in while waiting, and the
      // pull is how they discover approval has landed.
      return SimfRefreshableMessage(
        onRefresh: _refresh,
        child: SimfEmptyState(
          icon: Icons.lock_outline,
          message: l10n.badgeNotApprovedBody,
        ),
      );
    }
    final identity = _identity;
    if (_error || identity == null) {
      return SimfRefreshableMessage(
        onRefresh: _refresh,
        child: SimfErrorState(
          message: l10n.badgeError,
          retryLabel: l10n.retryLabel,
          // The same refresh the pull runs: the button used to call `_load`
          // alone, so tapping Retry and pulling on the very same surface did
          // two different things.
          onRetry: () => unawaited(_refresh()),
        ),
      );
    }
    final qrId = identity.qrId?.trim() ?? '';
    if (qrId.isEmpty) {
      // Pending approval — the badge is issued once approved (Page_014 L-1).
      return SimfRefreshableMessage(
        onRefresh: _refresh,
        child: SimfEmptyState(
          icon: Icons.qr_code_2_outlined,
          message: l10n.badgePendingBody,
        ),
      );
    }
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
