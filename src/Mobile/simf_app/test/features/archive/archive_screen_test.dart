import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/archive/archive_screen.dart';
import 'package:simf_app/features/archive/data/archive_models.dart';

const _editions = <ArchiveEdition>[
  ArchiveEdition(
    id: 'a1',
    year: 2025,
    titleEn: '3rd Edition',
    titleAr: 'النسخة الثالثة',
    attendees: 1200,
    sessions: 40,
    speakers: 60,
    summaryEn: 'A great year.',
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
        home: const ArchiveScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('ArchiveScreen (Page 024)', () {
    testWidgets('lists the editions with stats', (tester) async {
      await _pump(tester, <Override>[
        archiveEditionsProvider.overrideWith((ref) async => _editions),
      ]);
      expect(find.text('3rd Edition'), findsOneWidget);
      expect(find.textContaining('1200 attendees'), findsOneWidget);
    });

    testWidgets('empty shows the empty state', (tester) async {
      await _pump(tester, <Override>[
        archiveEditionsProvider.overrideWith((ref) async => const <ArchiveEdition>[]),
      ]);
      expect(find.text('No past editions'), findsOneWidget);
    });

    testWidgets('error shows the error state', (tester) async {
      await _pump(tester, <Override>[
        archiveEditionsProvider.overrideWith((ref) async => throw Exception('x')),
      ]);
      expect(find.text('Could not load the archive.'), findsOneWidget);
    });
  });

  group('ArchiveEdition.fromJson', () {
    test('binds the En/Ar wire names + stats', () {
      final e = ArchiveEdition.fromJson(<String, dynamic>{
        'id': 'a1',
        'year': 2025,
        'titleEn': '3rd Edition',
        'titleAr': 'النسخة الثالثة',
        'attendees': 1200,
        'sessions': 40,
        'speakers': 60,
      });
      expect(e.localizedTitle(true), 'النسخة الثالثة');
      expect(e.localizedTitle(false), '3rd Edition');
      expect(e.attendees, 1200);
    });
  });
}
