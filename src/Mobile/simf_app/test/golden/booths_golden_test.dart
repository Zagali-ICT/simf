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
import 'package:simf_app/features/booths/booths_screen.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/data/venue_map_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the Booths/Exhibition screen against Figma frame
/// **922:2458** ("Halls"/المعرض). Compare `goldens/booths_922-2458.png` to the
/// frame: flutter test --update-goldens test/golden/booths_golden_test.dart
///
/// Frame parity expected: centred header (المعرض), a bordered search field,
/// then one exhibitor card per booth — company header (the 48×48 logo tile at
/// the inline-start/right, the gold short name over the beige full name in the
/// middle, the country **flag tile** at the inline-end/left), a gold code pill
/// (A-12) beside the deep-navy hall box, and a full-width gold guide-me CTA;
/// RTL throughout. The logo tile falls back to the short name here (no network
/// in tests); in production it hosts the booth's own CP-uploaded BoothLogo
/// (D-357).

// Three identical SAMI booths matching the frame. countryId drives the
// inline-end flag tile; countryName is left null so no extra country text line
// renders (the frame shows only the gold short name over the full Arabic name).
const _booths = <BoothSummary>[
  BoothSummary(
    id: 'b1',
    code: 'A-12',
    name: 'SAMI',
    nameArabic: 'SAMI',
    exhibitorName: 'Saudi Arabian Military Industries',
    exhibitorNameArabic: 'الشركة السعودية للصناعات العسكرية',
    hallName: 'Hall A',
    hallNameArabic: 'القاعة الرئيسية',
    exhibitorContactId: 'c1',
    countryId: 682,
  ),
  BoothSummary(
    id: 'b2',
    code: 'A-12',
    name: 'SAMI',
    nameArabic: 'SAMI',
    exhibitorName: 'Saudi Arabian Military Industries',
    exhibitorNameArabic: 'الشركة السعودية للصناعات العسكرية',
    hallName: 'Hall A',
    hallNameArabic: 'القاعة الرئيسية',
    exhibitorContactId: 'c2',
    countryId: 682,
  ),
  BoothSummary(
    id: 'b3',
    code: 'A-12',
    name: 'SAMI',
    nameArabic: 'SAMI',
    exhibitorName: 'Saudi Arabian Military Industries',
    exhibitorNameArabic: 'الشركة السعودية للصناعات العسكرية',
    hallName: 'Hall A',
    hallNameArabic: 'القاعة الرئيسية',
    exhibitorContactId: 'c3',
    countryId: 682,
  ),
];

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

class _FakeVenueRepo implements VenueMapRepository {
  @override
  Future<List<BoothSummary>> getBooths() async => _booths;
  @override
  Future<BoothDetail> getBoothDetail(String id) => throw UnimplementedError();
  @override
  Future<List<VenueMapNode>> getNodes() => throw UnimplementedError();
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Booths @375x1031 — Figma 922:2458 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 1031);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/booths',
      routes: <RouteBase>[
        GoRoute(
          path: '/booths',
          name: RouteNames.booths,
          builder: (_, __) => const BoothsScreen(),
        ),
        GoRoute(
          path: '/booths/:boothId',
          name: RouteNames.exhibitorDetail,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/booths/:boothId/map',
          name: RouteNames.boothMap,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_config),
          venueMapRepositoryProvider.overrideWithValue(_FakeVenueRepo()),
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
      find.byType(BoothsScreen),
      matchesGoldenFile('goldens/booths_922-2458.png'),
    );
  });
}
