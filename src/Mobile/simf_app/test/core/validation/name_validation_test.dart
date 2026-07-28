import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/validation/name_validation.dart';

/// BUG-021 — the Arabic name class used to stop at U+064A, so an ordinary name
/// carrying a SHADDA (U+0651) was rejected by the server and, worse, silently
/// dropped by the field's input formatter as the user typed. These mirror the
/// server's `UpsertUserProfileRequestValidator` cases so the two stay in step.
void main() {
  group('arabicNameLettersOnly', () {
    test('accepts a name carrying tashkeel (shadda + fatha)', () {
      expect(arabicNameLettersOnly.hasMatch('محمَّد عبدالله'), isTrue);
      expect(arabicNameLettersOnly.hasMatch('محمد عبدالله'), isTrue);
    });

    test('accepts tatweel and still rejects Latin letters and digits', () {
      expect(arabicNameLettersOnly.hasMatch('محمـد عبدالله'), isTrue);
      expect(arabicNameLettersOnly.hasMatch('محمد Ahmed'), isFalse);
      expect(arabicNameLettersOnly.hasMatch('محمد 123'), isFalse);
      expect(arabicNameLettersOnly.hasMatch('محمد ١٢٣'), isFalse);
    });
  });

  group('arabicNameCharacters (the input-formatter class)', () {
    test('keeps a typed shadda instead of swallowing it', () {
      expect(_typed('محمَّد'), 'محمَّد');
    });

    test('still filters out Latin letters and digits', () {
      expect(_typed('محمد Ahmed'), 'محمد ');
      expect(_typed('محمد123'), 'محمد');
    });
  });

  group('hasFullNameParts', () {
    test('needs at least two whitespace-separated parts', () {
      expect(hasFullNameParts('محمَّد'), isFalse);
      expect(hasFullNameParts('محمَّد عبدالله'), isTrue);
    });
  });
}

/// Runs [value] through the same formatter the Arabic name fields install.
String _typed(String value) => FilteringTextInputFormatter.allow(
      arabicNameCharacters,
    )
        .formatEditUpdate(
          TextEditingValue.empty,
          TextEditingValue(
            text: value,
            selection: TextSelection.collapsed(offset: value.length),
          ),
        )
        .text;
