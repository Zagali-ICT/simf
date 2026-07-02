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

    test('D-576 — the session detail is login-gated (was Guest-open)', () {
      expect(routePathRequiresAuth('/sessions/:sessionId'), isTrue);
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
      // المقابلات (1701:9406) — approved attendee only, so auth-gated.
      expect(routePathRequiresAuth('/my-meetings'), isTrue);
      // D-576 — the agenda + session detail are login-gated (redirect).
      expect(routePathRequiresAuth('/sessions'), isTrue);
      expect(routePathRequiresAuth('/sessions/:sessionId'), isTrue);
      // D-577 — /live is NOT redirect-gated; it shows an in-screen need-login
      // prompt instead, so a guest still lands on the live screen.
      expect(routePathRequiresAuth('/live'), isFalse);
    });

    test('guest-accessible content is not gated', () {
      expect(routePathRequiresAuth('/'), isFalse);
      expect(routePathRequiresAuth('/map'), isFalse);
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
      // The venue map stays public (D-576 gated /sessions, so use /map here).
      expect(
        redirectDecision(
          isInitial: false,
          isSignedIn: false,
          goingTo: '/map',
          fullPath: '/map',
        ),
        isNull,
      );
    });

    test('D-576 — a signed-out guest hitting /sessions or a session detail → '
        'sign-in', () {
      for (final p in <String>['/sessions', '/sessions/:sessionId']) {
        expect(
          redirectDecision(
            isInitial: false,
            isSignedIn: false,
            goingTo: p,
            fullPath: p,
          ),
          equals('/sign-in'),
          reason: p,
        );
      }
    });

    test('D-577 — a signed-out guest hitting /live is NOT redirected (the gate '
        'is in-screen on the live screen)', () {
      expect(
        redirectDecision(
          isInitial: false,
          isSignedIn: false,
          goingTo: '/live',
          fullPath: '/live',
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

    testWidgets('a public tab (/map) is NOT bounced when signed out',
        (tester) async {
      final auth = _AuthFlag();
      final router = await pumpShell(tester, auth);
      // The venue map is the remaining public tab (D-576 gated /sessions).
      router.go('/map');
      await tester.pumpAndSettle();
      expect(find.text('MAP'), findsOneWidget);

      auth.signOut();
      await tester.pumpAndSettle();

      // A public branch stays put when the session ends — no over-redirect.
      expect(find.text('MAP'), findsOneWidget);
      expect(find.text('SIGN-IN'), findsNothing);
    });

    testWidgets('D-576 — the /sessions tab bounces a signed-out guest',
        (tester) async {
      final auth = _AuthFlag();
      final router = await pumpShell(tester, auth);
      auth.signOut();
      await tester.pumpAndSettle();
      router.go('/sessions');
      await tester.pumpAndSettle();
      expect(find.text('SIGN-IN'), findsOneWidget);
      expect(find.text('SESSIONS'), findsNothing);
    });
  });

  group('role gate (D-519 — explicit allowed-roles sets)', () {
    const moderate = '/sessions/:sessionId/moderate';
    const gate = '/gates/scan';
    const register = '/staff/register-visitor';
    const scanVisitor = '/exhibitor/scan';
    const badge = '/badge';
    const meet = '/meet';

    // The redirect for a signed-in role hitting a route pattern.
    String? hit(String fullPath, AppRole role) => redirectDecision(
          isInitial: false,
          isSignedIn: true,
          goingTo: fullPath,
          fullPath: fullPath,
          appRole: role,
        );

    test('allowedRolesForPath returns the route\'s set (or null when open)', () {
      expect(allowedRolesForPath(moderate), <AppRole>{AppRole.moderator});
      expect(allowedRolesForPath(gate), <AppRole>{AppRole.staff});
      expect(allowedRolesForPath(register), <AppRole>{AppRole.staff});
      expect(allowedRolesForPath(scanVisitor), <AppRole>{AppRole.exhibitor});
      expect(
        allowedRolesForPath(meet),
        <AppRole>{AppRole.visitor, AppRole.exhibitor},
      );
      // Universal-auth + public routes carry no role restriction.
      expect(allowedRolesForPath(badge), isNull);
      expect(allowedRolesForPath('/sessions'), isNull);
    });

    test('the moderator desk is moderator-EXCLUSIVE (staff no longer inherits)',
        () {
      expect(hit(moderate, AppRole.moderator), isNull); // allowed
      expect(hit(moderate, AppRole.staff), '/'); // D-519: staff bounced
      expect(hit(moderate, AppRole.visitor), '/');
      expect(hit(moderate, AppRole.exhibitor), '/');
    });

    test('the gate + register pages are staff-exclusive', () {
      for (final p in <String>[gate, register]) {
        expect(hit(p, AppRole.staff), isNull, reason: p);
        expect(hit(p, AppRole.moderator), '/', reason: p);
        expect(hit(p, AppRole.visitor), '/', reason: p);
        expect(hit(p, AppRole.exhibitor), '/', reason: p);
      }
    });

    test('the exhibitor pages are exhibitor-exclusive', () {
      expect(hit(scanVisitor, AppRole.exhibitor), isNull);
      expect(hit(scanVisitor, AppRole.visitor), '/');
      expect(hit(scanVisitor, AppRole.staff), '/');
      expect(hit(scanVisitor, AppRole.moderator), '/');
    });

    test('attendee features allow Visitor + Exhibitor, exclude Staff/Moderator',
        () {
      expect(hit(meet, AppRole.visitor), isNull);
      expect(hit(meet, AppRole.exhibitor), isNull);
      expect(hit(meet, AppRole.staff), '/'); // focused
      expect(hit(meet, AppRole.moderator), '/'); // focused
    });

    test('المقابلات (my-meetings) is attendee-only, like the requests feed', () {
      const myMeetings = '/my-meetings';
      expect(
        allowedRolesForPath(myMeetings),
        <AppRole>{AppRole.visitor, AppRole.exhibitor},
      );
      expect(hit(myMeetings, AppRole.visitor), isNull);
      expect(hit(myMeetings, AppRole.exhibitor), isNull);
      expect(hit(myMeetings, AppRole.staff), '/');
      expect(hit(myMeetings, AppRole.moderator), '/');
    });

    test('the badge tab is universal — every signed-in role keeps it', () {
      for (final role in <AppRole>[
        AppRole.visitor,
        AppRole.exhibitor,
        AppRole.moderator,
        AppRole.staff,
      ]) {
        expect(hit(badge, role), isNull, reason: role.wireName);
      }
    });

    test('signed-out on a role-gated route still goes to sign-in first', () {
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

    test('routeAllowsRole drives the nav visibility per role', () {
      expect(routeAllowsRole(RouteNames.gateScanner, AppRole.staff), isTrue);
      expect(routeAllowsRole(RouteNames.gateScanner, AppRole.visitor), isFalse);
      expect(routeAllowsRole(RouteNames.myVisitors, AppRole.exhibitor), isTrue);
      expect(routeAllowsRole(RouteNames.myVisitors, AppRole.visitor), isFalse);
      expect(
        routeAllowsRole(RouteNames.sessionModerate, AppRole.moderator),
        isTrue,
      );
      expect(
        routeAllowsRole(RouteNames.sessionModerate, AppRole.staff),
        isFalse,
      );
      // Unrestricted entries show for everyone (including a null/guest role).
      expect(routeAllowsRole(RouteNames.aboutForum, AppRole.staff), isTrue);
      expect(routeAllowsRole(RouteNames.aboutForum, null), isTrue);
    });
  });
}
