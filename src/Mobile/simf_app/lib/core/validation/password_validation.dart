/// Password policy rule — the client-side mirror of the server policy
/// (≥ 8 chars + at least one letter + at least one digit; SIMF-MOB-API-001),
/// for instant feedback only. The server re-validates.
library;

final RegExp _letter = RegExp('[A-Za-z]');
final RegExp _digit = RegExp(r'\d');

/// True when [value] satisfies the policy: at least 8 characters, containing at
/// least one Latin letter and at least one digit.
bool isValidPassword(String value) =>
    value.length >= 8 && _letter.hasMatch(value) && _digit.hasMatch(value);
