import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/meet/data/meet_models.dart';

void main() {
  group('Recommendation.listFromData', () {
    test('decodes the matches envelope', () {
      final data = <String, dynamic>{
        'matches': <dynamic>[
          <String, dynamic>{
            'userProfileId': 'u1',
            'englishName': 'Sarah Hill',
            'arabicName': 'سارة هل',
            'jobTitle': 'Naval Architect',
            'profileTypeName': 'Visitor',
            'profileTypeNameArabic': 'زائر',
            'sharedInterests': <dynamic>[
              <String, dynamic>{
                'id': 'i1',
                'name': 'Shipbuilding',
                'nameArabic': 'بناء السفن',
              },
            ],
            'sharedInterestCount': 3,
            'score': 0.82,
          },
        ],
      };

      final matches = Recommendation.listFromData(data);

      expect(matches, hasLength(1));
      final m = matches.first;
      expect(m.userProfileId, 'u1');
      expect(m.englishName, 'Sarah Hill');
      expect(m.arabicName, 'سارة هل');
      expect(m.jobTitle, 'Naval Architect');
      expect(m.sharedInterestCount, 3);
      expect(m.score, closeTo(0.82, 0.0001));
      expect(m.sharedInterests, hasLength(1));
      expect(m.sharedInterests.first.name, 'Shipbuilding');
    });

    test('localized name + profile type pick by language with fallback', () {
      const m = Recommendation(
        userProfileId: 'u1',
        englishName: 'Sarah Hill',
        arabicName: 'سارة هل',
        profileTypeName: 'Visitor',
        profileTypeNameArabic: 'زائر',
        sharedInterests: <MatchedInterest>[],
        sharedInterestCount: 0,
        score: 0,
      );
      expect(m.localizedName(true), 'سارة هل');
      expect(m.localizedName(false), 'Sarah Hill');
      expect(m.localizedProfileType(true), 'زائر');
      expect(m.localizedProfileType(false), 'Visitor');
    });

    test('localizedProfileType is null when neither side is set', () {
      const m = Recommendation(
        userProfileId: 'u1',
        englishName: 'Sarah Hill',
        arabicName: '',
        sharedInterests: <MatchedInterest>[],
        sharedInterestCount: 0,
        score: 0,
      );
      expect(m.localizedProfileType(false), isNull);
      expect(m.localizedProfileType(true), isNull);
    });

    test('tolerates a missing envelope / fields', () {
      expect(Recommendation.listFromData(null), isEmpty);
      expect(Recommendation.listFromData(<String, dynamic>{}), isEmpty);
      final partial = Recommendation.listFromData(<String, dynamic>{
        'matches': <dynamic>[<String, dynamic>{}],
      });
      expect(partial, hasLength(1));
      expect(partial.first.englishName, '');
      expect(partial.first.sharedInterestCount, 0);
      expect(partial.first.sharedInterests, isEmpty);
    });
  });
}
