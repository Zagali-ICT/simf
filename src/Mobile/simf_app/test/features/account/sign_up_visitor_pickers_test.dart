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

/// Opens the picker on a real MaterialApp, the only way the SDK's
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

/// Pins the 18-to-120 date-of-birth eligibility rule at its boundaries.
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
    // Dart rolls an out-of-range day forward silently, so `DateTime(2026, 2,
    // 29)` is already 2026-03-01 before the rule sees it.
    final range = visitorDateOfBirthRange(DateTime(2026, 2, 29));

    // `DateTime(2008, 3)` is 2008-03-01; the analyzer rejects the explicit 1.
    expect(range.latest, DateTime(2008, 3));
  });

  test('the range is ordered, so showDatePicker cannot be handed an empty one',
      () {
    final range = visitorDateOfBirthRange(DateTime(2026, 8, 20));

    expect(range.earliest.isBefore(range.latest), isTrue);
  });

  // A stored date of birth is never checked against the 18-to-120 rule, and
  // `showDatePicker` ASSERTS `initialDate` sits within `firstDate..lastDate`.
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

  // Only opening the real picker proves the clamp is wired to `initialDate`.
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

  // The tests above all inject `now`, so a device clock reinstated as the
  // PARAMETER's default walks past them; the device zone cannot be faked.
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
