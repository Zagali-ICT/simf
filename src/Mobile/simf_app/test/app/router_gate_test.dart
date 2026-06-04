import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/router.dart';

void main() {
  group('routePathRequiresAuth (auth gate, SIMF-MAA-001 §8)', () {
    test('the My-seat sub-route requires auth (not shadowed by session detail)',
        () {
      // Regression: the old loose prefix match resolved this pattern to the
      // un-gated session-detail (#17), leaving My-seat (#18) reachable signed-out.
      expect(routePathRequiresAuth('/sessions/:sessionId/my-seat'), isTrue);
    });

    test('the session detail itself is open (Guest)', () {
      expect(routePathRequiresAuth('/sessions/:sessionId'), isFalse);
    });

    test('the explicitly gated screens require auth', () {
      // Page_007 L-1 — visitor profile completion is AUTH-only.
      expect(routePathRequiresAuth('/sign-up/visitor'), isTrue);
      expect(routePathRequiresAuth('/my-area'), isTrue);
      expect(routePathRequiresAuth('/badge'), isTrue);
      expect(routePathRequiresAuth('/notifications'), isTrue);
      expect(routePathRequiresAuth('/meet'), isTrue);
      expect(routePathRequiresAuth('/live/question'), isTrue);
    });

    test('guest-accessible content is not gated', () {
      expect(routePathRequiresAuth('/'), isFalse);
      expect(routePathRequiresAuth('/map'), isFalse);
      expect(routePathRequiresAuth('/sessions'), isFalse);
      expect(routePathRequiresAuth('/speakers/:speakerId'), isFalse);
    });

    test('an unknown or null pattern is never gated', () {
      expect(routePathRequiresAuth(null), isFalse);
      expect(routePathRequiresAuth('/does-not-exist'), isFalse);
    });

    test('routeNumberForPath matches the pattern exactly', () {
      expect(routeNumberForPath('/sessions/:sessionId/my-seat'), equals(18));
      expect(routeNumberForPath('/sessions/:sessionId'), equals(17));
      expect(routeNumberForPath('/'), equals(13));
      expect(
        routeNumberForPath('/sessions/123/my-seat'),
        isNull,
        reason: 'A concrete location is not a route pattern.',
      );
    });
  });
}
