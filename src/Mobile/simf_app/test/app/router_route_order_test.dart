import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/router.dart';

/// Regression cover for **BUG-016** — "Book a seat" was unreachable.
///
/// go_router matches in DECLARATION order and keeps the first hit. The route
/// table declared `/sessions/:sessionId` (session detail, #17) above the static
/// `/sessions/join` (the seat-booking hub, #110), so `/sessions/join` matched
/// the detail route with `sessionId = "join"`: the screen rendered "session not
/// found" (`GET /app/programme/sessions/join` → 404) and the hub — which has a
/// single entry point, the Profile "Book a seat" row — was dead.
///
/// [buildRoutes] now emits every static path before the parameterised ones, so
/// the shadowing cannot come back when a route is added anywhere in the table.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('route table match order (BUG-016)', () {
    late GoRouter router;

    setUp(() {
      router = GoRouter(routes: buildRoutes(), initialLocation: '/');
    });

    tearDown(() => router.dispose());

    /// The name of the deepest route the real table matches for [location].
    String? matchedRouteName(String location) {
      final match = router.configuration.findMatch(Uri.parse(location));
      final matches = match.matches;
      if (matches.isEmpty) {
        return null;
      }
      final route = matches.last.route;
      return route is GoRoute ? route.name : null;
    }

    test('/sessions/join resolves to the join-session hub, not session detail',
        () {
      expect(matchedRouteName('/sessions/join'), RouteNames.joinSessionHub);
    });

    test('/sessions/<id> still resolves to the session detail', () {
      expect(
        matchedRouteName('/sessions/3f1c9a2e-8d64-4a51-9c7b-0e2f5a6b7c8d'),
        RouteNames.sessionDetail,
      );
    });

    test('the parameterised session sub-routes still resolve', () {
      const id = '3f1c9a2e-8d64-4a51-9c7b-0e2f5a6b7c8d';
      expect(matchedRouteName('/sessions/$id/my-seat'), RouteNames.mySeat);
      expect(matchedRouteName('/sessions/$id/pick-seat'), RouteNames.seatPicker);
      expect(
        matchedRouteName('/sessions/$id/moderate'),
        RouteNames.sessionModerate,
      );
    });

    test('the other static/parameterised siblings are unaffected', () {
      expect(matchedRouteName('/sessions'), RouteNames.sessions);
      expect(matchedRouteName('/speakers'), RouteNames.speakers);
      expect(matchedRouteName('/speakers/s-1'), RouteNames.speakerProfile);
      expect(matchedRouteName('/sponsors'), RouteNames.sponsors);
      expect(matchedRouteName('/sponsors/sp-1'), RouteNames.sponsorDetail);
      expect(matchedRouteName('/'), RouteNames.home);
      expect(matchedRouteName('/auth/badge'), RouteNames.badgeSignIn);
    });

    test('no parameterised route is emitted before a static one', () {
      // The invariant that makes the class of bug impossible, asserted on the
      // real emitted table rather than on the declaration list.
      final paths = router.configuration.routes
          .whereType<GoRoute>()
          .map((r) => r.path)
          .toList();
      final firstParameterised = paths.indexWhere((p) => p.contains(':'));
      if (firstParameterised == -1) {
        return;
      }
      final staticAfterParameterised = paths
          .skip(firstParameterised)
          .where((p) => !p.contains(':'))
          .toList();
      expect(
        staticAfterParameterised,
        isEmpty,
        reason: 'These static paths are declared after a parameterised route '
            'and can be shadowed by it: $staticAfterParameterised',
      );
    });
  });
}
