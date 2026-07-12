import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/requests/data/request_models.dart';

void main() {
  group('AppRequestKind.fromIndex', () {
    test('maps each wire value, defaulting to speakerMeeting', () {
      expect(AppRequestKind.fromIndex(0), AppRequestKind.speakerMeeting);
      expect(AppRequestKind.fromIndex(1), AppRequestKind.delegationMeeting);
      expect(AppRequestKind.fromIndex(2), AppRequestKind.sessionAttendance);
      expect(AppRequestKind.fromIndex(3), AppRequestKind.participationDocument);
      expect(AppRequestKind.fromIndex(4), AppRequestKind.badgeUpdate);
      expect(AppRequestKind.fromIndex(null), AppRequestKind.speakerMeeting);
      expect(AppRequestKind.fromIndex(99), AppRequestKind.speakerMeeting);
    });
  });

  group('AppRequestStatus.fromIndex', () {
    test('maps each wire value, defaulting to pending', () {
      expect(AppRequestStatus.fromIndex(0), AppRequestStatus.pending);
      expect(AppRequestStatus.fromIndex(1), AppRequestStatus.accepted);
      expect(AppRequestStatus.fromIndex(2), AppRequestStatus.rejected);
      expect(AppRequestStatus.fromIndex(3), AppRequestStatus.cancelled);
      expect(AppRequestStatus.fromIndex(null), AppRequestStatus.pending);
    });
  });

  group('AppRequestItem.fromJson', () {
    test('parses every field', () {
      final item = AppRequestItem.fromJson(<String, dynamic>{
        'kind': 3,
        'id': 'abc',
        'title': 'Official attendance certificate',
        'titleArabic': 'شهادة حضور رسمية',
        'status': 0,
        'eventDateUtc': null,
        'createdAt': '2026-01-10T08:00:00Z',
        'canCancel': true,
      });
      expect(item.kind, AppRequestKind.participationDocument);
      expect(item.id, 'abc');
      expect(item.status, AppRequestStatus.pending);
      expect(item.canCancel, isTrue);
      expect(item.eventDateUtc, isNull);
      expect(item.localizedSubtitle(true), 'شهادة حضور رسمية');
      expect(item.localizedSubtitle(false), 'Official attendance certificate');
    });

    test('parses responseNote and treats blank as null', () {
      final withNote = AppRequestItem.fromJson(<String, dynamic>{
        'kind': 3,
        'id': 'r1',
        'title': 'Doc',
        'titleArabic': 'وثيقة',
        'status': 2,
        'responseNote': '  Missing passport copy.  ',
      });
      expect(withNote.responseNote, 'Missing passport copy.');

      final blankNote = AppRequestItem.fromJson(<String, dynamic>{
        'kind': 3,
        'id': 'r2',
        'title': 'Doc',
        'titleArabic': 'وثيقة',
        'status': 2,
        'responseNote': '   ',
      });
      expect(blankNote.responseNote, isNull);

      final noKey = AppRequestItem.fromJson(<String, dynamic>{
        'kind': 3,
        'id': 'r3',
        'title': 'Doc',
        'titleArabic': 'وثيقة',
        'status': 0,
      });
      expect(noKey.responseNote, isNull);
    });

    test('canCancel defaults to false and missing dates fall back', () {
      final item = AppRequestItem.fromJson(<String, dynamic>{
        'kind': 0,
        'id': 'x',
        'title': 'Speaker',
        'titleArabic': 'متحدث',
        'status': 1,
      });
      expect(item.canCancel, isFalse);
      expect(item.createdAt, DateTime.fromMillisecondsSinceEpoch(0, isUtc: true));
      // displayDate falls back to createdAt when there is no event date.
      expect(item.displayDate, item.createdAt);
    });

    test('displayDate prefers the event date when present', () {
      final item = AppRequestItem.fromJson(<String, dynamic>{
        'kind': 2,
        'id': 'b',
        'title': 'Vision 2030 · Main Hall',
        'titleArabic': 'رؤية 2030 · القاعة',
        'status': 1,
        'eventDateUtc': '2026-01-12T09:00:00Z',
        'createdAt': '2026-01-01T09:00:00Z',
      });
      expect(item.displayDate, DateTime.utc(2026, 1, 12, 9));
    });
  });

  group('listFromData', () {
    test('reads the bare list the endpoint returns', () {
      final items = AppRequestItem.listFromData(<dynamic>[
        <String, dynamic>{'kind': 4, 'id': '1', 'title': 'A', 'titleArabic': 'أ', 'status': 0},
        <String, dynamic>{'kind': 3, 'id': '2', 'title': 'B', 'titleArabic': 'ب', 'status': 2},
      ]);
      expect(items, hasLength(2));
      expect(items.first.kind, AppRequestKind.badgeUpdate);
      expect(items.last.status, AppRequestStatus.rejected);
    });

    test('tolerates null data', () {
      expect(AppRequestItem.listFromData(null), isEmpty);
    });
  });

  group('ParticipationDocumentType', () {
    test('wireValue is the enum index', () {
      expect(ParticipationDocumentType.attendanceCertificate.wireValue, 0);
      expect(ParticipationDocumentType.participationLetter.wireValue, 1);
      expect(ParticipationDocumentType.invitationLetter.wireValue, 2);
    });
  });
}
