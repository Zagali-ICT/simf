import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/gate_models.dart';
import 'data/gates_repository.dart';

/// Staff gate-operator console — Figma 758:4380/4651 (scan + manual),
/// 758:4735 (scanning), 758:4819 (ممنوع / denied), 758:4886 (مسموح / allowed),
/// D-406. Role-gated to `AppRole.staff`+ (router); the server additionally
/// requires the `Gates.Operate` permission and a gate assignment.
///
/// Flow: load the operator's gate assignments → scan a QR (or enter it
/// manually) → POST the scan → show the green **مسموح** or red **ممنوع** result
/// with the holder/gate/direction → "scan again". **Hold** pauses auto-scanning
/// (a client-only state — there is no server "hold" outcome, D-406).
class GateScanScreen extends ConsumerStatefulWidget {
  const GateScanScreen({super.key, this.enableCamera = true});

  /// Off in widget tests (no camera) so the manual-entry path drives the flow.
  final bool enableCamera;

  @override
  ConsumerState<GateScanScreen> createState() => _GateScanScreenState();
}

class _GateScanScreenState extends ConsumerState<GateScanScreen> {
  final TextEditingController _manual = TextEditingController();
  bool _loading = true;
  bool _forbidden = false;
  bool _error = false;
  bool _held = false;
  bool _busy = false;
  String _lastQr = '';
  List<OperatorGate> _gates = const <OperatorGate>[];
  OperatorGate? _gate;
  GateScanResult? _result;

  @override
  void initState() {
    super.initState();
    unawaited(_loadGates());
  }

  @override
  void dispose() {
    _manual.dispose();
    super.dispose();
  }

  Future<void> _loadGates() async {
    setState(() {
      _loading = true;
      _forbidden = false;
      _error = false;
    });
    try {
      final gates = await ref.read(gatesRepositoryProvider).myAssignments();
      if (!mounted) {
        return;
      }
      setState(() {
        _gates = gates;
        _gate = gates.isEmpty ? null : gates.first;
        _loading = false;
      });
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      setState(() {
        _forbidden = e.httpStatus == 403;
        _error = e.httpStatus != 403;
        _loading = false;
      });
    }
  }

  void _onDetect(BarcodeCapture capture) {
    if (_held || _busy) {
      return;
    }
    final code = capture.barcodes
        .map((b) => b.rawValue)
        .firstWhere((v) => v != null && v.isNotEmpty, orElse: () => null);
    if (code != null) {
      unawaited(_scan(code));
    }
  }

  Future<void> _scan(String qr) async {
    final gate = _gate;
    final trimmed = qr.trim();
    if (gate == null || trimmed.isEmpty || _busy) {
      return;
    }
    // Debounce a repeat of the same code while a result is already shown.
    if (trimmed == _lastQr && _result != null) {
      return;
    }
    _busy = true;
    _lastQr = trimmed;
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      final result = await ref.read(gatesRepositoryProvider).recordScan(
            gateId: gate.gateId,
            qr: trimmed,
            idempotencyKey: '${gate.gateId}-${_stamp()}',
          );
      if (!mounted) {
        return;
      }
      setState(() => _result = result);
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(
        SnackBar(content: Text(_failureText(l10n, e.httpStatus))),
      );
    } finally {
      _busy = false;
    }
  }

  /// A monotonic-ish stamp for the idempotency key (no Date.now in the test
  /// path — the field is only used by the server to dedupe a rapid re-scan).
  int _stamp() => DateTime.now().microsecondsSinceEpoch;

  String _failureText(AppL10n l10n, int? status) {
    switch (status) {
      case 403:
        return l10n.gateForbidden;
      case 429:
        return l10n.gateRateLimited;
      default:
        return l10n.gateError;
    }
  }

  void _scanAgain() {
    setState(() {
      _result = null;
      _lastQr = '';
      _manual.clear();
    });
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    final gateName = _gate?.localizedName(isArabic) ?? l10n.gateScannerEntry;
    final direction = _result == null
        ? ''
        : ' • ${_directionLabel(l10n, _result!.direction)}';
    return Scaffold(
      backgroundColor: SimfTokens.navy,
      appBar: AppBar(
        backgroundColor: SimfTokens.navy,
        foregroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        title: Text('$gateName$direction'),
      ),
      body: SafeArea(child: _body(l10n, isArabic)),
    );
  }

  Widget _body(AppL10n l10n, bool isArabic) {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_forbidden) {
      return _Centered(icon: Icons.lock_outline, message: l10n.gateForbidden);
    }
    if (_error) {
      return _Retry(
        message: l10n.gateError,
        label: l10n.retryLabel,
        onRetry: () => unawaited(_loadGates()),
      );
    }
    if (_gate == null) {
      return _Centered(
        icon: Icons.sensor_door_outlined,
        message: l10n.gateNotAssigned,
      );
    }
    final result = _result;
    if (result != null) {
      return _GateResult(
        l10n: l10n,
        isArabic: isArabic,
        result: result,
        gateName: _gate!.localizedName(isArabic),
        onScanAgain: _scanAgain,
      );
    }
    return _Scanner(
      l10n: l10n,
      isArabic: isArabic,
      gates: _gates,
      gate: _gate!,
      enableCamera: widget.enableCamera,
      held: _held,
      manual: _manual,
      onDetect: _onDetect,
      onManual: () => unawaited(_scan(_manual.text)),
      onToggleHold: () => setState(() => _held = !_held),
      onGate: (g) => setState(() => _gate = g),
    );
  }

  static String _directionLabel(AppL10n l10n, ScanDirection d) =>
      d == ScanDirection.checkOut ? l10n.gateDirectionOut : l10n.gateDirectionIn;
}

class _Scanner extends StatelessWidget {
  const _Scanner({
    required this.l10n,
    required this.isArabic,
    required this.gates,
    required this.gate,
    required this.enableCamera,
    required this.held,
    required this.manual,
    required this.onDetect,
    required this.onManual,
    required this.onToggleHold,
    required this.onGate,
  });

  final AppL10n l10n;
  final bool isArabic;
  final List<OperatorGate> gates;
  final OperatorGate gate;
  final bool enableCamera;
  final bool held;
  final TextEditingController manual;
  final void Function(BarcodeCapture) onDetect;
  final VoidCallback onManual;
  final VoidCallback onToggleHold;
  final ValueChanged<OperatorGate> onGate;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          if (gates.length > 1) ...<Widget>[
            _GatePicker(
              l10n: l10n,
              isArabic: isArabic,
              gates: gates,
              gate: gate,
              onGate: onGate,
            ),
            const SizedBox(height: SimfTokens.space4),
          ],
          Expanded(
            child: ClipRRect(
              borderRadius: BorderRadius.circular(SimfTokens.radius),
              child: DecoratedBox(
                decoration: BoxDecoration(
                  color: SimfTokens.navyDeep,
                  borderRadius: BorderRadius.circular(SimfTokens.radius),
                  border: Border.all(color: SimfTokens.accent, width: 2),
                ),
                child: Stack(
                  fit: StackFit.expand,
                  children: <Widget>[
                    if (enableCamera && !held)
                      MobileScanner(onDetect: onDetect)
                    else
                      Center(
                        child: Icon(
                          held ? Icons.pause_circle_outline : Icons.qr_code_2,
                          size: 72,
                          color: SimfTokens.beigeBorder,
                        ),
                      ),
                    Positioned(
                      left: 0,
                      right: 0,
                      bottom: SimfTokens.space3,
                      child: Text(
                        held ? l10n.gateHold : l10n.gateScanHint,
                        textAlign: TextAlign.center,
                        style: const TextStyle(color: Colors.white),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(height: SimfTokens.space3),
          OutlinedButton.icon(
            onPressed: onToggleHold,
            style: OutlinedButton.styleFrom(
              minimumSize: const Size.fromHeight(44),
              side: const BorderSide(color: SimfTokens.accent),
            ),
            icon: Icon(
              held ? Icons.play_arrow : Icons.pause,
              color: SimfTokens.accent,
            ),
            label: Text(
              held ? l10n.gateResume : l10n.gateHold,
              style: const TextStyle(color: SimfTokens.accent),
            ),
          ),
          const SizedBox(height: SimfTokens.space3),
          Row(
            children: <Widget>[
              Expanded(
                child: TextField(
                  controller: manual,
                  style: const TextStyle(color: Colors.white),
                  decoration: InputDecoration(
                    hintText: l10n.gateManualHint,
                    hintStyle: const TextStyle(color: SimfTokens.beigeBorder),
                    enabledBorder: const OutlineInputBorder(
                      borderSide: BorderSide(color: SimfTokens.beigeBorder),
                    ),
                    focusedBorder: const OutlineInputBorder(
                      borderSide: BorderSide(color: SimfTokens.accent),
                    ),
                  ),
                  onSubmitted: (_) => onManual(),
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              FilledButton(
                onPressed: onManual,
                style: FilledButton.styleFrom(
                  backgroundColor: SimfTokens.accent,
                  foregroundColor: SimfTokens.navy,
                  minimumSize: const Size(72, 52),
                ),
                child: Text(l10n.gateManualSubmit),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _GatePicker extends StatelessWidget {
  const _GatePicker({
    required this.l10n,
    required this.isArabic,
    required this.gates,
    required this.gate,
    required this.onGate,
  });

  final AppL10n l10n;
  final bool isArabic;
  final List<OperatorGate> gates;
  final OperatorGate gate;
  final ValueChanged<OperatorGate> onGate;

  @override
  Widget build(BuildContext context) {
    return DropdownButtonFormField<String>(
      initialValue: gate.gateId,
      dropdownColor: SimfTokens.navyDeep,
      style: const TextStyle(color: Colors.white),
      decoration: InputDecoration(
        labelText: l10n.gateSelectGate,
        labelStyle: const TextStyle(color: SimfTokens.beigeBorder),
        enabledBorder: const OutlineInputBorder(
          borderSide: BorderSide(color: SimfTokens.beigeBorder),
        ),
      ),
      items: <DropdownMenuItem<String>>[
        for (final g in gates)
          DropdownMenuItem<String>(
            value: g.gateId,
            child: Text(g.localizedName(isArabic)),
          ),
      ],
      onChanged: (id) {
        final picked = gates.firstWhere((g) => g.gateId == id);
        onGate(picked);
      },
    );
  }
}

class _GateResult extends StatelessWidget {
  const _GateResult({
    required this.l10n,
    required this.isArabic,
    required this.result,
    required this.gateName,
    required this.onScanAgain,
  });

  final AppL10n l10n;
  final bool isArabic;
  final GateScanResult result;
  final String gateName;
  final VoidCallback onScanAgain;

  @override
  Widget build(BuildContext context) {
    final allowed = result.isAllowed;
    final accent = allowed ? SimfTokens.success : SimfTokens.danger;
    final name = result.userProfile?.localizedName(isArabic);
    final type = result.userProfile?.profileTypeName;
    final direction = result.direction == ScanDirection.checkOut
        ? l10n.gateDirectionOut
        : l10n.gateDirectionIn;
    return SingleChildScrollView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Container(
        padding: const EdgeInsets.all(SimfTokens.space5),
        decoration: BoxDecoration(
          color: accent.withValues(alpha: 0.08),
          borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
          border: Border.all(color: accent),
        ),
        child: Column(
          children: <Widget>[
            Icon(
              allowed ? Icons.check_circle_outline : Icons.cancel_outlined,
              size: 84,
              color: accent,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              allowed ? l10n.gateAllowed : l10n.gateDenied,
              style: TextStyle(
                color: accent,
                fontSize: SimfTokens.textXl,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: SimfTokens.space2),
            Text(
              allowed
                  ? l10n.gateAllowedSub
                  : (result.denialMessage?.trim().isNotEmpty ?? false
                      ? result.denialMessage!
                      : l10n.gateDeniedSub),
              textAlign: TextAlign.center,
              style: TextStyle(color: accent),
            ),
            const SizedBox(height: SimfTokens.space4),
            Container(
              padding: const EdgeInsets.all(SimfTokens.space4),
              decoration: BoxDecoration(
                color: SimfTokens.navyDeep,
                borderRadius: BorderRadius.circular(SimfTokens.radius),
              ),
              child: Column(
                children: <Widget>[
                  _row(l10n.gateFieldName, name ?? l10n.gateNone),
                  _row(
                    l10n.gateFieldType,
                    (type?.trim().isNotEmpty ?? false) ? type! : '—',
                  ),
                  _row(l10n.gateFieldGate, gateName),
                  _row(l10n.gateFieldDirection, direction, valueColor: accent),
                ],
              ),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton.icon(
              onPressed: onScanAgain,
              style: FilledButton.styleFrom(
                backgroundColor: accent,
                foregroundColor: Colors.white,
                minimumSize: const Size.fromHeight(48),
              ),
              icon: const Icon(Icons.qr_code_scanner),
              label: Text(l10n.gateScanAgain),
            ),
          ],
        ),
      ),
    );
  }

  Widget _row(String label, String value, {Color? valueColor}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: SimfTokens.space2),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              value,
              style: TextStyle(
                color: valueColor ?? Colors.white,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ),
          Text(
            label,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ],
      ),
    );
  }
}

class _Centered extends StatelessWidget {
  const _Centered({required this.icon, required this.message});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, size: 56, color: SimfTokens.beigeBorder),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white),
            ),
          ],
        ),
      ),
    );
  }
}

class _Retry extends StatelessWidget {
  const _Retry({
    required this.message,
    required this.label,
    required this.onRetry,
  });

  final String message;
  final String label;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(label)),
          ],
        ),
      ),
    );
  }
}
