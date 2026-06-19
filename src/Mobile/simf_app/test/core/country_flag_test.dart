import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/country_flag.dart';

void main() {
  group('countryFlagEmoji', () {
    test('maps Saudi Arabia (682) to the 🇸🇦 regional-indicator pair', () {
      // 🇸🇦 = U+1F1F8 (S) U+1F1E6 (A).
      expect(countryFlagEmoji(682), '\u{1F1F8}\u{1F1E6}');
    });

    test('maps a sample of other countries correctly', () {
      expect(countryFlagEmoji(840), '\u{1F1FA}\u{1F1F8}'); // US
      expect(countryFlagEmoji(826), '\u{1F1EC}\u{1F1E7}'); // GB
      expect(countryFlagEmoji(818), '\u{1F1EA}\u{1F1EC}'); // EG
      expect(countryFlagEmoji(784), '\u{1F1E6}\u{1F1EA}'); // AE
    });

    test('returns null for a null code', () {
      expect(countryFlagEmoji(null), isNull);
    });

    test('returns null for an unassigned code (no tofu / wrong flag)', () {
      expect(countryFlagEmoji(0), isNull);
      expect(countryFlagEmoji(999), isNull);
    });

    test('every flag is two code points (a well-formed pair)', () {
      for (final code in <int>[4, 156, 392, 682, 840]) {
        final flag = countryFlagEmoji(code)!;
        expect(flag.runes.length, 2, reason: 'code $code');
        for (final rune in flag.runes) {
          expect(rune, inInclusiveRange(0x1F1E6, 0x1F1FF));
        }
      }
    });
  });
}
