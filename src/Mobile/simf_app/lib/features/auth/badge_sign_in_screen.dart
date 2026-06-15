import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_zxing/flutter_zxing.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/scan_start_prompt.dart';

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
          child: LayoutBuilder(
            builder: (context, constraints) {
              final cameraHeight = widget.enableCamera
                  ? (constraints.maxHeight * 0.72).clamp(240.0, constraints.maxHeight)
                  : 220.0;
              return Column(
                children: <Widget>[
                  SizedBox(
                    width: double.infinity,
                    height: cameraHeight,
                    child: _buildCamera(l10n),
                  ),
                  Expanded(
                    child: SingleChildScrollView(child: _buildManual(l10n)),
                  ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }

  Widget _buildCamera(AppL10n l10n) {
    if (!widget.enableCamera) {
      return const ColoredBox(color: SimfTokens.field);
    }
    if (!_cameraOn) {
      return ScanStartPrompt(
        label: l10n.scanStartCamera,
        onStart: () => setState(() => _cameraOn = true),
      );
    }
    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        ReaderWidget(
          onScan: _onScan,
          codeFormat: Format.qrCode,
          showGallery: false,
          showToggleCamera: false,
          tryInverted: true,
          onActionSecondButton: _leave,
          actionSecondButtonIcon: const Icon(Icons.arrow_back),
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
    );
  }

  Widget _buildManual(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.badgeScanHint,
            style: const TextStyle(
              color: SimfTokens.inkMuted,
              fontSize: SimfTokens.textSm,
            ),
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(
            l10n.badgeManualLabel,
            style: const TextStyle(
              color: SimfTokens.inkMuted,
              fontSize: SimfTokens.textSm,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Row(
            children: <Widget>[
              Expanded(
                child: TextField(
                  controller: _manual,
                  textDirection: TextDirection.ltr,
                  decoration: InputDecoration(
                    labelText: l10n.badgeManualField,
                    border: const OutlineInputBorder(),
                  ),
                  onSubmitted: (value) => unawaited(_handle(value)),
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              FilledButton(
                onPressed:
                    _processing ? null : () => unawaited(_handle(_manual.text)),
                child: Text(l10n.badgeResolveButton),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
