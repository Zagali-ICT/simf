import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../myarea/data/myarea_models.dart';
import '../myarea/data/myarea_repository.dart';

/// Page 032 — بطاقة الدخول · Entry badge (#32, `/badge`).
///
/// **Auth-gated** (route 32 is in `_authenticatedRoutes`). The screen reuses the
/// shipped My-Area data layer (`myAreaRepositoryProvider.getDashboard()` →
/// `GET /app/account/dashboard`, `RequireApprovedAccount`) and renders **only**
/// the identity's `qrId` as a scannable entry badge, with the visitor's localized
/// name and a "show this at entry" hint. When `qrId` is null/empty — a pending
/// account whose badge is not yet issued (Page_014 L-1) — it shows the pending
/// state instead. loading / error+retry are the standard surfaces. The QR encodes
/// the opaque `qrId` only; the final visuals come from SIMF-VID-001.
class BadgeScreen extends ConsumerStatefulWidget {
  const BadgeScreen({super.key});

  @override
  ConsumerState<BadgeScreen> createState() => _BadgeScreenState();
}

class _BadgeScreenState extends ConsumerState<BadgeScreen> {
  bool _loading = true;
  bool _error = false;
  MyAreaIdentity? _identity;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
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
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = true;
        _identity = null;
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.badgeTitle)),
      bottomNavigationBar: const SimfBottomNav(current: SimfTab.badge),
      body: SafeArea(top: false, child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    final identity = _identity;
    if (_error || identity == null) {
      return _ErrorState(
        message: l10n.badgeError,
        onRetry: () => unawaited(_load()),
      );
    }
    final qrId = identity.qrId?.trim() ?? '';
    if (qrId.isEmpty) {
      return _PendingState(message: l10n.badgePendingBody);
    }
    return _Badge(
      qrId: qrId,
      name: identity.localizedName(l10n.isArabic),
      hint: l10n.badgeShowAtEntry,
    );
  }
}

/// The issued badge: a centred QR inside a white card with the visitor's name
/// below it and the "show this at entry" hint.
class _Badge extends StatelessWidget {
  const _Badge({required this.qrId, required this.name, required this.hint});

  final String qrId;
  final String name;
  final String hint;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Card(
              margin: EdgeInsets.zero,
              color: SimfTokens.surface,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
              ),
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space6),
                child: QrImageView(
                  data: qrId,
                  version: QrVersions.auto,
                  size: 240,
                  gapless: true,
                ),
              ),
            ),
            const SizedBox(height: SimfTokens.space5),
            Text(
              name,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textXl,
              ),
            ),
            const SizedBox(height: SimfTokens.space2),
            Text(
              hint,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: SimfTokens.inkMuted,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Shown when the account has no `qrId` yet (pending approval — Page_014 L-1):
/// the badge is issued once the account is approved.
class _PendingState extends StatelessWidget {
  const _PendingState({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.qr_code_2_outlined,
              size: 56,
              color: SimfTokens.inkMuted,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.inkMuted),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
