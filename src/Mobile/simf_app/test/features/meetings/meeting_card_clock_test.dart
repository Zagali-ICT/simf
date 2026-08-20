import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/meetings/widgets/meeting_card.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/widgets/request_card.dart';

// The "today" badge on a meeting/request card compares a Saudi wall-clock
// meeting date against "now". It once compared it against `DateTime.now()` —
// the DEVICE clock — so on a phone outside Riyadh the two calendars disagreed
// for three hours a day and the badge landed on the wrong rows (D-219 / D-770:
// Saudi +3 everywhere).
//
// The cards take that instant as a constructor parameter, so these tests fix
// it instead of reading a clock. That is what makes them a pin: a card that
// consults a wall clock in ANY spelling — `DateTime.now()`,
// `DateTime.timestamp().toLocal()`, anything else — renders the real today,
// which is not January 2026, and reds on every machine. Letting the card read
// its own clock could never do that, because the device zone cannot be faked
// in-process and on a +03:00 box (every SIMF dev box and CI agent) the buggy
// and the correct clock produce the same value.

// Both instants sit in the three-hour window each day where the Saudi and the
// device calendar genuinely name different days: 02:30 on the 12th in Riyadh
// is still 18:30 on the 11th at UTC-5, and 23:30 on the 11th in Riyadh is
// already the 12th at UTC+13. So the two meetings below swap sides as the
// injected instant crosses Saudi midnight — a flip no wall-clock reading can
// reproduce, because it is fixed to one real instant.

/// Just after Saudi midnight: 02:30 on 12 January 2026.
final DateTime _afterSaudiMidnight = DateTime(2026, 1, 12, 2, 30);

/// Just before it: 23:30 on 11 January 2026.
final DateTime _beforeSaudiMidnight = DateTime(2026, 1, 11, 23, 30);

/// A meeting each side of that midnight.
final DateTime _meetingAfter = DateTime(2026, 1, 12, 2);
final DateTime _meetingBefore = DateTime(2026, 1, 11, 23);

const String _afterTodayEn = '02:00 AM · Today';
const String _afterTodayAr = '02:00 AM · اليوم';
const String _beforeTodayEn = '11:00 PM · Today';
const String _afterDateEn = '12 Jan 2026';
const String _beforeDateEn = '11 Jan 2026';

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

Future<void> _pump(
  WidgetTester tester,
  Widget Function(AppL10n) build, {
  String locale = 'en',
}) async {
  await tester.pumpWidget(
    MaterialApp(
      locale: Locale(locale),
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

/// Every way of asking the DEVICE for the wall clock. A grep pins one
/// spelling, which is why it is the second line here and not the first.
const List<String> _deviceClockSpellings = <String>[
  'DateTime.now()',
  'DateTime.timestamp()',
  'toLocal()',
];

void main() {
  group('the today badge follows the injected Saudi instant', () {
    testWidgets('MeetingCard badges the meeting just after Saudi midnight',
        (tester) async {
      await _pump(
        tester,
        (l10n) => MeetingCard(
          item: _meetingAt(_meetingAfter),
          isArabic: false,
          l10n: l10n,
          baseUrl: 'https://example.invalid',
          now: _afterSaudiMidnight,
        ),
      );
      expect(find.text(_afterTodayEn), findsOneWidget);
    });

    testWidgets('MeetingCard badges the meeting just before Saudi midnight',
        (tester) async {
      await _pump(
        tester,
        (l10n) => MeetingCard(
          item: _meetingAt(_meetingBefore),
          isArabic: false,
          l10n: l10n,
          baseUrl: 'https://example.invalid',
          now: _beforeSaudiMidnight,
        ),
      );
      expect(find.text(_beforeTodayEn), findsOneWidget);
    });

    testWidgets('MeetingCard dates the same meeting an hour earlier',
        (tester) async {
      await _pump(
        tester,
        (l10n) => MeetingCard(
          item: _meetingAt(_meetingAfter),
          isArabic: false,
          l10n: l10n,
          baseUrl: 'https://example.invalid',
          now: _beforeSaudiMidnight,
        ),
      );
      expect(find.text(_afterDateEn), findsOneWidget);
      expect(find.text(_afterTodayEn), findsNothing);
    });

    testWidgets('MeetingCard badges in Arabic too', (tester) async {
      await _pump(
        tester,
        (l10n) => MeetingCard(
          item: _meetingAt(_meetingAfter),
          isArabic: true,
          l10n: l10n,
          baseUrl: 'https://example.invalid',
          now: _afterSaudiMidnight,
        ),
        locale: 'ar',
      );
      expect(find.text(_afterTodayAr), findsOneWidget);
    });

    testWidgets('RequestCard badges the request just after Saudi midnight',
        (tester) async {
      await _pump(
        tester,
        (l10n) => RequestCard(
          item: _meetingAt(_meetingAfter),
          isArabic: false,
          l10n: l10n,
          onCancel: () {},
          now: _afterSaudiMidnight,
        ),
      );
      expect(find.text(_afterTodayEn), findsOneWidget);
    });

    testWidgets('RequestCard dates the meeting on the Saudi day before',
        (tester) async {
      await _pump(
        tester,
        (l10n) => RequestCard(
          item: _meetingAt(_meetingBefore),
          isArabic: false,
          l10n: l10n,
          onCancel: () {},
          now: _afterSaudiMidnight,
        ),
      );
      expect(find.text(_beforeDateEn), findsOneWidget);
      expect(find.text(_beforeTodayEn), findsNothing);
    });
  });

  // Second line only. The widget tests above are what actually pin the
  // behaviour; this catches the one case they cannot see — a device clock
  // reinstated as the PARAMETER's default, which every injecting test walks
  // straight past.
  group('neither card reads the device clock', () {
    for (final path in _cardFiles) {
      test('$path calls no device-clock API', () {
        final source = File(path).readAsStringSync();
        for (final spelling in _deviceClockSpellings) {
          expect(
            source.contains(spelling),
            isFalse,
            reason: 'A Saudi wall-clock date measured against the device '
                'clock badges the wrong rows "today" outside Riyadh. Use '
                'saudiNow() (core/utils/saudi_time.dart), and take the '
                'instant from the caller so a test can fix it.',
          );
        }
      });
    }
  });
}
