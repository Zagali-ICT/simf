import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';

void main() {
  group('SessionDetail.fromJson', () {
    test('binds the detail fields + the D-271 speaker cluster', () {
      final detail = SessionDetail.fromJson(<String, dynamic>{
        'id': 's1',
        'code': 'OP-1',
        'title': 'Opening',
        'titleArabic': 'الافتتاح',
        'hallId': 'h1',
        'hallName': 'Main Hall',
        'hallNameArabic': 'القاعة الرئيسية',
        'start': '2026-11-23T06:00:00Z',
        'end': '2026-11-23T07:30:00Z',
        'description': 'Welcome',
        'descriptionArabic': 'أهلاً',
        'categoryName': 'Main Session',
        'categoryNameArabic': 'جلسة رئيسية',
        'liveStreamUrl': 'https://youtu.be/abcdefghijk',
        'displayOrder': 2, // D-567 — day-ordinal for the gold badge
        'speakers': <dynamic>[
          <String, dynamic>{
            'id': 'sp1',
            'name': 'Dr Reef',
            'nameArabic': 'د. ريف',
            'title': 'Chief Scientist',
            'displayOrder': 0,
            'role': 1, // Host (int wire)
            'countryId': 682,
            'countryNameEn': 'Saudi Arabia',
            'countryNameAr': 'السعودية',
            'photoRelativePath': '/media/sp1.jpg',
          },
        ],
      });

      expect(detail.localizedTitle(true), 'الافتتاح');
      expect(detail.localizedHall(false), 'Main Hall');
      expect(detail.localizedDescription(false), 'Welcome');
      expect(detail.localizedCategory(true), 'جلسة رئيسية');
      expect(detail.liveStreamUrl, 'https://youtu.be/abcdefghijk');
      expect(detail.hasLiveStream, isTrue);
      expect(detail.displayOrder, 2); // D-567
      // Saudi wall-clock carries no zone, so a decoded value must NOT be
      // left untagged: tagging it would let a later toLocal() shift it by the
      // device offset (owner decision 2026-07-31).
      expect(detail.start.isUtc, isFalse);

      expect(detail.speakers, hasLength(1));
      final speaker = detail.speakers.single;
      expect(speaker.role, SessionSpeakerRole.host);
      expect(speaker.countryId, 682);
      expect(speaker.localizedCountry(true), 'السعودية');
      expect(speaker.photoRelativePath, '/media/sp1.jpg');
    });

    test('optional fields decode to null; speakers default to empty', () {
      final detail = SessionDetail.fromJson(<String, dynamic>{
        'id': 's2',
        'code': 'X',
        'title': 'Bare',
        'start': '2026-11-23T06:00:00Z',
        'end': '2026-11-23T07:00:00Z',
      });
      expect(detail.localizedDescription(false), isNull);
      expect(detail.localizedCategory(false), isNull);
      expect(detail.liveStreamUrl, isNull);
      expect(detail.hasLiveStream, isFalse);
      expect(detail.speakers, isEmpty);
      // #29 — an older API sends no `type`; the detail then renders in full.
      expect(detail.type, isNull);
    });


    // #29 — the detail carries the same tolerant `type` the list does (D-452):
    // the app reduces a WORKSHOP to its title + time.
    test('the session type decodes from the int OR the name wire form', () {
      SessionDetail decode(Object? type) =>
          SessionDetail.fromJson(<String, dynamic>{
            'id': 's3',
            'code': 'W-1',
            'title': 'Workshop',
            'start': '2026-11-23T06:00:00Z',
            'end': '2026-11-23T07:00:00Z',
            'type': type,
          });

      expect(decode(0).type, SessionType.workshop);
      expect(decode('Workshop').type, SessionType.workshop);
      expect(decode(1).type, SessionType.session);
      expect(decode('nonsense').type, isNull);
    });
  });

  group('MySeat.fromSeatMap', () {
    test('reads myCell into a MySeat (row + seat, no column)', () {
      final seat = MySeat.fromSeatMap(<String, dynamic>{
        'sessionId': 's1',
        'myCell': <String, dynamic>{
          'reservationId': 'r1',
          'rowLabel': 'B',
          'seatNumber': 12,
          'kind': 0,
        },
      });
      expect(seat, isNotNull);
      expect(seat!.rowLabel, 'B');
      expect(seat.seatNumber, 12);
      expect(seat.reservationId, 'r1');
    });

    test('a null / absent myCell yields null (no card)', () {
      expect(
        MySeat.fromSeatMap(<String, dynamic>{'sessionId': 's1', 'myCell': null}),
        isNull,
      );
      expect(MySeat.fromSeatMap(<String, dynamic>{'sessionId': 's1'}), isNull);
      expect(MySeat.fromSeatMap(null), isNull);
    });
  });
}
