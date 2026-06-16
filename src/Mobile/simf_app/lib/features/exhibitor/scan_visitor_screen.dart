import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/widgets/qr_scan_view.dart';
import 'data/exhibitor_repository.dart';

/// D-426 — مسح بطاقة زائر / scan a visitor's entry-badge QR (exhibitor "Other"
/// lead capture). On a successful scan the visitor is captured server-side and
/// the screen routes to My Visitors. Uses the shared [QrScanView] (D-430) so the
/// manual-entry path always works and the camera can never trap the user on
/// EMUI. Approved + non-visitor only (a visitor-tier caller gets 403 → a toast).
class ScanVisitorScreen extends ConsumerStatefulWidget {
  const ScanVisitorScreen({super.key, this.enableCamera = true});

  /// Off in widget tests (no camera) so the manual-entry path drives the flow.
  final bool enableCamera;

  @override
  ConsumerState<ScanVisitorScreen> createState() => _ScanVisitorScreenState();
}

class _ScanVisitorScreenState extends ConsumerState<ScanVisitorScreen> {
  void _leave() {
    final router = GoRouter.maybeOf(context);
    if (router == null) {
      if (Navigator.of(context).canPop()) {
        Navigator.of(context).pop();
      }
      return;
    }
    if (router.canPop()) {
      router.pop();
    } else {
      router.goNamed(RouteNames.badge);
    }
  }

  Future<void> _onCode(String qrId) async {
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(exhibitorRepositoryProvider).scanByBadge(qrId.trim());
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(SnackBar(content: Text(l10n.scanVisitorCaptured)));
      // Captured server-side — show the updated My Visitors list.
      context.goNamed(RouteNames.myVisitors);
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(SnackBar(content: Text(_failureText(l10n, e))));
    }
  }

  String _failureText(AppL10n l10n, ApiFailure e) {
    switch (e.httpStatus) {
      case 404:
        return l10n.scanVisitorNotFound;
      case 403:
        return l10n.scanVisitorForbidden;
      default:
        return l10n.scanVisitorError;
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return QrScanView(
      title: l10n.scanVisitorTitle,
      fieldLabel: l10n.scanContactManualField,
      continueLabel: l10n.scanContactResolve,
      cameraLabel: l10n.scanStartCamera,
      enableCamera: widget.enableCamera,
      onCode: _onCode,
      onLeave: _leave,
    );
  }
}
