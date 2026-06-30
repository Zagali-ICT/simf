import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/data/profile_models.dart';

void main() {
  group('UpsertUserProfileRequest.toJson', () {
    test('serialises every field with gender as the wire integer', () {
      const request = UpsertUserProfileRequest(
        profileTypeId: 'pt-1',
        interestIds: <String>['i-1', 'i-2'],
        arabicName: 'راكان السالم',
        englishName: 'Rakan Alsalem',
        jobTitle: 'Engineer',
        nationalityCode: 'SA',
        dateOfBirth: '2000-01-31',
        placeOfBirth: 'Riyadh',
        isSaudi: true,
        nationalId: '1234567890',
        organisationId: 'org-1',
        gender: AppGender.male,
      );

      final json = request.toJson();

      expect(json['profileTypeId'], 'pt-1');
      expect(json['interestIds'], <String>['i-1', 'i-2']);
      expect(json['arabicName'], 'راكان السالم');
      expect(json['englishName'], 'Rakan Alsalem');
      expect(json['nationalityCode'], 'SA');
      expect(json['dateOfBirth'], '2000-01-31');
      expect(json['isSaudi'], true);
      expect(json['nationalId'], '1234567890');
      expect(json['organisationId'], 'org-1');
      expect(json['gender'], 1); // AppGender.male.value
      // Unset optionals are present as null (server treats them as absent).
      expect(json.containsKey('iqamaNumber'), isTrue);
      expect(json['iqamaNumber'], isNull);
    });
  });

  group('UserProfileResponse.fromJson + isComplete', () {
    test('a saved profile parses and reads as complete', () {
      final response = UserProfileResponse.fromJson(<String, dynamic>{
        'profileTypeId': 'pt-1',
        'interestIds': <dynamic>['i-1'],
        'arabicName': 'راكان',
        'englishName': 'Rakan',
        'nationalityCode': 'SA',
        'dateOfBirth': '2000-01-31',
        'placeOfBirth': 'Riyadh',
        'isSaudi': true,
        'gender': 2,
        'hasIdImage': true,
        'qrId': 'ABCDEFGH1234',
      });

      expect(response.englishName, 'Rakan');
      expect(response.interestIds, <String>['i-1']);
      expect(response.gender, AppGender.female);
      expect(response.hasIdImage, isTrue);
      expect(response.qrId, 'ABCDEFGH1234');
      expect(response.isComplete, isTrue);
    });

    test('a first-time (empty) profile reads as incomplete', () {
      final response = UserProfileResponse.fromJson(const <String, dynamic>{});

      expect(response.interestIds, isEmpty);
      expect(response.englishName, isEmpty);
      expect(response.gender, AppGender.unspecified);
      expect(response.hasIdImage, isFalse);
      expect(response.isComplete, isFalse);
    });

    test('names present but no interests is still incomplete', () {
      final response = UserProfileResponse.fromJson(<String, dynamic>{
        'arabicName': 'راكان',
        'englishName': 'Rakan',
        'interestIds': <dynamic>[],
      });

      expect(response.isComplete, isFalse);
    });

    test('hasAvatar decodes and gates the male completeness rule', () {
      // Two-photo split — a male needs BOTH the ID document and the face photo.
      final maleNoAvatar = UserProfileResponse.fromJson(<String, dynamic>{
        'interestIds': <dynamic>['i-1'],
        'arabicName': 'محمد عبدالله أحمد الزهراني',
        'englishName': 'Mohammed Abdullah Ahmed Alzahrani',
        'gender': 1, // male
        'hasIdImage': true,
        'hasAvatar': false,
      });
      expect(maleNoAvatar.hasAvatar, isFalse);
      expect(maleNoAvatar.isComplete, isFalse);

      final maleWithBoth = UserProfileResponse.fromJson(<String, dynamic>{
        'interestIds': <dynamic>['i-1'],
        'arabicName': 'محمد عبدالله أحمد الزهراني',
        'englishName': 'Mohammed Abdullah Ahmed Alzahrani',
        'gender': 1,
        'hasIdImage': true,
        'hasAvatar': true,
      });
      expect(maleWithBoth.isComplete, isTrue);
    });

    test('a profile without the ID document is incomplete for every gender', () {
      final femaleNoId = UserProfileResponse.fromJson(<String, dynamic>{
        'interestIds': <dynamic>['i-1'],
        'arabicName': 'سارة عبدالله أحمد الزهراني',
        'englishName': 'Sara Abdullah Ahmed Alzahrani',
        'gender': 2, // female — face optional, but the ID document is required
        'hasIdImage': false,
        'hasAvatar': false,
      });
      expect(femaleNoId.isComplete, isFalse);
    });
  });

  group('AppGender.fromValue is tolerant', () {
    test('known + unknown values', () {
      expect(AppGender.fromValue(0), AppGender.unspecified);
      expect(AppGender.fromValue(1), AppGender.male);
      expect(AppGender.fromValue(2), AppGender.female);
      expect(AppGender.fromValue(99), AppGender.unspecified);
      expect(AppGender.fromValue(null), AppGender.unspecified);
    });
  });

  group('lookup item decoders', () {
    test('CountryItem / ProfileTypeItem / InterestItem / OrganisationItem', () {
      final country = CountryItem.fromJson(
        const <String, dynamic>{'code': 'SA', 'name': 'Saudi Arabia', 'nameArabic': 'السعودية'},
      );
      expect(country.code, 'SA');
      expect(country.nameArabic, 'السعودية');

      final type = ProfileTypeItem.fromJson(
        const <String, dynamic>{'id': 't1', 'name': 'Visitor', 'nameArabic': 'زائر', 'pageColor': '#0B5', 'isVisitor': true},
      );
      expect(type.isVisitor, isTrue);
      expect(type.pageColor, '#0B5');

      final interest = InterestItem.fromJson(
        const <String, dynamic>{'id': 'n1', 'name': 'Naval Defence', 'nameArabic': 'الدفاع البحري', 'displayOrder': 3},
      );
      expect(interest.displayOrder, 3);

      final org = OrganisationItem.fromJson(
        const <String, dynamic>{'id': 'o1', 'nameAr': 'القوات البحرية', 'nameEn': 'RSNF', 'city': 'Riyadh'},
      );
      expect(org.nameAr, 'القوات البحرية');
      expect(org.nameEn, 'RSNF');
      expect(org.city, 'Riyadh');
    });
  });
}
