@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/sponsors/data/sponsor_models.dart';
import 'package:simf_app/features/sponsors/data/sponsors_repository.dart';
import 'package:simf_app/features/sponsors/sponsors_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the Sponsors screen against Figma frame **922:2824**
/// ("Shepherds"/الرعاة). Compare `goldens/sponsors_922-2824.png` to the frame:
///   flutter test --update-goldens test/golden/sponsors_golden_test.dart
///
/// Frame parity expected: a gold STRATEGIC hero card, a PREMIUM band of navy
/// cards, and a GOLD band as a 3-column logo-tile grid; RTL throughout. The
/// square badges fall back to name-initials here (no network in tests); in
/// production they host the CP-uploaded SponsorLogo asset (D-357), which is why
/// the frame shows English acronyms (SAMI/GAMI/…) baked into the mockup logos.

Sponsor _sponsor({
  required String id,
  required String nameAr,
  required String tierName,
  required int order,
  String? taglineArabic,
}) =>
    Sponsor(
      id: id,
      nameEn: '',
      nameAr: nameAr,
      tierName: tierName,
      displayOrder: order,
      taglineArabic: taglineArabic,
    );

// Three position-based bands matching Figma 922:2824.
final _groups = <SponsorTierGroup>[
  SponsorTierGroup(
    tier: 0,
    tierName: 'الرعاية الاستراتيجية',
    sponsors: <Sponsor>[
      _sponsor(
        id: 's1',
        nameAr: 'الشركة السعودية للصناعات العسكرية',
        tierName: 'الرعاية الاستراتيجية',
        order: 0,
        taglineArabic:
            'الراعي الاستراتيجي · شريك التحول الدفاعي للملتقى البحري السعودي '
                'الدولي.',
      ),
    ],
  ),
  SponsorTierGroup(
    tier: 1,
    tierName: 'رعاة بريميوم',
    sponsors: <Sponsor>[
      _sponsor(
        id: 's2',
        nameAr: 'الهيئة العامة للصناعات العسكرية',
        tierName: 'رعاة بريميوم',
        order: 0,
        taglineArabic: 'تطوير وتعزيز قدرات الصناعة العسكرية في المملكة.',
      ),
      _sponsor(
        id: 's3',
        nameAr: 'القوات البحرية الملكية السعودية',
        tierName: 'رعاة بريميوم',
        order: 1,
        taglineArabic: 'الذراع البحري للقوات المسلحة وحامي السواحل والممرات.',
      ),
      _sponsor(
        id: 's4',
        nameAr: 'الهيئة العامة للتطوير الدفاعي',
        tierName: 'رعاة بريميوم',
        order: 2,
        taglineArabic: 'تمكين المنظومة الدفاعية وتسريع الابتكار في القطاع.',
      ),
    ],
  ),
  SponsorTierGroup(
    tier: 2,
    tierName: 'رعاة ذهبيون',
    sponsors: <Sponsor>[
      for (var i = 0; i < 6; i++)
        _sponsor(
          id: 'g$i',
          nameAr: 'راع ذهبي',
          tierName: 'رعاة ذهبيون',
          order: i,
        ),
    ],
  ),
];

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Sponsors @375x908 — Figma 922:2824 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 908);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/sponsors',
      routes: <RouteBase>[
        GoRoute(
          path: '/sponsors',
          name: RouteNames.sponsors,
          builder: (_, __) => const SponsorsScreen(),
        ),
        GoRoute(
          path: '/sponsors/:sponsorId',
          name: RouteNames.sponsorDetail,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_config),
          sponsorGroupsProvider.overrideWith((ref) async => _groups),
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
      find.byType(SponsorsScreen),
      matchesGoldenFile('goldens/sponsors_922-2824.png'),
    );
  });
}
