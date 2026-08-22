import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Data-driven role x route redirect matrix (B1 / D-694).
///
/// The `router_gate_test.dart` suite spot-checks single routes; this file is
/// the exhaustive grid: every auth-gated route pattern x every account state,
/// asserted against a hand-written expected table. Crucially the expectations
/// come from an independent oracle — each path is assigned an access tier by
/// hand from the route spec (SIMF-MAA-001 section 8 auth gate + D-519 role gate
/// + D-750 agenda public [reverses the D-576 session login-gate] + D-666
///   pending=guest + D-694 identity verification), NOT read back from the
///   router's own private `_authenticatedRoutes` / `_routeRoles` sets. So a
///   change to those sets that diverges from the spec makes a row fail here
///   instead of silently agreeing with itself. This is the test that would have
///   caught the D-666 face-capture regression: route 103 flipped from
///   reachable-by-pending to bounced-home, and no test noticed.
///
/// Known limitation: the aux auth routes (`/auth/forgot-password`,
/// `/auth/reset-password`, `/auth/verify-otp`, `/auth/badge*`,
/// `/auth/biometric-step-up`, `/account/my-devices`) live in the router's
/// separate aux table, so `routeNumberForPath` reports them as unnumbered and
/// the number-keyed gate sets cannot gate them one at a time — they read as
/// "public" via these pure functions and are backend-enforced instead. They are
/// the only intentional exclusion.
///
/// That `buildRouter` feeds the redirect `CurrentUser.effectiveAppRole` and not
/// the raw token role (D-666) is unreachable through [redirectDecision], which
/// takes the role as a parameter; it lives in
/// `router_effective_role_wiring_test.dart`.
void main() {
  // Access tiers, assigned by hand from the spec (the independent oracle).
  //  - public:        no sign-in required (open, or an in-screen prompt like
  //                   D-577 /live)
  //  - universalAuth: any signed-in account, incl. a pending one (effective
  //                   guest)
  //  - attendee:      Visitor + Exhibitor only
  //  - exhibitor / staff / moderator: that single role only
  const publicPaths = <String>[
    '/', // 13 home (role-aware, but reachable)
    '/splash', // 1 — the cold-start park; never gated (Page_001 L-6)
    '/onboarding', // 2
    '/sign-in', // 3
    '/sign-up', // 5
    '/sign-up/otp', // 6
    '/terms', // 9
    '/guest', // 12
    '/map', // 15
    '/speakers', // 19
    '/speakers/:speakerId', // 20
    '/delegations', // 21
    '/booths', // 22
    '/sponsors', // 23
    '/exhibitors/:boothId', // 220
    '/sponsors/:sponsorId', // 221
    '/archive', // 24
    '/sessions', // 16 — public again (D-750, reverses the D-576 login-gate)
    '/sessions/:sessionId', // 17 — public again (D-750, reverses D-576)
    '/live', // 25 — NOT redirect-gated (D-577 in-screen prompt)
    '/news', // 29
    '/news/:newsId', // 290
    '/media', // 30
    '/media-partners', // 31
    '/ai-summary', // 34
    '/chatbot', // 36
    '/about', // 37
    '/settings/accessibility', // 38
    '/more', // 41
    '/session-summaries', // 111
    '/booths/:boothId/map', // 112
    '/forum-guide', // 200
    '/faq', // 201
    '/session-presentations', // 202 — public (owner 2026-07-22; a guest opens the
    // "الجلسات" Sessions list from the home tile; reads are AllowAnonymous)
    '/contact-us', // 203
    '/about-app', // 207
    // (B18: /bilateral-meetings [204] + /saved-meetings [206] dropped with the
    //  dead routes themselves — no screen, no caller, nothing persisted.)
  ];

  const universalAuthPaths = <String>[
    '/sign-up/visitor', // 7
    '/sign-up/interests', // 701
    '/my-area/interests', // 702 — edit interests from My-Area (#14)
    '/my-area/mobile', // 703 — add / edit the mobile number (owner 2026-07-26)
    '/registration/success', // 10
    '/registration/status', // 11
    '/my-area', // 14
    '/badge', // 32
    '/notifications', // 33
    '/my-area/verify-identity', // 103 (D-694 — was attendee-gated; the fix)
  ];

  const attendeePaths = <String>[
    '/sessions/:sessionId/my-seat', // 18
    '/live/question', // 26
    '/meet', // 35
    '/rate', // 40
    '/contacts', // 100
    '/contacts/share', // 101
    '/contacts/scan', // 102
    '/requests', // 108
    '/meetings', // 116 (VIP-only enforced in-screen; role gate = attendee)
    '/sessions/:sessionId/pick-seat', // 109
    '/sessions/join', // 110
    '/my-sessions', // 113 (D-710 — restored)
    '/meeting-confirm', // 117 — the other-party Bi-Meeting confirm
  ];

  const exhibitorPaths = <String>[
    '/exhibitor/scan', // 106
    '/exhibitor/visitors', // 107
  ];

  const staffPaths = <String>[
    '/gates/scan', // 105
    '/staff/register-visitor', // 114
    '/staff/seating/:sessionId', // 118 — seating desk (D-771)
  ];

  const moderatorPaths = <String>[
    '/sessions/:sessionId/moderate', // 104
  ];

  // The redirect for a signed-in (or signed-out) state hitting a route pattern.
  // `role` is the effective role; a pending account is [AppRole.guest] (D-666).
  String? decide({
    required bool signedIn,
    required AppRole? role,
    required String path,
  }) =>
      redirectDecision(
        isInitial: false,
        isSignedIn: signedIn,
        goingTo: path,
        fullPath: path,
        appRole: role,
      );

  // Assert a tier's expected redirect per account state. '/sign-in' = auth
  // gate, '/' = role gate (sent home), null = allowed. The expected values are
  // the by-hand oracle, mirroring redirectDecision's spec but written from the
  // route's tier, not from the router's private sets.
  void expectTier(
    List<String> paths, {
    required String? signedOut,
    required String? pending,
    required String? visitor,
    required String? exhibitor,
    required String? staff,
    required String? moderator,
  }) {
    for (final p in paths) {
      expect(
        decide(signedIn: false, role: null, path: p),
        signedOut,
        reason: 'signed-out · $p',
      );
      expect(
        decide(signedIn: true, role: AppRole.guest, path: p),
        pending,
        reason: 'pending · $p',
      );
      expect(
        decide(signedIn: true, role: AppRole.visitor, path: p),
        visitor,
        reason: 'visitor · $p',
      );
      expect(
        decide(signedIn: true, role: AppRole.exhibitor, path: p),
        exhibitor,
        reason: 'exhibitor · $p',
      );
      expect(
        decide(signedIn: true, role: AppRole.staff, path: p),
        staff,
        reason: 'staff · $p',
      );
      expect(
        decide(signedIn: true, role: AppRole.moderator, path: p),
        moderator,
        reason: 'moderator · $p',
      );
    }
  }

  group('role x route redirect matrix (B1 / D-694)', () {
    test('public routes never redirect any state', () {
      expectTier(
        publicPaths,
        signedOut: null,
        pending: null,
        visitor: null,
        exhibitor: null,
        staff: null,
        moderator: null,
      );
    });

    test('universal-auth: signed-out to sign-in, every signed-in state OK', () {
      // The regression guard: pending is allowed (null) for all of these,
      // including /my-area/verify-identity (route 103).
      expectTier(
        universalAuthPaths,
        signedOut: '/sign-in',
        pending: null,
        visitor: null,
        exhibitor: null,
        staff: null,
        moderator: null,
      );
    });

    test('attendee routes: Visitor + Exhibitor; pending/staff/moderator home',
        () {
      expectTier(
        attendeePaths,
        signedOut: '/sign-in',
        pending: '/', // D-666 — pending presents as guest, sent home
        visitor: null,
        exhibitor: null,
        staff: '/',
        moderator: '/',
      );
    });

    test('exhibitor routes: Exhibitor only', () {
      expectTier(
        exhibitorPaths,
        signedOut: '/sign-in',
        pending: '/',
        visitor: '/',
        exhibitor: null,
        staff: '/',
        moderator: '/',
      );
    });

    test('staff routes: Staff only', () {
      expectTier(
        staffPaths,
        signedOut: '/sign-in',
        pending: '/',
        visitor: '/',
        exhibitor: '/',
        staff: null,
        moderator: '/',
      );
    });

    test('moderator routes: Moderator only (Staff does not inherit)', () {
      expectTier(
        moderatorPaths,
        signedOut: '/sign-in',
        pending: '/',
        visitor: '/',
        exhibitor: '/',
        staff: '/',
        moderator: null,
      );
    });

    test('cold-start parks protected routes on splash; public passes', () {
      // On the initial navigation the restore is unresolved, so any auth-gated
      // route parks on /splash regardless of role; public routes fall through.
      String? cold(String path) => redirectDecision(
            isInitial: true,
            isSignedIn: true,
            goingTo: path,
            fullPath: path,
            appRole: AppRole.visitor,
          );
      for (final p in <String>[
        ...universalAuthPaths,
        ...attendeePaths,
        ...exhibitorPaths,
        ...staffPaths,
        ...moderatorPaths,
      ]) {
        expect(cold(p), '/splash', reason: 'cold-start · $p');
      }
      for (final p in publicPaths) {
        expect(cold(p), isNull, reason: 'cold-start public · $p');
      }
    });

    test('every matrixed path is a real route pattern (no silent typos)', () {
      // A wrong path string would resolve to number null -> treated public ->
      // false-green. Assert each gated path maps to a known route number.
      for (final p in <String>[
        ...publicPaths,
        ...universalAuthPaths,
        ...attendeePaths,
        ...exhibitorPaths,
        ...staffPaths,
        ...moderatorPaths,
      ]) {
        expect(routeNumberForPath(p), isNotNull, reason: p);
      }
    });

    test('every route in the real table carries exactly one tier', () {
      // Coverage comes from the router; the verdicts stay hand-written, since
      // reading them back out would let the file agree with itself.
      final classified = <String>[
        ...publicPaths,
        ...universalAuthPaths,
        ...attendeePaths,
        ...exhibitorPaths,
        ...staffPaths,
        ...moderatorPaths,
      ];
      expect(
        classified.toSet().length,
        classified.length,
        reason: 'a path is listed in two tiers (or twice in one)',
      );

      // The unnumbered aux auth table is deliberately out of scope.
      final unclassified = <String>[
        for (final route in buildRoutes().whereType<GoRoute>())
          if (routeNumberForPath(route.path) != null &&
              !classified.contains(route.path))
            route.path,
      ];
      expect(
        unclassified,
        isEmpty,
        reason: 'route(s) declared with no access tier in this matrix',
      );
    });
  });
}
