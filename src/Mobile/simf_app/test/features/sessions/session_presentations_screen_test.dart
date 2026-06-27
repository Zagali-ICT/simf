import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sessions/data/presentation_models.dart';
import 'package:simf_app/features/sessions/data/presentation_repository.dart';
import 'package:simf_app/features/sessions/session_presentations_screen.dart';

PresentationItem _item(String id, String title) => PresentationItem(
      id: id,
      sessionId: 's-$id',
      sessionTitle: title,
      sessionTitleArabic: title,
      sessionStartUtc: DateTime.utc(2026, 11, 23, 6),
      speakerName: 'Dr. Omari',
      speakerNameArabic: 'د. العمري',
      fileName: '$id.pdf',
      contentType: 'application/pdf',
      sizeBytes: 2048,
    );

Future<void> _pump(WidgetTester tester, List<PresentationItem> items) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        presentationsProvider
            .overrideWith((ref) async => PresentationsPage(items)),
      ],
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const SessionPresentationsScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SessionPresentationsScreen (Figma 1388:7621)', () {
    testWidgets('lists the decks with the speaker and a Download button',
        (tester) async {
      await _pump(tester, <PresentationItem>[_item('p1', 'Future of Investment')]);

      expect(find.text('Future of Investment'), findsOneWidget);
      expect(find.text('Dr. Omari'), findsOneWidget);
      expect(find.text('Download'), findsOneWidget);
      // The الكل / All day tab is present.
      expect(find.text('All'), findsOneWidget);
      // "Day 1" appears as both the day tab and the card's event-day label
      // (Figma 1388:7664) — the card label is the one this re-skin added.
      expect(find.text('Day 1'), findsNWidgets(2));
    });

    testWidgets('shows the empty state when there are no presentations',
        (tester) async {
      await _pump(tester, const <PresentationItem>[]);
      expect(find.text('No presentations available yet.'), findsOneWidget);
    });
  });
}
