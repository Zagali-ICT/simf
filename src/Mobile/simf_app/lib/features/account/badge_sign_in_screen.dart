import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/qr_scan_view.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Part B (D-430) — badge-QR sign-in entry. The holder scans the QR printed on
/// their badge; the server resolves it and the app branches: an account that
/// already has a password goes to the password-completion step; a passwordless
/// account goes to the set-password activation screen. Pre-login (anonymous).
///
/// Uses the shared [QrScanView] (D-737) so the scanner is consistent with the
/// rest of the app: camera-first with the gold viewfinder, an always-usable
/// manual-entry fallback, a single dedupe policy (no more repeated "not
/// recognised" snackbars on a steady QR), and a visible camera-error state
/// instead of a black dead-end when the camera permission is denied. The
/// camera-first look keeps the KSA-Project design (node 758:4735, D-657).
///
/// Route: `RouteNames.badgeSignIn`.
/// Data: [authRepositoryProvider].
/// Perf: no list — a single-screen layout.
class BadgeSignInScreen extends ConsumerStatefulWidget {
  const BadgeSignInScreen({super.key, this.enableCamera = true});

  /// Off in widget tests (no camera) so the manual-entry path drives the flow.
  final bool enableCamera;

  @override
  ConsumerState<BadgeSignInScreen> createState() => _BadgeSignInScreenState();
}

class _BadgeSignInScreenState extends ConsumerState<BadgeSignInScreen> {
  Future<void> _onCode(String qr) async {
    final code = qr.trim();
    if (code.isEmpty) {
      return;
    }
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      final result =
          await ref.read(authRepositoryProvider).resolveBadge(qrId: code);
      if (!mounted) {
        return;
      }
      if (!result.found) {
        messenger
            .showSnackBar(SnackBar(content: Text(l10n.badgeNotRecognised)));
        return;
      }
      if (result.hasPassword) {
        // Already activated — finish sign-in with the password step (D-738),
        // carrying the resolved name + masked email so no email is typed.
        context.goNamed(
          RouteNames.badgePassword,
          queryParameters: <String, String>{
            'qrId': code,
            if (result.displayName != null) 'name': result.displayName!,
            if (result.maskedEmail != null) 'masked': result.maskedEmail!,
          },
        );
        return;
      }
      // Passwordless — set the first password (activation).
      context.goNamed(
        RouteNames.badgeActivation,
        queryParameters: <String, String>{
          'qrId': code,
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
      title: l10n.badgePortalSignInTitle,
      fieldLabel: l10n.badgeManualField,
      continueLabel: l10n.badgeResolveButton,
      enableCamera: widget.enableCamera,
      onCode: _onCode,
      onLeave: _leave,
    );
  }
}
