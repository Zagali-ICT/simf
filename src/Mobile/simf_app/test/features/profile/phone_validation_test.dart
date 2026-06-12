import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/profile/phone_validation.dart';

/// C4 (D-371) — the same cases as the server's `UserProfileTests` phone
/// theories, so the client/server mirror stays visibly in lockstep.
void main() {
  group('isStandardSaudiMobile', () {
    test('accepts the standard shapes (separators ignored)', () {
      expect(isStandardSaudiMobile('0501234567'), isTrue);
      expect(isStandardSaudiMobile('+966501234567'), isTrue);
      expect(isStandardSaudiMobile('050 123-4567'), isTrue);
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
    test('accepts E.164 (separators ignored)', () {
      expect(isStandardInternationalMobile('+447700900123'), isTrue);
      expect(isStandardInternationalMobile('+44-7700900123'), isTrue);
      expect(isStandardInternationalMobile('+12025550123'), isTrue);
    });

    test('rejects non-E.164 shapes', () {
      expect(isStandardInternationalMobile('00447700900123'), isFalse);
      expect(isStandardInternationalMobile('+0447700900123'), isFalse);
      expect(isStandardInternationalMobile('+44'), isFalse);
      expect(isStandardInternationalMobile('0501234567'), isFalse); // no "+"
      expect(isStandardInternationalMobile(''), isFalse);
    });
  });
}
