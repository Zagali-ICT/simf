import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/meet/data/meet_models.dart';
import 'package:simf_app/features/meet/meet_people_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

const _matches = <Recommendation>[
  Recommendation(
    userProfileId: 'u1',
    englishName: 'Sarah Hill',
    arabicName: 'سارة هل',
    jobTitle: 'Naval Architect',
    profileTypeName: 'Captain',
    profileTypeNameArabic: 'القبطان',
    sharedInterests: <MatchedInterest>[
      MatchedInterest(id: 'i1', name: 'Shipbuilding', nameArabic: 'بناء السفن'),
    ],
    sharedInterestCount: 3,
    score: 0.82,
  ),
];

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

Future<void> _pump(
  WidgetTester tester,
  List<Override> overrides, {
  Locale locale = const Locale('en'),
}) async {
  tester.view.physicalSize = const Size(375, 1400);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  final router = GoRouter(
    initialLocation: '/meet',
    routes: <RouteBase>[
      GoRoute(
        path: '/meet',
        name: RouteNames.meetPeople,
        builder: (_, __) => const MeetPeopleScreen(),
      ),
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.myArea, '/my-area', 'MY-AREA'),
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
        simfDataConfigProvider.overrideWithValue(_testConfig),
        ...overrides,
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
  group('MeetPeopleScreen (Page 035 — KSA frame 1072:13409)', () {
    testWidgets('renders the smart header and a match card with the % score',
        (tester) async {
      await _pump(tester, <Override>[
        meetRecommendationsProvider.overrideWith((ref) async => _matches),
      ]);
      // Header card.
      expect(
        find.text('Smart suggestions based on your interests'),
        findsOneWidget,
      );
      expect(find.text('Artificial intelligence'), findsOneWidget);
      // Match card: name, profile-type line, reason, and the % from score 0.82.
      expect(find.text('Sarah Hill'), findsOneWidget);
      expect(find.text('Captain'), findsOneWidget);
      expect(find.text('3 shared interests'), findsOneWidget);
      expect(find.text('82%'), findsOneWidget);
      expect(find.text('match'), findsOneWidget);
    });

    testWidgets('empty list keeps the header and shows the empty notice',
        (tester) async {
      await _pump(tester, <Override>[
        meetRecommendationsProvider
            .overrideWith((ref) async => const <Recommendation>[]),
      ]);
      expect(
        find.text('Smart suggestions based on your interests'),
        findsOneWidget,
      );
      expect(find.text('No matches yet'), findsOneWidget);
    });

    testWidgets('error shows the error state', (tester) async {
      await _pump(tester, <Override>[
        meetRecommendationsProvider
            .overrideWith((ref) async => throw Exception('x')),
      ]);
      expect(find.text('Could not load your matches.'), findsOneWidget);
    });

    testWidgets('Arabic: the % block sits to the left of the name',
        (tester) async {
      await _pump(
        tester,
        <Override>[
          meetRecommendationsProvider.overrideWith((ref) async => _matches),
        ],
        locale: const Locale('ar'),
      );
      final percentX = tester.getCenter(find.text('82%')).dx;
      final nameX = tester.getCenter(find.text('سارة هل')).dx;
      expect(percentX, lessThan(nameX));
    });
  });
}
