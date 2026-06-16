import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/archive/archive_screen.dart';
import 'package:simf_app/features/archive/data/archive_models.dart';

const _editions = <ArchiveEdition>[
  ArchiveEdition(
    id: 'a1',
    year: 2024,
    titleEn: 'Saudi International Maritime Forum 2024',
    titleAr: 'الملتقى البحري السعودي الدولي 2024',
    attendees: 375,
    sessions: 30,
    speakers: 250,
    summaryEn: 'A great year.',
    summaryAr: 'سنة رائعة.',
  ),
  ArchiveEdition(
    id: 'a2',
    year: 2022,
    titleEn: '2022 Edition',
    titleAr: 'نسخة 2022',
    attendees: 200,
    sessions: 18,
    speakers: 120,
  ),
];

final _detail = ArchiveEditionDetail(
  id: 'a1',
  year: 2024,
  titleEn: 'Saudi International Maritime Forum 2024',
  titleAr: 'الملتقى البحري السعودي الدولي 2024',
  attendees: 375,
  sessions: 30,
  speakers: 250,
  locationEn: 'Riyadh',
  locationAr: 'الرياض',
  dateLabelEn: 'November 2024',
  dateLabelAr: 'نوفمبر 2024',
);

Future<void> _pump(
  WidgetTester tester, {
  required List<Override> overrides,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/archive',
    routes: <RouteBase>[
      GoRoute(
        path: '/archive',
        name: RouteNames.archive,
        builder: (_, __) => const ArchiveScreen(),
      ),
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.myArea, '/my-area', 'MY-AREA'),
        (RouteNames.notifications, '/notifications', 'NOTIFS'),
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
      overrides: overrides,
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
  group('ArchiveScreen (Page 024 — KSA frame 925:3079)', () {
    testWidgets('renders the notice, an edition pill per edition, the selected '
        'edition detail and its stats', (tester) async {
      await _pump(
        tester,
        overrides: <Override>[
          archiveEditionsProvider.overrideWith((ref) async => _editions),
          archiveEditionDetailProvider('a1')
              .overrideWith((ref) async => _detail),
        ],
      );

      // Header + notice banner.
      expect(find.text('Archive'), findsWidgets);
      expect(find.text('Choose a forum edition'), findsOneWidget);

      // One selector pill per edition (the year-labelled pill).
      expect(find.textContaining('2024'), findsWidgets);
      expect(find.textContaining('2022'), findsWidgets);

      // The (first) selected edition's title + summary.
      expect(
        find.text('Saudi International Maritime Forum 2024'),
        findsOneWidget,
      );
      expect(find.text('A great year.'), findsOneWidget);

      // The lazily-loaded place/time detail.
      expect(find.text('Riyadh'), findsOneWidget);
      expect(find.text('November 2024'), findsOneWidget);

      // Stat tiles: speakers / attendees / sessions of the selected edition.
      expect(find.text('250'), findsOneWidget);
      expect(find.text('375'), findsOneWidget);
      expect(find.text('30'), findsOneWidget);
      expect(find.text('Speakers'), findsOneWidget);
      expect(find.text('Attendees'), findsOneWidget);
      expect(find.text('Activities'), findsOneWidget);
    });

    testWidgets('tapping another edition pill switches the shown detail',
        (tester) async {
      await _pump(
        tester,
        overrides: <Override>[
          archiveEditionsProvider.overrideWith((ref) async => _editions),
          archiveEditionDetailProvider('a1')
              .overrideWith((ref) async => _detail),
          archiveEditionDetailProvider('a2').overrideWith((ref) async => null),
        ],
      );

      expect(
        find.text('Saudi International Maritime Forum 2024'),
        findsOneWidget,
      );

      await tester.tap(find.textContaining('2022').first);
      await tester.pumpAndSettle();

      // The 2022 edition is now the selected detail.
      expect(find.text('2022 Edition'), findsOneWidget);
      expect(find.text('120'), findsOneWidget); // 2022 speakers
    });

    testWidgets('empty shows the empty state', (tester) async {
      await _pump(
        tester,
        overrides: <Override>[
          archiveEditionsProvider
              .overrideWith((ref) async => const <ArchiveEdition>[]),
        ],
      );
      expect(find.text('No past editions'), findsOneWidget);
    });

    testWidgets('error shows the error state', (tester) async {
      await _pump(
        tester,
        overrides: <Override>[
          archiveEditionsProvider
              .overrideWith((ref) async => throw Exception('x')),
        ],
      );
      expect(find.text('Could not load the archive.'), findsOneWidget);
    });
  });

  group('ArchiveEdition.fromJson', () {
    test('binds the En/Ar wire names + stats', () {
      final e = ArchiveEdition.fromJson(<String, dynamic>{
        'id': 'a1',
        'year': 2025,
        'titleEn': '3rd Edition',
        'titleAr': 'النسخة الثالثة',
        'attendees': 1200,
        'sessions': 40,
        'speakers': 60,
      });
      expect(e.localizedTitle(true), 'النسخة الثالثة');
      expect(e.localizedTitle(false), '3rd Edition');
      expect(e.attendees, 1200);
    });
  });
}
