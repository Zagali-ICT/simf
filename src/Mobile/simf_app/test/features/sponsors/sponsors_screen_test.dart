import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sponsors/data/sponsor_models.dart';
import 'package:simf_app/features/sponsors/sponsors_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// The HTTP client fails network-image loads in tests, so each sponsor logo's
// errorBuilder falls back to its initials — no real bytes are needed.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

/// Every NetworkImage URL currently in the tree — lets a test assert the logo
/// is wired to the D-357 SponsorLogo route.
Set<String> _networkImageUrls(WidgetTester tester) => tester
    .widgetList<Image>(find.byType(Image))
    .map((img) => img.image)
    .whereType<NetworkImage>()
    .map((n) => n.url)
    .toSet();

const _groups = <SponsorTierGroup>[
  SponsorTierGroup(
    tier: 1,
    tierName: 'Strategic',
    sponsors: <Sponsor>[
      Sponsor(
        id: 's1',
        nameEn: 'SAMI',
        nameAr: 'سامي',
        tierName: 'Strategic',
        displayOrder: 0,
        url: 'https://sami.sa',
      ),
    ],
  ),
  SponsorTierGroup(
    tier: 2,
    tierName: 'Premium',
    sponsors: <Sponsor>[
      Sponsor(
        id: 's2',
        nameEn: 'GAMI Authority',
        nameAr: 'الهيئة العامة للصناعات العسكرية',
        tierName: 'Premium',
        displayOrder: 0,
      ),
    ],
  ),
];

Future<void> _pump(
  WidgetTester tester,
  List<Override> overrides, {
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        ...overrides,
      ],
      child: MaterialApp(
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const SponsorsScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SponsorsScreen (Page 023)', () {
    testWidgets('renders tier headers + sponsor cards (strategic + premium)',
        (tester) async {
      await _pump(tester, <Override>[
        sponsorGroupsProvider.overrideWith((ref) async => _groups),
      ]);
      // Both tier section labels render, in order.
      expect(find.text('Strategic'), findsOneWidget);
      expect(find.text('Premium'), findsOneWidget);
      // Each tier's sponsor name + the strategic url line render.
      expect(find.text('SAMI'), findsOneWidget);
      expect(find.text('GAMI Authority'), findsOneWidget);
      expect(find.text('https://sami.sa'), findsOneWidget);
    });

    testWidgets('P6 — the lowest tier renders as a logo grid, the top as a hero '
        'card', (tester) async {
      // Three tiers: top → hero card, middle → premium cards, lowest → the grid.
      const threeTiers = <SponsorTierGroup>[
        SponsorTierGroup(
          tier: 1,
          tierName: 'Strategic',
          sponsors: <Sponsor>[
            Sponsor(
              id: 's1', nameEn: 'SAMI', nameAr: 'سامي',
              tierName: 'Strategic', displayOrder: 0,
            ),
          ],
        ),
        SponsorTierGroup(
          tier: 2,
          tierName: 'Premium',
          sponsors: <Sponsor>[
            Sponsor(
              id: 's2', nameEn: 'GAMI', nameAr: 'غامي',
              tierName: 'Premium', displayOrder: 0,
            ),
          ],
        ),
        SponsorTierGroup(
          tier: 3,
          tierName: 'Gold',
          sponsors: <Sponsor>[
            Sponsor(
              id: 's3', nameEn: 'Bronze Co', nameAr: 'شركة',
              tierName: 'Gold', displayOrder: 0,
            ),
          ],
        ),
      ];
      await _pump(tester, <Override>[
        sponsorGroupsProvider.overrideWith((ref) async => threeTiers),
      ]);
      // The lowest tier is the compact grid.
      expect(find.byType(GridView), findsOneWidget);
      // Every tier's name still renders (hero + cards + grid tile).
      expect(find.text('SAMI'), findsOneWidget);
      expect(find.text('GAMI'), findsOneWidget);
      expect(find.text('Bronze Co'), findsOneWidget);
      // The grid tile's logo is wired to the SponsorLogo route too.
      expect(
        _networkImageUrls(tester),
        contains('http://test.local/api/v1/app/assets/SponsorLogo/s3/image'),
      );
    });

    testWidgets('P6 — each sponsor logo is wired to the D-357 SponsorLogo route',
        (tester) async {
      await _pump(tester, <Override>[
        sponsorGroupsProvider.overrideWith((ref) async => _groups),
      ]);
      final urls = _networkImageUrls(tester);
      expect(
        urls,
        contains('http://test.local/api/v1/app/assets/SponsorLogo/s1/image'),
      );
      expect(
        urls,
        contains('http://test.local/api/v1/app/assets/SponsorLogo/s2/image'),
      );
    });

    testWidgets('PAR-S2 — RTL: the logo badge leads at the inline start (right) '
        'of the sponsor name', (tester) async {
      await _pump(
        tester,
        <Override>[
          sponsorGroupsProvider.overrideWith(
            (ref) async => const <SponsorTierGroup>[
              SponsorTierGroup(
                tier: 1,
                tierName: 'Strategic',
                sponsors: <Sponsor>[
                  Sponsor(
                    id: 's1', nameEn: 'SAMI', nameAr: 'سامي',
                    tierName: 'Strategic', displayOrder: 0,
                  ),
                ],
              ),
            ],
          ),
        ],
        locale: const Locale('ar'),
      );
      // Initials fallback ('س' from 'سامي') marks the logo badge; it must sit
      // to the inline start (physical right) of the name, per frame 922:2824.
      final badgeDx = tester.getCenter(find.text('س')).dx;
      final nameDx = tester.getCenter(find.text('سامي')).dx;
      expect(badgeDx, greaterThan(nameDx));
    });

    testWidgets('empty groups show the empty state', (tester) async {
      await _pump(tester, <Override>[
        sponsorGroupsProvider.overrideWith((ref) async => const <SponsorTierGroup>[]),
      ]);
      expect(find.text('No sponsors'), findsOneWidget);
    });

    testWidgets('groups with only empty tiers show the empty state',
        (tester) async {
      await _pump(tester, <Override>[
        sponsorGroupsProvider.overrideWith(
          (ref) async => const <SponsorTierGroup>[
            SponsorTierGroup(tier: 1, tierName: 'Empty', sponsors: <Sponsor>[]),
          ],
        ),
      ]);
      expect(find.text('No sponsors'), findsOneWidget);
      expect(find.text('Empty'), findsNothing);
    });

    testWidgets('error shows the error state', (tester) async {
      await _pump(tester, <Override>[
        sponsorGroupsProvider.overrideWith((ref) async => throw Exception('x')),
      ]);
      expect(find.text('Could not load the sponsors.'), findsOneWidget);
    });
  });
}
