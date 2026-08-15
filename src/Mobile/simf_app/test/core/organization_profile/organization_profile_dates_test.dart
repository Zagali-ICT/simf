import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';

void main() {
  group('OrgProfile event dates (#43)', () {
    test('decodes eventStartDate/eventEndDate and formats the range', () {
      final p = OrgProfile.fromJson(<String, dynamic>{
        'name': 'SIMF',
        'nameArabic': 'الملتقى',
        'eventStartDate': '2026-11-23T00:00:00+00:00',
        'eventEndDate': '2026-11-25T00:00:00Z',
        'social': const <String, dynamic>{},
      });
      expect(p.eventStartDate, isNotNull);
      expect(p.eventEndDate, isNotNull);
      // Timezone-independent: the calendar date is taken from the string head,
      // so a midnight-elsewhere value never shifts to the 22nd in a negative
      // offset.
      expect(p.eventStartDate!.day, 23);
      expect(p.eventEndDate!.day, 25);
      expect(p.eventDateRange(isArabic: false), '23-25 November 2026');
      expect(p.eventDateRange(isArabic: true), '23-25 نوفمبر 2026');
    });

    test('eventDateRange is null when the dates are absent', () {
      final p = OrgProfile.fromJson(const <String, dynamic>{
        'name': 'SIMF',
        'social': <String, dynamic>{},
      });
      expect(p.eventStartDate, isNull);
      expect(p.eventDateRange(isArabic: false), isNull);
    });
  });
}
