import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../account/data/profile_repository.dart' show referenceNumberProvider;
import '../myarea/data/myarea_models.dart';
import '../myarea/data/myarea_repository.dart';
import 'widgets/badge_actions.dart';
import 'widgets/badge_qr_card.dart';

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
  MyAreaIdentity? _identity;

  @override
  void initState() {
    super.initState();
    final user = _currentUser;
    if (user != null && user.isApproved) {
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
    if (_notApproved) {
      // Signed-in but not approved — show "account not approved", not the QR.
      return SimfEmptyState(
        icon: Icons.lock_outline,
        message: l10n.badgeNotApprovedBody,
      );
    }
    final identity = _identity;
    if (_error || identity == null) {
      return SimfErrorState(
        message: l10n.badgeError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }
    final qrId = identity.qrId?.trim() ?? '';
    if (qrId.isEmpty) {
      // Pending approval — the badge is issued once approved (Page_014 L-1).
      return SimfEmptyState(
        icon: Icons.qr_code_2_outlined,
        message: l10n.badgePendingBody,
      );
    }
    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        BadgeQrCard(
          l10n: l10n,
          identity: identity,
          qrId: qrId,
          maskedId: maskedBadgeId(referenceNumber ?? qrId),
        ),
        const SizedBox(height: SimfTokens.space4),
        // DEF-EXH-005 — the actions gate on the signed-in app ROLE, not on the
        // dashboard's isVisitor flag (which is false for every partner type, so
        // Staff / Moderator / Media / Sponsor were all shown the exhibitor-only
        // scan button and then bounced by the router).
        BadgeActions(
          l10n: l10n,
          role: _currentUser?.effectiveAppRole ?? AppRole.guest,
        ),
      ],
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
