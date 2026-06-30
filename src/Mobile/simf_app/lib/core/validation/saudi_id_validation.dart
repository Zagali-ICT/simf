/// Saudi identity-document shape rules — the client mirror of the server checks
/// for the national ID, the Iqama, and the passport (instant feedback only; the
/// server re-validates, D-197). Pure predicates only — the screen owns the l10n
/// messages and the order the checks run in.
library;

final RegExp _nationalIdShape = RegExp(r'^1\d{9}$');
final RegExp _iqamaShape = RegExp(r'^2\d{9}$');
final RegExp _passportShape = RegExp(r'^[A-Za-z0-9]{6,9}$');

/// Standard Luhn mod-10 over a digits-only string. Any non-digit character
/// makes it false. Mirrors the server check for the Saudi national id / Iqama.
bool isValidLuhn(String number) {
  var sum = 0;
  var doubleDigit = false;
  for (var i = number.length - 1; i >= 0; i--) {
    final code = number.codeUnitAt(i);
    if (code < 0x30 || code > 0x39) {
      return false;
    }
    var digit = code - 0x30;
    if (doubleDigit) {
      digit *= 2;
      if (digit > 9) {
        digit -= 9;
      }
    }
    sum += digit;
    doubleDigit = !doubleDigit;
  }
  return sum % 10 == 0;
}

/// True when [value] is a valid Saudi national id: `1` + 9 digits + a passing
/// Luhn check. The caller trims before calling.
bool isValidNationalId(String value) =>
    _nationalIdShape.hasMatch(value) && isValidLuhn(value);

/// True when [value] is a valid Iqama number: `2` + 9 digits + a passing Luhn
/// check. The caller trims before calling.
bool isValidIqama(String value) =>
    _iqamaShape.hasMatch(value) && isValidLuhn(value);

/// True when [value] is a valid passport number: 6 to 9 alphanumeric
/// characters. The caller trims before calling.
bool isValidPassport(String value) => _passportShape.hasMatch(value);
