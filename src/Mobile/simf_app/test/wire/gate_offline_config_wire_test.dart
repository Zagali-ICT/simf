import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/gates/data/gate_offline_config.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../features/accessibility/_fake_prefs.dart';

// Sentinel wire fixtures for the cached offline gate config (D-820).
//
// See `pending_gate_scan_wire_test.dart` for why frozen literals are needed at
// all: every decoder here is tolerant, so a renamed key decodes to a fallback
// instead of throwing, and a `fromJson(toJson())` round trip cannot see it.
//
// This blob is persisted on the device, so a drift breaks a shipped scanner at
// upgrade with no server involved — and the failure is silent in the worst
// way: a dropped `badgeKey` leaves `canVerifyOffline` false, so every offline
// scan is queued with no verdict and the console merely looks unarmed.
//
// The one key/field asymmetry to watch: the field is `issuedAtIso`, the wire
// key is `issuedAt`.

/// Deterministic AES-256 material — 32 bytes, so `keyFor` returns it rather
/// than failing closed on a wrong-length key.
const String _badgeKey = 'AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=';
const String _previousKey = 'ZWZnaGlqa2xtbm9wcXJzdHV2d3h5ent8fX5/gIGCg4Q=';

/// Every field carrying a value nothing in the decoder can produce by
/// accident: non-null keys, non-zero versions, true bools, a non-empty
/// allow-list, a non-empty gate list.
const String _sentinelJson = '''
{
  "badgeKey": "$_badgeKey",
  "badgeKeyVersion": 4242,
  "previousBadgeKey": "$_previousKey",
  "previousBadgeKeyVersion": 4141,
  "sessionWalkIn": true,
  "issuedAt": "2031-04-17T13:24:42.424Z",
  "gates": [
    {
      "gateId": "WIRE:gateId",
      "code": "WIRE:code",
      "allowedProfileTypeCodes": [7, 9],
      "isHallDoor": true,
      "isActive": true
    }
  ]
}
''';

/// Every key present, every value null. Pins the fallbacks themselves.
const String _allNullsJson = '''
{
  "badgeKey": null,
  "badgeKeyVersion": null,
  "previousBadgeKey": null,
  "previousBadgeKeyVersion": null,
  "sessionWalkIn": null,
  "issuedAt": null,
  "gates": null
}
''';

const String _allNullsRuleJson = '''
{
  "gateId": null,
  "code": null,
  "allowedProfileTypeCodes": null,
  "isHallDoor": null,
  "isActive": null
}
''';

const Set<String> _configKeys = <String>{
  'badgeKey',
  'badgeKeyVersion',
  'previousBadgeKey',
  'previousBadgeKeyVersion',
  'sessionWalkIn',
  'issuedAt',
  'gates',
};

const Set<String> _ruleKeys = <String>{
  'gateId',
  'code',
  'allowedProfileTypeCodes',
  'isHallDoor',
  'isActive',
};

Map<String, dynamic> _decode(String json) =>
    jsonDecode(json) as Map<String, dynamic>;

void main() {
  group('GateOfflineConfig — frozen sentinel fixture', () {
    test('every key decodes to its sentinel, not to a fallback', () {
      final config = GateOfflineConfig.fromJson(_decode(_sentinelJson));

      expect(config.badgeKey, _badgeKey);
      expect(config.badgeKeyVersion, 4242);
      expect(config.previousBadgeKey, _previousKey);
      expect(config.previousBadgeKeyVersion, 4141);
      expect(config.sessionWalkIn, isTrue);
      // The field is issuedAtIso; the WIRE key is issuedAt.
      expect(config.issuedAtIso, '2031-04-17T13:24:42.424Z');
      expect(config.gates, hasLength(1));
    });

    test('the nested rule decodes every key to its sentinel', () {
      final rule =
          GateOfflineConfig.fromJson(_decode(_sentinelJson)).gates.single;

      expect(rule.gateId, 'WIRE:gateId');
      expect(rule.code, 'WIRE:code');
      expect(rule.allowedProfileTypeCodes, <int>[7, 9]);
      expect(rule.isHallDoor, isTrue);
      expect(rule.isActive, isTrue);
    });

    test('the key pairs with its version, so a rotation still verifies', () {
      final config = GateOfflineConfig.fromJson(_decode(_sentinelJson));

      expect(config.canVerifyOffline, isTrue);
      expect(config.keyFor(4242), base64Decode(_badgeKey));
      expect(config.keyFor(4141), base64Decode(_previousKey));
      expect(config.keyFor(1), isNull);
    });

    test('the sentinel survives the prefs round trip', () async {
      final prefs = FakePrefs(<String, Object>{
        StorageKeys.gateOfflineConfig: _sentinelJson,
      });
      final cache = GateOfflineConfigCache(prefs);

      final read = cache.read()!;
      expect(read.badgeKeyVersion, 4242);
      expect(read.issuedAtIso, '2031-04-17T13:24:42.424Z');
      expect(read.ruleFor('WIRE:gateId')?.code, 'WIRE:code');

      await cache.write(read);
      final written = _decode(prefs.getString(StorageKeys.gateOfflineConfig)!);
      expect(written.keys.toSet(), equals(_configKeys));
      expect(written, equals(_decode(_sentinelJson)));
    });
  });

  group('GateOfflineConfig — fallbacks', () {
    test('an all-nulls object defaults every field', () {
      final config = GateOfflineConfig.fromJson(_decode(_allNullsJson));

      expect(config.badgeKey, isNull);
      expect(config.badgeKeyVersion, 0);
      expect(config.previousBadgeKey, isNull);
      expect(config.previousBadgeKeyVersion, 0);
      expect(config.sessionWalkIn, isFalse);
      expect(config.issuedAtIso, '');
      expect(config.gates, isEmpty);
      // Fail closed: no key means the capability is not armed.
      expect(config.canVerifyOffline, isFalse);
    });

    test('an empty object defaults every field identically', () {
      final config = GateOfflineConfig.fromJson(_decode('{}'));

      expect(config.badgeKey, isNull);
      expect(config.badgeKeyVersion, 0);
      expect(config.previousBadgeKey, isNull);
      expect(config.previousBadgeKeyVersion, 0);
      expect(config.sessionWalkIn, isFalse);
      expect(config.issuedAtIso, '');
      expect(config.gates, isEmpty);
      expect(config.canVerifyOffline, isFalse);
    });

    test('an all-nulls rule defaults every field', () {
      final rule = GateOfflineRule.fromJson(_decode(_allNullsRuleJson));

      expect(rule.gateId, '');
      expect(rule.code, '');
      expect(rule.allowedProfileTypeCodes, isEmpty);
      expect(rule.isHallDoor, isFalse);
      expect(rule.isActive, isFalse);
      // An empty allow-list is "no restriction", not "nobody".
      expect(rule.admits(7), isTrue);
    });

    test('an empty rule object defaults every field identically', () {
      final rule = GateOfflineRule.fromJson(_decode('{}'));

      expect(rule.gateId, '');
      expect(rule.code, '');
      expect(rule.allowedProfileTypeCodes, isEmpty);
      expect(rule.isHallDoor, isFalse);
      expect(rule.isActive, isFalse);
    });
  });

  group('GateOfflineConfig — toJson emits the fixture back', () {
    test('the emitted key SET equals the sentinel fixture key set', () {
      final fixture = _decode(_sentinelJson);
      final emitted = GateOfflineConfig.fromJson(fixture).toJson();

      expect(emitted.keys.toSet(), equals(fixture.keys.toSet()));
      expect(emitted.keys.toSet(), equals(_configKeys));
      expect(emitted, equals(fixture));
    });

    test('the emitted rule key SET equals the fixture rule key set', () {
      final fixture = _decode(_sentinelJson);
      final fixtureRule =
          (fixture['gates'] as List<dynamic>).single as Map<String, dynamic>;
      final emitted =
          GateOfflineConfig.fromJson(fixture).gates.single.toJson();

      expect(emitted.keys.toSet(), equals(fixtureRule.keys.toSet()));
      expect(emitted.keys.toSet(), equals(_ruleKeys));
      expect(emitted, equals(fixtureRule));
    });

    test('a null key is written as a PRESENT null, never omitted', () {
      // Unlike PendingGateScan.direction, nothing here is conditional: all
      // seven keys are emitted whatever the values are. Pinning that stops a
      // future `if (badgeKey != null)` from quietly changing the artefact.
      final emitted =
          GateOfflineConfig.fromJson(_decode(_allNullsJson)).toJson();

      expect(emitted.keys.toSet(), equals(_configKeys));
      expect(emitted['badgeKey'], isNull);
      expect(emitted['previousBadgeKey'], isNull);
      expect(emitted['badgeKeyVersion'], 0);
      expect(emitted['sessionWalkIn'], false);
      expect(emitted['issuedAt'], '');
      expect(emitted['gates'], isEmpty);
    });
  });
}
