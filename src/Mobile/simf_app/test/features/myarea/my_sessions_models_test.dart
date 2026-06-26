import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/myarea/data/my_sessions_models.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';

void main() {
  group('MyAreaSessions.fromData', () {
    test('decodes items, per-user flags, and the bilingual fields', () {
      final page = MyAreaSessions.fromData(<String, dynamic>{
        'items': <dynamic>[
          <String, dynamic>{
            'id': 's1',
            'title': 'Keynote',
            'titleArabic': 'الكلمة الرئيسية',
            'startUtc': '2026-11-23T06:00:00Z',
            'endUtc': '2026-11-23T07:00:00Z',
            'status': 3, // Published
            'attended': true,
            'isFavourite': true,
            'hallNameEn': 'Main Hall',
            'hallNameAr': 'القاعة الرئيسية',
            'categoryNameEn': 'Digital Economy',
            'categoryNameAr': 'الاقتصاد الرقمي',
            'speakerNameEn': 'Dr. Omari',
            'speakerNameAr': 'د. العمري',
            'speakerTitle': 'Chair',
          },
        ],
      });

      final item = page.items.single;
      expect(item.id, 's1');
      expect(item.attended, isTrue);
      expect(item.isFavourite, isTrue);
      expect(item.status, SessionStatus.published);
      expect(item.durationMinutes, 60);
      expect(item.isArchived, isTrue); // Published counts as archive
      expect(item.localizedTitle(true), 'الكلمة الرئيسية');
      expect(item.localizedTitle(false), 'Keynote');
      expect(item.localizedHall(false), 'Main Hall');
      expect(item.localizedCategory(true), 'الاقتصاد الرقمي');
      expect(item.localizedSpeaker(false), 'Dr. Omari');
    });

    test('upcoming vs ended derive from the device clock', () {
      final item = MyAreaSessionItem.fromJson(<String, dynamic>{
        'id': 's2',
        'title': 'Talk',
        'titleArabic': 'جلسة',
        'startUtc': '2026-11-23T06:00:00Z',
        'endUtc': '2026-11-23T07:00:00Z',
        'status': 0,
        'attended': false,
        'isFavourite': false,
      });

      final before = DateTime.utc(2026, 11, 23, 5);
      final after = DateTime.utc(2026, 11, 23, 8);
      expect(item.isUpcoming(before), isTrue);
      expect(item.hasEnded(before), isFalse);
      expect(item.isUpcoming(after), isFalse);
      expect(item.hasEnded(after), isTrue);
      expect(item.isArchived, isFalse); // Scheduled is not archived
    });

    test('a missing items array decodes to an empty list', () {
      expect(MyAreaSessions.fromData(null).items, isEmpty);
      expect(MyAreaSessions.fromData(<String, dynamic>{}).items, isEmpty);
    });
  });
}
