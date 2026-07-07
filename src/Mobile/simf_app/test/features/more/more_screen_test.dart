import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/more/more_screen.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: CurrentUser(
        id: 'u1',
        email: 'v@example.sa',
        displayName: 'Raed',
        appRole: AppRole.visitor,
        preferredLanguage: PreferredLanguage.fromJson('en'),
        registrationStatus: RegistrationStatus.approved,
      ),
    );

class _FakeAuthController extends AuthController {
  _FakeAuthController({required this.signedIn});

  final bool signedIn;

  @override
  AuthState build() =>
      signedIn ? AuthStateSignedIn(_session()) : const AuthStateSignedOut();
}

class _FakeMyAreaRepository implements MyAreaRepository {
  @override
  Future<MyAreaDashboard> getDashboard() async => MyAreaDashboard.fromJson(
        const <String, dynamic>{
          'identity': <String, dynamic>{
            'fullNameAr': 'رائد السالم',
            'fullNameEn': 'Raed Al-Salem',
            'tierNameEn': 'VIP',
            'tierNameAr': 'VIP',
          },
          'counters': <String, dynamic>{},
          'todaySchedule': <dynamic>[],
        },
      );
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

GoRouter _router() {
  return GoRouter(
    initialLocation: '/more',
    routes: <RouteBase>[
      GoRoute(path: '/more', builder: (context, state) => const MoreScreen()),
      GoRoute(
        name: RouteNames.aboutForum,
        path: '/about',
        builder: (context, state) => const Scaffold(
          body: Center(child: Text('ABOUT-MARKER')),
        ),
      ),
      GoRoute(
        name: RouteNames.forgotPassword,
        path: '/forgot',
        builder: (context, state) => const Scaffold(
          body: Center(child: Text('FORGOT-MARKER')),
        ),
      ),
      // The shared shell's bottom-nav destinations + the my-area target.
      for (final (name, path) in <(String, String)>[
        (RouteNames.home, '/'),
        (RouteNames.sessions, '/sessions'),
        (RouteNames.badge, '/badge'),
        (RouteNames.venueMap, '/map'),
        (RouteNames.myArea, '/my-area'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (c, s) => Scaffold(body: Text(name)),
        ),
    ],
  );
}

Future<void> _pump(
  WidgetTester tester, {
  required GoRouter router,
  bool signedIn = false,
}) async {
  // A tall surface so the whole grouped list lays out under the lazy ListView.
  tester.view.physicalSize = const Size(375, 2000);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        authControllerProvider
            .overrideWith(() => _FakeAuthController(signedIn: signedIn)),
        myAreaRepositoryProvider.overrideWithValue(_FakeMyAreaRepository()),
      ],
      child: MaterialApp.router(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        routerConfig: router,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MoreScreen (Page 041 — frame 1129:17224)', () {
    testWidgets('renders the three grouped sections and their rows',
        (tester) async {
      // Signed-in Visitor sees every row — including the attendee-only Rate
      // (D-519 role-filters it off the guest view). D-609 removed the
      // "Session presentations" / my-sessions row entirely.
      await _pump(tester, router: _router(), signedIn: true);
      // Section headers.
      expect(find.text('Forum information'), findsOneWidget);
      expect(find.text('Settings'), findsOneWidget);
      expect(find.text('Legal'), findsOneWidget);
      // معلومات الملتقى rows.
      expect(find.text('About the forum'), findsOneWidget);
      expect(find.text('Forum guide'), findsOneWidget);
      expect(find.text('FAQ'), findsOneWidget);
      expect(find.text('Explore Riyadh · VisitSaudi'), findsOneWidget);
      // الإعدادات rows (Language shows the current value).
      expect(find.text('Language'), findsOneWidget);
      expect(find.text('English'), findsOneWidget);
      expect(find.text('Accessibility'), findsOneWidget);
      expect(find.text('Notifications'), findsOneWidget);
      // Reset password (signed-in only, D-658) — a deliberate profile action
      // beyond frame 1129:17224.
      expect(find.text('Reset password'), findsOneWidget);
      // قانوني rows.
      expect(find.text('Terms & conditions'), findsOneWidget);
      expect(find.text('Contact us'), findsOneWidget);
      expect(find.text('Rate the app'), findsOneWidget);
    });

    testWidgets('shows the app-version line', (tester) async {
      await _pump(tester, router: _router());
      expect(find.text('SIMF 2026 · v1.0.0'), findsOneWidget);
    });

    testWidgets('guest hides the profile card and sign-out', (tester) async {
      await _pump(tester, router: _router());
      expect(find.text('My area'), findsNothing);
      expect(find.text('Sign out'), findsNothing);
      // D-519 — the attendee-only Rate row is role-filtered off the guest view
      // (a guest cannot reach it; tapping would bounce to sign-in).
      expect(find.text('Rate the app'), findsNothing);
      // D-669 — notifications are auth-only, hidden from the guest view too.
      expect(find.text('Notifications'), findsNothing);
      // The public rows remain.
      expect(find.text('About the forum'), findsOneWidget);
      expect(find.text('FAQ'), findsOneWidget);
      // Reset password is an account action — hidden from the guest view.
      expect(find.text('Reset password'), findsNothing);
    });

    testWidgets('signed-in shows the منطقتي profile card + sign-out',
        (tester) async {
      await _pump(tester, router: _router(), signedIn: true);
      expect(find.text('My area'), findsOneWidget);
      expect(find.textContaining('Raed Al-Salem'), findsOneWidget);
      expect(find.text('Sign out'), findsOneWidget);
    });

    testWidgets('tapping About navigates to the About route', (tester) async {
      await _pump(tester, router: _router());
      await tester.tap(find.text('About the forum'));
      await tester.pumpAndSettle();
      expect(find.text('ABOUT-MARKER'), findsOneWidget);
    });

    testWidgets('signed-in tapping Reset password opens the forgot flow',
        (tester) async {
      await _pump(tester, router: _router(), signedIn: true);
      await tester.tap(find.text('Reset password'));
      await tester.pumpAndSettle();
      expect(find.text('FORGOT-MARKER'), findsOneWidget);
    });
  });
}
