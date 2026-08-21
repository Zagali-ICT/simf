import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/sign_up_visitor_pickers.dart';

/// A fixed "today", so the eligible window is 1906-01-01 .. 2008-08-20.
final DateTime _now = DateTime(2026, 8, 20);

/// The file under test, read as source by the clock pin below.
const String _pickersFile = 'lib/features/account/sign_up_visitor_pickers.dart';

/// Every way of asking the DEVICE for the wall clock.
const List<String> _deviceClockSpellings = <String>[
  'DateTime.now()',
  'DateTime.timestamp()',
  'toLocal()',
];

/// Opens the picker on a real MaterialApp, which is the only way the SDK's
/// `initialDate` asserts can fire.
Future<void> _openPicker(
  WidgetTester tester, {
  required DateTime? current,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () => pickVisitorDateOfBirth(
              context,
              current: current,
              now: _now,
            ),
            child: const Text('open'),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
}

/// The 18-to-120 eligibility rule, pinned at its boundaries.
///
/// It used to be three lines inside `_pickDateOfBirth`, computed from
/// `DateTime.now()` — a business rule reachable only by pumping a widget and
/// opening an OS dialog, and therefore never tested. Moving it out of the
/// screen (which also brought the file back under the 400-line ratchet) makes
/// it a pure function that takes `now`, so the boundary can be asserted on a
/// fixed date rather than on whatever day the suite happens to run.
void main() {
  test('the newest eligible date of birth is exactly the 18th birthday', () {
    final range = visitorDateOfBirthRange(DateTime(2026, 8, 20));

    expect(range.latest, DateTime(2008, 8, 20));
  });

  test('the oldest eligible date of birth is 120 years back', () {
    final range = visitorDateOfBirthRange(DateTime(2026, 8, 20));

    expect(range.earliest, DateTime(1906));
  });

  test('an impossible calendar date rolls forward rather than throwing', () {
    // 2026 is not a leap year, so `DateTime(2026, 2, 29)` is ALREADY
    // 2026-03-01 before the rule ever sees it — Dart rolls an out-of-range day
    // forward silently instead of throwing. The 18-years-back arithmetic then
    // carries that rolled month and day through, which is why the expectation
    // is March 1st and not February 29th.
    //
    // Pinned because the silence is the hazard: a rewrite that clamps instead
    // of rolling, or that parses the date from a string, would move this
    // boundary by a day with nothing to announce it.
    final range = visitorDateOfBirthRange(DateTime(2026, 2, 29));

    // `DateTime(2008, 3)` IS 2008-03-01 — the day argument defaults to 1 and
    // the analyzer rejects spelling it out.
    expect(range.latest, DateTime(2008, 3));
  });

  test('the range is ordered, so showDatePicker cannot be handed an empty one',
      () {
    final range = visitorDateOfBirthRange(DateTime(2026, 8, 20));

    expect(range.earliest.isBefore(range.latest), isTrue);
  });

  // The stored date of birth comes back from the server profile, which is
  // never checked against the 18-to-120 rule, and `showDatePicker` ASSERTS
  // that its `initialDate` sits within `firstDate..lastDate`. So a profile
  // carrying an ineligible date crashed the picker in debug rather than
  // opening it at the nearest date the user is allowed to pick.
  group('the seed is pulled inside the eligible range', () {
    test('a date of birth younger than 18 seeds at the newest eligible date',
        () {
      final range = visitorDateOfBirthRange(_now);

      final seed = visitorDateOfBirthSeed(
        current: DateTime(2020, 5, 4),
        range: range,
      );

      expect(seed, range.latest);
    });

    test('a date of birth older than 120 seeds at the oldest eligible date',
        () {
      final range = visitorDateOfBirthRange(_now);

      final seed = visitorDateOfBirthSeed(
        current: DateTime(1890, 5, 4),
        range: range,
      );

      expect(seed, range.earliest);
    });

    test('an eligible date of birth is seeded verbatim', () {
      final range = visitorDateOfBirthRange(_now);

      final seed = visitorDateOfBirthSeed(
        current: DateTime(1990, 5, 4),
        range: range,
      );

      expect(seed, DateTime(1990, 5, 4));
    });

    test('nothing stored yet seeds at the newest eligible date', () {
      final range = visitorDateOfBirthRange(_now);

      expect(
        visitorDateOfBirthSeed(current: null, range: range),
        range.latest,
      );
    });
  });

  // The unit tests above prove the clamp; only opening the real picker proves
  // it is WIRED to `initialDate`. An unclamped seed trips the SDK's own
  // `initialDate ... must be on or before lastDate` assert, which fails the
  // test and leaves no dialog behind.
  group('the picker opens on an out-of-range stored date', () {
    testWidgets('a date of birth younger than 18 opens the picker',
        (tester) async {
      await _openPicker(tester, current: DateTime(2020, 5, 4));

      expect(find.byType(DatePickerDialog), findsOneWidget);
    });

    testWidgets('a date of birth older than 120 opens the picker',
        (tester) async {
      await _openPicker(tester, current: DateTime(1890, 5, 4));

      expect(find.byType(DatePickerDialog), findsOneWidget);
    });
  });

  // Second line only, and it catches what the tests above cannot see: they all
  // inject `now`, so a device clock reinstated as the PARAMETER's default
  // walks straight past them. The device zone cannot be faked in-process, and
  // on a +03:00 box (every SIMF dev box and CI agent) the device clock and the
  // Saudi clock agree — so no behavioural test can tell them apart here.
  test('the default clock is the Saudi one, not the device clock', () {
    final source = File(_pickersFile).readAsStringSync();

    for (final spelling in _deviceClockSpellings) {
      expect(
        source.contains(spelling),
        isFalse,
        reason: 'An age boundary measured on the device clock is a day out '
            'for a traveller, so the 18th birthday falls on the wrong date '
            'for them. Use saudiNow() (core/utils/saudi_time.dart) — D-219 / '
            'D-770.',
      );
    }
  });
}
