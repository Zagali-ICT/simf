import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/event_date_range.dart';

void main() {
  group('formatEventDateRange (#43 — mirrors SIMF.Common.EventDateRange)', () {
    test('same month + year collapses to "23-25 <month> 2026" (en + ar)', () {
      final start = DateTime(2026, 11, 23);
      final end = DateTime(2026, 11, 25);
      expect(
        formatEventDateRange(start, end, isArabic: false),
        '23-25 November 2026',
      );
      expect(
        formatEventDateRange(start, end, isArabic: true),
        '23-25 نوفمبر 2026',
      );
    });

    test('a single day renders one date', () {
      final d = DateTime(2026, 11, 23);
      expect(formatEventDateRange(d, d, isArabic: false), '23 November 2026');
    });

    test('same year, cross month spells the year once at the end', () {
      expect(
        formatEventDateRange(
          DateTime(2026, 11, 30),
          DateTime(2026, 12, 2),
          isArabic: false,
        ),
        '30 November - 2 December 2026',
      );
    });

    test('cross year spells both endpoints', () {
      expect(
        formatEventDateRange(
          DateTime(2026, 12, 31),
          DateTime(2027, 1, 2),
          isArabic: false,
        ),
        '31 December 2026 - 2 January 2027',
      );
    });

    test('a reversed pair is ordered so the earlier date reads first', () {
      expect(
        formatEventDateRange(
          DateTime(2026, 11, 25),
          DateTime(2026, 11, 23),
          isArabic: false,
        ),
        '23-25 November 2026',
      );
    });

    test('digits stay Western in Arabic (matches the splash + Figma)', () {
      final ar = formatEventDateRange(
        DateTime(2026, 11, 23),
        DateTime(2026, 11, 25),
        isArabic: true,
      );
      expect(ar.contains('23'), isTrue);
      expect(ar.contains('٢٣'), isFalse);
    });
  });
}
