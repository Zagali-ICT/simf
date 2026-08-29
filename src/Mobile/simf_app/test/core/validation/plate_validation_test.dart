import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/validation/plate_validation.dart';

/// C6 (D-459) — the same cases as the server's `UserProfileTests` plate
/// theories, so the client/server mirror stays visibly in lockstep.
void main() {
  group('isStandardPlateNumber', () {
    test('accepts the Saudi standard (separators ignored, both orders)', () {
      expect(isStandardPlateNumber('ABJ1234'), isTrue);
      expect(isStandardPlateNumber('abj 1234'), isTrue);
      expect(isStandardPlateNumber('1234-ABJ'), isTrue);
      expect(isStandardPlateNumber('ابح1234'), isTrue); // Arabic plate letters
      expect(isStandardPlateNumber('ابح١٢٣٤'), isTrue); // + Arabic-Indic digits
      expect(isStandardPlateNumber('ABJ1'), isTrue);
    });

    // Owner 2026-08-29: a plate that IS entered must carry at least one letter
    // AND at least one digit, up to 3 letters + up to 4 digits.
    test('accepts a plate carrying letters AND digits', () {
      expect(isStandardPlateNumber('AB1234'), isTrue); // 2 letters + digits
      expect(isStandardPlateNumber('A1'), isTrue); // the floor
      // Arabic letters + digits
      expect(isStandardPlateNumber('ابح1234'), isTrue);
    });

    // These were ACCEPTED until 2026-08-29. The tightening is the point, so
    // they are inverted rather than deleted.
    test('rejects a plate missing letters or missing digits', () {
      expect(isStandardPlateNumber('ABJ'), isFalse); // letters only
      expect(isStandardPlateNumber('1234'), isFalse); // digits only
      // Arabic letters only
      expect(isStandardPlateNumber('ابح'), isFalse);
    });

    test('rejects out-of-range or non-plate characters', () {
      expect(isStandardPlateNumber('ABCD123'), isFalse); // 4 letters
      expect(isStandardPlateNumber('ABJ12345'), isFalse); // 5 digits
      expect(isStandardPlateNumber('12345'), isFalse); // 5 digits
      expect(isStandardPlateNumber('AB!1234'), isFalse); // symbol
      expect(isStandardPlateNumber('ABC1234'), isFalse); // C not a plate letter
      expect(isStandardPlateNumber('ابج1234'), isFalse); // ج not a plate letter
      expect(isStandardPlateNumber(''), isFalse);
    });
  });

  group('normalizePlate', () {
    test('canonicalises any input to the Latin code', () {
      expect(normalizePlate('abj 1234'), 'ABJ1234');
      expect(normalizePlate('ابح1234'), 'ABJ1234');
      expect(normalizePlate('ابح١٢٣٤'), 'ABJ1234');
      expect(normalizePlate('1234-ABJ'), '1234ABJ');
      expect(normalizePlate(''), isNull);
    });
  });

  group('toArabicPlate', () {
    test('renders the plate in Arabic letters + Arabic-Indic digits', () {
      expect(toArabicPlate('ABJ1234'), 'ابح١٢٣٤');
      expect(toArabicPlate('ابح1234'), 'ابح١٢٣٤');
      expect(toArabicPlate(''), isNull);
    });
  });

  group('assemblePlate', () {
    test('joins letters then digits by default', () {
      expect(
        assemblePlate(
          letter1: 'A',
          letter2: 'B',
          letter3: 'J',
          digits: '1234',
          digitsFirst: false,
        ),
        'ABJ1234',
      );
    });

    test('re-emits digits-first when the stored order led with digits', () {
      expect(
        assemblePlate(
          letter1: 'A',
          letter2: 'B',
          letter3: 'J',
          digits: '1234',
          digitsFirst: true,
        ),
        '1234ABJ',
      );
    });

    test('is empty when nothing is set (the plate is optional)', () {
      expect(assemblePlate(digits: '', digitsFirst: false), '');
      expect(assemblePlate(digits: '   ', digitsFirst: false), '');
    });

    test('trims surrounding whitespace on the digits', () {
      expect(
        assemblePlate(
          letter1: 'A',
          letter2: 'B',
          letter3: 'J',
          digits: ' 12 ',
          digitsFirst: false,
        ),
        'ABJ12',
      );
    });
  });

  group('parsePlate', () {
    test('splits a letters-first plate into the dropdowns + digits', () {
      final p = parsePlate('ABJ1234');
      expect(p.letter1, 'A');
      expect(p.letter2, 'B');
      expect(p.letter3, 'J');
      expect(p.digits, '1234');
      expect(p.digitsFirst, isFalse);
      expect(p.rawOverride, isNull);
    });

    test('records digits-first order so it round-trips (D-471)', () {
      final p = parsePlate('1234ABJ');
      expect(p.digitsFirst, isTrue);
      expect(
        assemblePlate(
          letter1: p.letter1,
          letter2: p.letter2,
          letter3: p.letter3,
          digits: p.digits,
          digitsFirst: p.digitsFirst,
        ),
        '1234ABJ',
      );
    });

    test('normalises an Arabic-script plate to the Latin dropdown codes', () {
      final p = parsePlate('ابح١٢٣٤');
      expect(p.letter1, 'A');
      expect(p.letter2, 'B');
      expect(p.letter3, 'J');
      expect(p.digits, '1234');
      expect(p.rawOverride, isNull);
    });

    test('blank → an empty override that clears the field', () {
      final p = parsePlate('');
      expect(p.rawOverride, '');
      expect(p.letter1, isNull);
      expect(p.digits, '');
    });

    test('keeps a non-conforming code verbatim as rawOverride (D-468)', () {
      // 'C' is not one of the 17 plate letters, so the dropdowns can't
      // represent it — the value is kept rather than silently erased.
      final p = parsePlate('ABC1234');
      expect(p.rawOverride, isNotNull);
      expect(p.letter1, isNull);
    });
  });
}
