import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/meet/data/partner_directory_models.dart';
import 'package:simf_app/features/meet/meet_people_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

// Build #13 — the meet screen is now the partner directory. One row per kind.
const _entries = <PartnerDirectoryEntry>[
  PartnerDirectoryEntry(
    kind: 'speaker',
    id: 's1',
    name: 'Sarah Hill',
    nameArabic: 'سارة هل',
    subtitle: 'Rear Admiral',
    subtitleArabic: 'لواء بحري',
  ),
  PartnerDirectoryEntry(
    kind: 'sponsor',
    id: 'p1',
    name: 'Acme Marine',
    nameArabic: 'أكمي مارين',
    subtitle: 'Strategic partner',
    subtitleArabic: 'شريك استراتيجي',
  ),
  PartnerDirectoryEntry(
    kind: 'booth',
    id: 'b1',
    name: 'Blue Shipping Co',
    nameArabic: 'شركة الشحن الأزرق',
  ),
  PartnerDirectoryEntry(
    kind: 'person',
    id: 'u1',
    name: 'Omar Nasser',
    nameArabic: 'عمر ناصر',
    subtitle: 'Port Engineer',
    subtitleArabic: 'مهندس موانئ',
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
      // Per-kind detail stubs so a tap can be asserted by the destination
      // label.
      GoRoute(
        path: '/speakers/:speakerId',
        name: RouteNames.speakerProfile,
        builder: (c, s) => Scaffold(
          body: Text('SPEAKER ${s.pathParameters[RouteParams.speakerId]}'),
        ),
      ),
      GoRoute(
        path: '/sponsors/:sponsorId',
        name: RouteNames.sponsorDetail,
        builder: (c, s) => Scaffold(
          body: Text('SPONSOR ${s.pathParameters[RouteParams.sponsorId]}'),
        ),
      ),
      GoRoute(
        path: '/exhibitors/:boothId',
        name: RouteNames.exhibitorDetail,
        builder: (c, s) => Scaffold(
          body: Text('BOOTH ${s.pathParameters[RouteParams.boothId]}'),
        ),
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
    simfTestScope(
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
  group('MeetPeopleScreen (Page 035 — Build #13 partner directory)', () {
    testWidgets('renders a row per directory entry with its subtitle',
        (tester) async {
      await _pump(tester, <Override>[
        partnerDirectoryProvider.overrideWith((ref) async => _entries),
      ]);
      expect(find.text('Sarah Hill'), findsOneWidget);
      expect(find.text('Rear Admiral'), findsOneWidget);
      expect(find.text('Acme Marine'), findsOneWidget);
      expect(find.text('Blue Shipping Co'), findsOneWidget);
      expect(find.text('Omar Nasser'), findsOneWidget);
      expect(find.text('Port Engineer'), findsOneWidget);
    });

    testWidgets('tapping a speaker opens the speaker profile', (tester) async {
      await _pump(tester, <Override>[
        partnerDirectoryProvider.overrideWith((ref) async => _entries),
      ]);
      await tester.tap(find.text('Sarah Hill'));
      await tester.pumpAndSettle();
      expect(find.text('SPEAKER s1'), findsOneWidget);
    });

    testWidgets('tapping a sponsor opens the sponsor detail', (tester) async {
      await _pump(tester, <Override>[
        partnerDirectoryProvider.overrideWith((ref) async => _entries),
      ]);
      await tester.tap(find.text('Acme Marine'));
      await tester.pumpAndSettle();
      expect(find.text('SPONSOR p1'), findsOneWidget);
    });

    testWidgets('tapping a booth company opens the exhibitor detail',
        (tester) async {
      await _pump(tester, <Override>[
        partnerDirectoryProvider.overrideWith((ref) async => _entries),
      ]);
      await tester.tap(find.text('Blue Shipping Co'));
      await tester.pumpAndSettle();
      expect(find.text('BOOTH b1'), findsOneWidget);
    });

    testWidgets('tapping an opted-in person does not navigate', (tester) async {
      await _pump(tester, <Override>[
        partnerDirectoryProvider.overrideWith((ref) async => _entries),
      ]);
      await tester.tap(find.text('Omar Nasser'));
      await tester.pumpAndSettle();
      // Still on the directory — the person row is non-navigating.
      expect(find.text('Omar Nasser'), findsOneWidget);
      expect(find.textContaining('SPEAKER'), findsNothing);
    });

    testWidgets('empty list shows the empty notice', (tester) async {
      await _pump(tester, <Override>[
        partnerDirectoryProvider
            .overrideWith((ref) async => const <PartnerDirectoryEntry>[]),
      ]);
      expect(find.text('No one to show yet'), findsOneWidget);
    });

    testWidgets('error shows the error state', (tester) async {
      await _pump(tester, <Override>[
        partnerDirectoryProvider
            .overrideWith((ref) async => throw Exception('x')),
      ]);
      expect(find.text('Could not load the directory.'), findsOneWidget);
    });
  });
}
