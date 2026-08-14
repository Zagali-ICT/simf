import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';

void main() {
  group('OperatorGate.fromJson / listFromData', () {
    test('parses a gate assignment', () {
      final g = OperatorGate.fromJson(const <String, dynamic>{
        'gateId': 'g1',
        'code': 'MAIN',
        'name': 'Main Gate',
        'nameArabic': 'البوابة الرئيسية',
        'isActive': true,
      });
      expect(g.gateId, 'g1');
      expect(g.localizedName(true), 'البوابة الرئيسية');
      expect(g.localizedName(false), 'Main Gate');
      // No directionMode on the wire → the operator-switchable default (D-509).
      expect(g.directionMode, GateDirectionMode.both);
    });

    test('decodes the gate direction mode (In=0, Out=1, Both=2)', () {
      GateDirectionMode mode(int v) =>
          OperatorGate.fromJson(<String, dynamic>{'directionMode': v})
              .directionMode;
      expect(mode(0), GateDirectionMode.inOnly);
      expect(mode(1), GateDirectionMode.outOnly);
      expect(mode(2), GateDirectionMode.both);
    });

    test('listFromData maps a bare list', () {
      final list = OperatorGate.listFromData(<dynamic>[
        <String, dynamic>{'gateId': 'a'},
        <String, dynamic>{'gateId': 'b'},
      ]);
      expect(list.map((g) => g.gateId), <String>['a', 'b']);
    });
  });

  group('GateScanResult.fromJson', () {
    test('an allowed scan with a profile', () {
      final r = GateScanResult.fromJson(const <String, dynamic>{
        'scanId': 42,
        'outcome': 0,
        'direction': 0,
        'userProfile': <String, dynamic>{
          'displayName': 'Raed',
          'displayNameArabic': 'راند',
          'profileTypeName': 'VIP',
        },
      });
      expect(r.isAllowed, isTrue);
      expect(r.direction, ScanDirection.checkIn);
      expect(r.userProfile?.localizedName(false), 'Raed');
      expect(r.userProfile?.profileTypeName, 'VIP');
      expect(r.denialMessage, isNull);
      // DEF-CHK-004 — no advisory on an ordinary allowed scan.
      expect(r.noticeMessage, isNull);
    });

    test('an allowed scan can carry an advisory notice', () {
      // DEF-CHK-004 — a hall-door scan taken outside every session window is
      // still allowed, but the server flags that no attendance was recorded.
      final r = GateScanResult.fromJson(const <String, dynamic>{
        'scanId': 43,
        'outcome': 0,
        'direction': 0,
        'noticeMessage': 'Entry allowed, but attendance was not recorded.',
      });
      expect(r.isAllowed, isTrue);
      expect(r.denialMessage, isNull);
      expect(
        r.noticeMessage,
        'Entry allowed, but attendance was not recorded.',
      );
    });

    test('a denied scan carries the server message', () {
      final r = GateScanResult.fromJson(const <String, dynamic>{
        'scanId': 7,
        'outcome': 1,
        'direction': 1,
        'denialReasonCode': 2, // HolderNotApproved (not surfaced as an enum)
        'denialMessage': 'Holder not approved',
      });
      expect(r.isAllowed, isFalse);
      expect(r.direction, ScanDirection.checkOut);
      expect(r.denialMessage, 'Holder not approved');
      expect(r.userProfile, isNull);
    });
  });
}
