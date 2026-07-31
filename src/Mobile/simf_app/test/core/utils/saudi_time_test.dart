import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// Owner decision 2026-07-31 — the API sends Saudi local wall-clock with no zone,
/// so there is no conversion left to test. This suite proves the opposite and
/// stronger property: **a wire value is taken verbatim**, on any device timezone.
///
/// Every case here fails if anyone reintroduces a `toUtc()` / `toLocal()` — which
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
      // The trap case. 22:30 on the 20th must stay 22:30 on the 20th. A leftover
      // +03:00 projection would report 01:30 on the 21st.
      final stored = DateTime(2026, 11, 20, 22, 30);
      expect(saudiOf(stored).day, 20);
      expect(saudiOf(stored).hour, 22);
      expect(saudiOf(stored).minute, 30);
    });
  });

  group('parseWireUtc', () {
    test('reads a zone-free wire value verbatim', () {
      final parsed = parseWireUtc('2026-11-23T09:00:00', 'start');
      expect(parsed.hour, 9);
      expect(parsed.day, 23);
      // Not tagged UTC: the value is Saudi wall clock, and tagging it would let
      // a later toLocal() shift it by the device offset.
      expect(parsed.isUtc, isFalse);
    });

    test('projects a legacy zone-bearing value onto the Saudi clock', () {
      // Dart normalises BOTH of these to UTC while parsing and throws the
      // original offset away, so the sender's wall clock cannot be read off the
      // fields. Legacy SIMF stored UTC and displayed +03:00, so the offset is
      // re-applied — which is why the two lines below disagree by three hours and
      // are both right.
      expect(parseWireUtc('2026-11-23T09:00:00Z', 'start').hour, 12);
      expect(parseWireUtc('2026-11-23T09:00:00+03:00', 'start').hour, 9);
    });

    test('is device-timezone independent', () {
      // No toUtc()/toLocal() anywhere in the path, so the parsed fields are the
      // string's fields on a machine in Riyadh, London or Los Angeles alike.
      final a = parseWireUtc('2026-11-23T09:00:00', 'start');
      expect(<int>[a.year, a.month, a.day, a.hour, a.minute],
          <int>[2026, 11, 23, 9, 0]);
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

  group('formatWire', () {
    test('emits zone-free ISO-8601 — no Z, no offset', () {
      final wire = formatWire(DateTime(2026, 11, 23, 9, 30));
      expect(wire, startsWith('2026-11-23T09:30:00'));
      expect(wire, isNot(contains('Z')));
      expect(wire, isNot(contains('+')));
    });

    test('round-trips through parseWireUtc unchanged', () {
      final original = DateTime(2026, 11, 23, 16, 45);
      expect(parseWireUtc(formatWire(original), 'slotStart'), original);
    });
  });

  group('saudiNow', () {
    test('converts from the device clock, because that one must', () {
      // The ONE remaining conversion: "now" starts at the device clock, which may
      // be in any timezone, so it is normalised through UTC and offset to Riyadh.
      // Without it, a phone outside Riyadh would compare "is this session live"
      // against the wrong clock.
      // The raw device UTC clock is the deliberate comparison point here — this
      // is the one place `DateTime.now().toUtc()` is correct, because the whole
      // point is to measure the offset saudiNow() applies to it.
      final gap = saudiNow().difference(DateTime.now().toUtc());
      expect(gap.inMinutes, inInclusiveRange(179, 181));
    });
  });

  test('formatSaudiTime12 renders 12-hour AM/PM verbatim', () {
    expect(formatSaudiTime12(DateTime(2026, 11, 22, 16, 45)), '04:45 PM');
    expect(formatSaudiTime12(DateTime(2026, 11, 20, 22, 30)), '10:30 PM');
  });
}
