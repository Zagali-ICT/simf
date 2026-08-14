import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/notifications/data/notification_models.dart';

void main() {
  group('NotificationItem.fromJson', () {
    test('decodes the string kind/severity, isRead and localized fields', () {
      final item = NotificationItem.fromJson(const <String, dynamic>{
        'id': 'n1',
        'kind': 'SessionReminder',
        'title': 'Session starts soon',
        'titleArabic': 'تبدأ الجلسة قريباً',
        'body': 'Hall A in 15 minutes.',
        'bodyArabic': 'القاعة أ خلال ١٥ دقيقة.',
        'severity': 'Warning',
        'isRead': false,
        'readAt': null,
        'createdAt': '2026-06-05T09:00:00Z',
      });

      expect(item.id, 'n1');
      expect(item.kind, 'SessionReminder');
      expect(item.severity, NotificationSeverity.warning);
      expect(item.isRead, isFalse);
      expect(item.readAt, isNull);
      expect(item.createdAt, isNotNull);
      // Saudi wall-clock carries no zone, so a decoded value must NOT be
      // left untagged: tagging it would let a later toLocal() shift it by the
      // device offset (owner decision 2026-07-31).
      expect(item.createdAt!.isUtc, isFalse);
      expect(item.localizedTitle(isArabic: false), 'Session starts soon');
      expect(item.localizedTitle(isArabic: true), 'تبدأ الجلسة قريباً');
      expect(item.localizedBody(isArabic: false), 'Hall A in 15 minutes.');
    });

    test('a read item parses readAt and isRead true', () {
      final item = NotificationItem.fromJson(const <String, dynamic>{
        'id': 'n2',
        'kind': 'BookingConfirmed',
        'title': 'Confirmed',
        'titleArabic': '',
        'body': 'Your seat is booked.',
        'bodyArabic': '',
        'severity': 'Success',
        'isRead': true,
        'readAt': '2026-06-05T10:00:00Z',
        'createdAt': '2026-06-05T09:30:00Z',
      });

      expect(item.isRead, isTrue);
      expect(item.severity, NotificationSeverity.success);
      expect(item.readAt, isNotNull);
      // Arabic missing → falls back to English.
      expect(item.localizedTitle(isArabic: true), 'Confirmed');
    });

    test('an unknown or missing severity falls back to info', () {
      expect(
        NotificationSeverity.fromName('Nope'),
        NotificationSeverity.info,
      );
      expect(NotificationSeverity.fromName(null), NotificationSeverity.info);
      final item =
          NotificationItem.fromJson(const <String, dynamic>{'id': 'n3'});
      expect(item.severity, NotificationSeverity.info);
      expect(item.isRead, isFalse);
      expect(item.localizedTitle(isArabic: false), '');
    });
  });

  group('NotificationItem.listFromData', () {
    test('reads the GridPage items envelope', () {
      final items = NotificationItem.listFromData(<String, dynamic>{
        'items': <dynamic>[
          <String, dynamic>{
            'id': 'n1',
            'kind': 'Info',
            'title': 'Hello',
            'titleArabic': '',
            'body': '',
            'bodyArabic': '',
            'severity': 'Info',
            'isRead': false,
            'createdAt': '2026-06-05T09:00:00Z',
          },
        ],
        'pageNumber': 1,
        'pageSize': 50,
        'total': 1,
      });

      expect(items, hasLength(1));
      expect(items.first.id, 'n1');
    });

    test('a missing items key yields an empty list', () {
      expect(NotificationItem.listFromData(null), isEmpty);
      expect(NotificationItem.listFromData(<String, dynamic>{}), isEmpty);
    });
  });
}
