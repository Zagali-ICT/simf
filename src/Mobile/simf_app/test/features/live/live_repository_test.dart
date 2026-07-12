import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/live/data/live_repository.dart';

void main() {
  group('LiveSession.fromJson endUtc (S-3)', () {
    test('decodes endUtc when present on the wire', () {
      final session = LiveSession.fromJson(<String, dynamic>{
        'title': 'Opening',
        'titleArabic': 'الافتتاح',
        'status': 1,
        'startUtc': '2026-11-23T06:00:00Z',
        'endUtc': '2026-11-23T07:00:00Z',
      });
      expect(session.startUtc, DateTime.utc(2026, 11, 23, 6));
      expect(session.endUtc, DateTime.utc(2026, 11, 23, 7));
    });

    test('endUtc is null when the wire omits it (global main-live synthetic)',
        () {
      final session = LiveSession.fromJson(<String, dynamic>{
        'title': 'Opening',
        'titleArabic': 'الافتتاح',
        'status': 1,
      });
      expect(session.endUtc, isNull);
    });
  });
}
