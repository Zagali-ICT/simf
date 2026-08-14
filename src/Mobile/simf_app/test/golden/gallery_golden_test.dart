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
import 'package:simf_app/features/gallery/gallery_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the Media-gallery screen against Figma frame **947:3764**
/// (المركز الاعلامي — معرض الصور والفيديوهات). Regenerate: flutter test
/// --update-goldens test/golden/gallery_golden_test.dart
///
/// Frame parity expected: the shared media-coverage header, the three-tab
/// selector (the gold-active معرض الصور والفيديوهات right-most, then الشركاء
/// الإعلاميون, then الأخبار), and the two labelled sections — الصور then
/// الفيديوهات — each a two-up grid of rounded tiles with a navy
/// bottom-gradient; the video tile overlays the play glyph. RTL throughout.
///
/// Fixed items with no bitmap (so tiles render the deterministic kind-icon
/// placeholder — no network) → the PNG is stable run-to-run.

const _items = <MediaItem>[
  MediaItem(id: 'm1', kind: MediaKind.image, title: 'الافتتاح', album: 'اليوم ١'),
  MediaItem(id: 'm2', kind: MediaKind.video, title: 'الكلمة الرئيسية'),
];

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Media gallery @375x900 — Figma 947:3764 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/media',
      routes: <RouteBase>[
        GoRoute(
          path: '/media',
          name: RouteNames.gallery,
          builder: (_, __) => const GalleryScreen(),
        ),
        for (final (name, path) in <(String, String)>[
          (RouteNames.home, '/'),
          (RouteNames.news, '/news'),
          (RouteNames.mediaPartners, '/media-partners'),
          (RouteNames.badge, '/badge'),
          (RouteNames.venueMap, '/map'),
          (RouteNames.myArea, '/my-area'),
          (RouteNames.sessions, '/sessions'),
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
          mediaItemsProvider.overrideWith((ref) async => _items),
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
      find.byType(GalleryScreen),
      matchesGoldenFile('goldens/gallery_947-3764.png'),
    );
  });
}
