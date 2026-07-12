import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_zxing/flutter_zxing.dart';

import '../../core/utils/scan_gate.dart';
import '../../core/widgets/simf_field_style.dart';
import '../localization/app_l10n.dart';
import '../theme/tokens.dart';
import 'simf_scanner_frame.dart';

/// The single QR-scanning experience shared by every scanner in the app
/// (badge sign-in, gate console, contact scan, exhibitor lead capture) — D-737.
///
/// It replaces the two divergent patterns that made scanning feel inconsistent
/// (the old bespoke badge reader and the gate's plain navy box) with one body:
/// the [SimfScannerFrame] gold-bracket viewfinder over a bounded live
/// [ReaderWidget], the always-usable manual-entry field, a single [ScanGate]
/// dedupe/single-flight policy, a busy overlay, and — new — a visible
/// **camera-error / permission-denied state** so a denied camera is never a
/// silent black dead-end.
///
/// It hosts no Scaffold/header/back so it can drop into either a full-page
/// scanner ([QrScanView]) or the gate console's own AppBar shell. The EMUI
/// safety contract is kept (D-423/D-426): the camera is bounded to the card,
/// the "stop camera" control sits OUTSIDE the camera surface, and the manual
/// path always works.
class SimfScannerBody extends StatefulWidget {
  const SimfScannerBody({
    required this.fieldLabel,
    required this.continueLabel,
    required this.onCode,
    this.cameraLabel,
    this.hint,
    this.bottomHint,
    this.enableCamera = true,
    super.key,
  });

  /// Manual-entry field hint.
  final String fieldLabel;

  /// Manual-entry submit button label.
  final String continueLabel;

  /// "Start camera" button label (shown when the camera is off — either the
  /// opt-in default or after "Stop camera"). Falls back to the shared label.
  final String? cameraLabel;

  /// Resolve / capture / branch on the scanned or typed code. Awaited; the
  /// scanner shows a busy overlay and blocks re-fires while it runs.
  final Future<void> Function(String code) onCode;

  /// Optional lead-in hint above the viewfinder.
  final String? hint;

  /// Optional caption under the viewfinder (e.g. the gate's "point at the badge").
  final String? bottomHint;

  /// Off in widget tests (no camera) so the manual-entry path drives the flow.
  final bool enableCamera;

  @override
  State<SimfScannerBody> createState() => _SimfScannerBodyState();
}

class _SimfScannerBodyState extends State<SimfScannerBody> {
  final TextEditingController _manual = TextEditingController();
  final ScanGate _gate = ScanGate();

  bool _cameraOn = false;
  bool _cameraError = false;
  bool _processing = false;
  bool _awaitingController = false;
  // Bumped to force a fresh ReaderWidget on retry so the plugin re-initialises.
  int _readerGeneration = 0;
  Timer? _watchdog;

  @override
  void initState() {
    super.initState();
    // Camera-first (D-737): open on entry; the "Start camera" button is still
    // reachable after the user taps "Stop camera".
    _cameraOn = widget.enableCamera;
    if (_cameraOn) {
      _armWatchdog();
    }
  }

  @override
  void dispose() {
    _watchdog?.cancel();
    _manual.dispose();
    super.dispose();
  }

  // ── Camera lifecycle ──────────────────────────────────────────────────────

  void _startCamera() {
    setState(() {
      _cameraOn = true;
      _cameraError = false;
      _readerGeneration++;
    });
    _armWatchdog();
  }

  void _stopCamera() {
    _watchdog?.cancel();
    setState(() {
      _cameraOn = false;
      _awaitingController = false;
    });
  }

  void _retryCamera() {
    setState(() {
      _cameraError = false;
      _readerGeneration++;
    });
    _armWatchdog();
  }

  /// flutter_zxing swallows an `availableCameras()` failure / no-camera device
  /// with only a debugPrint — no callback ever fires and the reader shows the
  /// loading box forever. This watchdog turns that silent hang into the visible
  /// error state so the manual path is offered.
  void _armWatchdog() {
    _awaitingController = true;
    _watchdog?.cancel();
    _watchdog = Timer(const Duration(seconds: 8), () {
      if (mounted && _awaitingController) {
        setState(() {
          _cameraError = true;
          _awaitingController = false;
        });
      }
    });
  }

  void _onControllerCreated(CameraController? controller, Exception? error) {
    _watchdog?.cancel();
    if (!mounted) {
      return;
    }
    setState(() {
      _awaitingController = false;
      // A permission denial surfaces here as a CameraException (the camera
      // plugin auto-requests inside initialize) — no permission_handler needed.
      _cameraError = error != null || controller == null;
    });
  }

  // ── Scan handling ─────────────────────────────────────────────────────────

  void _onScan(Code code) {
    if (!code.isValid) {
      return;
    }
    final raw = code.text?.trim() ?? '';
    if (_gate.shouldHandle(raw)) {
      unawaited(_run(raw));
    }
  }

  void _onScanFailure(Code? _) => _gate.onNoCode();

  void _submitManual() {
    final value = _manual.text.trim();
    if (value.isEmpty || !_gate.beginManual()) {
      return;
    }
    unawaited(_run(value));
  }

  Future<void> _run(String code) async {
    if (mounted) {
      setState(() => _processing = true);
    }
    try {
      await widget.onCode(code);
    } finally {
      _gate.markIdle();
      if (mounted) {
        setState(() => _processing = false);
      }
    }
  }

  // ── UI ────────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
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
        if (widget.enableCamera) ...<Widget>[
          _buildCameraSection(l10n),
          const SizedBox(height: SimfTokens.space4),
          _OrDivider(label: l10n.orDividerLabel),
          const SizedBox(height: SimfTokens.space4),
        ],
        _buildManual(l10n),
      ],
    );
  }

  Widget _buildCameraSection(AppL10n l10n) {
    if (_cameraError) {
      return _CameraErrorCard(
        message: l10n.scannerCameraError,
        retryLabel: l10n.scannerCameraRetry,
        onRetry: _retryCamera,
      );
    }
    if (!_cameraOn) {
      return OutlinedButton.icon(
        onPressed: _processing ? null : _startCamera,
        icon: const Icon(Icons.qr_code_scanner),
        label: Text(widget.cameraLabel ?? l10n.scanStartCamera),
        style: OutlinedButton.styleFrom(
          foregroundColor: SimfTokens.accent,
          side: const BorderSide(color: SimfTokens.accent),
          minimumSize: const Size.fromHeight(48),
        ),
      );
    }
    // The "Stop camera" control stays OUTSIDE the camera surface (D-426) so EMUI
    // can't swallow it behind the platform view.
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Align(
          alignment: AlignmentDirectional.centerStart,
          child: TextButton.icon(
            onPressed: _stopCamera,
            icon: const Icon(Icons.close),
            label: Text(l10n.qrStopCamera),
            style: TextButton.styleFrom(foregroundColor: SimfTokens.accent),
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        Center(
          child: SimfScannerFrame(
            statusLabel: l10n.scanningCode,
            active: !_processing,
            camera: Stack(
              fit: StackFit.expand,
              children: <Widget>[
                ReaderWidget(
                  key: ValueKey<int>(_readerGeneration),
                  onScan: _onScan,
                  onScanFailure: _onScanFailure,
                  onControllerCreated: _onControllerCreated,
                  codeFormat: Format.qrCode,
                  showGallery: false,
                  showToggleCamera: false,
                  // Keep the built-in torch (auto-hides where unsupported) and
                  // drop zxing's own crop-border overlay — our gold brackets are
                  // the viewfinder.
                  showScannerOverlay: false,
                  tryInverted: true,
                  // Spend more effort per frame locking onto the code — more
                  // reliable reads under real lighting/angle/distance on device.
                  tryHarder: true,
                  // Decode the WHOLE frame, not zxing's default centre-50% crop.
                  // A QR held to fill the gold viewfinder pushes its corner
                  // finder patterns outside a 0.5 crop, so ZXing can't lock on —
                  // the reason a phone camera (full-frame) reads a code the app
                  // could not.
                  cropPercent: 1,
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
        if (widget.bottomHint != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          Text(
            widget.bottomHint!,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ],
      ],
    );
  }

  Widget _buildManual(AppL10n l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
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
          onSubmitted: (_) => _submitManual(),
        ),
        const SizedBox(height: SimfTokens.space3),
        FilledButton(
          // Disabled (not a spinner) while busy: an onCode that opens a modal
          // stays pending, and an infinite spinner would hang pumpAndSettle.
          onPressed: _processing ? null : _submitManual,
          style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
          child: Text(widget.continueLabel),
        ),
      ],
    );
  }
}

/// The gold "or" divider between the camera and manual paths.
class _OrDivider extends StatelessWidget {
  const _OrDivider({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        const Expanded(child: Divider(color: SimfTokens.accent)),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space3),
          child: Text(
            label,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ),
        const Expanded(child: Divider(color: SimfTokens.accent)),
      ],
    );
  }
}

/// Shown when the camera cannot start (permission denied / no camera / init
/// failure). Points at system settings and keeps the manual path below usable,
/// so the scanner is never a silent black dead-end.
class _CameraErrorCard extends StatelessWidget {
  const _CameraErrorCard({
    required this.message,
    required this.retryLabel,
    required this.onRetry,
  });

  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: SimfTokens.scannerCard,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(color: SimfTokens.accent),
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space5),
        child: Column(
          children: <Widget>[
            const Icon(
              Icons.no_photography_outlined,
              color: SimfTokens.accent,
              size: 40,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.beigeBorder),
            ),
            const SizedBox(height: SimfTokens.space3),
            TextButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: Text(retryLabel),
              style: TextButton.styleFrom(foregroundColor: SimfTokens.accent),
            ),
          ],
        ),
      ),
    );
  }
}
