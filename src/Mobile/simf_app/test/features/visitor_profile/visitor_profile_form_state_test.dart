import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_form_state.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

void main() {
  late VisitorProfileFormState state;
  late int notifications;

  setUp(() {
    state = VisitorProfileFormState();
    notifications = 0;
    state.addListener(() => notifications++);
  });

  tearDown(() => state.dispose());

  group('defaults match what the screens declared', () {
    test('starts empty, with the frame defaults', () {
      expect(state.nationalityCode, isNull);
      expect(state.profileTypeId, isNull);
      expect(state.organisationId, isNull);
      expect(state.gender, AppGender.unspecified);
      expect(state.docType, VisitorDocType.iqama);
      expect(state.triedSubmit, isFalse);
      expect(state.countries, isEmpty);
      expect(state.profileTypes, isEmpty);
    });
  });

  group('isSaudi derives from the nationality pick (D-373)', () {
    test('is false until SA is chosen', () {
      expect(state.isSaudi, isFalse);
      state.nationalityCode = 'GB';
      expect(state.isSaudi, isFalse);
    });

    test('is true for SA', () {
      state.nationalityCode = 'SA';
      expect(state.isSaudi, isTrue);
    });
  });

  group('notification', () {
    test('fires on a real change', () {
      state.nationalityCode = 'SA';
      state.gender = AppGender.male;
      state.docType = VisitorDocType.passport;
      state.triedSubmit = true;
      expect(notifications, 4);
    });

    // A screen rebuilds on every notification, and these setters are called
    // from pickers that re-emit the current value. Without this guard, opening
    // a picker and choosing the same entry would rebuild the form.
    test('does NOT fire when the value is unchanged', () {
      state.nationalityCode = 'SA';
      state.nationalityCode = 'SA';
      state.gender = AppGender.unspecified;
      state.docType = VisitorDocType.iqama;
      state.triedSubmit = false;
      expect(notifications, 1);
    });

    // Both screens load their lookups concurrently and assign them together.
    test('setLookups notifies once for both lists', () {
      state.setLookups(
        countries: const <CountryItem>[],
        profileTypes: const <ProfileTypeItem>[],
      );
      expect(notifications, 1);
    });
  });

  group('resetForNextEntry', () {
    test('clears the picks and the submit flag', () {
      state
        ..nationalityCode = 'SA'
        ..profileTypeId = 'p1'
        ..organisationId = 'o1'
        ..gender = AppGender.female
        ..docType = VisitorDocType.passport
        ..triedSubmit = true;

      state.resetForNextEntry();

      expect(state.nationalityCode, isNull);
      expect(state.profileTypeId, isNull);
      expect(state.organisationId, isNull);
      expect(state.gender, AppGender.unspecified);
      expect(state.docType, VisitorDocType.iqama);
      expect(state.triedSubmit, isFalse);
    });

    // The walk-in desk registers visitor after visitor; re-fetching the country
    // list between each would be wasteful, so the lookups deliberately survive.
    test('KEEPS the loaded lookups', () {
      state.setLookups(
        countries: const <CountryItem>[
          CountryItem(code: 'SA', name: 'Saudi Arabia', nameArabic: 'السعودية'),
        ],
      );
      state.resetForNextEntry();
      expect(state.countries, hasLength(1));
    });
  });
}
