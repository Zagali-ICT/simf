import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/validation/phone_validation.dart';

/// C4 (D-371) — the same cases as the server's `UserProfileTests` phone
/// theories, so the client/server mirror stays visibly in lockstep.
void main() {
  group('isStandardSaudiMobile', () {
    test('accepts the standard shapes (separators ignored, 00 or +)', () {
      expect(isStandardSaudiMobile('0501234567'), isTrue);
      expect(isStandardSaudiMobile('+966501234567'), isTrue);
      expect(isStandardSaudiMobile('009665 0123-4567'), isTrue); // 00 → +
      expect(isStandardSaudiMobile('050 123-4567'), isTrue);
      expect(isStandardSaudiMobile('٠٥٠١٢٣٤٥٦٧'), isTrue); // Arabic-Indic digits
    });

    test('rejects non-standard shapes', () {
      expect(isStandardSaudiMobile('0401234567'), isFalse); // not 05
      expect(isStandardSaudiMobile('050123456'), isFalse); // 9 digits
      expect(isStandardSaudiMobile('05012345678'), isFalse); // 11 digits
      expect(isStandardSaudiMobile('+966401234567'), isFalse); // not mobile 5
      expect(isStandardSaudiMobile(''), isFalse);
    });
  });

  group('isStandardInternationalMobile', () {
    test('accepts + or 00 international (separators ignored)', () {
      expect(isStandardInternationalMobile('+447700900123'), isTrue);
      expect(isStandardInternationalMobile('+44-7700900123'), isTrue);
      expect(isStandardInternationalMobile('+12025550123'), isTrue);
      expect(isStandardInternationalMobile('00447700900123'), isTrue); // 00 → +
    });

    test('rejects malformed shapes', () {
      expect(isStandardInternationalMobile('+0447700900123'), isFalse); // 0 lead
      expect(isStandardInternationalMobile('+44'), isFalse);
      expect(isStandardInternationalMobile('0501234567'), isFalse); // no + / 00
      expect(isStandardInternationalMobile(''), isFalse);
    });
  });

  group('normalizePhone', () {
    test('folds Arabic digits, strips separators, rewrites 00 → +', () {
      expect(normalizePhone('009665 0123-4567'), '+966501234567');
      expect(normalizePhone('+966 50 123 4567'), '+966501234567');
      expect(normalizePhone('٠٥٠١٢٣٤٥٦٧'), '0501234567');
      expect(normalizePhone(''), '');
    });
  });
}
