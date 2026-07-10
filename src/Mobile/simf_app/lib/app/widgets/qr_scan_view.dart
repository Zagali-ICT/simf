import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_zxing/flutter_zxing.dart';

import '../../core/widgets/simf_field_style.dart';
import '../localization/app_l10n.dart';
import '../theme/tokens.dart';
import 'simf_scanner_frame.dart';

/// D-430 — the shared, EMUI-safe QR-reading screen used by every visitor-facing
/// QR scanner in the app (visitor contact scan, exhibitor lead capture). It can
/// never trap the user:
///
/// * the page scrolls and the **manual code entry is the primary, always-visible
///   path** — it works even when the camera misbehaves;
/// * the camera is an explicit, **bounded** opt-in (Huawei composites the camera
///   platform-view only when bounded, D-423) with a **"Stop camera" button
///   rendered OUTSIDE the camera surface** (so EMUI can't swallow it, D-426);
/// * there are **two reliable exits** — the header back and a body "Back" button.
///
/// D-724 (owner batch item 10): re-skinned to the navy/gold KSA-Project language
/// so it matches the badge "Share my QR" page (Figma 1701:7080) instead of the
/// old plain-Material look — a navy scaffold with a circular back header, the
/// shared beige field chrome, a gold "or" divider, and the [SimfScannerFrame]
/// gold-bracket viewfinder (node 758:4735) for the camera-on state. Only the
/// presentation changed; the EMUI-safety contract above is untouched.
///
/// The widget is display-agnostic: the host screen passes the labels + an
/// [onCode] callback that resolves / captures / branches on the scanned or typed
/// code. [onCode] is awaited with a busy overlay so the camera can't re-fire
/// while it runs; the camera de-dupes a code so the reader doesn't loop.
class QrScanView extends StatefulWidget {
  const QrScanView({
    required this.title,
    required this.fieldLabel,
    required this.continueLabel,
    required this.cameraLabel,
    required this.onCode,
    required this.onLeave,
    this.hint,
    this.enableCamera = true,
    super.key,
  });

  final String title;
  final String? hint;
  final String fieldLabel;
  final String continueLabel;
  final String cameraLabel;

  /// Resolve / capture / branch on the scanned or typed code. Awaited; the
  /// scanner shows a busy state and pauses the camera while it runs.
  final Future<void> Function(String code) onCode;

  /// Leave the screen (host owns the navigation semantics).
  final VoidCallback onLeave;

  /// Off in widget tests (no camera) so the manual-entry path drives the flow.
  final bool enableCamera;

  @override
  State<QrScanView> createState() => _QrScanViewState();
}

class _QrScanViewState extends State<QrScanView> {
  final TextEditingController _manual = TextEditingController();
  bool _processing = false;
  bool _cameraOn = false;
  String? _lastHandled;

  @override
  void dispose() {
    _manual.dispose();
    super.dispose();
  }

  // Camera path — the ZXing reader fires repeatedly while a code is in view, so
  // a code is handled once and not re-fired until a different one appears.
  void _onScan(Code code) {
    if (_processing || !code.isValid) {
      return;
    }
    final raw = code.text?.trim() ?? '';
    if (raw.isEmpty || raw == _lastHandled) {
      return;
    }
    _lastHandled = raw;
    unawaited(_process(raw));
  }

  Future<void> _process(String code) async {
    final value = code.trim();
    if (value.isEmpty || _processing) {
      return;
    }
    setState(() => _processing = true);
    try {
      await widget.onCode(value);
    } finally {
      if (mounted) {
        setState(() => _processing = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) {
          widget.onLeave();
        }
      },
      child: Scaffold(
        backgroundColor: SimfTokens.navySurface,
        body: SafeArea(
          child: Column(
            children: <Widget>[
              _ScannerHeader(title: widget.title, onBack: widget.onLeave),
              Expanded(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.all(SimfTokens.space4),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      if (widget.hint != null) ...<Widget>[
                        Text(
                          widget.hint!,
                          style: const TextStyle(
                            color: SimfTokens.beigeBorder,
                            fontSize: SimfTokens.textSm,
                          ),
                        ),
                        const SizedBox(height: SimfTokens.space4),
                      ],
                      // Primary path — type the code (always usable).
                      Text(
                        l10n.qrManualLabel,
                        style: const TextStyle(
                          color: SimfTokens.beigeBorder,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space2),
                      TextField(
                        controller: _manual,
                        textDirection: TextDirection.ltr,
                        enabled: !_processing,
                        style: const TextStyle(color: Colors.white),
                        decoration: simfFieldDecoration(hintText: widget.fieldLabel),
                        onSubmitted: (value) => unawaited(_process(value)),
                      ),
                      const SizedBox(height: SimfTokens.space3),
                      FilledButton(
                        // Disabled (not a spinner) while busy: an onCode that opens
                        // a modal stays pending, and an infinite spinner here would
                        // hang pumpAndSettle. The camera path shows its own overlay.
                        onPressed: _processing
                            ? null
                            : () => unawaited(_process(_manual.text)),
                        style: FilledButton.styleFrom(
                          minimumSize: const Size.fromHeight(48),
                        ),
                        child: Text(widget.continueLabel),
                      ),
                      if (widget.enableCamera) ...<Widget>[
                        const SizedBox(height: SimfTokens.space4),
                        Row(
                          children: <Widget>[
                            const Expanded(
                              child: Divider(color: SimfTokens.accent),
                            ),
                            Padding(
                              padding: const EdgeInsets.symmetric(
                                horizontal: SimfTokens.space3,
                              ),
                              child: Text(
                                l10n.orDividerLabel,
                                style: const TextStyle(
                                  color: SimfTokens.beigeBorder,
                                  fontSize: SimfTokens.textSm,
                                ),
                              ),
                            ),
                            const Expanded(
                              child: Divider(color: SimfTokens.accent),
                            ),
                          ],
                        ),
                        const SizedBox(height: SimfTokens.space4),
                        _buildCameraSection(l10n),
                      ],
                      const SizedBox(height: SimfTokens.space4),
                      Center(
                        child: TextButton(
                          onPressed: _processing ? null : widget.onLeave,
                          style: TextButton.styleFrom(
                            foregroundColor: SimfTokens.beigeBorder,
                          ),
                          child: Text(l10n.qrBack),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildCameraSection(AppL10n l10n) {
    if (!_cameraOn) {
      return OutlinedButton.icon(
        onPressed: _processing ? null : () => setState(() => _cameraOn = true),
        icon: const Icon(Icons.qr_code_scanner),
        label: Text(widget.cameraLabel),
        style: OutlinedButton.styleFrom(
          foregroundColor: SimfTokens.accent,
          side: const BorderSide(color: SimfTokens.accent),
          minimumSize: const Size.fromHeight(48),
        ),
      );
    }
    // The "Stop camera" control stays OUTSIDE the camera surface (D-426) — above
    // the viewfinder card, not composited by EMUI over the platform view.
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Align(
          alignment: AlignmentDirectional.centerStart,
          child: TextButton.icon(
            onPressed: () => setState(() => _cameraOn = false),
            icon: const Icon(Icons.close),
            label: Text(l10n.qrStopCamera),
            style: TextButton.styleFrom(foregroundColor: SimfTokens.accent),
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        Center(
          child: SimfScannerFrame(
            statusLabel: l10n.scanningCode,
            camera: Stack(
              fit: StackFit.expand,
              children: <Widget>[
                ReaderWidget(
                  onScan: _onScan,
                  codeFormat: Format.qrCode,
                  showGallery: false,
                  showToggleCamera: false,
                  tryInverted: true,
                  loading: const ColoredBox(color: Colors.black),
                ),
                if (_processing)
                  const ColoredBox(
                    color: Color(0x66000000),
                    child: Center(child: CircularProgressIndicator()),
                  ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

/// The scanner's navy header band (56-high): a circular navy-deep back button at
/// the inline start over a centred white title — mirrors [AccountSubHeader] with
/// `circular: true` (kept local so `app/` doesn't depend on `features/`).
class _ScannerHeader extends StatelessWidget {
  const _ScannerHeader({required this.title, required this.onBack});

  final String title;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 56,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          Align(
            alignment: Alignment.centerLeft,
            child: Padding(
              padding: const EdgeInsets.only(left: 8),
              child: DecoratedBox(
                decoration: const BoxDecoration(
                  color: SimfTokens.navyDeep,
                  shape: BoxShape.circle,
                ),
                child: IconButton(
                  key: const ValueKey<String>('scannerBack'),
                  onPressed: onBack,
                  icon: const Icon(
                    Icons.arrow_back_ios_new,
                    color: Colors.white,
                    size: 20,
                    textDirection: TextDirection.ltr,
                  ),
                ),
              ),
            ),
          ),
          Text(
            title,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 24,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }
}
