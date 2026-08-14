import 'package:simf_app/app/widgets/qr_scan_view.dart' show QrScanView;

/// Dedupe + single-flight policy shared by every in-app QR scanner (D-737).
///
/// The live ZXing reader fires `onScan` on every decoded frame (~1×/s by
/// default), so without a gate a steady QR launches duplicate resolves and
/// stacks navigations / spams snackbars — the badge-sign-in "not recognised"
/// stream and the old [QrScanView] `_lastHandled` (which never reset, so a
/// same-code retry after a transient network error was silently blocked) both
/// trace to a missing gate. This one policy replaces the three per-screen ad-hoc
/// guards:
///
/// * [shouldHandle] runs one code at a time (single-flight) and ignores the same
///   code while it stays in view;
/// * [onNoCode] resets the last code once the QR leaves the frame, so
///   deliberately re-presenting the same badge after a failure works;
/// * a [_sameCodeCooldown] floor still absorbs decode flicker (a no-code frame
///   that never arrives between two identical decodes).
///
/// Pure and clock-injectable so the whole matrix is unit-testable without a
/// camera.
class ScanGate {
  ScanGate({DateTime Function()? clock}) : _now = clock ?? DateTime.now;

  final DateTime Function() _now;

  /// Minimum gap before the *same* code may fire again when no no-code frame
  /// separated the two decodes.
  static const Duration _sameCodeCooldown = Duration(seconds: 2);

  bool _busy = false;
  String? _lastCode;
  DateTime? _lastAt;

  /// Camera path — true when [code] should be handled now. Marks the gate busy;
  /// the caller MUST call [markIdle] when its async handler completes.
  bool shouldHandle(String code) {
    if (_busy || code.isEmpty) {
      return false;
    }
    final now = _now();
    final last = _lastAt;
    if (code == _lastCode &&
        last != null &&
        now.difference(last) < _sameCodeCooldown) {
      return false;
    }
    _lastCode = code;
    _lastAt = now;
    _busy = true;
    return true;
  }

  /// Manual-entry path — single-flight only, no dedupe: an explicit tap should
  /// always run (even the same code the camera just failed on). Marks the gate
  /// busy; the caller MUST call [markIdle] afterwards.
  bool beginManual() {
    if (_busy) {
      return false;
    }
    _busy = true;
    return true;
  }

  void markIdle() => _busy = false;

  /// Called for frames with no decodable code — lets the same code fire again
  /// once it has actually left and re-entered the viewfinder.
  void onNoCode() => _lastCode = null;
}
