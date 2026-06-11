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
    testWidgets('renders tier headers + sponsor cards', (tester) async {
      await _pump(tester, <Override>[
        sponsorGroupsProvider.overrideWith((ref) async => _groups),
      ]);
      expect(find.text('Strategic'), findsOneWidget);
      expect(find.text('SAMI'), findsOneWidget);
      expect(find.text('https://sami.sa'), findsOneWidget);
    });

    testWidgets('empty groups show the empty state', (tester) async {
      await _pump(tester, <Override>[
        sponsorGroupsProvider.overrideWith((ref) async => const <SponsorTierGroup>[]),
      ]);
      expect(find.text('No sponsors'), findsOneWidget);
    });

    testWidgets('error shows the error state', (tester) async {
      await _pump(tester, <Override>[
        sponsorGroupsProvider.overrideWith((ref) async => throw Exception('x')),
      ]);
      expect(find.text('Could not load the sponsors.'), findsOneWidget);
    });
  });
}
