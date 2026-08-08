import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// One gate scan that could not reach the server (network down / timeout / a
/// 5xx) and is held on-device for automatic retry. It carries its own
/// [idempotencyKey] — the same fresh per-scan UUIDv4 the first attempt used
/// (SIMF-API-GATES-001 §9) — so a retry of a scan the server ALREADY recorded
/// (but whose response never arrived) replays instead of double-counting the
/// person. G-4: an admitted person must not be dropped on a flaky link.
@immutable
class PendingGateScan {
  const PendingGateScan({
    required this.gateId,
    required this.qr,
    required this.idempotencyKey,
    required this.queuedAtIso,
    this.direction,
  });

  final String gateId;
  final String qr;
  final String idempotencyKey;
  final String queuedAtIso;
  final ScanDirection? direction;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'gateId': gateId,
        'qr': qr,
        'idempotencyKey': idempotencyKey,
        'queuedAtIso': queuedAtIso,
        if (direction != null)
          'direction': direction == ScanDirection.checkOut ? 1 : 0,
      };

  static PendingGateScan fromJson(Map<String, dynamic> json) => PendingGateScan(
        gateId: json['gateId'] as String? ?? '',
        qr: json['qr'] as String? ?? '',
        idempotencyKey: json['idempotencyKey'] as String? ?? '',
        queuedAtIso: json['queuedAtIso'] as String? ?? '',
        direction: _directionFromWire(json['direction']),
      );

  /// CheckIn=0, CheckOut=1; absent/anything else -> null (a Both-gate scan the
  /// operator left unset auto-resolves server-side).
  static ScanDirection? _directionFromWire(Object? value) {
    switch ((value as num?)?.toInt()) {
      case 0:
        return ScanDirection.checkIn;
      case 1:
        return ScanDirection.checkOut;
      default:
        return null;
    }
  }
}

/// On-device FIFO backlog for pending gate scans, persisted as a JSON array in
/// the non-sensitive prefs store (a badge reference is not a secret — it is
/// already sent in the clear to the gate API). Bounded so a long outage cannot
/// grow prefs without limit. Drained by `GatesRepository.flushPending`.
class GateScanQueue {
  GateScanQueue(this._prefs);

  final SimfPrefsStorage _prefs;

  /// Keep the newest scans if the backlog ever runs away during a long outage;
  /// the oldest are dropped first.
  ///
  /// D-819: raised from 500. A dropped scan is a person who walked through a
  /// gate with no record of it, and 500 is inside one busy gate's shift — the
  /// exact scenario the offline capability exists for is also the one most
  /// likely to overrun it. Each entry is a small JSON object, so several
  /// thousand is well within what the preferences store holds comfortably.
  static const int maxItems = 5000;

  List<PendingGateScan> all() {
    final raw = _prefs.getString(StorageKeys.pendingGateScans);
    if (raw == null || raw.isEmpty) {
      return const <PendingGateScan>[];
    }
    final Object? decoded;
    try {
      decoded = jsonDecode(raw);
    } on FormatException {
      // Corrupt prefs (a partial write / bad encoding) must not crash the
      // queue — treat an unparseable backlog as empty.
      return const <PendingGateScan>[];
    }
    if (decoded is! List) {
      return const <PendingGateScan>[];
    }
    return decoded
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => PendingGateScan.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);
  }

  int get length => all().length;

  Future<void> enqueue(PendingGateScan scan) {
    final items = all().toList()..add(scan);
    if (items.length > maxItems) {
      items.removeRange(0, items.length - maxItems);
    }
    return _write(items);
  }

  Future<void> remove(String idempotencyKey) {
    final items = all()
        .where((s) => s.idempotencyKey != idempotencyKey)
        .toList(growable: false);
    return _write(items);
  }

  Future<void> _write(List<PendingGateScan> items) => _prefs.setString(
        StorageKeys.pendingGateScans,
        jsonEncode(items.map((e) => e.toJson()).toList(growable: false)),
      );
}

final gateScanQueueProvider = Provider<GateScanQueue>((ref) {
  return GateScanQueue(ref.watch(simfPrefsStorageProvider));
});
