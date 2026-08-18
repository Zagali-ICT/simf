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
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/data/news_repository.dart';
import 'package:simf_app/features/news/news_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the News screen against Figma frame **1049:12629** (المركز
/// الاعلامي — media coverage). Regenerate:
///   flutter test --update-goldens test/golden/news_golden_test.dart
///
/// Frame parity expected: the shared media-coverage tab strip (احدث المستجدات
/// active gold left / الشركاء الإعلاميون inactive right) over the news list —
/// each row a navy card with the text block at the inline-start (category, gold
/// date, bold title) and the 155×85 thumbnail (gold category chip + navy
/// gradient) at the inline-end. RTL. The test HTTP client fails the network
/// image, so tiles show the navy article-icon fallback (deterministic).

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

final _items = <NewsListItem>[
  NewsListItem(
    id: 'n1',
    title: 'Forum opens',
    titleArabic: 'انطلاق الملتقى البحري السعودي الدولي الرابع',
    category: 'Press',
    categoryArabic: 'صحافة',
    publishedAt: DateTime.utc(2026, 11, 23),
    excerpt: '',
  ),
  NewsListItem(
    id: 'n2',
    title: 'Delegations arrive',
    titleArabic: 'وصول وفود الدول المشاركة',
    category: 'Coverage',
    categoryArabic: 'تغطية',
    publishedAt: DateTime.utc(2026, 11, 24),
    excerpt: '',
  ),
];

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('News @375x750 — Figma 1049:12629 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 750);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/news',
      routes: <RouteBase>[
        GoRoute(
          path: '/news',
          name: RouteNames.news,
          builder: (_, __) => const NewsScreen(),
        ),
        for (final (name, path) in <(String, String)>[
          (RouteNames.home, '/'),
          (RouteNames.badge, '/badge'),
          (RouteNames.venueMap, '/map'),
          (RouteNames.myArea, '/my-area'),
          (RouteNames.sessions, '/sessions'),
          (RouteNames.notifications, '/notifications'),
          (RouteNames.mediaPartners, '/media-partners'),
          (RouteNames.gallery, '/gallery'),
        ])
          GoRoute(
            name: name,
            path: path,
            builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
          ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_testConfig),
          newsListProvider.overrideWith((ref) async => _items),
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
      find.byType(NewsScreen),
      matchesGoldenFile('goldens/news_1049-12629.png'),
    );
  });
}
