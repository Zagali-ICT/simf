import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// These validators were members of a 1393-line screen's State, so exercising
/// them meant pumping the whole sign-up form. As pure functions they are
/// testable directly, which is the point of the move: the rules that decide
/// whether a visitor can register are now checkable in milliseconds.
void main() {
  const ar = AppL10n(Locale('ar'));
  const en = AppL10n(Locale('en'));

  group('name', () {
    test('rejects empty', () {
      expect(validateArabicName('', ar), ar.requiredField);
      expect(validateEnglishName('   ', en), en.requiredField);
    });

    test('rejects the wrong script', () {
      expect(validateArabicName('John Smith', ar), ar.arabicNameLettersOnly);
      expect(validateEnglishName('محمد عبدالله', en), en.englishNameLettersOnly);
    });

    test('requires more than one part', () {
      expect(validateEnglishName('John', en), en.fullNameParts);
      expect(validateArabicName('محمد', ar), ar.fullNameParts);
    });

    test('accepts a full name in the matching script', () {
      expect(validateEnglishName('John Smith', en), isNull);
      expect(validateArabicName('محمد عبدالله', ar), isNull);
    });
  });

  group('national id', () {
    // `1` + 9 digits AND a passing Luhn check, mirroring the server.
    test('accepts a well-formed Saudi id', () {
      expect(validateNationalId('1234567897', en), isNull);
    });

    test('rejects a malformed one', () {
      expect(validateNationalId('123', en), en.nationalIdInvalid);
      expect(validateNationalId('', en), en.nationalIdInvalid);
    });

    // The shape alone is not enough: this is `1` + 9 digits but fails Luhn.
    // Asserting it keeps the checksum from being dropped by a later "cleanup".
    test('rejects a right-shaped id that fails the checksum', () {
      expect(validateNationalId('1234567890', en), en.nationalIdInvalid);
    });

    test('rejects an Iqama number in the national-id field', () {
      expect(validateNationalId('2234567895', en), en.nationalIdInvalid);
    });
  });

  group('document number', () {
    // The same input is valid or invalid depending on the document, which is
    // why docType is a parameter rather than screen state.
    test('is required whichever document it is', () {
      for (final kind in VisitorDocType.values) {
        expect(validateDocumentNumber('', en, kind), en.documentRequired,
            reason: kind.name,);
      }
    });

    test('applies the iqama rule to an iqama', () {
      expect(
        validateDocumentNumber('!!!', en, VisitorDocType.iqama),
        en.iqamaInvalid,
      );
    });

    test('applies the passport rule to a passport', () {
      expect(
        validateDocumentNumber('!!!', en, VisitorDocType.passport),
        en.passportInvalid,
      );
    });
  });

  group('mobile', () {
    // D-723 - mobile is required; only the plate stays optional.
    test('is required', () {
      expect(validateSaudiMobile('', en), en.mobileRequired);
      expect(validateInternationalMobile('  ', en), en.mobileRequired);
    });

    test('accepts the standard Saudi form', () {
      expect(validateSaudiMobile('0512345678', en), isNull);
    });

    test('rejects a non-Saudi number on the Saudi field', () {
      expect(validateSaudiMobile('12345', en), en.saudiMobileInvalid);
    });
  });

  group('plate', () {
    // The one optional field.
    test('accepts empty', () {
      expect(validatePlate('', en), isNull);
      expect(validatePlate('   ', en), isNull);
    });

    test('rejects a malformed plate when one is given', () {
      expect(validatePlate('!!', en), en.plateNumberInvalid);
    });
  });
}
