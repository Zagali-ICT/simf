import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// A toggleable auth flag used as the router's [Listenable] refresh source, so a
/// test can flip "signed in → signed out" and re-run the redirect — exactly the
/// `refreshListenable`-triggered path that pure `redirectDecision` tests cannot
/// exercise.
class _AuthFlag extends ChangeNotifier {
  bool signedIn = true;
  void signOut() {
    signedIn = false;
    notifyListeners();
  }
}

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

  group('StatefulShellRoute auth gate (D-422 — fixed-bar shell)', () {
    // The 5 bottom-nav tabs moved into a StatefulShellRoute. This drives the
    // real shell + refreshListenable + fullPath-keyed redirect to prove the
    // gate still fires when the session ends while sitting on a gated tab
    // (/badge) — the case the pure redirectDecision tests cannot cover, and
    // the one flagged in review (go_router fullPath-on-refresh).
    Future<GoRouter> pumpShell(WidgetTester tester, _AuthFlag auth) async {
      StatefulShellBranch branch(String name, String path, String label) =>
          StatefulShellBranch(
            routes: <RouteBase>[
              GoRoute(
                name: name,
                path: path,
                builder: (c, s) => Center(child: Text(label)),
              ),
            ],
          );
      final router = GoRouter(
        initialLocation: '/badge',
        refreshListenable: auth,
        redirect: (context, state) {
          if (routePathRequiresAuth(state.fullPath) && !auth.signedIn) {
            return '/sign-in';
          }
          return null;
        },
        routes: <RouteBase>[
          StatefulShellRoute.indexedStack(
            builder: (context, state, navigationShell) => navigationShell,
            branches: <StatefulShellBranch>[
              branch(RouteNames.home, '/', 'HOME'),
              branch(RouteNames.sessions, '/sessions', 'SESSIONS'),
              branch(RouteNames.badge, '/badge', 'BADGE'),
              branch(RouteNames.venueMap, '/map', 'MAP'),
              branch(RouteNames.myArea, '/my-area', 'MY-AREA'),
            ],
          ),
          GoRoute(
            name: RouteNames.signIn,
            path: '/sign-in',
            builder: (c, s) => const Center(child: Text('SIGN-IN')),
          ),
        ],
      );
      await tester.pumpWidget(MaterialApp.router(routerConfig: router));
      await tester.pumpAndSettle();
      return router;
    }

    testWidgets('session ends on the /badge tab → bounced to /sign-in',
        (tester) async {
      final auth = _AuthFlag();
      await pumpShell(tester, auth);
      expect(find.text('BADGE'), findsOneWidget);

      auth.signOut();
      await tester.pumpAndSettle();

      expect(find.text('SIGN-IN'), findsOneWidget);
      expect(find.text('BADGE'), findsNothing);
    });

    testWidgets('a public tab (/sessions) is NOT bounced when signed out',
        (tester) async {
      final auth = _AuthFlag();
      final router = await pumpShell(tester, auth);
      // Move to the public sessions branch first.
      router.go('/sessions');
      await tester.pumpAndSettle();
      expect(find.text('SESSIONS'), findsOneWidget);

      auth.signOut();
      await tester.pumpAndSettle();

      // A public branch stays put when the session ends — no over-redirect.
      expect(find.text('SESSIONS'), findsOneWidget);
      expect(find.text('SIGN-IN'), findsNothing);
    });
  });

  group('role gate (D-405)', () {
    const moderate = '/sessions/:sessionId/moderate';

    test('requiredRoleForPath returns moderator for the Q&A desk', () {
      expect(requiredRoleForPath(moderate), AppRole.moderator);
      expect(requiredRoleForPath('/badge'), isNull);
    });

    test('a visitor hitting a moderator route is sent home', () {
      expect(
        redirectDecision(
          isInitial: false,
          isSignedIn: true,
          goingTo: '/sessions/s1/moderate',
          fullPath: moderate,
          appRole: AppRole.visitor,
        ),
        '/',
      );
    });

    test('a moderator (or staff, higher) is allowed through', () {
      for (final role in <AppRole>[AppRole.moderator, AppRole.staff]) {
        expect(
          redirectDecision(
            isInitial: false,
            isSignedIn: true,
            goingTo: '/sessions/s1/moderate',
            fullPath: moderate,
            appRole: role,
          ),
          isNull,
        );
      }
    });

    test('signed-out on a moderator route still goes to sign-in first', () {
      expect(
        redirectDecision(
          isInitial: false,
          isSignedIn: false,
          goingTo: '/sessions/s1/moderate',
          fullPath: moderate,
        ),
        '/sign-in',
      );
    });

    test('the gate scanner requires staff exactly', () {
      expect(requiredRoleForPath('/gates/scan'), AppRole.staff);
      // A moderator (below staff) is sent home; staff is allowed.
      expect(
        redirectDecision(
          isInitial: false,
          isSignedIn: true,
          goingTo: '/gates/scan',
          fullPath: '/gates/scan',
          appRole: AppRole.moderator,
        ),
        '/',
      );
      expect(
        redirectDecision(
          isInitial: false,
          isSignedIn: true,
          goingTo: '/gates/scan',
          fullPath: '/gates/scan',
          appRole: AppRole.staff,
        ),
        isNull,
      );
    });
  });
}
