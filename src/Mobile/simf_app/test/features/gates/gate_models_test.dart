import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';

void main() {
  group('OperatorGate.fromJson / listFromData', () {
    test('parses a gate assignment', () {
      final g = OperatorGate.fromJson(<String, dynamic>{
        'gateId': 'g1',
        'code': 'MAIN',
        'name': 'Main Gate',
        'nameArabic': 'البوابة الرئيسية',
        'isActive': true,
      });
      expect(g.gateId, 'g1');
      expect(g.localizedName(true), 'البوابة الرئيسية');
      expect(g.localizedName(false), 'Main Gate');
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
      final r = GateScanResult.fromJson(<String, dynamic>{
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
      expect(r.denialReason, isNull);
    });

    test('a denied scan maps the reason code + message', () {
      final r = GateScanResult.fromJson(<String, dynamic>{
        'scanId': 7,
        'outcome': 1,
        'direction': 1,
        'denialReasonCode': 2, // HolderNotApproved
        'denialMessage': 'Holder not approved',
      });
      expect(r.isAllowed, isFalse);
      expect(r.direction, ScanDirection.checkOut);
      expect(r.denialReason, GateDenialReason.holderNotApproved);
      expect(r.denialMessage, 'Holder not approved');
      expect(r.userProfile, isNull);
    });

    test('an unknown reason code degrades to unknown (not a crash)', () {
      final r = GateScanResult.fromJson(<String, dynamic>{
        'outcome': 1,
        'denialReasonCode': 99,
      });
      expect(r.denialReason, GateDenialReason.unknown);
    });
  });
}
