import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/core/startup/app_version_policy.dart';
import 'package:simf_app/features/more/more_screen.dart';
import 'package:simf_app/features/more/widgets/more_profile_card.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_app/features/myarea/my_area_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Regression guard for the My-Area blank/frozen bug (owner-reported
/// 2026-07-11): My Area IS the Profile bottom-nav tab, so opening it from the
/// More card must **switch to that tab** (go), not **push a second copy** on
/// top. The duplicate instance re-ran its own dashboard load and hung forever.
/// This test drives the real router shape — `/my-area` lives only inside a
/// StatefulShellRoute branch, `/more` is a flat pushed route — and asserts
/// exactly one MyAreaScreen exists after the card tap (two would mean a
/// regression back to `pushNamed`).

const _config = SimfDataConfig(
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

class _FakeAuth extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_session());
}

class _FakeMyAreaRepo implements MyAreaRepository {
  @override
  Future<MyAreaDashboard> getDashboard() async => MyAreaDashboard.fromJson(
        const <String, dynamic>{
          'identity': <String, dynamic>{
            'fullNameAr': 'رائد',
            'fullNameEn': 'Raed',
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
  GoRoute stubTab(String name, String path) => GoRoute(
        name: name,
        path: path,
        builder: (_, __) => Scaffold(body: Text(name)),
      );
  return GoRouter(
    // Start ON the Profile/My-Area tab — the user's real starting point.
    initialLocation: '/my-area',
    routes: <RouteBase>[
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => shell,
        branches: <StatefulShellBranch>[
          StatefulShellBranch(routes: <RouteBase>[stubTab(RouteNames.home, '/')]),
          StatefulShellBranch(
            routes: <RouteBase>[stubTab(RouteNames.sessions, '/sessions')],
          ),
          StatefulShellBranch(
            routes: <RouteBase>[stubTab(RouteNames.badge, '/badge')],
          ),
          StatefulShellBranch(
            routes: <RouteBase>[stubTab(RouteNames.venueMap, '/map')],
          ),
          StatefulShellBranch(
            routes: <RouteBase>[
              GoRoute(
                name: RouteNames.myArea,
                path: '/my-area',
                builder: (_, __) => const MyAreaScreen(),
              ),
            ],
          ),
        ],
      ),
      GoRoute(
        name: RouteNames.more,
        path: '/more',
        builder: (_, __) => const MoreScreen(),
      ),
    ],
  );
}

Future<void> _pump(WidgetTester tester, GoRouter router) async {
  tester.view.physicalSize = const Size(375, 2000);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_config),
        authControllerProvider.overrideWith(_FakeAuth.new),
        myAreaRepositoryProvider.overrideWithValue(_FakeMyAreaRepo()),
        installedAppVersionProvider.overrideWithValue('1.0.0'),
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
  testWidgets(
    'opening My Area from the More card switches to the Profile tab — one '
    'MyAreaScreen, no stacked duplicate (blank/frozen regression)',
    (tester) async {
      final router = _router();
      await _pump(tester, router);

      // On the Profile tab, the My-Area screen is built.
      expect(find.byType(MyAreaScreen), findsOneWidget);

      // Open More on top, then tap its profile card (the "My Area" tap).
      unawaited(router.pushNamed(RouteNames.more));
      await tester.pumpAndSettle();
      expect(find.byType(MoreScreen), findsOneWidget);

      await tester.tap(find.byType(MoreProfileCard));
      await tester.pumpAndSettle();

      // go (not push): More is gone and there is still exactly ONE My-Area
      // instance — the existing tab — never a second, hang-prone copy.
      expect(find.byType(MoreScreen), findsNothing);
      expect(find.byType(MyAreaScreen), findsOneWidget);
    },
  );
}
