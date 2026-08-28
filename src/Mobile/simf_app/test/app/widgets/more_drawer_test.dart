import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/more_drawer.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../features/accessibility/_fake_prefs.dart';
import '../../support/simf_test_scope.dart';

Session _session({
  AppRole role = AppRole.visitor,
  RegistrationStatus status = RegistrationStatus.approved,
}) =>
    Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: CurrentUser(
        id: 'u1',
        email: 'v@example.sa',
        displayName: 'Raed',
        appRole: role,
        preferredLanguage: PreferredLanguage.fromJson('ar'),
        registrationStatus: status,
      ),
    );

/// Records sign-out so the logout flow can be asserted without a real revoke.
class _RecordingAuthController extends AuthController {
  _RecordingAuthController({
    required this.signedIn,
    this.role = AppRole.visitor,
    this.status = RegistrationStatus.approved,
  });

  final bool signedIn;
  final AppRole role;
  final RegistrationStatus status;
  int signOutCalls = 0;

  @override
  AuthState build() => signedIn
      ? AuthStateSignedIn(_session(role: role, status: status))
      : const AuthStateSignedOut();

  @override
  Future<void> signOut() async {
    signOutCalls++;
    state = const AuthStateSignedOut();
  }
}

class _FakeMyAreaRepository implements MyAreaRepository {
  @override
  Future<MyAreaDashboard> getDashboard() async => throw UnimplementedError();
  @override
  Future<String> getContactCardVcf() async => '';
  @override
  Future<String> getCalendarIcs() async =>
      'BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n';
  @override
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async =>
      true;
}

Future<void> _pump(
  WidgetTester tester, {
  required AuthController auth,
}) async {
  final prefs = FakePrefs();
  final router = GoRouter(
    initialLocation: '/host',
    routes: <RouteBase>[
      GoRoute(
        path: '/host',
        builder: (c, s) => Scaffold(
          drawer: const MoreDrawer(),
          body: Builder(
            builder: (ctx) => Center(
              child: ElevatedButton(
                onPressed: () => Scaffold.of(ctx).openDrawer(),
                child: const Text('OPEN'),
              ),
            ),
          ),
        ),
      ),
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.aboutForum, '/about', 'ABOUT'),
        (RouteNames.accessibility, '/settings/accessibility', 'A11Y'),
        (RouteNames.terms, '/terms', 'TERMS'),
        (RouteNames.rate, '/rate', 'RATE'),
        (RouteNames.notifications, '/notifications', 'NOTIFS'),
        (RouteNames.shareMyContact, '/contacts/share', 'SHARE'),
        (RouteNames.myContacts, '/contacts', 'CONTACTS'),
        (RouteNames.mediaPartners, '/media-partners', 'PARTNERS'),
        (RouteNames.signIn, '/sign-in', 'SIGN-IN'),
        // D-519 role-specific entries.
        (RouteNames.gateScanner, '/gates/scan', 'GATE'),
        (
          RouteNames.staffRegisterVisitor,
          '/staff/register-visitor',
          'REGISTER'
        ),
        (RouteNames.scanVisitor, '/exhibitor/scan', 'SCAN-VISITOR'),
        (RouteNames.myVisitors, '/exhibitor/visitors', 'MY-VISITORS'),
        (RouteNames.registrationStatus, '/registration/status', 'REG-STATUS'),
        (RouteNames.contactUs, '/contact-us', 'CONTACT-US'),
        (RouteNames.aboutApp, '/about-app', 'ABOUT-APP'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (c, s) => Scaffold(body: Text(label)),
        ),
    ],
  );

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        authControllerProvider.overrideWith(() => auth),
        myAreaRepositoryProvider.overrideWithValue(_FakeMyAreaRepository()),
        localeControllerProvider
            .overrideWith(() => LocaleController(prefs: prefs)),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: const Locale('en'),
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
  await tester.tap(find.text('OPEN'));
  await tester.pumpAndSettle();
}

/// The drawer is a lazily-built ListView; bottom tiles need a scroll to build.
Future<void> _scrollTo(WidgetTester tester, Finder finder) async {
  await tester.scrollUntilVisible(
    finder,
    80,
    scrollable: find.byType(Scrollable).last,
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MoreDrawer (shared shell side menu)', () {
    testWidgets('signed-in shows the nav hub + calendar/logout',
        (tester) async {
      await _pump(tester, auth: _RecordingAuthController(signedIn: true));

      // Nav hub (top of the list, visible without scrolling).
      expect(find.text('About the forum'), findsOneWidget);
      // Account actions moved here from the profile page (D-396) — at the
      // bottom of the scrollable drawer. The language toggle + dark-mode tile
      // were removed 2026-07-08 (owner); language lives on the More screen.
      await _scrollTo(tester, find.text('Sign out'));
      expect(find.text('العربية · English'), findsNothing);
      expect(find.text('Light / dark mode'), findsNothing);
      expect(find.text('Share my calendar'), findsOneWidget);
      // The end group (D-668): contact us + about (app) + logout.
      expect(find.text('Contact us'), findsOneWidget);
      expect(find.text('About the app'), findsOneWidget);
      expect(find.text('Sign out'), findsOneWidget);
    });

    testWidgets('BUG-017 — the drawer is titled "Menu", not a second "More"',
        (tester) async {
      // The side drawer (a flat list of every destination) and the Profile
      // "More" hub (My area / Forum info / Settings / Legal — the only home of
      // the language row) were both labelled "More", so the two different menus
      // were indistinguishable.
      await _pump(tester, auth: _RecordingAuthController(signedIn: true));

      expect(find.text('Menu'), findsOneWidget);
      expect(find.text('More'), findsNothing);
    });

    testWidgets('signed-out hides the calendar + logout, keeps contact + about',
        (tester) async {
      await _pump(tester, auth: _RecordingAuthController(signedIn: false));

      // Public items still present.
      expect(find.text('About the forum'), findsOneWidget);
      // Contact us + About are public — shown even to a not-signed-in guest
      // (owner 2026-07-06, D-668).
      await _scrollTo(tester, find.text('About the app'));
      expect(find.text('العربية · English'), findsNothing);
      expect(find.text('Light / dark mode'), findsNothing);
      expect(find.text('Contact us'), findsOneWidget);
      expect(find.text('About the app'), findsOneWidget);
      // Session-bound actions hidden.
      expect(find.text('Share my calendar'), findsNothing);
      expect(find.text('Sign out'), findsNothing);
    });

    testWidgets('a nav item closes the drawer and pushes its route',
        (tester) async {
      await _pump(tester, auth: _RecordingAuthController(signedIn: true));

      await tester.tap(find.text('About the forum'));
      await tester.pumpAndSettle();
      expect(find.text('ABOUT'), findsOneWidget);
      expect(find.text('About the forum'), findsNothing); // drawer closed
    });

    testWidgets('the external Privacy policy entry confirms instead of routing',
        (tester) async {
      // The one entry with no route: it opens the published web policy, so it
      // must reach the leave-the-app confirmation rather than `pushNamed` —
      // which would throw, there being no such route to match.
      await _pump(tester, auth: _RecordingAuthController(signedIn: true));

      await tester.tap(find.text('Privacy policy'));
      await tester.pumpAndSettle();
      expect(find.byType(Dialog), findsOneWidget);
      expect(find.text('Open'), findsOneWidget);

      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();
      expect(find.byType(Dialog), findsNothing);
    });

    testWidgets('logout → confirm → signOut() and navigates to sign-in',
        (tester) async {
      final auth = _RecordingAuthController(signedIn: true);
      await _pump(tester, auth: auth);

      await _scrollTo(tester, find.text('Sign out'));
      await tester.tap(find.text('Sign out'));
      await tester.pumpAndSettle();
      // Confirm dialog up (shared SimfConfirmDialog renders a Dialog).
      expect(find.byType(Dialog), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Sign out'));
      await tester.pumpAndSettle();

      expect(auth.signOutCalls, 1);
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('logout → cancel aborts (no signOut, no navigation)',
        (tester) async {
      final auth = _RecordingAuthController(signedIn: true);
      await _pump(tester, auth: auth);

      await _scrollTo(tester, find.text('Sign out'));
      await tester.tap(find.text('Sign out'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(auth.signOutCalls, 0);
      expect(find.text('SIGN-IN'), findsNothing);
    });
  });

  // D-519 — the drawer's role-conditional entries (gate scan / register visitor
  // for Staff, scan-visitor / my-visitors for Exhibitor). A tall viewport so the
  // whole list builds and `findsNothing` reliably means "not rendered".
  group('MoreDrawer role filtering (D-519)', () {
    Future<void> pumpRole(WidgetTester tester, AppRole role) async {
      tester.view.physicalSize = const Size(500, 2200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await _pump(
        tester,
        auth: _RecordingAuthController(signedIn: true, role: role),
      );
    }

    testWidgets(
        'Staff sees the gate + register entries, not the exhibitor ones',
        (tester) async {
      await pumpRole(tester, AppRole.staff);
      expect(find.text('Gate scanner'), findsOneWidget);
      expect(find.text('Register a visitor'), findsOneWidget);
      expect(find.text('Scan visitor badge'), findsNothing);
      expect(find.text('My Booth Visitors'), findsNothing);
    });

    testWidgets(
        'Exhibitor sees the scan + my-visitors entries, not the staff ones',
        (tester) async {
      await pumpRole(tester, AppRole.exhibitor);
      expect(find.text('Scan visitor badge'), findsOneWidget);
      expect(find.text('My Booth Visitors'), findsOneWidget);
      expect(find.text('Gate scanner'), findsNothing);
      expect(find.text('Register a visitor'), findsNothing);
    });

    testWidgets('Moderator sees neither the staff nor the exhibitor entries',
        (tester) async {
      await pumpRole(tester, AppRole.moderator);
      expect(find.text('About the forum'), findsOneWidget); // hub still present
      expect(find.text('Gate scanner'), findsNothing);
      expect(find.text('Register a visitor'), findsNothing);
      expect(find.text('Scan visitor badge'), findsNothing);
      expect(find.text('My Booth Visitors'), findsNothing);
    });

    testWidgets('Visitor sees none of the operational entries', (tester) async {
      await pumpRole(tester, AppRole.visitor);
      expect(find.text('Gate scanner'), findsNothing);
      expect(find.text('Register a visitor'), findsNothing);
      expect(find.text('Scan visitor badge'), findsNothing);
      expect(find.text('My Booth Visitors'), findsNothing);
    });
  });

  // D-666 — a signed-in but not-yet-approved account is presented as a guest:
  // the attendee + approved-only + calendar entries hide, and it gets the one
  // extra "Registration status" action a true guest never sees.
  group('MoreDrawer — a not-yet-approved account is treated as guest (D-666)',
      () {
    Future<void> pumpPending(WidgetTester tester) async {
      tester.view.physicalSize = const Size(500, 2200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await _pump(
        tester,
        auth: _RecordingAuthController(
          signedIn: true,
          status: RegistrationStatus.pending,
        ),
      );
    }

    testWidgets('hides the attendee + approved-only + calendar entries',
        (tester) async {
      await pumpPending(tester);
      // Attendee-only rows (rate, contacts) hide — it is an effective guest.
      expect(find.text('Rate'), findsNothing);
      expect(find.text('Share my contact'), findsNothing);
      expect(find.text('My Contacts'), findsNothing);
      // Approved-only row (media partners) hides too.
      expect(find.text('Media partners'), findsNothing);
      // The account action that needs an approved schedule hides.
      expect(find.text('Share my calendar'), findsNothing);
    });

    testWidgets('shows the public hub + Registration-status + contact/about',
        (tester) async {
      await pumpPending(tester);
      expect(find.text('About the forum'), findsOneWidget); // public info stays
      expect(find.text('Registration status'), findsOneWidget); // the exception
      expect(find.text('Contact us'), findsOneWidget); // D-668 end group
      expect(find.text('About the app'), findsOneWidget);
      expect(find.text('Notifications'),
          findsOneWidget,); // signed-in keeps it (D-669)
      expect(find.text('Sign out'), findsOneWidget); // still signed in
    });
  });

  // D-669 — notifications is an auth-only page, so the entry hides for a
  // not-logged-in guest (it would only dead-bounce to sign-in).
  group('MoreDrawer — notifications is signed-in only (D-669)', () {
    Future<void> pumpTall(WidgetTester tester, {required bool signedIn}) async {
      tester.view.physicalSize = const Size(500, 2200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await _pump(tester, auth: _RecordingAuthController(signedIn: signedIn));
    }

    testWidgets('a signed-in account sees Notifications', (tester) async {
      await pumpTall(tester, signedIn: true);
      expect(find.text('Notifications'), findsOneWidget);
    });

    testWidgets('a not-logged-in guest does NOT see Notifications',
        (tester) async {
      await pumpTall(tester, signedIn: false);
      expect(find.text('Notifications'), findsNothing);
      // Public entries still show for the guest.
      expect(find.text('About the forum'), findsOneWidget);
      expect(find.text('Contact us'), findsOneWidget);
    });
  });
}
