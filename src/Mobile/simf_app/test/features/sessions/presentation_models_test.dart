import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/presentation_models.dart';

void main() {
  group('PresentationsPage.fromData', () {
    test('decodes items and the bilingual session/speaker fields', () {
      final page = PresentationsPage.fromData(const <String, dynamic>{
        'items': <dynamic>[
          <String, dynamic>{
            'id': 'p1',
            'sessionId': 's1',
            'sessionTitle': 'Future of Investment',
            'sessionTitleArabic': 'مستقبل الاستثمار',
            'sessionStart': '2026-11-23T06:00:00Z',
            'speakerName': 'Dr. Omari',
            'speakerNameArabic': 'د. العمري',
            'fileName': 'deck.pdf',
            'contentType': 'application/pdf',
            'sizeBytes': 2048,
          },
        ],
      });

      final item = page.items.single;
      expect(item.id, 'p1');
      expect(item.sessionId, 's1');
      expect(item.fileName, 'deck.pdf');
      expect(item.contentType, 'application/pdf');
      expect(item.sizeBytes, 2048);
      expect(item.localizedSessionTitle(isArabic: true), 'مستقبل الاستثمار');
      expect(
          item.localizedSessionTitle(isArabic: false), 'Future of Investment',);
      expect(item.localizedSpeaker(isArabic: false), 'Dr. Omari');
    });

    test('defaults a missing content type and a missing items array', () {
      final item = PresentationItem.fromJson(const <String, dynamic>{
        'id': 'p2',
        'sessionId': 's2',
        'sessionTitle': 'Talk',
        'sessionTitleArabic': 'جلسة',
        'sessionStart': '2026-11-23T06:00:00Z',
        'speakerName': 'Speaker',
        'speakerNameArabic': 'متحدث',
        'fileName': 'file',
        'sizeBytes': 0,
      });
      expect(item.contentType, 'application/octet-stream');
      expect(PresentationsPage.fromData(null).items, isEmpty);
    });
  });
}
