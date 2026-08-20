import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/sign_up_visitor_pickers.dart';

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
}
