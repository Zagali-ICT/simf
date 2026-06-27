import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_zxing/flutter_zxing.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import 'data/gate_models.dart';
import 'data/gates_repository.dart';

/// Staff gate-operator console — Figma 758:4651 (setup: pick gate + movement),
/// 758:4819 (ممنوع / denied), 758:4886 (مسموح / allowed), D-406 / D-509.
/// Role-gated to `AppRole.staff`+ (router); the server additionally requires the
/// `Gates.Operate` permission and a gate assignment.
///
/// Flow (D-509): load the operator's gate assignments → **setup** screen (choose
/// the gate + the دخول/خروج movement type) → tap **سكان الرمز** to open the
/// camera (or type the code) → the green **مسموح** or red **ممنوع** result with
/// the holder / gate / direction → "سكان مرة أخرى". The دخول/خروج choice is sent
/// to the server and honoured for a **Both**-mode gate; a fixed In/Out gate
/// locks the toggle to its one direction (no CP round-trip to switch).
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
  bool _busy = false;
  // The console has two stages: the setup card (gate + movement) and, once the
  // operator taps "Scan code", the live camera / manual-entry scanner.
  bool _scanning = false;
  String _lastQr = '';
  List<OperatorGate> _gates = const <OperatorGate>[];
  OperatorGate? _gate;
  // The operator's chosen movement type. Null on a Both-mode gate until the
  // operator picks one (Figma 4651 — "choose the movement type first"); auto-set
  // for a fixed In/Out gate.
  ScanDirection? _direction;
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
        if (_gate != null) {
          _applyGateDefaults(_gate!);
        }
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

  /// A fixed In/Out gate locks the movement to its one direction; a Both gate
  /// starts unset so the operator must choose (Figma 4651 hint).
  void _applyGateDefaults(OperatorGate gate) {
    switch (gate.directionMode) {
      case GateDirectionMode.inOnly:
        _direction = ScanDirection.checkIn;
      case GateDirectionMode.outOnly:
        _direction = ScanDirection.checkOut;
      case GateDirectionMode.both:
        _direction = null;
    }
  }

  void _onGate(OperatorGate gate) {
    setState(() {
      _gate = gate;
      _applyGateDefaults(gate);
    });
  }

  void _onScan(Code result) {
    if (_busy || !result.isValid) {
      return;
    }
    final code = result.text?.trim();
    if (code != null && code.isNotEmpty) {
      unawaited(_scan(code));
    }
  }

  Future<void> _scan(String qr) async {
    final gate = _gate;
    final direction = _direction;
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
            direction: direction,
            // Derived from the gate + direction + code so a true rapid re-scan
            // of the SAME badge in the SAME direction collides server-side
            // (idempotent replay); switching direction is a fresh scan (D-509).
            idempotencyKey: '${gate.gateId}-${direction?.name ?? 'auto'}-$trimmed',
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

  /// "سكان مرة أخرى" — clears the result and returns to the live scanner with the
  /// same gate + direction so the operator can scan the next holder.
  void _scanAgain() {
    setState(() {
      _result = null;
      _lastQr = '';
      _manual.clear();
      _scanning = true;
    });
  }

  /// System / AppBar back walks the stages backwards (scanner/result → setup →
  /// leave) rather than dropping straight out of the console.
  void _back() {
    if (_result != null) {
      setState(() {
        _result = null;
        _lastQr = '';
        _manual.clear();
      });
      return;
    }
    if (_scanning) {
      setState(() => _scanning = false);
      return;
    }
    _leave();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    // The setup stage shows the screen title; once scanning, the bar shows the
    // selected "gate • direction" (Figma 4819/4886).
    final showContext = _scanning || _result != null;
    final gateName = _gate?.localizedName(isArabic) ?? l10n.gateScannerEntry;
    final directionForTitle = _result?.direction ?? _direction;
    final title = showContext
        ? '$gateName${directionForTitle == null ? '' : ' • ${_directionLabel(l10n, directionForTitle)}'}'
        : l10n.gateScanTitle;
    return PopScope(
      // Route the system back through _back (go_router); raw pop can't exit
      // this shell-pushed route (D-426).
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) {
          _back();
        }
      },
      child: Scaffold(
        backgroundColor: SimfTokens.navy,
        appBar: AppBar(
          backgroundColor: SimfTokens.navy,
          foregroundColor: Colors.white,
          elevation: 0,
          centerTitle: true,
          title: Text(title),
          leading: IconButton(
            icon: const Icon(Icons.arrow_back),
            onPressed: _back,
          ),
        ),
        body: SafeArea(child: _body(l10n, isArabic)),
      ),
    );
  }

  /// Leaves the gate console reliably even when it is the navigator root (deep
  /// link / route restore / a shell push that didn't stack) — pop when possible,
  /// else go home.
  void _leave() {
    final router = GoRouter.maybeOf(context);
    if (router == null) {
      if (Navigator.of(context).canPop()) {
        Navigator.of(context).pop();
      }
      return;
    }
    if (router.canPop()) {
      router.pop();
    } else {
      router.goNamed(RouteNames.home);
    }
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
        reference: _lastQr,
        onScanAgain: _scanAgain,
      );
    }
    if (!_scanning) {
      return _GateSetup(
        l10n: l10n,
        isArabic: isArabic,
        gates: _gates,
        gate: _gate!,
        direction: _direction,
        onGate: _onGate,
        onDirection: (d) => setState(() => _direction = d),
        onScan: () => setState(() => _scanning = true),
      );
    }
    return _Scanner(
      l10n: l10n,
      enableCamera: widget.enableCamera,
      manual: _manual,
      onScan: _onScan,
      onBack: () => setState(() => _scanning = false),
      onManual: () => unawaited(_scan(_manual.text)),
    );
  }

  static String _directionLabel(AppL10n l10n, ScanDirection d) =>
      d == ScanDirection.checkOut ? l10n.gateDirectionOut : l10n.gateDirectionIn;
}

/// Figma 758:4651 — the setup card: a QR glyph, the gate picker, the دخول/خروج
/// movement toggle, and the big "سكان الرمز" button (enabled once a movement
/// type is chosen).
class _GateSetup extends StatelessWidget {
  const _GateSetup({
    required this.l10n,
    required this.isArabic,
    required this.gates,
    required this.gate,
    required this.direction,
    required this.onGate,
    required this.onDirection,
    required this.onScan,
  });

  final AppL10n l10n;
  final bool isArabic;
  final List<OperatorGate> gates;
  final OperatorGate gate;
  final ScanDirection? direction;
  final ValueChanged<OperatorGate> onGate;
  final ValueChanged<ScanDirection> onDirection;
  final VoidCallback onScan;

  @override
  Widget build(BuildContext context) {
    final locked = gate.directionMode != GateDirectionMode.both;
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const Spacer(),
          // QR glyph in a rounded gold-bordered tile (Figma 758:4655).
          Center(
            child: Container(
              width: 140,
              height: 140,
              decoration: BoxDecoration(
                color: SimfTokens.navyDeep,
                borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
                border: Border.all(color: SimfTokens.accent, width: 1.5),
              ),
              child: const Icon(
                Icons.qr_code_2,
                size: 76,
                color: SimfTokens.accent,
              ),
            ),
          ),
          const Spacer(),
          _label(l10n.gateSelectGate),
          const SizedBox(height: SimfTokens.space2),
          _GatePicker(
            l10n: l10n,
            isArabic: isArabic,
            gates: gates,
            gate: gate,
            onGate: onGate,
          ),
          const SizedBox(height: SimfTokens.space4),
          _label(l10n.gateMovementType),
          const SizedBox(height: SimfTokens.space2),
          Row(
            children: <Widget>[
              Expanded(
                child: _DirectionButton(
                  label: l10n.gateDirectionIn,
                  icon: Icons.login,
                  selected: direction == ScanDirection.checkIn,
                  enabled: !locked || gate.directionMode == GateDirectionMode.inOnly,
                  onTap: () => onDirection(ScanDirection.checkIn),
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: _DirectionButton(
                  label: l10n.gateDirectionOut,
                  icon: Icons.logout,
                  selected: direction == ScanDirection.checkOut,
                  enabled:
                      !locked || gate.directionMode == GateDirectionMode.outOnly,
                  onTap: () => onDirection(ScanDirection.checkOut),
                ),
              ),
            ],
          ),
          if (direction == null) ...<Widget>[
            const SizedBox(height: SimfTokens.space3),
            Text(
              l10n.gateChooseDirectionFirst,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ],
          const Spacer(flex: 2),
          FilledButton.icon(
            onPressed: direction == null ? null : onScan,
            style: FilledButton.styleFrom(
              backgroundColor: SimfTokens.accent,
              foregroundColor: SimfTokens.navy,
              disabledBackgroundColor: SimfTokens.accent.withValues(alpha: 0.4),
              disabledForegroundColor: SimfTokens.navy.withValues(alpha: 0.6),
              minimumSize: const Size.fromHeight(56),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radius),
              ),
            ),
            icon: const Icon(Icons.photo_camera_outlined),
            label: Text(
              l10n.gateScanCode,
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textMd,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _label(String text) => Text(
        text,
        textAlign: TextAlign.start,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textSm,
          fontWeight: FontWeight.w600,
        ),
      );
}

class _DirectionButton extends StatelessWidget {
  const _DirectionButton({
    required this.label,
    required this.icon,
    required this.selected,
    required this.enabled,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final bool selected;
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final fg = selected
        ? SimfTokens.navy
        : (enabled ? Colors.white : SimfTokens.beigeBorder);
    return Opacity(
      opacity: enabled ? 1 : 0.5,
      child: InkWell(
        onTap: enabled ? onTap : null,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Container(
          height: 52,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected ? SimfTokens.accent : SimfTokens.navyDeep,
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            border: Border.all(
              color: selected ? SimfTokens.accent : SimfTokens.beigeBorder,
            ),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Text(
                label,
                style: TextStyle(
                  color: fg,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textMd,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Icon(icon, size: 18, color: fg),
            ],
          ),
        ),
      ),
    );
  }
}

/// The live scanner stage — the ZXing camera (native, no Google Play Services,
/// works on Huawei/HMS — D-426) plus the always-usable manual-entry path.
class _Scanner extends StatelessWidget {
  const _Scanner({
    required this.l10n,
    required this.enableCamera,
    required this.manual,
    required this.onScan,
    required this.onBack,
    required this.onManual,
  });

  final AppL10n l10n;
  final bool enableCamera;
  final TextEditingController manual;
  final void Function(Code) onScan;
  final VoidCallback onBack;
  final VoidCallback onManual;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
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
                    if (enableCamera)
                      ReaderWidget(
                        onScan: onScan,
                        codeFormat: Format.qrCode,
                        showGallery: false,
                        showToggleCamera: false,
                        tryInverted: true,
                        // Back inside flutter_zxing's overlay — tappable over the
                        // live camera where the AppBar back is swallowed (D-426).
                        onActionSecondButton: onBack,
                        actionSecondButtonIcon: const Icon(Icons.arrow_back),
                        loading: const Center(
                          child: Icon(
                            Icons.qr_code_2,
                            size: 72,
                            color: SimfTokens.beigeBorder,
                          ),
                        ),
                      )
                    else
                      const Center(
                        child: Icon(
                          Icons.qr_code_2,
                          size: 72,
                          color: SimfTokens.beigeBorder,
                        ),
                      ),
                    Positioned(
                      left: 0,
                      right: 0,
                      bottom: SimfTokens.space3,
                      child: Text(
                        l10n.gateScanHint,
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
      decoration: const InputDecoration(
        enabledBorder: OutlineInputBorder(
          borderSide: BorderSide(color: SimfTokens.beigeBorder),
        ),
        focusedBorder: OutlineInputBorder(
          borderSide: BorderSide(color: SimfTokens.accent),
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
        if (id == null) {
          return;
        }
        onGate(gates.firstWhere((g) => g.gateId == id, orElse: () => gate));
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
    required this.reference,
    required this.onScanAgain,
  });

  final AppL10n l10n;
  final bool isArabic;
  final GateScanResult result;
  final String gateName;

  /// The scanned/entered badge code — shown as the frame's "الرقم المرجعي".
  final String reference;
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
                  _row(
                    l10n.gateFieldName,
                    (name?.trim().isNotEmpty ?? false)
                        ? name!
                        : l10n.gateUnregistered,
                  ),
                  _row(
                    l10n.gateFieldReference,
                    reference.trim().isNotEmpty ? reference : l10n.gateNone,
                  ),
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
