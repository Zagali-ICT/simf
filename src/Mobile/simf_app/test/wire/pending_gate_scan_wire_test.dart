import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gate_scan_queue.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../features/accessibility/_fake_prefs.dart';

// Frozen wire fixtures for the on-device pending-gate-scan backlog (D-219).
// `PendingGateScan.fromJson` is tolerant, so a renamed key decodes to its own
// fallback silently; each sentinel below is distinct from that fallback, and
// the all-nulls fixture pins the fallbacks themselves.

/// Every field carrying a value nothing in the decoder can produce by
/// accident. `direction: 1` is checkOut — the non-default case; the fallback
/// is null.
const String _sentinelJson = '''
{
  "gateId": "WIRE:gateId",
  "qr": "WIRE:qr",
  "idempotencyKey": "WIRE:idempotencyKey",
  "queuedAtIso": "2031-04-17T13:24:42.424Z",
  "direction": 1
}
''';

/// Every key present, every value null. Pins the fallbacks themselves.
const String _allNullsJson = '''
{
  "gateId": null,
  "qr": null,
  "idempotencyKey": null,
  "queuedAtIso": null,
  "direction": null
}
''';

/// The backlog as it actually sits in prefs: a JSON array of scan objects.
const String _sentinelQueueJson = '''
[
  {
    "gateId": "WIRE:gateId",
    "qr": "WIRE:qr",
    "idempotencyKey": "WIRE:idempotencyKey",
    "queuedAtIso": "2031-04-17T13:24:42.424Z",
    "direction": 1
  }
]
''';

Map<String, dynamic> _decode(String json) =>
    jsonDecode(json) as Map<String, dynamic>;

void main() {
  group('PendingGateScan — frozen sentinel fixture', () {
    test('every key decodes to its sentinel, not to a fallback', () {
      final scan = PendingGateScan.fromJson(_decode(_sentinelJson));

      expect(scan.gateId, 'WIRE:gateId');
      expect(scan.qr, 'WIRE:qr');
      expect(scan.idempotencyKey, 'WIRE:idempotencyKey');
      expect(scan.queuedAtIso, '2031-04-17T13:24:42.424Z');
      expect(scan.direction, ScanDirection.checkOut);
    });

    test('the backlog decodes out of the persisted JSON array', () {
      final prefs = FakePrefs(<String, Object>{
        StorageKeys.pendingGateScans: _sentinelQueueJson,
      });
      final queue = GateScanQueue(prefs);

      final all = queue.all();
      expect(all, hasLength(1));
      expect(all.single.gateId, 'WIRE:gateId');
      expect(all.single.qr, 'WIRE:qr');
      expect(all.single.idempotencyKey, 'WIRE:idempotencyKey');
      expect(all.single.queuedAtIso, '2031-04-17T13:24:42.424Z');
      expect(all.single.direction, ScanDirection.checkOut);
    });
  });

  group('PendingGateScan — fallbacks', () {
    test('an all-nulls object defaults every field', () {
      final scan = PendingGateScan.fromJson(_decode(_allNullsJson));

      expect(scan.gateId, '');
      expect(scan.qr, '');
      expect(scan.idempotencyKey, '');
      expect(scan.queuedAtIso, '');
      expect(scan.direction, isNull);
    });

    test('an empty object defaults every field identically', () {
      final scan = PendingGateScan.fromJson(_decode('{}'));

      expect(scan.gateId, '');
      expect(scan.qr, '');
      expect(scan.idempotencyKey, '');
      expect(scan.queuedAtIso, '');
      expect(scan.direction, isNull);
    });
  });

  group('PendingGateScan — direction is an int on the wire', () {
    test('0 is checkIn — the falsy value, decoded as a real case', () {
      final scan = PendingGateScan.fromJson(
        _decode('{"direction": 0}'),
      );
      expect(scan.direction, ScanDirection.checkIn);
    });

    test('1 is checkOut', () {
      final scan = PendingGateScan.fromJson(
        _decode('{"direction": 1}'),
      );
      expect(scan.direction, ScanDirection.checkOut);
    });

    test('an unrecognised value falls back to null, it does not throw', () {
      expect(
        PendingGateScan.fromJson(_decode('{"direction": 7}')).direction,
        isNull,
      );
    });
  });

  group('PendingGateScan — toJson emits the fixture back', () {
    test('the emitted key SET equals the sentinel fixture key set', () {
      final fixture = _decode(_sentinelJson);
      final emitted = PendingGateScan.fromJson(fixture).toJson();

      expect(emitted.keys.toSet(), equals(fixture.keys.toSet()));
      expect(emitted, equals(fixture));
    });

    test('a null direction OMITS the key — it is not written as null', () {
      // An absent key and a key present with null are different wire shapes.
      final emitted = PendingGateScan.fromJson(_decode(_allNullsJson)).toJson();

      expect(emitted.containsKey('direction'), isFalse);
      expect(
        emitted.keys.toSet(),
        equals(<String>{'gateId', 'qr', 'idempotencyKey', 'queuedAtIso'}),
      );
    });

    test('a set direction ADDS the key back, making five', () {
      final emitted = PendingGateScan.fromJson(_decode(_sentinelJson)).toJson();

      expect(
        emitted.keys.toSet(),
        equals(<String>{
          'gateId',
          'qr',
          'idempotencyKey',
          'queuedAtIso',
          'direction',
        }),
      );
      expect(emitted['direction'], 1);
    });

    test('both omitted-key and present-key shapes survive prefs', () async {
      final prefs = FakePrefs();
      final queue = GateScanQueue(prefs);

      await queue.enqueue(
        PendingGateScan.fromJson(_decode(_allNullsJson)),
      );
      await queue.enqueue(
        PendingGateScan.fromJson(_decode(_sentinelJson)),
      );

      final written = jsonDecode(
        prefs.getString(StorageKeys.pendingGateScans)!,
      ) as List<dynamic>;
      final first = written[0] as Map<String, dynamic>;
      final second = written[1] as Map<String, dynamic>;

      expect(first.containsKey('direction'), isFalse);
      expect(second['direction'], 1);
      expect(queue.all().first.direction, isNull);
      expect(queue.all().last.direction, ScanDirection.checkOut);
    });
  });
}
