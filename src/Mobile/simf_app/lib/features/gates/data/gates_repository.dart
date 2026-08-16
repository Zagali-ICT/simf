import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gate_offline_config.dart';
import 'package:simf_app/features/gates/data/gate_scan_queue.dart';
import 'package:simf_app/features/gates/data/gates_endpoints.dart';
import 'package:simf_app/features/gates/data/offline_badge.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for the staff gate operator console (Figma 758:4380/4651/4735/
/// 4819/4886, D-406).
///
/// Authority: the endpoints require the JWT **`Gates.Operate`** permission
/// (Identity role `GateOperator`), which is **separate** from the mobile
/// `AppRole.staff` — a staff app user without the GateOperator grant gets 403
/// (`GATE_OPERATOR_NOT_ASSIGNED`). The screen renders that gracefully; the
/// grant pipeline is flagged for the owner (D-406).
class GatesRepository {
  GatesRepository(this._client, this._queue, this._offlineConfig);

  final SimfApiClient _client;
  final GateScanQueue _queue;
  final GateOfflineConfigCache _offlineConfig;

  /// `GET /app/gates/my-assignments` → the gates this operator may work.
  Future<List<OperatorGate>> myAssignments() {
    return _client.get<List<OperatorGate>>(
      GatesEndpoints.myAssignments,
      decodeData: OperatorGate.listFromData,
    );
  }

  /// `GET /app/gates/offline-config` → caches this operator's offline rules so
  /// the console keeps judging badges when the link drops (D-820). Failure is
  /// swallowed: the console must still open on a dead network, and the previous
  /// cache is better than none.
  Future<GateOfflineConfig?> refreshOfflineConfig() async {
    try {
      final config = await _client.get<GateOfflineConfig>(
        GatesEndpoints.offlineConfig,
        decodeData: (data) => GateOfflineConfig.fromJson(
          (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
        ),
      );
      await _offlineConfig.write(config);
      return config;
    } on ApiFailure {
      return _offlineConfig.read();
    }
  }

  /// The cached rules, or null when this device has never been online.
  GateOfflineConfig? cachedOfflineConfig() => _offlineConfig.read();

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
      GatesEndpoints.scans(gateId),
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

  /// D-820 — judges a scan on-device when the server is unreachable.
  ///
  /// Returns null when this device cannot decide at all — no cached rules, no
  /// badge key, an unknown gate, or a code that is not a badge this key opens.
  /// The caller shows the operator "queued, no verdict", which is what happened
  /// before D-820.
  ///
  /// **The verdict is ADVISORY.** The queued scan is still uploaded and the
  /// server re-decides it against live data, so this can admit someone a live
  /// check would have refused — an account approved this morning and disabled
  /// since reads as valid here. That is the accepted cost of a gate that keeps
  /// working through an outage, and it is exactly why every scan is still
  /// reconciled afterwards.
  OfflineGateVerdict? judgeOffline({
    required String gateId,
    required String qr,
  }) {
    final config = _offlineConfig.read();
    if (config == null) {
      return null;
    }
    final rule = config.ruleFor(gateId);
    if (rule == null) {
      return null;
    }
    if (!rule.isActive) {
      return const OfflineGateVerdict.denied(OfflineDenialReason.gateInactive);
    }

    // D-821 review — abstain on anything the length says is an ordinary minted
    // serial, mirroring QrResolver's guard on the server. Without this a normal
    // 12-character badge whose first character's Crockford index happens to
    // equal the loaded key version is fed to the decoder, fails the GCM tag,
    // and is reported to the operator as FORGED. This device has no roster, so
    // a minted serial is something it cannot judge — never something it can
    // refuse.
    if (qr.length == OfflineBadge.mintedQrIdLength) {
      return null;
    }

    final version = OfflineBadge.readKeyVersion(qr);
    if (version == null) {
      return null;
    }
    final key = config.keyFor(version);
    if (key == null) {
      // Either the capability is not armed here, or the badge was stamped with
      // a key version this device was never given. Both are "cannot judge",
      // never "denied" — refusing on a rotation gap would shut a working gate.
      return null;
    }

    final badge = OfflineBadge.decode(qr, key);
    if (badge == null) {
      // The key IS loaded and the payload still did not authenticate: this is a
      // forged or damaged badge, which is a real denial rather than an
      // abstention.
      return const OfflineGateVerdict.denied(OfflineDenialReason.badgeInvalid);
    }
    if (!rule.admits(badge.profileTypeCode)) {
      // The rule the walk-in mode NEVER relaxes.
      return OfflineGateVerdict.denied(
        OfflineDenialReason.profileTypeNotAllowed,
        badge: badge,
      );
    }
    if (rule.isHallDoor && !config.sessionWalkIn) {
      // A hall door needs a booking, and this device has no roster. Denying
      // would turn every offline hall door into a wall, so it abstains and lets
      // the server decide when the scan uploads.
      return null;
    }
    return OfflineGateVerdict.allowed(badge);
  }

  /// Scans held on-device awaiting retry (G-4).
  int pendingCount() => _queue.length;

  /// Retries every queued scan oldest-first with its original idempotency key.
  /// A scan that lands (allowed OR denied — both are HTTP 200) or is rejected
  /// for good (any response the server returned, e.g. a 409 replay of one
  /// already recorded, or a 404 for a deleted gate) is dropped; the first
  /// attempt that still never reaches the server (or is 429-throttled) stops
  /// the drain and leaves the rest for the next call. Returns the remaining
  /// backlog size.
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

/// D-820 — a verdict this device reached with no network. Advisory; the server
/// re-decides the queued scan.
class OfflineGateVerdict {
  const OfflineGateVerdict.allowed(OfflineBadge this.badge)
      : isAllowed = true,
        reason = null;

  const OfflineGateVerdict.denied(this.reason, {this.badge})
      : isAllowed = false;

  final bool isAllowed;
  final OfflineDenialReason? reason;

  /// The decrypted badge, when it decrypted at all. Present on an allowed
  /// verdict and on a profile-type denial, so the operator sees who was
  /// refused; absent when the badge itself would not open.
  final OfflineBadge? badge;
}

/// D-820 — why an offline scan was refused on-device. Deliberately a short
/// list: these are the only rules a device can check without the server.
enum OfflineDenialReason {
  /// The badge did not decrypt under a key this device holds — forged, damaged
  /// or not a SIMF badge.
  badgeInvalid,

  /// The badge's type is not on this gate's allow-list. Never relaxed by the
  /// walk-in mode, online or offline.
  profileTypeNotAllowed,

  /// The gate itself was switched off in the last synced rules.
  gateInactive,
}

final gatesRepositoryProvider = Provider<GatesRepository>((ref) {
  return GatesRepository(
    ref.watch(simfApiClientProvider),
    ref.watch(gateScanQueueProvider),
    ref.watch(gateOfflineConfigCacheProvider),
  );
});
