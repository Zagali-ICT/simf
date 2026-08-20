import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/meetings/widgets/meeting_card.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/widgets/request_card.dart';

// The "today" badge on a meeting/request card compared a Saudi wall-clock
// meeting date against `DateTime.now()` — the DEVICE clock. On a phone outside
// Riyadh the two calendars disagree for three hours a day, so the badge landed
// on the wrong meetings (D-219 / D-770: Saudi +3 everywhere).

AppRequestItem _meetingAt(DateTime slot) => AppRequestItem(
      kind: AppRequestKind.delegationMeeting,
      id: '1',
      title: 'Delegation meeting',
      titleArabic: 'لقاء وفد',
      status: AppRequestStatus.accepted,
      createdAt: DateTime(2026),
      canCancel: false,
      eventDate: slot,
    );

Future<void> _pump(WidgetTester tester, Widget Function(AppL10n) build) async {
  await tester.pumpWidget(
    MaterialApp(
      locale: const Locale('en'),
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: Scaffold(
        body: Builder(builder: (context) => build(AppL10n.of(context))),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

/// The two card files whose date line must read the Saudi clock.
const List<String> _cardFiles = <String>[
  'lib/features/meetings/widgets/meeting_card.dart',
  'lib/features/requests/widgets/request_card.dart',
];

void main() {
  // The device zone cannot be faked in-process — Dart reads it from the OS and
  // ignores `TZ` on Windows — and on a +03:00 dev machine the buggy and the
  // fixed clock agree, so a rendered "today" cannot tell them apart there.
  // Reading the source is what makes this regression fail the build on EVERY
  // machine rather than only on a traveller's phone.
  group('the card date line reads the Saudi clock, not the device clock', () {
    for (final path in _cardFiles) {
      test('$path does not call DateTime.now()', () {
        final source = File(path).readAsStringSync();
        expect(
          source.contains('DateTime.now()'),
          isFalse,
          reason: 'A Saudi wall-clock meeting date compared against the device '
              'clock labels the wrong rows "today" outside Riyadh. Use '
              'saudiNow() (core/utils/saudi_time.dart).',
        );
      });
    }
  });

  group('the today badge follows the Saudi calendar day', () {
    testWidgets('MeetingCard badges a meeting on the current Saudi date',
        (tester) async {
      final saudiToday = saudiNow();
      await _pump(
        tester,
        (l10n) => Text(l10n.requestTimeToday(saudiToday)),
      );
      final expected = tester.widget<Text>(find.byType(Text)).data!;

      await _pump(
        tester,
        (l10n) => MeetingCard(
          item: _meetingAt(saudiToday),
          isArabic: false,
          l10n: l10n,
          baseUrl: 'https://example.invalid',
        ),
      );
      expect(find.text(expected), findsOneWidget);
    });

    testWidgets('MeetingCard shows the absolute date one Saudi day earlier',
        (tester) async {
      final yesterday = saudiNow().subtract(const Duration(days: 1));
      await _pump(
        tester,
        (l10n) => Text(l10n.requestDate(yesterday)),
      );
      final expected = tester.widget<Text>(find.byType(Text)).data!;

      await _pump(
        tester,
        (l10n) => MeetingCard(
          item: _meetingAt(yesterday),
          isArabic: false,
          l10n: l10n,
          baseUrl: 'https://example.invalid',
        ),
      );
      expect(find.text(expected), findsOneWidget);
    });

    testWidgets('RequestCard badges a request on the current Saudi date',
        (tester) async {
      final saudiToday = saudiNow();
      await _pump(
        tester,
        (l10n) => Text(l10n.requestTimeToday(saudiToday)),
      );
      final expected = tester.widget<Text>(find.byType(Text)).data!;

      await _pump(
        tester,
        (l10n) => RequestCard(
          item: _meetingAt(saudiToday),
          isArabic: false,
          l10n: l10n,
          onCancel: () {},
        ),
      );
      expect(find.text(expected), findsOneWidget);
    });
  });
}
