import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/gallery/gallery_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

const _items = <MediaItem>[
  MediaItem(
    id: 'm1',
    kind: MediaKind.image,
    title: 'Opening',
    album: 'Day 1',
  ),
  MediaItem(id: 'm2', kind: MediaKind.video, title: 'Keynote'),
];

// The screen reads the base URL to build tile image URLs; every pump supplies
// one (the must-override config provider throws otherwise). The test items
// carry no bitmap, so tiles render the kind-icon placeholder — no network.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

Future<void> _pump(
  WidgetTester tester, {
  required List<MediaItem> Function() items,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/media',
    routes: <RouteBase>[
      GoRoute(
        path: '/media',
        name: RouteNames.gallery,
        builder: (_, __) => const GalleryScreen(),
      ),
      // The shell back-target + the two sibling coverage tabs the tab bar
      // navigates to. Plus the bottom-nav destinations SimfPageShell renders.
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.news, '/news', 'NEWS'),
        (RouteNames.mediaPartners, '/media-partners', 'PARTNERS'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.myArea, '/my-area', 'MY-AREA'),
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
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
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        mediaItemsProvider.overrideWith((ref) async => items()),
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
  group('GalleryScreen (Page 030 — KSA frame 947:3764)', () {
    testWidgets('renders the coverage header, the three tabs and the two '
        'media sections', (tester) async {
      await _pump(tester, items: () => _items);

      // The media-center header (shared hub title — renamed in Figma 947/1049).
      expect(find.text('Media center'), findsWidgets);
      // The three coverage tabs.
      expect(find.text('News'), findsOneWidget);
      expect(find.text('Media partners'), findsOneWidget);
      expect(find.text('Media gallery'), findsWidgets);
      // The two labelled sections.
      expect(find.text('Images'), findsOneWidget);
      expect(find.text('Videos'), findsOneWidget);
      // A tile per item, with its title overlaid.
      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('Keynote'), findsOneWidget);
      // The video tile shows a play glyph.
      expect(find.byIcon(Icons.play_arrow_rounded), findsOneWidget);
    });

    testWidgets('only the images section shows when there are no videos',
        (tester) async {
      await _pump(
        tester,
        items: () => const <MediaItem>[
          MediaItem(id: 'm1', kind: MediaKind.image, title: 'Opening'),
        ],
      );
      expect(find.text('Images'), findsOneWidget);
      expect(find.text('Videos'), findsNothing);
      expect(find.byIcon(Icons.play_arrow_rounded), findsNothing);
    });

    testWidgets('tapping the News tab navigates to the news route',
        (tester) async {
      await _pump(tester, items: () => _items);

      await tester.tap(find.text('News'));
      await tester.pumpAndSettle();
      expect(find.text('NEWS'), findsOneWidget);
    });

    testWidgets('tapping the Media-partners tab navigates to its route',
        (tester) async {
      await _pump(tester, items: () => _items);

      await tester.tap(find.text('Media partners'));
      await tester.pumpAndSettle();
      expect(find.text('PARTNERS'), findsOneWidget);
    });

    testWidgets('empty media shows the empty state', (tester) async {
      await _pump(tester, items: () => const <MediaItem>[]);
      expect(find.text('No media yet'), findsOneWidget);
    });

    testWidgets('a read failure shows the error + retry', (tester) async {
      await _pump(
        tester,
        items: () => throw Exception('x'),
      );
      expect(find.text('Could not load the media.'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Retry'), findsOneWidget);
    });

    testWidgets('renders right-to-left in Arabic', (tester) async {
      await _pump(tester, items: () => _items, locale: const Locale('ar'));

      expect(find.text('المركز الاعلامي'), findsWidgets);
      expect(
        Directionality.of(tester.element(find.text('الأخبار'))),
        TextDirection.rtl,
      );
    });

    test('MediaKind.fromJson decodes int / name', () {
      expect(MediaKind.fromJson(1), MediaKind.video);
      expect(MediaKind.fromJson(0), MediaKind.image);
      expect(MediaKind.fromJson('Video'), MediaKind.video);
      expect(MediaKind.fromJson(null), MediaKind.image);
    });

    test('MediaItem.fromJson reads image/thumbnail presence', () {
      final withBoth = MediaItem.fromJson(const <String, dynamic>{
        'id': 'm1',
        'kind': 0,
        'imageUrl': '/media/m1/image',
        'thumbnailUrl': '/media/m1/thumbnail',
      });
      expect(withBoth.hasImage, isTrue);
      expect(withBoth.hasThumbnail, isTrue);

      final none = MediaItem.fromJson(const <String, dynamic>{'id': 'm2', 'kind': 1});
      expect(none.hasImage, isFalse);
      expect(none.hasThumbnail, isFalse);
    });
  });
}
