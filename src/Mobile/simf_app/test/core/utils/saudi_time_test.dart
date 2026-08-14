import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// Owner decision 2026-07-31 — the API sends Saudi local wall-clock with no
/// zone,
/// so there is no conversion left to test. This suite proves the opposite and
/// stronger property: **a wire value is taken verbatim**, on any device
/// timezone.
///
/// Every case here fails if anyone reintroduces a `toUtc()` / `toLocal()` —
/// which
/// is the mistake worth guarding, because a three-hour shift passes code review
/// and is noticed only by the person who misses their session.
void main() {
  group('saudiOf', () {
    test('is the identity — a stored value is already Saudi wall clock', () {
      final stored = DateTime(2026, 11, 20, 9);
      expect(saudiOf(stored), stored);
      expect(saudiOf(stored).hour, 9);
    });

    test('does not move a late-evening value across the day boundary', () {
      // The trap case. 22:30 on the 20th must stay 22:30 on the 20th. A
      // leftover
      // +03:00 projection would report 01:30 on the 21st.
      final stored = DateTime(2026, 11, 20, 22, 30);
      expect(saudiOf(stored).day, 20);
      expect(saudiOf(stored).hour, 22);
      expect(saudiOf(stored).minute, 30);
    });
  });

  group('parseWireDateTime', () {
    test('reads a zone-free wire value verbatim', () {
      final parsed = parseWireDateTime('2026-11-23T09:00:00', 'start');
      expect(parsed.hour, 9);
      expect(parsed.day, 23);
      // Left untagged: the value is Saudi wall clock, and tagging it would let
      // a later toLocal() shift it by the device offset.
      expect(parsed.isUtc, isFalse);
    });

    test('projects a legacy zone-bearing value onto the Saudi clock', () {
      // Dart normalises BOTH of these while parsing and throws the
      // original offset away, so the sender's wall clock cannot be read off the
      // fields. Legacy SIMF stored a zoned value and displayed +03:00, so the
      // offset is
      // re-applied — which is why the two lines below disagree by three hours
      // and
      // are both right.
      expect(parseWireDateTime('2026-11-23T09:00:00Z', 'start').hour, 12);
      expect(parseWireDateTime('2026-11-23T09:00:00+03:00', 'start').hour, 9);
    });

    test('is device-timezone independent', () {
      // No toUtc()/toLocal() anywhere in the path, so the parsed fields are the
      // string's fields on a machine in Riyadh, London or Los Angeles alike.
      final a = parseWireDateTime('2026-11-23T09:00:00', 'start');
      expect(
        <int>[a.year, a.month, a.day, a.hour, a.minute],
        <int>[2026, 11, 23, 9, 0],
      );
    });

    test('a missing or unparseable value throws instead of yielding 1970', () {
      // BUG-011 — the epoch fallback made a broken/renamed wire field render as
      // 03:00 AM on every row with no error at all. It must fail loudly.
      expect(() => parseWireDateTime(null, 'start'), throwsFormatException);
      expect(() => parseWireDateTime('', 'start'), throwsFormatException);
      expect(() => parseWireDateTime('not-a-timestamp', 'start'),
          throwsFormatException,);
      expect(() => parseWireDateTime(0, 'start'), throwsFormatException);
    });
  });

  group('formatWire', () {
    test('emits zone-free ISO-8601 — no Z, no offset', () {
      final wire = formatWire(DateTime(2026, 11, 23, 9, 30));
      expect(wire, startsWith('2026-11-23T09:30:00'));
      expect(wire, isNot(contains('Z')));
      expect(wire, isNot(contains('+')));
    });

    test('round-trips through parseWireDateTime unchanged', () {
      final original = DateTime(2026, 11, 23, 16, 45);
      expect(parseWireDateTime(formatWire(original), 'slotStart'), original);
    });
  });

  group('saudiNow', () {
    test('shifts the device clock onto Riyadh, on any device timezone', () {
      // The ONE remaining conversion: "now" starts at the device clock, which
      // may be in any timezone. Without it a phone outside Riyadh would answer
      // "is this session live yet" against the wrong clock.
      //
      // Asserted as a RELATIONSHIP rather than a fixed gap, so the test proves
      // the same thing on a Riyadh workstation and on a CI runner elsewhere.
      // It previously compared epochs against a zoned "now" and only passed
      // because saudiNow() was tagged, which made it three hours adrift of the
      // zone-free values it is compared with everywhere else.
      final device = DateTime.now();
      final now = saudiNow();

      expect(
        now.isUtc,
        isFalse,
        reason: 'saudiNow must be zone-free, like every decoded wire value — '
            'tagging it would let a later comparison shift it.',
      );
      expect(
        now.difference(device),
        saudiOffset - device.timeZoneOffset,
        reason: 'the shift applied must be exactly the distance from this '
            'device to Riyadh: zero here if the device already runs +03:00.',
      );
    });
  });

  test('formatSaudiTime12 renders 12-hour AM/PM verbatim', () {
    expect(formatSaudiTime12(DateTime(2026, 11, 22, 16, 45)), '04:45 PM');
    expect(formatSaudiTime12(DateTime(2026, 11, 20, 22, 30)), '10:30 PM');
  });

  group('formatTime12h / formatDateTime12h', () {
    // One formatter replacing three private copies (the speaker sheet, the
    // delegation sheet and the meeting-confirm screen). The Arabic meridiem is
    // the reason these could not just call formatSaudiTime12, which is
    // Latin-only because it mirrors the backend's SaudiTime.TimeFormat.
    test('renders the Arabic meridiem', () {
      expect(formatTime12h(hour: 10, minute: 0, isArabic: true), '10:00 ص');
      expect(formatTime12h(hour: 14, minute: 30, isArabic: true), '02:30 م');
    });

    test('renders the English meridiem', () {
      expect(formatTime12h(hour: 10, minute: 0, isArabic: false), '10:00 AM');
      expect(formatTime12h(hour: 14, minute: 30, isArabic: false), '02:30 PM');
    });

    test('midnight and noon read as 12, not 00', () {
      expect(formatTime12h(hour: 0, minute: 5, isArabic: false), '12:05 AM');
      expect(formatTime12h(hour: 12, minute: 5, isArabic: false), '12:05 PM');
      expect(formatTime12h(hour: 0, minute: 5, isArabic: true), '12:05 ص');
      expect(formatTime12h(hour: 12, minute: 5, isArabic: true), '12:05 م');
    });

    test('pads both fields to two digits', () {
      expect(formatTime12h(hour: 9, minute: 7, isArabic: false), '09:07 AM');
    });

    test('formatDateTime12h reads the local wall clock off a DateTime', () {
      final local = DateTime(2026, 11, 24, 16, 45);
      expect(formatDateTime12h(local, isArabic: false), '04:45 PM');
      expect(formatDateTime12h(local, isArabic: true), '04:45 م');
    });
  });

  group('formatCountdown', () {
    test('pads both fields to two digits', () {
      expect(formatCountdown(0), '00:00');
      expect(formatCountdown(9), '00:09');
      expect(formatCountdown(60), '01:00');
    });

    test('carries minutes past ten', () {
      expect(formatCountdown(599), '09:59');
      expect(formatCountdown(600), '10:00');
    });

    test('does not wrap at an hour — these timers never run that long', () {
      expect(formatCountdown(3600), '60:00');
    });
  });
}
