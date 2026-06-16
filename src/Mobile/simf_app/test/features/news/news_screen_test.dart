import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/news_screen.dart';

final _items = <NewsListItem>[
  NewsListItem(
    id: 'n1',
    title: 'Forum opens',
    titleArabic: 'افتتاح الملتقى',
    category: 'Press',
    categoryArabic: 'صحافة',
    publishedAt: DateTime.utc(2026, 11, 23),
    excerpt: 'The 4th edition begins.',
  ),
];

/// Pumps the News screen inside a GoRouter, mirroring the shell: KsaPage and
/// SimfBottomNav resolve route names, and the inactive media-coverage tabs need
/// the media-partners + gallery destinations to exist.
Future<void> _pump(WidgetTester tester, List<Override> overrides) async {
  final router = GoRouter(
    initialLocation: '/news',
    routes: <RouteBase>[
      GoRoute(
        path: '/news',
        name: RouteNames.news,
        builder: (_, __) => const NewsScreen(),
      ),
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.myArea, '/my-area', 'MY-AREA'),
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
        (RouteNames.notifications, '/notifications', 'NOTIFS'),
        (RouteNames.mediaPartners, '/media-partners', 'PARTNERS'),
        (RouteNames.gallery, '/gallery', 'GALLERY'),
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
}

void main() {
  group('NewsScreen (Page 029 — KSA frame 958:2246)', () {
    testWidgets('renders the media-coverage tabs and a news card',
        (tester) async {
      await _pump(tester, <Override>[
        newsListProvider.overrideWith((ref) async => _items),
      ]);

      // The three media-coverage tabs.
      expect(find.text('News'), findsWidgets);
      expect(find.text('Media partners'), findsOneWidget);
      expect(find.text('Media gallery'), findsOneWidget);

      // The news content (category chip · title · excerpt).
      expect(find.text('Forum opens'), findsOneWidget);
      expect(find.text('Press'), findsOneWidget);
      expect(find.text('The 4th edition begins.'), findsOneWidget);
    });

    testWidgets('tapping the gallery tab routes to the gallery screen',
        (tester) async {
      await _pump(tester, <Override>[
        newsListProvider.overrideWith((ref) async => _items),
      ]);

      await tester.tap(find.text('Media gallery'));
      await tester.pumpAndSettle();
      expect(find.text('GALLERY'), findsOneWidget);
    });

    testWidgets('empty shows the empty state', (tester) async {
      await _pump(tester, <Override>[
        newsListProvider.overrideWith((ref) async => const <NewsListItem>[]),
      ]);
      expect(find.text('No news'), findsOneWidget);
    });

    testWidgets('error shows the error state with retry', (tester) async {
      await _pump(tester, <Override>[
        newsListProvider.overrideWith((ref) async => throw Exception('x')),
      ]);
      expect(find.text('Could not load the news.'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });
  });

  group('NewsArticle.fromJson', () {
    test('binds title + body', () {
      final a = NewsArticle.fromJson(<String, dynamic>{
        'id': 'n1',
        'title': 'T',
        'titleArabic': 'ع',
        'body': 'Body',
        'bodyArabic': 'نص',
        'category': 'Press',
        'categoryArabic': 'صحافة',
        'publishedAt': '2026-11-23T00:00:00Z',
      });
      expect(a.localizedTitle(false), 'T');
      expect(a.localizedBody(true), 'نص');
    });
  });
}
