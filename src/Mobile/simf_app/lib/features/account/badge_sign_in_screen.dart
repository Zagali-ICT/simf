import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/widgets/qr_scan_view.dart';

/// Part B (D-430) — badge-QR sign-in entry. The holder scans (or types) the QR
/// printed on their badge; the server resolves it and the app branches: an
/// account that already has a password goes to the normal sign-in; a passwordless
/// account goes to the set-password activation screen. Pre-login (anonymous).
/// The UI is the shared [QrScanView] (manual-first, bounded opt-in camera,
/// never traps the user — D-426).
class BadgeSignInScreen extends ConsumerStatefulWidget {
  const BadgeSignInScreen({super.key, this.enableCamera = true});

  /// Off in widget tests (no camera) so the manual-entry path drives the flow.
  final bool enableCamera;

  @override
  ConsumerState<BadgeSignInScreen> createState() => _BadgeSignInScreenState();
}

class _BadgeSignInScreenState extends ConsumerState<BadgeSignInScreen> {
  Future<void> _onCode(String qr) async {
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      final result = await ref.read(authRepositoryProvider).resolveBadge(qrId: qr);
      if (!mounted) {
        return;
      }
      if (!result.found) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.badgeNotRecognised)));
        return;
      }
      if (result.hasPassword) {
        // Already activated — continue with the normal password + OTP sign-in.
        context.goNamed(RouteNames.signIn);
        return;
      }
      // Passwordless — set the first password (activation).
      context.goNamed(
        RouteNames.badgeActivation,
        queryParameters: <String, String>{
          'qrId': qr,
          'needsEmail': result.needsEmail ? '1' : '0',
          if (result.maskedEmail != null) 'masked': result.maskedEmail!,
        },
      );
    } on AuthFailure catch (failure) {
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            failure is NetworkUnavailable
                ? l10n.networkErrorBody
                : l10n.badgeScanError,
          ),
        ),
      );
    }
  }

  void _leave() {
    if (context.canPop()) {
      context.pop();
      return;
    }
    context.goNamed(RouteNames.signIn);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return QrScanView(
      title: l10n.badgeScanTitle,
      hint: l10n.badgeScanHint,
      fieldLabel: l10n.badgeManualField,
      continueLabel: l10n.badgeResolveButton,
      cameraLabel: l10n.scanStartCamera,
      enableCamera: widget.enableCamera,
      onCode: _onCode,
      onLeave: _leave,
    );
  }
}
