import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/validation/phone_country_code.dart';

/// The calling-code split is the risky half of the country-code field: get it
/// wrong and re-opening a saved profile mangles a number the visitor already
/// entered, silently, on a required field.
void main() {
  // A realistic slice of what the country list actually supplies, including the
  // overlapping pair that makes naive prefix matching wrong.
  const codes = <String>['+966', '+973', '+965', '+20', '+249', '+1', '+1876'];

  group('splitPhone', () {
    test('splits a stored international number at its calling code', () {
      expect(
        splitPhone('+966501234567', codes),
        const PhoneParts(callingCode: '+966', number: '501234567'),
      );
    });

    test('LONGEST match wins, so +1876 does not lose its 876 to +1', () {
      // Both are real calling codes. Shortest-first would take "+1" off and
      // leave "876…" sitting in the subscriber box, which reads as a plausible
      // number and is wrong.
      expect(
        splitPhone('+18765550123', codes),
        const PhoneParts(callingCode: '+1876', number: '5550123'),
      );
      expect(
        splitPhone('+15550123456', codes),
        const PhoneParts(callingCode: '+1', number: '5550123456'),
      );
    });

    test('recognises the Saudi local form, which carries no plus at all', () {
      // 05XXXXXXXX is how Saudi numbers are written and stored, so no calling
      // code could ever match it by prefix.
      expect(
        splitPhone('0501234567', codes),
        const PhoneParts(callingCode: '+966', number: '501234567'),
      );
    });

    test('folds 00 and Arabic-Indic digits before matching', () {
      expect(
        splitPhone('00966501234567', codes),
        const PhoneParts(callingCode: '+966', number: '501234567'),
      );
      expect(
        splitPhone('+٩٦٦٥٠١٢٣٤٥٦٧', codes),
        const PhoneParts(callingCode: '+966', number: '501234567'),
      );
    });

    test('an unrecognised country is returned WHOLE, not truncated', () {
      // The alternative — dropping a leading digit or two to force a match — is
      // how a stored number silently becomes a different number.
      expect(
        splitPhone('+99912345678', codes),
        const PhoneParts(callingCode: '', number: '+99912345678'),
      );
    });

    test('empty and null are a usable empty pair, not a crash', () {
      expect(splitPhone(null, codes),
          const PhoneParts(callingCode: '', number: ''),);
      expect(splitPhone('   ', codes),
          const PhoneParts(callingCode: '', number: ''),);
    });

    test('a bare calling code keeps its code rather than emptying the field',
        () {
      // value.length > code.length, so "+966" alone does not split to
      // ("+966", "") and lose the fact that something was typed.
      expect(
        splitPhone('+966', codes),
        const PhoneParts(callingCode: '', number: '+966'),
      );
    });

    test('an empty code in the list is ignored', () {
      // Countries an administrator created without a prefix arrive as ''.
      expect(
        splitPhone('+966501234567', const <String>['', '  ', '+966']),
        const PhoneParts(callingCode: '+966', number: '501234567'),
      );
    });
  });

  group('composePhone', () {
    test('joins the code and the number', () {
      expect(composePhone('+966', '501234567'), '+966501234567');
    });

    test('drops a leading zero the visitor typed out of habit', () {
      // People write their number the local way even after picking a code, and
      // +9660501234567 is not a valid number.
      expect(composePhone('+966', '0501234567'), '+966501234567');
    });

    test('accepts a code with no plus', () {
      expect(composePhone('966', '501234567'), '+966501234567');
    });

    test('an empty number is empty, NOT a bare calling code', () {
      // Submitting "+966" would fail the server's shape check with a message
      // that reads as though the visitor mistyped, when they typed nothing.
      expect(composePhone('+966', ''), '');
      expect(composePhone('+966', '   '), '');
    });

    test('with no code chosen the number is passed through normalised', () {
      expect(composePhone('', '00966501234567'), '+966501234567');
    });

    test('round-trips with splitPhone', () {
      const codes = <String>['+966', '+249'];
      for (final stored in <String>['+966501234567', '+249912345678']) {
        final parts = splitPhone(stored, codes);
        expect(composePhone(parts.callingCode, parts.number), stored);
      }
    });
  });
}
