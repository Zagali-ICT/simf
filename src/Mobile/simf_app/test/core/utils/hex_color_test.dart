import 'package:flutter/painting.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/hex_color.dart';

void main() {
  group('parseHexColor', () {
    test('parses the canonical #RRGGBB the CP stores', () {
      expect(parseHexColor('#0E7490'), const Color(0xFF0E7490)); // VIP teal
      expect(parseHexColor('#F59E0B'), const Color(0xFFF59E0B)); // Media amber
    });

    test('parses the #RGB shorthand the API docs use', () {
      expect(parseHexColor('#0B5'), const Color(0xFF00BB55));
    });

    test('tolerates a missing # and surrounding whitespace', () {
      expect(parseHexColor('  244A77 '), const Color(0xFF244A77));
    });

    test('parses an explicit AARRGGBB alpha', () {
      expect(parseHexColor('#800E7490'), const Color(0x800E7490));
    });

    test('returns null for null / blank / non-hex so the caller can fall back',
        () {
      expect(parseHexColor(null), isNull);
      expect(parseHexColor(''), isNull);
      expect(parseHexColor('   '), isNull);
      expect(parseHexColor('teal'), isNull);
      expect(parseHexColor('#12'), isNull);
      expect(parseHexColor('#12345'), isNull);
    });
  });
}
