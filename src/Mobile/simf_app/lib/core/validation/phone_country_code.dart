/// Splitting a stored phone number back into a country code and a subscriber
/// number, and putting the two together again.
///
/// The mobile field shows the calling code in front of the number so it can be
/// changed independently of nationality — a Sudanese national attending on a
/// Saudi number picks `+966` and types the rest. The stored value stays a
/// single canonical E.164 string, so the split has to be reconstructed on load.
///
/// [splitPhone] is the risky half: get it wrong and re-opening a saved profile
/// mangles a number the visitor already entered. It is deliberately pure and
/// total — every input produces a usable pair, and an unrecognised value is
/// returned whole rather than guessed at.
library;

import 'package:flutter/foundation.dart';
import 'package:simf_app/core/validation/phone_validation.dart';

/// A phone number as the form edits it: a calling code (`+966`) and the
/// subscriber digits after it.
@immutable
class PhoneParts {
  const PhoneParts({required this.callingCode, required this.number});

  /// The `+`-prefixed calling code, or empty when none could be identified.
  final String callingCode;

  /// The digits after the calling code, with no leading zero.
  final String number;

  @override
  String toString() => 'PhoneParts($callingCode, $number)';

  @override
  bool operator ==(Object other) =>
      other is PhoneParts &&
      other.callingCode == callingCode &&
      other.number == number;

  @override
  int get hashCode => Object.hash(callingCode, number);
}

/// Splits [stored] against the [callingCodes] the country list supplies.
///
/// LONGEST MATCH WINS, which is why this is not a one-liner: `+1` and `+1876`
/// are both real calling codes, so scanning shortest-first would take `+1` off
/// a Jamaican number and leave `876…` as the subscriber digits.
///
/// When nothing matches, the whole normalised string comes back as
/// [PhoneParts.number] with an empty code — shown as-is rather than truncated.
PhoneParts splitPhone(String? stored, Iterable<String> callingCodes) {
  final value = normalizePhone(stored ?? '');
  if (value.isEmpty) {
    return const PhoneParts(callingCode: '', number: '');
  }

  // Saudi local form. Checked before the general path: "05…" carries no "+",
  // so no calling code could ever match it, and it is the most common value in
  // this dataset by a wide margin.
  if (RegExp(r'^05\d{8}$').hasMatch(value)) {
    return PhoneParts(callingCode: '+966', number: value.substring(1));
  }

  if (!value.startsWith('+')) {
    return PhoneParts(callingCode: '', number: value);
  }

  final byLongest = callingCodes
      .where((code) => code.trim().isNotEmpty)
      .map((code) => code.trim())
      .toList()
    ..sort((a, b) => b.length.compareTo(a.length));

  for (final code in byLongest) {
    if (value.startsWith(code) && value.length > code.length) {
      return PhoneParts(
        callingCode: code,
        number: value.substring(code.length),
      );
    }
  }

  return PhoneParts(callingCode: '', number: value);
}

/// Puts a calling code and subscriber digits back together as E.164.
///
/// Returns empty when there is no number, so an untouched field submits null
/// rather than a bare calling code — "+966" alone is not a phone number, and
/// the server would reject it with a shape error that reads like the visitor
/// typed something wrong.
///
/// A leading zero on the subscriber part is dropped: people write their number
/// the local way (`0501234567`) even after choosing a calling code, and
/// `+9660501234567` is not a valid number.
String composePhone(String callingCode, String number) {
  final digits = normalizePhone(number).replaceAll('+', '');
  final trimmedDigits = digits.startsWith('0') ? digits.substring(1) : digits;
  if (trimmedDigits.isEmpty) {
    return '';
  }
  final code = callingCode.trim();
  if (code.isEmpty) {
    // No code chosen: the number is already whole, or the visitor is mid-edit.
    return normalizePhone(number);
  }
  return '${code.startsWith('+') ? code : '+$code'}$trimmedDigits';
}
