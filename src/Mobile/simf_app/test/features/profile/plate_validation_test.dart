import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/profile/plate_validation.dart';

/// C6 (D-371) — the same cases as the server's `UserProfileTests` plate
/// theories, so the client/server mirror stays visibly in lockstep.
void main() {
  group('isStandardPlateNumber', () {
    test('accepts the Saudi standard (separators ignored, both orders)', () {
      expect(isStandardPlateNumber('ABJ1234'), isTrue);
      expect(isStandardPlateNumber('abj 1234'), isTrue);
      expect(isStandardPlateNumber('1234-ABJ'), isTrue);
      expect(isStandardPlateNumber('أبج1234'), isTrue);
      expect(isStandardPlateNumber('ABJ1'), isTrue);
    });

    test('rejects non-standard shapes', () {
      expect(isStandardPlateNumber('AB1234'), isFalse); // 2 letters
      expect(isStandardPlateNumber('ABCD123'), isFalse); // 4 letters
      expect(isStandardPlateNumber('ABJ12345'), isFalse); // 5 digits
      expect(isStandardPlateNumber('ABJ'), isFalse); // no digits
      expect(isStandardPlateNumber('1234567'), isFalse); // digits only
      expect(isStandardPlateNumber('AB!1234'), isFalse); // symbol
      expect(isStandardPlateNumber(''), isFalse);
    });
  });
}
