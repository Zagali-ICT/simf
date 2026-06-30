/// Full-name shape rules — the client mirror of
/// `UpsertUserProfileRequestValidator`:
/// the Arabic name is Arabic letters only, the English name Latin letters only,
/// and a "full name" is 2 to 4 whitespace-separated parts (in that one script).
/// Pure predicates only — the screen owns the per-script l10n messages and the
/// order the checks run in.
library;

/// Arabic letters + whitespace only (the whole string).
final RegExp arabicNameLettersOnly = RegExp(r'^[ء-ي\s]+$');

/// Latin letters + whitespace only (the whole string).
final RegExp englishNameLettersOnly = RegExp(r'^[A-Za-z\s]+$');

/// True when [value] is made up only of letters from [lettersOnly] (Arabic or
/// Latin) plus whitespace. The caller trims before calling.
bool isNameLettersOnly(String value, RegExp lettersOnly) =>
    lettersOnly.hasMatch(value);

/// True when [value] is 2 to 4 whitespace-separated parts. The caller trims and
/// has already confirmed the value is non-empty + letters-only.
bool hasFullNameParts(String value) {
  final parts = value.split(RegExp(r'\s+')).length;
  return parts >= 2 && parts <= 4;
}
