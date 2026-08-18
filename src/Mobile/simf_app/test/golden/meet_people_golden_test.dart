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
import 'package:simf_app/features/meet/data/partner_directory_models.dart';
import 'package:simf_app/features/meet/meet_people_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the Meet-people screen — Build #13 partner directory (قابل
/// أشخاص مثلك). Regenerate: flutter test --update-goldens
/// test/golden/meet_people_golden_test.dart
///
/// Parity expected: one `SimfIdentityCell` row per entry — the logo/initials
/// tile at the inline-start, the name (with an optional country flag) over the
/// bilingual subtitle, and a gold caret at the inline-end for the tappable
/// kinds (speaker / sponsor / booth); the opted-in person row has no caret.
/// RTL.

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

const _entries = <PartnerDirectoryEntry>[
  PartnerDirectoryEntry(
    kind: 'speaker',
    id: 's1',
    name: 'Sarah Hill',
    nameArabic: 'سارة الهاشمي',
    subtitle: 'Rear Admiral',
    subtitleArabic: 'لواء بحري',
  ),
  PartnerDirectoryEntry(
    kind: 'sponsor',
    id: 'p1',
    name: 'Acme Marine',
    nameArabic: 'أكمي مارين',
    subtitle: 'Strategic partner',
    subtitleArabic: 'الشريك الاستراتيجي',
  ),
  PartnerDirectoryEntry(
    kind: 'booth',
    id: 'b1',
    name: 'Blue Shipping Co',
    nameArabic: 'شركة الشحن الأزرق',
    subtitleArabic: 'الخدمات اللوجستية',
  ),
  PartnerDirectoryEntry(
    kind: 'person',
    id: 'u1',
    name: 'Omar Nasser',
    nameArabic: 'عمر ناصر',
    subtitleArabic: 'مهندس موانئ',
  ),
];

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Meet people directory @375x900 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/meet',
      routes: <RouteBase>[
        GoRoute(
          path: '/meet',
          name: RouteNames.meetPeople,
          builder: (_, __) => const MeetPeopleScreen(),
        ),
        for (final (name, path) in <(String, String)>[
          (RouteNames.home, '/'),
          (RouteNames.sessions, '/sessions'),
          (RouteNames.badge, '/badge'),
          (RouteNames.venueMap, '/map'),
          (RouteNames.myArea, '/my-area'),
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
          partnerDirectoryProvider.overrideWith((ref) async => _entries),
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
      find.byType(MeetPeopleScreen),
      matchesGoldenFile('goldens/meet_people_directory.png'),
    );
  });
}
