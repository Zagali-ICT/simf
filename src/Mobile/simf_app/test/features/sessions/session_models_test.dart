import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';

SessionListItem _session({
  required String id,
  required DateTime startUtc,
  String title = 'Session',
  String code = 'S-1',
  String? description,
}) {
  return SessionListItem(
    id: id,
    code: code,
    title: title,
    titleArabic: '',
    hallId: 'h1',
    hallName: 'Hall A',
    hallNameArabic: 'القاعة أ',
    startUtc: startUtc,
    endUtc: startUtc.add(const Duration(hours: 1)),
    status: SessionStatus.scheduled,
    speakers: const <SessionSpeaker>[],
    description: description,
  );
}

void main() {
  group('SessionStatus.fromJson', () {
    test('decodes the int wire value', () {
      expect(SessionStatus.fromJson(0), SessionStatus.scheduled);
      expect(SessionStatus.fromJson(3), SessionStatus.published);
    });

    test('decodes a string name and falls back on the unknown', () {
      expect(SessionStatus.fromJson('Recorded'), SessionStatus.recorded);
      expect(SessionStatus.fromJson(99), SessionStatus.scheduled);
      expect(SessionStatus.fromJson(null), SessionStatus.scheduled);
    });
  });

  group('SessionSpeakerRole.fromJson', () {
    test('decodes int / name; unknown → speaker', () {
      expect(SessionSpeakerRole.fromJson(1), SessionSpeakerRole.host);
      expect(SessionSpeakerRole.fromJson('Host'), SessionSpeakerRole.host);
      expect(SessionSpeakerRole.fromJson(7), SessionSpeakerRole.speaker);
    });
  });

  group('SessionListItem.fromJson', () {
    test('binds the real wire field names incl. the D-271 speaker fields', () {
      final item = SessionListItem.fromJson(<String, dynamic>{
        'id': 's1',
        'code': 'OP-1',
        'title': 'Opening',
        'titleArabic': 'الافتتاح',
        'hallId': 'h1',
        'hallName': 'Main Hall',
        'hallNameArabic': 'القاعة الرئيسية',
        'startUtc': '2026-11-23T06:00:00Z',
        'endUtc': '2026-11-23T07:00:00Z',
        'status': 3, // int on the wire (no string-enum converter, D-299)
        'categoryName': 'Main Session',
        'categoryNameArabic': 'جلسة رئيسية',
        'description': 'Welcome',
        'descriptionArabic': 'أهلاً',
        'speakers': <dynamic>[
          <String, dynamic>{
            'id': 'sp1',
            'name': 'Dr Reef',
            'nameArabic': 'د. ريف',
            'title': 'Chief Scientist',
            'displayOrder': 0,
            'role': 1, // Host
            'countryId': 682,
            'countryNameEn': 'Saudi Arabia',
            'countryNameAr': 'السعودية',
            'photoRelativePath': '/media/sp1.jpg',
          },
        ],
      });

      expect(item.id, 's1');
      expect(item.localizedTitle(true), 'الافتتاح');
      expect(item.localizedTitle(false), 'Opening');
      expect(item.localizedHall(false), 'Main Hall');
      expect(item.localizedCategory(true), 'جلسة رئيسية');
      expect(item.status, SessionStatus.published);
      expect(item.startUtc.isUtc, isTrue);

      expect(item.speakers, hasLength(1));
      final speaker = item.speakers.single;
      expect(speaker.localizedName(false), 'Dr Reef');
      expect(speaker.role, SessionSpeakerRole.host);
      expect(speaker.countryId, 682);
      expect(speaker.localizedCountry(true), 'السعودية');
      expect(speaker.photoRelativePath, '/media/sp1.jpg');
    });

    test('a missing speakers array decodes to an empty list (never null)', () {
      final item = SessionListItem.fromJson(<String, dynamic>{
        'id': 's2',
        'code': 'X',
        'title': 'No speakers',
        'startUtc': '2026-11-23T06:00:00Z',
        'endUtc': '2026-11-23T07:00:00Z',
      });
      expect(item.speakers, isEmpty);
      expect(item.localizedDescription(false), isNull);
      expect(item.localizedCategory(false), isNull);
    });
  });

  group('SessionsPage.fromJson', () {
    test('reads the items array from the envelope data', () {
      final page = SessionsPage.fromJson(<String, dynamic>{
        'items': <dynamic>[
          <String, dynamic>{
            'id': 'a',
            'startUtc': '2026-11-23T06:00:00Z',
            'endUtc': '2026-11-23T07:00:00Z',
          },
        ],
      });
      expect(page.items, hasLength(1));
      expect(page.items.single.id, 'a');
    });

    test('a malformed payload yields an empty page', () {
      expect(SessionsPage.fromJson(null).items, isEmpty);
      expect(SessionsPage.fromJson(<String, dynamic>{}).items, isEmpty);
    });
  });

  group('filterSessions', () {
    final past = _session(id: 'past', startUtc: DateTime.utc(2026, 11, 23, 9));
    final future = _session(
      id: 'future',
      startUtc: DateTime.utc(2026, 11, 25, 9),
      title: 'Closing keynote',
    );
    final now = DateTime.utc(2026, 11, 24);

    test('Upcoming drops sessions whose start is before now', () {
      final result = filterSessions(
        <SessionListItem>[past, future],
        view: SessionsView.upcoming,
        nowUtc: now,
      );
      expect(result.map((s) => s.id), <String>['future']);
    });

    test('Forum keeps the whole programme', () {
      final result = filterSessions(
        <SessionListItem>[past, future],
        view: SessionsView.forum,
        nowUtc: now,
      );
      expect(result, hasLength(2));
    });

    test('the query matches title and code (case-insensitive)', () {
      final result = filterSessions(
        <SessionListItem>[past, future],
        view: SessionsView.forum,
        nowUtc: now,
        query: 'keynote',
      );
      expect(result.map((s) => s.id), <String>['future']);
    });

    test('a day filter keeps only that local calendar day', () {
      // Use the session's own local day so the assertion is timezone-independent.
      final localDay = DateTime(
        future.startLocal.year,
        future.startLocal.month,
        future.startLocal.day,
      );
      final result = filterSessions(
        <SessionListItem>[past, future],
        view: SessionsView.forum,
        nowUtc: now,
        localDay: localDay,
      );
      expect(result.map((s) => s.id), contains('future'));
      expect(result.map((s) => s.id), isNot(contains('past')));
    });
  });

  group('sessionDays', () {
    test('returns the distinct local days, ascending', () {
      final days = sessionDays(<SessionListItem>[
        _session(id: 'b', startUtc: DateTime.utc(2026, 11, 25, 12)),
        _session(id: 'a', startUtc: DateTime.utc(2026, 11, 23, 12)),
        _session(id: 'a2', startUtc: DateTime.utc(2026, 11, 23, 15)),
      ]);
      expect(days, hasLength(2));
      expect(days.first.isBefore(days.last), isTrue);
    });
  });
}
