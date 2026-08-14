import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_scanner_body.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/core/utils/uuid_v4.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gates_repository.dart';
import 'package:simf_app/features/gates/widgets/gate_result_view.dart';
import 'package:simf_app/features/gates/widgets/gate_setup_view.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Staff gate-operator console — Figma 758:4651 (setup: pick gate + movement),
/// 758:4819 (ممنوع / denied), 758:4886 (مسموح / allowed), D-406 / D-509.
/// Role-gated to `AppRole.staff`+ (router); the server additionally requires
/// the `Gates.Operate` permission and a gate assignment.
///
/// Flow (D-509): load the operator's gate assignments → **setup** screen
/// (choose the gate + the دخول/خروج movement type) → tap **سكان الرمز** to open
/// the camera (or type the code) → the green **مسموح** or red **ممنوع** result
/// with the holder / gate / direction → "سكان مرة أخرى". The دخول/خروج choice
/// is sent to the server and honoured for a **Both**-mode gate; a fixed In/Out
/// gate locks the toggle to its one direction (no CP round-trip to switch).
///
/// Route: `RouteNames.gateScanner`.
/// Data: [gatesRepositoryProvider].
/// Perf: no list — a single-screen layout.
/// The gates this operator is assigned to (`GET /app/gates/my-assignments`).
///
/// The 403 stays an ERROR — it is a failure with its own copy, and DEF-STF-005
/// needs the SERVER'S specific text ("no Gates.Operate grant" vs "not assigned
/// to this gate" are both 403 and need different operator actions). Keeping it
/// an error means the body can read that message straight off the failure
/// `when` hands it, which is what removed the `_forbiddenMessage` field.
final operatorGatesProvider = FutureProvider.autoDispose<List<OperatorGate>>(
  (ref) => ref.watch(gatesRepositoryProvider).myAssignments(),
);

class GateScanScreen extends ConsumerStatefulWidget {
  const GateScanScreen({super.key, this.enableCamera = true});

  /// Off in widget tests (no camera) so the manual-entry path drives the flow.
  final bool enableCamera;

  @override
  ConsumerState<GateScanScreen> createState() => _GateScanScreenState();
}

class _GateScanScreenState extends ConsumerState<GateScanScreen> {
  // The console has two stages: the setup card (gate + movement) and, once the
  // operator taps "Scan code", the live camera / manual-entry scanner.
  bool _scanning = false;
  String _lastQr = '';
  OperatorGate? _gate;
  // The operator's chosen movement type. Null on a Both-mode gate until the
  // operator picks one (Figma 4651 — "choose the movement type first");
  // auto-set for a fixed In/Out gate.
  ScanDirection? _direction;
  GateScanResult? _result;

  /// Count of scans held on-device awaiting retry (G-4); drives the sync
  /// banner.
  int _pending = 0;

  @override
  void initState() {
    super.initState();
    unawaited(_openConsole());
  }

  /// The load's on-success side effects, which are the reason this awaits the
  /// provider's FIRST future instead of listening: picking the operator's gate,
  /// applying its direction defaults, and draining the offline backlog should
  /// happen when the console OPENS, not again on every pull-to-refresh.
  Future<void> _openConsole() async {
    final List<OperatorGate> gates;
    try {
      gates = await ref.read(operatorGatesProvider.future);
    } on Object {
      return; // The forbidden / error branch renders.
    }
    if (!mounted) {
      return;
    }
    setState(() {
      _gate = gates.isEmpty ? null : gates.first;
      if (_gate != null) {
        _applyGateDefaults(_gate!);
      }
      _pending = ref.read(gatesRepositoryProvider).pendingCount();
    });
    // Opening the console is a good moment to drain any backlog left by a
    // prior offline session (G-4).
    unawaited(_flushPending());
    // D-821 — and to refresh the rules this device falls back on when the
    // link drops. Fire-and-forget: it keeps its previous cache on failure,
    // and the console must open either way.
    unawaited(ref.read(gatesRepositoryProvider).refreshOfflineConfig());
  }

  Future<void> _refresh() => refreshAsync(ref, operatorGatesProvider.future);

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

  /// The scanner body (camera + manual) resolves/branches through this; single-
  /// flight + dedupe are owned by [SimfScannerBody]'s ScanGate.
  Future<void> _scan(String qr) async {
    final gate = _gate;
    final direction = _direction;
    final trimmed = qr.trim();
    if (gate == null || trimmed.isEmpty) {
      return;
    }
    _lastQr = trimmed;
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final repo = ref.read(gatesRepositoryProvider);
    try {
      final result = await repo.recordScanOrQueue(
        gateId: gate.gateId,
        qr: trimmed,
        direction: direction,
        // A FRESH UUIDv4 per scan (SIMF-API-GATES-001 §9 — ScanIdempotency.
        // Key is a client UUIDv4): a genuine re-entry of the same badge in
        // the same direction is a NEW scan, never a 24h-window replay of the
        // first one (G-1).
        idempotencyKey: randomUuidV4(),
      );
      if (!mounted) {
        return;
      }
      if (result == null) {
        // Server unreachable: the scan is safely queued with its idempotency
        // key and retried automatically, so the admitted person is not lost
        // (G-4).
        setState(() => _pending = repo.pendingCount());
        // D-821 — give the operator an answer instead of only "saved". The
        // device decrypts the badge and checks it against this gate's cached
        // rules; null means it could not decide, which is the pre-D-821
        // behaviour and still the honest answer.
        final verdict = repo.judgeOffline(gateId: gate.gateId, qr: trimmed);
        messenger.showSnackBar(
          SnackBar(content: Text(_offlineText(l10n, verdict))),
        );
        return;
      }
      setState(() => _result = result);
      // A successful online scan means the link is back — drain any backlog.
      unawaited(_flushPending());
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(SnackBar(content: Text(_failureText(l10n, e))));
    }
  }

  /// DEF-STF-005 — a 403 carries the reason the operator has to act on: either
  /// "you do not have permission to operate gates" (ask an admin for the
  /// `Gates.Operate` grant) or "you are not assigned to this gate" (ask for the
  /// assignment, or pick a gate you DO hold). The console used to render both
  /// as the first one, so the second was undiagnosable. The server's own
  /// bilingual text wins; the generic copy is the fallback when it sent none.
  String _failureText(AppL10n l10n, ApiFailure failure) {
    switch (failure.httpStatus) {
      case 403:
        return _serverMessage(failure) ?? l10n.gateForbidden;
      case 429:
        return l10n.gateRateLimited;
      default:
        return l10n.gateError;
    }
  }

  /// D-821 — what to tell the operator about a scan the server never saw.
  ///
  /// Every branch also says the scan was saved: whatever the device concluded,
  /// the authoritative decision is the server's when the queue drains, and an
  /// operator who reads an offline verdict as final would be misled.
  static String _offlineText(AppL10n l10n, OfflineGateVerdict? verdict) {
    if (verdict == null) {
      // The device could not decide — no cached rules, no key, or a hall door
      // whose booking check needs live data. Unchanged from before D-821.
      return l10n.gateSavedOffline;
    }
    if (verdict.isAllowed) {
      return l10n.gateOfflineAllowed;
    }
    final reason = switch (verdict.reason) {
      OfflineDenialReason.profileTypeNotAllowed =>
        l10n.gateOfflineDeniedProfileType,
      OfflineDenialReason.gateInactive => l10n.gateOfflineDeniedGateInactive,
      _ => l10n.gateOfflineDeniedBadge,
    };
    return '$reason ${l10n.gateSavedOffline}';
  }

  /// The server's message when it actually sent one — `ApiFailure.message` is
  /// already picked for the app's locale by the envelope decoder.
  static String? _serverMessage(ApiFailure failure) {
    final message = failure.message.trim();
    return message.isEmpty ? null : message;
  }

  /// Drains the offline scan backlog (G-4): retries each queued scan with its
  /// original idempotency key so a scan the server already recorded replays
  /// instead of double-counting. Cheap to call repeatedly.
  Future<void> _flushPending() async {
    final remaining = await ref.read(gatesRepositoryProvider).flushPending();
    if (!mounted) {
      return;
    }
    setState(() => _pending = remaining);
  }

  /// "سكان مرة أخرى" — clears the result and returns to the live scanner with
  /// the same gate + direction so the operator can scan the next holder.
  void _scanAgain() {
    setState(() {
      _result = null;
      _lastQr = '';
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
    final gateName =
        _gate?.localizedName(isArabic: isArabic) ?? l10n.gateScannerEntry;
    final directionForTitle = _result?.direction ?? _direction;
    final directionSuffix = directionForTitle == null
        ? ''
        : ' • ${_directionLabel(l10n, directionForTitle)}';
    final title =
        showContext ? '$gateName$directionSuffix' : l10n.gateScanTitle;
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
        backgroundColor: SimfTokens.navySurface,
        // Figma puts the back button on the LEFT (LTR header) even in the RTL
        // app; force the bar LTR so leading sits left, matching the frame.
        appBar: PreferredSize(
          preferredSize: const Size.fromHeight(kToolbarHeight),
          child: Directionality(
            textDirection: TextDirection.ltr,
            child: AppBar(
              backgroundColor: SimfTokens.navySurface,
              foregroundColor: SimfTokens.surface,
              elevation: 0,
              centerTitle: true,
              title: Text(
                title,
                textDirection: TextDirection.rtl,
                style: SimfTokens.labelWhiteSemiboldTitle,
              ),
              // Figma 758:4655 — circular navy back button (left).
              leading: Padding(
                padding: const EdgeInsets.all(SimfTokens.space2),
                child: Material(
                  color: SimfTokens.navyDeep,
                  shape: const CircleBorder(),
                  child: InkWell(
                    customBorder: const CircleBorder(),
                    onTap: _back,
                    child: const Padding(
                      padding: EdgeInsets.all(SimfTokens.gap6),
                      child: Icon(
                        Icons.chevron_left,
                        color: SimfTokens.surface,
                        size: SimfTokens.gateScanScreenSizeMd,
                      ),
                    ),
                  ),
                ),
              ),
            ),
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
    return ref.watch(operatorGatesProvider).when(
      loading: () => const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      ),
      // Both gate-less branches: a staff member granted the gate assignment
      // after this screen opened must be able to re-check without restarting.
      error: (error, _) {
        final failure = error is ApiFailure ? error : null;
        final forbidden = failure?.httpStatus == 403;
        return SimfRefreshableMessage(
          onRefresh: _refresh,
          child: forbidden
              ? SimfEmptyState(
                  icon: Icons.lock_outline,
                  // DEF-STF-005 — the server's own reason when it sent one:
                  // a missing grant and a missing assignment are both 403 and
                  // need different operator actions.
                  message: _serverMessage(failure!) ?? l10n.gateForbidden,
                )
              : SimfErrorState(
                  message: l10n.gateError,
                  retryLabel: l10n.retryLabel,
                  onRetry: () => ref.invalidate(operatorGatesProvider),
                ),
        );
      },
      data: (gates) => _gate == null
          ? SimfRefreshableMessage(
              onRefresh: _refresh,
              child: SimfEmptyState(
                icon: Icons.sensor_door_outlined,
                message: l10n.gateNotAssigned,
              ),
            )
          : _console(l10n, isArabic, gates),
    );
  }

  Widget _console(AppL10n l10n, bool isArabic, List<OperatorGate> gates) {
    final result = _result;
    if (result != null) {
      return GateResultView(
        l10n: l10n,
        isArabic: isArabic,
        result: result,
        gateName: _gate!.localizedName(isArabic: isArabic),
        reference: _lastQr,
        onScanAgain: _scanAgain,
      );
    }
    if (!_scanning) {
      return _withPendingBanner(
        l10n,
        GateSetupView(
          l10n: l10n,
          isArabic: isArabic,
          gates: gates,
          gate: _gate!,
          direction: _direction,
          onGate: _onGate,
          onDirection: (d) => setState(() => _direction = d),
          onScan: () => setState(() => _scanning = true),
        ),
      );
    }
    // The shared scanner (D-737): camera-first gold viewfinder, always-usable
    // manual entry, one dedupe policy, and a visible camera-error state.
    return _withPendingBanner(
      l10n,
      SingleChildScrollView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: SimfScannerBody(
          fieldLabel: l10n.gateManualHint,
          continueLabel: l10n.gateManualSubmit,
          bottomHint: l10n.gateScanHint,
          enableCamera: widget.enableCamera,
          onCode: _scan,
        ),
      ),
    );
  }

  /// Slim "N waiting to sync" banner above the active scanning stages while the
  /// offline backlog is non-empty (G-4); collapses to nothing when it drains.
  Widget _withPendingBanner(AppL10n l10n, Widget child) {
    if (_pending == 0) {
      return child;
    }
    return Column(
      children: <Widget>[
        Container(
          width: double.infinity,
          color: SimfTokens.navyDeep,
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space4,
            vertical: SimfTokens.space2,
          ),
          child: Row(
            children: <Widget>[
              const Icon(
                Icons.sync,
                color: SimfTokens.accent,
                size: SimfTokens.gateScanScreenSizeSm,
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: Text(
                  l10n.gatePendingSync(_pending),
                  style: SimfTokens.labelBeigeSm,
                ),
              ),
            ],
          ),
        ),
        Expanded(child: child),
      ],
    );
  }

  static String _directionLabel(AppL10n l10n, ScanDirection d) =>
      d == ScanDirection.checkOut
          ? l10n.gateDirectionOut
          : l10n.gateDirectionIn;
}
