import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'gate_models.dart';
import 'gate_scan_queue.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// Data layer for the staff gate operator console (Figma 758:4380/4651/4735/
/// 4819/4886, D-406).
///
/// Authority: the endpoints require the JWT **`Gates.Operate`** permission
/// (Identity role `GateOperator`), which is **separate** from the mobile
/// `AppRole.staff` — a staff app user without the GateOperator grant gets 403
/// (`GATE_OPERATOR_NOT_ASSIGNED`). The screen renders that gracefully; the grant
/// pipeline is flagged for the owner (D-406).
class GatesRepository {
  GatesRepository(this._client, this._queue);

  final SimfApiClient _client;
  final GateScanQueue _queue;

  /// `GET /app/gates/my-assignments` → the gates this operator may work.
  Future<List<OperatorGate>> myAssignments() {
    return _client.get<List<OperatorGate>>(
      '/app/gates/my-assignments',
      decodeData: OperatorGate.listFromData,
    );
  }

  /// `POST /app/gates/{gateId}/scans` — records a scan and returns the verdict.
  /// A denial is a 200 with `outcome == denied` — INCLUDING a scan at an
  /// inactive gate (DEF-STF-008); infra failures throw [ApiFailure] (404
  /// gate-not-found, 403 not-assigned, 409 idempotency conflict, 429
  /// circuit-open). [idempotencyKey] dedupes a rapid double-scan of the same
  /// code.
  Future<GateScanResult> recordScan({
    required String gateId,
    required String qr,
    required String idempotencyKey,
    ScanDirection? direction,
  }) {
    return _client.post<GateScanResult>(
      '/app/gates/$gateId/scans',
      body: <String, dynamic>{
        'qr': qr,
        'idempotencyKey': idempotencyKey,
        'source': 1, // ScanSource.MobileApp
        // D-509 — the operator's دخول/خروج choice; the server honours it only
        // for a Both-mode gate (fixed In/Out gates ignore it).
        if (direction != null)
          'direction': direction == ScanDirection.checkOut ? 1 : 0,
      },
      decodeData: (data) => GateScanResult.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }

  /// Records a scan, or — when the server never returned a verdict (network
  /// down / timeout) — queues it on-device for automatic retry and returns
  /// `null`. Any response that DID reach the server (a 4xx rejection, a 429
  /// throttle, or a deterministic 5xx) is rethrown for the caller to surface.
  /// The [idempotencyKey] (a fresh per-scan UUIDv4, G-1)
  /// is reused on every retry so a scan the server already recorded replays
  /// instead of double-counting the person (G-4).
  Future<GateScanResult?> recordScanOrQueue({
    required String gateId,
    required String qr,
    required String idempotencyKey,
    ScanDirection? direction,
  }) async {
    try {
      return await recordScan(
        gateId: gateId,
        qr: qr,
        idempotencyKey: idempotencyKey,
        direction: direction,
      );
    } on ApiFailure catch (failure) {
      if (!_isServerUnreachable(failure)) {
        rethrow;
      }
      await _queue.enqueue(
        PendingGateScan(
          gateId: gateId,
          qr: qr,
          idempotencyKey: idempotencyKey,
          direction: direction,
          queuedAtIso: formatWire(DateTime.now()),
        ),
      );
      return null;
    }
  }

  /// Scans held on-device awaiting retry (G-4).
  int pendingCount() => _queue.length;

  /// Retries every queued scan oldest-first with its original idempotency key.
  /// A scan that lands (allowed OR denied — both are HTTP 200) or is rejected
  /// for good (any response the server returned, e.g. a 409 replay of one
  /// already recorded, or a 404 for a deleted gate) is dropped; the first attempt
  /// that still never reaches the server (or is 429-throttled) stops the drain
  /// and leaves the rest for the next call. Returns the remaining backlog size.
  Future<int> flushPending() async {
    for (final scan in _queue.all()) {
      try {
        await recordScan(
          gateId: scan.gateId,
          qr: scan.qr,
          idempotencyKey: scan.idempotencyKey,
          direction: scan.direction,
        );
        await _queue.remove(scan.idempotencyKey);
      } on ApiFailure catch (failure) {
        if (_isServerUnreachable(failure) || failure.httpStatus == 429) {
          break;
        }
        await _queue.remove(scan.idempotencyKey);
      }
    }
    return _queue.length;
  }

  /// True only when the call never returned a verdict — no response at all
  /// (network down / timeout -> null status). A response that DID arrive (any
  /// HTTP status, including a deterministic 5xx) is a decision a blind retry
  /// can't change, so it is NOT queued (G-4 correction).
  static bool _isServerUnreachable(ApiFailure failure) =>
      failure.httpStatus == null;
}

final gatesRepositoryProvider = Provider<GatesRepository>((ref) {
  return GatesRepository(
    ref.watch(simfApiClientProvider),
    ref.watch(gateScanQueueProvider),
  );
});
