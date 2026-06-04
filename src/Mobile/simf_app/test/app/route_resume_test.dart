import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/route_resume.dart';

void main() {
  group('isResumableLocation (Page_001 Logic L-5)', () {
    test('content routes are resumable', () {
      expect(isResumableLocation('/'), isTrue);
      expect(isResumableLocation('/sessions'), isTrue);
      expect(isResumableLocation('/sessions/123'), isTrue);
      expect(isResumableLocation('/my-area'), isTrue);
      expect(isResumableLocation('/map'), isTrue);
      expect(isResumableLocation('/speakers/7'), isTrue);
    });

    test(
      'transient bootstrap / auth / onboarding routes are not resumable',
      () {
        expect(isResumableLocation('/splash'), isFalse);
        expect(isResumableLocation('/onboarding'), isFalse);
        expect(isResumableLocation('/sign-in'), isFalse);
        expect(isResumableLocation('/sign-up'), isFalse);
        expect(isResumableLocation('/sign-up/otp'), isFalse);
        expect(isResumableLocation('/terms'), isFalse);
        expect(isResumableLocation('/registration/success'), isFalse);
        expect(isResumableLocation('/registration/status'), isFalse);
        expect(isResumableLocation('/guest'), isFalse);
        expect(isResumableLocation('/auth/verify-totp'), isFalse);
        expect(isResumableLocation('/auth/forgot-password'), isFalse);
      },
    );

    test('an empty location is not resumable', () {
      expect(isResumableLocation(''), isFalse);
    });
  });
}
