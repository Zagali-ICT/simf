import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/router.dart';

void main() {
  group('routePathRequiresAuth (auth gate, SIMF-MAA-001 §8)', () {
    test('the My-seat sub-route requires auth (not shadowed by agenda detail)',
        () {
      // Regression: the old loose prefix match resolved this pattern to the
      // un-gated agenda-detail (#17), leaving My-seat (#18) reachable signed-out.
      expect(routePathRequiresAuth('/agenda/:sessionId/my-seat'), isTrue);
    });

    test('the agenda detail itself is open (Guest)', () {
      expect(routePathRequiresAuth('/agenda/:sessionId'), isFalse);
    });

    test('the explicitly gated screens require auth', () {
      expect(routePathRequiresAuth('/my-area'), isTrue);
      expect(routePathRequiresAuth('/badge'), isTrue);
      expect(routePathRequiresAuth('/notifications'), isTrue);
      expect(routePathRequiresAuth('/meet'), isTrue);
      expect(routePathRequiresAuth('/live/question'), isTrue);
      expect(routePathRequiresAuth('/live/interview'), isTrue);
    });

    test('guest-accessible content is not gated', () {
      expect(routePathRequiresAuth('/'), isFalse);
      expect(routePathRequiresAuth('/map'), isFalse);
      expect(routePathRequiresAuth('/agenda'), isFalse);
      expect(routePathRequiresAuth('/speakers/:speakerId'), isFalse);
    });

    test('an unknown or null pattern is never gated', () {
      expect(routePathRequiresAuth(null), isFalse);
      expect(routePathRequiresAuth('/does-not-exist'), isFalse);
    });

    test('routeNumberForPath matches the pattern exactly', () {
      expect(routeNumberForPath('/agenda/:sessionId/my-seat'), equals(18));
      expect(routeNumberForPath('/agenda/:sessionId'), equals(17));
      expect(routeNumberForPath('/'), equals(13));
      expect(
        routeNumberForPath('/agenda/123/my-seat'),
        isNull,
        reason: 'A concrete location is not a route pattern.',
      );
    });
  });
}
