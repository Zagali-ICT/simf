import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_svg_icon.dart';
import '../myarea/data/myarea_models.dart';
import '../myarea/data/myarea_repository.dart';
import '../profile/data/profile_repository.dart' show referenceNumberProvider;

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
    return KsaPage(
      title: l10n.badgeTitle,
      onBack: () => ksaBackOrHome(context),
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
      return KsaEmptyState(
        icon: Icons.lock_outline,
        message: l10n.badgeNotApprovedBody,
      );
    }
    final identity = _identity;
    if (_error || identity == null) {
      return KsaErrorState(
        message: l10n.badgeError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }
    final qrId = identity.qrId?.trim() ?? '';
    if (qrId.isEmpty) {
      // Pending approval — the badge is issued once approved (Page_014 L-1).
      return KsaEmptyState(
        icon: Icons.qr_code_2_outlined,
        message: l10n.badgePendingBody,
      );
    }
    return _Badge(
      l10n: l10n,
      identity: identity,
      qrId: qrId,
      referenceNumber: referenceNumber,
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

/// The issued badge (frame node tree under 758:1469): the gold-bordered white
/// card (QR + hint + gold identity strip) and the add-person action below it.
class _Badge extends StatelessWidget {
  const _Badge({
    required this.l10n,
    required this.identity,
    required this.qrId,
    this.referenceNumber,
  });

  final AppL10n l10n;
  final MyAreaIdentity identity;
  final String qrId;

  /// The human reference number (SIMF-2026-…) shown on the ID line; the QR still
  /// encodes [qrId]. Falls back to the qrId tail until it loads.
  final String? referenceNumber;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final name = identity.localizedName(isArabic);
    final tier = identity.localizedTier(isArabic);
    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        Container(
          padding: const EdgeInsets.all(SimfTokens.space4),
          decoration: BoxDecoration(
            color: SimfTokens.surface,
            borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
            // Frame 758:1469 — the gold card edge is a thin 1-px stroke.
            border: Border.all(color: SimfTokens.accent, width: 1),
          ),
          child: Column(
            children: <Widget>[
              Padding(
                // Frame 758:1477 — the QR (≈265 on the 343 card) sits ~24 inset
                // inside the card padding; sized to the available width so it
                // stays proportional on any device.
                padding: const EdgeInsets.all(SimfTokens.space6),
                child: LayoutBuilder(
                  builder: (context, constraints) => QrImageView(
                    data: qrId,
                    version: QrVersions.auto,
                    size: constraints.maxWidth,
                    gapless: true,
                    // Frame 758:1477 — rounded finder eyes, pure-black modules.
                    eyeStyle: const QrEyeStyle(
                      eyeShape: QrEyeShape.circle,
                      color: Colors.black,
                    ),
                    dataModuleStyle: const QrDataModuleStyle(
                      dataModuleShape: QrDataModuleShape.square,
                      color: Colors.black,
                    ),
                  ),
                ),
              ),
              Text(
                l10n.badgeScanToEnter,
                // Frame 758:1469 — black, 16px, slight negative tracking.
                style: const TextStyle(
                  color: Colors.black,
                  fontSize: SimfTokens.textLg,
                  letterSpacing: -0.366,
                ),
              ),
              const SizedBox(height: SimfTokens.space4),
              Container(
                padding: const EdgeInsets.all(SimfTokens.space2),
                decoration: BoxDecoration(
                  color: SimfTokens.accent,
                  borderRadius: BorderRadius.circular(SimfTokens.radius),
                ),
                child: Row(
                  children: <Widget>[
                    // Frame 758:1469 — a 64-px rounded box; the SIMF brand-mark
                    // fallback on its navy box stays visible on the gold strip,
                    // replaced by the photo when present.
                    KsaAvatar(
                      name: name,
                      currentUser: true,
                      size: 64,
                    ),
                    const SizedBox(width: SimfTokens.space2),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Text(
                            name,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            // Frame 758:1469 — 18px SemiBold white.
                            style: const TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.w600,
                              fontSize: SimfTokens.textTitle,
                            ),
                          ),
                          if (tier != null) ...<Widget>[
                            const SizedBox(height: SimfTokens.space2),
                            Text(
                              tier,
                              // Frame 758:1469 — 12px, muted #F0F0F0.
                              style: const TextStyle(
                                color: SimfTokens.onGoldMuted,
                                fontSize: SimfTokens.textSm,
                              ),
                            ),
                          ],
                          const SizedBox(height: SimfTokens.space2),
                          Text(
                            'ID · ${maskedBadgeId(referenceNumber ?? qrId)}',
                            textDirection: TextDirection.ltr,
                            // Frame 758:1469 — 12px, muted #F0F0F0.
                            style: const TextStyle(
                              color: SimfTokens.onGoldMuted,
                              fontSize: SimfTokens.textSm,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        // The add-person action → the FDS-014 contact scanner.
        OutlinedButton.icon(
          onPressed: () => context.pushNamed(RouteNames.scanContact),
          style: OutlinedButton.styleFrom(
            minimumSize: const Size.fromHeight(48),
            // Frame 758:1469 — gold 1-px border.
            side: const BorderSide(color: SimfTokens.accent, width: 1),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
          ),
          icon: const SimfSvgIcon(
            'assets/icons/badge_scan.svg',
            size: 24,
            color: Colors.white,
          ),
          label: Text(
            l10n.badgeAddPerson,
            // Frame 758:1469 — 16px Bold white.
            style: const TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textLg,
            ),
          ),
        ),
      ],
    );
  }
}

