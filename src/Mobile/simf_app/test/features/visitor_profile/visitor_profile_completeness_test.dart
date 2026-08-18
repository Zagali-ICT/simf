import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/data/app_gender.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_completeness.dart';

/// These rules gate every visitor registration. Inside the submit handler they
/// could only be exercised by driving the whole form; here each one is asserted
/// directly, including the cases the decision ids were filed for.
void main() {
  group('organisation (D-221) and nationality (D-373)', () {
    test('are required', () {
      expect(VisitorProfileCompleteness.organisation(null), isFalse);
      expect(VisitorProfileCompleteness.organisation('org-1'), isTrue);
      expect(VisitorProfileCompleteness.nationality(null), isFalse);
      expect(VisitorProfileCompleteness.nationality('SA'), isTrue);
    });
  });

  group('place of birth (D-723)', () {
    // A Saudi picks a region; the picker is not a FormField, so this is its
    // only gate.
    test('a Saudi must pick a region', () {
      expect(
        VisitorProfileCompleteness.placeOfBirth(
          isSaudi: true,
          birthRegionCode: null,
        ),
        isFalse,
      );
      expect(
        VisitorProfileCompleteness.placeOfBirth(
          isSaudi: true,
          birthRegionCode: 'riyadh',
        ),
        isTrue,
      );
    });

    // A non-Saudi types free text, already covered by the form validator, so
    // this rule must NOT block them.
    test('a non-Saudi passes without a region', () {
      expect(
        VisitorProfileCompleteness.placeOfBirth(
          isSaudi: false,
          birthRegionCode: null,
        ),
        isTrue,
      );
    });
  });

  group('profile type (D-471, L-6)', () {
    bool check({
      bool isVisitorType = false,
      bool loading = false,
      bool failed = false,
      bool hasItems = true,
      String? profileTypeId,
    }) =>
        VisitorProfileCompleteness.profileType(
          isVisitorType: isVisitorType,
          loading: loading,
          failed: failed,
          hasItems: hasItems,
          profileTypeId: profileTypeId,
        );

    test('is required when the picker is actually shown', () {
      expect(check(), isFalse);
      expect(check(profileTypeId: 'p1'), isTrue);
    });

    // The whole point of L-6: gating on a control the user cannot see would
    // block submit with no visible cause.
    test('is NOT required when the picker is hidden', () {
      expect(check(isVisitorType: true), isTrue, reason: 'Visitor-locked');
      expect(check(loading: true), isTrue, reason: 'still loading');
      expect(check(failed: true), isTrue, reason: 'lookup failed');
      expect(check(hasItems: false), isTrue, reason: 'no items');
    });
  });

  group('images', () {
    test('the ID document is mandatory for everyone', () {
      expect(
        VisitorProfileCompleteness.idImage(
          hasPickedImage: false,
          hasStoredImage: false,
        ),
        isFalse,
      );
    });

    // A returning visitor must not be asked to re-upload.
    test('a stored ID document counts', () {
      expect(
        VisitorProfileCompleteness.idImage(
          hasPickedImage: false,
          hasStoredImage: true,
        ),
        isTrue,
      );
    });

    test('the face photo is mandatory for men', () {
      expect(
        VisitorProfileCompleteness.facePhoto(
          gender: AppGender.male,
          hasPickedImage: false,
          hasStoredImage: false,
        ),
        isFalse,
      );
    });

    // The two-photo split: optional for women, and for an unspecified gender,
    // so neither is blocked from registering.
    test('the face photo is optional for anyone not male', () {
      for (final gender in <AppGender>[
        AppGender.female,
        AppGender.unspecified,
      ]) {
        expect(
          VisitorProfileCompleteness.facePhoto(
            gender: gender,
            hasPickedImage: false,
            hasStoredImage: false,
          ),
          isTrue,
          reason: gender.name,
        );
      }
    });
  });
}
