import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/data/app_gender.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/sign_up_visitor_form.dart';
import 'package:simf_app/features/account/data/sign_up_visitor_lookups.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_form_state.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// Unit cover for the two mappings lifted out of `sign_up_visitor_screen`
/// (D-368/D-373/D-459/D-469): profile → form and form → request. The screen's
/// own widget tests drive the controls; these pin the rules that have no UI of
/// their own and previously could not be reached without pumping the form.
UserProfileResponse _profile({
  String nationalityCode = 'SA',
  String placeOfBirth = '',
  String? profileTypeId,
  String? iqamaNumber,
  String? passportNumber,
  String? dateOfBirth,
  AppGender gender = AppGender.unspecified,
}) {
  return UserProfileResponse(
    interestIds: const <String>['i1'],
    arabicName: 'محمد',
    englishName: 'Mohammed',
    nationalityCode: nationalityCode,
    placeOfBirth: placeOfBirth,
    isSaudi: nationalityCode == 'SA',
    gender: gender,
    hasIdImage: true,
    hasAvatar: false,
    profileTypeId: profileTypeId,
    iqamaNumber: iqamaNumber,
    passportNumber: passportNumber,
    dateOfBirth: dateOfBirth,
    saudiMobile: '٠٥٠١٢٣٤٥٦٧',
  );
}

const List<CountryItem> _countries = <CountryItem>[
  CountryItem(code: 'SA', name: 'Saudi Arabia', nameArabic: 'السعودية'),
  CountryItem(code: 'EG', name: 'Egypt', nameArabic: 'مصر'),
];

void main() {
  group('SignUpVisitorForm.applyProfile', () {
    test('maps a Saudi profile onto the fields and the shared picks', () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);

      form.applyProfile(
        _profile(placeOfBirth: 'منطقة الرياض', dateOfBirth: '1990-04-05'),
        picks,
      );

      expect(form.arabicName.text, 'محمد');
      expect(form.placeOfBirth.text, 'منطقة الرياض');
      // D-469 — the stored name is resolved back to the region CODE, which is
      // what lets the field be re-read in the other language later.
      expect(form.birthRegionCode, 'riyadh');
      expect(form.dateOfBirth, DateTime(1990, 4, 5));
      expect(form.dateOfBirthDisplay, '05-04-1990');
      expect(picks.isSaudi, isTrue);
      // D-373 — an unspecified gender defaults to Male on a first open.
      expect(picks.gender, AppGender.male);
      expect(form.hasExistingIdImage, isTrue);
      expect(form.hasExistingAvatar, isFalse);
    });

    test('an iqama number selects the Iqama document type (Page_007 L-4)', () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);

      form.applyProfile(
        _profile(nationalityCode: 'EG', iqamaNumber: '2345678901'),
        picks,
      );

      expect(picks.docType, VisitorDocType.iqama);
      expect(form.documentNumber.text, '2345678901');
    });

    test('a passport number selects the Passport document type', () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);

      form.applyProfile(
        _profile(nationalityCode: 'EG', passportNumber: 'A1234567'),
        picks,
      );

      expect(picks.docType, VisitorDocType.passport);
      expect(form.documentNumber.text, 'A1234567');
    });

    test('a nationality the lookup does not carry falls back to SA', () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);

      form.applyProfile(_profile(nationalityCode: 'ZZ'), picks);

      expect(picks.nationalityCode, 'SA');
    });

    test('a profile-type id the lookup does not carry is dropped', () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);

      form.applyProfile(_profile(profileTypeId: 'gone'), picks);

      expect(picks.profileTypeId, isNull);
    });
  });

  group('SignUpVisitorForm.applyNationality', () {
    test('leaving Saudi clears the national id and keeps free-text birthplace',
        () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);
      form.applyProfile(_profile(placeOfBirth: 'منطقة الرياض'), picks);
      form.nationalId.text = '1234567890';

      form.applyNationality(picks, 'EG');

      expect(form.nationalId.text, isEmpty);
      expect(form.placeOfBirth.text, 'منطقة الرياض');
    });

    test('becoming Saudi drops a birthplace no region matches (D-469)', () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);
      form.applyProfile(_profile(nationalityCode: 'EG'), picks);
      form.placeOfBirth.text = 'Cairo';

      form.applyNationality(picks, 'SA');

      expect(form.birthRegionCode, isNull);
      expect(form.placeOfBirth.text, isEmpty);
    });

    test('re-picking the same Saudi-ness leaves the fields untouched', () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);
      form.applyProfile(_profile(nationalityCode: 'EG'), picks);
      form.documentNumber.text = 'A1234567';

      form.applyNationality(picks, 'EG');

      expect(form.documentNumber.text, 'A1234567');
    });
  });

  group('SignUpVisitorForm.toRequest', () {
    test('a Saudi request carries the national id and the Saudi mobile only',
        () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);
      form.applyProfile(
        _profile(placeOfBirth: 'منطقة الرياض', dateOfBirth: '1990-04-05'),
        picks,
      );
      form.nationalId.text = ' 1234567890 ';

      final request = form.toRequest(picks);

      expect(request.isSaudi, isTrue);
      expect(request.nationalId, '1234567890');
      expect(request.iqamaNumber, isNull);
      expect(request.passportNumber, isNull);
      expect(request.internationalMobile, isNull);
      // The Arabic digits are folded to the server's canonical shape.
      expect(request.saudiMobile, '0501234567');
      // The wire date stays ISO even though the field displays dd-MM-yyyy.
      expect(request.dateOfBirth, '1990-04-05');
      expect(request.interestIds, <String>['i1']);
    });

    test('a non-Saudi request carries the document number under its own key',
        () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);
      form.applyProfile(
        _profile(nationalityCode: 'EG', passportNumber: 'A1234567'),
        picks,
      );

      final request = form.toRequest(picks);

      expect(request.isSaudi, isFalse);
      expect(request.nationalId, isNull);
      expect(request.iqamaNumber, isNull);
      expect(request.passportNumber, 'A1234567');
    });

    test('an empty optional field is sent as null, never as an empty string',
        () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);
      form.applyProfile(_profile(), picks);
      form.jobTitle.text = '   ';

      final request = form.toRequest(picks);

      expect(request.jobTitle, isNull);
      expect(request.plateNumber, isNull);
      expect(request.dateOfBirth, isNull);
    });

    test('the draft carries the picked images alongside the request', () {
      final form = SignUpVisitorForm();
      addTearDown(form.dispose);
      final picks = VisitorProfileFormState()
        ..setLookups(countries: _countries);
      addTearDown(picks.dispose);
      form
        ..applyProfile(_profile(), picks)
        ..setIdImage(Uint8List.fromList(<int>[1, 2, 3]), 'id.png');

      final draft = form.toDraft(picks);

      expect(draft.idImageName, 'id.png');
      expect(draft.request.arabicName, 'محمد');

      form.clearIdImage();
      expect(form.toDraft(picks).idImageBytes, isNull);
    });
  });

  group('lockedVisitorProfileTypeId', () {
    const normal = ProfileTypeItem(
      id: 'n1',
      name: 'Normal',
      nameArabic: 'عادي',
      isVisitor: true,
    );
    const vip = ProfileTypeItem(
      id: 'v1',
      name: 'VIP',
      nameArabic: 'كبار',
      isVisitor: true,
    );

    test('picks the seeded Normal row when it exists (C5 — D-371)', () {
      expect(
        lockedVisitorProfileTypeId(const <ProfileTypeItem>[vip, normal]),
        'n1',
      );
    });

    test('falls back to the only row when there is exactly one', () {
      expect(lockedVisitorProfileTypeId(const <ProfileTypeItem>[vip]), 'v1');
    });

    test('leaves the type unassigned when the lookup is empty or ambiguous',
        () {
      expect(lockedVisitorProfileTypeId(const <ProfileTypeItem>[]), isNull);
      expect(
        lockedVisitorProfileTypeId(const <ProfileTypeItem>[vip, vip]),
        isNull,
      );
    });
  });

  group('countryPickerOptions', () {
    test('labels in the reading language and searches on both names', () {
      final arabic = countryPickerOptions(_countries, isArabic: true);
      final english = countryPickerOptions(_countries, isArabic: false);

      expect(arabic.first.label, 'السعودية');
      expect(english.first.label, 'Saudi Arabia');
      expect(arabic.first.value, 'SA');
      expect(arabic.first.search, contains('Saudi Arabia'));
      expect(english.first.search, contains('السعودية'));
    });
  });
}
