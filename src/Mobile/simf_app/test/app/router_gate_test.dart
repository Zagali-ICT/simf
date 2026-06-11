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
      // Page_007 / Page_007-01 L-1 — profile data + interests are AUTH-only (D-332).
      expect(routePathRequiresAuth('/sign-up/visitor'), isTrue);
      expect(routePathRequiresAuth('/sign-up/interests'), isTrue);
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

  group('redirectDecision (auth-gate redirect)', () {
    test(
        'a signed-in user on /sign-in is NOT bounced — SignInScreen owns the '
        'post-sign-in route (D-295)', () {
      // Regression: a blunt `/sign-in -> /` redirect fired on the auth-state
      // change, disposed SignInScreen before _routeAfterSignIn ran, and stranded
      // profile-incomplete visitors on Home instead of the profile screen.
      expect(
        redirectDecision(
          isInitial: false,
          isSignedIn: true,
          goingTo: '/sign-in',
          fullPath: '/sign-in',
        ),
        isNull,
      );
    });

    test('cold-start holds a protected route on the splash (D-295)', () {
      expect(
        redirectDecision(
          isInitial: true,
          isSignedIn: false,
          goingTo: '/my-area',
          fullPath: '/my-area',
        ),
        equals('/splash'),
      );
    });

    test('cold-start does NOT pin a public route on the splash (D-295)', () {
      // The splash's own route-out to onboarding / sign-in must never be bounced
      // back to the splash, or a stalled restore would strand the user (L-6).
      expect(
        redirectDecision(
          isInitial: true,
          isSignedIn: false,
          goingTo: '/onboarding',
          fullPath: '/onboarding',
        ),
        isNull,
      );
      expect(
        redirectDecision(
          isInitial: true,
          isSignedIn: false,
          goingTo: '/sign-in',
          fullPath: '/sign-in',
        ),
        isNull,
      );
    });

    test('a protected route while signed out redirects to sign-in', () {
      expect(
        redirectDecision(
          isInitial: false,
          isSignedIn: false,
          goingTo: '/badge',
          fullPath: '/badge',
        ),
        equals('/sign-in'),
      );
    });

    test('a protected route while signed in is allowed', () {
      expect(
        redirectDecision(
          isInitial: false,
          isSignedIn: true,
          goingTo: '/badge',
          fullPath: '/badge',
        ),
        isNull,
      );
    });

    test('a public route is always allowed', () {
      expect(
        redirectDecision(
          isInitial: false,
          isSignedIn: false,
          goingTo: '/sessions',
          fullPath: '/sessions',
        ),
        isNull,
      );
    });
  });
}
