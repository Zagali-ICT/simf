import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'gate_models.dart';

/// Data layer for the staff gate operator console (Figma 758:4380/4651/4735/
/// 4819/4886, D-406).
///
/// Authority: the endpoints require the JWT **`Gates.Operate`** permission
/// (Identity role `GateOperator`), which is **separate** from the mobile
/// `AppRole.staff` — a staff app user without the GateOperator grant gets 403
/// (`GATE_OPERATOR_NOT_ASSIGNED`). The screen renders that gracefully; the grant
/// pipeline is flagged for the owner (D-406).
class GatesRepository {
  GatesRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/gates/my-assignments` → the gates this operator may work.
  Future<List<OperatorGate>> myAssignments() {
    return _client.get<List<OperatorGate>>(
      '/app/gates/my-assignments',
      decodeData: OperatorGate.listFromData,
    );
  }

  /// `POST /app/gates/{gateId}/scans` — records a scan and returns the verdict.
  /// A denial is a 200 with `outcome == denied`; infra failures throw
  /// [ApiFailure] (404 gate-not-found, 403 not-assigned, 503 inactive, 409
  /// idempotency conflict, 429 circuit-open). [idempotencyKey] dedupes a rapid
  /// double-scan of the same code.
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
}

final gatesRepositoryProvider = Provider<GatesRepository>((ref) {
  return GatesRepository(ref.watch(simfApiClientProvider));
});
