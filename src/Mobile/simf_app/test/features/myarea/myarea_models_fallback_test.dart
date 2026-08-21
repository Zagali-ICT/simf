import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';

/// Pins `isVisitor`'s fallback, INVERTED relative to the other admission
/// flags: `?? true`, because `false` is the value carrying partner /
/// exhibitor treatment. Drives the home greeting only (DEF-EXH-005); the key
/// stays on the wire regardless (D-219).
void main() {
  group('MyAreaIdentity — isVisitor defaults to the AUDIENCE branch', () {
    test('a partner payload decodes false, not to the fallback', () {
      final identity = MyAreaIdentity.fromJson(const <String, dynamic>{
        'fullNameEn': 'Acme Maritime Systems',
        'isVisitor': false,
      });

      expect(identity.isVisitor, isFalse);
    });

    test('an ABSENT isVisitor reads as an audience attendee', () {
      final identity = MyAreaIdentity.fromJson(const <String, dynamic>{
        'fullNameAr': 'راكان السالم',
        'fullNameEn': 'Rakan Alsalem',
      });

      expect(identity.isVisitor, isTrue);
    });

    test('an explicitly NULL isVisitor reads as an audience attendee', () {
      final identity = MyAreaIdentity.fromJson(const <String, dynamic>{
        'isVisitor': null,
      });

      expect(identity.isVisitor, isTrue);
    });

    test('a dashboard with no identity block at all still reads audience', () {
      // No `identity` block at all: the decoder builds one from an empty map.
      final dashboard = MyAreaDashboard.fromJson(const <String, dynamic>{});

      expect(dashboard.identity.isVisitor, isTrue);
    });

    test('the partner value survives the nested dashboard decode', () {
      final dashboard = MyAreaDashboard.fromJson(const <String, dynamic>{
        'identity': <String, dynamic>{
          'fullNameEn': 'Acme Maritime Systems',
          'isVisitor': false,
        },
      });

      expect(dashboard.identity.isVisitor, isFalse);
    });
  });
}
