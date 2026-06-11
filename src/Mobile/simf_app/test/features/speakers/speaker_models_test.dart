import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';

void main() {
  group('SpeakerSummary.fromJson', () {
    test('binds the card fields incl. country', () {
      final s = SpeakerSummary.fromJson(<String, dynamic>{
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
      expect(s.localizedName(true), 'القبطان ريف');
      expect(s.localizedName(false), 'Capt. Reef');
      expect(s.localizedCountry(true), 'السعودية');
      expect(s.displayOrder, 3);
    });
  });

  group('SpeakerDetail.fromJson', () {
    test('binds CV pairs, gates, social URLs + sessions', () {
      final d = SpeakerDetail.fromJson(<String, dynamic>{
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
        'sessions': <dynamic>[
          <String, dynamic>{
            'id': 'se1',
            'code': 'S-1',
            'title': 'Talk',
            'titleArabic': 'حديث',
            'hallId': 'h1',
            'hallName': 'Main',
            'hallNameArabic': 'الرئيسية',
            'startUtc': '2026-11-23T06:00:00Z',
            'endUtc': '2026-11-23T07:00:00Z',
          },
        ],
      });
      expect(d.localizedBio(false), 'A bio');
      expect(d.localizedQualifications(false), 'Quals');
      expect(d.localizedAwards(false), isNull); // null pair → null
      expect(d.allowsMeetingRequests, isTrue);
      expect(d.allowsDataSharing, isTrue);
      expect(d.facebookUrl, 'https://fb/x');
      expect(d.sessions, hasLength(1));
      expect(d.sessions.single.localizedTitle(true), 'حديث');
      expect(d.sessions.single.startUtc.isUtc, isTrue);
    });

    test('gates + sessions default safely when absent', () {
      final d = SpeakerDetail.fromJson(<String, dynamic>{
        'id': 'sp2',
        'name': 'X',
        'nameArabic': 'س',
      });
      expect(d.allowsMeetingRequests, isFalse);
      expect(d.allowsDataSharing, isFalse);
      expect(d.sessions, isEmpty);
      expect(d.localizedBio(false), isNull);
    });
  });
}
