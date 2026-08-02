import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gate_scan_queue.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../accessibility/_fake_prefs.dart';

PendingGateScan _scan(String key, {ScanDirection? direction}) =>
    PendingGateScan(
      gateId: 'g1',
      qr: 'SIMF-$key',
      idempotencyKey: key,
      queuedAtIso: '2026-11-23T06:00:00.000Z',
      direction: direction,
    );

void main() {
  group('PendingGateScan JSON', () {
    test('round-trips a null direction', () {
      final scan = _scan('k1');
      final decoded = PendingGateScan.fromJson(scan.toJson());
      expect(decoded.gateId, 'g1');
      expect(decoded.qr, 'SIMF-k1');
      expect(decoded.idempotencyKey, 'k1');
      expect(decoded.queuedAtIso, '2026-11-23T06:00:00.000Z');
      expect(decoded.direction, isNull);
      // A null direction is omitted from the wire, not written as null.
      expect(scan.toJson().containsKey('direction'), isFalse);
    });

    test('round-trips checkIn (0) and checkOut (1)', () {
      final inScan = PendingGateScan.fromJson(
        _scan('a', direction: ScanDirection.checkIn).toJson(),
      );
      final outScan = PendingGateScan.fromJson(
        _scan('b', direction: ScanDirection.checkOut).toJson(),
      );
      expect(inScan.direction, ScanDirection.checkIn);
      expect(outScan.direction, ScanDirection.checkOut);
    });
  });

  group('GateScanQueue', () {
    test('all() returns const [] on empty prefs', () {
      final queue = GateScanQueue(FakePrefs());
      expect(queue.all(), isEmpty);
      expect(queue.length, 0);
    });

    test('all() returns const [] on garbage (non-array) prefs', () {
      final queue = GateScanQueue(
        FakePrefs(<String, Object>{
          StorageKeys.pendingGateScans: 'not-json-array',
        }),
      );
      expect(queue.all(), isEmpty);
    });

    test('enqueue preserves FIFO order', () async {
      final queue = GateScanQueue(FakePrefs());
      await queue.enqueue(_scan('k1'));
      await queue.enqueue(_scan('k2'));
      await queue.enqueue(_scan('k3'));
      expect(
        queue.all().map((s) => s.idempotencyKey).toList(),
        <String>['k1', 'k2', 'k3'],
      );
    });

    test('remove(key) drops only that item', () async {
      final queue = GateScanQueue(FakePrefs());
      await queue.enqueue(_scan('k1'));
      await queue.enqueue(_scan('k2'));
      await queue.enqueue(_scan('k3'));
      await queue.remove('k2');
      expect(
        queue.all().map((s) => s.idempotencyKey).toList(),
        <String>['k1', 'k3'],
      );
    });

    test('exceeding maxItems keeps the newest (oldest dropped)', () async {
      final queue = GateScanQueue(FakePrefs());
      for (var i = 0; i <= GateScanQueue.maxItems; i++) {
        await queue.enqueue(_scan('$i'));
      }
      final keys = queue.all().map((s) => s.idempotencyKey).toList();
      expect(keys.length, GateScanQueue.maxItems);
      // The very first (oldest) enqueue was dropped; the rest remain in order.
      expect(keys.first, '1');
      expect(keys.last, '${GateScanQueue.maxItems}');
    });
  });
}
