import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
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

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: CurrentUser(
        id: 'u1',
        email: 'v@example.sa',
        displayName: 'Raed',
        appRole: AppRole.visitor,
        preferredLanguage: PreferredLanguage.fromJson('ar'),
        registrationStatus: RegistrationStatus.approved,
      ),
    );

/// Records sign-out so the logout flow can be asserted without a real revoke.
class _RecordingAuthController extends AuthController {
  _RecordingAuthController({required this.signedIn});

  final bool signedIn;
  int signOutCalls = 0;

  @override
  AuthState build() =>
      signedIn ? AuthStateSignedIn(_session()) : const AuthStateSignedOut();

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
  Future<String> getCalendarIcs() async => 'BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n';
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
        authControllerProvider.overrideWith(() => auth),
        myAreaRepositoryProvider.overrideWithValue(_FakeMyAreaRepository()),
        localeControllerProvider.overrideWith(() => LocaleController(prefs: prefs)),
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
    testWidgets('signed-in shows the nav hub + language/theme/calendar/logout',
        (tester) async {
      await _pump(tester, auth: _RecordingAuthController(signedIn: true));

      // Nav hub (top of the list, visible without scrolling).
      expect(find.text('About the forum'), findsOneWidget);
      // Account actions moved here from the profile page (D-396) — at the
      // bottom of the scrollable drawer.
      await _scrollTo(tester, find.text('Sign out'));
      expect(find.text('العربية · English'), findsOneWidget);
      expect(find.text('Light / dark mode'), findsOneWidget);
      expect(find.text('Share my calendar'), findsOneWidget);
      expect(find.text('Sign out'), findsOneWidget);
    });

    testWidgets('signed-out hides the calendar + logout (session-bound) actions',
        (tester) async {
      await _pump(tester, auth: _RecordingAuthController(signedIn: false));

      // Public items still present.
      expect(find.text('About the forum'), findsOneWidget);
      // The last public action when signed out is the (inert) theme tile.
      await _scrollTo(tester, find.text('Light / dark mode'));
      expect(find.text('العربية · English'), findsOneWidget);
      expect(find.text('Light / dark mode'), findsOneWidget);
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

    testWidgets('logout → confirm → signOut() and navigates to sign-in',
        (tester) async {
      final auth = _RecordingAuthController(signedIn: true);
      await _pump(tester, auth: auth);

      await _scrollTo(tester, find.text('Sign out'));
      await tester.tap(find.text('Sign out'));
      await tester.pumpAndSettle();
      // Confirm dialog up.
      expect(find.byType(AlertDialog), findsOneWidget);

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
}
