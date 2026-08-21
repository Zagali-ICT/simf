import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// D-666 wiring: the role the router's redirect actually receives.
///
/// `router_role_matrix_test.dart` grids every route against every role, but it
/// calls [redirectDecision] and PASSES the role in — so the one line that
/// derives that role from the live [AuthState],
/// `authState.session.user.effectiveAppRole` inside `buildRouter`, was never
/// executed by any test. Swapping it for the raw `.appRole` therefore changed
/// nothing anywhere in the suite while undoing D-666 outright: a pending
/// account holds a Visitor token and would walk into every attendee-gated
/// screen until an admin approved it.
///
/// These tests drive the REAL router built by [routerProvider] — the same
/// top-level redirect closure the app runs — against a faked auth state, and
/// assert where a navigation lands. `effectiveAppRole` collapses to
/// [AppRole.guest] until the account is approved, so the discriminator is a
/// signed-in account whose token role and effective role DISAGREE.
class _FakeAuth extends AuthController {
  _FakeAuth(this._state);

  final AuthState _state;

  @override
  AuthState build() => _state;
}

AuthState _signedIn(AppRole role, RegistrationStatus status) =>
    AuthStateSignedIn(
      Session(
        accessToken: 'access',
        refreshToken: 'refresh',
        accessTokenExpiresAt: DateTime.utc(2099),
        user: CurrentUser(
          id: 'u-1',
          email: 'pending@simf.test',
          displayName: 'Pending Visitor',
          appRole: role,
          preferredLanguage: PreferredLanguage.fromJson('ar'),
          registrationStatus: status,
        ),
      ),
    );

void main() {
  /// Where the real router sends a navigation to [location] for [auth].
  ///
  /// `configuration.redirect` runs the top-level redirect closure (and follows
  /// it recursively, exactly as go_router does on a real `go`) without building
  /// a single screen, so this stays a gate test rather than a render test. The
  /// pumped [Builder] exists only to hand it a live [BuildContext].
  Future<String> land(
    WidgetTester tester,
    AuthState auth,
    String location,
  ) async {
    final container = ProviderContainer(
      overrides: <Override>[
        authControllerProvider.overrideWith(() => _FakeAuth(auth)),
      ],
    );
    addTearDown(container.dispose);

    late BuildContext context;
    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: Builder(
          builder: (c) {
            context = c;
            return const SizedBox.shrink();
          },
        ),
      ),
    );

    final router = container.read(routerProvider);
    addTearDown(router.dispose);
    final resolved = await router.configuration.redirect(
      context,
      router.configuration.findMatch(Uri.parse(location)),
      redirectHistory: <RouteMatchList>[],
    );
    return resolved.uri.toString();
  }

  group('buildRouter feeds the redirect the EFFECTIVE role (D-666)', () {
    testWidgets('a pending Visitor token is gated as a guest, not a visitor',
        (tester) async {
      // The whole point of effectiveAppRole: the token says visitor, approval
      // has not happened, so the attendee routes must bounce home.
      final pending = _signedIn(AppRole.visitor, RegistrationStatus.pending);
      for (final path in <String>[
        '/meet',
        '/rate',
        '/contacts',
        '/requests',
        '/meetings',
      ]) {
        expect(await land(tester, pending, path), '/', reason: path);
      }
    });

    testWidgets('a rejected Visitor token is gated as a guest too',
        (tester) async {
      final rejected = _signedIn(AppRole.visitor, RegistrationStatus.rejected);
      expect(await land(tester, rejected, '/meet'), '/');
    });

    testWidgets('a pending account still reaches its universal-auth routes',
        (tester) async {
      // The control that stops the gate from degenerating into "bounce every
      // pending navigation": sign-up face capture and the status gate are the
      // pending account's only way forward (D-694).
      final pending = _signedIn(AppRole.visitor, RegistrationStatus.pending);
      for (final path in <String>[
        '/my-area/verify-identity',
        '/registration/status',
        '/badge',
      ]) {
        expect(await land(tester, pending, path), path, reason: path);
      }
    });

    testWidgets('an APPROVED Visitor reaches the same attendee routes',
        (tester) async {
      // The positive control: approval is what changes the outcome, not the
      // route. Without this the group would pass on a redirect that bounced
      // everyone.
      final approved = _signedIn(AppRole.visitor, RegistrationStatus.approved);
      for (final path in <String>['/meet', '/rate', '/contacts']) {
        expect(await land(tester, approved, path), path, reason: path);
      }
    });

    testWidgets('a pending Staff token cannot reach the staff-only pages',
        (tester) async {
      final pendingStaff = _signedIn(AppRole.staff, RegistrationStatus.pending);
      expect(await land(tester, pendingStaff, '/gates/scan'), '/');
      final approvedStaff =
          _signedIn(AppRole.staff, RegistrationStatus.approved);
      expect(await land(tester, approvedStaff, '/gates/scan'), '/gates/scan');
    });

    testWidgets('signed out still goes to sign-in, not home', (tester) async {
      expect(
        await land(tester, const AuthStateSignedOut(), '/meet'),
        '/sign-in',
      );
    });
  });
}
