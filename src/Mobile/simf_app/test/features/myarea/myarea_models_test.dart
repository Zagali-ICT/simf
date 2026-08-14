import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';

void main() {
  group('MyAreaDashboard.fromJson', () {
    test('parses identity, counters and the merged schedule', () {
      final json = <String, dynamic>{
        'identity': <String, dynamic>{
          'fullNameAr': 'رائد السالم',
          'fullNameEn': 'Raed Al-Salem',
          'qrId': 'ABC123',
          'avatarUrl': null,
          'tierNameEn': 'VIP',
          'tierNameAr': 'كبار الشخصيات',
          'pageColor': '#C9A14A',
        },
        'counters': <String, dynamic>{
          'bookedSessionsCount': 6,
          'meetingsCount': 3,
        },
        'todaySchedule': <dynamic>[
          <String, dynamic>{
            'kind': 'Session',
            'start': '2026-09-13T08:00:00Z',
            'end': '2026-09-13T09:00:00Z',
            'titleEn': 'Opening',
            'titleAr': 'الافتتاح',
            'hallNameEn': 'Hall A',
            'hallNameAr': 'القاعة أ',
            'subject': null,
            'status': 'Approved',
            'sessionId': 's1',
            'meetingId': null,
          },
          <String, dynamic>{
            'kind': 'Meeting',
            'start': '2026-09-13T10:30:00Z',
            'end': null,
            'titleEn': '',
            'titleAr': '',
            'hallNameEn': null,
            'hallNameAr': null,
            'subject': 'Intro chat',
            'status': 'Confirmed',
            'sessionId': null,
            'meetingId': 'm1',
          },
        ],
      };

      final dashboard = MyAreaDashboard.fromJson(json);

      expect(dashboard.identity.fullNameEn, 'Raed Al-Salem');
      expect(dashboard.identity.qrId, 'ABC123');
      expect(dashboard.identity.localizedTier(isArabic: true), 'كبار الشخصيات');
      expect(dashboard.identity.localizedName(isArabic: false), 'Raed Al-Salem');
      expect(dashboard.counters.bookedSessionsCount, 6);
      expect(dashboard.counters.meetingsCount, 3);
      expect(dashboard.todaySchedule, hasLength(2));

      final session = dashboard.todaySchedule.first;
      expect(session.isSession, isTrue);
      expect(session.localizedTitle(isArabic: false), 'Opening');
      expect(session.localizedHall(isArabic: true), 'القاعة أ');
      expect(session.sessionId, 's1');

      final meeting = dashboard.todaySchedule[1];
      expect(meeting.isSession, isFalse);
      // A business meeting carries no title → falls back to its subject.
      expect(meeting.localizedTitle(isArabic: false), 'Intro chat');
      expect(meeting.end, isNull);
    });

    test('degrades gracefully on empty / missing fields', () {
      final dashboard = MyAreaDashboard.fromJson(const <String, dynamic>{});

      expect(dashboard.identity.fullNameEn, '');
      expect(dashboard.identity.qrId, isNull);
      expect(dashboard.identity.localizedTier(isArabic: true), isNull);
      expect(dashboard.counters.bookedSessionsCount, 0);
      expect(dashboard.counters.meetingsCount, 0);
      expect(dashboard.todaySchedule, isEmpty);
    });
  });
}
