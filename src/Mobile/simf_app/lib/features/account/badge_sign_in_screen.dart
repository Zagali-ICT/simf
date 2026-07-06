import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_zxing/flutter_zxing.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_scanner_frame.dart';
import '../../core/widgets/simf_field_style.dart';
import 'widgets/account_sub_header.dart';

/// Part B (D-430) — badge-QR sign-in entry. The holder scans the QR printed on
/// their badge; the server resolves it and the app branches: an account that
/// already has a password goes to the normal sign-in; a passwordless account
/// goes to the set-password activation screen. Pre-login (anonymous).
///
/// The camera-first viewfinder is the KSA-Project design (node 758:4735, D-657):
/// the gold corner-bracket window over the live [ReaderWidget] camera, with the
/// scanning caption + progress bar. Where the camera is unavailable (tests,
/// no-camera devices) the same window shows over a manual-entry fallback so the
/// user is never trapped.
class BadgeSignInScreen extends ConsumerStatefulWidget {
  const BadgeSignInScreen({super.key, this.enableCamera = true});

  /// Off in widget tests (no camera) so the manual-entry path drives the flow.
  final bool enableCamera;

  @override
  ConsumerState<BadgeSignInScreen> createState() => _BadgeSignInScreenState();
}

class _BadgeSignInScreenState extends ConsumerState<BadgeSignInScreen> {
  final TextEditingController _manual = TextEditingController();

  /// Single-flight guard — the live camera fires `onScan` on every decoded
  /// frame, so without this a steady QR would launch concurrent resolves and
  /// stack duplicate navigations.
  bool _resolving = false;

  @override
  void dispose() {
    _manual.dispose();
    super.dispose();
  }

  void _onScan(Code result) {
    final code = result.text?.trim();
    if (code != null && code.isNotEmpty) {
      unawaited(_onCode(code));
    }
  }

  Future<void> _onCode(String qr) async {
    if (_resolving || qr.isEmpty) {
      return;
    }
    _resolving = true;
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
    } finally {
      if (mounted) {
        _resolving = false;
      }
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
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            AccountSubHeader(
              title: l10n.badgeScanTitle,
              onBack: _leave,
            ),
            Expanded(
              child: Center(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      SimfScannerFrame(
                        statusLabel: l10n.scanningCode,
                        camera: widget.enableCamera
                            ? ReaderWidget(
                                onScan: _onScan,
                                codeFormat: Format.qrCode,
                                showGallery: false,
                                showToggleCamera: false,
                                tryInverted: true,
                                loading: const _CameraLoading(),
                              )
                            : null,
                      ),
                      if (!widget.enableCamera) _buildManualFallback(l10n),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// The manual-entry fallback shown when the camera is unavailable, so a holder
  /// on a device without a working camera can still type the code (D-426).
  Widget _buildManualFallback(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.only(top: 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.badgeManualLabel,
            textAlign: TextAlign.center,
            style: const TextStyle(color: SimfTokens.beigeBorder, fontSize: 12),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _manual,
            style: const TextStyle(color: Colors.white),
            decoration: simfFieldDecoration(hintText: l10n.badgeManualField),
            onSubmitted: (value) => unawaited(_onCode(value.trim())),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: () => unawaited(_onCode(_manual.text.trim())),
            style: FilledButton.styleFrom(
              backgroundColor: SimfTokens.accent,
              minimumSize: const Size.fromHeight(48),
              shape: const RoundedRectangleBorder(
                borderRadius: SimfTokens.borderRadiusSmall,
              ),
            ),
            child: Text(
              l10n.badgeResolveButton,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 16,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The camera window's loading placeholder (before the native reader is ready).
class _CameraLoading extends StatelessWidget {
  const _CameraLoading();

  @override
  Widget build(BuildContext context) {
    return const ColoredBox(color: Colors.black);
  }
}
