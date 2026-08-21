import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';

/// `MyAreaIdentity.fromJson` is tolerant, so a dashboard that OMITS
/// `isVisitor` decodes to the fallback rather than throwing.
///
/// This one is INVERTED relative to the other admission flags: the safe
/// default is TRUE. `isVisitor` is true for the audience profile types and
/// false only for the partner / exhibitor ("Other") types, so `false` is the
/// value that carries partner treatment. Defaulting it false hands an ordinary
/// attendee — and any account whose payload simply predates the key — the
/// partner branch, which is why the fallback is `?? true` and not `?? false`.
///
/// It drives the home greeting, which shortens a PERSON's name but never an
/// organisation's (`home_screen.dart` reads
/// `profile?.identity.isVisitor ?? true`). It no longer gates the QR-page
/// actions — those key off the signed-in AppRole (DEF-EXH-005) — but the flag
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
      // The whole `identity` object is missing, so the decoder builds one from
      // an empty map — the path a first-load / older server actually takes.
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
