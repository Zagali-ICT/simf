import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/news_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

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

// The card builds the thumbnail URL from the base; every pump supplies one (the
// must-override config provider throws otherwise). The test HTTP client fails
// network-image loads, so the thumbnail's errorBuilder shows the fallback.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

/// Pumps the News screen inside a GoRouter, mirroring the shell: KsaPage and
/// SimfBottomNav resolve route names, and the inactive media-coverage tabs need
/// the media-partners + gallery destinations to exist.
Future<void> _pump(
  WidgetTester tester,
  List<Override> overrides, {
  Locale locale = const Locale('en'),
}) async {
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

      // The frame-948 card: title, the DD-MM-YYYY date, and the category (shown
      // both as the on-image chip and the label above the date).
      expect(find.text('Forum opens'), findsOneWidget);
      expect(find.text('23-11-2026'), findsOneWidget);
      expect(find.text('Press'), findsWidgets);
    });

    testWidgets('each card builds its thumbnail from the NewsImage asset route',
        (tester) async {
      await _pump(tester, <Override>[
        newsListProvider.overrideWith((ref) async => _items),
      ]);

      final urls = tester
          .widgetList<Image>(find.byType(Image))
          .map((image) => image.image)
          .whereType<NetworkImage>()
          .map((provider) => provider.url)
          .toList();
      expect(
        urls,
        contains('http://test.local/api/v1/app/assets/NewsImage/n1/image'),
      );
      // The test HTTP client fails the load, so the thumbnail shows its
      // article-glyph fall-back (the empty state is not rendered with data).
      expect(find.byIcon(Icons.article_outlined), findsOneWidget);
    });

    testWidgets('renders the date left-to-right so RTL keeps DD-MM-YYYY order',
        (tester) async {
      await _pump(
        tester,
        <Override>[newsListProvider.overrideWith((ref) async => _items)],
        locale: const Locale('ar'),
      );
      final date = tester.widget<Text>(find.text('23-11-2026'));
      expect(date.textDirection, TextDirection.ltr);
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

    // D-436 verification rule: confirm the card's RTL layout with a deterministic
    // Arabic-locale position test. Frame 957:2197 places the thumbnail at the
    // inline-end (LEFT in RTL) and the text block at the inline-start (RIGHT),
    // so the thumbnail image sits left of the gold date.
    testWidgets('lays the thumbnail left of the text in Arabic', (tester) async {
      await _pump(
        tester,
        <Override>[newsListProvider.overrideWith((ref) async => _items)],
        locale: const Locale('ar'),
      );

      final thumbnailDx = tester.getCenter(find.byType(Image)).dx;
      final dateDx = tester.getCenter(find.text('23-11-2026')).dx;
      expect(thumbnailDx, lessThan(dateDx));
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
