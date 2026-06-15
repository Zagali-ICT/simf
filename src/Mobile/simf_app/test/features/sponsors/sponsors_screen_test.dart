import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sponsors/data/sponsor_models.dart';
import 'package:simf_app/features/sponsors/sponsors_screen.dart';

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

Future<void> _pump(WidgetTester tester, List<Override> overrides) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: overrides,
      child: MaterialApp(
        locale: const Locale('en'),
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
