import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/media_partners/media_partners_screen.dart';

const _partners = <MediaPartner>[
  MediaPartner(
    id: 'p1',
    name: 'Al Arabiya',
    nameArabic: 'العربية',
    url: 'https://x',
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
        home: const MediaPartnersScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MediaPartnersScreen (Page 031)', () {
    testWidgets('renders the partner list', (tester) async {
      await _pump(tester, <Override>[
        mediaPartnersProvider.overrideWith((ref) async => _partners),
      ]);
      // The mockup `.partner` card shows the logo box + name (no raw URL).
      expect(find.text('Al Arabiya'), findsOneWidget);
      expect(find.byType(Card), findsOneWidget);
    });

    testWidgets('empty shows the empty state', (tester) async {
      await _pump(tester, <Override>[
        mediaPartnersProvider.overrideWith((ref) async => const <MediaPartner>[]),
      ]);
      expect(find.text('No media partners'), findsOneWidget);
    });

    testWidgets('error shows the error state', (tester) async {
      await _pump(tester, <Override>[
        mediaPartnersProvider.overrideWith((ref) async => throw Exception('x')),
      ]);
      expect(find.text('Could not load the media partners.'), findsOneWidget);
    });
  });
}
