import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/live/data/live_models.dart';

void main() {
  group('LiveSession.fromJson end (S-3)', () {
    test('decodes end when present on the wire', () {
      final session = LiveSession.fromJson(<String, dynamic>{
        'title': 'Opening',
        'titleArabic': 'الافتتاح',
        'status': 1,
        'start': '2026-11-23T06:00:00Z',
        'end': '2026-11-23T07:00:00Z',
      });
      expect(session.start, DateTime(2026, 11, 23, 9));
      expect(session.end, DateTime(2026, 11, 23, 10));
    });

    test('end is null when the wire omits it (global main-live synthetic)',
        () {
      final session = LiveSession.fromJson(<String, dynamic>{
        'title': 'Opening',
        'titleArabic': 'الافتتاح',
        'status': 1,
      });
      expect(session.end, isNull);
    });
  });

  // FR-702 (owner 2026-07-31) — the CP-authored notice rides the same
  // PublicSessionDetail wire the live slice already reads.
  group('LiveSession.fromJson liveNotice (FR-702)', () {
    LiveSession decode(Object? notice, Object? noticeArabic) =>
        LiveSession.fromJson(<String, dynamic>{
          'title': 'Opening',
          'titleArabic': 'الافتتاح',
          'status': 1,
          'liveNotice': notice,
          'liveNoticeArabic': noticeArabic,
        });

    test('decodes the pair and localizes with the shared fallback', () {
      final session = decode('English notice.', 'إشعار عربي.');
      expect(session.liveNotice, 'English notice.');
      expect(session.liveNoticeArabic, 'إشعار عربي.');
      expect(session.localizedNotice(isArabic: true), 'إشعار عربي.');
      expect(session.localizedNotice(isArabic: false), 'English notice.');
      // One side only → both locales read the authored side.
      expect(
        decode('English notice.', null).localizedNotice(isArabic: true),
        'English notice.',
      );
    });

    test('a missing / blank notice is null (the banner is not rendered)', () {
      expect(decode(null, null).localizedNotice(isArabic: false), isNull);
      expect(decode('   ', '').localizedNotice(isArabic: false), isNull);
      expect(decode('   ', '').localizedNotice(isArabic: true), isNull);
    });
  });
}
