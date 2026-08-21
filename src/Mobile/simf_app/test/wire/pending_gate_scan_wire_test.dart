import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gate_scan_queue.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../features/accessibility/_fake_prefs.dart';

// Sentinel wire fixtures for the on-device pending-gate-scan backlog.
//
// WHY THIS FILE EXISTS. `PendingGateScan.fromJson` is tolerant by design —
// `json['gateId'] as String? ?? ''`, and a direction it does not recognise
// becomes null. A renamed or dropped key therefore does NOT throw: it decodes
// to the fallback, silently. A round-trip test written with realistic data
// (`fromJson(scan.toJson())`) passes on a decoder that has renamed a key on
// BOTH sides, because it never compares against anything outside the code.
//
// These fixtures are frozen literals — hand-written JSON text, every key
// present, every value provably DISTINCT from that field's own fallback. If a
// key is renamed, the field lands on its fallback and the sentinel assertion
// fails loudly. The companion all-nulls / empty-map fixtures pin the
// FALLBACKS, which is the half the sentinel cannot see.
//
// THIS IS THE WORST-CASE MODEL OF THE FOUR. These are gate scans that never
// reached the server, held on-device for retry. A decode failure at upgrade
// drops attendance records with no server copy to recover them from.

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
      // The two shapes are different on the wire and both must be pinned: a
      // key absent entirely is not the same artefact as a key present with a
      // null value, and only the round trip through prefs proves which one
      // this queue writes.
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
