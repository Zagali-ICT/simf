import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/meet/data/meet_models.dart';
import 'package:simf_app/features/meet/meet_people_screen.dart';

const _matches = <Recommendation>[
  Recommendation(
    userProfileId: 'u1',
    englishName: 'Sarah Hill',
    arabicName: 'سارة هل',
    jobTitle: 'Naval Architect',
    profileTypeName: 'Visitor',
    profileTypeNameArabic: 'زائر',
    sharedInterests: <MatchedInterest>[
      MatchedInterest(id: 'i1', name: 'Shipbuilding', nameArabic: 'بناء السفن'),
    ],
    sharedInterestCount: 3,
    score: 0.82,
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
        home: const MeetPeopleScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MeetPeopleScreen (Page 035)', () {
    testWidgets('renders the match cards', (tester) async {
      await _pump(tester, <Override>[
        meetRecommendationsProvider.overrideWith((ref) async => _matches),
      ]);
      expect(find.text('Sarah Hill'), findsOneWidget);
      expect(find.textContaining('Naval Architect'), findsOneWidget);
      expect(find.text('Shipbuilding'), findsOneWidget);
      expect(find.textContaining('3'), findsOneWidget);
    });

    testWidgets('empty list shows the empty state', (tester) async {
      await _pump(tester, <Override>[
        meetRecommendationsProvider
            .overrideWith((ref) async => const <Recommendation>[]),
      ]);
      expect(find.text('No matches yet'), findsOneWidget);
    });

    testWidgets('error shows the error state', (tester) async {
      await _pump(tester, <Override>[
        meetRecommendationsProvider
            .overrideWith((ref) async => throw Exception('x')),
      ]);
      expect(find.text('Could not load your matches.'), findsOneWidget);
    });
  });
}
