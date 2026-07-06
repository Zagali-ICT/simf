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

  group('PhoneNumberFormatter', () {
    const formatter = PhoneNumberFormatter();

    String clean(String text) => formatter
        .formatEditUpdate(
          TextEditingValue.empty,
          TextEditingValue(
            text: text,
            selection: TextSelection.collapsed(offset: text.length),
          ),
        )
        .text;

    test('keeps digits and a single leading +', () {
      expect(clean('+966501234567'), '+966501234567');
      expect(clean('0501234567'), '0501234567');
    });

    test('drops letters and other symbols', () {
      expect(clean('+966 50a1-2b3'), '+96650123'); // space/dash/letters gone
      expect(clean('05x0y0'), '0500');
    });

    test('keeps a + only at the start', () {
      expect(clean('966+50'), '96650'); // non-leading + dropped
    });

    test('folds Arabic digits before filtering', () {
      expect(clean('+٩٦٦٥٠'), '+96650');
    });
  });
}
