import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_zxing/flutter_zxing.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Part B (D-430) — badge-QR sign-in entry. The holder scans the QR printed on
/// their badge; the server resolves it and the app branches: an account that
/// already has a password goes to the normal sign-in; a passwordless account
/// goes to the set-password activation screen. Pre-login (anonymous). Mirrors
/// the contact-scan screen: native ZXing reader (no Google Play Services),
/// camera-off-by-default (D-426), manual-entry fallback.
class BadgeSignInScreen extends ConsumerStatefulWidget {
  const BadgeSignInScreen({super.key, this.enableCamera = true});

  /// Off in widget tests (no camera) so the manual-entry path can be exercised.
  final bool enableCamera;

  @override
  ConsumerState<BadgeSignInScreen> createState() => _BadgeSignInScreenState();
}

class _BadgeSignInScreenState extends ConsumerState<BadgeSignInScreen> {
  final TextEditingController _manual = TextEditingController();
  bool _processing = false;
  bool _cameraOn = false;
  String? _lastHandled;

  @override
  void dispose() {
    _manual.dispose();
    super.dispose();
  }

  void _onScan(Code code) {
    if (_processing || !code.isValid) {
      return;
    }
    final raw = code.text?.trim() ?? '';
    if (raw.isEmpty || raw == _lastHandled) {
      return;
    }
    _lastHandled = raw;
    unawaited(_handle(raw));
  }

  Future<void> _handle(String token) async {
    final qr = token.trim();
    if (qr.isEmpty || _processing) {
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() => _processing = true);
    try {
      final result = await ref.read(authRepositoryProvider).resolveBadge(qrId: qr);
      if (!mounted) {
        return;
      }
      if (!result.found) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(l10n.badgeNotRecognised)),
        );
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
      ScaffoldMessenger.of(context).showSnackBar(
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
        setState(() => _processing = false);
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
    // The whole page scrolls and the manual-entry field is the primary,
    // always-visible path — so the screen can never trap the user even when the
    // camera misbehaves on EMUI/Huawei (camera renders black + swallows input,
    // D-426). The camera is an explicit, bounded opt-in below it, and there are
    // two reliable exits (the AppBar back + a body "Back" button).
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) {
          _leave();
        }
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text(l10n.badgeScanTitle),
          leading: IconButton(
            icon: const Icon(Icons.arrow_back),
            tooltip: MaterialLocalizations.of(context).backButtonTooltip,
            onPressed: _leave,
          ),
        ),
        body: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(SimfTokens.space4),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                Text(
                  l10n.badgeScanHint,
                  style: const TextStyle(
                    color: SimfTokens.inkMuted,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
                const SizedBox(height: SimfTokens.space4),
                // Primary path — enter the badge code (always usable).
                Text(
                  l10n.badgeManualLabel,
                  style: const TextStyle(
                    color: SimfTokens.inkMuted,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
                const SizedBox(height: SimfTokens.space2),
                TextField(
                  controller: _manual,
                  textDirection: TextDirection.ltr,
                  enabled: !_processing,
                  decoration: InputDecoration(
                    labelText: l10n.badgeManualField,
                    border: const OutlineInputBorder(),
                  ),
                  onSubmitted: (value) => unawaited(_handle(value)),
                ),
                const SizedBox(height: SimfTokens.space3),
                FilledButton(
                  onPressed: _processing
                      ? null
                      : () => unawaited(_handle(_manual.text)),
                  style: FilledButton.styleFrom(
                    minimumSize: const Size.fromHeight(48),
                  ),
                  child: _processing
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Text(l10n.badgeResolveButton),
                ),
                const SizedBox(height: SimfTokens.space4),
                Row(
                  children: <Widget>[
                    const Expanded(child: Divider()),
                    Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: SimfTokens.space3,
                      ),
                      child: Text(
                        l10n.orDividerLabel,
                        style: const TextStyle(
                          color: SimfTokens.inkMuted,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                    ),
                    const Expanded(child: Divider()),
                  ],
                ),
                const SizedBox(height: SimfTokens.space4),
                // Camera — explicit, bounded opt-in with an out-of-surface stop.
                if (widget.enableCamera) _buildCameraSection(l10n),
                const SizedBox(height: SimfTokens.space4),
                Center(
                  child: TextButton(
                    onPressed: _processing ? null : _leave,
                    child: Text(l10n.badgeCancel),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  /// The camera is opt-in and bounded (320 px) — Huawei composites the
  /// platform-view only when bounded (D-423), and a small box keeps the AppBar
  /// + the "Stop camera" button (rendered OUTSIDE the camera surface) reachable
  /// even when the live camera swallows on-surface taps (D-426). The ZXing
  /// overlay's own button also stops the camera.
  Widget _buildCameraSection(AppL10n l10n) {
    if (!_cameraOn) {
      return OutlinedButton.icon(
        onPressed: _processing ? null : () => setState(() => _cameraOn = true),
        icon: const Icon(Icons.qr_code_scanner),
        label: Text(l10n.scanStartCamera),
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(48),
        ),
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Align(
          alignment: AlignmentDirectional.centerStart,
          child: TextButton.icon(
            onPressed: () => setState(() => _cameraOn = false),
            icon: const Icon(Icons.close),
            label: Text(l10n.badgeStopCamera),
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        SizedBox(
          height: 320,
          child: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              ReaderWidget(
                onScan: _onScan,
                codeFormat: Format.qrCode,
                showGallery: false,
                showToggleCamera: false,
                tryInverted: true,
                onActionSecondButton: () => setState(() => _cameraOn = false),
                actionSecondButtonIcon: const Icon(Icons.close),
                loading: const ColoredBox(
                  color: SimfTokens.field,
                  child: Center(child: CircularProgressIndicator()),
                ),
              ),
              if (_processing)
                const ColoredBox(
                  color: Color(0x66000000),
                  child: Center(child: CircularProgressIndicator()),
                ),
            ],
          ),
        ),
      ],
    );
  }
}
