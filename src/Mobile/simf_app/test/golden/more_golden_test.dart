@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/more/more_screen.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the More screen against Figma frame **1129:17224** (المزيد).
/// Regenerate:
///   flutter test --update-goldens test/golden/more_golden_test.dart
///
/// Frame parity expected (signed-in): the منطقتي profile header card, the three
/// grouped sections (معلومات الملتقى / الإعدادات / قانوني) of bordered nav rows
/// (the اللغة row shows the current language), the تسجيل الخروج link and the
/// version line. RTL.

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
        displayName: 'رائد السالم',
        appRole: AppRole.visitor,
        preferredLanguage: PreferredLanguage.fromJson('ar'),
        registrationStatus: RegistrationStatus.approved,
      ),
    );

class _SignedInAuth extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_session());
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

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('More @375x1000 — Figma 1129:17224 (Arabic, signed-in)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 1000);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/more',
      routes: <RouteBase>[
        GoRoute(path: '/more', builder: (_, __) => const MoreScreen()),
        for (final (name, path) in <(String, String)>[
          (RouteNames.home, '/'),
          (RouteNames.sessions, '/sessions'),
          (RouteNames.badge, '/badge'),
          (RouteNames.venueMap, '/map'),
          (RouteNames.myArea, '/my-area'),
          (RouteNames.notifications, '/notifications'),
        ])
          GoRoute(
            name: name,
            path: path,
            builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
          ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_testConfig),
          authControllerProvider.overrideWith(_SignedInAuth.new),
          myAreaRepositoryProvider.overrideWithValue(_FakeMyAreaRepository()),
        ],
        child: MaterialApp.router(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          routerConfig: router,
          locale: const Locale('ar'),
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

    await expectLater(
      find.byType(MoreScreen),
      matchesGoldenFile('goldens/more_1129-17224.png'),
    );
  });
}
