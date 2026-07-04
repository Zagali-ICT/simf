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
import 'package:simf_app/features/media_partners/media_partners_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the Media-partners screen against Figma frame **958:2246**
/// (المركز الاعلامي — media coverage, partners tab). Regenerate:
///   flutter test --update-goldens test/golden/media_partners_golden_test.dart
///
/// Frame parity expected: the shared media-coverage tab strip (الشركاء
/// الإعلاميون active gold right / احدث المستجدات inactive left) over a two-column
/// grid of partner cards — each a navy card with a gold rounded-square logo
/// holder over the centred partner name. RTL. The test HTTP client fails the
/// logo image, so cards show the initials-on-gold fallback (deterministic).

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

const _partners = <MediaPartner>[
  MediaPartner(id: 'p1', name: 'SPA', nameArabic: 'واس'),
  MediaPartner(id: 'p2', name: 'Al Arabiya', nameArabic: 'العربية'),
  MediaPartner(id: 'p3', name: 'SBA', nameArabic: 'الهيئة السعودية'),
  MediaPartner(id: 'p4', name: 'Asharq', nameArabic: 'الشرق'),
];

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Media partners @375x750 — Figma 958:2246 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 750);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/media-partners',
      routes: <RouteBase>[
        GoRoute(
          path: '/media-partners',
          name: RouteNames.mediaPartners,
          builder: (_, __) => const MediaPartnersScreen(),
        ),
        for (final (name, path) in <(String, String)>[
          (RouteNames.home, '/'),
          (RouteNames.badge, '/badge'),
          (RouteNames.venueMap, '/map'),
          (RouteNames.myArea, '/my-area'),
          (RouteNames.sessions, '/sessions'),
          (RouteNames.notifications, '/notifications'),
          (RouteNames.news, '/news'),
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
      ProviderScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_testConfig),
          mediaPartnersProvider.overrideWith((ref) async => _partners),
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
      find.byType(MediaPartnersScreen),
      matchesGoldenFile('goldens/media_partners_958-2246.png'),
    );
  });
}
