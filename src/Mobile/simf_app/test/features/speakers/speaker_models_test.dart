import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';

void main() {
  group('SpeakerSummary.fromJson', () {
    test('binds the card fields incl. country', () {
      final s = SpeakerSummary.fromJson(const <String, dynamic>{
        'id': 'sp1',
        'name': 'Capt. Reef',
        'nameArabic': 'القبطان ريف',
        'rank': 'Sea captain',
        'countryId': 682,
        'countryNameEn': 'Saudi Arabia',
        'countryNameAr': 'السعودية',
        'photoRelativePath': 'speakers/1.jpg',
        'displayOrder': 3,
      });
      expect(s.localizedName(isArabic: true), 'القبطان ريف');
      expect(s.localizedName(isArabic: false), 'Capt. Reef');
      expect(s.localizedCountry(isArabic: true), 'السعودية');
      expect(s.displayOrder, 3);
    });
  });

  group('SpeakerDetail.fromJson', () {
    test('binds CV pairs, gates, social URLs + sessions', () {
      final d = SpeakerDetail.fromJson(const <String, dynamic>{
        'id': 'sp1',
        'name': 'Capt. Reef',
        'nameArabic': 'القبطان ريف',
        'rank': 'Sea captain',
        'bio': 'A bio',
        'bioArabic': 'نبذة',
        'qualifications': 'Quals',
        'awards': null,
        'allowsMeetingRequests': true,
        'allowsDataSharing': true,
        'facebookUrl': 'https://fb/x',
        'websiteUrl': 'https://reef.example.sa',
        'sessions': <dynamic>[
          <String, dynamic>{
            'id': 'se1',
            'code': 'S-1',
            'title': 'Talk',
            'titleArabic': 'حديث',
            'hallId': 'h1',
            'hallName': 'Main',
            'hallNameArabic': 'الرئيسية',
            'start': '2026-11-23T06:00:00Z',
            'end': '2026-11-23T07:00:00Z',
          },
        ],
      });
      expect(d.localizedBio(isArabic: false), 'A bio');
      expect(d.localizedQualifications(isArabic: false), 'Quals');
      expect(d.localizedAwards(isArabic: false), isNull); // null pair → null
      expect(d.allowsMeetingRequests, isTrue);
      expect(d.allowsDataSharing, isTrue);
      expect(d.facebookUrl, 'https://fb/x');
      expect(d.websiteUrl, 'https://reef.example.sa'); // D-544
      expect(d.sessions, hasLength(1));
      expect(d.sessions.single.localizedTitle(isArabic: true), 'حديث');
      // Saudi wall-clock carries no zone, so a decoded value must NOT be
      // left untagged: tagging it would let a later toLocal() shift it by the
      // device offset (owner decision 2026-07-31).
      expect(d.sessions.single.start.isUtc, isFalse);
    });

    test('gates + sessions default safely when absent', () {
      final d = SpeakerDetail.fromJson(const <String, dynamic>{
        'id': 'sp2',
        'name': 'X',
        'nameArabic': 'س',
      });
      expect(d.allowsMeetingRequests, isFalse);
      expect(d.allowsDataSharing, isFalse);
      expect(d.sessions, isEmpty);
      expect(d.localizedBio(isArabic: false), isNull);
    });
  });

  group('SpeakerSummary.matches + visibleSpeakers', () {
    // The rank branches matter: a speaker's rank may be entered in only one
    // language, and a search must still find them. The screen and the meeting
    // sheet each wrote this predicate out before it was shared.
    const sarah = SpeakerSummary(
      id: 's1',
      name: 'Dr. Sarah Al-Otaibi',
      nameArabic: 'د. سارة العتيبي',
      displayOrder: 0,
      rank: 'Rear Admiral',
    );
    const omar = SpeakerSummary(
      id: 's2',
      name: 'Capt. Omar Nasser',
      nameArabic: 'النقيب عمر ناصر',
      displayOrder: 1,
      rankArabic: 'عميد',
    );

    test('an empty or blank query matches everyone', () {
      expect(sarah.matches('', isArabic: false), isTrue);
      expect(sarah.matches('   ', isArabic: true), isTrue);
    });

    test('matches the localized name in the active language', () {
      expect(sarah.matches('sarah', isArabic: false), isTrue);
      expect(sarah.matches('سارة', isArabic: true), isTrue);
    });

    test('matches an English rank even while reading Arabic', () {
      expect(sarah.matches('admiral', isArabic: true), isTrue);
    });

    test('matches an Arabic rank even while reading English', () {
      expect(omar.matches('عميد', isArabic: false), isTrue);
    });

    test('is case-insensitive and ignores surrounding space', () {
      expect(sarah.matches('  ADMIRAL  ', isArabic: false), isTrue);
    });

    test('rejects a token in neither the name nor either rank', () {
      expect(sarah.matches('zzz', isArabic: false), isFalse);
    });

    test('visibleSpeakers keeps input order unless asked to sort', () {
      final all = visibleSpeakers(
        const <SpeakerSummary>[omar, sarah],
        '',
        isArabic: false,
      );
      expect(all.map((s) => s.id), <String>['s2', 's1']);
    });

    test('visibleSpeakers sorts by the localized name when asked', () {
      final sorted = visibleSpeakers(
        const <SpeakerSummary>[omar, sarah],
        '',
        isArabic: false,
        alphaSorted: true,
      );
      // 'Capt. Omar' before 'Dr. Sarah'.
      expect(sorted.map((s) => s.id), <String>['s2', 's1']);
    });

    test('visibleSpeakers applies the query', () {
      final filtered = visibleSpeakers(
        const <SpeakerSummary>[omar, sarah],
        'sarah',
        isArabic: false,
      );
      expect(filtered.map((s) => s.id), <String>['s1']);
    });
  });
}
