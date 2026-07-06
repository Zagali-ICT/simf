import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/validation/digit_normalization.dart';

void main() {
  group('toWesternDigits', () {
    test('folds Arabic-Indic digits to Western', () {
      expect(toWesternDigits('١٢٣٤٥٦٧٨٩٠'), '1234567890');
    });

    test('folds Extended Arabic-Indic / Persian digits to Western', () {
      expect(toWesternDigits('۰۱۲۳۴۵۶۷۸۹'), '0123456789');
    });

    test('leaves Western digits and other characters untouched', () {
      expect(toWesternDigits('+9665٠١٢'), '+9665012');
      expect(toWesternDigits('ABJ'), 'ABJ');
      expect(toWesternDigits(''), '');
    });
  });

  group('WesternDigitsFormatter', () {
    const formatter = WesternDigitsFormatter();

    TextEditingValue fold(String text) => formatter.formatEditUpdate(
          TextEditingValue.empty,
          TextEditingValue(
            text: text,
            selection: TextSelection.collapsed(offset: text.length),
          ),
        );

    test('rewrites Arabic digits as the user types', () {
      final result = fold('١٠٠٠');
      expect(result.text, '1000');
      expect(result.selection.baseOffset, 4);
    });

    test('returns the value unchanged when already Western', () {
      final result = fold('1000');
      expect(result.text, '1000');
    });
  });
}
