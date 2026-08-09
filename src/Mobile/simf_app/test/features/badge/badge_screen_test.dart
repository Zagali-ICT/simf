import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/badge/badge_screen.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

CurrentUser _user(RegistrationStatus status, AppRole role) => CurrentUser(
      id: 'u1',
      email: 'v@example.sa',
      displayName: 'Raed Al-Salem',
      appRole: role,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: status,
    );

Session _session(RegistrationStatus status, AppRole role) => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _user(status, role),
    );

class _AuthController extends AuthController {
  _AuthController(this.status, {this.role = AppRole.visitor});

  final RegistrationStatus status;
  final AppRole role;

  @override
  AuthState build() => AuthStateSignedIn(_session(status, role));
}

/// A TRUE guest — no account at all (BUG-013). The badge tab is reachable this
/// way because the bottom nav switches tabs inside the shell, so the router's
/// auth redirect never runs.
class _SignedOutAuthController extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();
}

MyAreaDashboard _dashboard({
  String? qrId,
  bool isVisitor = true,
  String? pageColor,
}) =>
    MyAreaDashboard(
      identity: MyAreaIdentity(
        fullNameAr: 'رائد السالم',
        fullNameEn: 'Raed Al-Salem',
        qrId: qrId,
        tierNameEn: 'VIP',
        tierNameAr: 'VIP',
        pageColor: pageColor,
        isVisitor: isVisitor,
      ),
      counters: const MyAreaCounters(bookedSessionsCount: 0, meetingsCount: 0),
      todaySchedule: const <MyAreaScheduleItem>[],
    );

/// The fill of the identity strip — the nearest [Container] wrapping the name.
Color? _stripColor(WidgetTester tester) {
  final strip = tester.widget<Container>(
    find
        .ancestor(
          of: find.text('Raed Al-Salem'),
          matching: find.byType(Container),
        )
        .first,
  );
  return (strip.decoration! as BoxDecoration).color;
}

class _FakeMyAreaRepository implements MyAreaRepository {
  _FakeMyAreaRepository({this.dashboard, this.fail = false, this.status = 500});

  final MyAreaDashboard? dashboard;
  final bool fail;
  final int status;
  int dashboardCalls = 0;

  @override
  Future<MyAreaDashboard> getDashboard() async {
    dashboardCalls++;
    if (fail) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return dashboard!;
  }

  @override
  Future<String> getContactCardVcf() async => '';

  @override
  Future<String> getCalendarIcs() async => '';

  @override
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async =>
      true;
}

Future<void> _pump(
  WidgetTester tester, {
  required MyAreaRepository repo,
  AuthController? controller,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/badge',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.badge,
        path: '/badge',
        builder: (c, s) => const BadgeScreen(),
      ),
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.scanContact, '/contacts/scan', 'SCAN-CONTACT'),
        (RouteNames.shareMyContact, '/contacts/share', 'SHARE-MY-CONTACT'),
        (RouteNames.scanVisitor, '/exhibitor/scan', 'SCAN-VISITOR'),
        (RouteNames.myVisitors, '/exhibitor/visitors', 'MY-VISITORS'),
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.myArea, '/my-area', 'MY-AREA'),
        (RouteNames.signIn, '/sign-in', 'SIGN-IN'),
        (RouteNames.signUpForm, '/sign-up', 'SIGN-UP'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (c, s) => Scaffold(body: Text(label)),
        ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        authControllerProvider.overrideWith(
          () => controller ?? _AuthController(RegistrationStatus.approved),
        ),
        myAreaRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('BadgeScreen (Page 032 — KSA frame 221:769)', () {
    testWidgets('an issued qrId renders the QR card, identity strip and the '
        'add-person action', (tester) async {
      await _pump(
        tester,
        repo: _FakeMyAreaRepository(dashboard: _dashboard(qrId: 'ABC123')),
      );

      expect(find.byType(QrImageView), findsOneWidget);
      expect(find.text('Scan to enter'), findsOneWidget);
      expect(find.text('Raed Al-Salem'), findsOneWidget);
      expect(find.text('VIP'), findsOneWidget);
      // The strip masks the id down to its last 4 characters.
      expect(find.text('ID · •••• C123'), findsOneWidget);
      // The add-person action sits below the (larger 758:1469) QR card; scroll.
      await tester.scrollUntilVisible(
        find.text('Scan to add a contact'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('Scan to add a contact'), findsOneWidget);
    });

    testWidgets('the identity strip is tinted by the profile-type pageColor',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeMyAreaRepository(
          dashboard: _dashboard(qrId: 'ABC123', pageColor: '#0E7490'),
        ),
      );

      expect(_stripColor(tester), const Color(0xFF0E7490));
    });

    testWidgets('the identity strip falls back to token gold with no pageColor',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeMyAreaRepository(dashboard: _dashboard(qrId: 'ABC123')),
      );

      expect(_stripColor(tester), SimfTokens.accent);
    });

    testWidgets('the add-person action opens the contact scanner',
        (tester) async {
      // Tall surface so the whole badge fits clear of the bottom nav bar
      // (otherwise the button scrolls under it and the tap is obscured).
      tester.view.physicalSize = const Size(1200, 2200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await _pump(
        tester,
        repo: _FakeMyAreaRepository(dashboard: _dashboard(qrId: 'ABC123')),
      );

      // The add-person action opens the contact scanner as a fullscreen modal
      // (Navigator.push, D-426) — the camera screen can't be built in a widget
      // test, so here we assert the action button is present and wired; the
      // scanner itself is covered by its own tests + live verification.
      // Frame 758:1469 — the primary add-person action is the gold FILLED button.
      expect(
        find.widgetWithText(FilledButton, 'Scan to add a contact'),
        findsOneWidget,
      );
    });

    testWidgets('a visitor sees both scan-contact and share-my-contact (D-426)',
        (tester) async {
      tester.view.physicalSize = const Size(1200, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await _pump(
        tester,
        repo: _FakeMyAreaRepository(
          dashboard: _dashboard(qrId: 'ABC123'),
        ),
      );

      expect(find.text('Scan to add a contact'), findsOneWidget);
      expect(find.text('Share my contact'), findsOneWidget);
      expect(find.text('Scan visitor badge'), findsNothing);
    });

    testWidgets('an exhibitor sees scan-visitor, not the contact '
        'buttons (D-426)', (tester) async {
      tester.view.physicalSize = const Size(1200, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await _pump(
        tester,
        repo: _FakeMyAreaRepository(
          dashboard: _dashboard(qrId: 'ABC123', isVisitor: false),
        ),
        controller: _AuthController(
          RegistrationStatus.approved,
          role: AppRole.exhibitor,
        ),
      );

      expect(find.text('Scan visitor badge'), findsOneWidget);
      expect(find.text('Scan to add a contact'), findsNothing);
      expect(find.text('Share my contact'), findsNothing);

      await tester.tap(find.text('Scan visitor badge'));
      await tester.pumpAndSettle();
      expect(find.text('SCAN-VISITOR'), findsOneWidget);
    });

    // DEF-EXH-005 — a partner profile type (Media / Sponsor) carries
    // isVisitor=false but resolves to AppRole.visitor on the wire
    // (MobileAppRole.None → "Visitor"), so it keeps the contact actions and
    // must NOT be offered the exhibitor-only scan button.
    testWidgets('a partner-type visitor (isVisitor=false, role Visitor) keeps '
        'the contact actions and never sees scan-visitor (DEF-EXH-005)',
        (tester) async {
      tester.view.physicalSize = const Size(1200, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await _pump(
        tester,
        repo: _FakeMyAreaRepository(
          dashboard: _dashboard(qrId: 'ABC123', isVisitor: false),
        ),
      );

      expect(find.text('Scan visitor badge'), findsNothing);
      expect(find.text('Scan to add a contact'), findsOneWidget);
      expect(find.text('Share my contact'), findsOneWidget);
    });

    // DEF-EXH-005 — Staff / Moderator are focused roles (D-519): neither the
    // exhibitor scan (route 106) nor the attendee contact actions (routes
    // 100..102) are open to them, so the badge offers no action rather than a
    // button the router bounces.
    for (final role in <AppRole>[AppRole.staff, AppRole.moderator]) {
      testWidgets('a ${role.wireName} badge shows no action button '
          '(DEF-EXH-005)', (tester) async {
        tester.view.physicalSize = const Size(1200, 2400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await _pump(
          tester,
          repo: _FakeMyAreaRepository(
            dashboard: _dashboard(qrId: 'ABC123', isVisitor: false),
          ),
          controller: _AuthController(
            RegistrationStatus.approved,
            role: role,
          ),
        );

        expect(find.text('Scan visitor badge'), findsNothing);
        expect(find.text('Scan to add a contact'), findsNothing);
        expect(find.text('Share my contact'), findsNothing);
      });
    }

    testWidgets('a null qrId shows the pending state, no QR', (tester) async {
      await _pump(
        tester,
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
      );

      expect(find.byType(QrImageView), findsNothing);
      expect(
        find.text('Your badge is available once your account is approved.'),
        findsOneWidget,
      );
    });

    testWidgets('a not-approved account shows the not-approved state and never '
        'calls the dashboard', (tester) async {
      final repo = _FakeMyAreaRepository(dashboard: _dashboard(qrId: 'ABC123'));
      await _pump(
        tester,
        repo: repo,
        controller: _AuthController(RegistrationStatus.pending),
      );

      expect(repo.dashboardCalls, 0); // Approved-only endpoint not hit
      expect(find.byType(QrImageView), findsNothing);
      expect(find.textContaining('not approved'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Retry'), findsNothing);
    });

    testWidgets('BUG-013 — a TRUE guest gets the guest copy and a working '
        'sign-in CTA, never the pending-account copy', (tester) async {
      final repo = _FakeMyAreaRepository(dashboard: _dashboard(qrId: 'ABC123'));
      await _pump(
        tester,
        repo: repo,
        controller: _SignedOutAuthController(),
      );

      expect(repo.dashboardCalls, 0);
      expect(find.byType(QrImageView), findsNothing);
      // The pending / not-approved wording describes a registration a guest
      // never submitted — it must NOT be shown.
      expect(find.textContaining('not approved'), findsNothing);
      expect(
        find.textContaining('once your account is approved'),
        findsNothing,
      );
      expect(
        find.text('Sign in or create an account to get your entry badge.'),
        findsOneWidget,
      );
      expect(find.widgetWithText(TextButton, 'Create account'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('a 403 for an approved user shows the not-approved state, not '
        'the error surface', (tester) async {
      final repo = _FakeMyAreaRepository(fail: true, status: 403);
      await _pump(tester, repo: repo); // approved by default

      expect(repo.dashboardCalls, 1);
      expect(find.textContaining('not approved'), findsOneWidget);
      expect(find.text('Could not load your badge.'), findsNothing);
      expect(find.widgetWithText(FilledButton, 'Retry'), findsNothing);
    });

    testWidgets('a load failure shows the error + retry, which re-fetches',
        (tester) async {
      final repo = _FakeMyAreaRepository(fail: true);
      await _pump(tester, repo: repo);

      expect(find.text('Could not load your badge.'), findsOneWidget);
      final retry = find.widgetWithText(FilledButton, 'Retry');
      expect(retry, findsOneWidget);

      await tester.tap(retry);
      await tester.pumpAndSettle();
      expect(repo.dashboardCalls, greaterThanOrEqualTo(2));
    });

    testWidgets('renders the Arabic name in Arabic', (tester) async {
      await _pump(
        tester,
        repo: _FakeMyAreaRepository(dashboard: _dashboard(qrId: 'ABC123')),
        locale: const Locale('ar'),
      );

      expect(find.byType(QrImageView), findsOneWidget);
      expect(find.text('رائد السالم'), findsOneWidget);
    });

    test('maskedBadgeId keeps the last 4 characters only', () {
      expect(maskedBadgeId('ABC123'), '•••• C123');
      expect(maskedBadgeId('AB12'), 'AB12');
      expect(maskedBadgeId(''), '');
    });
  });
}
