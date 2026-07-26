import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

void main() {
  group('saudiOf', () {
    test('shifts a UTC instant by +3 hours (Saudi wall clock)', () {
      final saudi = saudiOf(DateTime.utc(2026, 11, 20, 9)); // 09:00 UTC
      expect(saudi.hour, 12); // 12:00 AST
      expect(saudi.day, 20);
    });

    test('crosses the day boundary (22:30 UTC -> 01:30 next day AST)', () {
      final saudi = saudiOf(DateTime.utc(2026, 11, 20, 22, 30));
      expect(saudi.day, 21);
      expect(saudi.hour, 1);
      expect(saudi.minute, 30);
    });

    test('is device-timezone independent (normalised via toUtc first)', () {
      // Whatever Kind the input carries, it is normalised to UTC before the +3
      // shift, so the projected wall clock is the same on any test machine.
      expect(saudiOf(DateTime.utc(2026, 11, 22, 13, 45)).hour, 16); // 16:45 AST
    });
  });

  group('parseWireUtc', () {
    test('reads an ISO-8601 instant as UTC', () {
      final parsed = parseWireUtc('2026-11-23T06:00:00Z', 'start');
      expect(parsed.isUtc, isTrue);
      expect(parsed, DateTime.utc(2026, 11, 23, 6));
    });

    test('normalises an offset instant to UTC', () {
      expect(
        parseWireUtc('2026-11-23T09:00:00+03:00', 'start'),
        DateTime.utc(2026, 11, 23, 6),
      );
    });

    test('a missing or unparseable value throws instead of yielding 1970', () {
      // BUG-011 — the epoch fallback made a broken/renamed wire field render as
      // 03:00 AM on every row with no error at all. It must fail loudly.
      expect(() => parseWireUtc(null, 'start'), throwsFormatException);
      expect(() => parseWireUtc('', 'start'), throwsFormatException);
      expect(() => parseWireUtc('not-a-timestamp', 'start'), throwsFormatException);
      expect(() => parseWireUtc(0, 'start'), throwsFormatException);
    });
  });

  test('formatSaudiTime12 renders 12-hour AM/PM', () {
    expect(formatSaudiTime12(DateTime.utc(2026, 11, 22, 13, 45)), '04:45 PM');
    expect(formatSaudiTime12(DateTime.utc(2026, 11, 20, 22, 30)), '01:30 AM');
  });
}
