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
import 'package:simf_app/features/meet/data/meet_models.dart';
import 'package:simf_app/features/meet/meet_people_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the Meet-people screen against Figma frame **1072:13409**
/// (قابل أشخاص مثلك). Regenerate:
///   flutter test --update-goldens test/golden/meet_people_golden_test.dart
///
/// Frame parity expected: the smart-suggestions header card (title + subtitle +
/// three topic chips) over per-match cards — the gold initials avatar at the
/// inline-start, the name / profile-type / match-reason column, and the gold
/// "% تطابق" block at the inline-end. RTL.

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

const _matches = <Recommendation>[
  Recommendation(
    userProfileId: 'u1',
    englishName: 'Sarah Hill',
    arabicName: 'سارة الهاشمي',
    jobTitle: 'Naval Architect',
    profileTypeName: 'Captain',
    profileTypeNameArabic: 'قبطان',
    sharedInterests: <MatchedInterest>[
      MatchedInterest(id: 'i1', name: 'Shipbuilding', nameArabic: 'بناء السفن'),
    ],
    sharedInterestCount: 3,
    score: 0.82,
  ),
  Recommendation(
    userProfileId: 'u2',
    englishName: 'Omar Nasser',
    arabicName: 'عمر ناصر',
    jobTitle: 'Port Engineer',
    profileTypeName: 'Exhibitor',
    profileTypeNameArabic: 'عارض',
    sharedInterests: <MatchedInterest>[
      MatchedInterest(id: 'i2', name: 'Logistics', nameArabic: 'الخدمات اللوجستية'),
    ],
    sharedInterestCount: 2,
    score: 0.67,
  ),
];

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Meet people @375x900 — Figma 1072:13409 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/meet',
      routes: <RouteBase>[
        GoRoute(
          path: '/meet',
          name: RouteNames.meetPeople,
          builder: (_, __) => const MeetPeopleScreen(),
        ),
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
            builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
          ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_testConfig),
          meetRecommendationsProvider.overrideWith((ref) async => _matches),
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
      find.byType(MeetPeopleScreen),
      matchesGoldenFile('goldens/meet_people_1072-13409.png'),
    );
  });
}
