import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/session_lifecycle.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';

SessionListItem _session({
  required String id,
  required DateTime start,
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
    start: start,
    end: start.add(const Duration(hours: 1)),
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
      final item = SessionListItem.fromJson(const <String, dynamic>{
        'id': 's1',
        'code': 'OP-1',
        'title': 'Opening',
        'titleArabic': 'الافتتاح',
        'hallId': 'h1',
        'hallName': 'Main Hall',
        'hallNameArabic': 'القاعة الرئيسية',
        'start': '2026-11-23T06:00:00Z',
        'end': '2026-11-23T07:00:00Z',
        'status': 3, // int on the wire (no string-enum converter, D-299)
        'categoryName': 'Main Session',
        'categoryNameArabic': 'جلسة رئيسية',
        'description': 'Welcome',
        'descriptionArabic': 'أهلاً',
        'hasPublishedSummary': true,
        'speakers': <dynamic>[
          <String, dynamic>{
            'id': 'sp1',
            'name': 'Dr Reef',
            'nameArabic': 'د. ريف',
            'title': 'Chief Scientist',
            'titleArabic': 'كبير العلماء',
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
      expect(item.hasPublishedSummary, isTrue);
      // Saudi wall-clock carries no zone, so a decoded value must NOT be
      // left untagged: tagging it would let a later toLocal() shift it by the
      // device offset (owner decision 2026-07-31).
      expect(item.start.isUtc, isFalse);

      expect(item.speakers, hasLength(1));
      final speaker = item.speakers.single;
      expect(speaker.localizedName(false), 'Dr Reef');
      expect(speaker.role, SessionSpeakerRole.host);
      expect(speaker.countryId, 682);
      expect(speaker.localizedCountry(true), 'السعودية');
      expect(speaker.photoRelativePath, '/media/sp1.jpg');
      // Owner 2026-07-19 — the speaker rank/title localizes AR/EN.
      expect(speaker.localizedTitle(true), 'كبير العلماء');
      expect(speaker.localizedTitle(false), 'Chief Scientist');
    });

    test('a missing speakers array decodes to an empty list (never null)', () {
      final item = SessionListItem.fromJson(const <String, dynamic>{
        'id': 's2',
        'code': 'X',
        'title': 'No speakers',
        'start': '2026-11-23T06:00:00Z',
        'end': '2026-11-23T07:00:00Z',
      });
      expect(item.speakers, isEmpty);
      expect(item.localizedDescription(false), isNull);
      expect(item.localizedCategory(false), isNull);
      // Append-only wire default: absent hasPublishedSummary decodes to false.
      expect(item.hasPublishedSummary, isFalse);
    });

    test('a missing start surfaces a decode error instead of 1970', () {
      // BUG-011 — a dropped / renamed timestamp field used to fall back to the
      // Unix epoch, so a broken contract rendered 03:00 AM on every agenda row
      // with no error and no empty state. It must fail loudly instead.
      expect(
        () => SessionListItem.fromJson(const <String, dynamic>{
          'id': 's3',
          'code': 'X',
          'title': 'No start',
          'end': '2026-11-23T07:00:00Z',
        }),
        throwsFormatException,
      );
    });

    test('an unparseable start surfaces a decode error', () {
      expect(
        () => SessionListItem.fromJson(const <String, dynamic>{
          'id': 's4',
          'code': 'X',
          'title': 'Bad start',
          'start': 'not-a-timestamp',
          'end': '2026-11-23T07:00:00Z',
        }),
        throwsFormatException,
      );
    });
  });

  group('SessionsPage.fromJson', () {
    test('reads the items array from the envelope data', () {
      final page = SessionsPage.fromJson(const <String, dynamic>{
        'items': <dynamic>[
          <String, dynamic>{
            'id': 'a',
            'start': '2026-11-23T06:00:00Z',
            'end': '2026-11-23T07:00:00Z',
          },
        ],
      });
      expect(page.items, hasLength(1));
      expect(page.items.single.id, 'a');
    });

    test('a malformed payload yields an empty page', () {
      expect(SessionsPage.fromJson(null).items, isEmpty);
      expect(SessionsPage.fromJson(const <String, dynamic>{}).items, isEmpty);
    });
  });

  group('filterSessions', () {
    final past = _session(id: 'past', start: DateTime.utc(2026, 11, 23, 9));
    final future = _session(
      id: 'future',
      start: DateTime.utc(2026, 11, 25, 9),
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
      // Use the session's own local day so the assertion is
      // timezone-independent.
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
        _session(id: 'b', start: DateTime.utc(2026, 11, 25, 12)),
        _session(id: 'a', start: DateTime.utc(2026, 11, 23, 12)),
        _session(id: 'a2', start: DateTime.utc(2026, 11, 23, 15)),
      ]);
      expect(days, hasLength(2));
      expect(days.first.isBefore(days.last), isTrue);
    });
  });

  group('distinctLocalDays', () {
    test('groups typed items by local day, ascending, deduped, at midnight', () {
      final days = distinctLocalDays<DateTime>(
        <DateTime>[
          DateTime(2026, 11, 25, 12),
          DateTime(2026, 11, 23, 12),
          DateTime(2026, 11, 23, 15), // same local day as the previous
        ],
        (d) => d,
      );
      expect(days, hasLength(2));
      expect(days.first.isBefore(days.last), isTrue);
      expect(days.every((d) => d.hour == 0 && d.minute == 0), isTrue);
    });
  });

  group('sameLocalDay', () {
    test('true for the same calendar day regardless of time / arg order', () {
      final a = DateTime(2026, 11, 23, 8);
      final b = DateTime(2026, 11, 23, 22);
      expect(sameLocalDay(a, b), isTrue);
      expect(sameLocalDay(b, a), isTrue);
    });

    test('false across a day boundary', () {
      expect(
        sameLocalDay(DateTime(2026, 11, 23, 23), DateTime(2026, 11, 24)),
        isFalse,
      );
    });
  });

  group('SessionListItem.phase', () {
    final item = _session(id: 's', start: DateTime.utc(2026, 11, 24, 9));
    // ends 2026-11-24 10:00.
    test('classifies upcoming / live / ended against now', () {
      expect(
        item.phase(DateTime.utc(2026, 11, 24, 8)),
        SessionPhase.upcoming,
      );
      expect(
        item.phase(DateTime.utc(2026, 11, 24, 9, 30)),
        SessionPhase.live,
      );
      expect(
        item.phase(DateTime.utc(2026, 11, 24, 11)),
        SessionPhase.ended,
      );
    });
  });
}
