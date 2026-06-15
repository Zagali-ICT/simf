import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_zxing/flutter_zxing.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/scan_start_prompt.dart';
import 'data/exhibitor_repository.dart';

/// D-426 — مسح بطاقة زائر / scan a visitor's entry-badge QR (exhibitor "Other"
/// lead capture). Mirrors the contact scanner's robust shell (ZXing reader +
/// camera-off-until-tap + AppBar back + handle-once, so it never loops or
/// traps the user). On a successful scan the visitor is captured server-side and
/// the screen routes to My Visitors. A manual-entry field is the fallback when
/// the camera is unavailable. Approved + non-visitor only (a visitor-tier caller
/// gets 403 → a toast).
class ScanVisitorScreen extends ConsumerStatefulWidget {
  const ScanVisitorScreen({super.key, this.enableCamera = true});

  /// Off in widget tests (no camera) so the manual-entry path drives the flow.
  final bool enableCamera;

  @override
  ConsumerState<ScanVisitorScreen> createState() => _ScanVisitorScreenState();
}

class _ScanVisitorScreenState extends ConsumerState<ScanVisitorScreen> {
  final TextEditingController _manual = TextEditingController();
  bool _processing = false;
  // Camera starts only on tap — keeps the on-screen back / manual entry usable
  // where the live camera grabs taps window-wide (Huawei/EMUI). (D-426)
  bool _cameraOn = false;
  String? _lastHandled;

  @override
  void dispose() {
    _manual.dispose();
    super.dispose();
  }

  void _leave() {
    final navigator = Navigator.of(context);
    if (navigator.canPop()) {
      navigator.pop();
    } else {
      GoRouter.maybeOf(context)?.goNamed(RouteNames.badge);
    }
  }

  // Handle-once: the ZXing reader fires repeatedly while a code is in view.
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

  Future<void> _handle(String qrId) async {
    final code = qrId.trim();
    if (code.isEmpty || _processing) {
      return;
    }
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _processing = true);
    try {
      await ref.read(exhibitorRepositoryProvider).scanByBadge(code);
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.scanVisitorCaptured)),
      );
      // Captured server-side — show the updated My Visitors list.
      context.goNamed(RouteNames.myVisitors);
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      // Handle-once: keep the code debounced so the same badge doesn't re-fire
      // in a loop; the manual field re-tries on demand.
      messenger.showSnackBar(SnackBar(content: Text(_failureText(l10n, e))));
    } finally {
      if (mounted) {
        setState(() => _processing = false);
      }
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
    final canPop = Navigator.of(context).canPop();
    return PopScope(
      canPop: canPop,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) {
          GoRouter.maybeOf(context)?.goNamed(RouteNames.badge);
        }
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text(l10n.scanVisitorTitle),
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
                  ? (constraints.maxHeight * 0.78)
                      .clamp(240.0, constraints.maxHeight)
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
      return _Placeholder(label: l10n.scanContactCameraUnavailable);
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
        // ZXing reader (native, no Google Play Services) — works on Huawei/HMS.
        ReaderWidget(
          onScan: _onScan,
          codeFormat: Format.qrCode,
          showGallery: false,
          showToggleCamera: false,
          tryInverted: true,
          // Back inside flutter_zxing's overlay — tappable over the live camera
          // where the AppBar back is swallowed by the camera surface (D-426).
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
            l10n.scanContactManualLabel,
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
                  decoration: InputDecoration(
                    labelText: l10n.scanContactManualField,
                    border: const OutlineInputBorder(),
                  ),
                  onSubmitted: (v) => unawaited(_handle(v)),
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              FilledButton(
                onPressed:
                    _processing ? null : () => unawaited(_handle(_manual.text)),
                child: Text(l10n.scanContactResolve),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _Placeholder extends StatelessWidget {
  const _Placeholder({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.field,
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(Icons.qr_code_scanner, size: 56, color: SimfTokens.inkMuted),
            const SizedBox(height: SimfTokens.space2),
            Text(label, style: const TextStyle(color: SimfTokens.inkMuted)),
          ],
        ),
      ),
    );
  }
}
